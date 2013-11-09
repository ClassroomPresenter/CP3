// $Id: FilmStripSlideViewer.cs 1898 2009-07-06 18:48:47Z jing $

using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Windows.Forms;

using System.Runtime.InteropServices;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Background;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Model.Workspace;
using UW.ClassroomPresenter.Viewer.Background;
using UW.ClassroomPresenter.Viewer.Slides;

namespace UW.ClassroomPresenter.Viewer.FilmStrips {
    internal class FilmStripSlideViewer : SlideViewer {
        private readonly EventQueue.PropertyEventDispatcher m_CurrentEntryChangedDispatcher;
        private readonly EventQueue.PropertyEventDispatcher m_EntriesChangedDispatcher;
        private readonly IDisposable m_CurrentDeckTraversalChangedDispatcher;
        private readonly EventQueue.PropertyEventDispatcher m_DeckBackgroundColorChangedDispatcher;
        private readonly EventQueue.PropertyEventDispatcher m_DeckBackgroundTemplateChangedDispatcher;

        private readonly PresenterModel m_Model;
        public TableOfContentsModel.Entry m_Entry;
        private readonly DeckTraversalModel m_DeckTraversal;
        private readonly DeckTraversalModel m_PreviewDeckTraversal;
        private readonly MultiColumnFilmStrip m_Parent;
        private bool m_AlreadyFitSlideToViewerBounds;
        private bool m_IsCurrentEntry;
        private bool m_IsCurrentDeckTraversal;
        private int m_ChildrenCount;
        private bool m_Disposed;

        // The width of the 3D border drawn by ControlPaint.DrawBorder3D.
        private const int BORDER_WIDTH = 2;

        public enum FloatingDimension {
            Width = 0,
            Height = 1
        }

        public FloatingDimension Floating = FloatingDimension.Height;

        // The highlight color
        private readonly Color HIGHLIGHT_COLOR = Color.SteelBlue;

        // The width of the highlighted border drawn when the FilmStripSlideViewer's entry is the current one.
        private static readonly int HIGHLIGHT_BORDER_WIDTH = (int)(SystemPens.Highlight.Width * 7);

        // Number of pixels each "layered" slide will be offset relative to the main slide view
        // when displaying an entry with children.
        private const int HIERARCHICAL_INDENT = 6;

        // Maximum number of "layered" slides that will be shown behind the main entry.
        private const int HIERARCHICAL_MAX_NESTED = 2;

        // If m_ChildrenCount > 0, this is the width of the space at the bottom of the control.
        private const int HIERARCHICAL_BOTTOM = HIERARCHICAL_INDENT;

        // Minimum size, in pixels, of width and height of the slide bounds, after reduction by the various borders.
        // This is for Bug 605; used by FitSlideToViewerBounds.  This value is arbitrary.
        private const int MINIMUM_SLIDE_SIZE = 10;

