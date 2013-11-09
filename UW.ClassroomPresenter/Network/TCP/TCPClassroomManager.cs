
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Model.Presentation;

using UW.ClassroomPresenter.Network.Messages;

namespace UW.ClassroomPresenter.Network.TCP
{
    /// <summary>
    /// Here we start and stop the TCP client and server as well as the TCPMessageSender/Receiver.
    /// </summary>
    public class TCPClassroomManager : PropertyPublisher, IDisposable
    {
        private readonly PresenterModel m_Model;
        private readonly ClassroomModel m_Classroom;
        private TCPMessageSender m_Sender;
        private TCPMessageReceiver m_Receiver;

        private TCPClient m_Client;
        private TCPServer m_Server;
        private bool m_ClientConnected;
        private bool m_ServerStarted;
        private IPEndPoint m_ServerEP;

        public ClassroomModel Classroom {
            get { return this.m_Classroom; }
        }

        /// <summary>
        /// This property indicates that the TCPClient currently has a connection established with a server.
        /// </summary>
        [Published]
        public bool ClientConnected {
            get { return this.GetPublishedProperty("ClientConnected", ref this.m_ClientConnected); }
        }

        /// <summary>
        /// Indicates whether the server is running or not.
        /// </summary>
        [Published]
        public bool ServerStarted { 
            get { return this.GetPublishedProperty("ServerStarted", ref this.m_ServerStarted); }      
        }

        /// <summary>
        /// The Endpoint which the server will connect, or is connected on.  Null if the server is not available.
        /// </summary>
        public IPEndPoint ServerEndPoint {
            get {
                if (m_Server != null) {
                    return m_Server.ServerEndPoint;
                }
                return null;
            }
        }

        /// <summary>
        /// If the client needs to connect to a remote server by custom endpoint, set this property
        /// before connecting the ClassroomModel.
        /// </summary>
        public IPEndPoint RemoteServerEndPoint {
            set { m_ServerEP = value; }
        }



        public TCPClassroomManager(PresenterModel model, TCPConnectionManager connection, string humanName)
        {        
            this.m_Model = model;
            this.m_ClientConnected = false;
            this.m_ServerStarted = false;
            this.m_ServerEP = null;
           
                        
            this.m_Classroom = new ClassroomModel(connection.Protocol, humanName, ClassroomModelType.TCPStatic);
            this.m_Classroom.Changing["Connected"].Add(new PropertyEventHandler(this.HandleConnectedChanging));
        }

        #region IDisposable Members

        ~TCPClassroomManager()
        {
            this.Dispose(false);
        }

        public void Dispose()
        {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.m_Classroom.Changing["Connected"].Remove(new PropertyEventHandler(this.HandleConnectedChanging));

                StopClient();
                StopServer();   
            }
        }

        #endregion IDisposable Members


        #region Event Handlers

        #region ClassroomModel Events

