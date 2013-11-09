// $Id: InstructorToolBarButtons.cs 1787 2008-10-09 23:17:16Z lining $

using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using System.Collections.Generic;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Model.Viewer;
using UW.ClassroomPresenter.Viewer.Slides;
using UW.ClassroomPresenter.Viewer.Menus;

namespace UW.ClassroomPresenter.Viewer.ToolBars {
    /// <summary>
    /// Builds the instructor ToolBar buttons
    /// </summary>
    public class InstructorToolBarButtons {
        /// <summary>
        /// The presenter model
        /// </summary>
        private readonly PresenterModel m_Model;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="model">The presenter model</param>
        public InstructorToolBarButtons(PresenterModel model) {
            this.m_Model = model;
        }

        /// <summary>
        /// Make the student submission button and linked nav button
        /// </summary>
        /// <param name="parent">The parent ToolStrip</param>
        /// <param name="dispatcher">The event queue</param>
        public void MakeButtons(ToolStrip parent, ControlEventQueue dispatcher) {
            ParticipantToolBarButton qp, nav;

            //ParticipantToolBarButton ss
            //ss = new AcceptingStudentSubmissionsToolBarButton( dispatcher, this.m_Model );
            //ss.AutoSize = false;
            //ss.Width = 68;
            //ss.Height = 44;

            nav = new ForcingStudentNavigationLockToolBarButton(dispatcher, this.m_Model);
            nav.Image = UW.ClassroomPresenter.Properties.Resources.linked;
            nav.AutoSize = true;

            qp = new QuickPollToolBarButton( dispatcher, this.m_Model );
            nav.AutoSize = true;

            //parent.Items.Add(ss);
            parent.Items.Add(qp);
            parent.Items.Add(new ToolStripSeparator());
            parent.Items.Add(nav);
  
        }

        /// <summary>
        /// Make the student submission button and linked nav button
        /// </summary>
        /// <param name="main">The main ToolStrip</param>
        /// <param name="main">The extra ToolStrip</param>
        /// <param name="dispatcher">The event queue</param>
        public void MakeButtons(ToolStrip main, ToolStrip extra, ControlEventQueue dispatcher) {
            //ParticipantToolBarButton ss, qp, nav;
            ParticipantToolBarButton qp, nav;

            //ss = new AcceptingStudentSubmissionsToolBarButton( dispatcher, this.m_Model );
            //ss.AutoSize = false;
            //ss.Width = 54;
            //ss.Height = 44;

            nav = new ForcingStudentNavigationLockToolBarButton(dispatcher, this.m_Model);
            nav.Image = UW.ClassroomPresenter.Properties.Resources.linked;
            nav.AutoSize = false;
            nav.Width = 54;
            nav.Height = 44;

            qp = new QuickPollToolBarButton( dispatcher, this.m_Model );

            using(Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                if(this.m_Model.Participant.Role is InstructorModel) {
                    main.Items.Add(new ToolStripSeparator());
                    extra.Items.Add(new ToolStripSeparator());
                }
            }
            //main.Items.Add(ss);
            //main.Items.Add(qp);
            main.Items.Add(nav);
            using(Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                if(this.m_Model.Participant.Role is InstructorModel) {
                    main.Items.Add(new ToolStripSeparator());
                    extra.Items.Add(new ToolStripSeparator());
                }
            }
            
            //main.Items.Add(new ToolStripSeparator());
        }

        #region ParticipantToolBarButton

        /// <summary>
        /// A utility class which keeps its abstract <see cref="Participant"/> property in
        /// sync with that of the associated <see cref="PresenterModel"/>.
        /// Subclasses can define their own setter for the <c>Presenter</c> property and
        /// handle changes as they wish.
        /// </summary>
        private abstract class ParticipantToolBarButton : ToolStripButton {
            /// <summary>
            /// The event queue
            /// </summary>
            protected readonly ControlEventQueue m_EventQueue;
            /// <summary>
            /// The role change dispatcher
            /// </summary>
            private readonly EventQueue.PropertyEventDispatcher m_RoleChangedDispatcher;

            /// <summary>
            /// The presenter model
            /// </summary>
            protected readonly PresenterModel m_Model;
            /// <summary>
            /// True when disposed
            /// </summary>
            private bool m_Disposed;

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="dispatcher">The event queue</param>
            /// <param name="model">The model</param>
            protected ParticipantToolBarButton(ControlEventQueue dispatcher, PresenterModel model) {
                this.m_EventQueue = dispatcher;
                this.m_Model = model;

                this.m_RoleChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandleRoleChanged));
                this.m_Model.Participant.Changed["Role"].Add(this.m_RoleChangedDispatcher.Dispatcher);
            }

