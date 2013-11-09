// $Id: StudentMenu.cs 799 2005-09-27 18:55:08Z shoat $

using System;
using System.Windows.Forms;
using System.Collections.Generic;

using UW.ClassroomPresenter.Decks;
using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Viewer.Slides;

namespace UW.ClassroomPresenter.Viewer.Menus {
    #region StudentMenu

    /// <summary>
    /// Represents the student menu and all of the menu items under it
    /// </summary>
    public class StudentMenu : MenuItem {
        private readonly PresenterModel m_Model;
        private readonly ControlEventQueue m_EventQueue;
        private readonly EventQueue.PropertyEventDispatcher m_StudentMenuDispatcher;
        private bool m_Disposed = false;

        public StudentMenu(ControlEventQueue dispatcher, PresenterModel model) {
            this.m_Model = model;
            this.m_EventQueue = dispatcher;
            this.Text = Strings.Student;

            //Listen to the Role and disable if it isn't an instructor
            this.m_StudentMenuDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandleRoleChanged));
            using (Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                this.m_Model.Participant.Changed["Role"].Add(this.m_StudentMenuDispatcher.Dispatcher);
            }
            this.HandleRoleChanged(this, null);
        }

        protected override void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            try {
                if(disposing) {
                    this.m_Model.Participant.Changed["Role"].Remove(this.m_StudentMenuDispatcher.Dispatcher);
                }
            } finally {
                base.Dispose(disposing);
            }
            this.m_Disposed = true;
        }

        private void HandleRoleChanged(object sender, PropertyEventArgs args) {
            using (Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                if (this.m_Model.Participant.Role is InstructorModel) {
                    this.MenuItems.Add( new AcceptingStudentSubmissionsMenuItem( this.m_EventQueue, this.m_Model ) );
                    this.MenuItems.Add( new AcceptingQuickPollSubmissionsMenuItem( this.m_EventQueue, this.m_Model ) );
                    this.MenuItems.Add( new LinkedNavigationMenuItem( this.m_EventQueue, this.m_Model ) );
                    this.MenuItems.Add(new StudentNavigationTypeMenuItem(this.m_EventQueue, this.m_Model));
                    //this.MenuItems.Add( new StudentSubmissionStyleMenu( this.m_EventQueue, this.m_Model ) );
                    this.Enabled = this.Visible = true;
                } else {
                    this.Enabled = this.Visible = false;
                    for (int i = 0; i < this.MenuItems.Count; i++) {
                        MenuItem mi = this.MenuItems[i];
                        if (mi != null) {
                            mi.Dispose();
                        }
                    }
                    this.MenuItems.Clear();
                }
            }
        }
    }

    #endregion

    #region InstructorMenuItem

    /// <summary>
    /// An abstract class representing a menu option that is only available to instructors
    /// </summary>
    public abstract class InstructorMenuItem : MenuItem {
        protected readonly PresenterModel m_Model;
        protected readonly ControlEventQueue m_EventQueue;
        protected RoleModel m_Role;
        protected bool m_Disposed = false;
        protected readonly EventQueue.PropertyEventDispatcher m_HandleRoleChangedDispatcher;

        public InstructorMenuItem(ControlEventQueue dispatcher, PresenterModel model) {
            this.m_Model = model;
            this.m_EventQueue = dispatcher;
            this.m_HandleRoleChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandleRoleChanged));
            //Save a copy of the role
            this.m_Role = this.m_Model.Participant.Role;
            //Listen to changes in Role
            this.m_Model.Participant.Changed["Role"].Add(this.m_HandleRoleChangedDispatcher.Dispatcher);
        }

        protected override void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            try {
                if(disposing) {
                    this.m_Model.Participant.Changed["Role"].Remove(this.m_HandleRoleChangedDispatcher.Dispatcher);
                }
            } finally {
                base.Dispose(disposing);
            }
        }

        protected abstract void HandleRoleChanged(object sender, PropertyEventArgs args);
    }

    #endregion

    #region LinkedNavigationMenuItem

    /// <summary>
    /// Menu option to enable of disable linked navigation
    /// </summary>
    public class LinkedNavigationMenuItem : InstructorMenuItem {
        private readonly EventQueue.PropertyEventDispatcher m_HandleLinkedNavigationChangedDispatcher;

        public LinkedNavigationMenuItem(ControlEventQueue dispatcher, PresenterModel model) : base(dispatcher, model) {
            this.Text = Strings.ForceLinkedNavigation;
            this.m_HandleLinkedNavigationChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandleLinkedNavChanged));
            using (Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                //Listen for changes in Linked Nav for the current role, if it is an InstructorModel
                if (this.m_Role is InstructorModel) {
                    using (Synchronizer.Lock(this.m_Model.Participant.Role.SyncRoot)) {
                        ((InstructorModel)this.m_Role).Changed["ForcingStudentNavigationLock"].Add(this.m_HandleLinkedNavigationChangedDispatcher.Dispatcher);
                    }
                }
            }
            this.HandleLinkedNavChanged(this, null);
        }

        protected override void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            try {
                if(disposing) {
                    if (this.m_Role is InstructorModel) {
                        ((InstructorModel)this.m_Role).Changed["ForcingStudentNavigationLock"].Remove(this.m_HandleLinkedNavigationChangedDispatcher.Dispatcher);
                    }
                }
            } finally {
                base.Dispose(disposing);
            }
            this.m_Disposed = true;
        }

        protected override void HandleRoleChanged(object sender, PropertyEventArgs args) {
            //Unregister the previous role's handler, if needed
            if (this.m_Role is InstructorModel) {
                using (Synchronizer.Lock(this.m_Role.SyncRoot)) {
                    ((InstructorModel)this.m_Role).Changed["ForcingStudentNavigationLock"].Remove(this.m_HandleLinkedNavigationChangedDispatcher.Dispatcher);
                }
            }
            //Update the local copy
            using (Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                this.m_Role = this.m_Model.Participant.Role;
                //Register new handlers, if needed
                if (this.m_Role is InstructorModel) {
                    ((InstructorModel)this.m_Role).Changed["ForcingStudentNavigationLock"].Add(this.m_HandleLinkedNavigationChangedDispatcher.Dispatcher);
                }
            }
        }

        private void HandleLinkedNavChanged(object sender, PropertyEventArgs args) {
            if (this.m_Role is InstructorModel) {
                using (Synchronizer.Lock(this.m_Role.SyncRoot)) {
                    this.Checked = ((InstructorModel)this.m_Role).ForcingStudentNavigationLock;
                }
            }
        }

        protected override void OnClick(EventArgs e) {
            base.OnClick (e);
            if (this.m_Role is InstructorModel) {
                this.Checked = ! this.Checked;
                using (Synchronizer.Lock(this.m_Role.SyncRoot)) {
                    ((InstructorModel)this.m_Role).ForcingStudentNavigationLock = this.Checked;
                }
            }
        }

    }

    #endregion

    #region StudentNavigationTypeItem
    /// <summary>
    /// Represents the menu option to set student navigation types
    /// </summary>
    public class StudentNavigationTypeMenuItem : InstructorMenuItem {
        private readonly EventQueue.PropertyEventDispatcher m_HandleStudentNavChangedDispatcher;
        private LinkedDeckTraversalModel.NavigationSelector m_StudentNavigationType;
        private MenuItem m_FullNavigation;
        private MenuItem m_VisitedSlidesNavigation;
        private MenuItem m_NoNavigation;


        public StudentNavigationTypeMenuItem(ControlEventQueue dispatcher, PresenterModel model)
            : base(dispatcher, model) {
            this.Text = Strings.StudentNavigationType;

            this.m_FullNavigation = new MenuItem(Strings.FullNavigation);
            this.m_FullNavigation.Click += new EventHandler(m_FullNavigation_Click);
            this.MenuItems.Add(this.m_FullNavigation);

            this.m_VisitedSlidesNavigation = new MenuItem(Strings.VisitedSlidesNavigation);
            this.m_VisitedSlidesNavigation.Click += new EventHandler(m_VisitedSlidesNavigation_Click);
            this.MenuItems.Add(this.m_VisitedSlidesNavigation);

            this.m_NoNavigation = new MenuItem(Strings.NoNavigation);
            this.m_NoNavigation.Click += new EventHandler(m_NoNavigation_Click);
            this.MenuItems.Add(this.m_NoNavigation);
            this.m_HandleStudentNavChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandleStudentNavChanged));
            using (Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                //Listen to changes in Student Nav Types for the current role, if it is an InstructorModel
                if (this.m_Role is InstructorModel) {
                    using (Synchronizer.Lock(this.m_Model.Participant.Role.SyncRoot)) {
                        ((InstructorModel)this.m_Role).Changed["StudentNavigationType"].Add(this.m_HandleStudentNavChangedDispatcher.Dispatcher);
                    }
                }
            }            
            this.HandleStudentNavChanged(this, null);
            using (Synchronizer.Lock(this.m_Role)) {
                if (((InstructorModel)this.m_Role).StudentNavigationType == LinkedDeckTraversalModel.NavigationSelector.Full)
                    this.m_FullNavigation.Checked = true;
            }
        }

        protected void m_FullNavigation_Click(object sender, EventArgs e) {
            this.m_StudentNavigationType = LinkedDeckTraversalModel.NavigationSelector.Full;
            this.m_FullNavigation.Checked = true;
            this.m_VisitedSlidesNavigation.Checked = false;
            this.m_NoNavigation.Checked = false;
            using (Synchronizer.Lock(this.m_Role.SyncRoot)) {
                ((InstructorModel)this.m_Role).StudentNavigationType = this.m_StudentNavigationType;
                ((InstructorModel)this.m_Role).ForcingStudentNavigationLock = false;
            }
        }

        protected void m_VisitedSlidesNavigation_Click(object sender, EventArgs e) {
            this.m_StudentNavigationType = LinkedDeckTraversalModel.NavigationSelector.Visited;
            this.m_FullNavigation.Checked = false;
            this.m_VisitedSlidesNavigation.Checked = true;
            this.m_NoNavigation.Checked = false;
            using (Synchronizer.Lock(this.m_Role.SyncRoot)) {
                ((InstructorModel)this.m_Role).StudentNavigationType = this.m_StudentNavigationType;
                ((InstructorModel)this.m_Role).ForcingStudentNavigationLock = false;
            }
        }

        protected void m_NoNavigation_Click(object sender, EventArgs e) {
            this.m_StudentNavigationType = LinkedDeckTraversalModel.NavigationSelector.None;
            this.m_FullNavigation.Checked = false;
            this.m_VisitedSlidesNavigation.Checked = false;
            this.m_NoNavigation.Checked = true;
            using (Synchronizer.Lock(this.m_Role.SyncRoot)) {
                ((InstructorModel)this.m_Role).StudentNavigationType = this.m_StudentNavigationType;
                ((InstructorModel)this.m_Role).ForcingStudentNavigationLock = true;
            }
        }

        protected override void Dispose(bool disposing){
            if (this.m_Disposed) return;
            try {
                if (disposing) {
                    if (this.m_Role is InstructorModel) {
                        ((InstructorModel)this.m_Role).Changed["StudentNavigationTypes"].Remove(this.m_HandleStudentNavChangedDispatcher.Dispatcher);
                    }
                }
            }
            finally {
                base.Dispose(disposing);
            }
            this.m_Disposed = true;
        }

        protected override void HandleRoleChanged(object sender, PropertyEventArgs args) {
            //Unregister the previous role's handler, if needed
            if (this.m_Role is InstructorModel) {
                using (Synchronizer.Lock(this.m_Role.SyncRoot)) {
                    ((InstructorModel)this.m_Role).Changed["StudentNavigationType"].Remove(this.m_HandleStudentNavChangedDispatcher.Dispatcher);
                }
            }
            //Update the local copy
            using (Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                this.m_Role = this.m_Model.Participant.Role;
                //Register new handlers, if needed
                if (this.m_Role is InstructorModel) {
                    ((InstructorModel)this.m_Role).Changed["StudentNavigationType"].Add(this.m_HandleStudentNavChangedDispatcher.Dispatcher);
                }
            }
        }

        private void HandleStudentNavChanged(object sender, PropertyEventArgs args) {
            if (this.m_Role is InstructorModel) {
                using (Synchronizer.Lock(this.m_Role.SyncRoot)) {
                    this.m_StudentNavigationType = ((InstructorModel)this.m_Role).StudentNavigationType;
                }
            }
        }

        protected override void OnClick(EventArgs e) {
            base.OnClick(e);
            if (this.m_Role is InstructorModel) {
                this.Checked = !this.Checked;
                using (Synchronizer.Lock(this.m_Role.SyncRoot)) {
                    ((InstructorModel)this.m_Role).StudentNavigationType = this.m_StudentNavigationType;
                }
            }
        }
    }

    #endregion

    #region AcceptingStudentSubmissionsMenuItem

    /// <summary>
    /// Represents the menu option to enable or disable student submissions
    /// </summary>
    public class AcceptingStudentSubmissionsMenuItem : InstructorMenuItem {
        private readonly EventQueue.PropertyEventDispatcher m_HandleAcceptingSSChangedDispatcher;

        public AcceptingStudentSubmissionsMenuItem(ControlEventQueue dispatcher, PresenterModel model) : base(dispatcher, model) {
            this.Text = Strings.AcceptStudentSubmissions;
            this.m_HandleAcceptingSSChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandleAcceptingSSChanged));
            using (Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                //Listen to to changes in Accepting SS for the current role, if it is an InstructorModel
                if (this.m_Role is InstructorModel) {
                    using (Synchronizer.Lock(this.m_Model.Participant.Role.SyncRoot)) {
                        ((InstructorModel)this.m_Role).Changed["AcceptingStudentSubmissions"].Add(this.m_HandleAcceptingSSChangedDispatcher.Dispatcher);
                    }
                }
            }
            this.HandleAcceptingSSChanged(this, null);
        }

        protected override void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            try {
                if(disposing) {
                    if (this.m_Role is InstructorModel) {
                        ((InstructorModel)this.m_Role).Changed["AcceptingStudentSubmissions"].Remove(this.m_HandleAcceptingSSChangedDispatcher.Dispatcher);
                    }
                }
            } finally {
                base.Dispose(disposing);
            }
            this.m_Disposed = true;
        }

        protected override void HandleRoleChanged(object sender, PropertyEventArgs args) {
            //Unregister the previous role's handler, if needed
            if (this.m_Role is InstructorModel) {
                using (Synchronizer.Lock(this.m_Role.SyncRoot)) {
                    ((InstructorModel)this.m_Role).Changed["AcceptingStudentSubmissions"].Remove(this.m_HandleAcceptingSSChangedDispatcher.Dispatcher);
                }
            }
            //Update the local copy
            using (Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                this.m_Role = this.m_Model.Participant.Role;
                //Register new handlers, if needed
                if (this.m_Role is InstructorModel) {
                    ((InstructorModel)this.m_Role).Changed["AcceptingStudentSubmissions"].Add(this.m_HandleAcceptingSSChangedDispatcher.Dispatcher);
                    }
            }
        }

        private void HandleAcceptingSSChanged(object sender, PropertyEventArgs args) {
            if (this.m_Role is InstructorModel) {
                using (Synchronizer.Lock(this.m_Role.SyncRoot)) {
                    this.Checked = ((InstructorModel)this.m_Role).AcceptingStudentSubmissions;
                }
            }
        }

        protected override void OnClick(EventArgs e) {
            base.OnClick (e);
            if (this.m_Role is InstructorModel) {
                this.Checked = ! this.Checked;
                using (Synchronizer.Lock(this.m_Role.SyncRoot)) {
                    ((InstructorModel)this.m_Role).AcceptingStudentSubmissions = this.Checked;
                }
            }
        }
    }

    #endregion

    #region AcceptingQuickPollSubmissionsMenuItem

    /// <summary>
    /// Menu item that controls whether or not there is currently a QuickPoll being conducted
    /// </summary>
    public class AcceptingQuickPollSubmissionsMenuItem : InstructorMenuItem, DeckTraversalModelAdapter.IAdaptee {
        #region Private Members

        /// <summary>
        /// Event dispatcher for when the AcceptingQuickPollSubmissions property changes
        /// </summary>
        private readonly EventQueue.PropertyEventDispatcher m_HandleAcceptingQPChangedDispatcher;
        /// <summary>
        /// The adapter to keep the workspace in sync
        /// </summary>
        private WorkspaceModelAdapter m_Adapter;
        /// <summary>
        /// The slide to listen to
        /// </summary>
        private SlideModel m_Slide;

        #endregion

        #region Constructors

        /// <summary>
        /// Constructor for this menu item
        /// </summary>
        /// <param name="dispatcher">The event dispatcher</param>
        /// <param name="model">The model to work with</param>
        public AcceptingQuickPollSubmissionsMenuItem( ControlEventQueue dispatcher, PresenterModel model )
            : base( dispatcher, model ) {
            this.Text = Strings.EnableQuickPolling;
            this.m_HandleAcceptingQPChangedDispatcher = new EventQueue.PropertyEventDispatcher( this.m_EventQueue, new PropertyEventHandler( this.HandleAcceptingQPChanged ) );
            using( Synchronizer.Lock( this.m_Model.Participant.SyncRoot ) ) {
                //Listen to to changes in Accepting QP for the current role, if it is an InstructorModel
                if( this.m_Role is InstructorModel ) {
                    using( Synchronizer.Lock( this.m_Model.Participant.Role.SyncRoot ) ) {
                        ((InstructorModel)this.m_Role).Changed["AcceptingQuickPollSubmissions"].Add( this.m_HandleAcceptingQPChangedDispatcher.Dispatcher );
                    }
                }
            }
            this.HandleAcceptingQPChanged( this, null );

            // Enable or disable based on there being a valid slide
            this.Enabled = false;
            this.m_Adapter = new WorkspaceModelAdapter( this.m_EventQueue, this, this.m_Model );
        }

        #endregion

        #region Disposing

        /// <summary>
        /// Dispose of this control
        /// </summary>
        /// <param name="disposing">True if we are in the middle of disposing the object, false otherwise</param>
        protected override void Dispose( bool disposing ) {
            if( this.m_Disposed ) return;
            try {
                if( disposing ) {
                    if( this.m_Role is InstructorModel ) {
                        ((InstructorModel)this.m_Role).Changed["AcceptingQuickPollSubmissions"].Remove( this.m_HandleAcceptingQPChangedDispatcher.Dispatcher );
                    }
                    this.m_Adapter.Dispose();
                }
            } finally {
                base.Dispose( disposing );
            }
            this.m_Disposed = true;
        }

        #endregion

        #region IAdaptee Members

        /// <summary>
        /// Updates the current slide
        /// </summary>
        public SlideModel Slide {
            set {
                using( Synchronizer.Lock( this ) ) {
                    this.m_Slide = value;

                    if( this.m_Slide == null ) {
                        this.Enabled = false;
                    } else {
                        this.Enabled = true;
                    }
                }
            }
        }

        //Unused IAdaptee member
        public System.Drawing.Color DefaultDeckBGColor {
            set { }
        }
        UW.ClassroomPresenter.Model.Background.BackgroundTemplate DeckTraversalModelAdapter.IAdaptee.DefaultDeckBGTemplate {
            set { }
        }
        #endregion

        #region Event Handlers

        /// <summary>
        /// Handle the Role properties changing
        /// </summary>
        /// <param name="sender">The event sender</param>
        /// <param name="args">The arguments</param>
        protected override void HandleRoleChanged( object sender, PropertyEventArgs args ) {
            //Unregister the previous role's handler, if needed
            if( this.m_Role is InstructorModel ) {
                using( Synchronizer.Lock( this.m_Role.SyncRoot ) ) {
                    ((InstructorModel)this.m_Role).Changed["AcceptingQuickPollSubmissions"].Remove( this.m_HandleAcceptingQPChangedDispatcher.Dispatcher );
                }
            }
            //Update the local copy
            using( Synchronizer.Lock( this.m_Model.Participant.SyncRoot ) ) {
                this.m_Role = this.m_Model.Participant.Role;
                //Register new handlers, if needed
                if( this.m_Role is InstructorModel ) {
                    ((InstructorModel)this.m_Role).Changed["AcceptingQuickPollSubmissions"].Add( this.m_HandleAcceptingQPChangedDispatcher.Dispatcher );
                }
            }
        }

        /// <summary>
        /// Handle the InstructorModel properties changing
        /// </summary>
        /// <param name="sender">The event sender</param>
        /// <param name="args">The arguments</param>
        private void HandleAcceptingQPChanged( object sender, PropertyEventArgs args ) {
            if( this.m_Role is InstructorModel ) {
                using( Synchronizer.Lock( this.m_Role.SyncRoot ) ) {
                    this.Checked = ((InstructorModel)this.m_Role).AcceptingQuickPollSubmissions;
                }
            }
        }

        /// <summary>
        /// Handle this menu item being selected
        /// </summary>
        /// <param name="e">The event arguments</param>
        protected override void OnClick( EventArgs e ) {
            if( !this.Enabled ) return;

            if( this.m_Role is InstructorModel ) {
                // Update the checked state
                this.Checked = !this.Checked;

                // Start or end the QuickPoll as needed
                if( this.Checked ) {
                    AcceptingQuickPollSubmissionsMenuItem.CreateNewQuickPoll( this.m_Model, this.m_Role );
                } else {
                    AcceptingQuickPollSubmissionsMenuItem.EndQuickPoll( this.m_Model );
                }

                // Set the AcceptQuickPollSubmission state
                using( Synchronizer.Lock( this.m_Role.SyncRoot ) ) {
                    ((InstructorModel)this.m_Role).AcceptingQuickPollSubmissions = this.Checked;
                }
            }

            base.OnClick( e );
        }

        #endregion

        #region Static QuickPoll Methods

        // TODO: Figure out what slide bounds should be, merge with FilmStripContextMenu.NewSlideMenuItem,
        //   and allow the aspect ratio to be user-customizable.
        public static readonly System.Drawing.Rectangle DEFAULT_SLIDE_BOUNDS = new System.Drawing.Rectangle( 0, 0, 720, 540 );

        /// <summary>
        /// Creates a new QuickPoll and the associated slide (and deck)
        /// </summary>
        /// <param name="model">The PresenterModel</param>
        /// <param name="role">The RoleModel</param>
        public static void CreateNewQuickPoll( PresenterModel model, RoleModel role )
        {
            // Create the quickpoll
            QuickPollModel newQuickPoll = null;
            using( Synchronizer.Lock( model.ViewerState.SyncRoot ) ) {
                newQuickPoll = new QuickPollModel( Guid.NewGuid(), Guid.NewGuid(), model.ViewerState.PollStyle );
            }

            // Get the Current Slide
            SlideModel oldSlide;
            using (Synchronizer.Lock(role.SyncRoot)) {
                using (Synchronizer.Lock(((InstructorModel)role).CurrentDeckTraversal.SyncRoot)) {
                    using (Synchronizer.Lock(((InstructorModel)role).CurrentDeckTraversal.Current.SyncRoot)) {
                        oldSlide = ((InstructorModel)role).CurrentDeckTraversal.Current.Slide;
                    }
                }
            }

            // Add a new QuickPoll to the model
            // NOTE: This should trigger a network message about the new QuickPoll
            // NOTE: Need to do this first before adding the sheet otherwise the 
            //       public display will not be able to associate the sheet with
            //       this quick poll.
            using( model.Workspace.Lock() ) {
                using( Synchronizer.Lock( (~model.Workspace.CurrentPresentation).SyncRoot ) ) {
                    (~model.Workspace.CurrentPresentation).QuickPoll = newQuickPoll;
                }
            }

            // Add the quickpoll slide to the quickpoll
            using( model.Workspace.Lock() ) {
                using( Synchronizer.Lock( model.Participant.SyncRoot ) ) {
                    DeckTraversalModel qpTraversal = null;
                    DeckModel qpDeck = null;

                    // Find the first quickpoll slidedeck.
                    foreach( DeckTraversalModel candidate in model.Workspace.DeckTraversals ) {
                        if( (candidate.Deck.Disposition & DeckDisposition.QuickPoll) != 0 ) {
                            qpTraversal = candidate;
                            using( Synchronizer.Lock( qpTraversal.SyncRoot ) ) {
                                qpDeck = qpTraversal.Deck;
                            }
                            break;
                        }
                    }

                    // If there is no existing quickpoll deck, create one.
                    if( qpTraversal == null ) {
                        // Change the name of quickpoll according to the number of it
                        string qpName = "QuickPoll";
                        // NOTE: This code is duplicated in DecksMenu.CreateBlankWhiteboardDeckMenuItem.
                        qpDeck = new DeckModel( Guid.NewGuid(), DeckDisposition.QuickPoll, qpName );
                        qpDeck.Group = Network.Groups.Group.Submissions;
                        qpTraversal = new SlideDeckTraversalModel( Guid.NewGuid(), qpDeck );

                        if( model.Workspace.CurrentPresentation.Value != null ) {
                            using( Synchronizer.Lock( (~model.Workspace.CurrentPresentation).SyncRoot ) ) {
                                (~model.Workspace.CurrentPresentation).DeckTraversals.Add( qpTraversal );
                            }
                        } else {
                            model.Workspace.DeckTraversals.Add( qpTraversal );
                        }
                    }

                    // Add the slide
                    // TODO CMPRINCE: Associate the quickpoll with this slide
                    using( Synchronizer.Lock( qpDeck.SyncRoot ) ) {
                        // Copy the values and sheets from the old slide to the new slide
                        // Get oldSlide's Guid
                        Guid oldId = Guid.Empty;
                        using( Synchronizer.Lock( oldSlide.SyncRoot ) ) {
                            oldId = oldSlide.Id;
                        }
                        // Create the new slide to add
                        SlideModel newSlide = new SlideModel(Guid.NewGuid(), new LocalId(), SlideDisposition.Remote | SlideDisposition.StudentSubmission, DEFAULT_SLIDE_BOUNDS, oldId);

                        // Make a list of image content sheets that need to be added to the deck.
                        List<ImageSheetModel> images = new List<ImageSheetModel>();
                        
                        // Update the fields of the slide
                        using( Synchronizer.Lock( oldSlide.SyncRoot ) ) {                        
                            using( Synchronizer.Lock( newSlide.SyncRoot ) ) {
                                newSlide.Title = oldSlide.Title;
                                newSlide.Bounds = oldSlide.Bounds;
                                newSlide.Zoom = oldSlide.Zoom;
                                newSlide.BackgroundColor = oldSlide.BackgroundColor;
                                newSlide.SubmissionSlideGuid = oldSlide.SubmissionSlideGuid;
                                newSlide.SubmissionStyle = oldSlide.SubmissionStyle;

                                // Copy all of the content sheets.
                                // Because ContentSheets do not change, there is no 
                                // need to do a deep copy (special case for ImageSheetModels).
                                foreach( SheetModel s in oldSlide.ContentSheets ) {
                                    newSlide.ContentSheets.Add( s );

                                    // Queue up any image content to be added the deck below.
                                    ImageSheetModel ism = s as ImageSheetModel;
                                    if( ism != null )
                                        images.Add( ism );
                                }

                                // Add the QuickPollSheet
                                newSlide.ContentSheets.Add( new QuickPollSheetModel( Guid.NewGuid(), newQuickPoll ) );

                                // Make a deep copy of all the ink sheets
                                foreach( SheetModel s in oldSlide.AnnotationSheets ) {
                                    SheetModel newSheet = SheetModel.SheetDeepCopyHelper( s );
                                    newSlide.AnnotationSheets.Add( newSheet );

                                    // Queue up any image content to be added the deck below.
                                    ImageSheetModel ism = s as ImageSheetModel;
                                    if( ism != null )
                                        images.Add( ism );
                                }
                            }
                        }

                            // Add the slide content to the deck.
                            foreach( ImageSheetModel ism in images ) {
                                System.Drawing.Image image = ism.Image;
                                if( image == null )
                                    using( Synchronizer.Lock( ism.Deck.SyncRoot ) )
                                    using( Synchronizer.Lock( ism.SyncRoot ) )
                                        image = ism.Deck.GetSlideContent( ism.MD5 );
                                if( image != null )
                                    qpDeck.AddSlideContent( ism.MD5, image );
                            }

                            // Add the slide to the deck.
                            qpDeck.InsertSlide( newSlide );

                            // Add an entry to the deck traversal so that we can navigate to the slide
                            using( Synchronizer.Lock( qpDeck.TableOfContents.SyncRoot ) ) {
                                TableOfContentsModel.Entry e = new TableOfContentsModel.Entry( Guid.NewGuid(), qpDeck.TableOfContents, newSlide );
                                qpDeck.TableOfContents.Entries.Add( e );
                            }
                    }
                }
            }
        }

        /// <summary>
        /// End a quick poll
        /// </summary>
        /// <param name="model">The PresenterModel</param>
        public static void EndQuickPoll( PresenterModel model )
        {
            // Set the QuickPoll to null
            // NOTE: This should trigger a network message about the null QuickPoll
            using( model.Workspace.Lock() ) {
                using( Synchronizer.Lock( (~model.Workspace.CurrentPresentation).SyncRoot ) ) {
                    (~model.Workspace.CurrentPresentation).QuickPoll = null;
                }
            }
        }

        #endregion
    }

    #endregion

    #region StudentSubmisisonStyleMenu
