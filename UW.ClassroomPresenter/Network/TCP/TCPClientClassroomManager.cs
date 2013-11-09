
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Model.Presentation;

using UW.ClassroomPresenter.Network.Groups;
using UW.ClassroomPresenter.Network.Messages;
using UW.ClassroomPresenter.Network.Messages.Network;
using UW.ClassroomPresenter.Network.Broadcast;

namespace UW.ClassroomPresenter.Network.TCP {
    /// <summary>
    /// This is the classroom manager type used for the dynamic classrooms created in response to instructor advertisements.
    /// </summary>
    public class TCPClientClassroomManager : PropertyPublisher, IDisposable {
        private readonly PresenterModel m_Model;
        private readonly ClassroomModel m_Classroom;
        private TCPMessageSender m_Sender;
        private TCPMessageReceiver m_Receiver;

        private TCPClient m_Client;
        private bool m_ClientConnected;
        private readonly InstructorAdvertisement m_InstructorAdvertisement;

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

        public TCPClientClassroomManager(PresenterModel model, TCPConnectionManager connection, 
            InstructorAdvertisement instructorAdvertisement) {
            this.m_InstructorAdvertisement = instructorAdvertisement;
            this.m_Model = model;
            this.m_ClientConnected = false;
            this.m_Classroom = new ClassroomModel(connection.Protocol, m_InstructorAdvertisement.HumanName, ClassroomModelType.Dynamic);
            this.m_Classroom.Changing["Connected"].Add(new PropertyEventHandler(this.HandleConnectedChanging));
        }

        #region IDisposable Members

        ~TCPClientClassroomManager() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (disposing) {
                this.m_Classroom.Changing["Connected"].Remove(new PropertyEventHandler(this.HandleConnectedChanging));
                StopClient();
            }
        }

        #endregion IDisposable Members


        #region Event Handlers

        #region ClassroomModel Events

        //The user selected or deselected the classroom
        private void HandleConnectedChanging(object sender, PropertyEventArgs args_) {
            Trace.WriteLine("HandleConnectedChanging", this.GetType().ToString());
            PropertyChangeEventArgs args = args_ as PropertyChangeEventArgs;
            if (args == null) return;

            using (Synchronizer.Lock(this.Classroom.SyncRoot)) {
                try {
                    if (((bool)args.NewValue) != this.Classroom.Connected) {
                        if ((bool)args.NewValue) {
                            StartClient();
                        }
                        else {
                            StopClient();
                        }
                    }
                }
                catch (Exception e) {
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
        }

        #endregion ClassroomModel Events

        #endregion Event Handlers

        #region Private Methods

        private void StartClient() {
            if (m_Client != null) {
                Trace.WriteLine("Attempt to start a TCPClient when a client is already running.", this.GetType().ToString());
                return;

                //throw (new ApplicationException("Attempt to start a TCPClient when a client is already running."));
            }
            m_Client = new TCPClient(m_InstructorAdvertisement.Endpoint, this.m_Model.Participant, this.m_Model.Network, m_InstructorAdvertisement.InstructorID);
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
                    else SendRoleAndGroupMessage(m_Sender);
                }
                else {
                    using (Synchronizer.Lock(this.SyncRoot))
                        this.SetPublishedProperty("ClientConnected", ref m_ClientConnected, false);
                }
            }
        }

        /// <summary>
        /// If current role is public, then resend RoleMessage and GroupMessage for reconnection
        /// </summary>
        private void SendRoleAndGroupMessage(TCPMessageSender sender)
        {
            using (Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                if (this.m_Model.Participant.Role is PublicModel) {

                    //Send RoleMessage
                    UW.ClassroomPresenter.Network.Messages.Message message = RoleMessage.ForRole(this.m_Model.Participant.Role);
                    message.Group = Group.AllParticipant;
                    sender.Send(message);

                    //Send GroupMessage
                    foreach (Group group in this.m_Model.Participant.Groups) {
                        message = new ParticipantGroupAddedMessage(group);
                        message.Group = Group.AllParticipant;
                        sender.Send(message);
                    }
                }
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


        #endregion Private Methods

    }
}
