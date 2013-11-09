

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
using UW.ClassroomPresenter.Network.TCP;
using System.Net.NetworkInformation;
using System.Collections;
using UW.ClassroomPresenter.Network.RTP;
using System.Net.Sockets;

namespace UW.ClassroomPresenter.Network.Broadcast
{
    /// <summary>
    /// Here we start and stop the UDP broadcast sender and listener used for instructor node discovery.
    /// When the TCP server is started, or we are connected to a multicast group in Instructor role, 
    /// we advertise the classroom by sending periodic broadcasts.
    /// We listen at all times.  When broadcast classrooms are discovered, we add them to the model
    /// </summary>
    public class BroadcastManager : PropertyPublisher, IDisposable
    {
        private readonly PresenterModel m_Model;
        private BroadcastListener m_Listener;
        private BroadcastSender m_Sender;
        private readonly Guid m_SenderID;
        private TCPClassroomManager m_TCPClassroomMgr;
        private TCPConnectionManager m_TCPConnectionMgr;
#if RTP_BUILD
        private RTPConnectionManager m_RTPConnectionMgr;
#endif
        private bool m_IsInstructor;
        private bool m_TcpServerStarted;
        private bool m_RtpClassroomConnected;
        private IPEndPoint m_RtpEndPoint;

        private readonly ThreadEventQueue m_EventQueue;
        private InstructorAdvertisementCollectionHelper m_InstructorAdvertisementCollectionHelper;
        private Hashtable m_TCPClientClassrooms;
        private Hashtable m_RTPAdvertisedClassrooms;

        public static bool UseIPv6;
        public static IPAddress IPv6LinkLocalMulticast;

        public BroadcastManager(PresenterModel model, TCPConnectionManager tcpMgr) {
            IPv6LinkLocalMulticast = IPAddress.Parse("ff02::1");
            UseIPv6 = false;
            //Use IPv6 only if IPv4 is unavailable

            if ((!Socket.OSSupportsIPv4) && (Socket.OSSupportsIPv6)) {
                UseIPv6 = true;
            }
            this.m_Model = model;
            this.m_TCPConnectionMgr = tcpMgr;
            this.m_TCPClassroomMgr = tcpMgr.ClassroomManager;
            this.m_TCPClientClassrooms = new Hashtable();
            this.m_RTPAdvertisedClassrooms = new Hashtable();
            m_EventQueue = new ThreadEventQueue();
           // this.m_RTPConnectionMgr = rtpMgr;

            StartListener();

            //Subscribe to the instructor advertisements that the BroadcastListener provides
            m_InstructorAdvertisementCollectionHelper = new InstructorAdvertisementCollectionHelper(this, this.m_Listener);

            //Subscribe to all the RTP Classrooms to learn when they connect/disconnect.
            //Notice that we assume there will be no new RTP classrooms created (other than the ones we create in response to advertisements).
            /*this.m_RtpClassroomConnected = false;
            using (Synchronizer.Lock(m_RTPConnectionMgr.Protocol.SyncRoot)) {
                foreach (ClassroomModel cm in m_RTPConnectionMgr.Protocol.Classrooms) {
                    cm.Changed["Connected"].Add(new PropertyEventHandler(OnRtpClassroomConnectedChanged));
                }
            }*/

            m_IsInstructor = false;
            using (Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                //This is the server's identifier which remains constant for the app instance
                m_SenderID = this.m_Model.Participant.Guid;
                //Subscribe to the local presenter's role to track when we are instructor
                this.m_Model.Participant.Changed["Role"].Add(new PropertyEventHandler(OnRoleChanged));
                if (this.m_Model.Participant.Role is InstructorModel) {
                    m_IsInstructor = true;
                }
            }

            //Subscribe to the TCP Server to find out when it starts and stops
            this.m_TCPClassroomMgr.Changed["ServerStarted"].Add(new PropertyEventHandler(ServerStartedChangedHandler));
            m_TcpServerStarted = false;
            using (Synchronizer.Lock(m_TCPClassroomMgr.SyncRoot)) {
                if (m_TCPClassroomMgr.ServerStarted) {
                    m_TcpServerStarted = true;
                }
            }

            UpdateSenderStatus();
        }
#if RTP_BUILD
        public BroadcastManager(PresenterModel model, TCPConnectionManager tcpMgr, RTPConnectionManager rtpMgr)
        {        
            this.m_Model = model;
            this.m_TCPConnectionMgr = tcpMgr;
            this.m_TCPClassroomMgr = tcpMgr.ClassroomManager;
            this.m_TCPClientClassrooms = new Hashtable();
            this.m_RTPAdvertisedClassrooms = new Hashtable();
            m_EventQueue = new ThreadEventQueue();
            this.m_RTPConnectionMgr = rtpMgr;

            StartListener();

            //Subscribe to the instructor advertisements that the BroadcastListener provides
            m_InstructorAdvertisementCollectionHelper = new InstructorAdvertisementCollectionHelper(this, this.m_Listener);

            //Subscribe to all the RTP Classrooms to learn when they connect/disconnect.
            //Notice that we assume there will be no new RTP classrooms created (other than the ones we create in response to advertisements).
            this.m_RtpClassroomConnected = false;
            using (Synchronizer.Lock(m_RTPConnectionMgr.Protocol.SyncRoot)) {
                foreach (ClassroomModel cm in m_RTPConnectionMgr.Protocol.Classrooms) {
                    cm.Changed["Connected"].Add(new PropertyEventHandler(OnRtpClassroomConnectedChanged));
                }
            }

            m_IsInstructor = false;
            using (Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                //This is the server's identifier which remains constant for the app instance
                m_SenderID = this.m_Model.Participant.Guid;
                //Subscribe to the local presenter's role to track when we are instructor
                this.m_Model.Participant.Changed["Role"].Add(new PropertyEventHandler(OnRoleChanged));
                if (this.m_Model.Participant.Role is InstructorModel) {
                    m_IsInstructor = true;
                }
            }

            //Subscribe to the TCP Server to find out when it starts and stops
            this.m_TCPClassroomMgr.Changed["ServerStarted"].Add(new PropertyEventHandler(ServerStartedChangedHandler));
            m_TcpServerStarted = false;
            using (Synchronizer.Lock(m_TCPClassroomMgr.SyncRoot)) {
                if (m_TCPClassroomMgr.ServerStarted) {
                    m_TcpServerStarted = true;
                }
            }

            UpdateSenderStatus();
        }

#endif
        /// <summary>
        /// Maybe start or stop the broadcast sender in response to a change of status
        /// </summary>
        private void UpdateSenderStatus() {
            if (m_TcpServerStarted) {
                StartSender(m_TCPClassroomMgr.ServerEndPoint);
            }
            else if (m_RtpClassroomConnected && m_IsInstructor) {
                StartSender(m_RtpEndPoint);
            }
            else {
                StopSender();
            }
        }


