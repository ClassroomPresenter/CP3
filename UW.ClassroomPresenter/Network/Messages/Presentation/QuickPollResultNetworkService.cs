using System;
using System.Collections.Generic;
using System.Text;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Network.Groups;

namespace UW.ClassroomPresenter.Network.Messages.Presentation {

    /// <summary>
    /// This class listens to changes to a QuickPollResultModel and
    /// sends those changes to all instructors/public displays
    /// </summary>
    public class QuickPollResultNetworkService : INetworkService, IDisposable {
        #region Private Members

        /// <summary>
        /// The message queue to use for this class
        /// </summary>
        private readonly SendingQueue m_Sender;
        /// <summary>
        /// Event listener for when a result changes
        /// </summary>
        private readonly EventQueue.PropertyEventDispatcher m_ResultChangedDispatcher;

        /// <summary>
        /// The PresentationModel that this result is part of
        /// </summary>
        private readonly PresentationModel m_Presentation;
        /// <summary>
        /// The QuickPollModel that this result is part of 
        /// </summary>
        private readonly QuickPollModel m_QuickPoll;
        /// <summary>
        /// The QuickPollResultModel that this class is listening to
        /// </summary>
        private readonly QuickPollResultModel m_Result;
        
        /// <summary>
        /// Keeps track of whether or not this class has been disposed of
        /// </summary>
        private bool m_Disposed;

        #endregion

        #region Constructors

        /// <summary>
        /// Constructs a new QuickPollResultNetworkService
        /// </summary>
        /// <param name="sender">The message queue to use</param>
        /// <param name="presentation">The PresentationModel to associate this service with</param>
        /// <param name="poll">The QuickPollModel to associate this service with</param>
        /// <param name="result">The QuickPollResultModel to associate this service with</param>
        public QuickPollResultNetworkService( SendingQueue sender, PresentationModel presentation, QuickPollModel poll, QuickPollResultModel result ) {
            this.m_Sender = sender;
            this.m_Presentation = presentation;
            this.m_QuickPoll = poll;
            this.m_Result = result;

            // Listen to changes tot he ResultString
            this.m_ResultChangedDispatcher = new EventQueue.PropertyEventDispatcher( this.m_Sender, new PropertyEventHandler( this.HandleResultChanged ) );
            this.m_Result.Changed["ResultString"].Add( this.m_ResultChangedDispatcher.Dispatcher );
        }

        #endregion

        #region IDisposable Members

        /// <summary>
        /// Destructor
        /// </summary>
        ~QuickPollResultNetworkService() {
            this.Dispose( false );
        }

        /// <summary>
        /// Dispose of this class
        /// </summary>
        public void Dispose() {
            this.Dispose( true );
            System.GC.SuppressFinalize( this );
        }
        /// <summary>
        /// Dispose of this class
        /// </summary>
        /// <param name="disposing">True if system is disposing, false otherwise</param>
        protected virtual void Dispose( bool disposing ) {
            if( this.m_Disposed ) return;
            if( disposing ) {
                this.m_Result.Changed["ResultString"].Remove( this.m_ResultChangedDispatcher.Dispatcher );
            }
            this.m_Disposed = true;
        }

        #endregion

        /// <summary>
        /// Accessor for getting the result model that this network service is listening to
        /// </summary>
        public QuickPollResultModel Result {
            get { return this.m_Result; }
        }

        /// <summary>
        /// Event handler for when any of the result fields change
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void HandleResultChanged( object sender, PropertyEventArgs args ) {
            Group receivers = Group.Submissions;
            this.SendResultChanged( receivers );
        }

        /// <summary>
        /// Send a QuickPollResultInformationMessage to the appropriate receivers
        /// </summary>
        /// <param name="receivers">The group of people that should receive the update</param>
        private void SendResultChanged( Group receivers ) {
            Message message, poll, res;
            message = new PresentationInformationMessage( this.m_Presentation );
            message.Group = receivers;
            poll = new QuickPollInformationMessage( this.m_QuickPoll );
            poll.Group = receivers;
            message.InsertChild( poll );
            res = new QuickPollResultInformationMessage( this.m_Result );
            res.Group = receivers;
            poll.InsertChild( res );
            this.m_Sender.Send( message );
        }

        #region INetworkService Members

        /// <summary>
        /// Send the appropriate information to late joiners
        /// </summary>
        /// <param name="receivers"></param>
        public void ForceUpdate( Group receivers ) {
            this.SendResultChanged( receivers );
        }

        #endregion
    }
}
