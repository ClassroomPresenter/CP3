// $Id: MultiColumnFilmStrip.cs 793 2005-09-26 18:17:38Z shoat $

using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Windows.Forms;

using System.Runtime.InteropServices;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Viewer.Slides;

namespace UW.ClassroomPresenter.Viewer.FilmStrips {
    /// <summary>
    /// For each deck that we have open, we have a unique film strip control that displays the deck
    /// </summary>
    public class MultiColumnFilmStrip : Panel {
        private readonly ControlEventQueue m_EventQueue;

        private readonly EventQueue.PropertyEventDispatcher m_FilmStripWidthListener;
        private readonly EventQueue.PropertyEventDispatcher m_SSFilmStripWidthListener;

        private readonly PresenterModel m_Model;
        private readonly DeckTraversalModel m_DeckTraversal;
        private readonly DeckTraversalModel m_PreviewDeckTraversal;
        private readonly EntriesCollectionHelper m_EntriesCollectionHelper;
        private DeckStrip.DeckTabPage m_DeckTabPage;
        private DeckStrip m_DeckStrip;
        private bool m_Disposed;

        // Autoscroll cursors
        private Cursor m_AutoScrollUpCursor = Cursors.PanNorth;
        private Cursor m_AutoScrollDownCursor = Cursors.PanSouth;

        // Autoscroll and Preview Timers
        private System.Windows.Forms.Timer m_AutoScrollDownTimer;
        private System.Windows.Forms.Timer m_AutoScrollUpTimer;
        private System.Windows.Forms.Timer m_SlideLeaveTimer;
        private System.Timers.Timer m_LongPauseTimer;

        // Other Autoscroll and Preview Variables
        private DateTime m_MouseDownTime;       // Capture the time when the mouse was last pressed
        private Rectangle m_VirtualSize;        // The bounds containing all the child controls of this one
        private int m_MaxScrollAmount = 25;     // The maximum scrolling amount (SHOULD BE A PARAMETER)
        private int m_ScrollAmount = 0;         // The current scrolling amount

        // Determines the number of columns, at least 1.

        private int m_NumColumns;

        /// <summary>
        /// Constructs a new Filmstrip
        /// </summary>
        /// <param name="traversal"></param>
        /// <param name="model"></param>
        public MultiColumnFilmStrip(DeckTraversalModel traversal, PresenterModel model) {
            this.m_Model = model;
            this.m_DeckTraversal = traversal;

            this.m_EventQueue = new ControlEventQueue(this);

            // Create a new deck traversal to handle to keep track of the current slide that the mouse is over
            this.m_PreviewDeckTraversal = new SlideDeckTraversalModel(Guid.NewGuid(), traversal.Deck);

            this.AutoScroll = true;

            // Make the default width a little wider.
            this.Width = this.Width * 3 / 2;

            // Make the default back color a little prettier.
            this.BackColor = SystemColors.Window;

            // Set a sunken border around the FilmStrip and its scroll bar(s).
            this.BorderStyle = BorderStyle.Fixed3D;

            this.m_EntriesCollectionHelper = new EntriesCollectionHelper(this);

            this.ContextMenu = new FilmStripContextMenu(traversal, model);

            // AutoScroll and SlidePreview Timers Init
            this.m_SlideLeaveTimer = new System.Windows.Forms.Timer();
            this.m_SlideLeaveTimer.Tick += new EventHandler(HandleSlideLeaveTimerTick);
            this.m_SlideLeaveTimer.Interval = 100;

            /// This timer gets stopped and started a lot when the user hovers the mouse over the filmstrip.
            /// All the Stop/Start cycles incur a lot of overhead when running under Vista.  The "fix" is to 
            /// use System.Timers.Timer which does not incur the overhead.
            this.m_LongPauseTimer = new System.Timers.Timer();
            this.m_LongPauseTimer.Elapsed += new System.Timers.ElapsedEventHandler(HandleLongPause);
            this.m_LongPauseTimer.Interval = 5000;
            this.m_LongPauseTimer.SynchronizingObject = this;

            this.m_AutoScrollDownTimer = new System.Windows.Forms.Timer();
            this.m_AutoScrollDownTimer.Tick += new EventHandler(HandleAutoScrollDown);
            this.m_AutoScrollDownTimer.Interval = 100;

            this.m_AutoScrollUpTimer = new System.Windows.Forms.Timer();
            this.m_AutoScrollUpTimer.Tick += new EventHandler(HandleAutoScrollUp);
            this.m_AutoScrollUpTimer.Interval = 100;

          //Attach property listeners to the filmstrip's width
            this.m_FilmStripWidthListener = new EventQueue.PropertyEventDispatcher(this.m_EventQueue,
                new PropertyEventHandler(this.OnFilmStripWidthChanged));
            this.m_Model.ViewerState.Changed["FilmStripWidth"].Add(this.m_FilmStripWidthListener.Dispatcher);
            this.m_FilmStripWidthListener.Dispatcher(this, null);

            //Attach property listeners to the Student Submission filmstrip's width
            this.m_SSFilmStripWidthListener = new EventQueue.PropertyEventDispatcher(this.m_EventQueue,
                new PropertyEventHandler(this.OnSSFilmStripWidthChanged));
            this.m_Model.ViewerState.Changed["SSFilmStripWidth"].Add(this.m_SSFilmStripWidthListener.Dispatcher);
            this.m_SSFilmStripWidthListener.Dispatcher(this, null);

            // Set the number of columns based on deck type.
            // Student Submission decks have more columns so the instructor
            // can more easily scroll through dozens of submissions.
             if ((traversal.Deck.Disposition & DeckDisposition.StudentSubmission) != 0)    
                 this.m_NumColumns = 3;
             else this.m_NumColumns = 1;

 

                     }

 

