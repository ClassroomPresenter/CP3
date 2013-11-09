
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.Serialization;
using System.Threading;

using UW.ClassroomPresenter.Model.Presentation;

namespace UW.ClassroomPresenter.Network.Messages.Presentation {
    /// <summary>
    /// Class representing a new student submission slide.
    /// </summary>
    [Serializable]
    public abstract class QuickPollMessage : Message {
        private QuickPollModel m_Model;

        public QuickPollModel Model {
            get { return m_Model; }
        }

        #region Construction

        /// <summary>
        /// Constructor
        /// </summary>
        public QuickPollMessage( QuickPollModel poll ) : base( (poll != null) ? poll.Id : Guid.Empty ) {
            if( poll != null ) {
                this.AddLocalRef( poll );
            }
            this.m_Model = poll;

        }

        #endregion

        #region Receiving

        /// <summary>
        /// Handle the receipt of this message
        /// </summary>
        /// <param name="context">The context of the receiver from which the message was sent</param>
        protected override bool UpdateTarget( ReceiveContext context ) {
            QuickPollModel poll = this.Target as QuickPollModel;

            // Update Target and poll
            if( poll == null ) {
                if( this.m_Model == null ) {
                    this.Target = poll = null;
                    return false;
                } else {
                    // Create a new model
                    using( Synchronizer.Lock( this.m_Model.SyncRoot ) ) {
                        this.Target = poll = new QuickPollModel( ((Guid)this.TargetId), this.m_Model );
                    }

                    // Find a parent PresentationModel message
                    PresentationModel presentation = null;
                    Message parent = this.Parent;
                    while( parent != null && presentation == null ) {
                        if( parent.Target is PresentationModel ) {
                            presentation = parent.Target as PresentationModel;
                        } else {
                            parent = parent.Parent;
                        }
                    }
                    if( presentation == null )
                        return false;

                    using( Synchronizer.Lock( presentation.SyncRoot ) ) {
                        if( presentation.QuickPoll == null || !presentation.QuickPoll.Equals( poll ) )
                            presentation.QuickPoll = poll;
                    }
                }
            } else {
                // No properties can get updated
            }

            return true;
        }

        #endregion

        #region IGenericSerializable

        public override SerializedPacket Serialize() {
            SerializedPacket p = base.Serialize();
            p.Add( (this.m_Model != null) ?
                this.m_Model.Serialize() : SerializedPacket.NullPacket( PacketTypes.QuickPollModelId ) );
            return p;
        }

        public QuickPollMessage( Message parent, SerializedPacket p ) : base( parent, p ) {
            this.m_Model = (!SerializedPacket.IsNullPacket( p.PeekNextPart() )) ?
                new QuickPollModel( p.PeekNextPart() ) : null; p.GetNextPart();
        }

        public override int GetClassId() {
            return PacketTypes.QuickPollMessageId;
        }

        #endregion
    }

    [Serializable]
    public sealed class QuickPollInformationMessage : QuickPollMessage {
        public QuickPollInformationMessage( QuickPollModel poll ) : base( poll ) { }

        protected override MergeAction MergeInto( Message other ) {
            //Because of the way we reset the Target to a new SlideModel, we can't merge these messages.  Attempting to merge
            //them can sometimes cause multiple submissions to appear on one slide.
            return (other is QuickPollInformationMessage) ? MergeAction.KeepBothInOrder : base.MergeInto( other );
        }

        #region IGenericSerializable

        public QuickPollInformationMessage( Message parent, SerializedPacket p ) : base( parent, p ) {
        }

        public override int GetClassId() {
            return PacketTypes.QuickPollInformationMessageId;
        }

        #endregion
    }


    /// <summary>
    /// Class representing a new student submission slide.
    /// </summary>
    [Serializable]
    public abstract class QuickPollResultMessage : Message {
        private QuickPollResultModel m_Result;

        public QuickPollResultModel Result {
            get { return m_Result; }
        }

