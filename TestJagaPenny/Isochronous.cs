//#define IS_BENCHMARK_DEVICE

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using LibUsbDotNet;
using LibUsbDotNet.Info;
using LibUsbDotNet.Main;

using System;
using MonoLibUsb;
using MonoLibUsb.Profile;
using Usb = MonoLibUsb.MonoUsbApi;

namespace TestJagaPenny
{
    internal class ReadIsochronous
    {

        /// <summary>Use the first read endpoint</summary>
        public static readonly byte TRANSFER_ENDPOINT = UsbConstants.ENDPOINT_DIR_MASK;

        /// <summary>Number of transfers to sumbit before waiting begins</summary>
        public static readonly int TRANSFER_MAX_OUTSTANDING_IO = 20;

        /// <summary>Size of each transfer</summary>
        public static int TRANSFER_SIZE;

        private static DateTime mStartTime = DateTime.MinValue;

        /// <summary>
        /// Use this to calculate transfer rate
        /// </summary>
        private static double mTotalPacketsHandled = 0.0;
        private static int transfersCompleted = 0;

        static RichTextBox rtb;
        static TextBox rtbStatus;

        private delegate void ShowTransferDelegate(UsbTransferQueue.Handle h, int i, bool showTrailingZeros);

        public class TransferParams
        {
            public bool showErroneous;
            public bool showHeaders;
            public bool showLargeGapsOnly;
        }

        public static void StartIsochronous(RichTextBox _rtb, TextBox _tbStatus, TransferParams tParams)
        {
            try
            {
                new Thread(() =>
                    {
                        StartIsochronous2(_rtb, _tbStatus, tParams);
                    }).Start();
            }
            catch (Exception ex)
            {
            }
        }

        private static void StartIsochronous2(RichTextBox _rtb, TextBox _tbStatus, TransferParams tParams)
        {
            rtb = _rtb;

            rtbStatus = _tbStatus;

            ErrorCode ec = ErrorCode.None;

            UsbDevice MyUsbDevice = null;

            int interfaceID = 0;

            Program.quitFlag = false;

            try
            {

                UsbEndpointReader reader = GetReader(out MyUsbDevice, out interfaceID);

                int maxPacketSize = reader.EndpointInfo.Descriptor.MaxPacketSize;

                TRANSFER_SIZE = TRANSFER_MAX_OUTSTANDING_IO * maxPacketSize; //usbEndpointInfo.Descriptor.MaxPacketSize

                UsbTransferQueue transferQueue = new UsbTransferQueue(reader,
                                                                     TRANSFER_MAX_OUTSTANDING_IO,    // # of buffers allocated
                                                                     maxPacketSize,      // Transfer size
                                                                     5000,              // Timeout, milliseconds
                                                                     0);//If you specify zero, this will revert to usbEndpointInfo.Descriptor.MaxPacketSize);

                transfersCompleted = 0;
                transfersReceived = 0;
                mTotalPacketsHandled = 0;
                prevPacketCounter = -1;
                old_remnant = null;

                UsbTransferQueue.Handle handle;

                mStartTime = DateTime.Now;

                sb.Clear();
                sbPreviousTransfer.Clear();

                while (!Program.quitFlag)
                {
                    // Begin submitting transfers until TRANFER_MAX_OUTSTANDING_IO has been reached.
                    // then wait for the oldest outstanding transfer to complete.
                    ec = transferQueue.Transfer(out handle);

                    if (transfersCompleted == 0)
                        SafeAppendText(rtb, "Samples per transfer: " + handle.Context.IsoPacketSize + "\r\n\r\n");

                    if (ec != ErrorCode.Success)
                    {
                        // Send any residual text to screen.
                        SafeAppendText(rtb, sb.ToString());
                        sb.Clear();
                        throw new Exception("Failed to obtain data from JAGA Penny device");
                    }

                    // Show some information on the completed transfer.
                    transfersCompleted++;

                    showTransfer(handle, transfersCompleted, tParams);

                    // Count # packets received.
                    rtbStatus.BeginInvoke((MethodInvoker)delegate { rtbStatus.Text = mTotalPacketsHandled.ToString(); });

                    // Write string that was obtained from showTransfer()
                    if (sb.Length > 2000)
                    {
                        SafeAppendText(rtb, sb.ToString());
                        sb.Clear();
                    }

                    Application.DoEvents();
                }

                // Cancels any oustanding transfers and frees the transfer queue handles.
                // NOTE: A transfer queue can be reused after it's freed.
                transferQueue.Free();

                SafeAppendText(rtb, "\r\nDone!\r\n");
            }
            catch (Exception ex)
            {
                SafeAppendText(rtb, "\r\n" + (ec != ErrorCode.None ? ec + ":" : String.Empty) + ex.Message + " : " + System.Runtime.InteropServices.Marshal.GetLastWin32Error() + "\r\n");
            }
            finally
            {
                // If things crash, then some internal data structures in MyUsbDevice will already be disposed, and Close()
                // will throw exception. Just ignore.

                try
                {
                    if (MyUsbDevice != null)
                    {
                        if (MyUsbDevice.IsOpen)
                        {
                            // If this is a "whole" usb device (libusb-win32, linux libusb-1.0)
                            // it exposes an IUsbDevice interface. If not (WinUSB) the 
                            // 'wholeUsbDevice' variable will be null indicating this is 
                            // an interface of a device; it does not require or support 
                            // configuration and interface selection.
                            IUsbDevice wholeUsbDevice = MyUsbDevice as IUsbDevice;
                            if (!ReferenceEquals(wholeUsbDevice, null))
                            {
                                // Release interface #0.
                                wholeUsbDevice.ReleaseInterface(interfaceID);
                            }

                            MyUsbDevice.Close();
                        }
                        MyUsbDevice = null;
                    }
                }
                catch (Exception ex)
                {
                }

                // Free usb resources
                UsbDevice.Exit();

                prevPacketCounter = -1;
            }
        }

