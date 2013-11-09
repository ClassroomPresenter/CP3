

using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Model.Workspace;

namespace UW.ClassroomPresenter.Viewer.SecondMonitor{
    /// <summary>
    /// Represents the deck navigation buttons
    /// </summary>
    public class FullScreenButtons {
        /// <summary>
        /// The model that this UI component modifies
        /// </summary>
        private readonly PresenterModel m_Model;

        NavigationToolBarButton back, forward;
        Button ret;
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="model">The model that this class modifies</param>
        public FullScreenButtons(PresenterModel model, ControlEventQueue dispatcher) {
            this.m_Model = model;


            /// <summary>
            /// Constructs all of the child buttons belonging to this class
            /// </summary>
            /// <param name="parent">The parent ToolStrip to place the buttons in</param>
            /// <param name="dispatcher">The event queue to use for events</param>

            this.ret = new ReturnButton(this.m_Model);


            // Create the back button
            this.back = new BackwardNavigationToolBarButton(dispatcher, this.m_Model);
            this.back.Image = UW.ClassroomPresenter.Properties.Resources.left;
            //this.back.AutoSize = true;

            // Create the forward button
            this.forward = new ForwardNavigationToolBarButton(dispatcher, this.m_Model);
            this.forward.Image = UW.ClassroomPresenter.Properties.Resources.right;

        }
            // Add the buttons to the parent 
        public void AddButtons(ViewerPresentationLayout parent) {
   
            this.back.Size = new Size(50, 50);
            this.forward.Size = new Size(50, 50);
            this.ret.Size = new Size(50, 50);
            this.back.Location = new Point(0, 0);//parent.MainSlideView.Height);
            this.forward.Location = new Point(this.back.Right, 0);
            this.ret.Location = new Point(this.forward.Right, 0);

            parent.MainSlideView.Controls.Add(this.back);
            parent.MainSlideView.Controls.Add(this.forward);
            parent.MainSlideView.Controls.Add(this.ret);

        }

        public void RemoveButtons(ViewerPresentationLayout parent) {
            parent.MainSlideView.Controls.Remove(this.forward);
            parent.MainSlideView.Controls.Remove(this.back);
            parent.MainSlideView.Controls.Remove(this.ret);
        }




        #region NavigationToolBarButton

        /// <summary>
        /// Abstract class representing navigation toolbar buttons
        /// </summary>
        protected abstract class NavigationToolBarButton : Button {
            /// <summary>
            /// Event queue to post events to
            /// </summary>
            private readonly ControlEventQueue m_EventQueue;
            /// <summary>
            /// Property dispatcher
            /// </summary>
            private readonly IDisposable m_CurrentDeckTraversalDispatcher;
            /// <summary>
            /// Property Listener
            /// </summary>
            private readonly EventQueue.PropertyEventDispatcher m_TraversalChangedDispatcher;

            /// <summary>
            /// The model that this class modifies
            /// </summary>
            private readonly PresenterModel m_Model;
            /// <summary>
            /// The decktraversal model that this class modifies
            /// </summary>
            private DeckTraversalModel m_CurrentDeckTraversal;
            /// <summary>
            /// Signals if this object has been disposed or not
            /// </summary>
            private bool m_Disposed;

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="dispatcher">The event queue</param>
            /// <param name="model">The model</param>
            protected NavigationToolBarButton(ControlEventQueue dispatcher, PresenterModel model) {
                this.m_EventQueue = dispatcher;
                this.m_Model = model;

                // Listen to changes in the deck traversal
                this.m_TraversalChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandleTraversalChanged));
                // Don't call the event handler immediately (use Listen instead of ListenAndInitialize)
                // because it needs to be delayed until Initialize() below.
                this.m_CurrentDeckTraversalDispatcher =
                    this.m_Model.Workspace.CurrentDeckTraversal.Listen(dispatcher,
                    delegate(Property<DeckTraversalModel>.EventArgs args) {
                        this.CurrentDeckTraversal = args.New;
                    });
            }

