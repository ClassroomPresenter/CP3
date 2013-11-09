using System;
using System.Collections.Generic;
using System.Text;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Network.Groups;

namespace UW.ClassroomPresenter.Network.Messages.Presentation {
    /// <summary>
    /// On the instructor's side all we need to do is broadcast when a new quickpoll has been 
    /// added or when the settings of the existing quickpoll have changed.
    /// 
    /// This class handles updating the clients when the properties of an existing quickpoll 
    /// change. PresentationNetworkService is responsible for notifying clients when the 
    /// quickpoll itself changes.
    /// 
    /// TODO: Determine which properties need to be transmitted.
    /// </summary>
    public class QuickPollNetworkService : INetworkService, IDisposable {
        private readonly SendingQueue m_Sender;
        private readonly PresentationModel m_Presentation;
        private readonly QuickPollModel m_QuickPoll;
        /// <summary>
        /// Helper class that is responsible for listening to changes to the QuickPollResults collection
        /// </summary>
        private readonly QuickPollResultCollectionHelper m_QuickPollResultCollectionHelper;
        private bool m_Disposed;

        public QuickPollNetworkService(SendingQueue sender, PresentationModel presentation ) {
            this.m_Sender = sender;
            this.m_Presentation = presentation;
            using( Synchronizer.Lock( this.m_Presentation.SyncRoot ) ) {
                this.m_QuickPoll = this.m_Presentation.QuickPoll;
            }

            if( this.m_QuickPoll != null ) {
                this.m_QuickPollResultCollectionHelper = new QuickPollResultCollectionHelper( this );
            }
        }

        #region IDisposable Members

        ~QuickPollNetworkService() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            if(disposing) {
                if( this.m_QuickPollResultCollectionHelper != null ) {
                    this.m_QuickPollResultCollectionHelper.Dispose();
                }
            }
            this.m_Disposed = true;
        }

        #endregion

        #region INetworkService Members

        public void ForceUpdate(Group receivers) {
            if( this.m_QuickPollResultCollectionHelper != null ) {
                this.m_QuickPollResultCollectionHelper.ForceUpdate( receivers );
            }
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
            private readonly QuickPollNetworkService m_Service;

            public QuickPollResultCollectionHelper( QuickPollNetworkService service )
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

