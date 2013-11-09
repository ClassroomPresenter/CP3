// $Id: DeckTraversalMessages.cs 1872 2009-05-27 00:05:08Z jing $

using System;
using System.Collections;
using System.Diagnostics;
using System.Threading;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;

namespace UW.ClassroomPresenter.Network.Messages.Presentation {

    [Serializable]
    public abstract class DeckTraversalMessage : Message {

        // Note: deckId and disposition are included here to permit late joining and asynchronous transcoding
        // applications to glean enough information from the beacon message stream to permit navigation.
        public Guid DeckId;
        public DeckDisposition Dispositon;

        public DeckTraversalMessage(DeckTraversalModel traversal) : this(traversal,true) {}

        public DeckTraversalMessage(DeckTraversalModel traversal, bool addLocalRef) : base(traversal.Id) { 
            if (addLocalRef)
                this.AddLocalRef( traversal );
            using( Synchronizer.Lock( traversal.SyncRoot ) ) {
                this.Group = traversal.Deck.Group;
                this.DeckId = traversal.Deck.Id;
                this.Dispositon = traversal.Deck.Disposition;
            }
        }

        protected override bool UpdateTarget(ReceiveContext context) {
            DeckTraversalModel traversal = this.Target as DeckTraversalModel;
            if(traversal == null)
                return false;

            PresentationModel presentation = (this.Parent != null && this.Parent.Parent != null) ? this.Parent.Parent.Target as PresentationModel : null;
            if(presentation == null)
                return false;

            using(Synchronizer.Lock(presentation.SyncRoot)) {
                if(!presentation.DeckTraversals.Contains(traversal))
                    presentation.DeckTraversals.Add((DeckTraversalModel) this.Target);
            }

            TableOfContentsModel.Entry current = (this.Predecessor != null && this.Predecessor.Child != null)
                ? this.Predecessor.Child.Target as TableOfContentsModel.Entry: null;
            if(current != null) {
                using (Synchronizer.Lock(current.Slide.SyncRoot)){
                    current.Slide.Visited = true;
                }
                using(Synchronizer.Lock(traversal.SyncRoot)) {
                    traversal.Current = current;
                }
            }

            return false;
        }

        public static DeckTraversalMessage ForDeckTraversal(DeckTraversalModel traversal) {
            DeckTraversalMessage message;
            if(traversal is SlideDeckTraversalModel)
                message = new SlideDeckTraversalMessage((SlideDeckTraversalModel) traversal);
            else if(traversal == null)
                throw new ArgumentNullException("traversal");
            else
                throw new ArgumentException("Unknown deck traversal type: " + traversal.ToString());

            // Set the message's predecessor to the current TableOfContents.Entry.
            using(Synchronizer.Lock(traversal.SyncRoot)) {
                if( traversal.Current != null ) {
                    Message predecessor = message.Predecessor;
                    message.Predecessor = new SlideInformationMessage( traversal.Current.Slide );
                    message.Predecessor.InsertChild( new TableOfContentsEntryMessage( traversal.Current ) );
                    message.Predecessor.AddOldestPredecessor( predecessor );
                }
                return message;
            }
        }

        #region IGenericSerializable

        public DeckTraversalMessage( Message parent, SerializedPacket p ) : base( parent, p ) {
        }

        public override int GetClassId() {
            return PacketTypes.DeckTraversalMessageId;
        }

        #endregion
    }

    [Serializable]
    public class SlideDeckTraversalMessage : DeckTraversalMessage {
        public SlideDeckTraversalMessage(SlideDeckTraversalModel traversal) : base(traversal) {}

        protected override bool UpdateTarget(ReceiveContext context) {
            DeckModel deck = this.Parent != null ? this.Parent.Target as DeckModel : null;
            if(deck == null)
                return false;

            SlideDeckTraversalModel traversal = this.Target as SlideDeckTraversalModel;
            if(traversal == null)
                this.Target = traversal = new SlideDeckTraversalModel(((Guid) this.TargetId), deck);

            base.UpdateTarget(context);

            return true;
        }

        #region IGenericSerializable

        public SlideDeckTraversalMessage( Message parent, SerializedPacket p ) : base( parent, p ) {
        }

        public override int GetClassId() {
            return PacketTypes.SlideDeckTraversalMessageId;
        }

        #endregion
    }

    [Serializable]
    public class DeckTraversalRemovedFromPresentationMessage : DeckTraversalMessage {
        public DeckTraversalRemovedFromPresentationMessage(DeckTraversalModel traversal) : base(traversal) {}

        protected override bool UpdateTarget(ReceiveContext context) {
            PresentationModel presentation = this.Parent != null ? this.Parent.Target as PresentationModel : null;
            if(presentation != null) {
                using(Synchronizer.Lock(presentation.SyncRoot)) {
                    foreach(DeckTraversalModel traversal in presentation.DeckTraversals)
                        if(traversal.Id.Equals(this.TargetId))
                            presentation.DeckTraversals.Remove(traversal);
                }
            }

            return false;
        }

        #region IGenericSerializable

        public DeckTraversalRemovedFromPresentationMessage( Message parent, SerializedPacket p ) : base( parent, p ) {
        }

        public override int GetClassId() {
            return PacketTypes.DeckTraversalRemovedFromPresentationMessageId;
        }

        #endregion
    }
}