            protected virtual void Initialize() {
                using (Synchronizer.Lock(this.m_Model.Workspace.CurrentDeckTraversal.SyncRoot)) {
                    this.CurrentDeckTraversal = this.m_Model.Workspace.CurrentDeckTraversal.Value;
                }
            }

            protected override void Dispose(bool disposing) {
                if (this.m_Disposed) return;
                try {
                    if (disposing) {
                        this.m_CurrentDeckTraversalDispatcher.Dispose();
                        // Unregister event listeners via the CurrentDeckTraversal setter
                        this.CurrentDeckTraversal = null;
                    }
                }
                finally {
                    base.Dispose(disposing);
                }
                this.m_Disposed = true;
            }

            protected abstract string TraversalProperty { get; }

            protected virtual DeckTraversalModel CurrentDeckTraversal {
                get { return this.m_CurrentDeckTraversal; }
                set {
                    if (this.m_CurrentDeckTraversal != null) {
                        this.m_CurrentDeckTraversal.Changed[this.TraversalProperty].Remove(this.m_TraversalChangedDispatcher.Dispatcher);
                    }

                    this.m_CurrentDeckTraversal = value;

                    if (this.m_CurrentDeckTraversal != null) {
                        this.m_CurrentDeckTraversal.Changed[this.TraversalProperty].Add(this.m_TraversalChangedDispatcher.Dispatcher);
                    }

                    this.m_TraversalChangedDispatcher.Dispatcher(this, null);
                }
            }

