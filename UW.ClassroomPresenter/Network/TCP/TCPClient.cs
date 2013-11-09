using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;
using UW.ClassroomPresenter.Network.Chunking;
using UW.ClassroomPresenter.Network.Groups;
using System.IO;
using System.Collections;
using System.Runtime.Serialization.Formatters.Binary;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Network.Messages;
using UW.ClassroomPresenter.Network.Broadcast;
using System.Net.NetworkInformation;


namespace UW.ClassroomPresenter.Network.TCP {

    /// <summary>
    /// Attempt to maintain a persistent TCP connection to the given server endpoint.  Accept messages to be sent,
    /// and enqueue received messages to be Read.
    /// </summary>
    class TCPClient : PropertyPublisher, IDisposable, ITCPSender, ITCPReceiver {

        #region Private Members
        /// <summary>
        /// If we do not receive any messages for this timeout, we assume the server connection is dead, disconnect,
        /// and begin trying to reconnect.  Note we need a server heartbeat period somewhat less than the timeout.
        /// (Using 5 seconds initially)
        /// </summary>
        private double TCP_HEARTBEAT_TIMEOUT_DEFAULT_MS = 12000;

        private TimeSpan m_ClientTimeout;
        private bool m_Disposed = false;
        private IPEndPoint m_RemoteEP;
        private Socket m_Socket;
        private Thread m_ReceiveThread;
        private Thread m_ConnectThread;
        private Thread m_UtilityThread;
        private NetworkStream m_NetworkStream;
        private readonly Chunk.ChunkEncoder m_Encoder;
        private Queue m_ReceiveQueue;
        private ParticipantModel m_Participant;
        private ParticipantModel m_ServerParticipant;
        private Guid m_ServerId;
        private NetworkModel m_Network;
        private DateTime m_LastMsgReceived;
        private bool m_BridgeEnabled;

        //Keep track of the last received chunk sequence numbers across multiple instances of TCPClient for reconnect optimizations
        private static ulong m_LastMsgSequence = 0;
        private static ulong m_LastChunkSequence = 0;

        //published property
        private bool m_Connected;
        private NetworkStatus m_NetworkStatus;

        #endregion Private Members

        #region Properties

        [Published]
        public bool Connected {
            get { return this.GetPublishedProperty("Connected", ref this.m_Connected); }
        }

        [Published]
        public NetworkStatus NetworkStatus {
            get { return this.GetPublishedProperty("NetworkStatus", ref m_NetworkStatus); }
        }

        public bool BridgeEnabled {
            get { return this.m_BridgeEnabled; }
        } 

        #endregion Properties

        #region Construct

        public TCPClient(IPEndPoint remoteEP, ParticipantModel participant, NetworkModel network, Guid serverID) {
            m_Connected = false;
            m_Network = network;
            m_Participant = participant;
            m_LastMsgReceived = DateTime.MaxValue;
            m_ServerId = serverID;
            m_ClientTimeout = TimeSpan.FromMilliseconds(TCP_HEARTBEAT_TIMEOUT_DEFAULT_MS);
            this.m_RemoteEP = remoteEP;
            m_Socket = null;
            m_ServerParticipant = null;
            m_ReceiveQueue = new Queue();
            this.m_Encoder = new Chunk.ChunkEncoder();
            m_NetworkStatus = new NetworkStatus(ConnectionStatus.Disconnected, ConnectionProtocolType.TCP, TCPRole.Client, 0);
            this.m_Network.RegisterNetworkStatusProvider(this, true, m_NetworkStatus);

            //Find out if client-side bridging is enabled
            m_BridgeEnabled = false;
            string enableBridge = System.Configuration.ConfigurationManager.AppSettings[this.GetType().ToString() + ".EnableBridge"];
            if (enableBridge != null) {
                bool enable = false;
                if (bool.TryParse(enableBridge, out enable)) {
                    Trace.WriteLine("Unicast to Multicast Bridge enabled=" + enable.ToString(), this.GetType().ToString());
                    m_BridgeEnabled = enable;
                }
            }

        }

        #endregion Construct

        #region Public Methods

