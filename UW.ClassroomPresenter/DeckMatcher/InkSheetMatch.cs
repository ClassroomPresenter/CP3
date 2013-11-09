// $Id: InkSheetMatch.cs 975 2006-06-28 01:02:59Z pediddle $

using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Threading;

using Microsoft.Ink;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Network.Messages.Presentation;

namespace UW.ClassroomPresenter.DeckMatcher {
    /// <summary>
    /// Summary description for InkSheetMatch.
    /// </summary>
    public class InkSheetMatch : SheetMatch {
        private bool m_Disposed;

        private readonly InkSheetModel m_SourceSheet;
        private readonly InkSheetModel m_DestSheet;

        public InkSheetMatch(EventQueue sender, InkSheetModel srcSheet, InkSheetModel dstSheet) : base(sender, srcSheet, dstSheet) {
            this.m_SourceSheet = srcSheet;
            this.m_DestSheet = dstSheet;

            this.m_SourceSheet.InkAdded += new StrokesEventHandler(this.HandleInkAdded);
            this.m_SourceSheet.InkDeleting += new StrokesEventHandler(this.HandleInkDeleting);

            this.SendExistingInk();
        }

        protected override void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            try {
                if(disposing) {
                    this.m_SourceSheet.InkAdded -= new StrokesEventHandler(this.HandleInkAdded);
                    this.m_SourceSheet.InkDeleted -= new StrokesEventHandler(this.HandleInkDeleting);
                }
            }
            finally {
                base.Dispose(disposing);
            }
            this.m_Disposed = true;
        }

        private void SendExistingInk() {
            Ink extracted;
            using(Synchronizer.Lock(this.m_SourceSheet.Ink.Strokes.SyncRoot)) {
                // Ensure that each stroke has a Guid which will uniquely identify it on the remote side.
                foreach(Stroke stroke in this.m_SourceSheet.Ink.Strokes) {
                    if(!stroke.ExtendedProperties.DoesPropertyExist(InkSheetMessage.StrokeIdExtendedProperty))
                        stroke.ExtendedProperties.Add(InkSheetMessage.StrokeIdExtendedProperty, Guid.NewGuid().ToString());
                }

                // Extract all of the strokes.
                extracted = this.m_SourceSheet.Ink.ExtractStrokes(this.m_SourceSheet.Ink.Strokes, ExtractFlags.CopyFromOriginal);
            }

            // Send a message as if the already-existing ink was just added to the sheet.
            this.m_Sender.Post(delegate() {
                this.HandleInkAddedHelper(extracted);
            });
        }

        private void HandleInkAdded(object sender, StrokesEventArgs e) {
            // We must extract the added ink immediately so it can't be deleted before the
            // message is sent on another thread.

            Ink extracted;
            using( Synchronizer.Lock(this.m_SourceSheet.Ink.Strokes.SyncRoot) ) {
                using(Strokes strokes = this.m_SourceSheet.Ink.CreateStrokes(e.StrokeIds)) {
                    // Ensure that each stroke has a Guid which will uniquely identify it on the remote side.
                    foreach(Stroke stroke in strokes) {
                        if(!stroke.ExtendedProperties.DoesPropertyExist(InkSheetMessage.StrokeIdExtendedProperty))
                            stroke.ExtendedProperties.Add(InkSheetMessage.StrokeIdExtendedProperty, Guid.NewGuid().ToString());
                    }

                    extracted = this.m_SourceSheet.Ink.ExtractStrokes(strokes, ExtractFlags.CopyFromOriginal);
                }
            }

            this.m_Sender.Post(delegate() {
                this.HandleInkAddedHelper(extracted);
            });
        }