            protected abstract void HandleTraversalChanged(object sender, PropertyEventArgs args);
        }

        #endregion

        #region ForwardNavigationToolBarButton

        /// <summary>
        /// Button handling forward navigation in decks
        /// </summary>
        protected class ForwardNavigationToolBarButton : NavigationToolBarButton {
            /// <summary>
            /// The presenter model
            /// NOTE: Is this replicated with the base class???
            /// </summary>
            private readonly PresenterModel m_Model;

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="dispatcher">The event queue</param>
            /// <param name="model">The model</param>
            public ForwardNavigationToolBarButton(ControlEventQueue dispatcher, PresenterModel model)
                : base(dispatcher, model) {
                this.m_Model = model;
                //this.ToolTipText = "Go to the next slide.";

                this.Initialize();
            }

            /// <summary>
            /// This class listens to changes in the "Next" property of the deck traversal
            /// </summary>
            protected override string TraversalProperty {
                get { return "Next"; }
            }

            /// <summary>
            /// Handles changes to the DeckTraversal and the "Next" property
            /// </summary>
            /// <param name="sender">The event sender</param>
            /// <param name="args">Arguments</param>
            protected override void HandleTraversalChanged(object sender, PropertyEventArgs args) {
                if (this.CurrentDeckTraversal == null) {
                    this.Enabled = false;
                }
                else {
                    using (Synchronizer.Lock(this.CurrentDeckTraversal.SyncRoot)) {
                        using (Synchronizer.Lock(this.CurrentDeckTraversal.Deck.SyncRoot)) {
                            // Update the button state.  Enable the button if there is a "next" slide,
                            // OR if this is a non-remote whiteboard deck (to which we can add additional slides).
                            if (((this.CurrentDeckTraversal.Deck.Disposition & DeckDisposition.Whiteboard) != 0) && ((this.CurrentDeckTraversal.Deck.Disposition & DeckDisposition.Remote) == 0))
                                this.Enabled = true;
                            else
                                this.Enabled = (this.CurrentDeckTraversal.Next != null);

     
                        }
                    }
                }
            }

            /// <summary>
            /// Handle the button being clicked
            /// </summary>
            /// <param name="args">The event arguments</param>
            protected override void OnClick(EventArgs args) {
                using (Synchronizer.Lock(this)) {
                    if (this.CurrentDeckTraversal == null) return;
                    using (Synchronizer.Lock(this.CurrentDeckTraversal.SyncRoot)) {
                        if (this.CurrentDeckTraversal.Next != null)
                            this.CurrentDeckTraversal.Current = this.CurrentDeckTraversal.Next;
                        else {
                            // Add a whiteboard slide if we are at the end of the deck
                            using (Synchronizer.Lock(this.CurrentDeckTraversal.Deck.SyncRoot)) {
                                if (((this.CurrentDeckTraversal.Deck.Disposition & DeckDisposition.Whiteboard) != 0) && ((this.CurrentDeckTraversal.Deck.Disposition & DeckDisposition.Remote) == 0)) {
                                    // Add the new slide
                                    SlideModel slide = new SlideModel(Guid.NewGuid(), new LocalId(), SlideDisposition.Empty, UW.ClassroomPresenter.Viewer.ViewerForm.DEFAULT_SLIDE_BOUNDS);
                                    this.CurrentDeckTraversal.Deck.InsertSlide(slide);
                                    using (Synchronizer.Lock(this.CurrentDeckTraversal.Deck.TableOfContents.SyncRoot)) {
                                        // Add the new table of contents entry
                                        TableOfContentsModel.Entry entry = new TableOfContentsModel.Entry(Guid.NewGuid(), this.CurrentDeckTraversal.Deck.TableOfContents, slide);
                                        this.CurrentDeckTraversal.Deck.TableOfContents.Entries.Add(entry);

                                        // Move to the slide
                                        this.CurrentDeckTraversal.Current = entry;
                                    }
                                }
                            }
                        }
                    }
                }
                base.OnClick(args);
            }
        }

        #endregion

        #region BackwardNavigationToolBarButton

        /// <summary>
        /// Button handling backward navigation in decks
        /// </summary>
        protected class BackwardNavigationToolBarButton : NavigationToolBarButton {
            /// <summary>
            /// The presenter model
            /// NOTE: Is this replicated with the base class???
            /// </summary>
            private readonly PresenterModel m_Model;

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="dispatcher">The event queue</param>
            /// <param name="model">The model</param>
            public BackwardNavigationToolBarButton(ControlEventQueue dispatcher, PresenterModel model)
                : base(dispatcher, model) {
                this.m_Model = model;

                this.Initialize();
            }

            /// <summary>
            /// This class listens to changes in the "Previous" property of the deck traversal
            /// </summary>
            protected override string TraversalProperty {
                get { return "Previous"; }
            }

            /// <summary>
            /// Handles changes to the DeckTraversal and the "Previous" property
            /// </summary>
            /// <param name="sender">The event sender</param>
            /// <param name="args">Arguments</param>
            protected override void HandleTraversalChanged(object sender, PropertyEventArgs args) {
                if (this.CurrentDeckTraversal == null) {
                    this.Enabled = false;
                }
                else {
                    using (Synchronizer.Lock(this.CurrentDeckTraversal.SyncRoot)) {
                        this.Enabled = (this.CurrentDeckTraversal.Previous != null);
                    }
                }
            }

            /// <summary>
            /// Handle the button being clicked
            /// </summary>
            /// <param name="args">The event arguments</param>
            protected override void OnClick(EventArgs args) {
                if (this.CurrentDeckTraversal == null) return;
                using (Synchronizer.Lock(this.CurrentDeckTraversal.SyncRoot)) {
                    this.CurrentDeckTraversal.Current = this.CurrentDeckTraversal.Previous;
                }
                base.OnClick(args);
            }
        }

        #endregion

        protected class ReturnButton:Button {
            PresenterModel m_Model;
            public ReturnButton(PresenterModel model) {
                this.m_Model = model;
                this.Text = "Return";
            }
            protected override void OnClick(EventArgs e) {
                using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot)) {
                    this.m_Model.ViewerState.PrimaryMonitorFullScreen = false;
                }
                base.OnClick(e);
            }
        }
 
    }
}
