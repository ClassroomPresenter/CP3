// $Id: InkSheetUndoService.cs 975 2006-06-28 01:02:59Z pediddle $

using System;
using System.Collections;
using System.Diagnostics;
using System.Threading;

using Microsoft.Ink;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Model.Undo;

namespace UW.ClassroomPresenter.Undo {

    public class InkSheetUndoService : SheetUndoService {
        private readonly EventQueue m_EventQueue;
        private readonly HandleInkChangedDelegate m_HandleInkChangedDelegate;
        private readonly ArrayList m_Ignore;

        private readonly InkSheetModel m_InkSheet;
        private bool m_Disposed;

        private static readonly Guid StrokeIdExtendedProperty = Guid.NewGuid();

        public InkSheetUndoService(EventQueue dispatcher, UndoModel undo, DeckModel deck, SlideModel slide, InkSheetModel sheet) : base(undo, deck, slide, sheet) {
            this.m_EventQueue = dispatcher;
            this.m_InkSheet = sheet;

            this.m_Ignore = new ArrayList();

            //Ignore any sheets with the Remote flag
            if ((this.m_InkSheet.Disposition & SheetDisposition.Remote) == 0) {
                this.m_HandleInkChangedDelegate = new HandleInkChangedDelegate(this.HandleInkChanged);
                this.m_InkSheet.InkAdded += new StrokesEventHandler(this.HandleInkAdded);
                this.m_InkSheet.InkDeleting += new StrokesEventHandler(this.HandleInkDeleting);
            }
        }

        protected override void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            try {
                if(disposing) {
                    this.m_InkSheet.InkAdded -= new StrokesEventHandler(this.HandleInkAdded);
                    this.m_InkSheet.InkDeleting -= new StrokesEventHandler(this.HandleInkDeleting);
                }
            } finally {
                base.Dispose(disposing);
            }
            this.m_Disposed = true;
        }

        private void HandleInkAdded(object sender, StrokesEventArgs e) {
            using(Synchronizer.Lock(this.m_Ignore.SyncRoot)) {
                if(this.m_Ignore.Contains(e)) {
                    this.m_Ignore.Remove(e);
                    return;
                }
            }

            // Make a copy of the strokes that were added.  This must be done immediately
            // or the strokes might not exist by the time we need them.
            Strokes strokes = this.m_InkSheet.Ink.CreateStrokes(e.StrokeIds);
            SetStrokeIds(strokes);
            Ink ink = this.m_InkSheet.Ink.ExtractStrokes(strokes, ExtractFlags.CopyFromOriginal);
            this.m_EventQueue.Post(delegate() {
                this.m_HandleInkChangedDelegate(sender, ink.Strokes, true);
            });
        }

        private void HandleInkDeleting(object sender, StrokesEventArgs e) {
            using(Synchronizer.Lock(this.m_Ignore.SyncRoot)) {
                if(this.m_Ignore.Contains(e)) {
                    this.m_Ignore.Remove(e);
                    return;
                }
            }

            // Make a copy of the strokes that were removed.  This must be done immediately
            // or the strokes _will_not_ exist by the time we need them.
            Strokes strokes = this.m_InkSheet.Ink.CreateStrokes(e.StrokeIds);
            SetStrokeIds(strokes);
            Ink ink = this.m_InkSheet.Ink.ExtractStrokes(strokes, ExtractFlags.CopyFromOriginal);
            this.m_EventQueue.Post(delegate() {
                this.m_HandleInkChangedDelegate(sender, ink.Strokes, false);
            });
        }

        private delegate void HandleInkChangedDelegate(object sender, Strokes changed, bool adding);

        private void HandleInkChanged(object sender, Strokes changed, bool adding) {
            using(Synchronizer.Lock(this.Undo.SyncRoot)) {
                // Create an IUndoer that will re-add or re-delete the copied strokes.
                IUndoer undoer = new InkUndoer(this, changed, adding);
                this.Undo.Push(undoer);
            }
        }

        private static void SetStrokeIds(Strokes strokes) {
            foreach(Stroke stroke in strokes)
                if(!stroke.ExtendedProperties.DoesPropertyExist(StrokeIdExtendedProperty))
                    stroke.ExtendedProperties.Add(StrokeIdExtendedProperty, Guid.NewGuid().ToString());
        }

        private class InkUndoer : IUndoer {
            private readonly InkSheetUndoService m_Watcher;

            // Note: The Stroke.ExtendedProperties collection can only store data of certain primitive types.
            // This array is really an array of Guids, but Guids cannot be stored directly.
            // Another option would be to convert the Guids to byte arrays, but then testing equality
            // would be more difficult.  Therefore, strings seem to be the best option.
            private string[] m_StrokesToRemove;

            private Strokes m_StrokesToAdd;
            private readonly bool m_Adding;

            public InkUndoer(InkSheetUndoService watcher, Strokes strokes, bool adding) {
                this.m_Watcher = watcher;

                if(adding) {
                    this.UpdateAndSetStrokesToRemoveIds(strokes);
                } else {
                    this.m_StrokesToAdd = strokes;
                }

                this.m_Adding = adding;
            }

            #region IUndoer Members

            public void Undo() {
                if(this.m_Adding)
                    this.RemoveInk();
                else this.AddInk();
            }

            public void Redo() {
                if(this.m_Adding)
                    this.AddInk();
                else this.RemoveInk();
            }

            public object[] Actors {
                get { return new object[] { this.m_Watcher.Sheet, this.m_Watcher.Slide, this.m_Watcher.Deck }; }
            }

            #endregion