/*    
    /// <summary>
    /// Student Submission Style Menu
    /// </summary>
    public class StudentSubmissionStyleMenu : MenuItem {
        private readonly PresenterModel m_Model;
        private readonly ControlEventQueue m_EventQueue;

        public StudentSubmissionStyleMenu(ControlEventQueue dispatcher, PresenterModel model) {
            this.m_Model = model;
            this.m_EventQueue = dispatcher;
            this.Text = "SS Style";

            //Listen to the Role and disable if it isn't an instructor
            this.MenuItems.Add( new StudentSubmissionStyleMenuItem( "Normal", SlideModel.StudentSubmissionStyle.Normal, this.m_EventQueue, this.m_Model ) );
            this.MenuItems.Add( new StudentSubmissionStyleMenuItem( "Shared", SlideModel.StudentSubmissionStyle.Shared, this.m_EventQueue, this.m_Model ) );
            this.MenuItems.Add( new StudentSubmissionStyleMenuItem( "Real-Time", SlideModel.StudentSubmissionStyle.Realtime, this.m_EventQueue, this.m_Model ) );

            using (Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                this.m_Model.Participant.Changed["Role"].Add(new PropertyEventHandler(this.HandleRoleChanged));
            }

            // Initialize the state of the menu items.
            this.HandleRoleChanged(this, null);
        }

        protected void HandleRoleChanged(object sender, PropertyEventArgs e) {
            // Bug 988: disable the SS Style menu in non-instructor roles.
            using (Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                this.Enabled = this.Visible = this.m_Model.Participant.Role is InstructorModel;
            }
        }

        protected override void Dispose(bool disposing) {
            if (disposing) {
                using (Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                    this.m_Model.Participant.Changed["Role"].Remove(new PropertyEventHandler(this.HandleRoleChanged));
                }
            }
            base.Dispose(disposing);
        }
    }
*/
    #endregion

    #region StudentSubmissionStyleMenuItem
