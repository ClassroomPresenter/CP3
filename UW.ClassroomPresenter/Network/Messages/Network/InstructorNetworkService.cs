// $Id: InstructorNetworkService.cs 2233 2013-09-27 22:17:28Z fred $

using System;
using System.Diagnostics;
using System.Threading;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Network.Messages;
using UW.ClassroomPresenter.Network.Messages.Network;
using UW.ClassroomPresenter.Network.Messages.Presentation;
using UW.ClassroomPresenter.Network.Groups;

namespace UW.ClassroomPresenter.Network.Messages.Network {
    /// <summary>
    /// This class instansiates all of the network services needed on the 
    /// instructor machine
    /// </summary>
    public class InstructorNetworkService : RoleNetworkService {
        #region Private Members

        /// <summary>
        /// The Instrcutor Model that is the basis of this machine
        /// </summary>
        private readonly InstructorModel m_Instructor;
        /// <summary>
        /// The current presentation
        /// </summary>
        private PresentationModel m_CurrentPresentation;
        /// <summary>
        /// Keeps track of whether this class has been disposed of or not
        /// </summary>
        private bool m_Disposed;
        /// <summary>
        /// Dispatcher for handling simple changes to the members of the 
        /// InstructorModel
        /// </summary>
        private readonly EventQueue.PropertyEventDispatcher m_GenericChangeDispatcher;
        /// <summary>
        /// Dispatcher for handling changes to the CurrentPresentation member 
        /// of the InstructorModel
        /// </summary>
        private readonly EventQueue.PropertyEventDispatcher m_CurrentPresentationChangedDispatcher;
        /// <summary>
        /// Dispatcher for handling changes to the CurrentDeckTraversal member
        /// of the InstructorModel
        /// </summary>
        private readonly EventQueue.PropertyEventDispatcher m_CurrentDeckTraversalChangedDispatcher;
        /// <summary>
        /// Network service to keep track of the status of student submissions
        /// </summary>
        private readonly SubmissionStatusNetworkService submission_status_service_;
        /// <summary>
        /// Network service for the current presentation
        /// </summary>
        private PresentationNetworkService m_CurrentPresentationNetworkService;

#if WEBSERVER
        /// <summary>
        /// Web network service to keep track of changes to the instructor model
        /// </summary>
        private Web.Network.InstructorWebService m_InstructorWebService;

        /// <summary>
        /// Web network service for the current presentation
        /// </summary>
        private Web.Network.PresentationWebService m_CurrentPresentationWebService;

        /// <summary>
        /// Web network service for the web performance model.
        /// </summary>
        private Web.Network.WebPerformanceWebService m_WebPerformanceWebService;
#endif

        #endregion

        #region Constructors

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="sender">The sending queue to post messages to</param>
        /// <param name="role">The InstructorModel to create this class for</param>
        public InstructorNetworkService(SendingQueue sender, InstructorModel role)
            : base(sender, role) {
            this.m_Instructor = role;

            // Create the submission status service
            submission_status_service_ = new SubmissionStatusNetworkService(this.Sender, role);

            // Handle basic changes
            this.m_GenericChangeDispatcher = new EventQueue.PropertyEventDispatcher(this.Sender, new PropertyEventHandler(this.HandleGenericChange));
            this.m_Instructor.Changed["ForcingStudentNavigationLock"].Add(this.m_GenericChangeDispatcher.Dispatcher);
            this.m_Instructor.Changed["AcceptingStudentSubmissions"].Add(this.m_GenericChangeDispatcher.Dispatcher);
            this.m_Instructor.Changed["AcceptingQuickPollSubmissions"].Add(this.m_GenericChangeDispatcher.Dispatcher);
            this.m_Instructor.Changed["StudentNavigationType"].Add(this.m_GenericChangeDispatcher.Dispatcher);

            // Handle changes to the current presentation
            this.m_CurrentPresentationChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.Sender, new PropertyEventHandler(this.HandleCurrentPresentationChanged));
            this.m_Instructor.Changed["CurrentPresentation"].Add(this.m_CurrentPresentationChangedDispatcher.Dispatcher);
            this.m_CurrentPresentationChangedDispatcher.Dispatcher(this, null);

            // Handle changes to the current deck traversal
            this.m_CurrentDeckTraversalChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.Sender, new PropertyEventHandler(this.HandleCurrentDeckTraversalChanged));
            this.m_Instructor.Changed["CurrentDeckTraversal"].Add(this.m_CurrentDeckTraversalChangedDispatcher.Dispatcher);
            this.m_CurrentDeckTraversalChangedDispatcher.Dispatcher(this, null);

#if WEBSERVER
            // Create the web service equivalent to this service
            this.m_InstructorWebService = new Web.Network.InstructorWebService( this.Sender, role );