        private void HandleInkAddedHelper( Ink extracted ) {
            // Add the ink to the destination sheet
            using(Synchronizer.Lock(this.m_DestSheet.Ink.Strokes.SyncRoot)) {
                int[] newIds;
                this.LoadInkIntoTarget(this.m_DestSheet, extracted, out newIds);

                // Notify the renderers that a new set of strokes have been created.
                this.m_DestSheet.OnInkAdded(new StrokesEventArgs(newIds));
            }
        }

        private void LoadInkIntoTarget(InkSheetModel sheet, Ink extracted, out int[] ids) {
            Ink restored = extracted;

            using(Synchronizer.Lock(sheet.Ink.Strokes.SyncRoot)) {
                ids = new int[restored.Strokes.Count];

                for(int i = 0; i < ids.Length; i++) {
                    Stroke stroke = restored.Strokes[i];

                    // Remove any strokes that have the same remote Id as the new one.
                    // Unfortunately, because the InkSheetUndoService cannot preserve stroke referential identity,
                    // we must do a full search each time and cannot simply keep a table.
                    if(stroke.ExtendedProperties.DoesPropertyExist(InkSheetMessage.StrokeIdExtendedProperty)) {
                        object id = stroke.ExtendedProperties[InkSheetMessage.StrokeIdExtendedProperty].Data;
                        foreach(Stroke existing in sheet.Ink.Strokes) {
                            if(existing.ExtendedProperties.DoesPropertyExist(InkSheetMessage.StrokeIdExtendedProperty)) {
                                if(id.Equals(existing.ExtendedProperties[InkSheetMessage.StrokeIdExtendedProperty].Data)) {
                                    StrokesEventArgs args = new StrokesEventArgs(new int[] { existing.Id });
                                    sheet.OnInkDeleting(args);
                                    sheet.Ink.DeleteStroke(existing);
                                    sheet.OnInkDeleted(args);
                                }
                            }
                        }
                    }

                    // The stroke has no association with the current Ink object.
                    // Therefore, we have to recreate it by copying the raw packet data.
                    // This first requires recreating the TabletPropertyDescriptionCollection,
                    // which, for some stupid reason, must be done manually for lack of a better API.
                    TabletPropertyDescriptionCollection properties = new TabletPropertyDescriptionCollection();
                    foreach(Guid property in stroke.PacketDescription)
                        properties.Add(new TabletPropertyDescription(property, stroke.GetPacketDescriptionPropertyMetrics(property)));

                    // Create a new stroke from the raw packet data.
                    Stroke created = sheet.Ink.CreateStroke(stroke.GetPacketData(), properties);

                    // Copy the DrawingAttributes and all application data
                    // (especially the StrokesIdExtendedProperty) to the new stroke.
                    created.DrawingAttributes = stroke.DrawingAttributes;
                    foreach(ExtendedProperty prop in stroke.ExtendedProperties)
                        created.ExtendedProperties.Add(prop.Id, prop.Data);

                    ids[i] = created.Id;
                }
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
            using(Synchronizer.Lock(this.m_SourceSheet.Ink.Strokes.SyncRoot)) {
                int[] strokeIds = e.StrokeIds;
                ArrayList guids = new ArrayList(strokeIds.Length);
                using(Strokes strokes = this.m_SourceSheet.Ink.CreateStrokes(strokeIds)) {
                    foreach(Stroke stroke in strokes) {
                        if(stroke.ExtendedProperties.DoesPropertyExist(InkSheetMessage.StrokeIdExtendedProperty))
                            guids.Add(stroke.ExtendedProperties[InkSheetMessage.StrokeIdExtendedProperty].Data);
                    }
                }
                ids = ((string[]) guids.ToArray(typeof(string)));
            }

            if (ids.Length > 0)
                this.m_Sender.Post(delegate() {
                    this.HandleInkDeletingHelper(ids);
                });
        }

        private void HandleInkDeletingHelper(string[] ids) {
            // Remove the ink from the destination sheet
        }

        private delegate void InkAddedDelegate(Ink ink);
        private delegate void InkDeletedDelegate(string[] ids);
    }
}