        /// <summary>
        /// Start the client: attempt to connect.  Maintain the connection and receive inbound messages.
        /// </summary>
        public void Start()
        {
            using (Synchronizer.Lock(this.SyncRoot)) {
                NetworkStatus newStatus = m_NetworkStatus.Clone();
                newStatus.ConnectionStatus = ConnectionStatus.TryingToConnect;
                this.SetPublishedProperty("NetworkStatus", ref m_NetworkStatus, newStatus);
            }

            //A thread to establish and maintain the connection to the server
            m_ConnectThread = new Thread(new ThreadStart(ConnectThread));
            m_ConnectThread.Start();

            //A thread to receive inbound messages.
            m_ReceiveThread = new Thread(new ThreadStart(ReceiveThread));
            m_ReceiveThread.Start();

            //A thread for diagnostics
            m_UtilityThread = new Thread(new ThreadStart(UtilityThread));
            m_UtilityThread.Start();
        }

        /// <summary>
        /// If connected, send a message, otherwise do nothing.
        /// </summary>
        /// <param name="chunk"></param>
        /// <param name="group"></param>
        public void Send(Chunk chunk, Group group, MessageTags tags) {
            AsyncCallback ac = new AsyncCallback(SendCallback);
            using (MemoryStream stream = new MemoryStream((int)(this.m_Encoder.MaximumChunkSize * 2))) {
                stream.Position = 0;
                this.m_Encoder.EncodeChunk(stream, chunk);

                Trace.WriteLine(string.Format("TCPClient sending message #{0}, chunk #{1} of {2}, {3} bytes.",
                    chunk.MessageSequence,
                    chunk.ChunkSequenceInMessage + 1,
                    chunk.NumberOfChunksInMessage,
                    chunk.Data.Length),
                    this.GetType().ToString());

                byte[] buffer = stream.GetBuffer();
                int length = (int)stream.Length;

                if ((m_Socket.Connected) && (m_Connected))
                    while (true) {
                        try {
                            m_Socket.BeginSend(buffer, 0, length, SocketFlags.None, ac, new SendState(m_Socket, length)); //BeginSend since it won't block.
                            break;
                        }
                        catch (SocketException se) {
                            if (se.ErrorCode == 10055) {
                                Trace.WriteLine("Client Send queue is full.  Sleeping 50 mS", this.GetType().ToString());
                                Thread.Sleep(50);
                            }
                            else {
                                Trace.WriteLine("SendThread socket exception: " + se.ToString() + " Error code: " + se.ErrorCode.ToString(), this.GetType().ToString());
                                break;
                            }
                        }
                        catch (ObjectDisposedException ode) {
                           Trace.WriteLine("SendThread ObjectDisposedException: " + ode.Message, this.GetType().ToString());
                           break;
                        }
                        catch (Exception e) {
                            Trace.WriteLine("SendThread Exception: " + e.ToString(), this.GetType().ToString());
                            break;
                        }
                    }
                else {
                    Trace.WriteLine("SendThread found a socket disconnected. Send request ignored.", this.GetType().ToString());
                }
            }

        }

        /// <summary>
        /// return the next message we have received.
        /// </summary>
        /// <returns></returns>
        public object Read() {
            lock (m_ReceiveQueue) {
                if (m_ReceiveQueue.Count > 0) {
                    object o = m_ReceiveQueue.Dequeue();
                    return o;
                }
            }
            return null;
        }

        #endregion Public Methods

        #region Thread Methods

        /// <summary>
        /// Print heartbeat to log, and watch for connection timeouts.
        /// </summary>
        private void UtilityThread() {
            int secondCount = 0;
            while (!m_Disposed) {
                for (int i = 0; ((i < 10) && (!m_Disposed)); i++) {
                    Thread.Sleep(100);
                }
                if (m_Disposed) break;
                secondCount++;

                if (secondCount % 10 == 0) {
                    Trace.WriteLine(DateTime.Now.ToString(), this.GetType().ToString());
                }

                lock(m_ReceiveQueue) {
                    if (DateTime.Now - m_LastMsgReceived > m_ClientTimeout) {
                        Socket s = (Socket)Interlocked.Exchange(ref m_Socket, null);
                        Trace.WriteLine("TCPClient receive timeout exceeded.  Closing socket at " + DateTime.Now.ToString(), 
                            this.GetType().ToString());
                        if (s != null) {
                            s.Close();
                        }
                        m_LastMsgReceived = DateTime.MaxValue;
                    }
                }
            }
            Trace.WriteLine("UtilityThread ending. " + DateTime.Now.ToString(), this.GetType().ToString());
        }