        private static void SafeAppendText(RichTextBox rtb, string s)
        {
            rtb.BeginInvoke((MethodInvoker)delegate { rtb.AppendText(s); });
        }

        /// <summary>
        /// Get Endpoint reader
        /// </summary>
        /// <returns></returns>
        private static UsbEndpointReader GetReader(out UsbDevice MyUsbDevice, out int interfaceID)
        {
            UsbDeviceFinder MyUsbFinder = new UsbDeviceFinder(0x1915, 0x7B);

            // Find and open the usb device.
            UsbRegDeviceList regList = UsbDevice.AllDevices.FindAll(MyUsbFinder);

            if (regList.Count == 0)
                throw new Exception("No receiver devices found.\r\n");

            UsbInterfaceInfo usbInterfaceInfo = null;
            UsbEndpointInfo usbEndpointInfo = null;

            MyUsbDevice = null;

            // Look through all conected devices with this vid and pid until
            // one is found that has and and endpoint that matches TRANFER_ENDPOINT.
            // 
            foreach (UsbRegistry regDevice in regList)
            {
                if (regDevice.Open(out MyUsbDevice))
                {
                    if (MyUsbDevice.Configs.Count > 0)
                    {
                        // if TRANFER_ENDPOINT is 0x80 or 0x00, LookupEndpointInfo will return the 
                        // first read or write (respectively).
                        if (UsbEndpointBase.LookupEndpointInfo(MyUsbDevice.Configs[0], 0x88,//TRANSFER_ENDPOINT, 
                            out usbInterfaceInfo, out usbEndpointInfo))
                            break;

                        MyUsbDevice.Close();
                        MyUsbDevice = null;
                    }
                }
            }

            // If the device is open and ready
            if (MyUsbDevice == null)
                throw new Exception("Receiver device was found, but did not have the correct endpoint address.\r\n");

            // If this is a "whole" usb device (libusb-win32, linux libusb-1.0)
            // it exposes an IUsbDevice interface. If not (WinUSB) the 
            // 'wholeUsbDevice' variable will be null indicating this is 
            // an interface of a device; it does not require or support 
            // configuration and interface selection.
            IUsbDevice wholeUsbDevice = MyUsbDevice as IUsbDevice;
            if (!ReferenceEquals(wholeUsbDevice, null))
            {
                // This is a "whole" USB device. Before it can be used, 
                // the desired configuration and interface must be selected.

                // Select config #1
                wholeUsbDevice.SetConfiguration(1);

                wholeUsbDevice.ClaimInterface(usbInterfaceInfo.Descriptor.InterfaceID);
            }
            else
                throw new Exception("Could not find Penny receiver device");

            interfaceID = usbInterfaceInfo.Descriptor.InterfaceID;

            // open read endpoint.
            UsbEndpointReader reader = MyUsbDevice.OpenEndpointReader(
                (ReadEndpointID)usbEndpointInfo.Descriptor.EndpointID,
                0,
                (EndpointType)(usbEndpointInfo.Descriptor.Attributes & 0x3));

            if (ReferenceEquals(reader, null))
            {
                throw new Exception("Failed to locate read endpoint for JAGA Penny receiver.\r\n");
            }

            reader.Reset();

            return reader;
        }

        public static void Stop()
        {
            Program.quitFlag = true;
        }

        static StringBuilder sb = new StringBuilder();
        static StringBuilder sbPreviousTransfer = new StringBuilder();

        static int transfersReceived = 0;
        static int prevPacketCounter = -1;

