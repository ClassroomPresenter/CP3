// $Id: InstructorNetworkService.cs 913 2006-03-21 04:40:16Z cmprince $

using System;
using System.Diagnostics;
using System.Threading;
using System.Collections;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Model.Viewer;
using UW.ClassroomPresenter.Network.Messages;
using UW.ClassroomPresenter.Network.Messages.Network;
using UW.ClassroomPresenter.Network.Messages.Presentation;

namespace UW.ClassroomPresenter.Network.Messages.Network {
    /// <summary>
    /// This class controls the state machine mechanism for determining 
    /// who we are going to send latency messages to
    /// </summary>
    public class SynchronizationNetworkService {
        // Protected Members
        protected readonly SendingQueue Sender;
        protected readonly PresenterModel m_Model;
        protected readonly DiagnosticModel m_Diagnostic;
        private bool m_Disposed;
        private System.Windows.Forms.Timer m_Timer;

        // Event Dispatchers
        private readonly EventQueue.PropertyEventDispatcher m_ServerChangeDispatcher;
        private readonly EventQueue.PropertyEventDispatcher m_ClientChangeDispatcher;
        
        /// <summary>
        /// Constructor for the synchronization network service
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="model"></param>
        /// <param name="diag"></param>
        public SynchronizationNetworkService(SendingQueue sender, PresenterModel model) {
            this.Sender = sender;
            this.m_Model = model;
            this.m_Diagnostic = model.ViewerState.Diagnostic;

            // Set up the change listeners
            this.m_ServerChangeDispatcher = new EventQueue.PropertyEventDispatcher(this.Sender, new PropertyEventHandler(this.HandleServerStateChange));
            this.m_Diagnostic.Changed["ServerState"].Add(this.m_ServerChangeDispatcher.Dispatcher);
            this.m_ClientChangeDispatcher = new EventQueue.PropertyEventDispatcher(this.Sender, new PropertyEventHandler(this.HandleClientStateChange));
            this.m_Diagnostic.Changed["ClientState"].Add(this.m_ClientChangeDispatcher.Dispatcher);

            this.m_Timer = new System.Windows.Forms.Timer();
            this.m_Timer.Interval = 5000;
            this.m_Timer.Tick += new EventHandler( this.OnTimerTick );
        }

        void OnTimerTick( object sender, EventArgs e ) {
            using( Synchronizer.Lock( this.m_Diagnostic.SyncRoot ) ) {
                if( this.m_Diagnostic.ServerState == DiagnosticModel.ServerSyncState.SYNCING ) {
                    // Continue with the next action in the syncing state
                    this.m_Diagnostic.ServerState = DiagnosticModel.ServerSyncState.START;
                    this.m_Diagnostic.ServerState = DiagnosticModel.ServerSyncState.SYNCING;
                } else if( this.m_Diagnostic.ClientState == DiagnosticModel.ClientSyncState.PINGING ) {
                    // Return to the start state
                    this.m_Diagnostic.ClientState = DiagnosticModel.ClientSyncState.START;
                }
            }
        }

        #region IDisposable Members

        ~SynchronizationNetworkService() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            try {
                if(disposing) {
                    this.m_Diagnostic.Changed["ServerState"].Remove( this.m_ServerChangeDispatcher.Dispatcher );
                    this.m_Diagnostic.Changed["ClientState"].Remove( this.m_ClientChangeDispatcher.Dispatcher );
                }
            } finally {
            }
            this.m_Disposed = true;
        }

        #endregion

        protected int currentParticipantIndex = -1;
        protected ArrayList myParticipants = new ArrayList();

        /// <summary>
        /// Handle changes to the server state
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void HandleServerStateChange(object sender, PropertyEventArgs args) {
            using( Synchronizer.Lock( this.m_Diagnostic.SyncRoot ) ) {
                if( this.m_Diagnostic.ServerState == DiagnosticModel.ServerSyncState.BEGIN ) {
                    // Stop the timer
                    this.m_Timer.Stop();

                    // Clear the variables
                    this.myParticipants = new ArrayList();
                    this.currentParticipantIndex = -1;

                    // Get a list of participants that we need to sync
                    using( this.m_Model.Workspace.Lock() )
                        // Get each non-instructor participant ID
                        foreach( ParticipantModel p in this.Sender.Classroom.Participants ) {
                            using( Synchronizer.Lock( p.SyncRoot ) )
                                using( Synchronizer.Lock( p.Role.SyncRoot ) )
                                    if( !(p.Role is InstructorModel) )
                                        this.myParticipants.Add( p.Role.Id );
                        }

                    // Go to the syncing state
                    this.m_Diagnostic.ServerState = DiagnosticModel.ServerSyncState.SYNCING;

                } else if( this.m_Diagnostic.ServerState == DiagnosticModel.ServerSyncState.SYNCING ) {
                    // Increment the participant number
                    currentParticipantIndex++;

                    // Make sure there are more participants
                    if( currentParticipantIndex >= this.myParticipants.Count ) {
                        this.m_Diagnostic.ServerState = DiagnosticModel.ServerSyncState.START;
                        return;
                    }
                    
                    // Send the Sync Begin Message
                    this.Sender.Send( new SyncBeginMessage( (Guid)this.myParticipants[this.currentParticipantIndex] ) );
                    this.m_Timer.Start();

                } else if( this.m_Diagnostic.ServerState == DiagnosticModel.ServerSyncState.PONGING ) {
                    // Stop the timer
                    this.m_Timer.Stop();

                    // Make sure there are more participants
                    if( currentParticipantIndex >= this.myParticipants.Count )
                        return;

                    // Send the Pong Message
                    this.Sender.Send( new SyncPongMessage( (Guid)this.myParticipants[this.currentParticipantIndex] ) );

                    // Return the the Syncing State
                    this.m_Diagnostic.ServerState = DiagnosticModel.ServerSyncState.SYNCING;

                } else {
                    // Stop the timer
                    this.m_Timer.Stop();
                }
            }
        }

        /// <summary>
        /// Handle changes to the client state
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void HandleClientStateChange( object sender, PropertyEventArgs args ) {
            // Get our own guid
            Guid myId = Guid.Empty;
            using( Synchronizer.Lock( this.m_Model.Participant.SyncRoot ) ) {
                using( Synchronizer.Lock( this.m_Model.Participant.Role.SyncRoot ) )
                    myId = this.m_Model.Participant.Role.Id;
            }

            // Send out the ping message if we are in the PINGING state
            using( Synchronizer.Lock( this.m_Diagnostic.SyncRoot ) )
                if( this.m_Diagnostic.ClientState == DiagnosticModel.ClientSyncState.PINGING ) {
                    this.m_Diagnostic.PingStartTime = Misc.AccurateTiming.Now;
                    this.Sender.Send( new SyncPingMessage( myId ) );
                    this.m_Timer.Start();
                } else
                    this.m_Timer.Stop();
        }
    }
}