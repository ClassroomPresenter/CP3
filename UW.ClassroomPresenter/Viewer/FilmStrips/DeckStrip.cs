// $Id: DeckStrip.cs 1778 2008-09-24 22:50:50Z lining $

using System;
using System.Collections;
using System.Drawing;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Workspace;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Model.Network;

using Menus = UW.ClassroomPresenter.Viewer.Menus;

namespace UW.ClassroomPresenter.Viewer.FilmStrips {

    public class DeckStrip : TabControl {
        private readonly PresenterModel m_Model;

        private readonly ControlEventQueue m_EventQueue;
        private readonly IDisposable m_DeckTraversalChangedDispatcher;
        private readonly EventQueue.PropertyEventDispatcher m_FilmStripAlignmentListener;
        private readonly EventQueue.PropertyEventDispatcher m_FilmStripEnabledListener;

        /// <summary>
        /// A reverse mapping of <see cref="DeckTraversalModel"/>s to <see cref="DeckTabPage"/>s,
        /// used by <see cref="HandleCurrentDeckTraversalChanged"/>.
        /// </summary>
        private readonly Hashtable m_DeckTraversalToTabPageMap;

        private DeckTraversalsMirror m_DeckTraversalCollectionHelper;

        // The next two fields make it possible to change the dock without screwing up the width/height of the control.
        // Used by OnSizeChanged and OnDockChanged.
        private Size m_DockSize;
        private DockStyle m_DockStyle;

        private bool m_Disposed;


        #region Construction & Destruction

        public DeckStrip(PresenterModel model) {
            this.m_Model = model;
            this.m_EventQueue = new ControlEventQueue(this);

            this.m_DeckTraversalToTabPageMap = new Hashtable();

            this.Name = "DeckStrip";

            // The TabControl has no built-in support for scrolling when there are too many tabs to fit,
            // so making it Multiline is the easiest fix.  However, Multiline tabs are kind of clunky, so...
            // TODO: Consider implementing some sort of tab scrolling instead of using Multiline tabs.
            this.Multiline = true;

            // Make the tabs as big as possible, to be easy targets for a stylus.
            this.SizeMode = TabSizeMode.FillToRight;

            // Handle then drawing of tabs ourselves
            this.DrawMode = TabDrawMode.OwnerDrawFixed;

            // Create the control's handle immediately, or else the event queue will never process any events
            // and the control will never be made visible... chicken and the egg.
            this.CreateHandle();

            this.m_DeckTraversalCollectionHelper = new DeckTraversalsMirror(this, this.m_Model.Workspace.DeckTraversals);

            this.m_DeckTraversalChangedDispatcher =
                this.m_Model.Workspace.CurrentDeckTraversal.ListenAndInitialize(this.m_EventQueue,
                delegate(Property<DeckTraversalModel>.EventArgs args) {
                    if (args.New != null) {
                        this.SelectedTab = ((DeckTabPage)this.m_DeckTraversalToTabPageMap[args.New]);
                    }
                });

            ///to keep track of the current presentation.
            this.SelectedIndexChanged += new EventHandler(DeckStrip_SelectedIndexChanged);
            this.ControlAdded += new ControlEventHandler(DeckStrip_ControlAdded);

            //Attach property listeners to the DeckStrip's enabledness
            this.m_FilmStripEnabledListener = new EventQueue.PropertyEventDispatcher(this.m_EventQueue,
                new PropertyEventHandler(this.OnFilmStripEnabledChanged));
            this.m_Model.ViewerState.Changed["FilmStripEnabled"].Add(this.m_FilmStripEnabledListener.Dispatcher);
            this.m_FilmStripEnabledListener.Dispatcher(this, null);

            //Attatch property listeners to the DeckStrip's alignment
            this.m_FilmStripAlignmentListener = new EventQueue.PropertyEventDispatcher(this.m_EventQueue,
                new PropertyEventHandler(this.OnFilmStripAlignmentChanged));
            this.m_Model.ViewerState.Changed["FilmStripAlignment"].Add(this.m_FilmStripAlignmentListener.Dispatcher);
            this.m_FilmStripAlignmentListener.Dispatcher(this, null);
        }

        /// <summary>
        /// When we add our first deck, we need to update this in our presentation.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void DeckStrip_ControlAdded(object sender, ControlEventArgs e) {
            ///only change the presentation if this is our first deck to be added.
            if (this.Controls.Count == 1) {
                DeckStrip_SelectedIndexChanged(sender, (EventArgs)e);
            }
        }

