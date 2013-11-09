// $Id: SheetNetworkService.cs 1617 2008-06-12 23:28:28Z cmprince $

using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Network.Groups;

namespace UW.ClassroomPresenter.Network.Messages.Presentation {
    public abstract class SheetNetworkService : INetworkService, IDisposable {
        private readonly SendingQueue m_Sender;
        private readonly EventQueue.PropertyEventDispatcher m_BoundsChangedDispatcher;
        private readonly PresentationModel m_Presentation;
        private readonly DeckModel m_Deck;
        private readonly SlideModel m_Slide;
        private readonly SheetModel m_Sheet;
        private readonly SheetMessage.SheetCollection m_Selector;

        private bool m_Disposed;

        public SheetNetworkService(SendingQueue sender, PresentationModel presentation, DeckModel deck, SlideModel slide, SheetModel sheet, SheetMessage.SheetCollection selector) {
            this.m_Sender = sender;
            this.m_Presentation = presentation;
            this.m_Deck = deck;
            this.m_Slide = slide;
            this.m_Sheet = sheet;
            this.m_Selector = selector;

            this.m_BoundsChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.Sender, new PropertyEventHandler(this.HandleBoundsChanged));
            this.m_Sheet.Changed["Bounds"].Add(this.m_BoundsChangedDispatcher.Dispatcher);
        }

        #region IDisposable Members

        ~SheetNetworkService() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            if(disposing) {
                this.m_Sheet.Changed["Bounds"].Remove(this.m_BoundsChangedDispatcher.Dispatcher);
            }
            this.m_Disposed = true;
        }

        #endregion IDisposable Members

        protected SendingQueue Sender {
            get { return this.m_Sender; }
        }

        protected PresentationModel Presentation {
            get { return this.m_Presentation; }
        }

        protected DeckModel Deck {
            get { return this.m_Deck; }
        }

        protected SlideModel Slide {
            get { return this.m_Slide; }
        }

        public SheetModel Sheet {
            get { return this.m_Sheet; }
        }

        protected SheetMessage.SheetCollection SheetCollectionSelector {
            get { return this.m_Selector; }
        }

        protected virtual void HandleBoundsChanged(object sender, PropertyEventArgs args) {
            // Send a generic update with information about the sheet (including the new bounds).
            Message message, deck, slide, sheet;
            message = new PresentationInformationMessage(this.Presentation);
            message.InsertChild(deck = new DeckInformationMessage(this.Deck));
            deck.InsertChild(slide = new SlideInformationMessage(this.Slide));
            slide.InsertChild( sheet = SheetMessage.RemoteForSheet( this.Sheet, this.SheetCollectionSelector ) );
            using (Synchronizer.Lock(this.m_Slide.SyncRoot)) {
                message.Tags = new MessageTags();
                message.Tags.SlideID = this.m_Slide.Id;
            }
            this.Sender.Send(message);
        }

        public static SheetNetworkService ForSheet(SendingQueue sender, PresentationModel presentation, DeckModel deck, SlideModel slide, SheetModel sheet, SheetMessage.SheetCollection selector) {
            if(sheet is RealTimeInkSheetModel) {
                return new RealTimeInkSheetNetworkService(sender, presentation, deck, slide, ((RealTimeInkSheetModel) sheet), selector);
            } else if(sheet is InkSheetModel) {
                return new InkSheetNetworkService(sender, presentation, deck, slide, ((InkSheetModel) sheet), selector);
            } else if(sheet is ImageSheetModel) {
                return new ImageSheetNetworkService(sender, presentation, deck, slide, ((ImageSheetModel)sheet), selector);
            } else if(sheet is TextSheetModel) {
                return new TextSheetNetworkService(sender, presentation, deck, slide, ((TextSheetModel)sheet), selector);
            } else if(sheet is QuickPollSheetModel) {
                return new QuickPollSheetNetworkService( sender, presentation, deck, slide, ((QuickPollSheetModel)sheet), selector );
            } else {
                return null;
            }
        }

        #region INetworkService Members

        public abstract void ForceUpdate(Group receivers);

        #endregion
    }
}