        public FilmStripSlideViewer(MultiColumnFilmStrip parent, PresenterModel model, DeckTraversalModel traversal, DeckTraversalModel previewTraversal, TableOfContentsModel.Entry entry) {
            this.m_Model = model;
            this.m_Parent = parent;
            this.m_DeckTraversal = traversal;
            this.m_Entry = entry;

            this.m_PreviewDeckTraversal = previewTraversal;

            this.BorderStyle = BorderStyle.FixedSingle;

            this.m_CurrentEntryChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.SlideDisplay.EventQueue, new PropertyEventHandler(this.HandleCurrentEntryChanged));
            this.m_EntriesChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.SlideDisplay.EventQueue, new PropertyEventHandler(this.HandleEntriesChanged));
            this.m_DeckBackgroundColorChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.SlideDisplay.EventQueue, new PropertyEventHandler(this.HandleDeckBackgroundColorChanged));
            this.m_DeckBackgroundTemplateChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.SlideDisplay.EventQueue, new PropertyEventHandler(this.HandleDeckBackgroundTemplateChanged));

            this.m_DeckTraversal.Changed["Current"].Add(this.m_CurrentEntryChangedDispatcher.Dispatcher);
            this.m_Entry.TableOfContents.Changed["Entries"].Add(this.m_EntriesChangedDispatcher.Dispatcher);
            using (Synchronizer.Lock(this.m_DeckTraversal.Deck.SyncRoot)) {
                this.m_DeckTraversal.Deck.Changed["DeckBackgroundColor"].Add(this.m_DeckBackgroundColorChangedDispatcher.Dispatcher);
                this.m_DeckTraversal.Deck.Changed["DeckBackgroundTemplate"].Add(this.m_DeckBackgroundTemplateChangedDispatcher.Dispatcher);
                this.DefaultDeckBGColor = this.m_DeckTraversal.Deck.DeckBackgroundColor;
                this.DefaultDeckBGTemplate = this.m_DeckTraversal.Deck.DeckBackgroundTemplate;
            }

            this.m_EntriesChangedDispatcher.Dispatcher(this.m_Entry, null);
            this.m_CurrentEntryChangedDispatcher.Dispatcher(this.m_DeckTraversal, null);

            this.m_CurrentDeckTraversalChangedDispatcher =
                this.m_Model.Workspace.CurrentDeckTraversal.ListenAndInitialize(this.SlideDisplay.EventQueue,
                delegate(Property<DeckTraversalModel>.EventArgs args) {
                    bool old = this.m_IsCurrentDeckTraversal;

                    using (this.m_Model.Workspace.Lock()) {
                        this.m_IsCurrentDeckTraversal = (~this.m_Model.Workspace.CurrentDeckTraversal == this.m_DeckTraversal);
                    }

                    if (old != this.m_IsCurrentDeckTraversal) {
                        // Invalidate the display to draw the new selection border.
                        this.UpdateSelection();
                    }
                });
        }

        protected override void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            try {
                if(disposing) {
                    this.m_DeckTraversal.Changed["Current"].Remove(this.m_CurrentEntryChangedDispatcher.Dispatcher);
                    this.m_Entry.TableOfContents.Changed["Entries"].Remove(this.m_EntriesChangedDispatcher.Dispatcher);
                    this.m_CurrentDeckTraversalChangedDispatcher.Dispose();
                    using (Synchronizer.Lock(this.m_DeckTraversal.Deck.SyncRoot)) {
                        this.m_DeckTraversal.Deck.Changed["DeckBackgroundColor"].Remove(this.m_DeckBackgroundColorChangedDispatcher.Dispatcher);
                        this.m_DeckTraversal.Deck.Changed["DeckBackgroundTemplate"].Remove(this.m_DeckBackgroundTemplateChangedDispatcher.Dispatcher);
                    }
                }
            } finally {
                base.Dispose(disposing);
            }
            this.m_Disposed = true;
        }

        protected override void OnPaint( PaintEventArgs args ) {
            if (this.Slide == null) //Don't paint before the slide is set
                return;

            Color bkgColor;                 // Set the filmstrip background color
            BackgroundTemplate bkgTemplate;
            float slideZoom = 1f;
            RoleModel role;
            using (Synchronizer.Lock(PresenterModel.TheInstance.Participant.SyncRoot)) {
                role = PresenterModel.TheInstance.Participant.Role;
            }
            LinkedDeckTraversalModel.NavigationSelector studentNavigationType;
            using (Synchronizer.Lock(PresenterModel.TheInstance.ViewerState.SyncRoot)) {
                studentNavigationType = PresenterModel.TheInstance.ViewerState.StudentNavigationType;
            }
            using (Synchronizer.Lock(this.Slide.SyncRoot)) {
                bkgColor = (!this.Slide.BackgroundColor.IsEmpty) ? this.Slide.BackgroundColor : this.DefaultDeckBGColor;
                // For student role, paint unvisited slides gray.
                if (role is StudentModel && (!this.Slide.Visited) && studentNavigationType == LinkedDeckTraversalModel.NavigationSelector.Visited)
                    bkgColor = SystemColors.Control;
                bkgTemplate = (this.Slide.BackgroundTemplate != null) ? this.Slide.BackgroundTemplate : this.DefaultDeckBGTemplate;
                slideZoom = this.Slide.Zoom;
            }

            args.Graphics.Clear(bkgColor);

            if (bkgTemplate != null) {
                BackgroundTemplateRenderer render = new BackgroundTemplateRenderer(bkgTemplate);
                render.Zoom = slideZoom;
                render.DrawAll(args.Graphics, this.ClientRectangle);
            }

            base.OnPaint( args );

            // If the entry is selected, draw a thick border just inside of the 3D border.
            if( this.m_IsCurrentEntry ) {
                Rectangle highlight = this.ClientRectangle;

                if( this.m_IsCurrentDeckTraversal ) {
                    using( Pen pen = new Pen( HIGHLIGHT_COLOR, HIGHLIGHT_BORDER_WIDTH ) ) {
                        args.Graphics.DrawRectangle( pen, highlight );
                    }
                } else {
                    using( Pen pen = new Pen( HIGHLIGHT_COLOR, HIGHLIGHT_BORDER_WIDTH ) ) {
                        pen.DashStyle = DashStyle.Dot;
                        args.Graphics.DrawRectangle( pen, highlight );
                    }
                }
            }
        }
         
        protected override void OnDockChanged(EventArgs e) {
            base.OnDockChanged(e);
            this.LayoutSlide();
        }

        protected override void OnSizeChanged(EventArgs e) {
            base.OnSizeChanged(e);

            if(!this.m_AlreadyFitSlideToViewerBounds) {
                this.LayoutSlide();
            }
        }

        public event MouseEventHandler MyMouseDown;
        protected override void OnMouseDown(MouseEventArgs e) {
            this.Capture = false;

            MyMouseDown(this,e);
//            base.OnMouseDown(e);
        }

        /// <summary>
        /// Makes this the active slide in the deck
        /// </summary>
        public void Activate() {
            // Only set the deck traversal if it isn't already set
            if( !this.m_IsCurrentEntry ) {
                using(Synchronizer.Lock(this.m_DeckTraversal.SyncRoot)) {
                    this.m_DeckTraversal.Current = this.m_Entry;
                }
            }

            // Only set the current deck if it isn't already set
            if( !this.m_IsCurrentDeckTraversal ) {
                // If the user's role is an InstructorModel, set the InstructorModel's CurrentDeckTraversal.
                // The NetworkAssociationService will then use this to set the WorkspaceModel's CurrentDeckTraversal,
                // and the new deck traversal will also be broadcast.
                // TODO: This logic should be extracted to a service class.  It is shared with DeckStrip.DeckCollectionHelper.SetUpMember.
                // NOTE: Using a temporary variable IsInstructor to resolve lock ordering issue. 
                //   We don't want to lock the workspace model after the participant model in our code.
                bool IsInstructor = false;
                using( Synchronizer.Lock( this.m_Model.Participant.SyncRoot ) ) {
                    InstructorModel instructor = this.m_Model.Participant.Role as InstructorModel;
                    if( instructor != null ) {
                        IsInstructor = true;
                        using(Synchronizer.Lock(instructor.SyncRoot)) {
                            instructor.CurrentDeckTraversal = this.m_DeckTraversal;
                        }
                    }
                }
                if( !IsInstructor ) {
                    // Otherwise, we must set the CurrentDeckTraversal on the WorkspaceModel directly.
                    using(this.m_Model.Workspace.Lock()) {
                        this.m_Model.Workspace.CurrentDeckTraversal.Value = this.m_DeckTraversal;
                    }
                }
            }
        }

        // When you enter this slide, set the slide preview slide to this slide
        protected override void OnMouseEnter(EventArgs e) {
            base.OnMouseEnter(e);

            // Must set the current deck entry
            using(Synchronizer.Lock(this.m_PreviewDeckTraversal.SyncRoot)) {
                this.m_PreviewDeckTraversal.Current = this.m_Entry;
            }

            // Set the current deck traversal
            using(this.m_Model.Workspace.Lock()) {
                this.m_Model.Workspace.CurrentSlidePreviewDeckTraversal.Value = this.m_PreviewDeckTraversal;
            }
        }


        private void HandleCurrentEntryChanged(object sender, PropertyEventArgs args) {
            bool old = this.m_IsCurrentEntry;

            using(Synchronizer.Lock(this.m_DeckTraversal.SyncRoot)) {
                this.m_IsCurrentEntry = (this.m_DeckTraversal.Current == this.m_Entry);
            }

            if(old != this.m_IsCurrentEntry) {
                // Invalidate the display to draw the new selection border.
                this.UpdateSelection();
            }
        }

        private void HandleDeckBackgroundColorChanged(object sender, PropertyEventArgs args) {
            if (this.m_DeckTraversal != null) {
                using (Synchronizer.Lock(this.m_DeckTraversal.SyncRoot)) {
                    using (Synchronizer.Lock(this.m_DeckTraversal.Deck.SyncRoot)) {
                        this.DefaultDeckBGColor = this.m_DeckTraversal.Deck.DeckBackgroundColor;
                    }
                }
            }
        }

        private void HandleDeckBackgroundTemplateChanged(object sender, PropertyEventArgs args)
        {
            if (this.m_DeckTraversal != null) {
                using (Synchronizer.Lock(this.m_DeckTraversal.SyncRoot)) {
                    using (Synchronizer.Lock(this.m_DeckTraversal.Deck.SyncRoot)) {
                        this.DefaultDeckBGTemplate = this.m_DeckTraversal.Deck.DeckBackgroundTemplate;
                    }
                }
            }
        }

        private void UpdateSelection() {
            this.Invalidate();

            // Scroll the film strip to center this item.
            // This is done by calling what will be the topmost and bottommost 
            // visible slides into view.
            if(this.m_IsCurrentEntry && this.m_IsCurrentDeckTraversal) {
                int fswidth = 0;
                using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot)) {
                    if (this.m_DeckTraversal.Deck.Disposition == DeckDisposition.StudentSubmission)
                        fswidth = this.m_Model.ViewerState.SSFilmStripWidth;
                    else
                        fswidth = this.m_Model.ViewerState.FilmStripWidth;
                }
                int zOrder = this.m_Parent.Controls.IndexOf(this);
                float shown = this.m_Parent.Height / this.Height;
                int offset = (int)(shown / 2);
                this.m_Parent.ScrollControlIntoView(this.m_Parent.Controls[Math.Min(this.m_Parent.Controls.Count-1, zOrder + (offset*fswidth))]);
                this.m_Parent.ScrollControlIntoView(this.m_Parent.Controls[Math.Max(0, zOrder -(offset*fswidth))]);

            }
        }

        private void HandleEntriesChanged(object sender, PropertyEventArgs args) {
            if(sender == this.m_Entry) {
                using(Synchronizer.Lock(this.m_Entry.SyncRoot)) {
                    this.m_ChildrenCount = this.m_Entry.Children.Count;
                    #region Start Bug 803
                    ///BUG: sometimes, this.Slide has more than one InkSheet, even though
                    ///it should only have one. The other InkSheets often have erased ink
                    ///that the instructor should not see, but he does.
                    this.Slide = this.m_Entry.Slide;
#endregion
                    // TODO: Only refresh the display if things have changed.
                    this.LayoutSlide();
                    this.Invalidate();
                }
            }
        }

        protected override void LayoutSlide() {
            // Prevent OnSizeChanged from calling FitSlideToViewerBounds recursively after we change the Height.
            this.m_AlreadyFitSlideToViewerBounds = true;
            try {
                Rectangle bounds = this.ClientRectangle;

                // Bug 605: Enforce a minimum area for the slide bounds to avoid an invalid PixelTransform.
                if( bounds.Width < MINIMUM_SLIDE_SIZE || bounds.Height < MINIMUM_SLIDE_SIZE ) {
                    this.MinimumSize = new Size( MINIMUM_SLIDE_SIZE, MINIMUM_SLIDE_SIZE );

                    // Changing the minimum size will (eventually) cause FitSlideToViewerBounds to be called again.
                    return;
                }

                Rectangle slide;
                float zoom;
                if( this.Slide != null ) {
                    using( Synchronizer.Lock( this.Slide.SyncRoot ) ) {
                        slide = this.Slide.Bounds;
                        zoom = this.Slide.Zoom;
                    }
                } else {
                    slide = Rectangle.Empty;
                    zoom = 1f;
                }

                Matrix pixel, ink;
                if( this.Floating == FloatingDimension.Height )
                    this.SlideDisplay.FitSlideToBounds( DockStyle.Top, bounds, zoom, ref slide, out pixel, out ink );
                else
                    this.SlideDisplay.FitSlideToBounds( DockStyle.Left, bounds, zoom, ref slide, out pixel, out ink );

                using( Synchronizer.Lock( this.SlideDisplay.SyncRoot ) ) {
                    this.SlideDisplay.Bounds = slide;
                    this.SlideDisplay.PixelTransform = pixel;
                    this.SlideDisplay.InkTransform = ink;
                }

                // Resize the control to show the entire slide.
                // Compensate for the borders which were removed from the slide.
                // Also add a bit of padding if we're displaying nested layers.
                switch( this.Floating ) {
                    case FloatingDimension.Height:
                        this.Height = slide.Height+2;
                        break;
                    case FloatingDimension.Width:
                        this.Width = slide.Width+2;
                        break;
                    default:
                        break;
                }

                // Now that the slide's bounds have changed, we must repaint the slide.
                this.Invalidate();
            } finally {
                this.m_AlreadyFitSlideToViewerBounds = false;
            }
        }
    }
}
