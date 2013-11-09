using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Network.Messages.Presentation;

namespace UW.ClassroomPresenter.Viewer.ToolBars {
    /// <summary>
    /// Represents the tool bar buttons that are associated with the student view
    /// Namely, the student submissions button.
    /// </summary>
    public class StudentToolBarButtons {
        /// <summary>
        /// The link to the PresenterModel modified by this class
        /// </summary>
        private readonly PresenterModel m_Model;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="model">The PresenterModel object to associate with these buttons</param>
        public StudentToolBarButtons(PresenterModel model) {
            this.m_Model = model;
        }
        /// <summary>
        /// Make all the buttons for this tool bar and add them to the bar
        /// </summary>
        /// <param name="parent">The toolbar to add the button to</param>
        /// <param name="dispatcher">The event queue to dispatch message onto</param>
        public void MakeButtons(ToolStrip parent, ControlEventQueue dispatcher) {
            ParticipantToolBarButton submit;

            submit = new SubmitStudentSubmissionToolBarButton( dispatcher, this.m_Model );
            submit.AutoSize = false;
            submit.Width = 68;
            submit.Height = 44;
            submit.Image = UW.ClassroomPresenter.Properties.Resources.submission;

            parent.Items.Add( submit );

            parent.Items.Add( new StudentQuickPollToolBarButton( dispatcher, this.m_Model, Strings.QuickPollYes, "Yes" ) );
            parent.Items.Add( new StudentQuickPollToolBarButton( dispatcher, this.m_Model, Strings.QuickPollNo, "No" ) );
            parent.Items.Add( new StudentQuickPollToolBarButton( dispatcher, this.m_Model, Strings.QuickPollBoth, "Both" ) );
            parent.Items.Add( new StudentQuickPollToolBarButton( dispatcher, this.m_Model, Strings.QuickPollNeither, "Neither" ) );
            parent.Items.Add( new StudentQuickPollToolBarButton( dispatcher, this.m_Model, "A", "A" ) );
            parent.Items.Add( new StudentQuickPollToolBarButton( dispatcher, this.m_Model, "B", "B" ) );
            parent.Items.Add( new StudentQuickPollToolBarButton( dispatcher, this.m_Model, "C", "C" ) );
            parent.Items.Add( new StudentQuickPollToolBarButton( dispatcher, this.m_Model, "D", "D" ) );
            parent.Items.Add( new StudentQuickPollToolBarButton( dispatcher, this.m_Model, "E", "E" ) );
        }

        /// <summary>
        /// Make all the buttons for this tool bar and add them to the bar
        /// </summary>
        /// <param name="main">The main toolbar to add the button to</param>
        /// <param name="extra">The extra toolbar to add the button to</param>
        /// <param name="dispatcher">The event queue to dispatch message onto</param>
        public void MakeButtons(ToolStrip main, ToolStrip extra, ControlEventQueue dispatcher) {
            ParticipantToolBarButton submit, yes, no, both, neither, a, b, c, d, e;

            submit = new SubmitStudentSubmissionToolBarButton( dispatcher, this.m_Model );
            submit.AutoSize = false;
            submit.Width = 54;
            submit.Height = 44;
            submit.Image = UW.ClassroomPresenter.Properties.Resources.submission;

            extra.Items.Add( submit );

            yes = new StudentQuickPollToolBarButton(dispatcher, this.m_Model, Strings.QuickPollYes, "Yes");
            yes.AutoSize = false;
            yes.Width = 54;
            yes.Height = 32;

            main.Items.Add( yes );

            no = new StudentQuickPollToolBarButton(dispatcher, this.m_Model, Strings.QuickPollNo, "No");
            no.AutoSize = false;
            no.Width = 54;
            no.Height = 32;

            main.Items.Add( no );

            both = new StudentQuickPollToolBarButton(dispatcher, this.m_Model, Strings.QuickPollBoth, "Both");
            both.AutoSize = false;
            both.Width = 54;
            both.Height = 32;

            main.Items.Add( both );

            neither = new StudentQuickPollToolBarButton( dispatcher, this.m_Model, Strings.QuickPollNeither, "Neither" );
            neither.AutoSize = false;
            neither.Width = 54;
            neither.Height = 32;

            main.Items.Add( neither );

            a = new StudentQuickPollToolBarButton(dispatcher, this.m_Model, "A", "A");
            a.AutoSize = false;
            a.Width = 54;
            a.Height = 18;

            extra.Items.Add( a );

            b = new StudentQuickPollToolBarButton(dispatcher, this.m_Model, "B", "B");
            b.AutoSize = false;
            b.Width = 54;
            b.Height = 18;

            extra.Items.Add( b );

            c = new StudentQuickPollToolBarButton(dispatcher, this.m_Model, "C", "C");
            c.AutoSize = false;
            c.Width = 54;
            c.Height = 18;

            extra.Items.Add( c );

            d = new StudentQuickPollToolBarButton(dispatcher, this.m_Model, "D", "D");
            d.AutoSize = false;
            d.Width = 54;
            d.Height = 18;

            extra.Items.Add( d );

            e = new StudentQuickPollToolBarButton(dispatcher, this.m_Model, "E", "E");
            e.AutoSize = false;
            e.Width = 54;
            e.Height = 18;

            extra.Items.Add( e );
        }

