#if WEBSERVER
using System;
using System.Collections.Generic;
using System.Text;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Network.Messages;

using UW.ClassroomPresenter.Web.Model;

namespace UW.ClassroomPresenter.Web.Network
{
    /// <summary>
    /// This class handles updates to what the instructor's current state is
    /// </summary>
    public class InstructorWebService : IDisposable {
        #region Members

        private readonly SendingQueue m_Sender;
        /// <summary>
        /// The Instrcutor Model that is the basis of this machine
        /// </summary>
        private readonly InstructorModel m_Instructor;
        /// <summary>
        /// Dispatcher for handling simple changes to the members of the 
        /// InstructorModel
        /// </summary>
        private readonly EventQueue.PropertyEventDispatcher m_GenericChangeDispatcher;
        /// <summary>
        /// Dispatcher for handling changes to the CurrentDeckTraversal member
        /// of the InstructorModel
        /// </summary>
        private readonly EventQueue.PropertyEventDispatcher m_CurrentDeckTraversalChangedDispatcher;
        /// <summary>
        /// Keeps track of whether this class has been disposed of or not
        /// </summary>
        private bool m_Disposed;

        #endregion

        #region Constructors

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="sender">The sending queue to post messages to</param>
        /// <param name="role">The InstructorModel to create this class for</param>
        public InstructorWebService(SendingQueue sender, InstructorModel role)
        {
            this.m_Sender = sender;
            this.m_Instructor = role;

            // Handle basic changes
            this.m_GenericChangeDispatcher = new EventQueue.PropertyEventDispatcher(this.m_Sender, new PropertyEventHandler(this.HandleGenericChange));
            this.m_Instructor.Changed["ForcingStudentNavigationLock"].Add(this.m_GenericChangeDispatcher.Dispatcher);
            this.m_Instructor.Changed["AcceptingStudentSubmissions"].Add(this.m_GenericChangeDispatcher.Dispatcher);
            this.m_Instructor.Changed["AcceptingQuickPollSubmissions"].Add(this.m_GenericChangeDispatcher.Dispatcher);

            // Handle changes to the current deck traversal
            this.m_CurrentDeckTraversalChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_Sender, new PropertyEventHandler(this.HandleCurrentDeckTraversalChanged));
            this.m_Instructor.Changed["CurrentDeckTraversal"].Add(this.m_CurrentDeckTraversalChangedDispatcher.Dispatcher);
            this.m_CurrentDeckTraversalChangedDispatcher.Dispatcher(this, null);
        }

        #endregion

        #region IDisposable Members

        /// <summary>
        /// Finalizer for the service
        /// </summary>
        ~InstructorWebService()
        {
            this.Dispose(false);
        }

        /// <summary>
        /// Dispose method to destroy all resources
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        /// <summary>
        /// The inner dispose method for cleaning up resources
        /// </summary>
        /// <param name="disposing">True when user initiated</param>
        protected virtual void Dispose(bool disposing)
        {
            if (this.m_Disposed) return;
            if (disposing)
            {
                this.m_Instructor.Changed["ForcingStudentNavigationLock"].Remove(this.m_GenericChangeDispatcher.Dispatcher);
                this.m_Instructor.Changed["AcceptingStudentSubmissions"].Remove(this.m_GenericChangeDispatcher.Dispatcher);
                this.m_Instructor.Changed["AcceptingQuickPollSubmissions"].Remove(this.m_GenericChangeDispatcher.Dispatcher);

                this.m_Instructor.Changed["CurrentDeckTraversal"].Remove(this.m_CurrentDeckTraversalChangedDispatcher.Dispatcher);
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
        private void HandleGenericChange(object sender, PropertyEventArgs args)
        {
            bool acceptingSS = false;
            bool acceptingQP = false;
            bool forceLink = false;

            // Get the slide and deck index
            using (Synchronizer.Lock(this.m_Instructor.SyncRoot))
            {
                acceptingSS = this.m_Instructor.AcceptingStudentSubmissions;
                acceptingQP = this.m_Instructor.AcceptingQuickPollSubmissions;
                forceLink = this.m_Instructor.ForcingStudentNavigationLock;
            }

            // Change the SimpleWebModel
            lock (WebService.Instance.GlobalModel) {
                WebService.Instance.GlobalModel.AcceptingSS = acceptingSS;
                WebService.Instance.GlobalModel.AcceptingQP = acceptingQP;
                WebService.Instance.GlobalModel.ForceLink = forceLink;
            }
            WebService.Instance.UpdateModel();
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
            // Need to update the current deck if we change it as well as the 
            // current slide in the deck if it changes
            this.UpdateCurrentDeckTraversal();
        }

        /// <summary>
        /// Updated the current deck traversal in the model
        /// </summary>
        private void UpdateCurrentDeckTraversal() {
            int slideIndex = -1;
            int deckIndex = -1;

            // Get the slide and deck index
            using (Synchronizer.Lock(this.m_Instructor.SyncRoot))
            {
                DeckTraversalModel traversal = this.m_Instructor.CurrentDeckTraversal;
                if( traversal != null ) {
                    using (Synchronizer.Lock(traversal.SyncRoot)) {
                        slideIndex = traversal.AbsoluteCurrentSlideIndex;
                    }
                }

                PresentationModel presentation = this.m_Instructor.CurrentPresentation;
                if( presentation != null ) {
                    using( Synchronizer.Lock(presentation.SyncRoot) ) {
                        deckIndex = this.m_Instructor.CurrentPresentation.DeckTraversals.IndexOf( traversal );
                    }
                }
            }

            // Change the SimpleWebModel
            lock (WebService.Instance.GlobalModel) {
                WebService.Instance.GlobalModel.CurrentDeck = deckIndex;
                WebService.Instance.GlobalModel.CurrentSlide = slideIndex;
            }
            WebService.Instance.UpdateModel();
        }

        #endregion
    }
}
#endif