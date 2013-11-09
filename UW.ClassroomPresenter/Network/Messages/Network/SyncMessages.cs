// $Id: Message.cs 913 2006-03-21 04:40:16Z cmprince $

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Runtime.Serialization;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Viewer;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Network.Groups;

namespace UW.ClassroomPresenter.Network.Messages {
    [Serializable]
    public abstract class SynchronizationMessage : Message {
        protected readonly System.Guid participantId;

        protected SynchronizationMessage( Guid id ) : base( Guid.Empty ) {
            this.participantId = id;
        }

        #region IGenericSerializable

        public override SerializedPacket Serialize() {
            SerializedPacket p = base.Serialize();
            p.Add( SerializedPacket.SerializeGuid( this.participantId ) );
            return p;
        }

        public SynchronizationMessage( Message parent, SerializedPacket p ) : base( parent, p ) {
            this.participantId = SerializedPacket.DeserializeGuid( p.GetNextPart() );
        }

        public override int GetClassId() {
            return PacketTypes.SynchronizationMessageId;
        }

        #endregion
    }

    [Serializable]
    public sealed class SyncBeginMessage : SynchronizationMessage {
        public SyncBeginMessage( Guid id ) : base( id ) { }

        protected override bool UpdateTarget( ReceiveContext context ) {
            using( Synchronizer.Lock( context.Model.SyncRoot ) ) {
                // Check if this message is meant for this client
                using( Synchronizer.Lock( context.Model.Participant.SyncRoot ) )
                    if( context.Model.Participant.Role is InstructorModel ||
                        context.Model.Participant.Role.Id != this.participantId )
                        return false;

                // Set Client Model Sync State
                using( Synchronizer.Lock( context.Model.ViewerState.Diagnostic.SyncRoot ) )
                    context.Model.ViewerState.Diagnostic.ClientState = DiagnosticModel.ClientSyncState.PINGING;
            }

            return false;
        }

        #region IGenericSerializable

        public SyncBeginMessage( Message parent, SerializedPacket p ) : base( parent, p ) {
        }

        public override int GetClassId() {
            return PacketTypes.SyncBeginMessageId;
        }

        #endregion
    }

    [Serializable]
    public sealed class SyncPingMessage : SynchronizationMessage {
        public SyncPingMessage( Guid id ) : base( id ) { }

        protected override bool UpdateTarget( ReceiveContext context ) {
            using( Synchronizer.Lock( context.Model.SyncRoot ) ) {
                // Ensure that this message is only handled by the instructor
                using( Synchronizer.Lock( context.Model.Participant.SyncRoot ) )
                    if( !(context.Model.Participant.Role is InstructorModel) )
                        return false;

                // Set Server Model Sync State
                using( Synchronizer.Lock( context.Model.ViewerState.Diagnostic.SyncRoot ) )
                    context.Model.ViewerState.Diagnostic.ServerState = DiagnosticModel.ServerSyncState.PONGING;
            }

            return false;
        }

        #region IGenericSerializable

        public SyncPingMessage( Message parent, SerializedPacket p ) : base( parent, p ) {
        }

        public override int GetClassId() {
            return PacketTypes.SyncPingMessageId;
        }

        #endregion
    }

    [Serializable]
    public sealed class SyncPongMessage : SynchronizationMessage {
        public SyncPongMessage( Guid id ) : base( id ) { }

        protected override bool UpdateTarget(ReceiveContext context) {
            using (Synchronizer.Lock(context.Model.SyncRoot)) {
                // Ensure that this message is only handled by the correct student
                using (Synchronizer.Lock(context.Model.Participant.SyncRoot))
                    if (context.Model.Participant.Role is InstructorModel ||
                        context.Model.Participant.Role.Id != this.participantId)
                        return false;

                // Set the Client Model Sync State and update the latency
                using (Synchronizer.Lock(context.Model.ViewerState.Diagnostic.SyncRoot)) {
                    context.Model.ViewerState.Diagnostic.AddLatencyEntry(UW.ClassroomPresenter.Misc.AccurateTiming.Now);
                    context.Model.ViewerState.Diagnostic.ClientState = DiagnosticModel.ClientSyncState.START;
                }
            }

            return false;
        }

        #region IGenericSerializable

        public SyncPongMessage( Message parent, SerializedPacket p ) : base( parent, p ) {
        }

        public override int GetClassId() {
            return PacketTypes.SyncPongMessageId;
        }

        #endregion
    }
}