        /// <summary>
        /// Respond to changes to the ClassroomModel Connected property. We can use this to start and stop a server
        /// or to stop a client.  If the participant Role is InstructorModel, we start/stop the Server.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args_"></param>
        private void HandleConnectedChanging(object sender, PropertyEventArgs args_)
        {
            Trace.WriteLine("HandleConnectedChanging", this.GetType().ToString());
            PropertyChangeEventArgs args = args_ as PropertyChangeEventArgs;
            if (args == null) return;

            bool classroomConnectedChanged = false;
            using (Synchronizer.Lock(this.Classroom.SyncRoot))
            {
                if (((bool)args.NewValue) != this.Classroom.Connected)
                    classroomConnectedChanged = true;
            }

            try
            {
                if (classroomConnectedChanged)
                {
                    if ((bool)args.NewValue)
                    {
                        bool currentRoleIsInstructor = false;
                        InstructorModel instructor = null;
                        using (Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                            if (this.m_Model.Participant.Role is InstructorModel) {
                                currentRoleIsInstructor = true;
                                instructor = (InstructorModel)this.m_Model.Participant.Role;
                            }
                        }
                        if (currentRoleIsInstructor) {
                            StopClient();
                            StartServer(instructor);
                        }
                        else {
                            StopServer();
                            StartClient(m_ServerEP);
                       }
                    }
                    else
                    {
                        StopClient();
                        StopServer();
                    }
                }
            }
            catch (Exception e)
            {
                this.m_Classroom.Connected = false;
                MessageBox.Show(
                    string.Format("Classroom Presenter was unable to establish a TCP server "
                        + "because it encountered an exception:\n\n{0}\n{1}",
                       e.ToString(), e.StackTrace),
                    "Connection Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        
        }

        #endregion ClassroomModel Events

        #endregion Event Handlers

        #region Private Methods

        private void StartServer(InstructorModel instructor) {
            if (m_Server != null) {
                Trace.WriteLine("Attempt to start a TCPServer when a server is already running.", this.GetType().ToString());
                return;

                //throw (new ApplicationException("Attempt to start a TCPServer when a server is already running."));
            }

            m_Server = new TCPServer(this.m_Model, this.m_Classroom, instructor);
            m_Sender = new TCPMessageSender(m_Server, this.m_Model, this.m_Classroom);
            m_Server.Start(m_Sender);
            m_Receiver = new TCPMessageReceiver(m_Server, this.m_Model, this.m_Classroom);
            using (Synchronizer.Lock(this.SyncRoot))
                this.SetPublishedProperty("ServerStarted", ref m_ServerStarted, true);
        }

        private void StartClient(IPEndPoint remoteEP) {
            if (m_Client != null) {
                Trace.WriteLine("Attempt to start a TCPClient when a client is already running.", this.GetType().ToString());
                return;

                //throw (new ApplicationException("Attempt to start a TCPClient when a client is already running."));
            }
            m_Client = new TCPClient(remoteEP, this.m_Model.Participant, this.m_Model.Network, Guid.Empty);
            m_Client.Changed["Connected"].Add(new PropertyEventHandler(HandleClientConnectedChanged)); //Start sender only after we are connected.
            m_Client.Start();
            m_Receiver = new TCPMessageReceiver(m_Client, this.m_Model, this.m_Classroom);
        }

        private void HandleClientConnectedChanged(object sender, PropertyEventArgs args) {
            TCPClient client = (TCPClient)sender;
            if (args is PropertyChangeEventArgs) {
                if ((bool)((PropertyChangeEventArgs)args).NewValue == true) {
                    using (Synchronizer.Lock(this.SyncRoot))
                        this.SetPublishedProperty("ClientConnected", ref m_ClientConnected, true);
                    if (m_Sender == null) {
                        m_Sender = new TCPMessageSender(client, this.m_Model, this.m_Classroom);
                        Trace.WriteLine("Client Connection Handler Started TCPMessageSender.", this.GetType().ToString());
                    }
                }
                else {
                    using (Synchronizer.Lock(this.SyncRoot))
                        this.SetPublishedProperty("ClientConnected", ref m_ClientConnected, false);   
                }
            }          
        }

        private void StopServer() {
            TCPServer s = Interlocked.Exchange(ref this.m_Server, null);
            if (s != null) {
                StopSender();
                StopReceiver();
                s.Dispose();
                using (Synchronizer.Lock(this.SyncRoot))
                    this.SetPublishedProperty("ServerStarted", ref m_ServerStarted, false);
            }
        }

        private void StopClient() { 
            TCPClient c = Interlocked.Exchange(ref this.m_Client, null);
            if (c != null) {
                StopSender();
                StopReceiver();
                c.Changed["Connected"].Remove(new PropertyEventHandler(HandleClientConnectedChanged));
                using (Synchronizer.Lock(this.SyncRoot))
                    this.SetPublishedProperty("ClientConnected", ref m_ClientConnected, false);   
                c.Dispose();
            }
        }

        private void StopSender() {
            TCPMessageSender s = Interlocked.Exchange(ref this.m_Sender, null);
            if (s != null)
                s.Dispose();
        }
        
        private void StopReceiver() {
            TCPMessageReceiver r = Interlocked.Exchange(ref this.m_Receiver, null);
            if (r != null)
                r.Dispose();
        }

        private bool instructorRole() {
            using (Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                if (this.m_Model.Participant.Role is InstructorModel)
                    return true;
            }
            return false;
        }

        #endregion Private Methods



    }
}
