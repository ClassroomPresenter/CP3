#if WEBSERVER
using System;
using System.Collections;
using System.Text;

using UW.ClassroomPresenter.Model.Viewer;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Network.Messages;
using UW.ClassroomPresenter.Network.Messages.Presentation;
using Microsoft.Ink;
using UW.ClassroomPresenter.Web.Model;

namespace UW.ClassroomPresenter.Web.Network {
    /// <summary>
    /// Handle ink being added to a slide so we need to appropriately add it
    /// to the SimpleWebModel
    /// </summary>
    public class InkSheetWebService : SheetWebService {
        #region Members

        /// <summary>
        /// The ink sheet model
        /// </summary>
        private readonly InkSheetModel m_Sheet;

        /// <summary>
        /// Whether the object is disposed or not
        /// </summary>
        private bool m_Disposed;

        /// <summary>
        /// The slide id
        /// </summary>
        private Guid m_SlideID;

        #endregion

        #region Constructors

        /// <summary>
        /// Construct the InkSheetWebService, this class listens for strokes to finish 
        /// and sends them across the network
        /// </summary>
        /// <param name="sender">The queue to use</param>
        /// <param name="presentation">The presentation model</param>
        /// <param name="deck">The deck model</param>
        /// <param name="slide">The slide model</param>
        /// <param name="sheet">The sheet model</param>
        /// <param name="selector">The sheet collection we are part of</param>
        public InkSheetWebService(SendingQueue sender, PresentationModel presentation, DeckModel deck, SlideModel slide, SheetModel sheet, SheetMessage.SheetCollection selector)
            : base(sender, presentation, deck, slide, sheet, selector)
        {
            // Keep track of our sheet
            this.m_Sheet = (InkSheetModel)sheet;

            // Get the slide ID
            using (Synchronizer.Lock(slide.SyncRoot)) {
                m_SlideID = slide.Id;
            }

            // Set Events
            this.m_Sheet.InkAdded += new StrokesEventHandler(this.HandleInkAdded);
//            this.m_Sheet.InkDeleting += new StrokesEventHandler(this.HandleInkDeleting);
        }

        #endregion

        #region IDisposable Members

        /// <summary>
        /// Protected dispose method
        /// </summary>
        /// <param name="disposing">True when user initiated</param>
        protected override void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            try {
                if(disposing) {
                    this.m_Sheet.InkAdded -= new StrokesEventHandler(this.HandleInkAdded);
//                    this.m_Sheet.InkDeleted -= new StrokesEventHandler(this.HandleInkDeleting);
                }
            } finally {
                base.Dispose(disposing);
            }
            this.m_Disposed = true;
        }

        #endregion

        #region Handle Ink
/*
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
*/
        /// <summary>
        /// Handle when ink is added to the slide
        /// </summary>
        /// <param name="sender">The sender of this event</param>
        /// <param name="e">The parameters for this event</param>
        private void HandleInkAdded(object sender, StrokesEventArgs e) {
            // Always copy all strokes for now...
            Ink extracted;
            using (Synchronizer.Lock(this.m_Sheet.Ink.Strokes.SyncRoot)) {
                // Ensure that each stroke has a Guid which will uniquely identify it on the remote side.
                foreach (Stroke stroke in this.m_Sheet.Ink.Strokes) {
                    if (!stroke.ExtendedProperties.DoesPropertyExist(InkSheetMessage.StrokeIdExtendedProperty))
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

        /// <summary>
        /// Helper class that is run on a different thread that converts the ink coordinates into pixel coordinates
        /// </summary>
        /// <param name="extracted">The strokes to convert and add to the model</param>
        private void HandleInkAddedHelper(Ink extracted) {
            // Adjust for differences in DPI
            if (ViewerStateModel.NonStandardDpi) {
                extracted.Strokes.Transform(ViewerStateModel.DpiNormalizationSendMatrix, true);
            }

            // Get the model object to add it to
            lock (WebService.Instance.GlobalModel) {
                SimpleWebDeck deck = (SimpleWebDeck)WebService.Instance.GlobalModel.Decks[WebService.Instance.GlobalModel.CurrentDeck];
                SimpleWebSlide slide = (SimpleWebSlide)deck.Slides[WebService.Instance.GlobalModel.CurrentSlide - 1];
                slide.Inks = new ArrayList();
                // Extract each stroke
                foreach (Stroke s in extracted.Strokes) {
                    // Get the points
                    System.Drawing.Point[] pts = s.GetPoints();
                    // Convert and remove duplicates
                    bool bFirst = true;
                    ArrayList pts_new = new ArrayList();
                    System.Drawing.Point last = System.Drawing.Point.Empty;
                    foreach (System.Drawing.Point p in pts) {
                        System.Drawing.Point pNew = new System.Drawing.Point((int)Math.Round(p.X / 26.37f), (int)Math.Round(p.Y / 26.37f));
                        if (!bFirst && last == pNew)
                            continue;
                        bFirst = false;
                        last = pNew;
                        pts_new.Add(pNew);
                    }

                    // Add the points to the model
                    SimpleWebInk stroke = new SimpleWebInk();
                    stroke.Pts = (System.Drawing.Point[])pts_new.ToArray(typeof(System.Drawing.Point));
                    stroke.R = s.DrawingAttributes.Color.R;
                    stroke.G = s.DrawingAttributes.Color.G;
                    stroke.B = s.DrawingAttributes.Color.B;
                    stroke.Opacity = (s.DrawingAttributes.RasterOperation == RasterOperation.MaskPen) ? (byte)127 : (byte)255;
                    stroke.Width = s.DrawingAttributes.Width / 26.37f;
                    slide.Inks.Add(stroke);
                }
            }
            WebService.Instance.UpdateModel();
        }
/*
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
*/
        /// <summary>
        /// Delegate for when ink is added to the slide
        /// </summary>
        /// <param name="ink">The ink that was added</param>
        private delegate void InkAddedDelegate(Ink ink);

        /// <summary>
        /// Delegate for when ink is deleted from the slide
        /// </summary>
        /// <param name="ids">The id of the strokes that were deleted</param>
        private delegate void InkDeletedDelegate(string[] ids);

        #endregion
    }
}
#endif