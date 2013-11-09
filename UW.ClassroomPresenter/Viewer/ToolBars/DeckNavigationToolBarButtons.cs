// $Id: DeckNavigationToolBarButtons.cs 1658 2008-07-28 17:49:52Z lining $


using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Model.Workspace;

namespace UW.ClassroomPresenter.Viewer.ToolBars {
    /// <summary>
    /// Represents the deck navigation buttons
    /// </summary>
    public class DeckNavigationToolBarButtons {
        /// <summary>
        /// The model that this UI component modifies
        /// </summary>
        private readonly PresenterModel m_Model;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="model">The model that this class modifies</param>
        public DeckNavigationToolBarButtons(PresenterModel model) {
            this.m_Model = model;
        }

        /// <summary>
        /// Constructs all of the child buttons belonging to this class
        /// </summary>
        /// <param name="parent">The parent ToolStrip to place the buttons in</param>
        /// <param name="dispatcher">The event queue to use for events</param>
        public void MakeButtons(ToolStrip parent, ControlEventQueue dispatcher) {
            NavigationToolBarButton back, forward, linked, whiteboard;

            // Create the back button
            back = new BackwardNavigationToolBarButton( dispatcher, this.m_Model );
            back.Image = UW.ClassroomPresenter.Properties.Resources.left;
            back.AutoSize = true;

            // Create the forward button
            forward = new ForwardNavigationToolBarButton( dispatcher, this.m_Model );
            forward.Image = UW.ClassroomPresenter.Properties.Resources.right;
            forward.AutoSize = true;

            // Create the whiteboard button
            whiteboard = new WhiteboardToolBarButton( dispatcher, this.m_Model );
            whiteboard.Image = UW.ClassroomPresenter.Properties.Resources.whiteboard;
            whiteboard.AutoSize = true;

            // Create the linked button
            linked = new LinkedNavigationToolBarButton( dispatcher, this.m_Model );
            linked.Image = UW.ClassroomPresenter.Properties.Resources.linked;
            linked.AutoSize = true;

            // Add the buttons to the parent ToolStrip
           
            parent.Items.Add( linked );
            //If we are not in student mode, then linked wasn't drawn, and we don't want to add this ToolStripSeparator.
            
            parent.Items.Add(new ToolStripSeparator());

            parent.Items.Add( whiteboard );
            parent.Items.Add(new ToolStripSeparator());
            parent.Items.Add(back);
            parent.Items.Add(forward);
        }

        /// <summary>
        /// Constructs all of the child buttons belonging to this class
        /// </summary>
        /// <param name="main">The main ToolStrip to place the buttons in</param>
        /// <param name="extra">The extra ToolStrip to place the buttons in</param>
        /// <param name="dispatcher">The event queue to use for events</param>
        public void MakeButtons(ToolStrip main, ToolStrip extra, ControlEventQueue dispatcher) {
            //NavigationToolBarButton back, forward, linked, whiteboard;
            NavigationToolBarButton linked, whiteboard;

            // Create the back button
            //back = new BackwardNavigationToolBarButton( dispatcher, this.m_Model );
            //back.Image = UW.ClassroomPresenter.Properties.Resources.left;
            //back.AutoSize = true;

            // Create the forward button
            //forward = new ForwardNavigationToolBarButton( dispatcher, this.m_Model );
            //forward.Image = UW.ClassroomPresenter.Properties.Resources.right;
            //forward.AutoSize = true;

            // Create the whiteboard button
            whiteboard = new WhiteboardToolBarButton( dispatcher, this.m_Model );
            whiteboard.Image = UW.ClassroomPresenter.Properties.Resources.whiteboard;
            whiteboard.AutoSize = true;

            // Create the linked button
            linked = new LinkedNavigationToolBarButton( dispatcher, this.m_Model );
            linked.Image = UW.ClassroomPresenter.Properties.Resources.linked;
            linked.AutoSize = true;

            // Add the buttons to the parent ToolStrip
            extra.Items.Add( whiteboard );
            extra.Items.Add(new ToolStripSeparator());
            //main.Items.Add(back);
            //extra.Items.Add(forward);
            //main.Items.Add(new ToolStripSeparator());
            //extra.Items.Add(new ToolStripSeparator());

            //If we are not in student mode, then linked wasn't drawn, and we don't want to add this ToolStripSeparator.
            main.Items.Add(linked);
        }