        static byte[] old_remnant, new_remnant;

        private static void showTransfer(UsbTransferQueue.Handle handle, int transferIndex, TransferParams tParams)
        {
            double samplesPerSecond = 0;

            int numBytesTransferred = handle.Data.Length;

            int maxTransferSize = handle.Context.IsoPacketSize;
            int x = handle.Context.Transmitted;

            int packetCounter = 0;

            int[][] jagaPacketData;

            AlignTransfersIntoJagaPackets(handle.Data, old_remnant, out jagaPacketData, out new_remnant, handle.Transferred, maxTransferSize != 160);

            old_remnant = new_remnant;

            StringBuilder sbCurrentTransfer = new StringBuilder();

            bool hasHeader = MatchHeader2(handle.Data, 0);
            bool hasErroneousPacket = false;

            sbCurrentTransfer.Append("\r\n");

            // Show transfer # at beginning of each line
            sbCurrentTransfer.Append((transfersReceived++).ToString() + "\t");

            for (int j = 0; j < handle.Transferred; j++)
            {
                sbCurrentTransfer.Append(String.Format(" {0:X2}", handle.Data[j]));
            }

            sbCurrentTransfer.Append(string.Format("\t:{0} bytes\r\n", handle.Transferred));

            for (int i = 0; i < jagaPacketData.Length; i++)
            {
                if (jagaPacketData[i] == null)
                    // The # of packets is not deterministic. Once we hit nulls, we are done.
                    break;

                packetCounter = 0;
                sbCurrentTransfer.Append("\r\n" + mTotalPacketsHandled.ToString() + "\t");

                int numSamples = jagaPacketData[i].Length;

                for (int j = 0; j < numSamples; j++)
                {
                    // Calculate packet counter from LSB
                    int val = jagaPacketData[i][j];
                    packetCounter = packetCounter * 2 + (val & 1);

                    if (j % 4 == 0)
                        sbCurrentTransfer.Append("  ");

                    sbCurrentTransfer.Append(String.Format(" {0:X4}", val));
                }

                mTotalPacketsHandled++;

                // Append packet counter
                sbCurrentTransfer.Append(String.Format("\t:{0:X4}", packetCounter));

                if (prevPacketCounter >= 0)
                {
                    int gap = packetCounter - prevPacketCounter;
                    sbCurrentTransfer.Append(String.Format("\tGap {0}", gap));

                    if (gap != 1)
                    {
                        if ((gap < 0 || gap > 2) || !tParams.showLargeGapsOnly)
                        {
                            hasErroneousPacket = true;
                            sbCurrentTransfer.Append(" ******************** ");
                        }
                    }
                }

                prevPacketCounter = packetCounter;

            }

            sbCurrentTransfer.Append("\r\n");

            samplesPerSecond = mTotalPacketsHandled / (DateTime.Now - mStartTime).TotalSeconds;

            sbCurrentTransfer.Append(String.Format("#{0} complete. {1} samples/sec ({2} bytes)\r\n\r\n",
                            transferIndex,
                            Math.Round(samplesPerSecond, 2),
                            handle.Transferred));

            bool showThis;

            if (!tParams.showErroneous && !tParams.showHeaders)
                showThis = true;
            else
                showThis = (tParams.showErroneous && hasErroneousPacket) || (tParams.showHeaders && hasHeader);

            if (!showThis)
                goto Done;

            if (tParams.showErroneous && hasErroneousPacket)
                // Show previous transfer too, because it will help us understand error.
                sb.Append(sbPreviousTransfer);

            // Append to ongoing string that will eventually get sent to screen
            sb.Append(sbCurrentTransfer);

Done:
            // Save current text so that if we are showing only anomalous packets, then next time we can show both current and previous packets (since anomalous packets usually arise from
            // irregularities involving a packet spanning two transfers.
            sbPreviousTransfer.Clear();
            sbPreviousTransfer.Append(sbCurrentTransfer);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="data">Data from most recent USB transfer</param>
        /// <param name="old_remnant">This would be any unprocessed portion of the end of any previous transfers.</param>
        /// <param name="jagaPacketData">Output data, reorganized into single samples</param>
        /// <param name="new_remnant">Any unprocessed portion at the end of the current transfer</param>
        /// <param name="usbTransferSize">It is assumed that "data" consists of several consecutive transfers, e.g. 16 transfers of 102 bytes each. This is the size (102) of each individual transfer.</param>
        static void AlignTransfersIntoJagaPackets(byte[] data, byte[] old_remnant, out int[][] jagaPacketData, out byte[] new_remnant, int transferred, bool IsNewVersion)
        {
            int dataWordsPerJagaPacket = 16;

            int headerSize;

            if (IsNewVersion)
            {
                // New version has header
                headerSize = 4;
                if (old_remnant != null && old_remnant.Length > 0)
                {
                    // Append old and new transfers together.
                    byte[] newData = new byte[old_remnant.Length + data.Length];

                    int frame1 = CheckTransfer(old_remnant.Length, old_remnant);
                    int frame2 = CheckTransfer(transferred, data);

                    bool useRemnant = true;

                    if (frame1 >= 0 && frame2 >= 0)
                    {
                        int interval = frame2 + old_remnant.Length - frame1;

                        if (interval != headerSize + dataWordsPerJagaPacket * 2)
                            // Something is wrong. Just discard remnant.
                            useRemnant = false;
                    }

                    if (useRemnant)
                    {
                        Array.Copy(old_remnant, newData, old_remnant.Length);
                        Array.Copy(data, 0, newData, old_remnant.Length, data.Length);

                        data = newData;
                        transferred = old_remnant.Length + transferred;
                    }
                }
            }
            else
                // Old version has no header, and no possibility of packets bridging across frames.
                headerSize = 0;

            int bytesPerJagaPacketWithHeader = dataWordsPerJagaPacket * 2 + headerSize;

            int frameStart;

            if (IsNewVersion)
                frameStart = CheckTransfer(transferred - 1, data);
            else
                // Old version. Has max transfer size = 160, but only first 32 bytes are meaningful.
                frameStart = 0;

            int[][] sampleDataTmp = new int[5][];

            int currentPacket = 0;

            new_remnant = null;

            int bytesRemaining = 0;

            byte[] tmpPacket = new byte[bytesPerJagaPacketWithHeader];

            while (frameStart >= 0)
            {
                bytesRemaining = transferred - frameStart;

                if (bytesRemaining < bytesPerJagaPacketWithHeader)
                {
                    // Possible partial packet. Make sure we didn't just miss a zero.

                    // We have a partial JAGA packet. Must combine with next transfer's data.
                    break;
                }

                if (IsNewVersion && !MatchHeader(data, frameStart))
                {
                    // Although first frame is guaranteed to match, the next ones might not, since they haven't been explicitly checked.
                    // Actually, there is a chance that there is another frame here, but that it simply is offset. This is NOT LIKELY, so we don't check for it.
                    bytesRemaining = 0;
                    break;
                }

                sampleDataTmp[currentPacket] = new int[dataWordsPerJagaPacket];

                ByteToWordCopy(data, sampleDataTmp[currentPacket], frameStart + headerSize, 0, dataWordsPerJagaPacket);

                frameStart += bytesPerJagaPacketWithHeader;
                currentPacket++;
            }

            if (bytesRemaining > 0)
            {
                // We have a partial frame remaining
                // Store partial frame as remnant, so it can be combined with data from next transfer.
                new_remnant = new byte[bytesRemaining];
                Array.Copy(data, frameStart, new_remnant, 0, bytesRemaining);
            }

            if (bytesRemaining < 0)
                throw new Exception("Please contact program developer - Isochronous data transfer error #1");

            jagaPacketData = sampleDataTmp;
        }

        /// <summary>
        /// Find frame boundary ('jaga' header) and actual length of each transfer
        /// </summary>
        /// <param name="startIndex"></param>
        /// <param name="length"></param>
        /// <param name="data"></param>
        /// <param name="actualLengthInBytes"></param>
        /// <param name="frameBoundaryInBytes"></param>
        static int CheckTransfer(int length, byte[] data)
        {
            int frameBoundaryInBytes = -1;
            for (int j = 0; j <= length; j++)
            {
                if (MatchHeader(data, j))
                {
                    // Identify where 'jaga' header is located.
                    frameBoundaryInBytes = j;
                    break;
                }
            }

            return frameBoundaryInBytes;
        }

        static void ByteToWordCopy(byte[] src, int[] dest, int indexSrc, int indexDest, int bytePairsToCopy)
        {
            // Src data is big-endian.
            for (int i = 0; i < bytePairsToCopy; i++)
            {
                dest[indexDest++] = (src[indexSrc++] << 8) + (src[indexSrc++]);
            }
        }


        static bool MatchHeader(byte[] data, int index)
        {
            if (data.Length < index + 4)
                return false;

            if (data[index] != 'j')
                return false;

            if (data[++index] != 'a')
                return false;

            if (data[++index] != 'g')
                return false;

            return (data[++index] == 'a');
        }

        static bool MatchHeader2(byte[] data, int index)
        {
            if (data.Length < index + 4)
                return false;

            if (data[index] != 'J')
                return false;

            if (data[++index] != 'A')
                return false;

            if (data[++index] != 'G')
                return false;

            return (data[++index] == 'A');
        }

    }
}