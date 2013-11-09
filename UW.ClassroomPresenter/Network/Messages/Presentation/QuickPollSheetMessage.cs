using System;
using System.Collections.Generic;
using System.Text;

using UW.ClassroomPresenter.Model.Presentation;

namespace UW.ClassroomPresenter.Network.Messages.Presentation {
    /// <summary>
    /// This message is used to let people know about the QuickPollSheet
    /// </summary>
    [Serializable]
    public class QuickPollSheetMessage : SheetMessage {
        #region Public Members

        /// <summary>
        /// The Id of the QuickPoll that should be associated with this sheet
        /// </summary>
        public readonly Guid Id;

        #endregion

        #region Constructors

        /// <summary>
        /// Construct the sheet message
        /// </summary>
        /// <param name="sheet">The sheet to construct the message from</param>
        /// <param name="collection">The collection that this sheet is part of</param>
        public QuickPollSheetMessage( QuickPollSheetModel sheet, SheetCollection collection )
            : base( sheet, collection ) {
            using( Synchronizer.Lock( sheet.SyncRoot ) ) {
                this.Id = sheet.QuickPoll.Id;
            }
        }

        #endregion

        /// <summary>
        /// Handle this messsage being received and add this sheet to the appropriate place.
        /// </summary>
        /// <param name="context">The receiving context</param>
        /// <returns>True if handled, false otherwise</returns>
        protected override bool UpdateTarget( ReceiveContext context ) 
        {
            QuickPollSheetModel sheet = this.Target as QuickPollSheetModel;
            if( sheet == null ) {
                // Create a new sheet
                using( context.Model.Workspace.Lock() ) {
                    if( (~context.Model.Workspace.CurrentPresentation) != null ) {
                        using( Synchronizer.Lock( (~context.Model.Workspace.CurrentPresentation).SyncRoot ) ) {
                            this.Target = sheet = new QuickPollSheetModel( ((Guid)this.TargetId), (~context.Model.Workspace.CurrentPresentation).QuickPoll );
                        }
                    } else {
                        using( Synchronizer.Lock( context.Participant.SyncRoot ) ) {
                            if( context.Participant.Role is Model.Network.InstructorModel ) {
                                using( Synchronizer.Lock( context.Participant.Role.SyncRoot ) ) {
                                    using( Synchronizer.Lock( ((Model.Network.InstructorModel)context.Participant.Role).CurrentPresentation.SyncRoot ) ) {
                                        this.Target = sheet = new QuickPollSheetModel( ((Guid)this.TargetId), ((Model.Network.InstructorModel)context.Participant.Role).CurrentPresentation.QuickPoll );
                                    }
                                }
                            }
                        }
                    }
                }

            }

            // Update the parameters of the sheet
            using( Synchronizer.Lock( sheet.SyncRoot ) ) {
                sheet.QuickPollId = this.Id;
            }

            base.UpdateTarget( context );

            return true;
        }

        #region IGenericSerializable

        public override SerializedPacket Serialize() {
            SerializedPacket p = base.Serialize();
            p.Add( SerializedPacket.SerializeGuid( this.Id ) );
            return p;
        }

        public QuickPollSheetMessage( Message parent, SerializedPacket p ) : base( parent, p ) {
            this.Id = SerializedPacket.DeserializeGuid( p.GetNextPart() );
        }

        public override int GetClassId() {
            return PacketTypes.QuickPollSheetMessageId;
        }

        #endregion
    }
}