        protected override void Dispose(bool disposing) {
            if (this.m_Disposed) return;
            try {
                if (disposing) {
                    this.m_EntriesCollectionHelper.Dispose();
                    this.Parent = null;

                    this.m_Model.ViewerState.Changed["FilmStripWidth"].Remove(this.m_FilmStripWidthListener.Dispatcher);
                    this.m_Model.ViewerState.Changed["SSFilmStripWidth"].Remove(this.m_SSFilmStripWidthListener.Dispatcher);

                    this.m_EventQueue.Dispose();
                }
            } finally {
                base.Dispose(disposing);
            }
            this.m_Disposed = true;
        }

        #region Autoscroll UI
        /// <summary>
        /// This event occurs when there is a change to the visibility of the scrollbars of the filmstrip
        /// </summary>
        //        public event AutoScrollEventHandler AutoScrollChanged;

        #endregion

        #region Dock

        /// <summary>
        /// Event handler for the layout of this control changing, need to
        /// check if the auto-scrollbars appeared or not.
        /// </summary>
        /// <param name="levent">The LayoutEventArgs</param>
        protected override void OnLayout(LayoutEventArgs levent) {
            this.UpdateControlPositions();
            UpdateVirtualSize();
            base.OnLayout(levent);
        }

        /// <summary>
        /// Event handler for when child controls are added to this one.
        /// </summary>
        /// <param name="e">The event args</param>
        protected override void OnControlAdded(ControlEventArgs e) {
            this.UpdateControlPositions();
            UpdateVirtualSize();
            base.OnControlAdded(e);
        }

        protected void UpdateControlPositions() {
            this.SuspendLayout();
            if (this.m_DeckStrip != null) {
                // Base our dock on the DockStyle of the TabPage's parent, which is the DeckStrip.
                switch (this.m_DeckStrip.Dock) {
                    case DockStyle.Top:
                    case DockStyle.Bottom:
                        this.UpdateHorizontalControlPositions();
                        break;
                    default:
                        this.UpdateVerticalControlPositions();
                        break;
                }
            }
            this.ResumeLayout();
        }

        protected void UpdateHorizontalControlPositions() {
            int controlHeight = this.ClientRectangle.Height;
            int columnHeight = (controlHeight) / this.m_NumColumns;
            int firstColumnExtra = (controlHeight) - (this.m_NumColumns * columnHeight);
            int xPos = 0;
            int yPos = 0;
            int maxColWidth = 0;

            for (int i = this.Controls.Count - 1; i >= 0; i--) {
                FilmStripSlideViewer viewer = (FilmStripSlideViewer)this.Controls[i];
                int height = columnHeight;

                // Update the position and size of the control
                if (viewer.Floating != FilmStripSlideViewer.FloatingDimension.Width)
                    viewer.Floating = FilmStripSlideViewer.FloatingDimension.Width;
                if (viewer.Height != height)
                    viewer.Height = height;
                if (viewer.Top != yPos)
                    viewer.Top = yPos;
                if (viewer.Left != xPos)
                    viewer.Left = xPos;

                // Get the highest control in this row
                if (viewer.Width > maxColWidth)
                    maxColWidth = viewer.Width;

                // Update the cursor position
                yPos += height;
                if (yPos >= (controlHeight - firstColumnExtra)) {
                    yPos = 0;
                    xPos += maxColWidth;
                    maxColWidth = 0;
                }
            }
        }