        #region NavigationToolBarButton

        /// <summary>
        /// Abstract class representing navigation toolbar buttons
        /// </summary>
        protected abstract class NavigationToolBarButton : ToolStripButton {
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
                if(this.m_Disposed) return;
                try {
                    if(disposing) {
                        this.m_CurrentDeckTraversalDispatcher.Dispose();
                        // Unregister event listeners via the CurrentDeckTraversal setter
                        this.CurrentDeckTraversal = null;
                    }
                } finally {
                    base.Dispose(disposing);
                }
                this.m_Disposed = true;
            }

            protected abstract string TraversalProperty { get; }

            protected virtual DeckTraversalModel CurrentDeckTraversal {
                get { return this.m_CurrentDeckTraversal; }
                set {
                    if(this.m_CurrentDeckTraversal != null) {
                        this.m_CurrentDeckTraversal.Changed[this.TraversalProperty].Remove(this.m_TraversalChangedDispatcher.Dispatcher);
                    }

                    this.m_CurrentDeckTraversal = value;

                    if(this.m_CurrentDeckTraversal != null) {
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
            public ForwardNavigationToolBarButton(ControlEventQueue dispatcher, PresenterModel model) : base(dispatcher, model) {
                this.m_Model = model;
                this.ToolTipText = Strings.ForwardTooltipNextSlide;

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
                if(this.CurrentDeckTraversal == null) {
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

                            // Update the tooltip text
                            if (this.Enabled == true && this.CurrentDeckTraversal.Next == null)
                                this.ToolTipText = Strings.ForwardTooltipAddBlank;
                            else
                                this.ToolTipText = Strings.ForwardTooltipNextSlide;
                        }
                    }
                }
            }
            
            /// <summary>
            /// Handle the button being clicked
            /// </summary>
            /// <param name="args">The event arguments</param>
            protected override void OnClick( EventArgs args ) {
                using( Synchronizer.Lock( this ) ) {
                    if( this.CurrentDeckTraversal == null ) return;
                    using( Synchronizer.Lock( this.CurrentDeckTraversal.SyncRoot ) ) {
                        if( this.CurrentDeckTraversal.Next != null )
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
                base.OnClick( args );
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
            public BackwardNavigationToolBarButton( ControlEventQueue dispatcher, PresenterModel model )
                : base( dispatcher, model ) {
                this.m_Model = model;
                this.ToolTipText = Strings.BackwardTooltipPreviousSlide;

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
            protected override void HandleTraversalChanged( object sender, PropertyEventArgs args ) {
                if(this.CurrentDeckTraversal == null) {
                    this.Enabled = false;
                }
                else {
                    using(Synchronizer.Lock(this.CurrentDeckTraversal.SyncRoot)) {
                        this.Enabled = (this.CurrentDeckTraversal.Previous != null);
                    }
                }
            }

            /// <summary>
            /// Handle the button being clicked
            /// </summary>
            /// <param name="args">The event arguments</param>
            protected override void OnClick( EventArgs args ) {
                if( this.CurrentDeckTraversal == null ) return;
                using( Synchronizer.Lock( this.CurrentDeckTraversal.SyncRoot ) ) {
                    this.CurrentDeckTraversal.Current = this.CurrentDeckTraversal.Previous;
                }
                base.OnClick( args );
            }
        }

        #endregion

        #region LinkedNavigationToolBarButton

        /// <summary>
        /// Button handling student's having their navigation linked to the instructor
        /// </summary>
        protected class LinkedNavigationToolBarButton : NavigationToolBarButton {
            /// <summary>
            /// The presenter model
            /// NOTE: Is this replicated with the base class???
            /// </summary>
            private readonly PresenterModel m_Model;
            /// <summary>
            /// Listens for changes to the user role
            /// </summary>
            private readonly EventQueue.PropertyEventDispatcher m_RoleChangedDispatcher;

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="dispatcher">The event queue</param>
            /// <param name="model">The model</param>
            public LinkedNavigationToolBarButton(ControlEventQueue dispatcher, PresenterModel model) : base(dispatcher, model) {
                this.m_Model = model;

                // Listen for changes to the Role property
                this.m_RoleChangedDispatcher = new EventQueue.PropertyEventDispatcher(dispatcher, new PropertyEventHandler(this.HandleRoleChanged));
                this.m_Model.Participant.Changed["Role"].Add(this.m_RoleChangedDispatcher.Dispatcher);
                this.m_RoleChangedDispatcher.Dispatcher(this, null);

                this.Initialize();
            }

            /// <summary>
            /// Destroy resources, i.e. stop listening to model changes
            /// </summary>
            /// <param name="disposing">True if we are destroying all objects</param>
            protected override void Dispose(bool disposing) {
                if(disposing) {
                    this.m_Model.Participant.Changed["Role"].Remove(this.m_RoleChangedDispatcher.Dispatcher);
                }

                base.Dispose(disposing);
            }

            /// <summary>
            /// This class listens to changes in the "Mode" property of the deck traversal
            /// </summary>
            protected override string TraversalProperty {
                get { return "Mode"; }
            }

            /// <summary>
            /// Only update the current deck traversal if it is a linked deck traversal
            /// </summary>
            protected override DeckTraversalModel CurrentDeckTraversal {
                get { return base.CurrentDeckTraversal; }
                set { base.CurrentDeckTraversal = (value is LinkedDeckTraversalModel) ? value : null; }
            }

            /// <summary>
            /// Handle the deck traversal changing and the "Mode" property.
            /// </summary>
            /// <param name="sender">The event sender</param>
            /// <param name="args">The arguments</param>
            protected override void HandleTraversalChanged(object sender, PropertyEventArgs args) {
                if(this.CurrentDeckTraversal is LinkedDeckTraversalModel) {
                    using(Synchronizer.Lock(this.CurrentDeckTraversal.SyncRoot)) {
                        switch(((LinkedDeckTraversalModel) this.CurrentDeckTraversal).Mode) {
                            case LinkedDeckTraversalModel.DeckTraversalSelector.Linked: {
                                this.Checked = true;
                                this.ToolTipText = "Enable free navigation independent of the instructor";
                                this.Image = UW.ClassroomPresenter.Properties.Resources.linked;
                                break;
                            }
                            case LinkedDeckTraversalModel.DeckTraversalSelector.Unlinked:
                            default: {
                                this.Checked = false;
                                this.Image = UW.ClassroomPresenter.Properties.Resources.unlinked;
                                break;
                            }
                        }
                    }

                    this.Enabled = true;
                }
                else {
                    this.Checked = false;
                    this.Enabled = false;
                }

                this.ToolTipText = (this.Checked)
                    ? Strings.EnableNavigationIndInstructor
                    : Strings.DisableNavigationIndInstructor;
            }

            /// <summary>
            /// Handle the button being clicked
            /// </summary>
            /// <param name="args">The event arguments</param>
            protected override void OnClick(EventArgs args) {
                if (this.CurrentDeckTraversal is LinkedDeckTraversalModel) {                       
                    if (this.Checked) {
                        using (Synchronizer.Lock(this.CurrentDeckTraversal.SyncRoot)) {
                            ((LinkedDeckTraversalModel)this.CurrentDeckTraversal).Mode = LinkedDeckTraversalModel.DeckTraversalSelector.Unlinked;
                        }
                        using (Synchronizer.Lock(this.m_Model.Workspace.DeckTraversals.SyncRoot)) {
                            foreach (DeckTraversalModel deck in this.m_Model.Workspace.DeckTraversals) {
                                using (Synchronizer.Lock(deck.SyncRoot)) {
                                    if (deck is LinkedDeckTraversalModel) {
                                        ((LinkedDeckTraversalModel)deck).Mode = LinkedDeckTraversalModel.DeckTraversalSelector.Unlinked;
                                    }
                                }
                            }
                        }
                        using (Synchronizer.Lock(this.m_Model.Workspace.CurrentDeckTraversal.SyncRoot)) {
                            ((LinkedDeckTraversalModel)this.m_Model.Workspace.CurrentDeckTraversal.Value).Mode = LinkedDeckTraversalModel.DeckTraversalSelector.Unlinked;
                        }
                    } else {
                        using (Synchronizer.Lock(this.m_Model.Workspace.DeckTraversals.SyncRoot)) {
                            foreach (DeckTraversalModel deck in this.m_Model.Workspace.DeckTraversals) {
                                using (Synchronizer.Lock(deck.SyncRoot)) {
                                    if (deck is LinkedDeckTraversalModel) {
                                        ((LinkedDeckTraversalModel)deck).Mode = LinkedDeckTraversalModel.DeckTraversalSelector.Linked;
                                        using (Synchronizer.Lock(this.m_Model.Workspace.CurrentPresentation.Value.Owner.SyncRoot)) {
                                            using (Synchronizer.Lock(this.m_Model.Workspace.CurrentPresentation.Value.Owner.Role)) {
                                                using (Synchronizer.Lock(this.m_Model.Workspace.CurrentPresentation.Value.Owner.Role.SyncRoot)) {
                                                    DeckTraversalModel current = ((InstructorModel)(this.m_Model.Workspace.CurrentPresentation.Value.Owner.Role)).CurrentDeckTraversal;
                                                    if (current != null && deck.Id == current.Id) {
                                                        this.m_Model.Workspace.CurrentDeckTraversal.Value = deck;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                using( Synchronizer.Lock( this.CurrentDeckTraversal.SyncRoot ) ) {
                                    this.CurrentDeckTraversal = this.m_Model.Workspace.CurrentDeckTraversal.Value;
                                    ((LinkedDeckTraversalModel)this.m_Model.Workspace.CurrentDeckTraversal.Value).Mode = LinkedDeckTraversalModel.DeckTraversalSelector.Linked;
                                    ((LinkedDeckTraversalModel)this.CurrentDeckTraversal).Mode = LinkedDeckTraversalModel.DeckTraversalSelector.Linked;
                                }
                            }
                        }
  
                    }

                    this.HandleTraversalChanged(this, null);
                }
                base.OnClick(args);
            }

            /// <summary>
            /// Handle the role changing (only display when we are not an instructor)
            /// </summary>
            /// <param name="sender">The event sender</param>
            /// <param name="args">The arguments</param>
            private void HandleRoleChanged(object sender, PropertyEventArgs args) {
                using(Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                    this.Visible = !(this.m_Model.Participant.Role is InstructorModel);
                }
            }
        }

        #endregion

        #region WhiteboardToolBarButton

        /// <summary>
        /// Button handling the display of the whiteboard deck
        /// </summary>
        protected class WhiteboardToolBarButton : NavigationToolBarButton
        {
            /// <summary>
            /// The presenter model
            /// NOTE: Is this replicated with the base class???
            /// </summary>
            private readonly PresenterModel m_Model;

            /// Listens for changes to the user role
            /// </summary>
            private readonly EventQueue.PropertyEventDispatcher m_RoleChangedDispatcher;



            /// <summary>

            /// The previous whiteboard deck
            /// </summary>
            /// 
            private DeckTraversalModel m_PreviousWhiteboard;
            /// <summary>
            /// The previous normal (non-whiteboard) deck
            /// </summary>
            private DeckTraversalModel m_PreviousNormal;

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="dispatcher">The event queue</param>
            /// <param name="model">The model</param>
            public WhiteboardToolBarButton(ControlEventQueue dispatcher, PresenterModel model)
                : base(dispatcher, model)
            {
                this.m_Model = model;


                 // Listen for changes to the Role property
                this.m_RoleChangedDispatcher = new EventQueue.PropertyEventDispatcher(dispatcher, new PropertyEventHandler(this.HandleRoleChanged));
                this.m_Model.Participant.Changed["Role"].Add(this.m_RoleChangedDispatcher.Dispatcher);
                this.m_RoleChangedDispatcher.Dispatcher(this, null);


                this.Initialize();
            }


            /// <summary>

            /// Destroy resources, i.e. stop listening to model changes
            /// </summary>
            /// <param name="disposing">True if we are destroying all objects</param>
            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    this.m_Model.Participant.Changed["Role"].Remove(this.m_RoleChangedDispatcher.Dispatcher);
                }

                base.Dispose(disposing);
            }


            /// <summary>

            /// This class listens to changes in the "Current" property of the deck traversal
            /// </summary>
            protected override string TraversalProperty
            {
                get { return "Current"; }
            }

            /// <summary>
            /// Handles changes to the deck traversal and changes to the "Current" property
            /// </summary>
            /// <param name="sender">The event sender</param>
            /// <param name="args">The arguments</param>
            protected override void HandleTraversalChanged(object sender, PropertyEventArgs args)
            {
                if (this.CurrentDeckTraversal == null)
                {
                    this.Checked = false;
                }
                else
                {
                    if ((this.CurrentDeckTraversal.Deck.Disposition & DeckDisposition.Whiteboard) != 0)
                    {
                        this.m_PreviousWhiteboard = this.CurrentDeckTraversal;
                        this.Checked = true;
                    }
                    else
                    {
                        this.m_PreviousNormal = this.CurrentDeckTraversal;
                        this.Checked = false;
                    }
                }

                this.ToolTipText = (this.Checked)
                    ? Strings.SwitchToNonWhiteboardDeck
                    : Strings.SwitchWhiteboardDeck;
            }


            /// <summary>
            /// Handle clicking of this button (switch between whiteboard and normal deck)
            /// </summary>
            /// <param name="args">The event arguments</param>
            protected override void OnClick(EventArgs args)
            {
                using (this.m_Model.Workspace.Lock())
                {
                    using( Synchronizer.Lock( this.m_Model.Participant.SyncRoot ) ) 
                    {
                        DeckTraversalModel traversal;

                        if (this.CurrentDeckTraversal == null || (this.CurrentDeckTraversal.Deck.Disposition & DeckDisposition.Whiteboard) == 0)
                        {
                            traversal = this.m_PreviousWhiteboard;

                            // Make sure the deck hasn't been closed.
                            if (!this.m_Model.Workspace.DeckTraversals.Contains(traversal))
                                traversal = this.m_PreviousWhiteboard = null;

                            // If there is no previous whiteboard traversal, find one or create one.
                            if (traversal == null)
                            {
                                // TODO: If the participant is an instructor, *only* search the whiteboards in the active presentation.
                                //   Then, when changing the current deck, also change the current presentation if necessary.

                                foreach (DeckTraversalModel candidate in this.m_Model.Workspace.DeckTraversals)
                                {
                                    if ((candidate.Deck.Disposition & DeckDisposition.Whiteboard) != 0)
                                    {
                                        traversal = candidate;
                                        break;
                                    }
                                }

                                // If there is no existing whiteboard deck, create one.
                                if (traversal == null)
                                {
                                    // Change the name of whiteboard according to the number of it
                                    string wbname = "WhiteBoard " + (++UW.ClassroomPresenter.Viewer.ViewerForm.white_board_num).ToString();
                                    // NOTE: This code is duplicated in DecksMenu.CreateBlankWhiteboardDeckMenuItem.
                                    DeckModel deck = new DeckModel(Guid.NewGuid(), DeckDisposition.Whiteboard, wbname);
                                    using (Synchronizer.Lock(deck.SyncRoot))
                                    {
                                        SlideModel slide = new SlideModel(Guid.NewGuid(), new LocalId(), SlideDisposition.Empty, UW.ClassroomPresenter.Viewer.ViewerForm.DEFAULT_SLIDE_BOUNDS);

                                        deck.InsertSlide(slide);

                                        using (Synchronizer.Lock(deck.TableOfContents.SyncRoot))
                                        {
                                            TableOfContentsModel.Entry entry = new TableOfContentsModel.Entry(Guid.NewGuid(), deck.TableOfContents, slide);
                                            deck.TableOfContents.Entries.Add(entry);
                                        }
                                    }

                                    traversal = new SlideDeckTraversalModel(Guid.NewGuid(), deck);

                                    if (this.m_Model.Workspace.CurrentPresentation.Value != null)
                                    {
                                        using (Synchronizer.Lock((~this.m_Model.Workspace.CurrentPresentation).SyncRoot))
                                        {
                                            (~this.m_Model.Workspace.CurrentPresentation).DeckTraversals.Add(traversal);
                                        }
                                    }
                                    else
                                    {
                                        this.m_Model.Workspace.DeckTraversals.Add(traversal);
                                    }
                                }
                            }
                        }
                        else
                        {
                            traversal = this.m_PreviousNormal;

                            // Make sure the deck hasn't been closed.
                            if (!this.m_Model.Workspace.DeckTraversals.Contains(traversal))
                                traversal = this.m_PreviousNormal = null;

                            // Search the workspace for a non-whiteboard deck if we haven't encountered one before.
                            if (traversal == null)
                            {
                                // TODO: If the participant is an instructor, *only* search the whiteboards in the active presentation.
                                foreach (DeckTraversalModel candidate in this.m_Model.Workspace.DeckTraversals)
                                {
                                    if ((candidate.Deck.Disposition & DeckDisposition.Whiteboard) == 0)
                                    {
                                        traversal = candidate;
                                        break;
                                    }
                                }

                                // If we still haven't found a non-whiteboard deck, do nothing.
                                if (traversal == null)
                                    return;
                            }
                        }

                        // If the user's role is an InstructorModel, set the InstructorModel's CurrentDeckTraversal.
                        // The NetworkAssociationService will then use this to set the WorkspaceModel's CurrentDeckTraversal,
                        // and the new deck traversal will also be broadcast.
                        InstructorModel instructor = this.m_Model.Participant.Role as InstructorModel;
                        if (instructor != null)
                        {
                            using (Synchronizer.Lock(instructor.SyncRoot))
                            {
                                instructor.CurrentDeckTraversal = traversal;
                            }
                        }
                        else
                        {
                            // Otherwise, we must set the CurrentDeckTraversal on the WorkspaceModel directly.
                            using (this.m_Model.Workspace.Lock())
                            {
                                this.m_Model.Workspace.CurrentDeckTraversal.Value = traversal;
                            }
                        }
                    }
                }
                base.OnClick(args);
            }



            /// <summary>
            /// Handle the role changing (only display when we are an instructor)
            /// </summary>
            /// <param name="sender">The event sender</param>
            /// <param name="args">The arguments</param>
            private void HandleRoleChanged(object sender, PropertyEventArgs args)
            {
                using (Synchronizer.Lock(this.m_Model.Participant.SyncRoot))
                {
                    this.Visible = (this.m_Model.Participant.Role is InstructorModel);
                }
            }

        }

     
        #endregion
    }
}
