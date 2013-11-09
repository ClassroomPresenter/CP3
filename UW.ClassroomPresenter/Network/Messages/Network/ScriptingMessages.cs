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
    public sealed class ExecuteScriptMessage : Message {
        public ExecuteScriptMessage() : base( Guid.Empty ) {}

        protected override bool UpdateTarget( ReceiveContext context ) {
            using( Synchronizer.Lock( context.Model.SyncRoot ) ) {
                using( Synchronizer.Lock( context.Model.ViewerState.Diagnostic.SyncRoot ) )
                    context.Model.ViewerState.Diagnostic.ExecuteLocalScript = !context.Model.ViewerState.Diagnostic.ExecuteLocalScript;
            }

            return false;
        }

        #region IGenericSerializable

        public ExecuteScriptMessage( Message parent, SerializedPacket p ) : base( parent, p ) {
        }

        public override int GetClassId() {
            return PacketTypes.ExecuteScriptMessageId;
        }

        #endregion
    }
}