        protected void UpdateVerticalControlPositions() {
            int controlWidth = this.ClientRectangle.Width;
            int columnWidth = (controlWidth) / this.m_NumColumns;
            int firstColumnExtra = (controlWidth) - (this.m_NumColumns * columnWidth);
            int xPos = 0;
            int yPos = this.DisplayRectangle.Top;
            int maxRowHeight = 0;

            for (int i = this.Controls.Count - 1; i >= 0; i--) {
                FilmStripSlideViewer viewer = (FilmStripSlideViewer)this.Controls[i];
                int width = columnWidth;

                // Update the position and size of the control
                if (viewer.Floating != FilmStripSlideViewer.FloatingDimension.Height)
                    viewer.Floating = FilmStripSlideViewer.FloatingDimension.Height;
                if (viewer.Width != width)
                    viewer.Width = width;
                if (viewer.Top != yPos)
                    viewer.Top = yPos;
                if (viewer.Left != xPos)
                    viewer.Left = xPos;

                // Get the highest control in this row
                if (viewer.Height > maxRowHeight)
                    maxRowHeight = viewer.Height;

                // Update the cursor position
                xPos += width;
                if (xPos >= (controlWidth - firstColumnExtra)) {
                    xPos = 0;
                    yPos += maxRowHeight;
                    maxRowHeight = 0;
                }
            }
        }

        // Recalculate the virtual control size on resize
        protected override void OnSizeChanged(EventArgs e) {
            this.UpdateControlPositions();
            UpdateVirtualSize();
            base.OnSizeChanged(e);
        }

