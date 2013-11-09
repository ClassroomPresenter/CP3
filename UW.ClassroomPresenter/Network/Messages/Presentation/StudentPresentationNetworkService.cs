using System;
using System.Diagnostics;
using System.Threading;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Network.Groups;

namespace UW.ClassroomPresenter.Network.Messages.Presentation {
    /// <summary>
    /// One the student side this class is responsible for keeping track of when a quickpoll is 
    /// added/removed/changed from the presentation. When this happens we need to dispose of
    /// all the child network services and recreate them.
    /// </summary>
    public class StudentPresentationNetworkService : INetworkService, IDisposable {
        #region Private Members

        /// <summary>
        /// The event queue to use for dealing with changes in this object
        /// </summary>
        private readonly SendingQueue m_Sender;
        /// <summary>
        /// The PresentationModel tat this class keeps track of
        /// </summary>
        private readonly PresentationModel m_Presentation;

        /// <summary>
        /// Dispatcher that is called when the QuickPollModel changes
        /// </summary>
        private readonly EventQueue.PropertyEventDispatcher m_StudentQuickPollChangedDispatcher;
        /// <summary>
        /// The network service responsible for listening to changes to the QuickPollModel
        /// </summary>
        private StudentQuickPollNetworkService m_StudentQuickPollNetworkService;

        /// <summary>
        /// Determines whether or not this object has been disposed of
        /// </summary>
        private bool m_Disposed;

        #endregion

        #region Constructors

        /// <summary>
        /// Construct a StudentPresentationNetworkService
        /// </summary>
        /// <param name="sender">The event queue to use</param>
        /// <param name="presentation">The PresentationModel to listen for changes to</param>
        public StudentPresentationNetworkService( SendingQueue sender, PresentationModel presentation ) {
            this.m_Sender = sender;
            this.m_Presentation = presentation;

            this.m_StudentQuickPollNetworkService = new StudentQuickPollNetworkService( this.m_Sender, this.m_Presentation );

            this.m_StudentQuickPollChangedDispatcher = new EventQueue.PropertyEventDispatcher( this.m_Sender, new PropertyEventHandler( this.HandleStudentQuickPollChanged ) );
            this.m_Presentation.Changed["QuickPoll"].Add( this.m_StudentQuickPollChangedDispatcher.Dispatcher );
        }

        #endregion

        #region IDisposable Members

        ~StudentPresentationNetworkService() {
            this.Dispose( false );
        }

        public void Dispose() {
            this.Dispose( true );
            System.GC.SuppressFinalize( this );
        }

        protected virtual void Dispose( bool disposing ) {
            if( this.m_Disposed ) return;
            if( disposing ) {
                this.m_Presentation.Changed["QuickPoll"].Remove( this.m_StudentQuickPollChangedDispatcher.Dispatcher );
                this.m_StudentQuickPollNetworkService.Dispose();
            }
            this.m_Disposed = true;
        }

        #endregion

        #region INetworkService Members

        public void ForceUpdate( Group receivers ) {
            this.m_StudentQuickPollNetworkService.ForceUpdate( receivers );
        }

        #endregion

        /// <summary>
        /// Handle when the quickpoll changes by disposing of the old service and recreating it
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void HandleStudentQuickPollChanged( object sender, PropertyEventArgs args ) {
            if( this.m_StudentQuickPollNetworkService != null ) {
                this.m_StudentQuickPollNetworkService.Dispose();
            }

            this.m_StudentQuickPollNetworkService = new StudentQuickPollNetworkService( this.m_Sender, this.m_Presentation );
        }
    }
}
