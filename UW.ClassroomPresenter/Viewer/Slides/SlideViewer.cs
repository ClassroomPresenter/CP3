// $Id: SlideViewer.cs 2051 2009-08-19 18:20:16Z lining $

using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Windows.Forms;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Background;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Model.Viewer;
using UW.ClassroomPresenter.Model.Network;

namespace UW.ClassroomPresenter.Viewer.Slides {
    /// <summary>
    /// Provides basic functionality for viewing slides.
    /// <para>
    /// The viewer displays the slide's content sheets, and listens for changes
    /// to the sheets, their order, or their content, repainting the slide
    /// whenever necessary.
    /// </para>
    /// </summary>
    public abstract class SlideViewer : UserControl, DeckTraversalModelAdapter.IAdaptee {
        protected  ControlEventQueue m_EventQueue;
        private readonly SlideRenderer m_ContentSlideRenderer;
        private readonly SlideRenderer m_AnnotationsSlideRenderer;
        protected  SlideDisplayModel m_SlideDisplay;
        private SlideModel m_Slide;
        private bool m_Disposed;
        private Color m_DefaultDeckBGColor;
        private BackgroundTemplate m_DeckBackgroundTemplate;
        public int SlideWidth;
        public int SlideHeight;

        // HACK: The PixelTransform will become invalid and some SheetRenderers will fail
        //   if the size of the control becomes too small.  This prevents this from happening.
        //   The slide is useless if it's smaller than about this size anyway.
        private Size m_MinimumSize = new Size(16, 16);

        #region Construction & Destruction

        public SlideViewer() {
            if (this.m_EventQueue == null) {
                this.m_EventQueue = new ControlEventQueue(this);
            }
            if (this.m_SlideDisplay == null) {
                this.m_SlideDisplay = new SlideDisplayModel(this, this.m_EventQueue);
            }
            this.m_DefaultDeckBGColor = Color.White;

            // Enable double-buffering of the SlideView.
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.DoubleBuffer | ControlStyles.UserPaint, true);

            this.m_ContentSlideRenderer = new SlideRenderer(this.m_SlideDisplay, "ContentSheets");
            this.m_AnnotationsSlideRenderer = new SlideRenderer(this.m_SlideDisplay, "AnnotationSheets");

            this.m_SlideDisplay.Invalidated += new InvalidateEventHandler(this.HandleDisplayInvalidated);

            this.Name = "SlideViewer";


        }