            /// <summary>
            /// Releases all resources
            /// </summary>
            /// <param name="disposing">True if truely disposing</param>
            protected override void Dispose(bool disposing) {
                if(this.m_Disposed) return;
                try {
                    if(disposing) {
                        this.m_Model.Participant.Changed["Role"].Remove(this.m_RoleChangedDispatcher.Dispatcher);
                    }
                } finally {
                    base.Dispose(disposing);
                }
                this.m_Disposed = true;
            }

            /// <summary>
            /// Get and set the role
            /// </summary>
            protected abstract RoleModel Role { get; set; }

            /// <summary>
            /// Handle the role changing
            /// </summary>
            /// <param name="sender">The event sender</param>
            /// <param name="args">The arguments</param>
            private void HandleRoleChanged(object sender, PropertyEventArgs args) {
                this.InitializeRole();
            }

            /// <summary>
            /// Sets the <see cref="Role"/> property to the current
            /// <see cref="ParticipantModel.Role">Role</see> of
            /// the <see cref="ParticipantModel"/> of the associated <see cref="PresenterModel"/>.
            /// </summary>
            /// <remarks>
            /// This method should be called once from a subclass's constructor,
            /// and may be called any number of times thereafter.
            /// <para>
            /// This method should be called from the <see cref="m_EventQueue"/> or
            /// the control's creator thread.
            /// </para>
            /// </remarks>
            protected void InitializeRole() {
                using(Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                    this.Role = this.m_Model.Participant.Role;
                    this.Visible = (this.m_Model.Participant.Role is InstructorModel);
                }
            }
        }

        #endregion

        #region AcceptionStudentSubmissionsToolBarButton

        /// <summary>
        /// This button enables and disables the sending of student submissions to the instructor
        /// </summary>
        private class AcceptingStudentSubmissionsToolBarButton : ParticipantToolBarButton {
            /// <summary>
            /// The listener for changing of the accept student submissions property
            /// </summary>
            private readonly EventQueue.PropertyEventDispatcher m_AcceptingStudentSubmissionsChangedDispatcher;
            /// <summary>
            /// The current role
            /// </summary>
            private RoleModel m_Role;
            /// <summary>
            /// True if disposed
            /// </summary>
            private bool m_Disposed;

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="dispatcher">The event queue</param>
            /// <param name="model">The model</param>
            public AcceptingStudentSubmissionsToolBarButton(ControlEventQueue dispatcher, PresenterModel model) : base(dispatcher, model) {
                this.m_AcceptingStudentSubmissionsChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue,
                    new PropertyEventHandler(this.HandleAcceptingStudentSubmissionsChanged));
                base.InitializeRole();
            }

            /// <summary>
            /// Release all resources
            /// </summary>
            /// <param name="disposing">True if truely disposing</param>
            protected override void Dispose(bool disposing) {
                if(this.m_Disposed) return;
                try {
                    if(disposing) {
                        // Unregister event listeners via the Role setter.
                        this.Role = null;
                    }
                } finally {
                    base.Dispose(disposing);
                }
                this.m_Disposed = true;
            }

            /// <summary>
            /// Handle the role changing
            /// </summary>
            protected override RoleModel Role {
                get { return this.m_Role; }
                set {
                    if(this.m_Role is InstructorModel) {
                        this.m_Role.Changed["AcceptingStudentSubmissions"].Remove(this.m_AcceptingStudentSubmissionsChangedDispatcher.Dispatcher);
                    }

                    this.m_Role = value;

                    if(this.m_Role is InstructorModel) {
                        this.m_Role.Changed["AcceptingStudentSubmissions"].Add(this.m_AcceptingStudentSubmissionsChangedDispatcher.Dispatcher);
                    }

                    this.m_AcceptingStudentSubmissionsChangedDispatcher.Dispatcher(value, null);
                }
            }

            /// <summary>
            /// Handle clicking on the button
            /// </summary>
            /// <param name="args">The event args</param>
            protected override void OnClick( EventArgs args ) {
                if( this.Role is InstructorModel ) {
                    using( Synchronizer.Lock( this.Role.SyncRoot ) ) {
                        this.Checked = !this.Checked;
                        ((InstructorModel)this.Role).AcceptingStudentSubmissions = this.Checked;
                    }
                }
                base.OnClick( args );
            }

            /// <summary>
            /// Handle the student submission setting changed
            /// </summary>
            /// <param name="sender">The event sender</param>
            /// <param name="args">The event args</param>
            private void HandleAcceptingStudentSubmissionsChanged(object sender, PropertyEventArgs args) {
                bool enable, push;

                if(this.Role is InstructorModel) {
                    using(Synchronizer.Lock(this.Role.SyncRoot)) {
                        // If the participant *is* an Instructor, indicate its *current* state.
                        enable = true;
                        push = ((InstructorModel) this.Role).AcceptingStudentSubmissions;
                    }
                } else {
                    enable = push = false;
                }

                this.Enabled = enable;
                this.Checked = push;

                this.Image = (push)
                    ? UW.ClassroomPresenter.Properties.Resources.submission_on
                    : UW.ClassroomPresenter.Properties.Resources.submission_off;

                this.ToolTipText = (push)
                    ? Strings.DisableStudentSubmissions
                    : Strings.EnableStudentSubmissions;
            }
        }

        #endregion

        #region QuickPollToolBarButton

        /// <summary>
        /// This button enables and disables the the QuickPoll for the instructor
        /// </summary>
        private class QuickPollToolBarButton : ParticipantToolBarButton, DeckTraversalModelAdapter.IAdaptee {
            /// <summary>
            /// The listener for changing of the quickpoll
            /// </summary>
            private readonly EventQueue.PropertyEventDispatcher m_QuickPollChangedDispatcher;
            /// <summary>
            /// The adapter to keep the workspace in sync
            /// </summary>
            private WorkspaceModelAdapter m_Adapter;
            /// <summary>
            /// The slide to modify
            /// </summary>
            private SlideModel m_Slide;
            /// <summary>
            /// The current role
            /// </summary>
            private RoleModel m_Role;
            /// <summary>
            /// True if disposed
            /// </summary>
            private bool m_Disposed;

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="dispatcher">The event queue</param>
            /// <param name="model">The model</param>
            public QuickPollToolBarButton( ControlEventQueue dispatcher, PresenterModel model )
                : base( dispatcher, model ) {
                this.m_QuickPollChangedDispatcher = new EventQueue.PropertyEventDispatcher( this.m_EventQueue,
                    new PropertyEventHandler( this.HandleQuickPollChanged ) );
                base.InitializeRole();

                this.Enabled = false;
                this.m_Adapter = new WorkspaceModelAdapter( this.m_EventQueue, this, this.m_Model );
            }

            /// <summary>
            /// Release all resources
            /// </summary>
            /// <param name="disposing">True if truely disposing</param>
            protected override void Dispose( bool disposing ) {
                if( this.m_Disposed ) return;
                try {
                    if( disposing ) {
                        // Unregister event listeners via the Role setter.
                        this.Role = null;
                        this.m_Adapter.Dispose();
                    }
                } finally {
                    base.Dispose( disposing );
                }
                this.m_Disposed = true;
            }

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
            public UW.ClassroomPresenter.Model.Background.BackgroundTemplate DefaultDeckBGTemplate {
                set { }
            }

            /// <summary>
            /// Handle the role changing
            /// </summary>
            protected override RoleModel Role {
                get { return this.m_Role; }
                set {
                    if( this.m_Role is InstructorModel ) {
                        this.m_Role.Changed["AcceptingQuickPollSubmissions"].Remove( this.m_QuickPollChangedDispatcher.Dispatcher );
                    }

                    this.m_Role = value;

                    if( this.m_Role is InstructorModel ) {
                        this.m_Role.Changed["AcceptingQuickPollSubmissions"].Add( this.m_QuickPollChangedDispatcher.Dispatcher );
                    }

                    this.m_QuickPollChangedDispatcher.Dispatcher( value, null );
                }
            }

            /// <summary>
            /// Handle clicking on the button
            /// </summary>
            /// <param name="args">The event args</param>
            protected override void OnClick( EventArgs args ) {
                if( !this.Enabled ) return;

                this.Checked = !this.Checked;

                // Add a slide to the quickpoll deck containing a new slide for this quickpoll
                if( this.Checked ) {
                    AcceptingQuickPollSubmissionsMenuItem.CreateNewQuickPoll( this.m_Model, this.m_Role );
                } else {
                    AcceptingQuickPollSubmissionsMenuItem.EndQuickPoll( this.m_Model );
                }

                // Handle changing the value of whether a quickpoll is enabled or not
                // NOTE: This should trigger a network message about the value changing
                if( this.Role is InstructorModel ) {
                    using( Synchronizer.Lock( this.Role.SyncRoot ) ) {
                        ((InstructorModel)this.Role).AcceptingQuickPollSubmissions = this.Checked;
                    }
                }

                base.OnClick( args );
            }

            /// <summary>
            /// Handle the student submission setting changed
            /// </summary>
            /// <param name="sender">The event sender</param>
            /// <param name="args">The event args</param>
            private void HandleQuickPollChanged( object sender, PropertyEventArgs args ) {
                bool push;

                if( this.Role is InstructorModel ) {
                    using( Synchronizer.Lock( this.Role.SyncRoot ) ) {
                        // If the participant *is* an Instructor, indicate its *current* state.
                        push = ((InstructorModel)this.Role).AcceptingQuickPollSubmissions;
                    }
                } else {
                    push = false;
                }

                this.Checked = push;

                this.Image = (push)
                    ? UW.ClassroomPresenter.Properties.Resources.quickpoll_off
                    : UW.ClassroomPresenter.Properties.Resources.quickpoll_on;

                this.ToolTipText = (push)
                    ? Strings.EndQuickPoll
                    : Strings.StartQuickPoll;
            }
        }

        #endregion

        #region ForcingStudentNavigationLockToolBarButton

        /// <summary>
        /// Button that enables and disables the ability for students to freely navigate slides
        /// </summary>
        private class ForcingStudentNavigationLockToolBarButton : ParticipantToolBarButton {
            /// <summary>
            /// The property listener
            /// </summary>
            private readonly EventQueue.PropertyEventDispatcher m_ForcingStudentNavigationLockChangedDispatcher;
            /// <summary>
            /// The current role
            /// </summary>
            private RoleModel m_Role;
            /// <summary>
            /// True if disposed
            /// </summary>
            private bool m_Disposed;

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="dispatcher">The event queue</param>
            /// <param name="model">The model</param>
            public ForcingStudentNavigationLockToolBarButton(ControlEventQueue dispatcher, PresenterModel model) : base(dispatcher, model) {
                this.m_ForcingStudentNavigationLockChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue,
                    new PropertyEventHandler(this.HandleForcingStudentNavigationLockChanged));
                base.InitializeRole();
            }

            /// <summary>
            /// Release all resources
            /// </summary>
            /// <param name="disposing">True if truely disposing</param>
            protected override void Dispose(bool disposing) {
                if(this.m_Disposed) return;
                try {
                    if(disposing) {
                        // Unregister event listeners via the Role setter.
                        this.Role = null;
                    }
                } finally {
                    base.Dispose(disposing);
                }
                this.m_Disposed = true;
            }

            /// <summary>
            /// Handle changes to the role
            /// </summary>
            protected override RoleModel Role {
                get { return this.m_Role; }
                set {
                    if(this.m_Role is InstructorModel) {
                        this.m_Role.Changed["ForcingStudentNavigationLock"].Remove(this.m_ForcingStudentNavigationLockChangedDispatcher.Dispatcher);
                    }

                    this.m_Role = value;

                    if(this.m_Role is InstructorModel) {
                        this.m_Role.Changed["ForcingStudentNavigationLock"].Add(this.m_ForcingStudentNavigationLockChangedDispatcher.Dispatcher);
                    }

                    this.m_ForcingStudentNavigationLockChangedDispatcher.Dispatcher(value, null);
                }
            }

            /// <summary>
            /// Handle the button being pressed
            /// </summary>
            /// <param name="args">The event args</param>
            protected override void OnClick( EventArgs args ) {
                if( this.Role is InstructorModel ) {
                    using( Synchronizer.Lock( this.Role.SyncRoot ) ) {
                        this.Checked = !this.Checked;
                        ((InstructorModel)this.Role).ForcingStudentNavigationLock = this.Checked;
                    }
                }
                base.OnClick( args );
            }

            /// <summary>
            /// Handle the property changing
            /// </summary>
            /// <param name="sender">The event sender</param>
            /// <param name="args">The event args</param>
            private void HandleForcingStudentNavigationLockChanged(object sender, PropertyEventArgs args) {
                bool enable, push;

                if(this.Role is InstructorModel) {
                    using(Synchronizer.Lock(this.Role.SyncRoot)) {
                        // If the participant *is* an Instructor, indicate its *current* state.
                        enable = true;
                        push = ((InstructorModel) this.Role).ForcingStudentNavigationLock;
                    }
                } else {
                    enable = push = false;
                }

                this.Enabled = enable;
                this.Checked = push;

                this.ToolTipText = (push)
                    ? Strings.EnableStudentNavigation
                    : Strings.DisableStudentNavigation;
            }
        }

        #endregion
    }
}
