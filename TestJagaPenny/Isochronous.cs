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
        public static readonly int TRANSFER_MAX_OUTSTANDING_IO = 16;

        /// <summary>Size of each transfer</summary>
        public static int TRANSFER_SIZE;

        private static DateTime mStartTime = DateTime.MinValue;

        /// <summary>
        /// Use this to calculate transfer rate
        /// </summary>
        private static double mTotalSamplesHandled = 0.0;
        private static int transfersCompleted = 0;

        static RichTextBox rtb;
        static TextBox rtbStatus;

        private delegate void ShowTransferDelegate(UsbTransferQueue.Handle h, int i, bool showTrailingZeros);

        public static void StartIsochronous(RichTextBox _rtb, TextBox _tbStatus, bool showTrailingZeros)
        {
            try
            {
                new Thread(() =>
                    {
                        StartIsochronous2(_rtb, _tbStatus, showTrailingZeros);
                    }).Start();
            }
            catch (Exception ex)
            {
            }
        }

        private static void StartIsochronous2(RichTextBox _rtb, TextBox _tbStatus, bool showTrailingZeros)
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
                                                                     TRANSFER_SIZE,      // Transfer size
                                                                     5000,              // Timeout, milliseconds
                                                                     maxPacketSize);//usbEndpointInfo.Descriptor.MaxPacketSize);

                transfersCompleted = 0;
                samplesDisplayed = 0;
                mTotalSamplesHandled = 0;
                old_remnant = null;

                UsbTransferQueue.Handle handle;

                mStartTime = DateTime.Now;

                while (!Program.quitFlag)
                {
                    // Begin submitting transfers until TRANFER_MAX_OUTSTANDING_IO has been reached.
                    // then wait for the oldest outstanding transfer to complete.
                    ec = transferQueue.Transfer(out handle);

                    if (ec != ErrorCode.Success)
                        throw new Exception("Failed to obtain data from JAGA Penny device\r\n");

                    // Show some information on the completed transfer.
                    transfersCompleted++;

                    if (rtb.InvokeRequired)
                        rtb.BeginInvoke((ShowTransferDelegate) showTransfer, handle, transfersCompleted, showTrailingZeros);
                    else
                        showTransfer(handle, transfersCompleted, showTrailingZeros);

                    Application.DoEvents();
                }

                prevPacketCounter = -1;

                // Cancels any oustanding transfers and frees the transfer queue handles.
                // NOTE: A transfer queue can be reused after it's freed.
                transferQueue.Free();

                rtb.BeginInvoke((MethodInvoker)delegate { rtb.AppendText("\r\nDone!\r\n"); });
            }
            catch (Exception ex)
            {
                rtb.BeginInvoke((MethodInvoker)delegate { rtb.AppendText("\r\n" + (ec != ErrorCode.None ? ec + ":" : String.Empty) + ex.Message); });
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
            }
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
        static int samplesDisplayed = 0;
        static int prevPacketCounter = -1;

        static byte[] old_remnant, new_remnant;

        private static void showTransfer(UsbTransferQueue.Handle handle, int transferIndex, bool showTrailingZeros)
        {
            double samplesPerSecond = 0;
            sb.Clear();

            int numBytesTransferred = handle.Data.Length;

            int transferSize = handle.Context.IsoPacketSize;
            int x = handle.Context.Transmitted;

            int packetCounter = 0;

            if (showTrailingZeros)
            {
                for (int i = 0; i < numBytesTransferred; i++)
                {
                    if (i % transferSize == 0)
                    {
                        // Show sample # at beginning of each line
                        sb.Append("\r\n" + (samplesDisplayed++).ToString() + "\t");
                    }

                    sb.Append(String.Format(" {0:X2}", handle.Data[i]));
                }

                sb.Append("\r\n");
            }
            else
            {
                int numFrames = numBytesTransferred / transferSize;
                sb.Append("\r\n");
                for (int i = 0; i < numFrames; i++)
                {
                    // Show sample # at beginning of each line
                    sb.Append((samplesDisplayed++).ToString() + "\t");

                    int actualSize = transferSize;
                    int index = i * transferSize;
                    for (int j = transferSize - 1; j >= 0; j--)
                    {
                        if (handle.Data[index + j] > 0)
                            break;

                        actualSize--;
                    }

                    for (int j = 0; j < actualSize; j++)
                        sb.Append(String.Format(" {0:X2}", handle.Data[index + j]));

                    sb.Append(string.Format("\t:{0}\r\n", actualSize));
                }
            }

            int[][] jagaPacketData;

            AlignTransfersIntoJagaPackets(handle.Data, old_remnant, out jagaPacketData, out new_remnant, transferSize);

            old_remnant = new_remnant;

            for (int i = 0; i < jagaPacketData.Length; i++)
            {
                if (jagaPacketData[i] == null)
                    // The # of packets is not deterministic. Once we hit nulls, we are done.
                    break;

                packetCounter = 0;
                sb.Append("\r\n" + i.ToString() + "\t");

                int numSamples = jagaPacketData[i].Length;

                for (int j = 0; j < numSamples; j++)
                {
                    // Calculate packet counter from LSB
                    int val = jagaPacketData[i][j];
                    packetCounter = packetCounter * 2 + (val & 1);

                    sb.Append(String.Format(" {0:X4}", val));
                }

                mTotalSamplesHandled += numSamples;

                // Append packet counter
                sb.Append(String.Format("\t:{0:X4}", packetCounter));

                if (prevPacketCounter >= 0)
                    sb.Append(String.Format("\tGap {0}", packetCounter - prevPacketCounter));

                prevPacketCounter = packetCounter;

            }

            sb.Append("\r\n");

            samplesPerSecond = mTotalSamplesHandled / (DateTime.Now - mStartTime).TotalSeconds;

            // Count # samples received.
            rtbStatus.Text = mTotalSamplesHandled.ToString();

            sb.Append(String.Format("#{0} complete. {1} samples/sec ({2} bytes)\r\n\r\n",
                            transferIndex,
                            Math.Round(samplesPerSecond, 2),
                            handle.Transferred));

            rtb.AppendText(sb.ToString());
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="data">Data from most recent USB transfer</param>
        /// <param name="old_remnant">This would be any unprocessed portion of the end of any previous transfers.</param>
        /// <param name="jagaPacketData">Output data, reorganized into single samples</param>
        /// <param name="new_remnant">Any unprocessed portion at the end of the current transfer</param>
        /// <param name="usbTransferSize">It is assumed that "data" consists of several consecutive transfers, e.g. 16 transfers of 102 bytes each. This is the size (102) of each individual transfer.</param>
        static void AlignTransfersIntoJagaPackets(byte[] data, byte[] old_remnant, out int[][] jagaPacketData, out byte[] new_remnant, int usbTransferSize)
        {
            int numNewTransfers = data.Length / usbTransferSize;
            int numOldTransfers = 0;

            int dataWordsPerJagaPacket = 16;

            bool IsNewVersion = (usbTransferSize != 160);

            int headerSize;

            if (IsNewVersion)
            {
                // New version has header
                headerSize = 4;
                if (old_remnant != null && old_remnant.Length > 0)
                {
                    // Append old and new transfers together.
                    numOldTransfers = 1;
                }
            }
            else
                // Old version has no header, and no possibility of packets bridging across frames.
                headerSize = 0;

            int bytesPerJagaPacketWithHeader = dataWordsPerJagaPacket * 2 + headerSize;

            int[] actualLength = new int[numNewTransfers + numOldTransfers];
            int[] frameStart = new int[numNewTransfers + numOldTransfers];

            if (numOldTransfers > 0)
                CheckTransfer(0, old_remnant.Length - 1, old_remnant, out actualLength[0], out frameStart[0]);

            for (int i = 0; i < numNewTransfers; i++)
            {
                int startIndex = i * usbTransferSize;
                int endIndex = startIndex + usbTransferSize - 1;

                if (IsNewVersion)
                    CheckTransfer(startIndex, endIndex, data, out actualLength[i + numOldTransfers], out frameStart[i + numOldTransfers]);
                else
                {
                    // Old version. Has max transfer size = 160, but only first 32 bytes are meaningful.
                    actualLength[i] = 32;
                    frameStart[i] = usbTransferSize * i;
                }
            }

            int[][] sampleDataTmp = new int[64][];
            int currentTransfer = 0;
            int currentSample = 0;

            new_remnant = null;

            int bytesRemaining;

            byte[] tmpPacket = new byte[bytesPerJagaPacketWithHeader];

            if (numOldTransfers > 0)
            {
                while ((bytesRemaining = actualLength[0] - frameStart[0]) > bytesPerJagaPacketWithHeader)
                {
                    ByteToWordCopy(old_remnant, sampleDataTmp[currentSample], frameStart[0] + headerSize, 0, dataWordsPerJagaPacket);

                    frameStart[0] += bytesPerJagaPacketWithHeader;
                    currentSample++;
                }

                if (bytesRemaining == 0)
                {
                    // We have used up all data in this transfer. Go on to the next one.
                    currentTransfer++;
                }
                else if (bytesRemaining < 0)
                {
                    throw new Exception("Please contact program developer - Isochronous data transfer error #1");
                }
                else
                {
                    // We have a partial frame remaining
                    // Figure out how many bytes are in the next transfer
                    int bytesInNextFrame;
                    bytesInNextFrame = frameStart[1];
                    int mismatch = bytesInNextFrame + bytesRemaining - bytesPerJagaPacketWithHeader;
                    if (mismatch < 0)
                    {
                        // Anomalous frame, has insufficient # of bytes
                        // Assume that we've underestimated size of first frame, by falsely assuming that trailing
                        // zeros were padding
                        if (mismatch > -10)
                        {
                            // If shortfall is less than 10, then just assume trailing zeros are part of the first packet portion, and hope for the best.
                            bytesRemaining = bytesPerJagaPacketWithHeader - bytesInNextFrame;
                            Array.Resize<byte>(ref old_remnant, bytesRemaining);
                        }
//                        else
//                            throw new Exception("Please contact program developer - Isochronous data transfer error #3");
                    }
                    else if (mismatch > 0)
                    {
                        // Have excess. Probably due to a lost header, causing two packets to appear as one mega-packet
//                        throw new Exception("Please contact program developer - Isochronous data transfer error #2");
                    }

                    if (mismatch == 0)
                    {
                        // Frame is spread over two transfers
                        int wordsRemaining = bytesRemaining >> 1;
                        sampleDataTmp[currentSample] = new int[dataWordsPerJagaPacket];

                        Array.Copy(old_remnant, frameStart[0], tmpPacket, 0, bytesRemaining);
                        Array.Copy(data,        0,             tmpPacket, bytesRemaining, (bytesPerJagaPacketWithHeader - bytesRemaining));

                        ByteToWordCopy(tmpPacket, sampleDataTmp[currentSample], 4, 0, dataWordsPerJagaPacket);
                        currentSample++;
                    }

                    currentTransfer++;
                }
            }

            while(currentTransfer < numNewTransfers + numOldTransfers)
            {
                int newTransferIndex = currentTransfer - numOldTransfers;   // Index into new data
                int transferBoundary1 = newTransferIndex * usbTransferSize; // Start of current transfer boundary

                if (frameStart[newTransferIndex] < 0)
                {
                    // No frame was detected in this transfer. Dropped packets?
                    currentTransfer++;
                    continue;
                }

                while (true)
                {
                    bytesRemaining = actualLength[currentTransfer] - (frameStart[currentTransfer] - transferBoundary1);

                    if (bytesRemaining < bytesPerJagaPacketWithHeader)
                    {
                        // Possible partial packet. Make sure we didn't just miss a zero.
                        
                        // We have a partial JAGA packet. Must combine with next transfer's data.
                        break;
                    }
                    sampleDataTmp[currentSample] = new int[dataWordsPerJagaPacket];

                    ByteToWordCopy(data, sampleDataTmp[currentSample], frameStart[currentTransfer] + headerSize, 0, dataWordsPerJagaPacket);

                    frameStart[currentTransfer] += bytesPerJagaPacketWithHeader;
                    currentSample++;
                }

                if (bytesRemaining == 0)
                {
                    // We have used up all data in this transfer. Go on to the next one.
                    currentTransfer++;
                    continue;
                }

                if (bytesRemaining < 0)
                {
                    throw new Exception("Please contact program developer - Isochronous data transfer error #1");
                }

                // We have a partial frame remaining
                if (newTransferIndex + 1 >= numNewTransfers)
                {
                    // We are at the end. Store partial frame as remnant, so it can be combined with data from next transfer.
                    new_remnant = new byte[bytesRemaining];
                    for (int i = 0; i < bytesRemaining; i++)
                        new_remnant[i] = data[frameStart[currentTransfer] + i];

                    break;
                }
                else
                {
                    // Frame is spread over two transfers. Merge them together.
                    int transferBoundary2 = (newTransferIndex + 1) * usbTransferSize;

                    // Figure out how many bytes are in the next transfer
                    int bytesInNextFrame;
                    if (frameStart[currentTransfer + 1] > 0)
                    {
                        bytesInNextFrame = frameStart[currentTransfer + 1] - transferBoundary2;
                        int mismatch = bytesInNextFrame + bytesRemaining - bytesPerJagaPacketWithHeader;

                        if (mismatch < 0)
                        {
                            // Anomalous frame, has insufficient # of bytes
                            // Assume that we've underestimated size of first frame, by falsely assuming that trailing
                            // zeros were padding
                            if (mismatch > -10)
                                // If shortfall is less than 10, then just assume trailing zeros are part of the first packet portion, and hope for the best.
                                bytesRemaining = bytesPerJagaPacketWithHeader - bytesInNextFrame;
                            else
                            {
                                // Large shortfall, probably indicating communication error.
                                currentTransfer++;
                                continue;
//                                throw new Exception("Please contact program developer - Isochronous data transfer error #4");
                            }
                        }
                        else if (mismatch > 0)
                        {
                            // Have excess. Probably due to a lost header, causing two packets to appear as one mega-packet
                            currentTransfer++;
                            continue;
//                            throw new Exception("Please contact program developer - Isochronous data transfer error #5");
                        }
                    }
                    else
                    {
                        // Well, drat, the next transfer has no detected frames. We'll end up with a packet padded with zeros.
                        // Ignore this problem for now.
                    }

                    int wordsRemaining = bytesRemaining >> 1;
                    sampleDataTmp[currentSample] = new int[dataWordsPerJagaPacket];

                    Array.Copy(data, frameStart[currentTransfer], tmpPacket, 0, bytesRemaining);
                    Array.Copy(data, transferBoundary2,           tmpPacket, bytesRemaining, (bytesPerJagaPacketWithHeader - bytesRemaining));

                    ByteToWordCopy(tmpPacket, sampleDataTmp[currentSample], 4, 0, dataWordsPerJagaPacket);

                    currentSample++;
                    currentTransfer++;
                }
            }

            jagaPacketData = sampleDataTmp;
        }

        static void ByteToWordCopy(byte[] src, int[] dest, int indexSrc, int indexDest, int bytePairsToCopy)
        {
            // Src data is big-endian.
            for (int i = 0; i < bytePairsToCopy; i++)
            {
                dest[indexDest++] = (src[indexSrc++] << 8) + (src[indexSrc++]);
            }
        }

        /// <summary>
        /// Find frame boundary ('jaga' header) and actual length of each transfer
        /// </summary>
        /// <param name="startIndex"></param>
        /// <param name="endIndex"></param>
        /// <param name="data"></param>
        /// <param name="actualLengthInBytes"></param>
        /// <param name="frameBoundaryInBytes"></param>
        static void CheckTransfer(int startIndex, int endIndex, byte[] data, out int actualLengthInBytes, out int frameBoundaryInBytes)
        {
            actualLengthInBytes = 0;
            for (int j = endIndex; j >= startIndex; j--)
            {
                if (data[j] > 0)
                {
                    actualLengthInBytes = j - startIndex + 1;
                    break;
                }
            }

            frameBoundaryInBytes = -1;
            for (int j = startIndex; j <= endIndex; j++)
            {
                if (MatchHeader(data, j))
                {
                    // Identify where 'jaga' header is located.
                    frameBoundaryInBytes = j;
                    break;
                }
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
    }
}