        /// <summary>
        /// Maintain the connection to the server endpoint.
        /// </summary>
        private void ConnectThread() {
            
            while (!m_Disposed) {
                if ((m_Socket == null) || (!m_Socket.Connected) || (!m_Connected)) {
                    Socket s = (Socket)Interlocked.Exchange(ref m_Socket, null);
                    if (s != null) {
                        s.Close();
                    }

                    try {
                        if (m_Disposed) break;
                        using (Synchronizer.Lock(this.SyncRoot)) {
                            this.SetPublishedProperty("Connected", ref this.m_Connected, false);
                            NetworkStatus newStatus = m_NetworkStatus.Clone();
                            newStatus.ConnectionStatus = ConnectionStatus.TryingToConnect;
                            this.SetPublishedProperty("NetworkStatus", ref m_NetworkStatus, newStatus);
                        }
                        Trace.WriteLine("Attempting connection to server: " + this.m_RemoteEP.ToString(), this.GetType().ToString());
                        m_Socket = new Socket(this.m_RemoteEP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                        m_Socket.ReceiveTimeout = 20000; //Temporarily set to 20 seconds for the handshaking phase.
                        //In case the server endpoint changed:
                        UpdateRemoteEndpoint();
                        m_Socket.Connect(this.m_RemoteEP);
                        m_NetworkStream = new NetworkStream(m_Socket); //NetworkStream does not own the socket.

                        //Receive handshake
                        BinaryFormatter bf = new BinaryFormatter();
#if GENERIC_SERIALIZATION
                        IGenericSerializable o = PacketTypes.DecodeMessage( null, new SerializedPacket( m_NetworkStream ) );
#else
                        object o = bf.Deserialize(m_NetworkStream);
#endif
                        if (o is TCPHandshakeMessage) {
                            Trace.WriteLine("Handshake received from " + ((TCPHandshakeMessage)o).ParticipantId.ToString() + " ep=" + ((TCPHandshakeMessage)o).EndPoint.ToString());
                            //send a handshake
                            TCPHandshakeMessage handshake = new TCPHandshakeMessage(m_Participant, (IPEndPoint)m_Socket.LocalEndPoint);
                            lock (this.m_ReceiveQueue) {
                                //If this is a reconnect, these values tell the server where we left off
                                handshake.LastMessageSequence = m_LastMsgSequence;
                                handshake.LastChunkSequence = m_LastChunkSequence;
                            }
                            MemoryStream ms = new MemoryStream();
#if GENERIC_SERIALIZATION
                            handshake.Serialize().WriteToStream( ms );
#else
                            bf.Serialize(ms, handshake);
#endif
                            m_NetworkStream.Write(ms.GetBuffer(), 0, (int)ms.Length);
                            Trace.WriteLine("Handshake sent.", this.GetType().ToString());
                            //The first time we connect to a server we create a new ParticipantModel to represent the server.
                            if (m_ServerParticipant == null) {
                                TCPHandshakeMessage h = (TCPHandshakeMessage)o;
                                m_ServerId = h.ParticipantId;
                                m_ServerParticipant = new ParticipantModel(m_ServerId, h.HumanName);
                            }
                            else {
                                //In reconnect scenarios we keep the same server ParticipantModel, but the Guid could 
                                //change if the server was restarted.  In this case we just want to update the Guid.
                                //Notice that we can't create a new ParticipantModel here without breaking some things.
                                if (!m_ServerId.Equals(((TCPHandshakeMessage)o).ParticipantId)) {
                                    m_ServerId = ((TCPHandshakeMessage)o).ParticipantId;
                                    m_ServerParticipant.Guid = m_ServerId;
                                }
                            }
                        }
                        else {
                            throw new ApplicationException("Invalid handshake received: " + o.GetType().ToString());
                        }

                        m_Socket.ReceiveTimeout = 0; //Reset socket to infinite timeout.
                        m_ClientTimeout = SetClientTimeout();

                        using (Synchronizer.Lock(this.SyncRoot)) {
                            //Setting this property allows the ReceiveThread to begin:
                            this.SetPublishedProperty("Connected", ref this.m_Connected, true);
                            NetworkStatus newStatus = m_NetworkStatus.Clone();
                            newStatus.ConnectionStatus = ConnectionStatus.Connected;
                            this.SetPublishedProperty("NetworkStatus", ref m_NetworkStatus, newStatus);
                        }

                        lock (this.m_ReceiveQueue) {
                            //This enables the client timeout:
                            this.m_LastMsgReceived = DateTime.Now;
                        }

                        Trace.WriteLine("Connected.", this.GetType().ToString());
                    }
                    catch (SocketException se) {
                        if (se.ErrorCode == 10060) {
                            Trace.WriteLine("ConnectThread SocketException 10060: remote host failed to respond.");
                        }
                        else if (se.ErrorCode == 10038) {
                            Trace.WriteLine("ConnectThread SocketException 10038: operation attempted on non-socket.");
                        }
                        else {
                            Trace.WriteLine("ConnectThread SocketException " + se.ErrorCode.ToString() + ": " + se.ToString());
                        }
                    }
                    catch (IOException ioe) {
                        Trace.WriteLine("ConnectThread IOException: " + ioe.Message);
                        if ((ioe.InnerException != null) && (ioe.InnerException is SocketException)) {
                            Trace.WriteLine("  InnerException: SocketException " + ((SocketException)ioe.InnerException).ErrorCode.ToString());
                        }
                    }
                    catch (Exception e) {
                        Trace.WriteLine("ConnectThread exception: " + e.ToString());
                    }
                }

                for (int i=0; ((i<10) && (!m_Disposed)); i++)
                    Thread.Sleep(100);
            }
            Trace.WriteLine("ConnectThread is ending.", this.GetType().ToString());
        }

        /// <summary>
        /// Attempt to adjust client timeout based on link speed.
        /// </summary>
        /// <returns></returns>
        private TimeSpan SetClientTimeout() {
            if ((m_Socket != null) && (m_Socket.Connected)){
                IPEndPoint ep = (IPEndPoint)m_Socket.LocalEndPoint;
                if (ep != null) {
                    NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
                    if (nics == null || nics.Length < 1) {
                        return TimeSpan.FromMilliseconds(this.TCP_HEARTBEAT_TIMEOUT_DEFAULT_MS);
                    }
                    foreach (NetworkInterface adapter in nics) {
                        IPInterfaceProperties properties = adapter.GetIPProperties();
                        UnicastIPAddressInformationCollection uniCast = properties.UnicastAddresses;
                        if (uniCast != null) {
                            foreach (UnicastIPAddressInformation uni in uniCast) {
                                if (uni.Address.Equals(ep.Address)) { 
                                    //found address match for this nic
                                    Trace.WriteLine("Detected link speed: " + adapter.Speed.ToString(), this.GetType().ToString());
                                    if (adapter.Speed <= 11000000) {
                                        Trace.WriteLine("Increasing receive timeout due to slow link speed.", this.GetType().ToString());
                                        return TimeSpan.FromMilliseconds(this.TCP_HEARTBEAT_TIMEOUT_DEFAULT_MS * 2);
                                    }
                                    else
                                        return TimeSpan.FromMilliseconds(this.TCP_HEARTBEAT_TIMEOUT_DEFAULT_MS);
                                }
                            }
                        }
                    }
                }
            }
            return TimeSpan.FromMilliseconds(this.TCP_HEARTBEAT_TIMEOUT_DEFAULT_MS);
        }

        /// <summary>
        /// If the server IP address changes during a session, we use the broadcast to try to adjust.
        /// If the server broadcast does not reach our subnet, and the IP address changes, we won't be
        /// able to reconnect.
        /// </summary>
        private void UpdateRemoteEndpoint() {
            if (m_ServerId.Equals(Guid.Empty))
                return;

            IPEndPoint[] endpoints = BroadcastListener.GuidToEndPoints(m_ServerId);
            
            if (endpoints.Length == 0)
                return;

            foreach (IPEndPoint ep in endpoints) {
                if (m_RemoteEP.Equals(ep)) {
                    //Old endpoint is still valid
                    return;
                }
            }

            if (endpoints.Length > 1)
                Trace.WriteLine("Warning: Updating server endpoint by arbitrarily picking the first one of " + 
                    endpoints.Length.ToString(), this.GetType().ToString());

            m_RemoteEP = endpoints[0];
            Trace.WriteLine("TCPClient updating server endpoint to: " + m_RemoteEP.ToString(), this.GetType().ToString());
        }


        /// <summary>
        /// Receive and enqueue messages from the server endpoint.
        /// </summary>
        private void ReceiveThread() {
            BinaryFormatter binaryFormatter = new BinaryFormatter();
#if GENERIC_SERIALIZATION
            IGenericSerializable msg = null;
#else
            object msg = null;
#endif

            while (!m_Disposed) {
                if ((m_Socket != null) && (m_Socket.Connected) && (m_Connected)) {

                    try {
#if GENERIC_SERIALIZATION
                        msg = PacketTypes.DecodeMessage( null, new SerializedPacket( m_NetworkStream ) );
#else
                        msg = binaryFormatter.Deserialize(m_NetworkStream);  //blocks
#endif
                    }
                    catch (Exception e) {
                        if (e.InnerException is SocketException) {
                            if (((SocketException)e.InnerException).ErrorCode == 10053) {
                                Trace.WriteLine("SocketException 10053: Read aborted by local host.");
                            }
                            else if (((SocketException)e.InnerException).ErrorCode == 10054) {
                                Trace.WriteLine("SocketException 10054: Read aborted.  Remote connection lost.");
                            }
                            else {
                                Trace.WriteLine("ReceiveThread SocketException while reading.  ErrorCode=" + ((SocketException)e.InnerException).ErrorCode.ToString());
                            }
                        }
                        else {
                            Trace.WriteLine("ReceiveThread failed to read: " + e.ToString());
                        }

                        continue;
                    }

                    lock (m_ReceiveQueue) {
                        this.m_LastMsgReceived = DateTime.Now;
                        if (!(msg is TCPHeartbeatMessage)) {
                            m_ReceiveQueue.Enqueue(new ReceiveMessageAndParticipant(msg, m_ServerParticipant));
                            Chunk c = (msg as Chunk);
                            if (c != null) {
                                m_LastMsgSequence = c.MessageSequence;
                                m_LastChunkSequence = c.ChunkSequenceInMessage;
                            }
                        }
                        else {
                            Trace.WriteLine("TCPClient received heartbeat message at " + DateTime.Now.ToString(), this.GetType().ToString());
                        }
                    }
                }
                else {
                    Thread.Sleep(100);
                }
            }
            Trace.WriteLine("Receive Thread is ending.", this.GetType().ToString());
        }

        #endregion Thread Methods

        #region Callback Methods

        private void SendCallback(IAsyncResult ar) {
            try {
                SendState state = (SendState)ar.AsyncState;
                int len = state.SendSocket.EndSend(ar);
                if (len != state.BufferLength) {
                    Trace.WriteLine("!!!Critical error: Client send operation failed.  Buffer length = " + state.BufferLength.ToString() +
                        "; bytes sent = " + len.ToString(), this.GetType().ToString());
                }
            }
            catch (SocketException se) {
                Trace.WriteLine("SendCallback SocketException errorcode: " + se.ErrorCode.ToString(), this.GetType().ToString());
            }
            catch (ObjectDisposedException ode) {
                Trace.WriteLine("SendCallback: " + ode.Message, this.GetType().ToString());
            }
            catch (Exception e) {
                Trace.WriteLine("SendCallback: " + e.ToString(), this.GetType().ToString());
            }
        }

        #endregion Callback Methods

        #region SendState Class

        private class SendState {
            public Socket SendSocket;
            public int BufferLength;

            public SendState(Socket s, int bufLen) {
                this.SendSocket = s;
                this.BufferLength = bufLen;
            }
        }

        #endregion SendState Class

        #region IDisposable Members

        ~TCPClient() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            this.m_Disposed = true;
            if (disposing) {
                m_Network.RegisterNetworkStatusProvider(this, false, null);
                Socket s = (Socket)Interlocked.Exchange(ref m_Socket, null);
                if (s != null) {
                    s.Close();
                }
            }
        }

        #endregion

    }
}
