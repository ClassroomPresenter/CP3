// $Id: InkSheetNetworkService.cs 667 2005-08-30 22:06:13Z pediddle $

using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Threading;

using Microsoft.Ink;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;

namespace UW.ClassroomPresenter.Network.Messages.Presentation {

    public class SSInkSheetNetworkService : SSSheetNetworkService {
        private readonly InkSheetModel m_Sheet;

        private bool m_Disposed;

        public SSInkSheetNetworkService(SendingQueue sender, PresentationModel presentation, DeckModel deck, SlideModel slide, InkSheetModel sheet, SheetMessage.SheetCollection selector) : base(sender, presentation, deck, slide, sheet, selector) {
            this.m_Sheet = sheet;

            this.m_Sheet.InkAdded += new StrokesEventHandler(this.HandleInkAdded);
            this.m_Sheet.InkDeleting += new StrokesEventHandler(this.HandleInkDeleting);

            this.SendExistingInk();
        }

        protected override void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            try {
                if(disposing) {
                    this.m_Sheet.InkAdded -= new StrokesEventHandler(this.HandleInkAdded);
                    this.m_Sheet.InkDeleted -= new StrokesEventHandler(this.HandleInkDeleting);
                }
            } finally {
                base.Dispose(disposing);
            }
            this.m_Disposed = true;
        }

        private void SendExistingInk() {
            Ink extracted;
            using(Synchronizer.Lock(this.m_Sheet.Ink.Strokes.SyncRoot)) {
                // Ensure that each stroke has a Guid which will uniquely identify it on the remote side.
                foreach(Stroke stroke in this.m_Sheet.Ink.Strokes) {
                    if(!stroke.ExtendedProperties.DoesPropertyExist(InkSheetMessage.StrokeIdExtendedProperty))
                        stroke.ExtendedProperties.Add(InkSheetMessage.StrokeIdExtendedProperty, Guid.NewGuid().ToString());
                }

                // Extract all of the strokes.
                extracted = this.m_Sheet.Ink.ExtractStrokes(this.m_Sheet.Ink.Strokes, ExtractFlags.CopyFromOriginal);
            }

            // Send a message as if the already-existing ink was just added to the sheet.
            this.Sender.Post(delegate() {
                this.HandleInkAddedHelper(extracted);
            });
        }

        private void HandleInkAdded(object sender, StrokesEventArgs e) {
            // We must extract the added ink immediately so it can't be deleted before the
            // message is sent on another thread.

            Ink extracted;
            using(Synchronizer.Lock(this.m_Sheet.Ink.Strokes.SyncRoot)) {
                using(Strokes strokes = this.m_Sheet.Ink.CreateStrokes(e.StrokeIds)) {

                    // Ensure that each stroke has a Guid which will uniquely identify it on the remote side.
                    foreach(Stroke stroke in strokes) {
                        if(!stroke.ExtendedProperties.DoesPropertyExist(InkSheetMessage.StrokeIdExtendedProperty))
                            stroke.ExtendedProperties.Add(InkSheetMessage.StrokeIdExtendedProperty, Guid.NewGuid().ToString());
                    }

                    extracted = this.m_Sheet.Ink.ExtractStrokes(strokes, ExtractFlags.CopyFromOriginal);
                }
            }

            this.Sender.Post(delegate() {
                this.HandleInkAddedHelper(extracted);
            });
        }

        private void HandleInkAddedHelper(Ink extracted) {
            Message message, deck, slide, sheet;
            message = new PresentationInformationMessage(this.Presentation);
            message.InsertChild(deck = new DeckInformationMessage(this.Deck));
            deck.InsertChild(slide = new SlideInformationMessage( GetSubmissionSlideModel(), false ));
            slide.InsertChild(sheet = new InkSheetStrokesAddedMessage(this.m_Sheet, (Guid)slide.TargetId, this.SheetCollectionSelector, extracted));
            sheet.AddOldestPredecessor( SheetMessage.RemoteForSheet( this.m_Sheet, this.SheetCollectionSelector ) );
            this.Sender.Send(message);
        }

        private void HandleInkDeleting(object sender, StrokesEventArgs e) {
            // We must extract the deleted ink immediately, or else it won't be around
            // by the time we process the event on another thread.
            // Compile a list of all of the Guids which identify the strokes to be removed.
            // The Guids are created by HandleInkAdded above.
            // If a stroke does not have a Guid, then it was not broadcast and therefore
            // we don't need to worry about telling the remote clients to remove it.
            string[] ids;
            using(Synchronizer.Lock(this.m_Sheet.Ink.Strokes.SyncRoot)) {
                int[] strokeIds = e.StrokeIds;
                ArrayList guids = new ArrayList(strokeIds.Length);
                using(Strokes strokes = this.m_Sheet.Ink.CreateStrokes(strokeIds)) {
                    foreach(Stroke stroke in strokes) {
                        if(stroke.ExtendedProperties.DoesPropertyExist(InkSheetMessage.StrokeIdExtendedProperty))
                            guids.Add(stroke.ExtendedProperties[InkSheetMessage.StrokeIdExtendedProperty].Data);
                    }
                }
                ids = ((string[]) guids.ToArray(typeof(string)));
            }

            if (ids.Length > 0)
                this.Sender.Post(delegate() {
                    this.HandleInkDeletingHelper(ids);
                });
        }

        private void HandleInkDeletingHelper(string[] ids) {
            Message message, deck, slide, sheet;
            message = new PresentationInformationMessage(this.Presentation);
            message.InsertChild(deck = new DeckInformationMessage(this.Deck));
            deck.InsertChild(slide = new SlideInformationMessage( GetSubmissionSlideModel(), false ));
            slide.InsertChild(sheet = new InkSheetStrokesDeletingMessage(this.m_Sheet, this.SheetCollectionSelector, ids));
            this.Sender.Send(message);
        }

        private delegate void InkAddedDelegate(Ink ink);
        private delegate void InkDeletedDelegate(string[] ids);
    }
}