       //Change the width (in slides) of the filmstrip, ie update NumColumns, if NOT Student Submission deck
        protected void OnFilmStripWidthChanged(object sender, PropertyEventArgs args)
        {
            using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot))
            {

                if ((this.m_DeckTraversal.Deck.Disposition & DeckDisposition.StudentSubmission) == 0)
                {
                    this.m_NumColumns = this.m_Model.ViewerState.FilmStripWidth;
                    this.UpdateControlPositions();
                }//this.Invalidate();
            }
        }

        //Change the width (in slides) of the filmstrip, ie update NumColumns, if Student Submission deck
        protected void OnSSFilmStripWidthChanged(object sender, PropertyEventArgs args)
        {
            using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot))
            {
                if ((this.m_DeckTraversal.Deck.Disposition & DeckDisposition.StudentSubmission) != 0)
                {
                    this.m_NumColumns = this.m_Model.ViewerState.SSFilmStripWidth;
                    this.UpdateControlPositions();
                }//this.Invalidate();
            }
        }
        // Calculate the size of the virtual panel
        // That is, find the largest bounding box for the controls
        public void UpdateVirtualSize() {
            this.m_VirtualSize = this.Bounds;
            foreach (Control c in this.Controls) {
                if (c.Top < this.m_VirtualSize.Top)
                    this.m_VirtualSize.Y = c.Top;
                if (c.Left < this.m_VirtualSize.Left)
                    this.m_VirtualSize.X = c.Left;
                if (c.Bottom > this.m_VirtualSize.Bottom)
                    this.m_VirtualSize.Height = c.Bottom - this.m_VirtualSize.Y;
                if (c.Right > this.m_VirtualSize.Right)
                    this.m_VirtualSize.Width = c.Right - this.m_VirtualSize.X;
            }
        }

        protected override void OnParentChanged(EventArgs e) {
            base.OnParentChanged(e);
            Debug.Assert(!this.InvokeRequired);

            if (this.m_DeckTabPage != null)
                this.m_DeckTabPage.ParentChanged -= new EventHandler(this.HandleDeckStripChanged);
            this.m_DeckTabPage = this.Parent as DeckStrip.DeckTabPage;
            if (this.m_DeckTabPage != null)
                this.m_DeckTabPage.ParentChanged += new EventHandler(this.HandleDeckStripChanged);
            this.HandleDeckStripChanged(this, EventArgs.Empty);
        }

        private void HandleDeckStripChanged(object sender, EventArgs e) {
            Debug.Assert(!this.InvokeRequired);

            if (this.m_DeckStrip != null)
                this.m_DeckStrip.DockChanged -= new EventHandler(this.HandleDeckStripDockChanged);
            this.m_DeckStrip = (this.m_DeckTabPage != null) ? this.m_DeckTabPage.Parent as DeckStrip : null;
            if (this.m_DeckStrip != null)
                this.m_DeckStrip.DockChanged += new EventHandler(this.HandleDeckStripDockChanged);
            this.HandleDeckStripDockChanged(this, EventArgs.Empty);
        }

        private void HandleDeckStripDockChanged(object sender, EventArgs e) {
            Debug.Assert(!this.InvokeRequired);
            this.UpdateControlPositions();
            UpdateVirtualSize();
        }

        #endregion Dock

        #region AutoScroll/Slide Preview

        /// <summary>
        /// Stops the filmstrip from auto-scrolling.
        /// </summary>
        private void StopAutoScroll(bool changeCursor) {
            // Change the cursor back
            if (changeCursor)
                this.Cursor = Cursors.Default;

            // Stop the scrolling timers
            this.m_AutoScrollUpTimer.Stop();
            this.m_AutoScrollDownTimer.Stop();
        }

        /// <summary>
        /// Handles the event when the "slide leave" timer invokes a tick event,
        /// which means that the mouse is no longer on any of the slides.
        /// Therefore we stop the previewing of slides.
        /// </summary>
        protected void HandleSlideLeaveTimerTick(object sender, EventArgs args) {
            // Stop the timer
            this.m_SlideLeaveTimer.Stop();

            // We no longer want to display the slide preview control
            using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot)) {
                this.m_Model.ViewerState.SlidePreviewVisible = false;
            }

            // Stop autoscrolling since we've left the slide
            StopAutoScroll(false);
        }

        /// <summary>
        /// Helper function that auto-scrolls the filmstrip down when we are near the
        /// edge of the control
        /// </summary>
        protected virtual void HandleAutoScrollDown(object sender, System.EventArgs args) {
            switch (this.m_DeckStrip.Dock) {
                case DockStyle.Top:
                case DockStyle.Bottom:
                    // Check if we need to scroll and by how much
                    if (this.AutoScrollPosition.X + this.ClientSize.Width + this.m_ScrollAmount < this.m_VirtualSize.Width) {
                        this.AutoScrollPosition = new Point(-this.AutoScrollPosition.X + this.m_ScrollAmount,
                                                             this.AutoScrollPosition.Y);
                    }
                    break;
                default:
                    // Check if we need to scroll and by how much
                    if (this.AutoScrollPosition.Y + this.ClientSize.Height + this.m_ScrollAmount < this.m_VirtualSize.Height) {
                        this.AutoScrollPosition = new Point(this.AutoScrollPosition.X,
                                                             -this.AutoScrollPosition.Y + this.m_ScrollAmount);
                    }
                    break;
            }
        }

        /// <summary>
        /// Helper function that auto-scrolls the filmstrip up when we are near the
        /// edge of the control
        /// </summary>
        protected virtual void HandleAutoScrollUp(object sender, System.EventArgs args) {
            switch (this.m_DeckStrip.Dock) {
                case DockStyle.Top:
                case DockStyle.Bottom:
                    // Check if we need to scroll and by how much
                    if (this.AutoScrollPosition.X < 0) {
                        this.AutoScrollPosition = new Point(-this.AutoScrollPosition.X - this.m_ScrollAmount,
                                                             this.AutoScrollPosition.Y);
                    }
                    break;
                default:
                    // Check if we need to scroll and by how much
                    if (this.AutoScrollPosition.Y < 0) {
                        this.AutoScrollPosition = new Point(this.AutoScrollPosition.X,
                                                             -this.AutoScrollPosition.Y - this.m_ScrollAmount);
                    }
                    break;
            }
        }

        /// <summary>
        /// Handle the case where the cursor is in the same spot for a long time. In
        /// this case we want to make the slide preview window go away
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        protected virtual void HandleLongPause(object sender, System.EventArgs args) {
            // Stop the timer
            this.m_LongPauseTimer.Stop();

            // We no longer want to display the slide preview control
            using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot)) {
                this.m_Model.ViewerState.SlidePreviewVisible = false;
            }
        }

        /// <summary>
        /// Handle the event when the mouse in pressed down in this control or any of the
        /// child controls of this one.
        /// </summary>
        /// <param name="sender">The control in which the mouse was depressed</param>
        /// <param name="e">The mouse event parameters</param>
        private void HandleMouseDown(object sender, MouseEventArgs e) {
            // Need to record when we pressed the mouse on a slide
            this.m_MouseDownTime = System.DateTime.Now;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HandleMouseUp(object sender, MouseEventArgs e) {
            // Check if we should register as a click or not
            if (this.Enabled &&
                (System.DateTime.Now - this.m_MouseDownTime) < System.TimeSpan.FromMilliseconds(200)) {
                // It was a click so stop all scroll timers
                this.m_AutoScrollDownTimer.Stop();
                this.m_AutoScrollUpTimer.Stop();

                // Activate the correct slide
                if (sender is FilmStripSlideViewer) {
                    ((FilmStripSlideViewer)sender).Activate();
                }
            }

            // Mouse has been released so we no longer want to show the preview window
            this.m_LongPauseTimer.Stop();
            this.m_SlideLeaveTimer.Start();
        }

        /// <summary>
        /// Handles the event when the mouse enters a slide.
        /// </summary>
        protected virtual void HandleSlideEnter(object sender, EventArgs args) {
            // We've entered a slide, so stop the slide leave timer.
            this.m_SlideLeaveTimer.Stop();
        }

        /// <summary>
        /// Handles the event when the mouse leaves a slide.
        /// </summary>
        protected virtual void HandleSlideLeave(object sender, EventArgs args) {
            // if the mouse leaves the slide, then it must have moved,
            // so stop the long pause timer.
            this.m_LongPauseTimer.Stop();
            this.m_SlideLeaveTimer.Start();
        }

        /// <summary>
        /// Handles the event when the mouse is moving on a slide.
        /// </summary>
        protected virtual void HandleSlideMove(object sender, MouseEventArgs args) {
            // The mouse just moved so stop this timer
            this.m_LongPauseTimer.Stop();

            // Check if the mouse is in an area for auto-scrolling
            if (sender is FilmStripSlideViewer) {
                // Check if autoscrolling is enabled
                bool bAutoScroll = false;
                using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot)) {
                    bAutoScroll = this.m_Model.ViewerState.AutoScrollEnabled;
                }

                if (bAutoScroll) {
                    // Get the scroll threshhold and mouse position
                    int mouse;
                    int scrollThreshhold = 30;
                    int maxEdge;
                    switch (this.m_DeckStrip.Dock) {
                        // Horizontal Alignment
                        case DockStyle.Top:
                        case DockStyle.Bottom:
                            mouse = ((FilmStripSlideViewer)sender).Location.X + args.X;
                            maxEdge = this.ClientSize.Width;
                            scrollThreshhold = (int)(0.5 * ((FilmStripSlideViewer)sender).Height);
                            break;
                        // Vertical Alignment
                        default:
                            mouse = ((FilmStripSlideViewer)sender).Location.Y + args.Y;
                            maxEdge = this.ClientSize.Height;
                            scrollThreshhold = (int)(0.5 * ((FilmStripSlideViewer)sender).Width);
                            break;
                    }

                    // Check if the scroll threshold is within the mouse position
                    if (mouse - scrollThreshhold < 0) {
                        // Calculate the Speed
                        this.m_ScrollAmount = (int)(-((double)this.m_MaxScrollAmount / scrollThreshhold) * mouse) + this.m_MaxScrollAmount;
                        // Start the Scroll Timer
                        if (!this.m_AutoScrollUpTimer.Enabled) {
                            StopAutoScroll(false);
                            this.m_AutoScrollUpTimer.Start();
                        }
                        // Set the Cursor
                        if (this.VScroll || this.HScroll)
                            this.Cursor = this.m_AutoScrollUpCursor;
                    }
                        // else, if the cursor is near the bottom...
                    else if (mouse + scrollThreshhold > maxEdge) {
                        // Calculate the Speed
                        int distFromEdge = maxEdge - mouse;
                        this.m_ScrollAmount = (int)(-((double)this.m_MaxScrollAmount / scrollThreshhold) * distFromEdge) + this.m_MaxScrollAmount;
                        // Start the Scroll Timer
                        if (!this.m_AutoScrollDownTimer.Enabled) {
                            StopAutoScroll(false);
                            this.m_AutoScrollDownTimer.Start();
                        }
                        // Set the Cursor
                        if (this.VScroll || this.HScroll)
                            this.Cursor = this.m_AutoScrollDownCursor;
                    }
                        // otherwise, don't scroll at all.
                    else {
                        StopAutoScroll(true);
                    }
                } else {
                    //Set the cursor back to normal
                    this.Cursor = Cursors.Default;
                }
            }

            // Check if we need to display the slide preview because we are dragging on the slide
            if ((args.Button == System.Windows.Forms.MouseButtons.Left ||
                 args.Button == System.Windows.Forms.MouseButtons.Right) &&
                (System.DateTime.Now - this.m_MouseDownTime) >= System.TimeSpan.FromMilliseconds(200)) {
                // We are displaying the slide so turn off this timer
                this.m_SlideLeaveTimer.Stop();

                // Display the slide preview
                using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot)) {
                    this.m_Model.ViewerState.SlidePreviewVisible = true;
                }
            }

            // NOTE: Fixes a bug where the MouseUp event was not properly being handled.
            if (!(args.Button == System.Windows.Forms.MouseButtons.Left ||
                  args.Button == System.Windows.Forms.MouseButtons.Right)) {
                // Mouse has been released so we no longer want to show the preview window
                this.m_LongPauseTimer.Stop();
                this.m_SlideLeaveTimer.Start();
            }

            // Start the timer back up
            this.m_LongPauseTimer.Start();
        }

        /// <summary>
        /// Turn off all the timers if this control becomes invisible
        /// </summary>
        /// <param name="e"></param>
        protected override void OnVisibleChanged(EventArgs e) {
            base.OnVisibleChanged(e);

            if (!this.Visible) {
                this.m_SlideLeaveTimer.Stop();
                this.m_LongPauseTimer.Stop();
                this.m_AutoScrollDownTimer.Stop();
                this.m_AutoScrollUpTimer.Stop();
            }
        }

        #endregion

        #region EntriesCollectionHelper

        private sealed class EntriesCollectionHelper : PropertyCollectionHelper {
            private readonly MultiColumnFilmStrip m_Owner;

            public EntriesCollectionHelper(MultiColumnFilmStrip owner)
                : base(owner.m_EventQueue, owner.m_DeckTraversal.Deck.TableOfContents, "Entries") {
                this.m_Owner = owner;

                base.Initialize();
            }

            protected override void Dispose(bool disposing) {
                if (disposing)
                    foreach (object o in this.Tags)
                        this.TearDownMember(-1, null, o);
                base.Dispose(disposing);
            }

            protected override object SetUpMember(int index, object member) {
                TableOfContentsModel.Entry entry = ((TableOfContentsModel.Entry)member);
                FilmStripSlideViewer viewer;

                using (Synchronizer.Lock(entry.SyncRoot)) {
                    // Only deal with entries that are direct children of the TableOfContentsModel.
                    // Deeper entries are dealt with by the FilmStripSlideViewers.
                    if (entry.Parent != null)
                        return null;
                }

                Debug.Assert(this.m_Owner.IsHandleCreated);
                Debug.Assert(!this.m_Owner.InvokeRequired);

                this.m_Owner.SuspendLayout();
                viewer = new FilmStripSlideViewer(
                    this.m_Owner, this.m_Owner.m_Model, this.m_Owner.m_DeckTraversal,
                    this.m_Owner.m_PreviewDeckTraversal, entry);

                viewer.MyMouseDown += new MouseEventHandler(this.m_Owner.HandleMouseDown);
                viewer.MouseUp += new MouseEventHandler(this.m_Owner.HandleMouseUp);
                viewer.MouseEnter += new EventHandler(this.m_Owner.HandleSlideEnter);
                viewer.MouseLeave += new EventHandler(this.m_Owner.HandleSlideLeave);
                viewer.MouseMove += new MouseEventHandler(this.m_Owner.HandleSlideMove);
                try {


                    // Set the entry's Z-order.  The control at index 0 will be docked farthest
                    // away from the edge, so the "last" entry should be at index 0.
                    // Unfortunately we don't have a guarantee that all of the TableOfContentsModel's
                    // entries have yet been added to the FilmStrip (though that should be the case),
                    // so we just go through (starting with z-order 0, the last thing in the filmstrip)
                    // until we find the first entry whose index is less than ours.

#if DEBUG
                    Debug.Assert(index >= 0);
                    using (Synchronizer.Lock(entry.TableOfContents.SyncRoot)) {
                        Debug.Assert(entry.TableOfContents.Entries.Count > index);
                    }
#endif

                    int zOrder = 0;
                    using (Synchronizer.Lock(entry.SyncRoot)) {
                        int i;
                        for (i = 0; i < this.m_Owner.Controls.Count; i++) {
                            TableOfContentsModel.Entry en = ((FilmStripSlideViewer)(this.m_Owner.Controls[i])).m_Entry;
                            if (entry.TableOfContents.Entries.IndexOf(en) < entry.TableOfContents.Entries.IndexOf(entry)) {
                                zOrder = Math.Max(0, i);
                                i = this.m_Owner.Controls.Count + 1;
                            }
                        }
                        if (i == this.m_Owner.Controls.Count) {
                            zOrder = i;
                        }
                    }
                    this.m_Owner.Controls.Add(viewer);
                    this.m_Owner.Controls.SetChildIndex(viewer, zOrder);
                }
                catch {
                    viewer.MyMouseDown -= new MouseEventHandler(this.m_Owner.HandleMouseDown);
                    viewer.MouseUp -= new MouseEventHandler(this.m_Owner.HandleMouseUp);
                    viewer.MouseEnter -= new EventHandler(this.m_Owner.HandleSlideEnter);
                    viewer.MouseLeave -= new EventHandler(this.m_Owner.HandleSlideLeave);
                    viewer.MouseMove -= new MouseEventHandler(this.m_Owner.HandleSlideMove);

                    this.m_Owner.Controls.Remove(viewer);
                    viewer.Dispose();
                    throw;
                }

                this.m_Owner.ResumeLayout();

                RoleSynchronizer rs = new RoleSynchronizer(viewer, this.m_Owner.m_Model.Participant);
                return rs;
            }

            protected override void TearDownMember(int index, object member, object tag) {
                RoleSynchronizer sw = ((RoleSynchronizer)tag);
                if (tag != null) {
                    this.m_Owner.SuspendLayout();
                    this.m_Owner.Controls.Remove(sw.SlideViewer);
                    ((FilmStripSlideViewer)(sw.SlideViewer)).MyMouseDown -= new MouseEventHandler(this.m_Owner.HandleMouseDown);
                    sw.SlideViewer.MouseUp -= new MouseEventHandler(this.m_Owner.HandleMouseUp);
                    sw.SlideViewer.MouseEnter -= new EventHandler(this.m_Owner.HandleSlideEnter);
                    sw.SlideViewer.MouseLeave -= new EventHandler(this.m_Owner.HandleSlideLeave);
                    sw.SlideViewer.MouseMove -= new MouseEventHandler(this.m_Owner.HandleSlideMove);
                    sw.SlideViewer.Dispose();
                    sw.Dispose();
                    this.m_Owner.ResumeLayout();
                }
            }
        }

        #endregion EntriesCollectionHelper
    }

    /// <summary>
    /// Defines the event handler for the AutoScrollChanged event
    /// </summary>
    public delegate void AutoScrollEventHandler(object sender, AutoScrollEventArgs e);

    /// <summary>
    /// Defines the parameters for the AutoScrollChanged event
    /// </summary>
    public class AutoScrollEventArgs : EventArgs {
        private readonly bool m_appeared;
        /// <summary>
        /// Did the scrollbar appear or disappear
        /// </summary>
        public bool Appeared {
            get { return this.m_appeared; }
        }
        /// <summary>
        /// Construct the AutoScrollEventArgs class
        /// </summary>
        /// <param name="appeared">Value for the parameter</param>
        public AutoScrollEventArgs(bool appeared) {
            this.m_appeared = appeared;
        }
    }
}
