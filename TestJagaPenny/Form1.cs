using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using LibUsbDotNet;
using LibUsbDotNet.Main;
using LibUsbDotNet.LibUsb;
using MonoLibUsb;

namespace TestJagaPenny
{
    public partial class Form1 : Form
    {
        UsbRegDeviceList devs;
        UsbDevice dev;
        UsbRegistry reg;
        UsbEndpointReader rdr;

        static string DiagnosticSubFolder = "TestJagaPenny";

        public Form1()
        {
            InitializeComponent();

            devs = UsbDevice.AllDevices;

            if (devs.Count > 0)
                reg = devs[0];

            Process p = Process.GetCurrentProcess();
            p.PriorityClass = ProcessPriorityClass.High;

            this.WindowState = FormWindowState.Maximized;
        }

        private void buttonRead_Click(object sender, EventArgs e)
        {
            dev = UsbDevice.OpenUsbDevice(new UsbDeviceFinder(0x1915, 0x7B));
//            bool success = reg.Open(out dev);
            IUsbDevice wholeUsbDevice = dev as IUsbDevice;
            wholeUsbDevice.SetConfiguration(1);
            wholeUsbDevice.ClaimInterface(0);
            rdr = wholeUsbDevice.OpenEndpointReader((ReadEndpointID)0x81);
            rdr.Flush();
            Read();
        }

        private void Read()
        {
            byte[] readBuffer = new byte[32];

            int uiTransmitted;
            ErrorCode eReturn;
            Program.quitFlag = false;

            int total = 0;

            while ((eReturn = rdr.Read(readBuffer, 1000, out uiTransmitted)) == ErrorCode.None)
            {
//                showBytes(readBuffer, uiTransmitted);

                total += uiTransmitted;
                richTextBox1.Text = total + " bytes read.";

                WriteDiagnostics(total.ToString());

                if (Program.quitFlag)
                {
                    Program.quitFlag = false;
                    richTextBox1.Text += "User quit";

                    closeDevice();
                    return;
                }
                Application.DoEvents();
            }

            richTextBox1.Text += "Data stream ended! " + eReturn;
            closeDevice();
        }

        private void closeDevice()
        {
            if (rdr != null)
            {
                rdr.DataReceivedEnabled = false;
                rdr.Dispose();
                rdr = null;
            }

            // If this is a "whole" usb device (libusb-win32, linux libusb)
            // it will have an IUsbDevice interface. If not (WinUSB) the 
            // variable will be null indicating this is an interface of a 
            // device.
            IUsbDevice wholeUsbDevice = dev as IUsbDevice;
            if (!ReferenceEquals(wholeUsbDevice, null))
            {
                // Release interface #0.
                wholeUsbDevice.ReleaseInterface(0);
            }

            dev.Close();
            dev = null;
        }

        private void showBytes(byte[] readBuffer, int uiTransmitted)
        {
            // Display the raw data
            richTextBox1.AppendText(GetHexString(readBuffer, 0, uiTransmitted).ToString());
        }

        /// <summary>
        /// Converts bytes into a hexidecimal string
        /// </summary>
        /// <param name="data">Bytes to converted to a a hex string.</param>
        private static StringBuilder GetHexString(byte[] data, int offset, int length)
        {
            StringBuilder sb = new StringBuilder(length * 3);
            for (int i = offset; i < (offset + length); i++)
            {
                sb.Append(data[i].ToString("X2") + " ");
            }
            return sb.Append("\r\n");
        }

        private void buttonEnd_Click(object sender, EventArgs e)
        {
            Program.quitFlag = true;
        }

        string diagnosticFilePath;
        StreamWriter diagnosticStreamWriter;
        DateTime logFileStartDateTime;

        private void CreateDiagnosticFileIfNotPresent()
        {
            if (diagnosticFilePath == null)
            {
                DateTime dt = DateTime.Now;
                logFileStartDateTime = dt;

                string dateString;

                dateString = dt.Year.ToString() + "_" + dt.Month.ToString("00") + "_" + dt.Day.ToString("00") + "_" + dt.Hour.ToString("00") + "-" + dt.Minute.ToString("00") + "-" + dt.Second.ToString("00");
                diagnosticFilePath = dateString + "_JAGA_diagnostics.txt";
                diagnosticFilePath = GetAppDataFolderFullPath() + "\\" + diagnosticFilePath;
                diagnosticStreamWriter = new StreamWriter(diagnosticFilePath);
                diagnosticStreamWriter.WriteLine("Date\tTime\tSeconds\tPackets lost\tCumulative discarded\tCumulative reconstructed");
                diagnosticStreamWriter.WriteLine("\n");
            }
        }

        private void WriteDiagnostics(string msg)
        {
            DateTime dt = DateTime.Now;
            string dateString;

            try
            {
                CreateDiagnosticFileIfNotPresent();

                dateString = dt.Year.ToString() + "-" + dt.Month.ToString("00") + "-" + dt.Day.ToString("00") + "\t" + dt.Hour.ToString("00") + ":" + dt.Minute.ToString("00") + ":" + dt.Second.ToString("00");
                TimeSpan s = dt.Subtract(logFileStartDateTime);
                // Write to text log.  Need \r\n so it will show up correctly in NotePad.
                diagnosticStreamWriter.WriteLine(dateString + "." + dt.Millisecond.ToString("000") + "\t" + (s.TotalMilliseconds / 1000.0).ToString() + "\t" + msg);
                diagnosticStreamWriter.Flush();
            }
            catch
            {
                // Ignore errors.
            }
        }

        /// <summary>
        /// Get full path of folder where XML files are kept, e.g. Device Dictionary, and Licensing Info.
        /// If folder does not exist, then create it.
        /// </summary>
        /// <returns></returns>
        public static string GetAppDataFolderFullPath()
        {
            string f = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\" + DiagnosticSubFolder;

            try
            {
                if (!Directory.Exists(f))
                    Directory.CreateDirectory(f);
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show("Error creating folder \"" + f + "\" for storing configuration information. Please contact program developer.\r\n\r\n" + ex.Message);
            }

            return f;
        }

        private void buttonBrowseLogs_Click(object sender, EventArgs e)
        {
            string folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\" + DiagnosticSubFolder;// Path.GetDirectoryName(logFilePath);
            System.Diagnostics.Process.Start("explorer", folder);
        }

        private void buttonReadIso_Click(object sender, EventArgs e)
        {
            ReadIsochronous.TransferParams tParams = new ReadIsochronous.TransferParams();
            tParams.showErroneous = checkBoxShowErroneousPackets.Checked;
            tParams.showHeaders = checkBoxSelectivelyShowHeaders.Checked;

            ReadIsochronous.StartIsochronous(richTextBox1, textBoxCount, tParams);
        }

        private void buttonStopIso_Click(object sender, EventArgs e)
        {
            ReadIsochronous.Stop();
        }

        private void buttonReadMono_Click(object sender, EventArgs e)
        {
//            MonoTest.ShowInfo(richTextBox1);

//            MonoTest2.ShowConfig(richTextBox1);
            MonoTest3.ShowConfig(richTextBox1);
        }

        private void buttonClearText_Click(object sender, EventArgs e)
        {
            richTextBox1.Clear();
        }
    }
}
