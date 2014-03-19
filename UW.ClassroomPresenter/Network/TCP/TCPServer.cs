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
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Collections;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Network.Messages;
using System.Windows.Forms;
using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Network;
using UW.ClassroomPresenter.Misc;
using Microsoft.Win32;

namespace UW.ClassroomPresenter.Network.TCP {
    public class TCPServer : PropertyPublisher, IDisposable, ITCPSender, ITCPReceiver  {
        #region Public Static

        public static readonly int DefaultPort = 5664;
        public static int TCPListenPort = DefaultPort; 

        #endregion Public Static

        #region Private Members

        private const int HEARTBEAT_PERIOD = 5;     //How often (in seconds) to send a heartbeat message to clients

        //Queues with disconnected sockets cleaned up after this many minutes.  
        //10 is the default, but the value can be set in the registry.
        private int m_QueueTimeout = 10;

        private TcpListener m_Listener;
        private TCPServerSender m_ServerSender;
        private readonly IPEndPoint m_ListenEP;     //Local listener endpoint
        private Hashtable m_AllClients;             //Key is Participant Guid, value is ClientData
        private Queue m_ReceiveQueue;               //Queue for inbound messages
        private bool m_Disposed = false;
        private readonly Chunk.ChunkEncoder m_Encoder;
        private ManualResetEvent m_ClientConnected;
        private ParticipantModel m_Participant;
        private TCPMessageSender m_Sender;
        private ClassroomModel m_Classroom;
        private int m_ClientCount;
        private NetworkStatus m_NetworkStatus;
        private NetworkModel m_Network;
#if RTP_BUILD
        private UnicastToMulticastBridge m_U2MBridge;
#endif
        #endregion Private Members

        #region Public Properties

        /// <summary>
        /// This is the endpoint the server will listen, or is listening on.
        /// </summary>
        public IPEndPoint ServerEndPoint {
            get { return m_ListenEP;  }
        }

        [Published]
        public NetworkStatus NetworkStatus { 
            get { return this.GetPublishedProperty("NetworkStatus", ref m_NetworkStatus); }
        }

        [Published]
        public int ClientCount {
            get { return this.GetPublishedProperty("ClientCount", ref m_ClientCount); }
        }

        #endregion Public Properties

        #region Construct

        public TCPServer(PresenterModel model, ClassroomModel classroom, InstructorModel instructor) : 
            this(null,model,classroom,instructor) {}

        public TCPServer(IPEndPoint localEP, PresenterModel model, ClassroomModel classroom,
            InstructorModel instructor) {
            string portStr = System.Configuration.ConfigurationManager.AppSettings[this.GetType().ToString() + ".TCPListenPort"];
            int p;
            if (Int32.TryParse(portStr, out p)) {
                TCPListenPort = p;
            }
            RestoreConfig();
            m_ClientConnected = new ManualResetEvent(false);
            m_Participant = model.Participant;
            m_Classroom = classroom;
            m_ClientCount = 0;
            this.m_Network = model.Network;
            m_NetworkStatus = new NetworkStatus(ConnectionStatus.Disconnected, ConnectionProtocolType.TCP, TCPRole.Server, 0);
            this.m_Network.RegisterNetworkStatusProvider(this, true, m_NetworkStatus);

            if (localEP != null) {
                this.m_ListenEP = localEP;
            }
            else {
                IPAddress ip = IPAddress.Any;
                //Use IPv4 unless it is unavailable.  TODO: Maybe this should be a configurable parameter?
                if ((!Socket.OSSupportsIPv4) && (Socket.OSSupportsIPv6)) {
                    ip = IPAddress.IPv6Any;
                }
                this.m_ListenEP = new IPEndPoint(ip, TCPListenPort); 
            }

            m_AllClients = new Hashtable();
            m_ReceiveQueue = new Queue();

            this.m_Encoder = new Chunk.ChunkEncoder();
            this.m_ServerSender = new TCPServerSender(instructor);

            //Start bridging if config file says so
#if RTP_BUILD
            m_U2MBridge = null;
            string enableBridge = System.Configuration.ConfigurationManager.AppSettings[this.GetType().ToString() + ".EnableBridge"];
            if (enableBridge != null) {
                bool enable = false;
                if (bool.TryParse(enableBridge, out enable) && enable) {
                    Trace.WriteLine("Unicast to Multicast Bridge enabled.", this.GetType().ToString());
                    m_U2MBridge = new UnicastToMulticastBridge(model);
                }
            }
#endif
        }
        #endregion Construct

