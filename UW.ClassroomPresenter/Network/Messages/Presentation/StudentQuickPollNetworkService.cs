using System;
using System.Collections.Generic;
using System.Text;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Network.Groups;

namespace UW.ClassroomPresenter.Network.Messages.Presentation {
    /// <summary>
    /// On the student side this class is responsible for keeping track of when new 
    /// results are added to the collection of results and submitting that result.
    /// 
    /// Also, this should create a network service for each result which should 
    /// keep track of when that result gets changed.
    /// </summary>
    public class StudentQuickPollNetworkService : INetworkService, IDisposable {
        #region Private Members

        /// <summary>
        /// The event queue to post messages to
        /// </summary>
        private readonly SendingQueue m_Sender;
        /// <summary>
        /// The PresentationModel that this network service listens to
        /// </summary>
        private readonly PresentationModel m_Presentation;
        /// <summary>
        /// The QuickPollModel that this network service listens to
        /// </summary>
        private readonly QuickPollModel m_QuickPoll;
        /// <summary>
        /// Helper class that is responsible for listening to changes to the QuickPollResults collection
        /// </summary>
        private readonly QuickPollResultCollectionHelper m_QuickPollResultCollectionHelper;
        /// <summary>
        /// Keeps track of whether this class has been disposed of or not
        /// </summary>
        private bool m_Disposed;

        #endregion

        #region Constructors

        /// <summary>
        /// Constructs a new StudentQuickPollNetworkService
        /// </summary>
        /// <param name="sender">The message queue to post messages to</param>
        /// <param name="presentation">The PresentationModel to create this class from</param>
        /// <param name="poll">The QuickPollModel to create this class from</param>
        public StudentQuickPollNetworkService( SendingQueue sender, PresentationModel presentation ) {
            this.m_Sender = sender;
            this.m_Presentation = presentation;
            using( Synchronizer.Lock( this.m_Presentation.SyncRoot ) ) {
                this.m_QuickPoll = this.m_Presentation.QuickPoll;
            }

            if( this.m_QuickPoll != null ) {
                this.m_QuickPollResultCollectionHelper = new QuickPollResultCollectionHelper( this );
            }
        }

        #endregion

        #region IDisposable Members

        /// <summary>
        /// Destructor for this class
        /// </summary>
        ~StudentQuickPollNetworkService() {
            this.Dispose( false );
        }

        /// <summary>
        /// Disposes of the resources for this class
        /// </summary>
        public void Dispose() {
            this.Dispose( true );
            System.GC.SuppressFinalize( this );
        }

        /// <summary>
        /// Disposes of the resources for this class
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose( bool disposing ) {
            if( this.m_Disposed ) return;
            if( disposing ) {
                if( this.m_QuickPollResultCollectionHelper != null ) {
                    this.m_QuickPollResultCollectionHelper.Dispose();
                }
            }
            this.m_Disposed = true;
        }

        #endregion

        #region INetworkService Members

        /// <summary>
        /// Force this network service to resend all information that it is responsible for
        /// </summary>
        /// <param name="receivers">The group of receivers that should get the update</param>
        public void ForceUpdate( Group receivers ) {
            this.m_QuickPollResultCollectionHelper.ForceUpdate( receivers );
        }

        #endregion

        #region QuickPollResultCollectionHelper

        /// <summary>
        /// Helper class that listens to when new results are added to the 
        /// collection and creates a listener for each of these results.
        /// These listeners are responsible for sending any changes to the
        /// results from the student to the instructor.
        /// </summary>
        private class QuickPollResultCollectionHelper : PropertyCollectionHelper, INetworkService {
            private readonly StudentQuickPollNetworkService m_Service;

            public QuickPollResultCollectionHelper( StudentQuickPollNetworkService service )
                : base( service.m_Sender, service.m_QuickPoll, "QuickPollResults" ) {
                this.m_Service = service;
                base.Initialize();
            }

            protected override void Dispose( bool disposing ) {
                if( disposing )
                    foreach( IDisposable disposable in this.Tags )
                        if( disposable != null )
                            disposable.Dispose();
                base.Dispose( disposing );
            }

            protected override object SetUpMember( int index, object member ) {
                QuickPollResultModel result = ((QuickPollResultModel)member);

                Group receivers = Group.Submissions;
                this.SendPollResultInformation( result, receivers );

                return new QuickPollResultNetworkService( this.m_Service.m_Sender, this.m_Service.m_Presentation, this.m_Service.m_QuickPoll, result );
            }

            protected override void TearDownMember( int index, object member, object tag ) {
                QuickPollResultNetworkService service = ((QuickPollResultNetworkService)tag);

                if( service != null ) {
                    QuickPollResultModel result = service.Result;
                    service.Dispose();

                    // Note: Do not need to send a removed message here since there should be no 
                    // case where a result is removed by a student (the student can abstain from 
                    // voting)
                }
            }

            private void SendPollResultInformation( QuickPollResultModel result, Group receivers ) {
                Message message, poll, res;
                message = new PresentationInformationMessage( this.m_Service.m_Presentation );
                message.Group = receivers;
                poll = new QuickPollInformationMessage( this.m_Service.m_QuickPoll );
                poll.Group = receivers;
                message.InsertChild( poll );
                res = new QuickPollResultInformationMessage( result );
                res.Group = receivers;
                poll.InsertChild( res );

                this.m_Service.m_Sender.Send( message );
            }

            #region INetworkService Members

            public void ForceUpdate( Group receivers ) {
                using( Synchronizer.Lock( this.Owner.SyncRoot ) ) {
                    foreach( QuickPollResultNetworkService service in this.Tags ) {
                        if( service != null ) {
                            this.SendPollResultInformation( service.Result, receivers );
                            service.ForceUpdate( receivers );
                        }
                    }
                }
            }

            #endregion
        }

        #endregion
    }
}