        /// <summary>
        /// When we change our deck, we also want to change the title of our presentation
        /// since the presentation title is the same 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void DeckStrip_SelectedIndexChanged(object sender, EventArgs e) {
            DeckTabPage page = (DeckTabPage)this.SelectedTab;
            ///for when we're closing
            if (page == null) { return; }
            using (this.m_Model.Workspace.Lock()) {
                PresentationModel current = ~this.m_Model.Workspace.CurrentPresentation;
                if (current != null)
                    using (Synchronizer.Lock(current.SyncRoot))
                        current.HumanName = page.Text;
            }
        }



        protected override void Dispose(bool disposing) {
            if (this.m_Disposed) return;
            try {
                if (disposing) {
                    this.m_Model.ViewerState.Changed["FilmStripEnabled"].Remove(this.m_FilmStripEnabledListener.Dispatcher);
                    this.m_Model.ViewerState.Changed["FilmStripAlignment"].Remove(this.m_FilmStripAlignmentListener.Dispatcher); 
                    this.m_DeckTraversalChangedDispatcher.Dispose();

                    this.m_DeckTraversalCollectionHelper.Dispose();
                    this.m_DeckTraversalToTabPageMap.Clear();
                    this.m_EventQueue.Dispose();
                }
            } finally {
                base.Dispose(disposing);
            }
            this.m_Disposed = true;
        }

        #endregion Construction & Destruction

        #region Custom Drawing
        protected override void OnDrawItem(DrawItemEventArgs e) {
            Graphics g = e.Graphics;
            Brush textBrush, backBrush;

            // Get the item from the collection
            DeckTabPage tab = (DeckTabPage)this.TabPages[e.Index];

            ///Draw the background by accesing the our tabpage's
            ///TabBackColor property
            backBrush = new SolidBrush(tab.TabBackColor);
            g.FillRectangle(backBrush, e.Bounds);
            backBrush.Dispose();

            // Get the bounds for the tab rectangle
            Rectangle bounds = this.GetTabRect(e.Index);

            // Create a brush for drawing the text
            textBrush = new SolidBrush(SystemColors.MenuText);

            // Draw string, centering the text
            StringFormat flags = new StringFormat();
            flags.Alignment = StringAlignment.Center;
            flags.LineAlignment = StringAlignment.Center;

            // Adjust the alignment so that the text is drawn vertically
            if (this.Alignment == TabAlignment.Left ||
                this.Alignment == TabAlignment.Right)
                g.RotateTransform(-90, System.Drawing.Drawing2D.MatrixOrder.Append);

            // Center the text in the tab control
            g.TranslateTransform(bounds.X + (bounds.Width / 2.0f),
                                  bounds.Y + (bounds.Height / 2.0f),
                                  System.Drawing.Drawing2D.MatrixOrder.Append);

            // Draw the control string
            g.DrawString(tab.Text, e.Font, textBrush, new PointF(0, 0), new StringFormat(flags));

            // Cleanup
            textBrush.Dispose();
        }

        #endregion

        #region Auto-Scroll UI Handling

        /// <summary>
        /// Need to capture the event when a child tab-page changes whether it displays the auto-scroll bar
        /// or not. In this case we change our own size in order to resize all chil controls and prevent
        /// The scroll-bar from appearing and disappearing.
        ///
        /// NOTE: Part of the fix for BUG 678.
        /// </summary>
        /// <param name="sender">The TabPage that had a change in it's auto-scroll</param>
        /// <param name="e">Value whether the scroll-bar appeared or disappeared</param>
        public bool m_AlreadyChangingSize = false;
        protected void OnAutoScrollChanged(object sender, AutoScrollEventArgs e) {
            if (!this.m_AlreadyChangingSize && ((Control)sender).Visible) {
                try {
                    this.m_AlreadyChangingSize = true;

                    if (e.Appeared)
                        this.BeginInvoke(new MethodInvoker(this.AutoScrollIncreaseSize));
                    else
                        this.BeginInvoke(new MethodInvoker(this.AutoScrollReduceSize));
                } finally {
                    this.m_AlreadyChangingSize = false;
                }
            }
        }
        /// <summary>
        /// Reduces the size of the DeckStrip by the width/height of the scrollbar
        ///
        /// NOTE: Part of the fix for BUG 678.
        /// </summary>
        protected void AutoScrollReduceSize() {
            if (this.Dock == DockStyle.Left || this.Dock == DockStyle.Right)
                this.Width -= new VScrollBar().Width;
            else
                this.Height -= new HScrollBar().Height;
        }
        /// <summary>
        /// Increases the size of the DeckStrip by the width/height of the scrollbar
        ///
        /// NOTE: Part of the fix for BUG 678.
        /// </summary>
        protected void AutoScrollIncreaseSize() {
            if (this.Dock == DockStyle.Left || this.Dock == DockStyle.Right)
                this.Width += new VScrollBar().Width;
            else
                this.Height += new HScrollBar().Height;
        }

        #endregion

        #region Enabled Event Listener

        private void OnFilmStripEnabledChanged(object sender, PropertyEventArgs args)
        {
            PropertyChangeEventArgs pcea = args as PropertyChangeEventArgs;
            if (args != null)
            {
                this.Visible = (bool)pcea.NewValue;
            }
        }

        private void OnFilmStripAlignmentChanged(object sender, PropertyEventArgs args) {
            using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot))
                this.Dock = this.m_Model.ViewerState.FilmStripAlignment;
        }

        #endregion


        #region Dock

        protected override void OnDockChanged(EventArgs e) {
            base.OnDockChanged(e);

            // If we're docked and there are no tabs, hide the control.
            this.UpdateVisibility();

            // This does two things:
            // (1) Keeps the tabs on the outside edge of the screen.
            // (2) When the dock is changed perpendicularly, resets the "resizable" dimension to its previous value.
            // See OnSizeChanged for more comments.
            // Note that when the Dock is changed, OnSizeChange gets called *before* OnDockChanged (a quirk of Windows Forms)!
            switch (this.Dock) {
                case DockStyle.Left:
                    this.Alignment = TabAlignment.Left;
                    if (this.m_DockSize != Size.Empty) this.Width = this.m_DockSize.Width;
                    break;
                case DockStyle.Right:
                    this.Alignment = TabAlignment.Right;
                    if (this.m_DockSize != Size.Empty) this.Width = this.m_DockSize.Width;
                    break;
                case DockStyle.Top:
                    this.Alignment = TabAlignment.Top;
                    if (this.m_DockSize != Size.Empty) this.Height = this.m_DockSize.Height;
                    break;
                case DockStyle.Bottom:
                    this.Alignment = TabAlignment.Bottom;
                    if (this.m_DockSize != Size.Empty) this.Height = this.m_DockSize.Height;
                    break;
                default:
                    this.Alignment = TabAlignment.Top;
                    break;
            }

            // Only update m_DockStyle at the end, so OnSizeChange can know when the dock is in the process of changing.
            this.m_DockStyle = this.Dock;
        }

        protected override void OnSizeChanged(EventArgs e) {
            // Note that when the Dock is changed, OnSizeChange gets called *before* OnDockChanged (a quirk of Windows Forms)!
            base.OnSizeChanged(e);

            // When the DeckStrip is docked, one of its dimensions is "resizable" via a LinkedSplitter.
            // We want to make sure that if the dock is changed perpendicularly, the resizable dimension
            // gets the same width/height as its previous value (ie., the width or height from last time
            // the DeckStrip was docked along the same edge).  (More practically, this keeps the filmstrip
            // from blowing up to the size of the whole screen when the dock is changed.)
            // So, keep track of the "current" width/height, **but only if the dock is not in the process of changing**!
            // If the dock is currently changing, m_DockStyle won't have been updated by OnDockChanged yet.
            if (this.Dock == this.m_DockStyle) {
                // Note: When the control is initialized, this.Dock == DockStyle.None,
                // so m_DockSize will be initialized on both dimensions.  Thus the first time the control
                // is docked to an edge, the resizable dimension will be set to the
                // default size of the control in OnDockChanged.
                switch (this.Dock) {
                    case DockStyle.Left:
                    case DockStyle.Right:
                        this.m_DockSize.Width = this.Width;
                        break;
                    case DockStyle.Top:
                    case DockStyle.Bottom:
                        this.m_DockSize.Height = this.Height;
                        break;
                    case DockStyle.None:
                        this.m_DockSize = this.Size;
                        break;
                    case DockStyle.Fill:
                        // If DockStyle.Fill, then both dimensions are meaningless!  Ignored.
                        break;
                }
            }
        }

        protected override void OnControlAdded(ControlEventArgs e) {
            base.OnControlAdded(e);
            this.UpdateVisibility();
        }

        protected override void OnControlRemoved(ControlEventArgs e) {
            this.UpdateVisibility();
            base.OnControlRemoved(e);
        }

        private void UpdateVisibility() {
            if (this.Dock != DockStyle.None && this.Dock != DockStyle.Fill)
                this.Visible = (this.TabPages.Count > 0);
        }

        #endregion Dock


        #region DeckTraversalsMirror

        /// <summary>
        /// Creates, registers, and destroys <see cref="DeckTabPage"/>s
        /// whenever decks are added to or removed from the <see cref="WorkspaceModel"/>.
        /// </summary>
        private class DeckTraversalsMirror : CollectionMirror<DeckTraversalModel, DeckTabPage> {
            private readonly DeckStrip m_Owner;

            public DeckTraversalsMirror(DeckStrip owner, Collection<DeckTraversalModel> collection)
                : base(owner.m_EventQueue, collection) {
                this.m_Owner = owner;
                base.Initialize(true);
            }

            protected override void Dispose(bool disposing) {
                if (disposing) {
                    foreach (Control disposable in this.Tags) {
                        if (disposable.InvokeRequired)
                            disposable.BeginInvoke(new MethodInvoker(disposable.Dispose));
                        else disposable.Dispose();
                    }
                }
                base.Dispose(disposing);
            }

            protected override bool SetUp(int index, DeckTraversalModel traversal, out DeckTabPage page) {
                Debug.Assert(!this.m_Owner.InvokeRequired);
                Debug.Assert(this.m_Owner.IsHandleCreated);

                page = new DeckTabPage(traversal, this.m_Owner.m_Model);
                page.AutoScrollChanged += new AutoScrollEventHandler(this.m_Owner.OnAutoScrollChanged);
                this.m_Owner.TabPages.Add(page);
                this.m_Owner.m_DeckTraversalToTabPageMap.Add(traversal, page);

                // If there does not already exist a current deck traversal, set it.
                // FIXME: This really should be done by a separate "service" class.  This logic is shared with FilmStripSlideViewer.OnMouseUp.
                // FIXME: Unset the workspace's deck traversal if the deck is ever removed from the workspace in the future.
                //   Consider keeping a global list of DeckTraversalModels and update the Workspace when DTMs are added to the list.
                // NOTE: Using a temporary variable IsInstructor to resolve lock ordering issue. 
                //   We don't want to lock the workspace model after the participant model in our code.
                bool IsInstructor = false;
                using (Synchronizer.Lock(this.m_Owner.m_Model.Participant.SyncRoot)) {
                    // If the user's role is an InstructorModel, set the InstructorModel's CurrentDeckTraversal.
                    // The NetworkAssociationService will then use this to set the WorkspaceModel's CurrentDeckTraversal,
                    // and the new deck traversal will also be broadcast.
                    InstructorModel instructor = this.m_Owner.m_Model.Participant.Role as InstructorModel;
                    if (instructor != null) {
                        IsInstructor = true;
                        using (Synchronizer.Lock(instructor.SyncRoot)) {
                            if (instructor.CurrentDeckTraversal == null)
                                instructor.CurrentDeckTraversal = traversal;
                        }
                    }
                }
                
                if( !IsInstructor ) {
                    // Otherwise, we must set the CurrentDeckTraversal on the WorkspaceModel directly.
                    using (this.m_Owner.m_Model.Workspace.Lock()) {
                        if (this.m_Owner.m_Model.Workspace.CurrentDeckTraversal == null)
                            this.m_Owner.m_Model.Workspace.CurrentDeckTraversal.Value = traversal;
                    }
                }

                return true;
            }

            protected override void TearDown(int index, DeckTraversalModel member, DeckTabPage tab) {
                tab.AutoScrollChanged -= new AutoScrollEventHandler(this.m_Owner.OnAutoScrollChanged);
                this.m_Owner.m_DeckTraversalToTabPageMap.Remove(tab.DeckTraversal);
                this.m_Owner.TabPages.Remove(tab);
                tab.Dispose();
            }
        }

        #endregion DeckTraversalsMirror


        #region DeckTabPage

        /// <summary>
        /// Encapsulates a <see cref="FilmStrip"/> instance associated with a specific <see cref="DeckTraversalModel"/>.
        /// </summary>
        /// <remarks>
        /// The <see cref="DeckTabPage"/> also keeps its tab text in sync with the <see cref="DeckModel.HumanName"/> property.
        /// </remarks>
        protected internal class DeckTabPage : TabPage {
            private readonly PresenterModel m_Model;
            private readonly DeckTraversalModel m_Traversal;
            private readonly MultiColumnFilmStrip m_FilmStrip;
            private bool m_Disposed;
            /// <summary>
            /// the background color of the tab. This depends on what type of deck
            /// this tabpage represents.
            /// </summary>
            private Color back_color_;

            public DeckTabPage(DeckTraversalModel traversal, PresenterModel model) {
                ///set the background color of the tab by calling the GetDispositionColor
                ///method.
                back_color_ = traversal.Deck.GetDispositionColor();

                this.m_Model = model;
                this.m_Traversal = traversal;

                this.m_FilmStrip = new MultiColumnFilmStrip(traversal, this.m_Model);
                this.m_FilmStrip.Dock = DockStyle.Fill;



                //                this.m_FilmStrip.AutoScrollChanged += new AutoScrollEventHandler(this.OnAutoScrollChanged);

                this.m_Traversal.Deck.Changed["HumanName"].Add(new PropertyEventHandler(this.HandleHumanNameChanged));

                this.HandleHumanNameChanged(this.m_Traversal.Deck, null);

                this.Controls.Add(this.m_FilmStrip);


            }
            /// <summary>
            /// This allows us to acces the background color that the tab for this TabPage
            /// should display.
            /// </summary>
            public Color TabBackColor {
                get { return back_color_; }
            }
            protected override void Dispose(bool disposing) {
                if (this.m_Disposed) return;
                try {
                    if (disposing) {
                        this.m_Traversal.Deck.Changed["HumanName"].Remove(new PropertyEventHandler(this.HandleHumanNameChanged));
                        //                        this.m_FilmStrip.AutoScrollChanged -= new AutoScrollEventHandler(this.OnAutoScrollChanged);
                    }
                } finally {
                    base.Dispose(disposing);
                }
                this.m_Disposed = true;
            }

            public DeckTraversalModel DeckTraversal {
                get { return this.m_Traversal; }
            }

            private void HandleHumanNameChanged(object sender, PropertyEventArgs args) {
                // HandleHumanNameChangedHelper is simple enough that we don't need to make a full-fledged event queue.
                if (this.InvokeRequired)
                    this.BeginInvoke(new MethodInvoker(this.HandleHumanNameChangedHelper));
                else this.HandleHumanNameChangedHelper();
            }

            private void HandleHumanNameChangedHelper() {
                using (Synchronizer.Lock(this.m_Traversal.Deck.SyncRoot)) {
                    if ((this.m_Traversal.Deck.Disposition & DeckDisposition.Whiteboard) != 0)
                        this.Text = this.m_Traversal.Deck.HumanName.Replace("WhiteBoard", Strings.WhiteBoard);
                    else if ((this.m_Traversal.Deck.Disposition & DeckDisposition.StudentSubmission) != 0)
                        this.Text = Strings.StudentSubmissions;
                    else if ((this.m_Traversal.Deck.Disposition & DeckDisposition.PublicSubmission) != 0)
                        this.Text = Strings.PublicSubmissions;
                    else if ((this.m_Traversal.Deck.Disposition & DeckDisposition.QuickPoll) != 0)
                        this.Text = Strings.QuickPoll;
                    else this.Text = this.m_Traversal.Deck.HumanName;
                }
            }

            /// <summary>
            /// Event invoked when there is a change to the child's filmstrip auto-scroll bar
            /// </summary>
            public event AutoScrollEventHandler AutoScrollChanged;

            /// <summary>
            /// This notifies the parent control that a change to the auto-scrollbar of the child has
            /// occurred
            ///
            /// NOTE: This is part of the fix for bug 678
            /// </summary>
            /// <param name="sender"></param>
            /// <param name="e"></param>
            private void OnAutoScrollChanged(object sender, AutoScrollEventArgs e) {
                this.AutoScrollChanged(this, e);
            }
        }

        #endregion DeckTabpage
    }
}
