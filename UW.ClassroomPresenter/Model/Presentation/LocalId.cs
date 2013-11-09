// $Id: LocalId.cs 1871 2009-05-26 19:29:56Z cmprince $

using System;
using UW.ClassroomPresenter.Network;

namespace UW.ClassroomPresenter.Model.Presentation {
    /// <summary>
    /// Represents the locally-unique ID of a <see cref="SlideModel"/> within a <see cref="DeckModel"/>.
    /// </summary>
    [Serializable]
    public class LocalId : IGenericSerializable {
        // FIXME: Decide what's involved in constructing a LocalId.
        public LocalId() {
        }

        #region IGenericSerializable
        public SerializedPacket Serialize() {
            SerializedPacket p = new SerializedPacket( this.GetClassId() );
            return p;
        }

        public LocalId( SerializedPacket p ) {
            SerializedPacket.VerifyPacket( p, this.GetClassId() );
        }

        public int GetClassId() {
            return PacketTypes.LocalIdId;
        }
        #endregion
    }
}
