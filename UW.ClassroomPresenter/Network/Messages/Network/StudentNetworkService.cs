// $Id: StudentNetworkService.cs 774 2005-09-21 20:22:33Z pediddle $

using System;
using System.Diagnostics;
using System.Threading;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Model.Workspace;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Network.Messages;
using UW.ClassroomPresenter.Network.Messages.Network;
using UW.ClassroomPresenter.Network.Messages.Presentation;
using UW.ClassroomPresenter.Network.Groups;

namespace UW.ClassroomPresenter.Network.Messages.Network {
    /// <summary>
    /// This class instanciates all the network services needed on a student 
    /// machine
    /// </summary>
    public class StudentNetworkService : RoleNetworkService {
        #region Private Members

        /// <summary>
        /// The student model
        /// </summary>
        private readonly StudentModel m_Student;
        /// <summary>
        /// Dispatcher for keeping track of when the CurrentPresentation changes
        /// </summary>
        private readonly IDisposable m_PresentationChangedDispatcher;
        /// <summary>
        /// Keeps track of when this object has been disposed of
        /// </summary>
        private bool m_Disposed;
        /// <summary>
        /// Reference to the PresenterModel object
        /// </summary>
        private readonly PresenterModel m_Model;
        /// <summary>
        /// Reference to the current presentation
        /// </summary>
        private PresentationModel m_CurrentPresentation;
        /// <summary>
        /// Network service for keeping track of anything that needs to be kept track of in the presentation on the student side
        /// </summary>
        private StudentPresentationNetworkService m_StudentPresentationNetworkService;
        /// <summary>
        /// Network service for keeping track of everything needed for different types of student submissions
        /// TODO: Currently unused, should this be removed?
        /// </summary>
        private SSPresentationNetworkService m_SSCurrentPresentationNetworkService;
        /// <summary>
        /// Network service for keeping track of the status of student submissions
        /// TODO: This seems a bit hacky, should look over and see if it can be improved
        /// </summary>
        private SubmissionStatusNetworkService submission_status_service_;

        #endregion

        #region Constructors

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="sender">The sending queue for this class</param>
        /// <param name="model">The PresenterModel for the network service</param>
        /// <param name="role">The StudentModel for this network service</param>
        public StudentNetworkService(SendingQueue sender, PresenterModel model, StudentModel role) : base(sender, role) {
            this.m_Student = role;
            this.m_Model = model;

            // Instanciate the submission status service
            submission_status_service_ = new SubmissionStatusNetworkService(this.Sender, role);

            // Create a delegate that will update the local copy of the current 
            // presentation whenever it changes in the workspace.
            //
            // This causes the set{} accessor for CurrentPresentation to be run,
            // which as a side effect disposes of and recreates all the network
            // services associated with the student.
            this.m_PresentationChangedDispatcher =
                this.m_Model.Workspace.CurrentPresentation.ListenAndInitialize(sender,
                    delegate(Property<PresentationModel>.EventArgs args) {
                        if (args.New != null) {
                            this.CurrentPresentation = args.New;
                        }
                    });
        }

        #endregion

        #region IDisposable Members

        protected override void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            try {
                if(disposing) {
                    this.m_PresentationChangedDispatcher.Dispose();
                    // Dispose of the CurrentPresentationNetworkService and send a PresentationEndedMessage via the CurrentPresentation setter.
                    this.CurrentPresentation = null;
                }
            } finally {
                base.Dispose(disposing);
            }
            this.m_Disposed = true;
        }

        #endregion

        #region CurrentPresentation

        protected PresentationModel CurrentPresentation {
            set {
                using(Synchronizer.Lock(this)) {
                    if( this.m_StudentPresentationNetworkService != null ) {
                        this.m_StudentPresentationNetworkService.Dispose();
                        this.m_StudentPresentationNetworkService = null;
                    }

                    if( this.m_SSCurrentPresentationNetworkService != null ) {
                        this.m_SSCurrentPresentationNetworkService.Dispose();
                        this.m_SSCurrentPresentationNetworkService = null;
                    }

                    this.m_CurrentPresentation = value;

                    if(this.m_CurrentPresentation != null) {
                        this.m_StudentPresentationNetworkService = new StudentPresentationNetworkService( this.Sender, this.m_CurrentPresentation );
                        //this.m_SSCurrentPresentationNetworkService = new SSPresentationNetworkService(this.Sender, this.m_CurrentPresentation);
                    }
                }
            }
        }

        #endregion CurrentPresentation

        #region INetworkService Members

        public override void ForceUpdate(Group receivers) {
            // Currently it doesn't make sense to rebroadcast student submissions,
            // so there is nothing to do here (for now).
        }

        #endregion
    }
}