            private void AddInk() {
                using(Synchronizer.Lock(this)) {
                    // Create an array of stroke Ids in order to fire the InkAdded event later.
                    int[] ids = new int[this.m_StrokesToAdd.Count];

                    using (Synchronizer.Lock(this.m_Watcher.m_InkSheet.Ink.Strokes.SyncRoot)) {
                        for (int i = 0; i < ids.Length; i++) {
                            Stroke stroke = this.m_StrokesToAdd[i];

                            // The stroke probably has no association with the current Ink object.
                            // Therefore, we have to recreate it by copying the raw packet data.
                            // This first requires recreating the TabletPropertyDescriptionCollection,
                            // which, for some stupid reason, must be done manually for lack of a better API.
                            TabletPropertyDescriptionCollection properties = new TabletPropertyDescriptionCollection();
                            foreach (Guid property in stroke.PacketDescription)
                                properties.Add(new TabletPropertyDescription(property, stroke.GetPacketDescriptionPropertyMetrics(property)));

                            // Create a new stroke from the raw packet data.
                            Stroke created = this.m_Watcher.m_InkSheet.Ink.CreateStroke(stroke.GetPacketData(), properties);

                            // Copy the DrawingAttributes and all application data
                            // (especially the StrokesIdExtendedProperty) to the new stroke.
                            created.DrawingAttributes = stroke.DrawingAttributes;
                            foreach (ExtendedProperty prop in stroke.ExtendedProperties)
                                created.ExtendedProperties.Add(prop.Id, prop.Data);

                            // Get the new stroke's Id so we can fire the InkAdded event.
                            ids[i] = created.Id;
                        }

                        // If the added strokes don't yet have StrokeIdExtendedProperty properties,
                        // create new Guids for them.  Regardless, set this.m_StrokesToRemove
                        // to the list of stroke Guids.
                        this.UpdateAndSetStrokesToRemoveIds(this.m_StrokesToAdd);

                        // Then, unset this.m_StrokesToAdd since they're already added.
                        this.m_StrokesToAdd = null;
                    }

                    // Create the event arguments and add them to the ignore list so the
                    // InkSheetUndoService won't create an InkUndoer for this change.
                    StrokesEventArgs args = new StrokesEventArgs(ids);
                    using(Synchronizer.Lock(this.m_Watcher.m_Ignore.SyncRoot)) {
                        this.m_Watcher.m_Ignore.Add(args);
                    }

                    // Finally fire the appropriate InkAdded event from the InkSheetModel.
                    this.m_Watcher.m_InkSheet.OnInkAdded(args);
                }
            }

            private void RemoveInk() {
                using (Synchronizer.Lock(this)) {
                    // Collect all of the strokes we're supposed to delete.
                    using (Synchronizer.Lock(this.m_Watcher.m_InkSheet.Ink.Strokes.SyncRoot)) {
                        Strokes deleting = this.m_Watcher.m_InkSheet.Ink.CreateStrokes();
                        foreach (Stroke stroke in this.m_Watcher.m_InkSheet.Ink.Strokes)
                            if (stroke.ExtendedProperties.DoesPropertyExist(StrokeIdExtendedProperty))
                                if (Array.IndexOf(this.m_StrokesToRemove, stroke.ExtendedProperties[StrokeIdExtendedProperty].Data) >= 0)
                                    deleting.Add(stroke);

                        // It's possible that some of the strokes have been deleted elsewhere.
                        // But this shouldn't happen because doing so should have caused an InkUndoer to be
                        // pushed onto the Undo stack.  So, for now, this check is "merely" a Debug.Assert.
                        // TODO: Decide whether this should be an error, or at least whether it should
                        //   invalidate the rest of the undo stack.
                        Debug.Assert(deleting.Count == this.m_StrokesToRemove.Length);

                        // Get the stroke Ids so we can make a copy of the ink and fire the OnDeleting and OnDeleted events.
                        int[] ids = new int[deleting.Count];
                        for (int i = 0; i < ids.Length; i++) // Is there a better way to get the array of Ids?
                            ids[i] = deleting[i].Id;

                        // Make a copy of the strokes, because if the original ink is later deleted
                        // from the Ink object then it will become unusable.
                        Ink ink = this.m_Watcher.m_InkSheet.Ink.ExtractStrokes(deleting, ExtractFlags.CopyFromOriginal);
                        this.m_StrokesToAdd = ink.Strokes;
                        this.m_StrokesToRemove = null;

                        // Create the event arguments and add them to the ignore list so the
                        // InkSheetUndoService won't create an InkUndoer for this change.
                        StrokesEventArgs args = new StrokesEventArgs(ids);
                        using (Synchronizer.Lock(this.m_Watcher.m_Ignore.SyncRoot)) {
                            this.m_Watcher.m_Ignore.Add(args);
                        }

                        // Actually delete the ink, firing the appropriate events on the InkSheetModel.
                        this.m_Watcher.m_InkSheet.OnInkDeleting(args);
                        this.m_Watcher.m_InkSheet.Ink.DeleteStrokes(deleting);
                        this.m_Watcher.m_InkSheet.OnInkDeleted(args);
                    }
                }
            }

            private void UpdateAndSetStrokesToRemoveIds(Strokes strokes) {
                using (Synchronizer.Lock(this.m_Watcher.m_InkSheet.Ink.Strokes.SyncRoot)) {
                    Debug.Assert(this.m_StrokesToRemove == null);
                    string[] ids = new string[strokes.Count];
                    for (int i = 0; i < ids.Length; i++) {
                        Stroke stroke = strokes[i];
                        Debug.Assert(stroke.ExtendedProperties.DoesPropertyExist(StrokeIdExtendedProperty));
                        ids[i] = ((string)stroke.ExtendedProperties[StrokeIdExtendedProperty].Data);
                    }
                    this.m_StrokesToRemove = ids;
                }
            }
        }
    }
}