/*
    /// <summary>
    /// Handle the changing of student submission styles
    /// Uses the DeckTraversalModelAdapter class to keep the SlideModel in sync with the UI
    /// </summary>
    public class StudentSubmissionStyleMenuItem : InstructorMenuItem, DeckTraversalModelAdapter.IAdaptee {
        private readonly EventQueue.PropertyEventDispatcher m_HandleStudentSubmissionStyleChangedDispatcher;
        private WorkspaceModelAdapter m_Adapter;
        private SlideModel m_Slide;
        private SlideModel.StudentSubmissionStyle m_Style;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="text">The text of this menu item</param>
        /// <param name="style">The submission style that this item represents</param>
        /// <param name="dispatcher">The event displatcher for this menu item</param>
        /// <param name="model">The presentation model</param>
        public StudentSubmissionStyleMenuItem( string text, SlideModel.StudentSubmissionStyle style, ControlEventQueue dispatcher, PresenterModel model )
            : base( dispatcher, model ) {

            this.m_Adapter = new WorkspaceModelAdapter( dispatcher, this, model );
            this.Text = text;
            this.m_Style = style;
            this.m_HandleStudentSubmissionStyleChangedDispatcher = new EventQueue.PropertyEventDispatcher( this.m_EventQueue, new PropertyEventHandler( this.HandleStudentSubmissionStyleChanged ) );
            this.HandleStudentSubmissionStyleChanged( this, null );
        }

        /// <summary>
        /// The Current slide model, as kept in sync by the workspace adapter
        /// </summary>
        public SlideModel Slide {
            set {
                using( Synchronizer.Lock( this ) ) {
                    if( this.m_Slide != null ) {
                        this.m_Slide.Changed["SubmissionStyle"].Remove( this.m_HandleStudentSubmissionStyleChangedDispatcher.Dispatcher );
                    }

                    this.m_Slide = value;

                    if( this.m_Slide != null ) {
                        this.m_Slide.Changed["SubmissionStyle"].Add( this.m_HandleStudentSubmissionStyleChangedDispatcher.Dispatcher );
                    }

                    this.m_HandleStudentSubmissionStyleChangedDispatcher.Dispatcher( this, null );
                }
            }
        }

        //Unused IAdaptee member
        public System.Drawing.Color DefaultDeckBGColor {
            set { }
        }

        // Unused InstructorMenuItem member
        protected override void HandleRoleChanged( object sender, PropertyEventArgs args ) {
        }

        /// <summary>
        /// Disposes of all resources, currently just disposes of the adapter
        /// </summary>
        /// <param name="disposing">True if we are in the process of disposing, false otherwise</param>
        protected override void Dispose( bool disposing ) {
            if( this.m_Disposed ) return;
            try {
                if( disposing ) {
                    this.m_Adapter.Dispose();
                }
            } finally {
                base.Dispose( disposing );
            }
            this.m_Disposed = true;
        }

        /// <summary>
        /// Update the UI because the value of the SubmissionStyle has changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void HandleStudentSubmissionStyleChanged( object sender, PropertyEventArgs args ) {
            if( this.m_Role is InstructorModel && this.m_Slide != null ) {
                using( Synchronizer.Lock( this.m_Slide.SyncRoot ) ) {
                    this.Checked = (this.m_Slide.SubmissionStyle == this.m_Style);
                }
            } else
                this.Checked = false;
        }

        /// <summary>
        /// Update the submission style of the slide because the menu item was clicked
        /// </summary>
        /// <param name="e"></param>
        protected override void OnClick( EventArgs e ) {
            base.OnClick( e );
            if( this.m_Role is InstructorModel && this.m_Slide != null ) {
                using( Synchronizer.Lock( this.m_Slide.SyncRoot ) ) {
                    this.Checked = !this.Checked;
                    this.m_Slide.SubmissionStyle = this.m_Style;
                }
            }
        }
    }
*/
    #endregion*/
}
