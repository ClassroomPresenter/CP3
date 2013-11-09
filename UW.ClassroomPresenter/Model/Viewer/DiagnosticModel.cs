// $Id: DiagnosticModel.cs 895 2006-02-16 03:03:16Z cmprince $
using System;
using System.Threading;
using System.Collections;
using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Misc;

namespace UW.ClassroomPresenter.Model.Viewer {
    /// <summary>
    /// Encapsulates all of the properties needed to conduct diagnoses and testing of the 
    /// application
    /// </summary>
    public class DiagnosticModel : PropertyPublisher, ICloneable {

        /// <summary>
        /// Class representing an entry for the skew for this device
        /// </summary>
        [Serializable]
        public class SkewEntry {
            public Int64 ServerTime = 0;
            public Int64 ClientTime = 0;
            public Int64 Skew {
                get { return (this.ClientTime-this.ServerTime); }
            }

            public SkewEntry( Int64 s, Int64 c ) {
                this.ServerTime = s;
                this.ClientTime = c;
            }
        }

        /// <summary>
        /// Class representing an entry for the latency of this device
        /// </summary>
        [Serializable]
        public class LatencyEntry {
            public Int64 StartTime = 0;
            public Int64 EndTime = 0;
            public Int64 Latency {
                get { return (this.EndTime-this.StartTime)/2; }
            }

            public LatencyEntry( Int64 s, Int64 e ) {
                this.StartTime = s;
                this.EndTime = e;
            }
        }

        // Protected Properties
        private ArrayList m_LatencyList;
        private ArrayList m_SkewList;

        public enum ServerSyncState {
            START = 0,
            BEGIN = 1,
            SYNCING = 2,
            PONGING = 3
        }
        public enum ClientSyncState {
            START = 0,
            PINGING = 1
        }
        private ServerSyncState m_ServerState;
        private ClientSyncState m_ClientState;
        private Int64 m_PingStartTime;

        /// <summary>
        /// Instructor's current state of syncing
        /// </summary>
        [Published]
        public ServerSyncState ServerState {
            get { return this.GetPublishedProperty( "ServerState", ref this.m_ServerState ); }
            set { this.SetPublishedProperty( "ServerState", ref this.m_ServerState, value ); }
        }

        /// <summary>
        /// Student's current state of synching
        /// </summary>
        [Published]
        public ClientSyncState ClientState {
            get { return this.GetPublishedProperty( "ClientState", ref this.m_ClientState ); }
            set { this.SetPublishedProperty( "ClientState", ref this.m_ClientState, value ); }
        }

        /// <summary>
        /// Time ping was started
        /// </summary>
        [Published]
        public Int64 PingStartTime {
            get { return this.GetPublishedProperty( "PingStartTime", ref this.m_PingStartTime ); }
            set { this.SetPublishedProperty( "PingStartTime", ref this.m_PingStartTime, value ); }
        }

        private bool m_ExecuteRemoteScript;
        private bool m_ExecuteLocalScript;

        /// <summary>
        /// Execute Remote Scripts
        /// </summary>
        [Published]
        public bool ExecuteRemoteScript {
            get { return this.GetPublishedProperty( "ExecuteRemoteScript", ref this.m_ExecuteRemoteScript ); }
            set { this.SetPublishedProperty( "ExecuteRemoteScript", ref this.m_ExecuteRemoteScript, value ); }
        }
        /// <summary>
        /// Execute Local Script
        /// </summary>
        [Published]
        public bool ExecuteLocalScript {
            get { return this.GetPublishedProperty( "ExecuteLocalScript", ref this.m_ExecuteLocalScript ); }
            set { this.SetPublishedProperty( "ExecuteLocalScript", ref this.m_ExecuteLocalScript, value ); }
        }

        private ArrayList LogMessages = new ArrayList();

        /// <summary>
        /// Instantiates a new <see cref="DiagnosticModel"/>.
        /// </summary>
        public DiagnosticModel() {
            this.m_LatencyList = new ArrayList();
            this.m_SkewList = new ArrayList();
            this.m_ServerState = ServerSyncState.START;
            this.m_ClientState = ClientSyncState.START;
            this.m_PingStartTime = 0;

        }

        public Diagnostics.NetworkPerformanceFile GetLogFile( Guid g ) {
            using( Synchronizer.Lock( this ) ) {
                return new Diagnostics.NetworkPerformanceFile( g, this.m_LatencyList, this.m_SkewList, this.LogMessages );
            }
        }

        /// <summary>
        /// Update the clock offset given the sync-signal from the instructor's beacon
        /// </summary>
        /// <param name="instructorTime">Time given from the instructor</param>
        public void AddSkewEntry( Int64 instructorTime ) {
            using( Synchronizer.Lock( this.SyncRoot ) ) {
                this.m_SkewList.Add( new SkewEntry( instructorTime, AccurateTiming.Now ) );
            }
        }

        /// <summary>
        /// Update the latency sliding window to give the median latency of all "pings"
        /// </summary>
        /// <param name="startTime">Time when the PingMessage is sent</param>
        /// <param name="endTime">Time when the PongMessage is received</param>
        public void AddLatencyEntry( Int64 endTime ) {
            using( Synchronizer.Lock( this.SyncRoot ) ) {
                this.m_LatencyList.Add( new LatencyEntry( this.PingStartTime, endTime ) );
            }
        }

        public void LogMessageSend( UW.ClassroomPresenter.Network.Messages.Message message, MessagePriority priority ) {
            using( Synchronizer.Lock( this.SyncRoot ) ) {
                this.LogMessages.Add( new SendLogMessage( AccurateTiming.Now, message, priority ) );
            }
        }

        public void LogMessageRecv( UW.ClassroomPresenter.Network.Messages.Message message ) {
            using( Synchronizer.Lock( this.SyncRoot ) ) {
                this.LogMessages.Add( new RecvLogMessage( AccurateTiming.Now, message ) );
            }
        }

        #region Log Messages

        /// <summary>
        /// Network performance logging message
        /// </summary>
        [Serializable]
        public abstract class LogMessage {
            public long TimeStamp;

            public LogMessage( long time ) {
                this.TimeStamp = time;
            }
        }

        /// <summary>
        /// Collected whenever a message is sent
        /// </summary>
        [Serializable]
        public class SendLogMessage : LogMessage {
            public UW.ClassroomPresenter.Network.Messages.Message message;
            public MessagePriority priority;

            public SendLogMessage( long time, UW.ClassroomPresenter.Network.Messages.Message msg, MessagePriority prior ) : base(time) {
                this.message = msg;
                this.priority = prior;
            }
        }

        /// <summary>
        /// Collected whenever a message is recieved
        /// </summary>
        [Serializable]
        public class RecvLogMessage : LogMessage {
            public UW.ClassroomPresenter.Network.Messages.Message message;

            public RecvLogMessage( long time, UW.ClassroomPresenter.Network.Messages.Message msg ) : base(time) {
                this.message = msg;
            }
        }

        #endregion

        #region ICloneable Members

        /// <summary>
        /// Clones this model object
        /// </summary>
        /// <returns>A Deep copy of this structure</returns>
        public object Clone() {
            // Create the clone
            DiagnosticModel clonedModel = new DiagnosticModel();

            clonedModel.m_LatencyList = (ArrayList)this.m_LatencyList.Clone();
            clonedModel.m_SkewList = (ArrayList)this.m_SkewList.Clone();

            // Copy the values
            return clonedModel;
        }

        #endregion
    }
}
