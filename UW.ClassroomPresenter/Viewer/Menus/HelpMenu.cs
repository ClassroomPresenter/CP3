// $Id: HelpMenu.cs 1709 2008-08-13 20:52:07Z fred $

using System;
using System.Windows.Forms;
using System.Drawing;
using System.Net;
using System.Diagnostics;
using System.Net.Sockets;

namespace UW.ClassroomPresenter.Viewer.Menus {
    public class HelpMenu : MenuItem {
        public HelpMenu() {
            this.Text = Strings.Help;
            this.MenuItems.Add( new OnlineHelpMenuItem() );
            this.MenuItems.Add( "-" );
            this.MenuItems.Add(new GettingStartedMenuItem());
            this.MenuItems.Add("-");
            this.MenuItems.Add(new VersionInfoMenuItem());
            this.MenuItems.Add(new IPAddressMenuItem());
            this.MenuItems.Add(new LicenseMenuItem());
            this.MenuItems.Add( "-" );
            this.MenuItems.Add( new AboutMenuItem() );
        }

        public class OnlineHelpMenuItem : MenuItem {
            public OnlineHelpMenuItem() {
                this.Text = Strings.OnlineHelp;
                this.Shortcut = Shortcut.F1;
                this.ShowShortcut = true;
            }

            protected override void OnClick( EventArgs e ) {
                base.OnClick( e );
                Help.ShowHelp( this.Parent.GetMainMenu().GetForm(), "http://www.cs.washington.edu/education/dl/presenter/" );
            }
        }
        public class GettingStartedMenuItem : MenuItem{
            public GettingStartedMenuItem() {
                this.Text = Strings.LaunchGettingStartedGuid;
            }
            protected override void OnClick(EventArgs e) {
                base.OnClick(e);
                string s = System.Reflection.Assembly.GetExecutingAssembly().Location;
                while (s[s.Length - 1] != '\\') {
                    s = s.Substring(0, s.Length - 1);
                } try {
                    Help.ShowHelp(((Control)(this.Parent.Container)), s + "Help\\startguide3.html");
                }
                catch { }
            }
        }

        public class IPAddressMenuItem : MenuItem {
            public IPAddressMenuItem() {
                this.Text = Strings.IPAddress;
            }

            protected override void OnClick(EventArgs e) {
                base.OnClick(e);

                IPAddressMessageBox mb = new IPAddressMessageBox(detectIPInformation());
                DialogResult dr = mb.ShowDialog();

            }

            /// <summary>
            /// Return IP address for the IP Address menu command.  The goal is to return one address
            /// and have it be useful most of the time.  There is no guarantee that the address we return
            /// will be the one the user wants in all cases.
            /// -Since it will be less practical for users to manually enter IPv6 addresses, we return them
            /// only if there are no IPv4 addresses.  IPv6 users will probably prefer to use DNS names for manual
            /// connections.
            /// -Othewise we prefer routable over non-routable addresses..
            /// </summary>
            /// <returns></returns>
            private String detectIPInformation() {
                String computerHostName = Dns.GetHostName();
                IPAddress[] IPlist = Dns.GetHostAddresses(computerHostName);
                IPAddress preferredAddress = null;

                foreach (IPAddress ip in IPlist) {
                    if (ip.AddressFamily == AddressFamily.InterNetwork) {  //IPv4
                        if (preferredAddress == null) {
                            //Replace null with any IPv4
                            preferredAddress = ip;
                        }
                        else if (preferredAddress.AddressFamily == AddressFamily.InterNetwork) {
                            //Replace IPv4 private address with any IPv4.
                            if (isPrivateSubnet(preferredAddress)) {
                                preferredAddress = ip;
                            }    
                        }
                        else if (preferredAddress.AddressFamily == AddressFamily.InterNetworkV6) { 
                            //Replace any IPv6 with any IPv4, even if the IPv4 is not routable and the IPv6 is.
                            preferredAddress = ip;
                        }
                    }
                    else if (ip.AddressFamily == AddressFamily.InterNetworkV6) {  //IPv6
                        if (preferredAddress == null) {
                            //Replace null with any IPv6
                            preferredAddress = ip;
                        }
                        else if (preferredAddress.AddressFamily == AddressFamily.InterNetworkV6) {
                            //Replace IPv6 link local, or site local with more or equally routable IPv6
                            if (preferredAddress.IsIPv6LinkLocal) {
                                preferredAddress = ip;
                            }
                            else if ((preferredAddress.IsIPv6SiteLocal) && (!ip.IsIPv6LinkLocal)) {
                                preferredAddress = ip;
                            }
                        }
                    }
                }

                if (preferredAddress == null) {
                    return "No Address Detected";
                }
                else {
                    return preferredAddress.ToString();
                }
            }

 
            /// <summary>
            /// Determine if a IPv4 address is in one of the private address ranges, eg. NAT'ed. (RFC1918)
            /// </summary>
            /// <param name="ep"></param>
            /// <returns></returns>
            private bool isPrivateSubnet(IPAddress address) {
                if (address.AddressFamily != AddressFamily.InterNetwork) {
                    Trace.WriteLine("Warning: isPrivateSubnet only applies to IPv4 addresses.");
                    return false;
                }

                uint aMin = 0x0A000000; //10.0.0.0
                uint aMax = 0x0AFFFFFF; //10.255.255.255
                uint bMin = 0xAC100000; //172.16.0.0
                uint bMax = 0xAC1FFFFF; //172.31.255.255
                uint cMin = 0xC0A80000; //192.168.0.0
                uint cMax = 0xC0A8FFFF; //192.168.255.255

                uint a = this.IPAddressToUInt(address);

                if (((a >= cMin) && (a <= cMax)) ||
                    ((a >= bMin) && (a <= bMax)) ||
                    ((a >= aMin) && (a <= aMax)))
                    return true;

                return false;
            }

