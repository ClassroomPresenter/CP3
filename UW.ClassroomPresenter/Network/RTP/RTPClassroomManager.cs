// $Id: RTPClassroomManager.cs 1463 2007-09-25 19:30:52Z linnell $
#if RTP_BUILD
using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Windows.Forms;

using MSR.LST.Net.Rtp;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Model.Presentation;

using UW.ClassroomPresenter.Network.Messages;

namespace UW.ClassroomPresenter.Network.RTP {
    public class RTPClassroomManager : PropertyPublisher, IDisposable {
        private readonly PresenterModel m_Model;
        private readonly ClassroomModel m_Classroom;
        private NetworkStatus m_NetworkStatus;
        private RTPMessageSender m_Sender;
        private string m_ClassroomName;
        private bool m_AdvertisedClassroom;
        private IPEndPoint m_IPEndPoint;

        [Published]
        public NetworkStatus NetworkStatus {
            get { return this.GetPublishedProperty("NetworkStatus", ref m_NetworkStatus); }
        }

        public RTPClassroomManager(PresenterModel model, RTPConnectionManager connection, string humanName, IPEndPoint endPoint, bool advertisedClassroom) {
            this.m_Model = model;
            this.m_AdvertisedClassroom = advertisedClassroom;
            this.m_IPEndPoint = endPoint;
            ClassroomModelType classroomType = ClassroomModelType.RTPStatic;
            if (m_AdvertisedClassroom) {
                this.m_ClassroomName = humanName;
                classroomType = ClassroomModelType.Dynamic;
            }
            else {
                this.m_ClassroomName = humanName + " (" + m_IPEndPoint.Address.ToString() + ")";
            }

            this.m_Classroom = new ClassroomModel(connection.Protocol, m_ClassroomName, classroomType);
            this.m_Classroom.RtpEndPoint = this.m_IPEndPoint;
            this.m_Classroom.Changing["Connected"].Add(new PropertyEventHandler(this.HandleConnectedChanging));
        }

        ~RTPClassroomManager() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if(disposing) {
                this.m_Classroom.Changing["Connected"].Remove(new PropertyEventHandler(this.HandleConnectedChanging));

                // Dispose of any network connections via the Disconnect() method.
                this.Disconnect();
            }
        }

        public ClassroomModel Classroom {
            get { return this.m_Classroom; }
        }

        #region Events
        #region ClassroomModel Events

        private void HandleConnectedChanging(object sender, PropertyEventArgs args_) {
            PropertyChangeEventArgs args = args_ as PropertyChangeEventArgs;
            if(args == null) return;

            using(Synchronizer.Lock(this.Classroom.SyncRoot)) {
                if(((bool) args.NewValue) != this.Classroom.Connected) {
                    if((bool) args.NewValue) {
                        this.Connect();
                    }

                    else {
                        this.Disconnect();
                    }
                }
            }

        }

        #endregion ClassroomModel Events
        #endregion Events

        private void Connect() {
            using(Synchronizer.Lock(this)) {
                if(this.m_Sender == null) {
                    try {
                        this.m_Sender = new RTPMessageSender(this.m_IPEndPoint, this.m_Model, this.m_Classroom);
                        m_NetworkStatus = new NetworkStatus(ConnectionStatus.Connected, ConnectionProtocolType.RTP, TCPRole.None, 0);
                        if (m_AdvertisedClassroom) {
                            m_NetworkStatus.ClassroomName = "Advertised Classroom (" + m_IPEndPoint.Address.ToString() + ")";
                        }
                        else { 
                            m_NetworkStatus.ClassroomName = m_ClassroomName;
                        }
                        m_Model.Network.RegisterNetworkStatusProvider(this, true, m_NetworkStatus);
                    } catch (System.Net.Sockets.SocketException se) {
                        //TODO: We need to display a real message box here...
                        MessageBox.Show("Connection failed\n\rMachine must be connected to a network.\r\n"
                            + "If you repeatedly encounter this error, you may need to restart your machine.\r\n"
                            + "This is a known bug.  Thanks for your patience.");
                        Debug.WriteLine("Connection failed: " + se.ToString());
                    } catch (Exception e) {
                        //TODO: We need to display a real message box here...
                        MessageBox.Show("Connection failed: " + e.ToString());
                    }
                }
            }
        }

        private void Disconnect() {
            RTPMessageSender sender = Interlocked.Exchange(ref this.m_Sender, null);
            if (sender != null) {
                m_Model.Network.RegisterNetworkStatusProvider(this, false, null);
                sender.Dispose();
            }

            // Clear out any presentations from the classroom
            using( Synchronizer.Lock( this.Classroom.SyncRoot ) ) {
                for( int i = this.Classroom.Presentations.Count - 1; i >= 0; i-- )
                    this.Classroom.Presentations.Remove( this.Classroom.Presentations[i] );
            }
        }
    }
}

#endif