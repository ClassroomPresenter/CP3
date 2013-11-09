using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Diagnostics;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Collections;
using System.Net.NetworkInformation;
using System.Runtime.Serialization;
using UW.ClassroomPresenter.Model;
using System.Windows.Forms;
using System.Net.Configuration;


namespace UW.ClassroomPresenter.Network.Broadcast {
    public class BroadcastListener : PropertyPublisher, IDisposable {

        private Thread m_ListenThread;
        private Thread m_MaintenanceThread;
        private bool m_Disposed;
        private UdpClient m_Listener;
        private ManualResetEvent m_MessageReceived;
        private InstructorAdvertisementCollection m_InstructorAdvertisements;
        private readonly Guid m_LocalSenderID;
        private static BroadcastListener thisInstance = null;

        /// <summary>
        /// This is how long (seconds) an instructor advertisements remains in the list after the last 
        /// broadcast message is received.
        /// </summary>
        public static int AdvertisementTimeout = 5; 

        public BroadcastListener(Guid localSenderID) {
            thisInstance = this;
            m_Disposed = false;
            m_InstructorAdvertisements = new InstructorAdvertisementCollection(this,"InstructorAdvertisements");
            m_MessageReceived = new ManualResetEvent(false);
            m_LocalSenderID = localSenderID;

            m_ListenThread = new Thread(new ThreadStart(ListenerThread));
            m_ListenThread.Name = "BroadcastListener";
            m_ListenThread.Start();

            m_MaintenanceThread = new Thread(new ThreadStart(MaintenanceThread));
            m_MaintenanceThread.Name = "BroadcastListener Maintenance Thread";
            m_MaintenanceThread.Start();

        }

        /// <summary>
        /// Given a participant ID, return known endpoints for that ID.
        /// </summary>
        /// <param name="participantId"></param>
        /// <returns></returns>
        public static IPEndPoint[] GuidToEndPoints(Guid participantId) {
            List<IPEndPoint> results = new List<IPEndPoint>();
            using (Synchronizer.Lock(thisInstance.SyncRoot)) {
                foreach (InstructorAdvertisement ia in thisInstance.m_InstructorAdvertisements) {
                    if (ia.InstructorID == participantId) {
                        results.Add(ia.Endpoint);
                    }
                }
            }
            return results.ToArray();
        }


        /// <summary>
        /// Clean up the table of instructors if they time out.
        /// </summary>
        private void MaintenanceThread() {
            while (!m_Disposed) {
                using (Synchronizer.Lock(this.SyncRoot)) { 
                    List<InstructorAdvertisement> remove = new List<InstructorAdvertisement>();
                    foreach (InstructorAdvertisement ia in m_InstructorAdvertisements) {
                        if ((DateTime.Now - ia.Timestamp) > TimeSpan.FromSeconds(BroadcastListener.AdvertisementTimeout)) {
                            remove.Add(ia);
                        }
                    }
                    foreach (InstructorAdvertisement ia in remove) {
                        Trace.WriteLine("Removing Instructor Table Entry: " + ia.ToString(), this.GetType().ToString());
                        m_InstructorAdvertisements.Remove(ia);
                    }
                }
                for (int i = 0; i < 10; i++) {
                    if (m_Disposed)
                        break;
                    Thread.Sleep(200);
                }
            }
        }