            /// <summary>
            /// Since IPAddress.Address is now deprecated
            /// </summary>
            /// <param name="address"></param>
            /// <returns></returns>
            private uint IPAddressToUInt(IPAddress address) {
                if (address.AddressFamily != AddressFamily.InterNetwork) {
                    Trace.WriteLine("Warning: IPAddressToUInt only applies to IPv4 addresses.");
                    return 0;
                }

                byte[] ba = address.GetAddressBytes();
                if (ba.Length != 4)
                    return 0;
                Array.Reverse(ba);
                return BitConverter.ToUInt32(ba, 0);
            }


        }

 
        public class IPAddressMessageBox : Form {
            public IPAddressMessageBox(string ipAddressString) {

                Text = ipAddressString;

                this.Font = Model.Viewer.ViewerStateModel.FormFont;
                this.FormBorderStyle = FormBorderStyle.FixedDialog;
                this.MaximizeBox = false;
                this.MinimizeBox = false;
                this.ShowInTaskbar = false;

                Label label = new Label();
                label.FlatStyle = FlatStyle.System;
                label.Location = new Point(10, 15);

                label.Font = new Font("Arial", 20);
                label.Text = Strings.IPAddressIs + ipAddressString;
                
                label.TextAlign = ContentAlignment.MiddleCenter;
                label.Parent = this;
                label.Size = label.PreferredSize;
      
                this.Width = label.Size.Width+20;
                this.Height = 160;
   

                Button button = new Button();
                button.FlatStyle = FlatStyle.System;
                button.Font = Model.Viewer.ViewerStateModel.StringFont1;
                button.Parent = this;
                button.Text = Strings.OK;
                button.Location = new Point(this.Width/2 - 30, 70);
                button.Size = new Size(60, 40);
                button.DialogResult = DialogResult.OK;
            }
            
        }

        public class LicenseMenuItem: MenuItem {
            public LicenseMenuItem() {
                this.Text = Strings.License;
            }

            protected override void  OnClick(EventArgs e) {
 	            base.OnClick(e);
                UW.ClassroomPresenter.Misc.LicenseForm lf = new UW.ClassroomPresenter.Misc.LicenseForm();
                lf.ShowDialog();
            }
        }

        public class AboutMenuItem : MenuItem {
            public AboutMenuItem() {
                this.Text = Strings.AboutClassroomPresenter;
            }

            protected override void OnClick(EventArgs e) {
                base.OnClick(e);
                UW.ClassroomPresenter.Misc.AboutForm af = new UW.ClassroomPresenter.Misc.AboutForm();
                af.ShowDialog();
            }
        }

        public class VersionInfoMenuItem : MenuItem {
            public VersionInfoMenuItem() {
                this.Text = Strings.VersionInfo;
            }

            protected override void OnClick(EventArgs e) {
                base.OnClick(e);
                UW.ClassroomPresenter.Misc.VersionCompatibilityInfoForm vf = new UW.ClassroomPresenter.Misc.VersionCompatibilityInfoForm();
                vf.ShowDialog();
            }
        }
    }
}
