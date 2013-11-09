using System;
using System.Collections.Generic;
using System.Net;

using UW.ClassroomPresenter.Model.Network;

using UW.ClassroomPresenter.Network.Messages;
using UW.ClassroomPresenter.Network.Messages.Network;
using UW.ClassroomPresenter.Network.Messages.Presentation;

namespace UW.ClassroomPresenter.Network.TCP {
    [Serializable]
    public class TCPHandshakeMessage : IGenericSerializable {
        public readonly Guid ParticipantId;
        public readonly IPEndPoint EndPoint;
        public readonly String HumanName;
        /// <summary>
        /// This is used in reconnect scenarios:  The client should set the last received message (and chunk)
        /// sequence numbers.  This will permit the server to resume sending where it left off.
        /// </summary>
        public ulong LastMessageSequence;
        /// <summary>
        /// This is used in reconnect scenarios:  The client should set the last received chunk (and message)
        /// sequence numbers.  This will permit the server to resume sending where it left off.
        /// </summary>
        public ulong LastChunkSequence;

        public TCPHandshakeMessage(ParticipantModel participant, IPEndPoint ep) {
            this.ParticipantId = participant.Guid;
            this.EndPoint = ep;
            using (Synchronizer.Lock(participant.SyncRoot)) {
                this.HumanName = participant.HumanName;
            }
            LastMessageSequence = LastChunkSequence = 0;
        }

        #region IGenericSerializable

        public SerializedPacket Serialize() {
            SerializedPacket p = new SerializedPacket( this.GetClassId() );
            p.Add( SerializedPacket.SerializeGuid( this.ParticipantId ) );
            p.Add( (this.EndPoint != null ) ? 
                SerializedPacket.SerializeIPEndPoint( this.EndPoint ) : SerializedPacket.NullPacket( PacketTypes.IPEndPointId ) );
            p.Add( SerializedPacket.SerializeString( this.HumanName ) );
            p.Add( SerializedPacket.SerializeULong( this.LastMessageSequence ) );
            p.Add( SerializedPacket.SerializeULong( this.LastChunkSequence ) );
            return p;
        }

        public TCPHandshakeMessage( SerializedPacket p ) {
            SerializedPacket.VerifyPacket( p, this.GetClassId() );
            this.ParticipantId = SerializedPacket.DeserializeGuid( p.GetNextPart() );
            this.EndPoint = (!SerializedPacket.IsNullPacket( p.PeekNextPart() )) ?
                SerializedPacket.DeserializeIPEndPoint( p.PeekNextPart() ) : null; p.GetNextPart();
            this.HumanName = SerializedPacket.DeserializeString( p.GetNextPart() );
            this.LastMessageSequence = SerializedPacket.DeserializeULong( p.GetNextPart() );
            this.LastChunkSequence = SerializedPacket.DeserializeULong( p.GetNextPart() );

            System.Diagnostics.Debug.Write( "TCPHandshakeMessage: " +
                this.ParticipantId.ToString() + " " +
                ((this.EndPoint != null) ? (this.EndPoint.Address.ToString() + ":" + this.EndPoint.Port.ToString() + " ") : "") +
                this.HumanName + " " +
                this.LastMessageSequence + " " +
                this.LastChunkSequence +
                System.Environment.NewLine );
        }

        public int GetClassId() {
            return PacketTypes.TCPHandshakeMessageId;
        }

        #endregion
    }
}