        #region ParticipantToolBarButton class

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
            /// The role changed dispatcher
            /// </summary>
            private readonly EventQueue.PropertyEventDispatcher m_RoleChangedDispatcher;

            /// <summary>
            /// The presenter model
            /// </summary>
            private readonly PresenterModel m_Model;
            /// <summary>
            /// True if disposed
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
            /// Handles changes to the role
            /// </summary>
            protected abstract RoleModel Role { get; set; }

            /// <summary>
            /// Handle changes to the role
            /// </summary>
            /// <param name="sender">The event sender</param>
            /// <param name="args">The event args</param>
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
                    this.Visible = (this.m_Model.Participant.Role is StudentModel);
                }
            }
        }

        #endregion

        #region SubmitStudentSubmissionToolBarButton class

        /// <summary>
        /// This class represents the button that submits the student submission 
        /// and ensures that the correct thing is displayed on the screen.
        /// </summary>
        private class SubmitStudentSubmissionToolBarButton : ParticipantToolBarButton {
            /// <summary>
            /// The current role
            /// </summary>
            private RoleModel m_Role;
            /// <summary>
            /// The presenter model
            /// </summary>
            private readonly PresenterModel m_Model;
            /// <summary>
            /// True if disposed
            /// </summary>
            private bool m_Disposed;

            private readonly EventQueue.PropertyEventDispatcher m_AcceptingSSubsChangedDispatcher;
            /// <summary>
            /// Property dispatcher
            /// </summary>
            private readonly IDisposable m_CurrentPresentationDispatcher;

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="dispatcher">The event queue</param>
            /// <param name="model">The presenter model</param>
            public SubmitStudentSubmissionToolBarButton(ControlEventQueue dispatcher, PresenterModel model)
                : base(dispatcher, model) {
                this.m_Model = model;
                this.Name = "StudentSubmissionToolBarButton";
                this.ToolTipText = "Submit the current slide to the instructor";
                
                // This is the listener that we will attatch to the AcceptingStudentSubmissions field
                // once we've got the current presentation.  We can't do this directly because
                // when this button is created there is no presentation yet.
                this.m_AcceptingSSubsChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandleAcceptingSSubsChanged));

                // Listen for the Presentation.
                // Don't call the event handler immediately (use Listen instead of ListenAndInitialize)
                // because it needs to be delayed until Initialize() below.
                this.m_CurrentPresentationDispatcher =
                    this.m_Model.Workspace.CurrentPresentation.Listen(dispatcher,
                    delegate(Property<PresentationModel>.EventArgs args) {
                        if (args.Old != null) {
                            if (args.Old.Owner != null) {
                                using (Synchronizer.Lock(args.Old.Owner)) {
                                    ((InstructorModel)(((PresentationModel)(args.Old)).Owner.Role)).Changed["AcceptingStudentSubmissions"].Remove(this.m_AcceptingSSubsChangedDispatcher.Dispatcher);
                                }
                            }
                         }
                        ParticipantModel participant = null;
                        using (Synchronizer.Lock(this.m_Model.Workspace.CurrentPresentation.SyncRoot)) {
                            if (this.m_Model.Workspace.CurrentPresentation.Value != null) {
                                using (Synchronizer.Lock(this.m_Model.Workspace.CurrentPresentation.Value.SyncRoot)) {
                                    participant = this.m_Model.Workspace.CurrentPresentation.Value.Owner;
                                }
                            }
                        }
                        if (participant != null) {
                            using (Synchronizer.Lock(participant.SyncRoot)) {
                                ((InstructorModel)(participant.Role)).Changed["AcceptingStudentSubmissions"].Add(this.m_AcceptingSSubsChangedDispatcher.Dispatcher);
                            }
                        }
                        this.HandleAcceptingSSubsChanged(null, null);                                   
                    });

                base.InitializeRole();
            }

            protected void Initialize() {

                using (Synchronizer.Lock(this.m_Model.Workspace.CurrentPresentation.SyncRoot)) {
                    using (Synchronizer.Lock(this.m_Model.Workspace.CurrentPresentation.Value.SyncRoot)) {
                        using (Synchronizer.Lock(this.m_Model.Workspace.CurrentPresentation.Value.Owner.SyncRoot)) {
                            ((InstructorModel)(this.m_Model.Workspace.CurrentPresentation.Value.Owner.Role)).Changed["AcceptingStudentSubmissions"].Add(this.m_AcceptingSSubsChangedDispatcher.Dispatcher);
                           
                        }
                    }
                }
                this.HandleAcceptingSSubsChanged(null, null);
            }



            /// <summary>
            /// Release all resources
            /// </summary>
            /// <param name="disposing">True if truely disposing</param>
            protected override void Dispose(bool disposing) {
                if (this.m_Disposed) return;
                try {
                    if (disposing) {
                        // Unregister event listeners 
                        ParticipantModel participant = null;
                        using (Synchronizer.Lock(this.m_Model.Workspace.CurrentPresentation.SyncRoot)) {
                            if (this.m_Model.Workspace.CurrentPresentation.Value != null) {
                                using (Synchronizer.Lock(this.m_Model.Workspace.CurrentPresentation.Value.SyncRoot)) {
                                    participant = this.m_Model.Workspace.CurrentPresentation.Value.Owner;
                                }
                            }
                        }
                        if (participant != null) {
                            using (Synchronizer.Lock(participant.SyncRoot)) {
                                ((InstructorModel)(participant.Role)).Changed["AcceptingStudentSubmissions"].Remove(this.m_AcceptingSSubsChangedDispatcher.Dispatcher);
                            }
                        }
                        this.Role = null;
                    }
                }
                finally {
                    base.Dispose(disposing);
                }
                this.m_Disposed = true;
            }

            /// <summary>
            /// Adds or removes any event handlers when the role is changed
            /// </summary>
            protected override RoleModel Role {
                get { return this.m_Role; }
                set { this.m_Role = value; }
            }

            /// <summary>
            /// Handle the button being clicked
            /// </summary>
            /// <param name="args">The event args</param>
            protected override void OnClick(EventArgs args) {
                if (this.Role is StudentModel) {
                    using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot)) {
                        this.m_Model.ViewerState.StudentSubmissionSignal = !this.m_Model.ViewerState.StudentSubmissionSignal;
                    }
                }
                base.OnClick(args);
            }
            /// <summary>
            /// Handle AcceptingStudentSubmissions changing (disable when false)
            /// </summary>
            /// <param name="sender">The event sender</param>
            /// <param name="args">The arguments</param>
            private void HandleAcceptingSSubsChanged(object sender, PropertyEventArgs args) {
                InstructorModel instructor = null;
                ParticipantModel participant = null;
                using (Synchronizer.Lock(this.m_Model.Workspace.CurrentPresentation.SyncRoot)) {
                    if (this.m_Model.Workspace.CurrentPresentation.Value != null) {
                        using (Synchronizer.Lock(this.m_Model.Workspace.CurrentPresentation.Value.SyncRoot)) {
                            participant = this.m_Model.Workspace.CurrentPresentation.Value.Owner;
                        }
                    }
                }
                if (participant != null) {
                    using (Synchronizer.Lock(participant.SyncRoot)) {
                        if (participant.Role != null) {
                            instructor = participant.Role as InstructorModel;
                        }
                    }
                }
                if (instructor != null) {
                    using (Synchronizer.Lock(instructor.SyncRoot)) {
                        this.Enabled = instructor.AcceptingStudentSubmissions;
                    }
                }

            }
        }

        #endregion

        #region StudentQuickPollButton class

        /// <summary>
        /// This class represents the button that submits the student submission 
        /// and ensures that the correct thing is displayed on the screen.
        /// </summary>
        private class StudentQuickPollToolBarButton : ParticipantToolBarButton {

            #region Private Members

            /// <summary>
            /// The current role
            /// </summary>
            private RoleModel m_Role;
            /// <summary>
            /// The presenter model
            /// </summary>
            private readonly PresenterModel m_Model;
            /// <summary>
            /// True if disposed
            /// </summary>
            private bool m_Disposed;

            /// <summary>
            /// The value of the button
            /// </summary>
            private string m_Value;

            /// <summary>
            /// Event dispatcher to handle when the AcceptingQuickPollSubmissions changes
            /// </summary>
            private readonly EventQueue.PropertyEventDispatcher m_QuickPollChangedDispatcher;

            /// <summary>
            /// Event dispatcher
            /// </summary>
            private readonly EventQueue.PropertyEventDispatcher m_CurrentQuickPollResultChangedDispatcher;
            /// <summary>
            /// Event dispatcher
            /// </summary>
            private readonly EventQueue.PropertyEventDispatcher m_CurrentQuickPollResultStringChangedDispatcher;
            private QuickPollResultModel m_CurrentResult;

            /// <summary>
            /// Property dispatcher to handle when the current presentation changes
            /// </summary>
            private readonly IDisposable m_CurrentPresentationDispatcher;

            #endregion

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="dispatcher">The event queue</param>
            /// <param name="model">The presenter model</param>
            public StudentQuickPollToolBarButton( ControlEventQueue dispatcher, PresenterModel model, string name, string value )
                : base( dispatcher, model ) {
                this.m_Model = model;
                this.m_Value = value;
                this.Name = name;
                this.ToolTipText = name;
                this.Text = name;
                this.DisplayStyle = ToolStripItemDisplayStyle.Text;

                // Handle the changed result string
                this.m_CurrentQuickPollResultStringChangedDispatcher = new EventQueue.PropertyEventDispatcher( this.m_EventQueue, new PropertyEventHandler( this.HandleQuickPollResultChanged ) );

                // This listener listens to changes in the CurrentStudentQuickPollResult field of the 
                // PresenterModel, when this changes we need to update the UI and attach a new listener
                // to the ResultString of the model so that we can update the UI.
                this.m_CurrentQuickPollResultChangedDispatcher = new EventQueue.PropertyEventDispatcher( this.m_EventQueue, new PropertyEventHandler( this.HandleQuickPollResultChanged ) );
                this.m_Model.Changed["CurrentStudentQuickPollResult"].Add( this.m_CurrentQuickPollResultChangedDispatcher.Dispatcher );
                using( Synchronizer.Lock( this.m_Model.SyncRoot)  ) {
                    this.m_CurrentResult = this.m_Model.CurrentStudentQuickPollResult;
                }
                this.HandleQuickPollResultChanged( null, null );



                // This is the listener that we will attatch to the AcceptingStudentSubmissions field
                // once we've got the current presentation.  We can't do this directly because
                // when this button is created there is no presentation yet.
                this.m_QuickPollChangedDispatcher = new EventQueue.PropertyEventDispatcher( this.m_EventQueue, new PropertyEventHandler( this.HandleQuickPollChanged ) );

                // Listen for the Presentation.
                // Don't call the event handler immediately (use Listen instead of ListenAndInitialize)
                // because it needs to be delayed until Initialize() below.
                this.m_CurrentPresentationDispatcher =
                    this.m_Model.Workspace.CurrentPresentation.Listen( dispatcher,
                    delegate( Property<PresentationModel>.EventArgs args ) {
                        // Remove any previous listener
                        if( args.Old != null ) {
                            if( args.Old.Owner != null ) {
                                using( Synchronizer.Lock( args.Old.Owner ) ) {
                                    ((InstructorModel)(((PresentationModel)(args.Old)).Owner.Role)).Changed["AcceptingQuickPollSubmissions"].Remove( this.m_QuickPollChangedDispatcher.Dispatcher );
                                }
                            }
                        }

                        // Get the new InstructorModel
                        ParticipantModel participant = null;
                        using( Synchronizer.Lock( this.m_Model.Workspace.CurrentPresentation.SyncRoot ) ) {
                            if( this.m_Model.Workspace.CurrentPresentation.Value != null ) {
                                using( Synchronizer.Lock( this.m_Model.Workspace.CurrentPresentation.Value.SyncRoot ) ) {
                                    participant = this.m_Model.Workspace.CurrentPresentation.Value.Owner;
                                }
                            }
                        }

                        // Attach to the new AcceptingQuickPollSubmissions
                        if( participant != null ) {
                            using( Synchronizer.Lock( participant.SyncRoot ) ) {
                                ((InstructorModel)(participant.Role)).Changed["AcceptingQuickPollSubmissions"].Add( this.m_QuickPollChangedDispatcher.Dispatcher );
                            }
                        }

                        // Update the UI
                        this.HandleQuickPollChanged( null, null );
                    } );

                base.InitializeRole();
            }

            protected void Initialize() {
                using( Synchronizer.Lock( this.m_Model.Workspace.CurrentPresentation.SyncRoot ) ) {
                    using( Synchronizer.Lock( this.m_Model.Workspace.CurrentPresentation.Value.SyncRoot ) ) {
                        using( Synchronizer.Lock( this.m_Model.Workspace.CurrentPresentation.Value.Owner.SyncRoot ) ) {
                            ((InstructorModel)(this.m_Model.Workspace.CurrentPresentation.Value.Owner.Role)).Changed["AcceptingQuickPollSubmissions"].Add( this.m_QuickPollChangedDispatcher.Dispatcher );

                        }
                    }
                }
                this.HandleQuickPollChanged( null, null );
            }



            /// <summary>
            /// Release all resources
            /// </summary>
            /// <param name="disposing">True if truely disposing</param>
            protected override void Dispose( bool disposing ) {
                if( this.m_Disposed ) return;
                try {
                    if( disposing ) {
                        // Unregister event listeners
                        this.m_CurrentResult = null;
                        this.m_Model.Changed["CurrentStudentQuickPollResult"].Remove( this.m_CurrentQuickPollResultChangedDispatcher.Dispatcher );

                        ParticipantModel participant = null;
                        using( Synchronizer.Lock( this.m_Model.Workspace.CurrentPresentation.SyncRoot ) ) {
                            if( this.m_Model.Workspace.CurrentPresentation.Value != null ) {
                                using( Synchronizer.Lock( this.m_Model.Workspace.CurrentPresentation.Value.SyncRoot ) ) {
                                    participant = this.m_Model.Workspace.CurrentPresentation.Value.Owner;
                                }
                            }
                        }
                        if( participant != null ) {
                            using( Synchronizer.Lock( participant.SyncRoot ) ) {
                                ((InstructorModel)(participant.Role)).Changed["AcceptingQuickPollSubmissions"].Remove( this.m_QuickPollChangedDispatcher.Dispatcher );
                            }
                        }
                        this.Role = null;
                    }
                } finally {
                    base.Dispose( disposing );
                }
                this.m_Disposed = true;
            }

            /// <summary>
            /// Adds or removes any event handlers when the role is changed
            /// </summary>
            protected override RoleModel Role {
                get { return this.m_Role; }
                set { this.m_Role = value; }
            }

            /// <summary>
            /// Create a new QuickPollResult and add it to the appropriate places 
            /// </summary>
            /// <param name="owner">The current participant</param>
            /// <param name="result">The result string</param>
            private void CreateNewQuickPollResult( ParticipantModel owner, string result ) {
                using( Synchronizer.Lock( this.m_Model ) ) {
                    // Create the QuickPollResultModel
                    using( Synchronizer.Lock( owner.SyncRoot ) ) {
                        this.m_Model.CurrentStudentQuickPollResult = new QuickPollResultModel( owner.Guid, result );
                    }

                    // Add to the QuickPollResults
                    using( this.m_Model.Workspace.Lock() ) {
                        if( ~this.m_Model.Workspace.CurrentPresentation != null ) {
                            using( Synchronizer.Lock( (~this.m_Model.Workspace.CurrentPresentation).SyncRoot ) ) {
                                if( (~this.m_Model.Workspace.CurrentPresentation).QuickPoll != null ) {
                                    using( Synchronizer.Lock( (~this.m_Model.Workspace.CurrentPresentation).QuickPoll.SyncRoot ) ) {
                                        if( (~this.m_Model.Workspace.CurrentPresentation).QuickPoll.QuickPollResults != null ) {
                                            (~this.m_Model.Workspace.CurrentPresentation).QuickPoll.QuickPollResults.Add( this.m_Model.CurrentStudentQuickPollResult );
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            /// <summary>
            /// Handle the button being clicked
            /// </summary>
            /// <param name="args">The event args</param>
            protected override void OnClick( EventArgs args ) {
                // Only do this if we are a student
                if( this.Role is StudentModel ) {
                    using( Synchronizer.Lock( this.m_Model ) ) {
                        if( this.m_Model.CurrentStudentQuickPollResult != null ) {
                            // Update the existing QuickPollResultModel
                            using( Synchronizer.Lock( this.m_Model.CurrentStudentQuickPollResult.SyncRoot ) ) {
                                this.m_Model.CurrentStudentQuickPollResult.ResultString = this.m_Value;
                            }
                        } else {
                            // Create a new QuickPollResultModel and put it in all the right places
                            CreateNewQuickPollResult( this.m_Model.Participant, this.m_Value );
                        }
                    }
                }
                base.OnClick( args );
            }

            /// <summary>
            /// Handle AcceptingQuickPollSubmissions changing (disable when false)
            /// </summary>
            /// <param name="sender">The event sender</param>
            /// <param name="args">The arguments</param>
            private void HandleQuickPollChanged( object sender, PropertyEventArgs args ) {
                InstructorModel instructor = null;
                ParticipantModel participant = null;

                // Determine if the button should be visible based on the quick poll style
                bool IsValidButton = false;
                using( this.m_Model.Workspace.Lock() ) {
                    if( ~this.m_Model.Workspace.CurrentPresentation != null ) {
                        using( Synchronizer.Lock( (~this.m_Model.Workspace.CurrentPresentation).SyncRoot ) ) {
                            if( (~this.m_Model.Workspace.CurrentPresentation).QuickPoll != null ) {
                                using( Synchronizer.Lock( (~this.m_Model.Workspace.CurrentPresentation).QuickPoll.SyncRoot ) ) {
                                    if( UW.ClassroomPresenter.Model.Presentation.QuickPollModel.GetVoteStringsFromStyle( (~this.m_Model.Workspace.CurrentPresentation).QuickPoll.PollStyle ).Contains( this.m_Value ) ) 
                                    {
                                        IsValidButton = true;
                                    }
                                }
                            }
                        }
                    }
                }


                // Get the instructor model and set the visibility to it's value
                if( Role is StudentModel ) {
                    using( Synchronizer.Lock( this.m_Model.Workspace.CurrentPresentation.SyncRoot ) ) {
                        if( this.m_Model.Workspace.CurrentPresentation.Value != null ) {
                            using( Synchronizer.Lock( this.m_Model.Workspace.CurrentPresentation.Value.SyncRoot ) ) {
                                participant = this.m_Model.Workspace.CurrentPresentation.Value.Owner;
                            }
                        }
                    }
                    if( participant != null ) {
                        using( Synchronizer.Lock( participant.SyncRoot ) ) {
                            if( participant.Role != null ) {
                                instructor = participant.Role as InstructorModel;
                            }
                        }
                    }
                    if( instructor != null ) {
                        using( Synchronizer.Lock( instructor.SyncRoot ) ) {
                            this.Enabled = instructor.AcceptingQuickPollSubmissions & IsValidButton;
                            this.Visible = instructor.AcceptingQuickPollSubmissions & IsValidButton;
                            if( instructor.AcceptingQuickPollSubmissions == false ) {
                                this.Checked = false;
                                using( Synchronizer.Lock( this.m_Model.SyncRoot ) ) {
                                    this.m_Model.CurrentStudentQuickPollResult = null;
                                }
                            }
                        }
                    }
                }
            }

            /// <summary>
            /// Handles when the value of the CurrentQuickPollResult changes
            /// </summary>
            /// <param name="sender"></param>
            /// <param name="args"></param>
            private void HandleQuickPollResultChanged( object sender, PropertyEventArgs args ) {
                if( this.m_CurrentResult != null ) {
                    this.m_CurrentResult.Changed["ResultString"].Remove( this.m_CurrentQuickPollResultStringChangedDispatcher.Dispatcher );
                }

                using( Synchronizer.Lock( this.m_Model.SyncRoot ) ) {
                    this.m_CurrentResult = this.m_Model.CurrentStudentQuickPollResult;
                    if( m_CurrentResult != null ) {
                        this.m_CurrentResult.Changed["ResultString"].Add( this.m_CurrentQuickPollResultStringChangedDispatcher.Dispatcher );
                    }
                }

                this.HandleQuickPollValueChanged( null, null );
            }

            /// <summary>
            /// Handles the value of quickpoll being changed
            /// </summary>
            /// <param name="sender">The event sender</param>
            /// <param name="args">The arguments</param>
            private void HandleQuickPollValueChanged( object sender, PropertyEventArgs args ) {
                using( Synchronizer.Lock( this.m_Model.SyncRoot ) ) {
                    if( this.m_Model.CurrentStudentQuickPollResult != null ) {
                        using( Synchronizer.Lock( this.m_Model.CurrentStudentQuickPollResult.SyncRoot ) ) {
                            this.Checked = (this.m_Model.CurrentStudentQuickPollResult.ResultString == this.m_Value);
                        }
                    }
                }
            }

        }

        #endregion
    }
}
