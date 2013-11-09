using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Net.NetworkInformation;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Windows.Forms;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Model;


namespace UW.ClassroomPresenter.Network.Broadcast {
    class BroadcastSender : IDisposable {
        private Thread m_SendThread;
        private byte[] m_MessageBuffer;
        private int m_MessageBufferLength;
        private bool m_Disposed;
        private Socket m_Socket;
        private IPAddress m_LocalAddress;
        IPEndPoint m_BroadcastEP;
        IPEndPoint m_ServerEP;
        Guid m_ServerID;
        PresenterModel m_Model;
        PresentationModel m_Presentation;
        private String m_PresentationName;
        private String m_ParticipantName;
        private readonly IDisposable m_CurrentPresentationChangedDispatcher;
        private bool m_showIP;
        private bool m_BroadcastDisabled;

        /// <summary>
        /// Control access to m_MessageBuffer; avoid interleaving Update and Send operations.
        /// </summary>
        private object m_BufferSyncObject;

        public static int ListenPort = 11303; //TODO: probably shouldn't be hard-coded.

        //public BroadcastSender(BroadcastMessage message) {
        public BroadcastSender(IPEndPoint serverEP, PresenterModel model, Guid serverID)
        {
            m_ServerEP = serverEP;
            m_Model = model;
            m_ServerID = serverID;
            m_Presentation = null;
            m_PresentationName = "Untitled Presentation";
            m_BufferSyncObject = new object();

            //The user can manually disable the broadcast for security/privacy reasons.
            m_Model.ViewerState.Changed["BroadcastDisabled"].Add(new PropertyEventHandler(OnBroadcastDisabledChanged));
            using (Synchronizer.Lock(m_Model.ViewerState.SyncRoot)) {
                m_BroadcastDisabled = m_Model.ViewerState.BroadcastDisabled;
            }

            //Get the initial Participant name. This probably never changes.
            m_Model.Participant.Changed["HumanName"].Add(new PropertyEventHandler(OnParticipantNameChanged));
            using (Synchronizer.Lock(m_Model.Participant.SyncRoot)) {
                m_ParticipantName = m_Model.Participant.HumanName;
            }

            m_Model.ViewerState.Changed["ShowIP"].Add(new PropertyEventHandler(OnShowIPChanged));
            using (Synchronizer.Lock(m_Model.ViewerState.SyncRoot)) {
               this.m_showIP = m_Model.ViewerState.ShowIP;
            }



            //Construct an initial broadcast message.
            UpdateMessage();

            //When OnPresentationChange occurs we will update the broadcast message and subscribe the HumanName property of the new
            //PresentationModel.  When the PresentationModel.HumanName changes we will also update the broadcast message.
            ThreadEventQueue eventQueue = new ThreadEventQueue();
            this.m_CurrentPresentationChangedDispatcher = this.m_Model.Workspace.CurrentPresentation.ListenAndInitialize(eventQueue, OnPresentationChanged);

            m_LocalAddress = GetLocalAddress();

            initSocket();

            //if init failed..
            if (m_Socket == null) {
                return;
            }

            /// Sending to either broadcast address works.  Probably we prefer IPAddress.Broadcast
            /// because this way one broadcast works for multiple interfaces, and it should
            /// also work on IPv6 networks.
            IPAddress broadcast = IPAddress.Broadcast; //this is 255.255.255.255 for ipv4.
            if (BroadcastManager.UseIPv6) {
                broadcast = BroadcastManager.IPv6LinkLocalMulticast;
            }
            m_BroadcastEP = new IPEndPoint(broadcast, BroadcastSender.ListenPort);

            m_SendThread = new Thread(new ThreadStart(SendThread));
            m_SendThread.Start();
        }

        private void OnParticipantNameChanged(object sender, PropertyEventArgs args) {
            if (args is PropertyChangeEventArgs) {
                PropertyChangeEventArgs pcargs = (PropertyChangeEventArgs)args;
                this.m_ParticipantName = "Anonymous Instructor";
                if (pcargs.NewValue != null)
                    this.m_ParticipantName = (string)pcargs.NewValue;
            }
            UpdateMessage();            
        }