        #region Construction

        /// <summary>
        /// Constructor
        /// </summary>
        public QuickPollResultMessage( QuickPollResultModel result ) : base( Guid.Empty ) {
//            this.AddLocalRef( result );
            this.m_Result = result;
        }

        #endregion

        #region Receiving

        /// <summary>
        /// Handle the receipt of this message
        /// </summary>
        /// <param name="context">The context of the receiver from which the message was sent</param>
        protected override bool UpdateTarget( ReceiveContext context ) {

            QuickPollResultModel result = this.Target as QuickPollResultModel;

            if( result == null ) {
                using( Synchronizer.Lock( this.m_Result.SyncRoot ) ) {
                    this.Target = result = new QuickPollResultModel( this.m_Result.OwnerId, this.m_Result.ResultString );
                }
            }

            if( result != null ) {

                PresentationModel presentation = (this.Parent != null && this.Parent.Parent != null) ? this.Parent.Parent.Target as PresentationModel : null;
                if( presentation == null )
                    return false;
                
                QuickPollModel poll = (this.Parent != null) ? this.Parent.Target as QuickPollModel : null;
                if( poll == null )
                    return false;

                using( Synchronizer.Lock( presentation.SyncRoot ) ) {
                    poll.AddResult( result );
                }
            }

            // Don't want to store this as a target
            return false;
        }

        #endregion

        #region IGenericSerializable

        public override SerializedPacket Serialize() {
            SerializedPacket p = base.Serialize();
            p.Add( (this.m_Result != null) ? 
                this.m_Result.Serialize() : SerializedPacket.NullPacket( PacketTypes.QuickPollResultModelId ) );
            return p;
        }

        public QuickPollResultMessage( Message parent, SerializedPacket p ) : base( parent, p ) {
            this.m_Result = ( !SerializedPacket.IsNullPacket( p.PeekNextPart() ) ) ?
                new QuickPollResultModel( p.PeekNextPart() ) : null; p.GetNextPart();
        }

        public override int GetClassId() {
            return PacketTypes.QuickPollResultMessageId;
        }

        #endregion
    }

    [Serializable]
    public sealed class QuickPollResultInformationMessage : QuickPollResultMessage {
        public QuickPollResultInformationMessage( QuickPollResultModel result ) : base( result ) { }

        protected override MergeAction MergeInto( Message other ) {
            //Because of the way we reset the Target to a new SlideModel, we can't merge these messages.  Attempting to merge
            //them can sometimes cause multiple submissions to appear on one slide.
            return (other is QuickPollResultInformationMessage) ? MergeAction.KeepBothInOrder : base.MergeInto( other );
        }

        #region IGenericSerializable

        public QuickPollResultInformationMessage( Message parent, SerializedPacket p ) : base( parent, p ) {
        }

        public override int GetClassId() {
            return PacketTypes.QuickPollResultInformationMessageId;
        }

        #endregion
    }

    [Serializable]
    public class QuickPollResultRemovedMessage : QuickPollResultMessage {
        public QuickPollResultRemovedMessage( QuickPollResultModel result ) : base( result ) { }

        protected override bool UpdateTarget( ReceiveContext context ) {
/*            PresentationModel presentation = this.Parent != null ? this.Parent.Target as PresentationModel : null;
            if( presentation != null ) {
                using( Synchronizer.Lock( presentation.SyncRoot ) ) {
                    foreach( DeckTraversalModel traversal in presentation.DeckTraversals )
                        if( traversal.Id.Equals( this.TargetId ) )
                            presentation.DeckTraversals.Remove( traversal );
                }
            }
*/
            return false;
        }

        #region IGenericSerializable

        public QuickPollResultRemovedMessage( Message parent, SerializedPacket p ) : base( parent, p ) {
        }

        public override int GetClassId() {
            return PacketTypes.QuickPollResultRemovedMessageId;
        }

        #endregion
    }
}
