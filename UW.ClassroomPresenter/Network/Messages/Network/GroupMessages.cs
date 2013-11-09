using System;
using System.Collections.Generic;

using UW.ClassroomPresenter.Network.Messages;
using UW.ClassroomPresenter.Network.Groups;
using UW.ClassroomPresenter.Model.Network;
using System.Diagnostics;

namespace UW.ClassroomPresenter.Network.Messages.Network {
    [Serializable]
    public abstract class GroupInformationMessage : Message {
        public readonly Guid GroupId;
        public readonly string FriendlyName;
        public readonly bool Singleton;

        public GroupInformationMessage(Group group) : base(Guid.Empty) {
            this.GroupId = group.ID;
            this.FriendlyName = group.FriendlyName;
            this.Singleton = (group is SingletonGroup);
        }

        #region IGenericSerializable

        public override SerializedPacket Serialize() {
            SerializedPacket p = base.Serialize();
            p.Add( SerializedPacket.SerializeGuid( this.GroupId ) );
            p.Add( SerializedPacket.SerializeString( this.FriendlyName ) );
            p.Add( SerializedPacket.SerializeBool( this.Singleton ) );
            return p;
        }

        public GroupInformationMessage( Message parent, SerializedPacket p ) : base( parent, p ) {
            this.GroupId = SerializedPacket.DeserializeGuid( p.GetNextPart() );
            this.FriendlyName = SerializedPacket.DeserializeString( p.GetNextPart() );
            this.Singleton = SerializedPacket.DeserializeBool( p.GetNextPart() );
        }

        public override int GetClassId() {
            return PacketTypes.GroupInformationMessageId;
        }

        #endregion
    }

    [Serializable]
    public class ParticipantGroupAddedMessage : GroupInformationMessage {
        public ParticipantGroupAddedMessage(Group group) : base(group) { }

        protected override bool UpdateTarget(ReceiveContext context) {
            // Discard bogus SingletonGroups.  A participant cannot
            // belong to a SingletonGroup that does not match his Guid.
            if (this.Singleton && this.GroupId != context.Participant.Guid)
                return false;

            Group group = this.Singleton
                ? new SingletonGroup(context.Participant)
                : new Group(this.GroupId, this.FriendlyName);

            using (Synchronizer.Lock(context.Participant.SyncRoot)) {
                if (!context.Participant.Groups.Contains(group)) {
                    context.Participant.Groups.Add(group);
                }
            }

            return false;
        }

        #region IGenericSerializable
        public ParticipantGroupAddedMessage( Message parent, SerializedPacket p ) : base( parent, p ) {
        }

        public override int GetClassId() {
            return PacketTypes.ParticipantGroupAddedMessageId;
        }
        #endregion
    }

    [Serializable]
    public class ParticipantGroupRemovedMessage : GroupInformationMessage {
        public ParticipantGroupRemovedMessage(Group group) : base(group) { }

        protected override bool UpdateTarget(ReceiveContext context) {
            // For the purposes of removal, whether the group is a Group or a SingletonGroup
            // doesn't matter; all that matters is the Guid.
            Group group = new Group(this.GroupId, this.FriendlyName);

            using (Synchronizer.Lock(context.Participant.SyncRoot)) {
                if (context.Participant.Groups.Contains(group))
                    context.Participant.Groups.Remove(group);
            }

            return false;
        }

        #region IGenericSerializable
        public ParticipantGroupRemovedMessage( Message parent, SerializedPacket p ) : base( parent, p ) {
        }

        public override int GetClassId() {
            return PacketTypes.ParticipantGroupRemovedMessageId;
        }
        #endregion
    }
}
