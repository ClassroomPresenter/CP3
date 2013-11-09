using System;
using System.Collections.Generic;
using System.Text;

namespace UW.ClassroomPresenter.Network.TCP {
    [Serializable]
    class TCPHeartbeatMessage : IGenericSerializable {
        public TCPHeartbeatMessage() {
        }

        #region IGenericSerializable

        public SerializedPacket Serialize() {
            SerializedPacket p = new SerializedPacket( this.GetClassId() );
            return p;
        }

        public TCPHeartbeatMessage( SerializedPacket p ) {
            SerializedPacket.VerifyPacket( p, this.GetClassId() );
        }

        public int GetClassId() {
            return PacketTypes.TCPHeartbeatMessageId;
        }

        #endregion
    }
}