        private void OnShowIPChanged(object sender, PropertyEventArgs args) {
            using (Synchronizer.Lock(m_Model.ViewerState.SyncRoot)) {
                this.m_showIP = m_Model.ViewerState.ShowIP;
            }
            UpdateMessage();
        }
        private void OnBroadcastDisabledChanged(object sender, PropertyEventArgs args) {
            if (args is PropertyChangeEventArgs) {
                PropertyChangeEventArgs pcargs = (PropertyChangeEventArgs)args;
                m_BroadcastDisabled = (bool)pcargs.NewValue;
            }
        }

        private void OnPresentationChanged(Property<PresentationModel>.EventArgs args) {
            //Remove old event handler if any
            if (this.m_Presentation != null) {
                this.m_Presentation.Changed["HumanName"].Remove(new PropertyEventHandler(OnPresentationNameChanged));
            }

            this.m_Presentation = args.New;

            //Update Presentation Name and add new event handler.
            if (m_Presentation != null) {
                using (Synchronizer.Lock(m_Presentation.SyncRoot)) {
                    m_PresentationName = m_Presentation.HumanName;
                }
                this.m_Presentation.Changed["HumanName"].Add(new PropertyEventHandler(OnPresentationNameChanged));
            }

            //Make a new broadcast message
            UpdateMessage();
        }

        private void OnPresentationNameChanged(object sender, PropertyEventArgs args) {
            if (args is PropertyChangeEventArgs) {
                PropertyChangeEventArgs pcargs = (PropertyChangeEventArgs)args;
                this.m_PresentationName = "Untitled Presentation";
                if (pcargs.NewValue != null)
                    this.m_PresentationName = (string)pcargs.NewValue;
            }

            //Update the broadcast message
            UpdateMessage();
        }        
        
        
        /// <summary>
        /// Update the broadcast message
        /// </summary>
        private void UpdateMessage() {
            lock (m_BufferSyncObject) {
                BroadcastMessage message = new BroadcastMessage(m_ServerEP, m_ParticipantName, m_ServerID, m_PresentationName, m_showIP);
                m_MessageBuffer = Serialize(message, out m_MessageBufferLength);
            }
        }

        private byte[] Serialize( Object o, out int length ) {
            MemoryStream ms = new MemoryStream();
#if GENERIC_SERIALIZATION
            if( o is IGenericSerializable ) {
                SerializedPacket p = ((IGenericSerializable)o).Serialize();
                p.WriteToStream( ms );
            } else {
                throw new Exception( "Object is not serializable" );
            }
#else
            BinaryFormatter bf = new BinaryFormatter();
            bf.Serialize( ms, o );
#endif
            ms.Position = 0;
            length = (int)ms.Length; //Note that the buffer length and the length of the valid data are not the same.
            return ms.GetBuffer();
        }