        #region Private Methods

        /// <summary>
        /// Restore config stored in registry
        /// </summary>
        private void RestoreConfig() {
            RegistryKey baseKey = Registry.CurrentUser.OpenSubKey(RegistryService.m_szRegistryString, true);
            if (baseKey == null) {
                baseKey = Registry.CurrentUser.CreateSubKey(RegistryService.m_szRegistryString);
                baseKey.SetValue("QueueTimeout",m_QueueTimeout.ToString());
                return;
            }

            object result = baseKey.GetValue("QueueTimeout");
            if (result == null) {
                baseKey.SetValue("QueueTimeout", m_QueueTimeout.ToString());
                return;
            }

            int tmp;
            if (Int32.TryParse(result.ToString(), out tmp)) {
                if (tmp > 0) {
                    this.m_QueueTimeout = tmp;
                    Trace.WriteLine("TCPServer setting queue timeout to " + m_QueueTimeout.ToString() + " minutes.", this.GetType().ToString());
                    return;
                }
            }

        }
        #endregion Private Methods

        #region Public Methods

        /// <summary>
        /// Start accepting client connections
        /// </summary>
        /// <param name="sender"></param>
        public void Start(TCPMessageSender sender) {
            m_Sender = sender;
            // Open a TcpListener to listen for incoming client connections.
            int standardPort = m_ListenEP.Port;
            for (; ; m_ListenEP.Port++) {
                try {
                    this.m_Listener = new TcpListener(m_ListenEP);
                    this.m_Listener.Start();
                }
                catch (SocketException se) {
                    // If the server was unable to start because another local process
                    // is already using the specified port number, then retry with a
                    // different port.
                    if (se.ErrorCode == 10048)  //10048: Address already in use
                    {
                        Trace.WriteLine("Warning: Server could not start on requested port.  The port is already in use: " + m_ListenEP.Port.ToString(), this.GetType().ToString());
                        continue;
                    }
                    else {
                        Trace.WriteLine("!!!Failed to start TCP Listener: " + se.ToString(), this.GetType().ToString());
                        MessageBox.Show("Unexpected error: Failed to start the TCP Listener.  " +
                            "SocketException ErrorCode=" + se.ErrorCode.ToString() + ". \r\n" +
                            "Exception text: " + se.ToString(), "Failed to Start Listener", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
                catch (Exception e) { 
                    Trace.WriteLine("!!!Failed to start TCP Listener: " + e.ToString(), this.GetType().ToString());
                    MessageBox.Show("Unexpected error: Failed to start the TCP Listener.  \r\n" +
                        "Exception text: " + e.ToString(), "Failed to Start Listener", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return; 
                }
                Trace.WriteLine("Started TCPListener on " + m_ListenEP.ToString(), this.GetType().ToString());
                
                //Warn if the port number is not the standard one.
                if (m_ListenEP.Port != standardPort) { 
                    MessageBox.Show(Strings.NonStandardMannulConnect + m_ListenEP.Port.ToString(), Strings.UsingNonStandardPort, MessageBoxButtons.OK, MessageBoxIcon.Warning);             
                }
                break;
            }

            //A thread to listen for new client connections
            Thread thread = new Thread(new ThreadStart(this.ListenThread));
            thread.Name = "TCPServer.ListenThread: " + m_ListenEP.ToString();
            thread.Start();

            //A thread to clean up resources when clients leave us
            Thread maintenanceThread = new Thread(new ThreadStart(MaintenanceThread));
            maintenanceThread.Name = "TCPServer.MaintenanceThread";
            maintenanceThread.Start();

            //Refresh NetworkStatus
            using (Synchronizer.Lock(this.SyncRoot)) {
                NetworkStatus newStatus = m_NetworkStatus.Clone();
                newStatus.ConnectionStatus = ConnectionStatus.Connected;
                this.SetPublishedProperty("NetworkStatus", ref m_NetworkStatus, newStatus);
            }
            
        }

        /// <summary>
        /// Serialize the chunk then enqueue a send operation for each client socket for which there is a 
        /// participant membership in the receivers group.
        /// </summary>
        /// <param name="chunk"></param>
        /// <param name="receivers"></param>
        public void Send(Chunk chunk, Group receivers, MessageTags tags) {
            using (MemoryStream stream = new MemoryStream((int)(this.m_Encoder.MaximumChunkSize * 2))) {
                stream.Position = 0;
                this.m_Encoder.EncodeChunk(stream, chunk);
                byte[] buffer = stream.GetBuffer();
                int length = (int)stream.Length;

                //Unicast to multicast bridge
#if RTP_BUILD
                if ((this.m_U2MBridge != null) && !(receivers is SingletonGroup)) {
                    this.m_U2MBridge.Send(buffer,length,tags);
                }
#endif
                lock (m_AllClients) {
                    foreach (ClientData client in m_AllClients.Values) {
                        if (client.Participant != null) {
                            if (!client.Participant.Groups.Contains(receivers)) {
                                if (receivers == Group.AllParticipant) {
                                    //Should never happen
                                    Trace.WriteLine("!!!Participant has lost membership in 'All Participants' (sending anyway). Client= " + client.Id.ToString(), this.GetType().ToString());
                                    if (client.Socket != null)
                                        Trace.WriteLine("    Socket endpoint=" + client.Socket.RemoteEndPoint.ToString());
                                }
                                else {
                                    //Ignoring participant (no group match)
                                    if (receivers is SingletonGroup) {
                                        //Trace.WriteLine("Ignoring participant; no group match for singleton group; participant= " + client.Id.ToString(), this.GetType().ToString());
                                    }
                                    else
                                        Trace.WriteLine("Ignoring participant; no group match for group: " + receivers.FriendlyName + "; participant= " + client.Id.ToString(), this.GetType().ToString());
                                    continue;
                                }
                            }
                        }
                        else {
                            //This probably shouldn't ever happen
                            Trace.WriteLine("Failed to find a participant for a connected socket.", this.GetType().ToString());
                            continue;
                        }
                        m_ServerSender.Send(buffer, length, client, tags, chunk.MessageSequence, chunk.ChunkSequenceInMessage,false);
                    }
                }
            }
        }
        

        /// <summary>
        /// Return the next received message or null if the receive queue is empty.
        /// This does not block.
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
        /// Close disconnected client sockets, send heartbeat messages and do other assorted cleanup.
        /// </summary>
        private void MaintenanceThread() {
            //Prepare the heartbeat message
            TCPHeartbeatMessage heartbeat = new TCPHeartbeatMessage();
            BinaryFormatter bf = new BinaryFormatter();
            MemoryStream hbms = new MemoryStream();
#if GENERIC_SERIALIZATION
            heartbeat.Serialize().WriteToStream( hbms );
#else
            bf.Serialize(hbms, heartbeat);
#endif
            byte[] hbbuf = hbms.GetBuffer();
            int hblen = (int)hbms.Length;
            MessageTags hbtags = new MessageTags();

            //Lists for socket maintenance
            List<Guid> toCleanup = new List<Guid>();
            List<Socket> toClose = new List<Socket>();
            int secondCounter = 0;
            int connectedClientCount = 0;

            while (!this.m_Disposed) {
                //iterate approximately once per second
                for (int i = 0; ((i < 10) && (!m_Disposed)); i++) {
                    Thread.Sleep(100);
                }
                if (this.m_Disposed) break;               
                secondCounter++;

                lock (m_AllClients) {
                    //Close and remove disconnected sockets. 
                    toCleanup.Clear();
                    toClose.Clear();
                    connectedClientCount = 0;
                    foreach (ClientData client in m_AllClients.Values) {
                        if (client.ConnectionState == ConnectionState.Disconnected) {
                            //Check for disconnected clients that have timed out
                            if (client.Timeout < DateTime.Now) {
                                this.m_ServerSender.RemoveQueue(client.Id);
                                toCleanup.Add(client.Id);
                            }
                        }
                        else if (client.ConnectionState == ConnectionState.Connected) {
                            //Find sockets to close for any new disconnects
                            if (!client.Socket.Connected) {
                                toClose.Add(client.Socket);
                                client.Socket = null;
                                client.ConnectionState = ConnectionState.Disconnected;
                                client.Timeout = DateTime.Now + TimeSpan.FromMinutes(m_QueueTimeout);
                            }
                            else {
                                connectedClientCount++;
                                //Send a heartbeat message to connected clients
                                if (secondCounter % HEARTBEAT_PERIOD == 0) {
                                    //Trace.WriteLine("TCPServer sending Heartbeat to client ID " + client.Id.ToString(), this.GetType().ToString());
                                    this.m_ServerSender.Send(hbbuf, hblen, client, hbtags,0,0,true);
                                }
                            }
                        }
                        else if (client.ConnectionState == ConnectionState.Reconnecting) {
                            //This is a temporary state used during handshaking
                            connectedClientCount++;
                        }
                    }

                    //Remove timed out clients from the master list
                    foreach (Guid goner in toCleanup) {
                        m_AllClients.Remove(goner);
                    }
                }

                //Update published client count
                if (m_ClientCount != connectedClientCount) {
                    using (Synchronizer.Lock(this.SyncRoot)) {
                        this.SetPublishedProperty("ClientCount", ref m_ClientCount, connectedClientCount);
                        NetworkStatus newStatus = m_NetworkStatus.Clone();
                        newStatus.ClientCount = m_ClientCount;
                        this.SetPublishedProperty("NetworkStatus", ref m_NetworkStatus, newStatus);
                    }
                }

                //Close sockets
                foreach (Socket s in toClose) {
                    try {
                        Trace.WriteLine("MaintenanceThread closing disconnected socket: " + s.RemoteEndPoint.ToString(), this.GetType().ToString());
                        s.Close();
                    }
                    catch (Exception e) {
                        Trace.WriteLine("MaintenanceThread exception while closing socket: " + e.ToString(), this.GetType().ToString()); 
                    }
                }

                if (toCleanup.Count > 0)
                    Trace.WriteLine("MaintenanceThread timed out " + toCleanup.Count.ToString() + " disconnected socket(s).", this.GetType().ToString());

                //Print a heartbeat to the log just to make it easier to parse by eye.
                if (secondCounter%10 == 0) {
                    Trace.WriteLine(DateTime.Now.ToString(), this.GetType().ToString());
                }
            }

            Trace.WriteLine("MaintenanceThread is ending.", this.GetType().ToString());
        
        }

        /// <summary>
        /// We use one receive thread per socket
        /// </summary>
        /// <param name="o"></param>
        private void ReceiveThread(Object o) {
            ReceiveThreadArgs args = (ReceiveThreadArgs)o;
            NetworkStream ns = args.NetStream;
            ParticipantModel participant = args.Participant;
            Guid participantId = participant.Guid;
            EndPoint remoteEP = args.RemoteEndPoint;

            IFormatter binaryFormatter = new BinaryFormatter();
#if GENERIC_SERIALIZATION
            IGenericSerializable msg = null;
#else
            object msg = null;
#endif

            while (!this.m_Disposed) {
                try {
#if GENERIC_SERIALIZATION
                    msg = PacketTypes.DecodeMessage( null, new SerializedPacket( ns ) );
#else
                    msg = binaryFormatter.Deserialize(ns);  //blocks
#endif
                }
                catch (SerializationException se) {
                    Trace.WriteLine(se.ToString(), this.GetType().ToString());
                    break;
                }
                catch (Exception e) {
                    if (e.InnerException is SocketException) {
                        SocketException se = (SocketException)e.InnerException;
                        if (se.ErrorCode == 10054) {
                            Trace.WriteLine("ReceiveThread detected a disconnected client during NetworkStream.Read: " + remoteEP.ToString(), this.GetType().ToString());
                            break;
                        }
                        else if (se.ErrorCode == 10053) {
                            Trace.WriteLine("ReceiveThread: socket was closed by local host.", this.GetType().ToString());
                            break;
                        }
                        else if (se.ErrorCode == 10004) {
                            Trace.WriteLine("ReceiveThread: a blocking operation was interrupted.", this.GetType().ToString());
                            break;
                        }
                        else {
                            Trace.WriteLine("SocketException in ReceiveThread. remote Endpoint= " + remoteEP.ToString() + " " + se.ToString() + " error code: " + se.ErrorCode.ToString(), this.GetType().ToString());
                            break;
                        }
                    }
                    else {
                        Trace.WriteLine("Exception while reading: " + e.ToString(), this.GetType().ToString());
                        break;
                    }
                }

                lock (m_ReceiveQueue) {
                    m_ReceiveQueue.Enqueue(new ReceiveMessageAndParticipant(msg,participant));
                }
            }

            //Remove the participant from the classroom here.  Notice that the socket for this participant may still be open.
            using (Synchronizer.Lock(m_Classroom.SyncRoot)) {
                m_Classroom.Participants.Remove(participant);
            }

            Trace.WriteLine("ReceiveThread is ending for remote endpoint=" + remoteEP.ToString(), this.GetType().ToString());
        }

        /// <summary>
        /// Listen for new client connections
        /// </summary>
        private void ListenThread() {
            AsyncCallback ac = new AsyncCallback(AcceptSocketCallback);
            while (!this.m_Disposed) {
                try {
                    m_ClientConnected.Reset();
                    IAsyncResult ar = this.m_Listener.BeginAcceptSocket(ac, m_Listener); //non-blocking
                    m_ClientConnected.WaitOne();
                }
                catch (ObjectDisposedException ode) {
                    Trace.WriteLine("ListenThread: " + ode.Message, this.GetType().ToString());
                    break;
                }
                catch (SocketException se) {
                    Trace.WriteLine("SocketException in ListenThread: " + se.Message +
                        " ErrorCode=" + se.ErrorCode.ToString(), this.GetType().ToString());
                    break;
                }
                catch (Exception e) {
                    Trace.WriteLine(e.ToString(), this.GetType().ToString());
                    break;
                }
            }
            Trace.WriteLine("ListenThread is ending.", this.GetType().ToString());
        }

        #endregion Thread Methods

        #region Callback Methods

        /// <summary>
        /// When the ListenThread has a prospective new client on the line, here we attempt to establish the connection.
        /// </summary>
        /// <param name="ar"></param>
        private void AcceptSocketCallback(IAsyncResult ar) {
            Socket s = null;
            TcpListener tcpListener = (TcpListener)ar.AsyncState;

            try {
                s = tcpListener.EndAcceptSocket(ar);
            }
            catch (ObjectDisposedException ode) {
                Trace.WriteLine("AcceptSocketCallback ObjectDisposedException" + ode.Message, this.GetType().ToString());
                return;
            }
            catch (SocketException se) {
                Trace.WriteLine("AcceptSocketCallback: " + se.ToString(), this.GetType().ToString());
                return;
            }
            catch (Exception e) {
                Trace.WriteLine(e.ToString(), this.GetType().ToString());
                return;
            }
            finally { 
                m_ClientConnected.Set();  //Let the ListenThread continue  
            }

            if (s != null) {
                try {
                    //Send a handshake
                    NetworkStream ns = new NetworkStream(s); //Here the network stream does not "own" the socket, so we have to close it explicitly.
                    BinaryFormatter bf = new BinaryFormatter();
                    MemoryStream ms = new MemoryStream();
                    TCPHandshakeMessage handshake = new TCPHandshakeMessage(this.m_Participant, new IPEndPoint(0, 0));
#if GENERIC_SERIALIZATION
                    handshake.Serialize().WriteToStream( ms );
#else
                    bf.Serialize(ms, handshake);
#endif
                    ns.Write(ms.GetBuffer(), 0, (int)ms.Length);
                    Trace.WriteLine("Handshake sent.", this.GetType().ToString());

                    //Receive a handshake
#if GENERIC_SERIALIZATION
                    IGenericSerializable o = PacketTypes.DecodeMessage( null, new SerializedPacket( ns ) );
#else
                    object o = bf.Deserialize(ns);
#endif

                    if (o is TCPHandshakeMessage) {
                        TCPHandshakeMessage h = (TCPHandshakeMessage)o;
                        Trace.WriteLine("Handshake received from " + h.ParticipantId.ToString() + " ep=" + h.EndPoint.ToString(), this.GetType().ToString());                        
                        ParticipantModel p;

                        //In case this client still has a socket open, force it to close
                        ClosePreviousSocket(h.ParticipantId);

                        //Notice that as soon as we add the entry to m_AllClients, it is eligible for sending of outbound messages:
                        bool newClient = false;
                        lock (m_AllClients) {
                            if (m_AllClients.ContainsKey(h.ParticipantId)) {
                                ((ClientData)m_AllClients[h.ParticipantId]).ConnectionState = ConnectionState.Connected;
                                ((ClientData)m_AllClients[h.ParticipantId]).Socket = s;
                                p = ((ClientData)m_AllClients[h.ParticipantId]).Participant;
                                //Add the participant to the classroom model
                                using (Synchronizer.Lock(m_Classroom.SyncRoot)) {
                                    m_Classroom.Participants.Add(p);
                                }
                                ((ClientData)m_AllClients[h.ParticipantId]).Timeout = DateTime.MaxValue;
                                this.m_ServerSender.Reconnect(((ClientData)m_AllClients[h.ParticipantId]), h.LastMessageSequence, h.LastChunkSequence);
                            }
                            else {
                                p = new ParticipantModel(h.ParticipantId,h.HumanName);
                                //Add the participant to the classroom model
                                using (Synchronizer.Lock(m_Classroom.SyncRoot)) {
                                    m_Classroom.Participants.Add(p);
                                }
                                ClientData client = new ClientData(s,h.ParticipantId,p);
                                this.m_ServerSender.AddClient(client);
                                m_AllClients.Add(h.ParticipantId, client);
                                newClient = true;
                            }
                        }

                        //Update connected client count for network status
                        using (Synchronizer.Lock(this.SyncRoot)) {
                            this.SetPublishedProperty("ClientCount", ref this.m_ClientCount, this.m_ClientCount+1);
                            NetworkStatus newStatus = m_NetworkStatus.Clone();
                            newStatus.ClientCount = this.m_ClientCount;
                            this.SetPublishedProperty("NetworkStatus", ref m_NetworkStatus, newStatus);
                        }

                        //Start a receive thread for this socket.                        
                        Thread receiveThread = new Thread(ReceiveThread);
                        receiveThread.Start(new ReceiveThreadArgs(p, ns, s.RemoteEndPoint));

                        //Send the current presentation state if this is a new client
                        if (newClient) {
                            m_Sender.ForceUpdate(new SingletonGroup(p));
                        }
                    }
                    else {
                        Trace.WriteLine("AcceptSocketCallback invalid handshake from " + s.RemoteEndPoint.ToString(), this.GetType().ToString());
                    }
                }
                catch (Exception e) {
                    Trace.WriteLine("AcceptSocketCallback exception while handshaking with " + s.RemoteEndPoint.ToString() + ": " + e.ToString(), this.GetType().ToString());
                }
            }
        }

        private void ClosePreviousSocket(Guid id) {
            Socket s = null;
            lock (m_AllClients) {
                if (m_AllClients.ContainsKey(id)) {
                    if (((ClientData)m_AllClients[id]).Socket != null) {
                        //Reconnecting state tells the maintenance thread not to mess with this socket for now:
                        ((ClientData)m_AllClients[id]).ConnectionState = ConnectionState.Reconnecting;
                        s = ((ClientData)m_AllClients[id]).Socket;
                        ((ClientData)m_AllClients[id]).Socket = null;
                    }
                }
            }
            if (s != null) {
                m_ServerSender.ForceSocketClose(id, s);
            }
        }

        #endregion Callback Methods

        #region IDisposable Members

        ~TCPServer() {
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
                this.m_Listener.Stop();
                foreach (ClientData client in m_AllClients.Values) {
                    if ((client.ConnectionState == ConnectionState.Connected) && (client.Socket != null))
                        client.Socket.Close();
                }
                this.m_ServerSender.Dispose();
#if RTP_BUILD
                if (this.m_U2MBridge != null) {
                    this.m_U2MBridge.Dispose();
                }
#endif
            }
        }

        #endregion IDisposable Members

        #region ReceiveThreadArgs Class

        /// <summary>
        /// A few items needed to give context to a new ReceiveThread.
        /// </summary>
        private class ReceiveThreadArgs {
            public ParticipantModel Participant;
            public NetworkStream NetStream;
            public EndPoint RemoteEndPoint;
            
            public ReceiveThreadArgs(ParticipantModel participant, NetworkStream networkStream, EndPoint remoteEP) {
                this.Participant = participant;
                this.NetStream = networkStream;
                this.RemoteEndPoint = remoteEP;
            }
        }

        #endregion ReceiveThreadArgs Class
 
    }

    #region ClientData Class

    /// <summary>
    /// Wrap up the relevant bits concerning one client.
    /// </summary>
    public class ClientData {
        public Socket Socket;
        public Guid Id;
        public ParticipantModel Participant;
        public ConnectionState ConnectionState;
        public DateTime Timeout;

        public ClientData(Socket s, Guid id, ParticipantModel part) {
            Socket = s;
            Id = id;
            Participant = part;
            ConnectionState = ConnectionState.Connected;
            Timeout = DateTime.MaxValue;
        }
    }

    public enum ConnectionState { 
        Connected, Disconnected, Reconnecting
    }

    #endregion ClientData Class

}
