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
        public static readonly int TRANSFER_MAX_OUTSTANDING_IO = 10;

        /// <summary>Size of each transfer</summary>
        public static int TRANSFER_SIZE;

        private static DateTime mStartTime = DateTime.MinValue;

        /// <summary>
        /// This is # of packets received. Dropped packets are not included.
        /// </summary>
        private static double mTotalPacketsHandled = 0.0;
        /// <summary>
        /// This will include (estimated) dropped packets
        /// </summary>
        private static double apparentPacketsTransmitted = 0;
        private static int transfersCompleted = 0;

        static StringBuilder sb = new StringBuilder();
        static StringBuilder sb_header = new StringBuilder();
        static StringBuilder sb_error_rate = new StringBuilder();

        private delegate void ShowTransferDelegate(UsbTransferQueue.Handle h, int i, bool showTrailingZeros);

        public class TransferParams
        {
            public bool showAll;
            public bool showErroneous;
            public bool showHeaders;
            public bool showCorrected;
        }

        public static void StartIsochronous(RichTextBox _rtb, RichTextBox _rtb_header, RichTextBox _rtb_error_rate, TextBox _tbStatus, TransferParams tParams)
        {
            try
            {
                new Thread(() =>
                    {
                        StartIsochronous2(_rtb, _rtb_header, _rtb_error_rate, _tbStatus, tParams);
                    }).Start();
            }
            catch (Exception ex)
            {
            }
        }

        private static void StartIsochronous2(RichTextBox rtb, RichTextBox _rtb_header, RichTextBox _rtb_error_rate, TextBox tbStatus, TransferParams tParams)
        {
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
                packetErrors = 0;
                mTotalPacketsHandled = 0;
                apparentPacketsTransmitted = 0;
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
                    tbStatus.BeginInvoke((MethodInvoker)delegate { tbStatus.Text = mTotalPacketsHandled.ToString(); });

                    // Write string that was obtained from showTransfer()
                    if (sb.Length > 2000)
                    {
                        SafeAppendText(rtb, sb.ToString());
                        sb.Clear();
                    }

                    if (sb_header.Length > 0)
                    {
                        SafeAppendText(_rtb_header, sb_header.ToString(), true);
                        sb_header.Clear();
                    }

                    if (sb_error_rate.Length > 0)
                    {
                        SafeAppendText(_rtb_error_rate, sb_error_rate.ToString(), true);
                        sb_error_rate.Clear();
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
                SafeAppendText(rtb, "\r\n" + (ec != ErrorCode.None ? ec + ":" : String.Empty) + ex.Message + " : Error code " + System.Runtime.InteropServices.Marshal.GetLastWin32Error() + "\r\n");
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

        private static void SafeAppendText(RichTextBox rtb, string s, bool clear = false)
        {
            if (clear)
                rtb.BeginInvoke((MethodInvoker)delegate { rtb.Clear(); });

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

        static StringBuilder sbPreviousTransfer = new StringBuilder();
        static StringBuilder sbPreviousTransfer2 = new StringBuilder();

        static int transfersReceived = 0;
        static int packetErrors = 0;
        static int packetsCorrected = 0;
        static int prevPacketCounter = -1;

        static int samplesPerPacket = -1;
        static int channels = -1;
        static int sample_rate = -1;

        static byte[] old_remnant, new_remnant;

        private static void showTransfer(UsbTransferQueue.Handle handle, int transferIndex, TransferParams tParams)
        {
            double samplesPerSecond = 0;

            int numBytesTransferred = handle.Data.Length;

            int maxTransferSize = handle.Context.IsoPacketSize;
            int x = handle.Context.Transmitted;

            int packetCounter = 0;

            int[][] jagaPacketData;
            int[] potentialErrors;

            int cachedOldRemnantLength = 0;
            if (old_remnant != null)
                cachedOldRemnantLength = old_remnant.Length;

            AlignTransfersIntoJagaPackets(handle.Data, old_remnant, out jagaPacketData, out new_remnant, handle.Transferred, maxTransferSize != 160, out potentialErrors);

            old_remnant = new_remnant;

            StringBuilder sbCurrentTransfer = new StringBuilder();

            int header = FindHeaderBoundary(handle.Transferred, handle.Data);
            bool hasErroneousPacket = false;
            bool hasCorrectedPacket = false;

            sbCurrentTransfer.Append("\r\nTransfer ");

            // Show transfer # at beginning of each line
            sbCurrentTransfer.Append((transfersReceived++).ToString() + ":\t");

            // Show bytes in transfer
            for (int j = 0; j < handle.Transferred; j++)
                sbCurrentTransfer.Append(String.Format(" {0:X2}", handle.Data[j]));

            sbCurrentTransfer.Append(string.Format("\t:{0} bytes in transfer", handle.Transferred));

            if (cachedOldRemnantLength > 0)
                sbCurrentTransfer.Append(", appended to " + cachedOldRemnantLength.ToString() + " from previous transfer");

            if (new_remnant != null && new_remnant.Length > 0)
            {
                sbCurrentTransfer.Append(", with " + new_remnant.Length.ToString() + " left over for next transfer");
            }

            if (header >= 0 && handle.Data.Length >= header + 13)
            {
                samplesPerPacket = handle.Data[header + 10];
                channels = handle.Data[header + 9];
                sample_rate = (handle.Data[header + 11] << 8) + handle.Data[header + 12];

                sbCurrentTransfer.Append(" HEADER PACKET");

                sb_header.Append(string.Format("Header info: version {0}, channels {1}, samples per packet {2}, sample rate {3}", handle.Data[header + 8], channels, samplesPerPacket, sample_rate));
            }

            if (jagaPacketData.Length > 0)
                // Now show packets
                sbCurrentTransfer.Append("\r\n");

            for (int packetNum = 0; packetNum < jagaPacketData.Length; packetNum++)
            {
                if (jagaPacketData[packetNum] == null)
                    // The # of packets is not deterministic. Once we hit nulls, we are done.
                    break;

                packetCounter = 0;
                sbCurrentTransfer.Append("\r\nPacket " + mTotalPacketsHandled.ToString() + ":\t");

                int numSamples = jagaPacketData[packetNum].Length;

                for (int j = 0; j < numSamples; j++)
                {
                    // Calculate packet counter from LSB
                    int val = jagaPacketData[packetNum][j];
                    packetCounter = packetCounter * 2 + (val & 1);

                    if (j % 4 == 0)
                        sbCurrentTransfer.Append("  ");

                    sbCurrentTransfer.Append(String.Format(" {0:X4}", val));
                }

                mTotalPacketsHandled++;

                // Append packet counter
                sbCurrentTransfer.Append(String.Format("\t: Counter {0:X4}", packetCounter));

                if (prevPacketCounter >= 0)
                {
                    int increment = CalculateIncrement14_bit(prevPacketCounter, packetCounter);

                    sbCurrentTransfer.Append(String.Format("\tIncrement {0}", increment));

                    if (increment <= 0)
                    {
                        // Check if bit reversal restores correct count
                        if (potentialErrors[0] >= 0)
                        {
                            int bitToFlip = 15 - (potentialErrors[0] / 2);
                            bitToFlip = 1 << bitToFlip;
                            if ((packetCounter & bitToFlip) == 0)
                            {
                                // Switch 0 to 1
                                int adjustedCounter = packetCounter + bitToFlip;

                                if (CalculateIncrement14_bit(prevPacketCounter, adjustedCounter) == 1)
                                {
                                    // We have fixed packet counter
                                    sbCurrentTransfer.Append(" \tCorrected packet counter from " + packetCounter + " to " + adjustedCounter);
                                    packetCounter = adjustedCounter;
                                    packetsCorrected++;

                                    increment = 1;
                                    hasCorrectedPacket = true;
                                }
                            }

                            if (!hasCorrectedPacket)
                            {
                                sbCurrentTransfer.Append(" \tUnable to correct packet counter - error location " + potentialErrors[0].ToString() + ", bitToFlip is " + bitToFlip.ToString());
                            }
                        }
                    }

                    if (increment > 0)
                        apparentPacketsTransmitted += increment;

                    if (increment != 1)
                    {
                        hasErroneousPacket = true;
                        sbCurrentTransfer.Append(" ERROR IN PACKET COUNTER, EXPECTED " + (packetCounter + 1).ToString());
                        packetErrors++;
                    }

                }

                prevPacketCounter = packetCounter;

            }

            sbCurrentTransfer.Append(", ");

            samplesPerSecond = apparentPacketsTransmitted * samplesPerPacket / channels / 2 / (DateTime.Now - mStartTime).TotalSeconds;

            sbCurrentTransfer.Append(String.Format("{0} avg samples/sec\r\n\r\n", Math.Round(samplesPerSecond, 2)));
            if (header >= 0)
                sb_header.Append(String.Format(", Calculated sample rate = {0} samples/sec", Math.Round(samplesPerSecond, 2)));


            if (hasErroneousPacket || hasCorrectedPacket)
                sb_error_rate.Append(string.Format("Counter errors {0}, error rate {3:F2}%, corrected packet counters {1}, total packets {2}",
                    packetErrors, packetsCorrected, mTotalPacketsHandled, packetErrors * 100.0 / mTotalPacketsHandled));

            bool showThisPacket;

            if (tParams.showAll)
                showThisPacket = true;
            else
                showThisPacket = (tParams.showErroneous && hasErroneousPacket) || (tParams.showHeaders && header >= 0) || (tParams.showCorrected && hasCorrectedPacket);

            if (showThisPacket)
            {
                if (!tParams.showAll && (tParams.showCorrected || tParams.showErroneous))
                {
                    // For errors, show previous transfers, to help understand error.
                    sb.Append("\r\n\r\n");
                    if (handle.Transferred < 50)
                        // If transfers are small, then show previous two, since each transfer doesn't have that much info.
                        sb.Append(sbPreviousTransfer2);
                    sb.Append(sbPreviousTransfer);
                }

                // Append to ongoing string that will eventually get sent to screen
                sb.Append(sbCurrentTransfer);
            }
//        Done:
            // Save current text so that if we are showing only anomalous packets, then next time we can show both current and previous packets (since anomalous packets usually arise from
            // irregularities involving a packet spanning two transfers.
            sbPreviousTransfer2.Clear();
            sbPreviousTransfer2.Append(sbPreviousTransfer);
            sbPreviousTransfer.Clear();
            sbPreviousTransfer.Append(sbCurrentTransfer);
        }

        /// <summary>
        /// Calculate gap between consecutive counters, accounting for possible 14-bit rollover.
        /// </summary>
        /// <param name="oldCtr"></param>
        /// <param name="newCtr"></param>
        /// <returns></returns>
        private static int CalculateIncrement14_bit(int oldCtr, int newCtr)
        {
            int gap = newCtr - oldCtr;
            if (gap < -8192)
                gap += 16384;

            return gap;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="data">Data from most recent USB transfer</param>
        /// <param name="old_remnant">This would be any unprocessed portion of the end of any previous transfers.</param>
        /// <param name="jagaPacketData">Output data, reorganized into single samples</param>
        /// <param name="new_remnant">Any unprocessed portion at the end of the current transfer</param>
        /// <param name="usbTransferSize">It is assumed that "data" consists of several consecutive transfers, e.g. 16 transfers of 102 bytes each. This is the size (102) of each individual transfer.</param>
        static void AlignTransfersIntoJagaPackets(byte[] data, byte[] old_remnant, out int[][] jagaPacketData, out byte[] new_remnant, int transferred, bool IsSpikeVersion, out int[] packetPotentialErrors)
        {
            int dataWordsPerJagaPacket = 16;
            packetPotentialErrors = new int[8];

            for (int i = 0; i < packetPotentialErrors.Length; i++)
                packetPotentialErrors[i] = -1;


            int frameHeaderSize;
            int headerSize = 13;  // Header packet size
            // If we inserted a 0 between frames, this is the location.
            int wildcardLocation = -1;

            if (IsSpikeVersion)
            {
                // New version has header
                frameHeaderSize = 4;
                if (old_remnant != null && old_remnant.Length > 0)
                {
                    // If there is leftover data from old transfer, use heuristics to determine whether
                    // it is contiguous with new transfer. Most of the time it will be, but sometimes there
                    // is a byte or two missing in between. Sometimes there is an entire packet missing.
                    byte[] concatenatedData = new byte[old_remnant.Length + transferred];

                    // Copy tail end of old data into new array
                    Array.Copy(old_remnant, concatenatedData, old_remnant.Length);
                    // Append new data onto end of old remnant
                    Array.Copy(data, 0, concatenatedData, old_remnant.Length, transferred);

                    // Look for frame boundaries in two consecutive transfers
                    int frame1 = FindFrameBoundary(old_remnant.Length, concatenatedData, 0);
                    int frame2 = FindFrameBoundary(concatenatedData.Length, concatenatedData, old_remnant.Length);

                    bool useRemnant = true;


                    if (frame1 >= 0 && frame2 >= 0)
                    {
                        int interval = frame2 - frame1;

                        int expectedInterval = frameHeaderSize + dataWordsPerJagaPacket * 2;

                        int remainder = (interval % expectedInterval);

                        if (remainder != 0 && remainder != headerSize)
                        {
                            if (remainder + 1 == expectedInterval || remainder + 1 == headerSize)
                            {
                                // Very often, a single byte is dropped between packets. Just insert a zero and hope for the best.
                                InsertZero(ref concatenatedData, old_remnant.Length);
                                wildcardLocation = old_remnant.Length;

                                // Because of zero, packet counter may be too low by a power of two. Flag location of possible error.
                                packetPotentialErrors[0] = old_remnant.Length - 4 - frame1;

                                // Account for this additional dummy byte.
                                transferred++;
                            }
                            else
                                // Interval between consecutive frames is wrong. Just discard old remnant, as there is most likely a missed byte or two, or possibly an entire missed packet.    
                                useRemnant = false;
                        }
                        else
                        {
                            // Interval between consecutive frames is correct. But first byte of new frame could still be wrong.
                            packetPotentialErrors[0] = old_remnant.Length - 4 - frame1;
                        }
                    }

                    if (useRemnant)
                    {
                        int h = FindHeaderBoundary(concatenatedData.Length, concatenatedData);

                        if (h >= 0)
                        {
                            // Delete 13 bytes starting at header
                            for (int j = h + headerSize; j < concatenatedData.Length; j++)
                                concatenatedData[j - headerSize] = concatenatedData[j];

                            transferred -= headerSize;
                        }

                        // Use combination of old remnant and new data.
                        data = concatenatedData;
                        // Include the old remnant in size of new transfer
                        transferred = old_remnant.Length + transferred;
                    }
                }
            }
            else
                // Old version has no header, and no possibility of packets bridging across frames.
                frameHeaderSize = 0;

            int bytesPerJagaPacketWithHeader = dataWordsPerJagaPacket * 2 + frameHeaderSize;

            int frameStart;

            if (IsSpikeVersion)
                frameStart = FindFrameBoundary(transferred, data, 0, wildcardLocation);
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
                    // Not enough bytes for a full packet. Must combine with next transfer's data.
                    break;
                }

                if (IsSpikeVersion)
                {
                    if (!MatchFrameBoundary(data, frameStart, wildcardLocation))
                    {
                        // Hmmmm, this is highly unusual. We have enough bytes for a packet, but the header isn't here.
                        // Probably something is seriously malformed. We can just append bytes to the next transfer and hope for
                        // the best.
                        break;
                    }
                }

                sampleDataTmp[currentPacket] = new int[dataWordsPerJagaPacket];

                ByteToWordCopy(data, sampleDataTmp[currentPacket], frameStart + frameHeaderSize, 0, dataWordsPerJagaPacket);

                frameStart += bytesPerJagaPacketWithHeader;
                currentPacket++;

                if (IsSpikeVersion)
                {
                    if (MatchHeader2(data, frameStart))
                        // Skip header frame
                        frameStart += headerSize;
                }

            }

            if (bytesRemaining > 0)
            {
                // We have a partial frame remaining
                // Store partial frame as remnant, so it can be combined with data from next transfer.
                new_remnant = new byte[bytesRemaining];
                Array.Copy(data, frameStart, new_remnant, 0, bytesRemaining);

                if (bytesRemaining < 4)
                {
                }
            }

            if (bytesRemaining < 0)
                throw new Exception("Please contact program developer - Isochronous data transfer error #1");

            jagaPacketData = sampleDataTmp;
        }

        /// <summary>
        /// Insert a zero at specified location in array
        /// </summary>
        /// <param name="concatenatedData"></param>
        /// <param name="index"></param>
        static void InsertZero(ref byte[] concatenatedData, int index)
        {
            // Very often, a single byte is dropped between packets. Just insert a zero and hope for the best.
            Array.Resize<byte>(ref concatenatedData, concatenatedData.Length + 1);
            for (int j = concatenatedData.Length - 1; j > index; j--)
                concatenatedData[j] = concatenatedData[j - 1];

            concatenatedData[index] = 0;
        }

        /// <summary>
        /// Find frame boundary ('jaga' header) and actual length of each transfer
        /// </summary>
        /// <param name="startIndex"></param>
        /// <param name="endIndexPlusOne"></param>
        /// <param name="data"></param>
        /// <param name="actualLengthInBytes"></param>
        /// <param name="frameBoundaryInBytes"></param>
        static int FindFrameBoundary(int endIndexPlusOne, byte[] data, int startIndex, int wildcardLocation = -1)
        {
            int frameBoundaryInBytes = -1;
            for (int j = startIndex; j < endIndexPlusOne; j++)
            {
                if (MatchFrameBoundary(data, j, wildcardLocation))
                {
                    // Identify where 'jaga' header is located.
                    frameBoundaryInBytes = j;
                    break;
                }
            }

            return frameBoundaryInBytes;
        }

        /// <summary>
        /// Search for "JAGAJAGA"
        /// </summary>
        /// <param name="length"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        static int FindHeaderBoundary(int length, byte[] data)
        {
            int boundaryInBytes = -1;
            for (int j = 0; j < length; j++)
            {
                if (MatchHeader2(data, j))
                {
                    // Identify where 'JAGA' header is located.
                    boundaryInBytes = j;
                    break;
                }
            }

            if (boundaryInBytes >= 0)
            {
                if (!MatchHeader2(data, boundaryInBytes + 4))
                    return -1;
            }

            return boundaryInBytes;
        }


        static void ByteToWordCopy(byte[] src, int[] dest, int indexSrc, int indexDest, int bytePairsToCopy)
        {
            // Src data is big-endian.
            for (int i = 0; i < bytePairsToCopy; i++)
            {
                dest[indexDest++] = (src[indexSrc++] << 8) + (src[indexSrc++]);
            }
        }


        static bool MatchFrameBoundary(byte[] data, int index, int wildcardLocation = -1)
        {
            if (data.Length < index + 4)
                return false;

            if (data[index] != 'j')
            {
                if (wildcardLocation != index)
                    return false;
            }

            if (data[++index] != 'a')
            {
                if (index != wildcardLocation)
                    return false;
            }

            if (data[++index] != 'g')
            {
                if (index != wildcardLocation)
                    return false;
            }

            if (data[++index] != 'a')
            {
                if (index != wildcardLocation)
                    return false;
            }

            return true;
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