        /// <summary>
        /// Here we just pick a local address to bind to, preferring routable over NAT'ed or local private, and 
        /// preferring the higher speed interfaces.  
        /// </summary>
        /// <returns></returns>
        private IPAddress GetLocalAddress() {

            IPGlobalProperties computerProperties = IPGlobalProperties.GetIPGlobalProperties();
            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();

            if (nics == null || nics.Length < 1) {
                Trace.WriteLine("No network interfaces found.", this.GetType().ToString());
                return IPAddress.Loopback;
            }

            long speed = 0;
            IPAddress address = null;
            long privateSpeed = 0;
            IPAddress privateAddress = null;

            foreach (NetworkInterface adapter in nics) {
                if (adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback) {
                    continue;
                }

                IPInterfaceProperties properties = adapter.GetIPProperties();

                UnicastIPAddressInformationCollection uniCast = properties.UnicastAddresses;
                if (uniCast != null) {
                    foreach (UnicastIPAddressInformation uni in uniCast) {
                        if ((uni.Address.AddressFamily.Equals(AddressFamily.InterNetwork)) &&
                            (isPrivateSubnet(uni.Address))) {
                            if (adapter.Speed > privateSpeed) {
                                privateAddress = uni.Address;
                                privateSpeed = adapter.Speed;
                            }
                        }
                        else {
                            if (adapter.Speed > speed) {
                                address = uni.Address;
                                speed = adapter.Speed;
                            }
                        }
                    }
                }
            }

            if (address == null)
                if (privateAddress == null)
                    return IPAddress.Loopback;
                else
                    return privateAddress;
            else
                return address;

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


        private void initSocket() {
            m_Socket = null;
            try {
                AddressFamily af = AddressFamily.InterNetwork;
                if (BroadcastManager.UseIPv6) {
                    af = AddressFamily.InterNetworkV6;
                }
                m_Socket = new Socket(af, SocketType.Dgram, ProtocolType.Udp);
                //Not binding the socket to an endpoint is probably best since that way it won't except of the 
                //local endpoint changes mid-session.
                //Which local address we bind to doesn't matter that much.  It's just a convenience for network traces, etc.
                //IPEndPoint localEP = new IPEndPoint(m_LocalAddress, 0);
                //m_Socket.Bind(localEP);
                //Trace.WriteLine("Bound UDP broadcast sender to: " + localEP.ToString());
                m_Socket.EnableBroadcast = true;
            }
            catch (SocketException se) { 
                Trace.WriteLine("Failed to initialize UDP Broacast sending socket. " + se.ToString(), this.GetType().ToString());
                MessageBox.Show("Presenter failed to initialize the instructor advertisement sender.  Instructor advertisements will be " +
                    "disabled.  SocketException ErrorCode=" + se.ErrorCode.ToString() + ".  Exception text: \r\n" + 
                    se.ToString(), "Failed to Start Instructor Advertisement Sender",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                m_Socket = null;           
            }
            catch (Exception e) {
                Trace.WriteLine("Failed to initialize UDP Broacast sending socket. " + e.ToString(), this.GetType().ToString());
                MessageBox.Show("Presenter failed to initialize the instructor advertisement sender.  Instructor advertisements will be " +
                    "disabled.  Exception text: \r\n" + e.ToString(), "Failed to Start Instructor Advertisement Sender",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                m_Socket = null;
            }
        }

        private void SendThread() {
            int secondCounter = 1;
            while (!m_Disposed) {
                if (!m_BroadcastDisabled) {
                    Send();
                }
                for (int i = 0; ((i < 10) && (!m_Disposed)); i++) {
                    Thread.Sleep(100);
                }
                secondCounter++;

                //Update message occasionally in case our network endpoints change.  
                if ((!m_Disposed) && ((secondCounter % 5) == 0)) {
                    UpdateMessage();
                }
            }
        }

        private void Send() {
            lock (m_BufferSyncObject) {
                if (m_MessageBuffer == null) {
                    Trace.WriteLine("Message is null. Broadcast send ignored.", this.GetType().ToString());
                    return;
                }
                try {
                    m_Socket.SendTo(m_MessageBuffer, m_MessageBufferLength, SocketFlags.None, m_BroadcastEP);
                }
                catch (Exception e) {
                    Trace.WriteLine("Failed to send UDP Broadcast: " + e.ToString(), this.GetType().ToString());
                }
            }
        }

        #region IDisposable Members

        ~BroadcastSender() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            this.m_Disposed = true;
            if (disposing) {
                m_Model.ViewerState.Changed["BroadcastDisabled"].Remove(new PropertyEventHandler(OnBroadcastDisabledChanged));
                m_Model.Participant.Changed["HumanName"].Remove(new PropertyEventHandler(OnParticipantNameChanged));
                m_Model.ViewerState.Changed["ShowIP"].Remove(new PropertyEventHandler(OnShowIPChanged));
                this.m_CurrentPresentationChangedDispatcher.Dispose();
                if (m_Presentation != null) {
                    m_Presentation.Changed["HumanName"].Remove(new PropertyEventHandler(OnPresentationNameChanged));
                    m_Presentation = null;
                }
                if (m_Socket != null) {
                    m_Socket.Close();
                    m_Socket = null;
                }
            }
        }

        #endregion
    }
}