        protected override void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            try {
                if(disposing) {
                    this.m_SlideDisplay.Invalidated -= new InvalidateEventHandler(this.HandleDisplayInvalidated);

                    // Note: this duplicates code in the Slide setter.
                    if(this.m_Slide != null) {
                        this.m_Slide.Changed["Bounds"].Remove(new PropertyEventHandler(this.HandleSlideBoundsChanged));
                        this.m_Slide.Changed["Zoom"].Remove(new PropertyEventHandler(this.HandleSlideBoundsChanged));
                        this.m_Slide.Changed["BackgroundColor"].Remove(new PropertyEventHandler(this.HandleSlideBoundsChanged));
                        this.m_Slide.Changed["BackgroundTemplate"].Remove(new PropertyEventHandler(this.HandleSlideBoundsChanged));
                    }

                    this.m_ContentSlideRenderer.Dispose();
                    this.m_AnnotationsSlideRenderer.Dispose();

                    this.m_EventQueue.Dispose();
                }
            } finally {
                base.Dispose(disposing);
            }
            this.m_Disposed = true;
        }

        #endregion Construction & Destruction

        public virtual Color DefaultDeckBGColor {
            get { return this.m_DefaultDeckBGColor; }
            set {
                if (this.m_DefaultDeckBGColor != value) {
                    this.m_DefaultDeckBGColor = value;
                    this.HandleSlideBoundsChanged(this, null);
                }
            }
        }

        public virtual BackgroundTemplate DefaultDeckBGTemplate {
            get { return this.m_DeckBackgroundTemplate; }
            set {
                if (this.m_DeckBackgroundTemplate != value) {
                    this.m_DeckBackgroundTemplate = value;
                    this.HandleSlideBoundsChanged(this, null);
                }
            }
        }

        public virtual SlideModel Slide {
            get { return this.m_Slide; }
            set {
                if(this.m_Slide != null) {
                    // Note: this duplicates code in Dispose.
                    this.m_Slide.Changed["Bounds"].Remove(new PropertyEventHandler(this.HandleSlideBoundsChanged));
                    this.m_Slide.Changed["Zoom"].Remove(new PropertyEventHandler(this.HandleSlideBoundsChanged));
                    this.m_Slide.Changed["BackgroundColor"].Remove(new PropertyEventHandler(this.HandleSlideBoundsChanged));
                    this.m_Slide.Changed["BackgroundTemplate"].Remove(new PropertyEventHandler(this.HandleSlideBoundsChanged));
                }

                this.m_Slide = value;

                Debug.Assert(!this.InvokeRequired);

                using(Synchronizer.Lock(this.m_SlideDisplay.SyncRoot)) {
                    this.m_SlideDisplay.Slide = value;
                }

                if(this.m_Slide != null) {
                    this.m_Slide.Changed["Bounds"].Add(new PropertyEventHandler(this.HandleSlideBoundsChanged));
                    this.m_Slide.Changed["Zoom"].Add(new PropertyEventHandler(this.HandleSlideBoundsChanged));
                    this.m_Slide.Changed["BackgroundColor"].Add(new PropertyEventHandler(this.HandleSlideBoundsChanged));
                    this.m_Slide.Changed["BackgroundTemplate"].Add(new PropertyEventHandler(this.HandleSlideBoundsChanged));
                }

                this.HandleSlideBoundsChanged(this.m_Slide, null);
            }
        }

        public override Size MinimumSize {
            get { return this.m_MinimumSize; }

            set {
                this.m_MinimumSize = value;
                if(this.Width < value.Width || this.Height < value.Height) {
                    // See also: OnSizeChanged event handler.
                    this.SuspendLayout();
                    this.Width = value.Width;
                    this.Height = value.Height;
                    this.ResumeLayout();
                }
            }
        }

        public SlideDisplayModel SlideDisplay {
            get { return this.m_SlideDisplay; }
        }

        protected override void OnPaint(PaintEventArgs args) {
            if (this.m_Slide == null) return;
            RoleModel role = null; 
            LinkedDeckTraversalModel.NavigationSelector studentNavigationType=LinkedDeckTraversalModel.NavigationSelector.Full;
            if (PresenterModel.TheInstance != null) {
                using (Synchronizer.Lock(PresenterModel.TheInstance.Participant.SyncRoot)) {
                    role = PresenterModel.TheInstance.Participant.Role;
                }

                using (Synchronizer.Lock(PresenterModel.TheInstance.ViewerState.SyncRoot)) {
                    studentNavigationType = PresenterModel.TheInstance.ViewerState.StudentNavigationType;
                }
            }
            // For student role, paint visited slides only.
            using (Synchronizer.Lock(this.m_Slide.SyncRoot)) {            
                if (role!=null && role is StudentModel && (!this.m_Slide.Visited) && studentNavigationType == LinkedDeckTraversalModel.NavigationSelector.Visited) return;
            }
            
            // Slide content is painted first, on the bottom.
            this.m_ContentSlideRenderer.Paint(args);

            // Annotations are layered on top of the content.
            this.m_AnnotationsSlideRenderer.Paint(args);

            // Tell everybody else who's listening to paint (DynamicRenderers, etc.).
            base.OnPaint(args);
        }

        protected override void OnSizeChanged(EventArgs e) {
            // TODO: If the SlideViewer is anchored or docked to the parent control, prevent the parent from becoming too small.

            this.SuspendLayout();

            // See also: MinimumSize setter.
            if(this.Width < this.MinimumSize.Width)
                this.Width = this.MinimumSize.Width;

            if(this.Height < this.MinimumSize.Height)
                this.Height = this.MinimumSize.Height;
            this.ResumeLayout();
            
            base.OnSizeChanged(e);
            

        }

        private void HandleDisplayInvalidated(object sender, InvalidateEventArgs e) {
            if(this.InvokeRequired) {
                this.BeginInvoke(new InvalidateEventHandler(this.HandleDisplayInvalidated),
                    new object[] { sender, e });
                return;
            }

            // TODO: Repaint only the necessary layer.
            // Note: Invalidate() already queues the event, so there's no need to start with a clean stack.
            this.Invalidate(e.InvalidRect);
        }

        /// <summary>
        /// Called when the slide's Bounds or Zoom properties change.
        /// </summary>
        private void HandleSlideBoundsChanged(object sender, PropertyEventArgs args) {
            this.m_EventQueue.Post(new EventQueue.EventDelegate(this.LayoutSlide));
        }

        /// <summary>
        /// Subclasses must implement this method to recalculate the <see cref="Bounds"/>,
        /// <see cref="PixelTransform"/>, and <see cref="InkTransform"/> of the <see cref="SlideDisplay"/>.
        /// <seealso cref="SlideDisplayModel.FitSlideToBounds"/>
        /// </summary>
        /// <remarks>
        /// This method is called whenever the slide's <see cref="SlideModel.Bounds"/> or <see cref="SlideModel.Zoom"/>
        /// properties change, or any other time the slide viewer needs to be laid out.
        /// </remarks>
        protected abstract void LayoutSlide();
    }
}