        #region IDisposable Members

        ~BroadcastManager()
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
                this.m_TCPClassroomMgr.Changed["ServerStarted"].Remove(new PropertyEventHandler(ServerStartedChangedHandler));
                foreach (TCPClientClassroomManager mgr in m_TCPClientClassrooms.Values) {
                    mgr.Dispose();
                }

                /*using (Synchronizer.Lock(m_RTPConnectionMgr.Protocol.SyncRoot)) {
                    foreach (ClassroomModel cm in m_RTPConnectionMgr.Protocol.Classrooms) {
                        cm.Changed["Connected"].Remove(new PropertyEventHandler(OnRtpClassroomConnectedChanged));
                    }
                }*/

                this.m_Model.Participant.Changed["Role"].Remove(new PropertyEventHandler(OnRoleChanged));

                StopListener();
                StopSender();   
            }
        }

        #endregion IDisposable Members

        /// <summary>
        /// Instructor advertisement changes cause client classrooms to be created and removed dynamically
        /// </summary>
        private class InstructorAdvertisementCollectionHelper : PropertyCollectionHelper {
            private readonly BroadcastManager m_Parent;

            public InstructorAdvertisementCollectionHelper(BroadcastManager parent, BroadcastListener broadcastListener)
                : base(parent.m_EventQueue, broadcastListener, "InstructorAdvertisements") {
                this.m_Parent = parent;
                base.Initialize();
            }

            protected override object SetUpMember(int index, object member) {
                InstructorAdvertisement ia = member as InstructorAdvertisement;
                if (ia.Multicast) {
#if RTP_BUILD
                    RTPClassroomManager mgr;
                    if (m_Parent.m_RTPAdvertisedClassrooms.ContainsKey(ia)) {
                        mgr = (RTPClassroomManager)m_Parent.m_RTPAdvertisedClassrooms[ia];
                    }
                    else {
                        mgr = new RTPClassroomManager(m_Parent.m_Model, m_Parent.m_RTPConnectionMgr, ia.HumanName, ia.Endpoint,true);
                        m_Parent.m_RTPAdvertisedClassrooms.Add(ia, mgr);
                    }
                    //put it in the model so the UI will see it.
                    using (Synchronizer.Lock(this.m_Parent.m_RTPConnectionMgr.Protocol.SyncRoot)) {
                        this.m_Parent.m_RTPConnectionMgr.Protocol.Classrooms.Add(mgr.Classroom);
                    }
                    //Set it as the return value to cause it to be supplied as the tag in teardown.
                    return mgr;
#else
                    return null;
#endif
                    }
                else {
                    TCPClientClassroomManager mgr;
                    //If we don't already have a TCPClientClassroomManager for this endpoint, create one.
                    if (m_Parent.m_TCPClientClassrooms.ContainsKey(ia)) {
                        mgr = (TCPClientClassroomManager)m_Parent.m_TCPClientClassrooms[ia];
                    }
                    else {
                        mgr = new TCPClientClassroomManager(m_Parent.m_Model, m_Parent.m_TCPConnectionMgr, ia);
                        m_Parent.m_TCPClientClassrooms.Add(ia, mgr);
                    }
                    //put it in the model so the UI will see it.
                    using (Synchronizer.Lock(this.m_Parent.m_TCPConnectionMgr.Protocol.SyncRoot)) {
                        this.m_Parent.m_TCPConnectionMgr.Protocol.Classrooms.Add(mgr.Classroom);
                    }
                    //Set it as the return value to cause it to be supplied as the tag in teardown.
                    return mgr;
                }
            }

            protected override void TearDownMember(int index, object member, object tag) {
                //Remove a ClassroomModel so that it will not appear in the UI.
                //Note that we don't dispose the *ClassroomManager here because we don't want it to disconnect.
                TCPClientClassroomManager mgr = tag as TCPClientClassroomManager;
                if (mgr != null) {
                    using (Synchronizer.Lock(this.m_Parent.m_TCPConnectionMgr.Protocol.SyncRoot)) {
                        this.m_Parent.m_TCPConnectionMgr.Protocol.Classrooms.Remove(mgr.Classroom);
                    }
                }
#if RTP_BUILD
                RTPClassroomManager rtpMgr = tag as RTPClassroomManager;
                if (rtpMgr != null) { 
                    using (Synchronizer.Lock(this.m_Parent.m_RTPConnectionMgr.Protocol.SyncRoot)) {
                        this.m_Parent.m_RTPConnectionMgr.Protocol.Classrooms.Remove(rtpMgr.Classroom);
                    }                
                }
#endif
            }
        }


        #region Event Handlers

        private void OnRtpClassroomConnectedChanged(object sender, PropertyEventArgs args) {
            PropertyChangeEventArgs pcargs = (PropertyChangeEventArgs)args;
            if ((bool)pcargs.NewValue) {
                this.m_RtpClassroomConnected = true;
                this.m_RtpEndPoint = ((ClassroomModel)sender).RtpEndPoint;
            }
            else
                this.m_RtpClassroomConnected = false;

           
            UpdateSenderStatus();
        }

        private void OnRoleChanged(object sender, PropertyEventArgs args) {
            PropertyChangeEventArgs pcargs = (PropertyChangeEventArgs)args;
            if (pcargs.NewValue is InstructorModel) {
                m_IsInstructor = true;
            }
            else {
                m_IsInstructor = false;
            }

            UpdateSenderStatus();
        }

        /// <summary>
        /// If the TCP server starts, begin advertising, otherwiser begin listening for advertisements.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args_"></param>
        private void ServerStartedChangedHandler(object sender, PropertyEventArgs args_) {
            Trace.WriteLine("ServerStartedChangedHandler", this.GetType().ToString());

            PropertyChangeEventArgs args = args_ as PropertyChangeEventArgs;
            if (args != null) {
                Trace.WriteLine("  new value=" + args.NewValue.ToString());
            }
            else {
                throw new ApplicationException("Unexpected PropertyEventArgs type.");
            }

            m_TcpServerStarted = (bool)args.NewValue;

            UpdateSenderStatus();
        }


        #endregion Event Handlers

        #region Private Methods


        private void StartListener() {
            if (this.m_Listener != null) {
                throw (new ApplicationException("Attempt to start BroadcastListener when it is already running."));
            }

            this.m_Listener = new BroadcastListener(m_SenderID);
        }

        private void StartSender(IPEndPoint ep) {
            if (this.m_Sender != null) {
                return;
            }

            this.m_Sender = new BroadcastSender(ep, m_Model, m_SenderID);
        }

        private void StopListener() {
            BroadcastListener l  = Interlocked.Exchange(ref this.m_Listener, null);
            if (l != null)
                l.Dispose();
        }
        
        private void StopSender() {
            BroadcastSender s = Interlocked.Exchange(ref this.m_Sender, null);
            if (s != null)
                s.Dispose();
        }

        #endregion Private Methods

    }
}
