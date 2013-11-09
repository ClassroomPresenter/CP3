using System;
using System.Collections.Generic;
using System.Text;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Network.Groups;

namespace UW.ClassroomPresenter.Network.Messages.Presentation {
    public class TextSheetNetworkService : SheetNetworkService{
        private bool m_Disposed;
        private readonly TextSheetModel sheet_;
        public TextSheetNetworkService(SendingQueue sender, PresentationModel presentation, DeckModel deck, SlideModel slide, TextSheetModel sheet, SheetMessage.SheetCollection selector) : base(sender, presentation, deck, slide, sheet, selector) {
            this.sheet_ = sheet;

            this.sheet_.Changed["Font"].Add(new PropertyEventHandler(this.SendText));
            this.sheet_.Changed["Text"].Add(new PropertyEventHandler(this.SendText));
            this.sheet_.Changed["Color"].Add(new PropertyEventHandler(this.SendText));
            this.sheet_.Changed["IsPublic"].Add(new PropertyEventHandler(this.SendPublic));
        }

        protected override void Dispose(bool disposing) {
            if (this.m_Disposed) return;
            try {
                if (disposing) {
                    this.sheet_.Changed["Font"].Remove(new PropertyEventHandler(this.SendText));
                    this.sheet_.Changed["Text"].Remove(new PropertyEventHandler(this.SendText));
                    this.sheet_.Changed["Color"].Remove(new PropertyEventHandler(this.SendText));
                    this.sheet_.Changed["IsPublic"].Remove(new PropertyEventHandler(this.SendPublic));
                }
            } finally {
                base.Dispose(disposing);
            }
            this.m_Disposed = true;
        }

        private void SendText(object o, PropertyEventArgs args) {
            this.Sender.Post(delegate() {
                bool sheet_is_public;
                using (Synchronizer.Lock(sheet_.SyncRoot)) {
                    sheet_is_public = sheet_.IsPublic;
                }
                if (sheet_is_public) {
                    ///only send the text message if the sheet is public
                    this.SendTextHelper(Group.AllParticipant);
                }
            });
        }

        private void SendPublic(object o, PropertyEventArgs args) {
            this.Sender.Post(delegate() {
                this.SendPublicHelper(Group.AllParticipant);
            });
        }

        protected override void HandleBoundsChanged(object sender, PropertyEventArgs args) {
            SendText(sender, args);
        }

        /// <summary>
        /// This is similar to the texthelpermethod, but here we send a removal message
        /// if the message is private. I am having two different methods for the sake of seperating
        /// the two different types of messages.
        /// </summary>
        /// <param name="receivers"></param>
        private void SendPublicHelper(Group receivers) {
            Message message, deck, slide;
            message = new PresentationInformationMessage(this.Presentation);
            message.Group = receivers;
            message.InsertChild(deck = new DeckInformationMessage(this.Deck));
            deck.InsertChild(slide = new SlideInformationMessage(this.Slide));

            ///send the text message
            bool sheet_is_public;
            using (Synchronizer.Lock(sheet_.SyncRoot)) {
                sheet_is_public = sheet_.IsPublic;
            }
            ///if the sheet is private, the users should not be able to see any part of the sheet,
            ///so we should just remove a private sheet.
            if (sheet_is_public) {
                slide.InsertChild(new TextSheetMessage(this.sheet_, this.SheetCollectionSelector));
            } else {
                slide.InsertChild(new SheetRemovedMessage(this.sheet_, this.SheetCollectionSelector));
            }

            using (Synchronizer.Lock(this.Slide.SyncRoot)) {
                message.Tags = new MessageTags();
                message.Tags.SlideID = this.Slide.Id;
            }

            this.Sender.Send(message);
        }

        private void SendTextHelper(Group receivers) {
            Message message, deck, slide;
            message = new PresentationInformationMessage(this.Presentation);
            message.Group = receivers;
            message.InsertChild(deck = new DeckInformationMessage(this.Deck));
            deck.InsertChild(slide = new SlideInformationMessage(this.Slide));

            ///send the text message
            slide.InsertChild(new TextSheetMessage(this.sheet_, this.SheetCollectionSelector));

            using (Synchronizer.Lock(this.Slide.SyncRoot)) {
                message.Tags = new MessageTags();
                message.Tags.SlideID = this.Slide.Id;
            }

            this.Sender.Send(message);
        }
        
        public override void ForceUpdate(Group receivers) {
            this.Sender.Post(delegate() {
                this.SendTextHelper(receivers);
            });
        }
    }
}
