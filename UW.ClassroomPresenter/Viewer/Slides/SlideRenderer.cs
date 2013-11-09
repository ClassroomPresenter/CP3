// $Id: SlideRenderer.cs 1268 2007-01-04 23:59:13Z fred $

using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using System.Drawing.Drawing2D;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Model.Viewer;

namespace UW.ClassroomPresenter.Viewer.Slides {
    public class SlideRenderer : IDisposable {
        private readonly SlideDisplayModel m_SlideDisplay;
        private readonly EventQueue.PropertyEventDispatcher m_SlideChangedDispatcher;
        private readonly EventQueue.PropertyEventDispatcher m_SheetDispositionChangedDispatcher;

        private readonly string m_SheetsCollectionTag;
        private readonly SheetRenderersCollection m_SheetRenderers;
        private SlideModel m_Slide;

        private bool m_Disposed;

        private SheetsCollectionHelper m_SheetsCollectionHelper;

        #region Construction & Destruction
        public SlideRenderer(SlideDisplayModel display, string sheetsCollectionTag) {
            this.m_SlideDisplay = display;
            this.m_SheetsCollectionTag = sheetsCollectionTag;
            this.m_SheetRenderers = new SheetRenderersCollection();

            this.m_SlideChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_SlideDisplay.EventQueue, new PropertyEventHandler(this.HandleSlideChanged));
            this.m_SheetDispositionChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_SlideDisplay.EventQueue, new PropertyEventHandler(this.HandleSheetDispositionChanged));

            this.m_SlideDisplay.Changed["Slide"].Add(this.m_SlideChangedDispatcher.Dispatcher);
            this.m_SlideDisplay.Changed["SheetDisposition"].Add(this.m_SheetDispositionChangedDispatcher.Dispatcher);
        }

        ~SlideRenderer() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            if(disposing) {
                this.m_SlideDisplay.Changed["Slide"].Remove(this.m_SlideChangedDispatcher.Dispatcher);
                this.m_SlideDisplay.Changed["SheetDisposition"].Remove(this.m_SheetDispositionChangedDispatcher.Dispatcher);

                // Unregister event listeners via the Slide setter.
                this.Slide = null;
            }
            this.m_Disposed = true;
        }

        #endregion Construction & Destruction

        protected virtual SlideModel Slide {
            get { return this.m_Slide; }
            set {
                if (value == this.m_Slide) return;
                if (this.m_Slide != null) {
                    this.m_SheetsCollectionHelper.Dispose();
                    this.m_SheetsCollectionHelper = null;
                }

                this.m_Slide = value;
                if (this.m_Slide != null) {
                    this.m_SheetsCollectionHelper = new SheetsCollectionHelper(this, this.m_Slide, this.m_SheetsCollectionTag);
                }
            }
        }

        private void HandleSlideChanged(object sender, PropertyEventArgs args) {
            using(Synchronizer.Lock(this.m_SlideDisplay.SyncRoot)) {
                this.Slide = this.m_SlideDisplay.Slide;
            }
        }

        private void HandleSheetDispositionChanged(object sender, PropertyEventArgs args) {
            this.m_SlideDisplay.Invalidate();
        }

        private class SheetsCollectionHelper : PropertyCollectionHelper {
            private readonly SlideRenderer m_Parent;
            private bool m_Disposed = false;

            public SheetsCollectionHelper(SlideRenderer parent, SlideModel publisher, string property) : base(parent.m_SlideDisplay.EventQueue, publisher, property) {
                this.m_Parent = parent;
                base.Initialize();
            }

            protected override void Dispose(bool disposing) {
                m_Disposed = true;
                if(disposing)
                    foreach(SheetRenderer renderer in this.Tags)
                        this.TearDownMember(-1, null, renderer);
                base.Dispose(disposing);     
            }

            protected override object SetUpMember(int index, object member) {
                //If a set of slide transitions occur rapidly, we can see a case where the enqueued SetUpMember call is 
                //invoked *after* dispose, causing bogus sheets to appear.  Explicitly short-circuiting this call 
                //after a dispose seems to be the fix:
                if (m_Disposed)
                    return null;

                // FIXME: Invalidate the affected region, but don't render immediately in case more than one sheet is affected.

                SheetRenderer renderer = SheetRenderer.ForSheet(this.m_Parent.m_SlideDisplay, (SheetModel) member);

                ///Invalidate the renderer...might not be a great idea for the reason above.
                renderer.SlideDisplay.Invalidate();

                // Ignore sheets for which a renderer isn't available.
                if(renderer == null) return null;

                // Add the renderer to the list.
                using(Synchronizer.Lock(this.m_Parent)) { // Prevent m_SheetRenderers list from changing.

                    if(index >= 0 && index < this.m_Parent.m_SheetRenderers.Count) {
                        this.m_Parent.m_SheetRenderers.Insert(index, renderer);
                    } else {
                        this.m_Parent.m_SheetRenderers.Add(renderer);
                    }
                }

                return renderer;
            }

            protected override void TearDownMember(int index, object member, object tag) {

                // FIXME: Invalidate the affected portion, but don't render immediately in case more than one sheet is affected.
                SheetRenderer renderer = ((SheetRenderer) tag);
                if(tag != null) {
                    this.m_Parent.m_SheetRenderers.Remove(renderer);
                    renderer.Dispose();
                }

            }
        }

        /// <summary>
        /// Called by users of a <see cref="SlideRenderer"/> to paint the slide's contents
        /// onto the specified <see cref="Graphics"/> instance.
        /// </summary>
        public virtual void Paint(PaintEventArgs args) {
            // Simply tell all of the SheetRenderers to paint themselves.
            // The renderers will handle testing that the clip is inside their bounds.

            // TODO: Paint each group of sheets onto a cached bitmap, and only update the bitmap when necessary.
            // TODO: Adding ink strokes, for example, shouldn't require a complete redraw; just paint the added stroke on top of the other annotations.

            Graphics g = args.Graphics;

            using(Synchronizer.Lock(this.m_SlideDisplay.SyncRoot)) {
                g.IntersectClip(this.m_SlideDisplay.Bounds);
                args = new PaintEventArgs(g, Rectangle.Ceiling(g.ClipBounds));

                //The SheetRenderersCollection appears to have indeterminate order, but we need to render background first.
                foreach (SheetRenderer renderer in this.m_SheetRenderers) {
                    if ((isBackgroundSheet(renderer.Sheet)) && (this.determineIfDisplay(renderer.Sheet))) {
                        GraphicsState state = g.Save();
                        try {
                            renderer.Paint(args);
                        } finally {
                            g.Restore(state);
                        }
                    }
                }

                //next render non-background sheets
                foreach(SheetRenderer renderer in this.m_SheetRenderers) {
                    if ((!isBackgroundSheet(renderer.Sheet)) && (this.determineIfDisplay(renderer.Sheet))) {
                        GraphicsState state = g.Save();
                        try {
                            renderer.Paint(args);
                        } finally {
                            g.Restore(state);
                        }
                    }
                }
            }
        }

        private bool isBackgroundSheet(SheetModel sm) {
            using(Synchronizer.Lock(sm.SyncRoot)) {
                return ((sm.Disposition & SheetDisposition.Background) != 0);
            }
        }

        private bool determineIfDisplay(SheetModel sm) {
            using(Synchronizer.Lock(this.m_SlideDisplay.SyncRoot)) {
                using(Synchronizer.Lock(sm.SyncRoot)) {
                    return ((sm.Disposition & this.m_SlideDisplay.SheetDisposition) != 0);
                }
            }
        }
    }
}
