using System;
using System.Collections.Generic;
using System.Text;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Network.Groups;

namespace UW.ClassroomPresenter.Network.Messages.Presentation {
    /// <summary>
    /// This only exists for consistency and so that SheetNetworkService.ForSheet can return something non-null if
    /// the sheet is an ImageSheetModel.
    /// </summary>
    class ImageSheetNetworkService : SheetNetworkService {
        private bool m_Disposed;  
        private readonly ImageSheetModel sheet_;
        private SlideModel m_SlideModel;

        public ImageSheetNetworkService(SendingQueue sender, PresentationModel presentation, DeckModel deck, SlideModel slide, ImageSheetModel sheet, SheetMessage.SheetCollection selector) : 
            base(sender, presentation, deck, slide, sheet, selector) {
            this.sheet_ = sheet;
            this.m_SlideModel = slide;
        }
        protected override void HandleBoundsChanged(object sender, PropertyEventArgs args) {
            SendImage(Group.AllParticipant);
        }

        private void SendImage(Group receivers) {
            this.Sender.Post(delegate() {
                this.SendImageHelper(receivers);
            });
        }

        private void SendImageHelper(Group receivers) {
            //Don't send message if it's an instructor note.
            if (this.sheet_.Disposition != SheetDisposition.Instructor) {
                Message message, deck, slide;
                message = new PresentationInformationMessage(this.Presentation);
                message.Group = receivers;
                message.InsertChild(deck = new DeckInformationMessage(this.Deck));
                deck.InsertChild(slide = new SlideInformationMessage(this.Slide));
                slide.InsertChild(new ImageSheetMessage(this.sheet_, this.SheetCollectionSelector));
                using (Synchronizer.Lock(m_SlideModel.SyncRoot)) {
                    message.Tags = new MessageTags();
                    message.Tags.SlideID = m_SlideModel.Id;
                }
                if (this.sheet_.Image != null) {
                    using (Synchronizer.Lock(this.sheet_.SyncRoot)) {
                        this.Sender.Send(message);
                    }
                }
                else
                    this.Sender.Send(message);
            }
        }
        protected override void Dispose(bool disposing) {
            if (this.m_Disposed) return;
            base.Dispose(disposing);
            this.m_Disposed = true;
        }
        public override void ForceUpdate(UW.ClassroomPresenter.Network.Groups.Group receivers) {
            this.SendImage(receivers);
        }
    }
}
