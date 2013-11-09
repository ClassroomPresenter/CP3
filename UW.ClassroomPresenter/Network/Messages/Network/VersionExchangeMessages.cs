using System;
using System.Collections.Generic;
using System.Text;
using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Network;
using System.Diagnostics;

namespace UW.ClassroomPresenter.Network.Messages.Network {

    [Serializable]
    public class VersionRequestMessage : Message {
        public Guid RequesterId;
        public Version RequesterVersion;
        public VersionRequestMessage() : base(PresenterModel.ParticipantId) {
            RequesterVersion = VersionExchangeModel.LocalVersion;
            RequesterId = PresenterModel.ParticipantId;
        }

        protected override bool UpdateTarget(ReceiveContext context) {
            //Update model, intiate response  
            context.Model.VersionExchange.ReceiveVersionRequest(context.Participant,this);
            return false;
        }

        #region IGenericSerializable

        public override SerializedPacket Serialize() {
            SerializedPacket p = base.Serialize();
            p.Add( SerializedPacket.SerializeGuid( this.RequesterId ) );
            p.Add( SerializedPacket.SerializeString( this.RequesterVersion.ToString() ) );
            return p;
        }

        public VersionRequestMessage( Message parent, SerializedPacket p ) : base( parent, p ) {
            this.RequesterId = SerializedPacket.DeserializeGuid( p.GetNextPart() );
            this.RequesterVersion = new Version( SerializedPacket.DeserializeString( p.GetNextPart() ) );
        }

        public override int GetClassId() {
            return PacketTypes.VersionRequestMessageId;
        }

        #endregion
    }

    [Serializable]
    public class VersionResponseMessage : Message {
        public Guid ResponderId;
        public Version ResponderVersion;

        /// <summary>
        /// Coarse assesment of compatiblity based on the local compatibility matrix.
        /// </summary>
        public VersionCompatibility Compatibility;

        /// <summary>
        /// Action recommended for remote down-rev nodes.
        /// </summary>
        public VersionIncompatibilityRecommendedAction Action;

        /// <summary>
        /// This string is displayed on the remote system if the remote system has an older rev of CP3.
        /// It may be displayed in a pop-up MessageBox when student or public nodes connect, and it will
        /// be displayed in the version compatiblity dialog for all roles.
        /// </summary>
        public string WarningMessage;

        /// <summary>
        /// This string is only displayed locally on the node with the later rev of CP3, and is not part of the serialized
        /// message.
        /// </summary>
        [NonSerialized]
        public string LocalWarningMessage;

        /// <summary>
        /// Defaults to Compatible; No Warning Message; No Action.
        /// </summary>
        public VersionResponseMessage() : base(PresenterModel.ParticipantId) {
            ResponderVersion = VersionExchangeModel.LocalVersion;
            ResponderId = PresenterModel.ParticipantId;
            Compatibility = VersionCompatibility.Compatible;
            WarningMessage = "";
            LocalWarningMessage = "";
            Action = VersionIncompatibilityRecommendedAction.NoAction;
        }

        protected override bool UpdateTarget(ReceiveContext context) {
            //Update model
            context.Model.VersionExchange.ReceiveVersionResponse(context.Participant,this);
            return false;
        }

        #region IGenericSerializable

        public override SerializedPacket Serialize() {
            SerializedPacket p = base.Serialize();
            p.Add( SerializedPacket.SerializeGuid( this.ResponderId ) );
            p.Add( SerializedPacket.SerializeString( this.ResponderVersion.ToString() ) );
            p.Add( SerializedPacket.SerializeInt( (int)this.Compatibility ) );
            p.Add( SerializedPacket.SerializeInt( (int)this.Action ) );
            p.Add( SerializedPacket.SerializeString( this.WarningMessage ) );
            return p;
        }

        public VersionResponseMessage( Message parent, SerializedPacket p ) : base( parent, p ) {
            this.ResponderId = SerializedPacket.DeserializeGuid( p.GetNextPart() );
            this.ResponderVersion = new Version( SerializedPacket.DeserializeString( p.GetNextPart() ) );
            this.Compatibility = (VersionCompatibility)SerializedPacket.DeserializeInt( p.GetNextPart() );
            this.Action = (VersionIncompatibilityRecommendedAction)SerializedPacket.DeserializeInt( p.GetNextPart() );
            this.WarningMessage = SerializedPacket.DeserializeString( p.GetNextPart() );
            this.LocalWarningMessage = "";
        }

        public override int GetClassId() {
            return PacketTypes.VersionResponseMessageId;
        }

        #endregion
    }

    [Serializable]
    public enum VersionCompatibility { 
        Unknown = 0,
        Incompatible = 1,
        Partial = 2,
        Compatible = 3
    }

    [Serializable]
    public enum VersionIncompatibilityRecommendedAction {
        NoAction = 0,
        IssueWarning = 1
    }

}
