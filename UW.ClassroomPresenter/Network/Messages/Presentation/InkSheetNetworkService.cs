// $Id: InkSheetNetworkService.cs 1862 2009-05-12 23:52:30Z jing $

using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Threading;

using Microsoft.Ink;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Network.Groups;
using System.Drawing.Drawing2D;
using UW.ClassroomPresenter.Model.Viewer;

namespace UW.ClassroomPresenter.Network.Messages.Presentation {

    public class InkSheetNetworkService : SheetNetworkService {
        private readonly InkSheetModel m_Sheet;

        private bool m_Disposed;
        private Guid m_SlideID;

        public InkSheetNetworkService(SendingQueue sender, PresentationModel presentation, DeckModel deck, SlideModel slide, InkSheetModel sheet, SheetMessage.SheetCollection selector) : base(sender, presentation, deck, slide, sheet, selector) {
            this.m_Sheet = sheet;
            using (Synchronizer.Lock(slide.SyncRoot)) {
                m_SlideID = slide.Id;
            }

            this.m_Sheet.InkAdded += new StrokesEventHandler(this.HandleInkAdded);
            this.m_Sheet.InkDeleting += new StrokesEventHandler(this.HandleInkDeleting);

            Group receivers = Group.AllParticipant;
            if( (deck.Disposition & (DeckDisposition.StudentSubmission | DeckDisposition.QuickPoll)) != 0 ) {
                receivers = Group.Submissions;
            }

            this.SendExistingInk(receivers);
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

        private void SendExistingInk(Group receivers) {
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
                this.HandleInkAddedHelper(extracted, receivers);
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
                this.HandleInkAddedHelper(extracted, Group.AllParticipant);
            });
        }

        private void HandleInkAddedHelper(Ink extracted, Group receivers) {
            if (ViewerStateModel.NonStandardDpi) {
                extracted.Strokes.Transform(ViewerStateModel.DpiNormalizationSendMatrix, true);
            }

            try
            {
                Message message = new InkSheetStrokesAddedMessage(this.m_Sheet, m_SlideID, this.SheetCollectionSelector, extracted);
                message.Group = receivers;
                message.Tags = new MessageTags();
                message.Tags.SlideID = m_SlideID;
                message.Tags.BridgePriority = MessagePriority.Higher;
                this.Sender.Send(message);
            }
            catch (OutOfMemoryException e)
            {
                Trace.WriteLine(e.ToString());
                GC.Collect();
            }
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
            deck.InsertChild(slide = new SlideInformationMessage(this.Slide));
            slide.InsertChild(sheet = new InkSheetStrokesDeletingMessage(this.m_Sheet, this.SheetCollectionSelector, ids));
            message.Tags = new MessageTags();
            message.Tags.SlideID = m_SlideID;
            message.Tags.BridgePriority = MessagePriority.Higher;
            this.Sender.Send(message);
        }

        private delegate void InkAddedDelegate(Ink ink, Group receivers);
        private delegate void InkDeletedDelegate(string[] ids);


        #region INetworkService Members

        public override void ForceUpdate(Group receivers) {
            this.SendExistingInk(receivers);
        }

        #endregion
    }
}