            using (Synchronizer.Lock(PresenterModel.TheInstance.SyncRoot)) {
                using (Synchronizer.Lock(PresenterModel.TheInstance.ViewerState.SyncRoot)) {
                    this.m_WebPerformanceWebService = new Web.Network.WebPerformanceWebService(this.Sender, PresenterModel.TheInstance.ViewerState.WebPerformance);
                }
            }
#endif
        }

        #endregion

        #region IDisposable Members

        protected override void Dispose(bool disposing) {
            if (this.m_Disposed) return;
            try {
                if (disposing) {
                    this.m_Instructor.Changed["ForcingStudentNavigationLock"].Remove(this.m_GenericChangeDispatcher.Dispatcher);
                    this.m_Instructor.Changed["AcceptingStudentSubmissions"].Remove(this.m_GenericChangeDispatcher.Dispatcher);
                    this.m_Instructor.Changed["AcceptingQuickPollSubmissions"].Remove(this.m_GenericChangeDispatcher.Dispatcher);

                    this.m_Instructor.Changed["CurrentDeckTraversal"].Remove(this.m_CurrentDeckTraversalChangedDispatcher.Dispatcher);

                    this.m_Instructor.Changed["CurrentPresentation"].Remove(this.m_CurrentPresentationChangedDispatcher.Dispatcher);

                    // Dispose of the CurrentPresentationNetworkService and send a PresentationEndedMessage via the CurrentPresentation setter.
                    this.CurrentPresentation = null;

#if WEBSERVER
                    // Dispose of the web service as well
                    this.m_InstructorWebService.Dispose();
                    this.m_WebPerformanceWebService.Dispose();
#endif
                }
            } finally {
                base.Dispose(disposing);
            }
            this.m_Disposed = true;
        }

        #endregion

        #region Generic Changes

        /// <summary>
        /// Handle when a basic change happens to the InstructorModel
        /// </summary>
        /// <param name="sender">The object that posted this event</param>
        /// <param name="args">The arguments for this event</param>
        private void HandleGenericChange(object sender, PropertyEventArgs args) {
            this.SendGenericChange(Group.AllParticipant);
        }

        /// <summary>
        /// Send a network message with the changes to the InstructorModel
        /// </summary>
        /// <param name="receivers"></param>
        private void SendGenericChange(Group receivers) {
            Message message = new InstructorMessage(this.m_Instructor);
            message.Group = receivers;
            this.Sender.Send(message);
        }

        #endregion

        #region CurrentDeckTraversal

        /// <summary>
        /// Handle when the CurrentDeckTraversal member of the 
        /// InstructorModel changes
        /// </summary>
        /// <param name="sender">The object that posted this event</param>
        /// <param name="args">The arguments for this event</param>
        private void HandleCurrentDeckTraversalChanged(object sender, PropertyEventArgs args) {
            this.SendCurrentDeckTraversalChange(Group.AllParticipant);
        }

        /// <summary>
        /// Send a network message with the updated deck traversal
        /// </summary>
        /// <param name="receivers"></param>
        private void SendCurrentDeckTraversalChange(Group receivers) {
            using(Synchronizer.Lock(this.m_Instructor.SyncRoot)) {
                DeckTraversalModel traversal = this.m_Instructor.CurrentDeckTraversal;
                if(traversal != null) {
                    Message message = new InstructorMessage(this.m_Instructor);
                    message.Group = receivers;

                    // Create a SlideMessage/TableOfContentsEntryMessage which will be the
                    // InstructorCurrentDeckTraversalChangedMessage's predecessor.
                    using (Synchronizer.Lock(traversal.SyncRoot)) {
                        Message slide = new SlideInformationMessage(traversal.Current.Slide);
                        using (Synchronizer.Lock(traversal.Deck.SyncRoot)) {
                            if( (traversal.Deck.Disposition & (DeckDisposition.StudentSubmission | DeckDisposition.QuickPoll) ) != 0 )
                                slide.Group = Groups.Group.Submissions;
                        }
                        slide.InsertChild(new TableOfContentsEntryMessage(traversal.Current));
                        message.InsertChild(slide);
                    }

                    message.InsertChild(new InstructorCurrentDeckTraversalChangedMessage(traversal));

#if DEBUG
                    // Add logging of slide change events
                    string pres_name = "";
                    using( Synchronizer.Lock( this.m_CurrentPresentation.SyncRoot ) )
                        pres_name = this.m_CurrentPresentation.HumanName;

                    string deck_name = "";
                    using( Synchronizer.Lock( traversal.Deck.SyncRoot ) )
                        deck_name = traversal.Deck.HumanName;

                    int slide_index = 0;
                    Guid slide_guid = Guid.Empty;
                    using( Synchronizer.Lock( traversal.SyncRoot ) ) {
                        if( traversal.Current != null )
                            using (Synchronizer.Lock(traversal.Current.SyncRoot)) {
                                slide_index = traversal.Current.IndexInParent;
                                using (Synchronizer.Lock(traversal.Current.Slide.SyncRoot)) {
                                    slide_guid = traversal.Current.Slide.Id;
                                }
                            }
                    }

                    Debug.WriteLine( string.Format( "DECK CHANGE ({0}): Pres -- {1}, Deck -- {2}, Slide -- {3}, Guid -- {4}", 
                        System.DateTime.Now.Ticks, pres_name, deck_name, slide_index, slide_guid ) );
#endif
                    message.Tags = new MessageTags();
                    message.Tags.BridgePriority = MessagePriority.Higher;

                    this.Sender.Send(message);
                }
            }
        }

        #endregion

        #region CurrentPresentation

        /// <summary>
        /// Protected member that does cleanup and setup when changing the current presentation
        /// </summary>
        protected PresentationModel CurrentPresentation {
            set {
                using (Synchronizer.Lock(this)) {
                    // Recreate the Presentation Network Services
                    Debug.Assert((this.m_CurrentPresentationNetworkService == null) == (this.m_CurrentPresentation == null));
                    if (this.m_CurrentPresentationNetworkService != null) {
                        this.m_CurrentPresentationNetworkService.Dispose();
                        this.m_CurrentPresentationNetworkService = null;

                        this.SendPresentationEndedMessage(Group.AllParticipant);
                    }

#if WEBSERVER
                    // Delete the old web presentation service
                    if (this.m_CurrentPresentationWebService != null)
                    {
                        this.m_CurrentPresentationWebService.Dispose();
                        this.m_CurrentPresentationWebService = null;
                    }
#endif

                    // Set the current presentation
                    this.m_CurrentPresentation = value;

                    if (this.m_CurrentPresentation != null) {
                        this.SendPresentationStartedMessage(Group.AllParticipant);

                        this.m_CurrentPresentationNetworkService = new PresentationNetworkService(this.Sender, this.m_CurrentPresentation);
#if WEBSERVER
                        // Create the new web presentation service
                        this.m_CurrentPresentationWebService = new Web.Network.PresentationWebService( this.Sender, this.m_CurrentPresentation );
#endif
                    }
                }
            }
        }
        
        /// <summary>
        /// Handle when the CurrentPresentation member of the InstructorModel changes
        /// </summary>
        /// <param name="sender">The objec that posted this event</param>
        /// <param name="args">The arguments to this event handler</param>
        private void HandleCurrentPresentationChanged(object sender, PropertyEventArgs args) {
            using (Synchronizer.Lock(this)) {
                using (Synchronizer.Lock(this.m_Instructor.SyncRoot)) {
                    this.CurrentPresentation = this.m_Instructor.CurrentPresentation;
                }
            }
        }

        /// <summary>
        /// Send a PresentationStartedMessage
        /// </summary>
        /// <param name="receivers">The group of receivers to get this message</param>
        private void SendPresentationStartedMessage(Group receivers) {
            // Caller must lock(this).
            Debug.Assert(this.m_CurrentPresentation != null);

            // FIXME: Send a message if the instructor's CurrentPresentation is set to null.
            if (this.m_CurrentPresentation != null) {
                Message message = new InstructorMessage(this.m_Instructor);
                message.Group = receivers;
                message.InsertChild(new InstructorCurrentPresentationChangedMessage(this.m_CurrentPresentation));
                this.Sender.Send(message);
            }
        }

        /// <summary>
        /// Send a PresentationEndedMessage
        /// </summary>
        /// <param name="receivers">The group of receivers to get this message</param>
        private void SendPresentationEndedMessage(Group receivers) {
            // Caller must lock(this).
            Debug.Assert(this.m_CurrentPresentation != null);
            Message message = new PresentationEndedMessage(this.m_CurrentPresentation);
            message.Group = receivers;
            this.Sender.Send(message);
        }

        #endregion CurrentPresentation

        #region INetworkService Members

        /// <summary>
        /// Force this class to be updated, also update all child network services
        /// This gets called when a new person joins this presentation
        /// </summary>
        /// <param name="receivers">The group to receive this message</param>
        public override void ForceUpdate(Group receivers) {
            using (Synchronizer.Lock(this)) {
                if (this.m_CurrentPresentation != null)
                    this.SendPresentationStartedMessage(receivers);
            }

            PresentationNetworkService presentation = this.m_CurrentPresentationNetworkService;
            if (presentation != null)
                presentation.ForceUpdate(receivers);

            //It appears that this needs to come AFTER the presentation.ForceUpdate or else late joiners will not see the current slide in the main slideview.
            this.SendCurrentDeckTraversalChange(receivers);
        }

        #endregion
    }
}