        /// <summary>
        /// Receive BroadcastMessages
        /// </summary>
        private void ListenerThread() {
            try {
                //Set non-exclusive port, so that multiple instances can run on one system.
                AddressFamily af = AddressFamily.InterNetwork;
                IPAddress ip = IPAddress.Any;
                if (BroadcastManager.UseIPv6) {
                    af = AddressFamily.InterNetworkV6;
                    ip = IPAddress.IPv6Any;
                }
                m_Listener = new UdpClient(af);
                m_Listener.Client.ExclusiveAddressUse = false;
                m_Listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                m_Listener.Client.Bind(new IPEndPoint(ip, BroadcastSender.ListenPort));
                if (BroadcastManager.UseIPv6) {
                    m_Listener.JoinMulticastGroup(BroadcastManager.IPv6LinkLocalMulticast);
                }
            }
            catch (SocketException se) {
                if (se.ErrorCode == 10048) {
                    //Some other app is using our port. There's no point in trying to continue.
                    //tell the user about this.
                    Trace.WriteLine("ListenerThread Terminating. " + se.Message +
                        " SocketException ErrorCode=" + se.ErrorCode.ToString(), this.GetType().ToString());
                    MessageBox.Show("The port Presenter would like to use to listen for insructor advertisements (" +
                        BroadcastSender.ListenPort.ToString() + ") is in use by another application.  Instructor advertisement " +
                        "listening will be disabled.", "Failed to Start Instructor Advertisement Listener", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else {
                    Trace.WriteLine("SocketException in ListenerThread: " + se.Message +
                        " errorcode=" + se.ErrorCode.ToString(), this.GetType().ToString());
                    MessageBox.Show("Presenter failed to initialize the instructor advertisement listener. " +
                        "SocketException ErrorCode=" + se.ErrorCode.ToString() + ".  Exception text: \r\n" +
                        se.ToString(), "Failed to Start Instructor Advertisement Listener", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return;
            }
            catch (Exception e) { 
                Trace.WriteLine("Exception in ListenerThread: " + e.ToString(), this.GetType().ToString());
                MessageBox.Show("Presenter failed to initialize the instructor advertisement listener.  Exception text: \r\n" +
                    e.ToString(), "Failed to Start Instructor Advertisement Listener", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            AsyncCallback ac = new AsyncCallback(ReceiveCallback);

            while (!m_Disposed) {
                m_MessageReceived.Reset();
                try {
                    m_Listener.BeginReceive(ac, m_Listener);
                    m_MessageReceived.WaitOne();
                }
                catch (Exception e) {
                    //BeginReceive can't except, but we're paranoid.
                    Trace.WriteLine("BroadcastListener thread exception: " + e.ToString(), this.GetType().ToString());
                    Thread.Sleep(1000);
                }
            }
        }

        private void ReceiveCallback(IAsyncResult ar) {
            UdpClient u = (UdpClient)ar.AsyncState;
            byte[] receiveBuf = null;
            IPEndPoint remoteEP = null;
            try {
                receiveBuf = u.EndReceive(ar, ref remoteEP);
            }
            catch (ObjectDisposedException ode) { 
                Trace.WriteLine("ReceiveCallback: " + ode.Message, this.GetType().ToString());
            }
            catch (Exception e) {
                Trace.WriteLine("ReceiveCallback exception: " + e.ToString(), this.GetType().ToString());
            }
            finally {
                m_MessageReceived.Set(); //Let the listen thread continue.
            }

            if (receiveBuf != null) {
                BroadcastMessage message = deserialize(receiveBuf);
                if (message != null) {
                    if (message.SenderID != m_LocalSenderID)
                        processMessage(message, DateTime.Now);
                }  
            }
        }

        /// <summary>
        /// Handle one inbound BroadcastMessage
        /// </summary>
        /// <param name="message"></param>
        /// <param name="timestamp"></param>
        private void processMessage(BroadcastMessage message, DateTime timestamp) {
            //List<InstructorAdvertisement> nonLocalPublicSubnets = new List<InstructorAdvertisement>();
            foreach (IPEndPoint ep in message.EndPoints) {
                InstructorAdvertisement ia = new InstructorAdvertisement(ep, message.HumanName, message.PresentationName, message.SenderID, message.ShowIP);
                using(Synchronizer.Lock(this.SyncRoot)) {
                    if (m_InstructorAdvertisements.Contains(ia)) {  //We already have the entry, just update the timestamp.
                        int index = m_InstructorAdvertisements.IndexOf(ia);
                        m_InstructorAdvertisements[index].Timestamp = DateTime.Now;
                        continue;
                    }

                    if (ep.AddressFamily.Equals(AddressFamily.InterNetwork)) { //IPv4
                        if (!isPrivateSubnet(ep.Address)) {
                            //If there are both local and non-local public subnets, keep only the local ones (plus multicast)
                            if (matchesAnyLocalSubnet(ep.Address)) {
                                Trace.WriteLine("Adding Instructor Table Entry: " + ia.ToString(), this.GetType().ToString());
                                m_InstructorAdvertisements.Add(ia); //Not a private subnet, add to the table.
                            }
                            else if (isMulticastAddress(ep.Address)) { 
                                Trace.WriteLine("Adding Instructor Table Entry: " + ia.ToString(), this.GetType().ToString());
                                ia.Multicast = true;
                                m_InstructorAdvertisements.Add(ia); //Not a private subnet, add to the table.
                            }
                            else {
                                //nonLocalPublicSubnets.Add(ia);
                            }
                        }
                        else { 
                            //If it is a private subnet, the listener also needs to be on that subnet
                            //In effect, we can't talk to NAT'ed hosts directly unless we are also on the same NAT'ed network.
                            //Otherwise, we would have to connect to a port forwarding router.
                            if (matchesAnyLocalSubnet(ep.Address)) {
                                Trace.WriteLine("Adding Instructor Table Entry: " + ia.ToString(), this.GetType().ToString());
                                m_InstructorAdvertisements.Add(ia);
                            }
                        }
                    }
                    else if (ep.AddressFamily.Equals(AddressFamily.InterNetworkV6)) {
                        if (BroadcastManager.UseIPv6) { 
                            //Don't show any IPv6 entries unless IPv6 is enabled on the local system.
                            //TODO: To correctly handle link local and site local, we need to do a bit more work here.
                            Trace.WriteLine("Adding Instructor Table Entry: " + ia.ToString(), this.GetType().ToString());
                            m_InstructorAdvertisements.Add(ia);   
                        }
                    }
                }
            }

            //Are there cases were we would want to show the non-local public subnet addresses?
        }

        /// <summary>
        /// True if address is a IPv4 multicast address.
        /// </summary>
        /// <param name="iPAddress"></param>
        /// <returns></returns>
        private bool isMulticastAddress(IPAddress address) {
            uint min = 0xE0000000; //224.0.0.0
            uint max = 0xEFFFFFFF; //239.255.255.255

            uint a = this.IPAddressToUInt(address);

            if ((a >= min) && (a <= max))
                return true;

            return false;           
        }

        /// <summary>
        /// Determine if there is a local IPv4 network that matches the network of the given IP address.
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        private bool matchesAnyLocalSubnet(IPAddress address) {
            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();

            if (nics == null || nics.Length < 1) {
                Trace.WriteLine("No network interfaces found.", this.GetType().ToString());
                return false;
            }

            foreach (NetworkInterface adapter in nics) {
                if (adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback) {
                    continue;
                }
                IPInterfaceProperties properties = adapter.GetIPProperties();

                UnicastIPAddressInformationCollection uniCast = properties.UnicastAddresses;
                if (uniCast != null) {
                    foreach (UnicastIPAddressInformation uni in uniCast) {
                        if (uni.Address.AddressFamily.Equals(AddressFamily.InterNetwork)) { //IPv4
                            if (matchesLocalSubnet(uni.Address, uni.IPv4Mask, address))
                                return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Given a specific local address, remote address and IPv4 subnet mask, Return true if the network parts match.
        /// </summary>
        /// <param name="thisAddress"></param>
        /// <param name="ipv4mask"></param>
        /// <param name="remoteAddress"></param>
        /// <returns></returns>
        private bool matchesLocalSubnet(IPAddress thisAddress, IPAddress ipv4mask, IPAddress remoteAddress) {
            if (ipv4mask == null)
                return thisAddress.Equals(remoteAddress);

            if ((IPAddressToUInt(thisAddress) & IPAddressToUInt(ipv4mask)) ==
                (IPAddressToUInt(remoteAddress) & IPAddressToUInt(ipv4mask)))
                return true;
            return false;
        }

        /// <summary>
        /// Determine if a IPv4 address is in one of the private address ranges, eg. NAT'ed. (RFC1918)
        /// </summary>
        /// <param name="ep"></param>
        /// <returns></returns>
        private bool isPrivateSubnet(IPAddress address) {
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
            byte[] ba = address.GetAddressBytes();
            if (ba.Length != 4)
                return 0;
            Array.Reverse(ba);
            return BitConverter.ToUInt32(ba, 0);
        }

        private BroadcastMessage deserialize( byte[] buf ) {
            MemoryStream ms = new MemoryStream( buf );
            BroadcastMessage message = null;
#if GENERIC_SERIALIZATION
            try {
                SerializedPacket p = new SerializedPacket( ms );
                message = (BroadcastMessage)PacketTypes.DecodeMessage(null, p);
            } catch( Exception e ) {
                Trace.WriteLine( e.ToString(), this.GetType().ToString() );
                return null;
            }
#else
            BinaryFormatter bf = new BinaryFormatter();
            try {
                message = (BroadcastMessage)bf.Deserialize( ms );
            } catch( SerializationException se ) {
                Trace.WriteLine( "Failed to deserialize a BroadcastMessage: " + se.Message, this.GetType().ToString() );
                return null;
            } catch( Exception e ) {
                Trace.WriteLine( e.ToString(), this.GetType().ToString() );
                return null;
            }
#endif
            return message;
        }

        #region IDisposable Members

        ~BroadcastListener() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            this.m_Disposed = true;
            if (disposing) {
                UdpClient uc = Interlocked.Exchange(ref m_Listener, null);
                if (uc != null) {
                    try {
                        uc.Close();
                    }
                    catch { }
                }
            }
        }

        #endregion

        [Published]
        public InstructorAdvertisementCollection InstructorAdvertisements {
            get { return this.m_InstructorAdvertisements; }
        }

        public class InstructorAdvertisementCollection : PropertyCollectionBase {
            internal InstructorAdvertisementCollection(PropertyPublisher owner, string property)
                : base(owner, property) { }

            public InstructorAdvertisement this[int index] {
                get { return ((InstructorAdvertisement)List[index]); }
                set { List[index] = value; }
            }

            public int Add(InstructorAdvertisement value) {
                return List.Add(value);
            }

            public int IndexOf(InstructorAdvertisement value) {
                return List.IndexOf(value);
            }

            public void Insert(int index, InstructorAdvertisement value) {
                List.Insert(index, value);
            }

            public void Remove(InstructorAdvertisement value) {
                List.Remove(value);
            }

            public bool Contains(InstructorAdvertisement value) {
                return List.Contains(value);
            }

            protected override void OnValidate(Object value) {
                if (!typeof(InstructorAdvertisement).IsInstanceOfType(value))
                    throw new ArgumentException("Value must be of type InstructorAdvertisement.", "value");
            }
        }
    }

    /// <summary>
    /// Contain one IPEndpoint, a human name, and the overrides to make it function as a 
    /// key in a Hashtable.
    /// </summary>
    public class InstructorAdvertisement {
        public IPEndPoint Endpoint;
        public String Name;
        public String PresentationName;
        public DateTime Timestamp;
        public bool Multicast;
        public Guid InstructorID;
        public bool ShowIP;

        public InstructorAdvertisement(IPEndPoint endpoint, string name, string presName, Guid instructorId, bool showIP) {
            this.Endpoint = endpoint;
            this.Name = name;
            this.PresentationName = presName;
            this.InstructorID = instructorId;
            this.ShowIP = showIP;
            this.Timestamp = DateTime.Now;
            this.Multicast = false;
        }

        public override string ToString() {
            if (ShowIP) {
                return Name + " - " + PresentationName + ": " + Endpoint.ToString();
            }
            else {
                return Name + " - " + PresentationName;
            }
        }

        public string HumanName {
            get { return Strings.JoinClassroom + this.ToString(); }
        }


        /// <summary>
        /// The name and Guid do not participate in the generation of hashcode
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode() {
            return Endpoint.GetHashCode();
        }

        /// <summary>
        /// Treat all instances with the same endpoint as equal
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj) {
            InstructorAdvertisement ie = obj as InstructorAdvertisement;
            return Endpoint.Equals(ie.Endpoint);
        }
    
    }
}
