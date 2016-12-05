using System;
using System.Collections.Generic;
using System.Windows.Forms;

using LibUsbDotNet.Main;
using MonoLibUsb;
using MonoLibUsb.Descriptors;
using MonoLibUsb.Profile;
using Usb = MonoLibUsb.MonoUsbApi;

namespace TestJagaPenny
{
    internal class MonoTest1
    {
        // The first time the Session property is used it creates a new session
        // handle instance in '__sessionHandle' and returns it. Subsequent 
        // request simply return '__sessionHandle'.
        private static MonoUsbSessionHandle __sessionHandle;
        public static MonoUsbSessionHandle Session
        {
            get
            {
                if (ReferenceEquals(__sessionHandle, null))
                    __sessionHandle = new MonoUsbSessionHandle();
                return __sessionHandle;
            }
        }

        public static void ShowInfo(RichTextBox rtb)
        {
            int ret;
            MonoUsbProfileList profileList = null;

            // Initialize the context.
            if (Session.IsInvalid)
                throw new Exception("Failed to initialize context.");

            MonoUsbApi.SetDebug(Session, 0);
            // Create a MonoUsbProfileList instance.
            profileList = new MonoUsbProfileList();

            // The list is initially empty.
            // Each time refresh is called the list contents are updated. 
            ret = profileList.Refresh(Session);
            if (ret < 0) throw new Exception("Failed to retrieve device list.");
            
            rtb.AppendText(string.Format("{0} device(s) found.\r\n", ret));

            MonoUsbProfile JagaProfile;

            // Iterate through the profile list; write the device descriptor to
            // console output.
            foreach (MonoUsbProfile profile in profileList)
            {
                rtb.AppendText(profile.DeviceDescriptor.ToString() + "\r\n");

                if (profile.DeviceDescriptor.VendorID == 0x1915)
                    JagaProfile = profile;
            }



            // Since profile list, profiles, and sessions use safe handles the
            // code below is not required but it is considered good programming
            // to explicitly free and close these handle when they are no longer
            // in-use.
            profileList.Close();
            Session.Close();
        }
    }

    internal class MonoTest2
    {
        private static MonoUsbSessionHandle sessionHandle;

        // Predicate functions for finding only devices with the specified VendorID & ProductID.
        private static bool MyVidPidPredicate(MonoUsbProfile profile)
        {
            if (profile.DeviceDescriptor.VendorID == 0x1915 && profile.DeviceDescriptor.ProductID == 0x007B)
                return true;
            return false;
        }

        public static void ShowConfig(RichTextBox rtb)
        {
            // Initialize the context.
            sessionHandle = new MonoUsbSessionHandle();
            if (sessionHandle.IsInvalid)
                throw new Exception(String.Format("Failed intialized libusb context.\n{0}:{1}",
                                                  MonoUsbSessionHandle.LastErrorCode,
                                                  MonoUsbSessionHandle.LastErrorString));

            MonoUsbProfileList profileList = new MonoUsbProfileList();

            // The list is initially empty.
            // Each time refresh is called the list contents are updated. 
            int ret = profileList.Refresh(sessionHandle);
            if (ret < 0) throw new Exception("Failed to retrieve device list.");
            rtb.AppendText(string.Format("{0} device(s) found.\r\n", ret));

            // Use the GetList() method to get a generic List of MonoUsbProfiles
            // Find all profiles that match in the MyVidPidPredicate.
            List<MonoUsbProfile> myVidPidList = profileList.GetList().FindAll(MyVidPidPredicate);

            // myVidPidList reresents a list of connected USB devices that matched
            // in MyVidPidPredicate.
            foreach (MonoUsbProfile profile in myVidPidList)
            {
                MonoUsbDeviceHandle h = profile.OpenDeviceHandle();// Usb.OpenDeviceWithVidPid(sessionHandle, 0x1915, 0x007B);

                if (h.IsInvalid)
                    throw new Exception(string.Format("Failed opening device handle.\r\n{0}: {1}",
                                                      MonoUsbDeviceHandle.LastErrorCode,
                                                      MonoUsbDeviceHandle.LastErrorString));

                Usb.SetConfiguration(h, 1);
                Usb.ClaimInterface(h, 1);
                MonoUsbProfileHandle ph = Usb.GetDevice(h);
                int packetSize = Usb.GetMaxIsoPacketSize(ph, 0x88);

                                   

                // Write the VendorID and ProductID to console output.
                rtb.AppendText(string.Format("[Device] Vid:{0:X4} Pid:{1:X4}\r\n", profile.DeviceDescriptor.VendorID, profile.DeviceDescriptor.ProductID));

                // Loop through all of the devices configurations.
                for (byte i = 0; i < profile.DeviceDescriptor.ConfigurationCount; i++)
                {
                    // Get a handle to the configuration.
                    MonoUsbConfigHandle configHandle;
                    if (MonoUsbApi.GetConfigDescriptor(profile.ProfileHandle, i, out configHandle) < 0) continue;
                    if (configHandle.IsInvalid) continue;

                    // Create a MonoUsbConfigDescriptor instance for this config handle.
                    MonoUsbConfigDescriptor configDescriptor = new MonoUsbConfigDescriptor(configHandle);

                    // Write the bConfigurationValue to console output.
                    rtb.AppendText(string.Format("  [Config] bConfigurationValue:{0}\r\n", configDescriptor.bConfigurationValue));

                    // Interate through the InterfaceList
                    foreach (MonoUsbInterface usbInterface in configDescriptor.InterfaceList)
                    {
                        // Interate through the AltInterfaceList
                        foreach (MonoUsbAltInterfaceDescriptor usbAltInterface in usbInterface.AltInterfaceList)
                        {
                            // Write the bInterfaceNumber and bAlternateSetting to console output.
                            rtb.AppendText(string.Format("    [Interface] bInterfaceNumber:{0} bAlternateSetting:{1}\r\n",
                                              usbAltInterface.bInterfaceNumber,
                                              usbAltInterface.bAlternateSetting));

                            // Interate through the EndpointList
                            foreach (MonoUsbEndpointDescriptor endpoint in usbAltInterface.EndpointList)
                            {
                                // Write the bEndpointAddress, EndpointType, and wMaxPacketSize to console output.
                                rtb.AppendText(string.Format("      [Endpoint] bEndpointAddress:{0:X2} EndpointType:{1} wMaxPacketSize:{2}\r\n",
                                                  endpoint.bEndpointAddress,
                                                  (EndpointType)(endpoint.bmAttributes & 0x3),
                                                  endpoint.wMaxPacketSize));

                                if (endpoint.bEndpointAddress == 0x88)
                                {
                                }
                            }
                        }
                    }
                    // Not neccessary, but good programming practice.
                    configHandle.Close();
                }
            }
            // Not neccessary, but good programming practice.
            profileList.Close();
            // Not neccessary, but good programming practice.
            sessionHandle.Close();
        }
    }
}