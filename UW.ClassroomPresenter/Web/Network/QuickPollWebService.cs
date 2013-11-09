#if WEBSERVER
using System;
using System.Collections.Generic;
using System.Text;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Network.Messages;

namespace UW.ClassroomPresenter.Web.Network {
    /// <summary>
    /// Handles changes done to the 
    /// </summary>
    public class QuickPollWebService : IDisposable {
        #region Members

        private readonly SendingQueue m_Sender;
        private readonly PresentationModel m_Presentation;
        private readonly QuickPollModel m_QuickPoll;
        /// <summary>
        /// Helper class that is responsible for listening to changes to the QuickPollResults collection
        /// </summary>
//        private readonly QuickPollResultCollectionHelper m_QuickPollResultCollectionHelper;
        private bool m_Disposed;

        #endregion

        #region Constructors

        /// <summary>
        /// Constructor for the web service
        /// </summary>
        /// <param name="sender">The event queue to use</param>
        /// <param name="presentation">The presentation to listen to</param>
        public QuickPollWebService( SendingQueue sender, PresentationModel presentation ) {
            this.m_Sender = sender;
            this.m_Presentation = presentation;
            using (Synchronizer.Lock(this.m_Presentation.SyncRoot))
            {
                this.m_QuickPoll = this.m_Presentation.QuickPoll;
            }

            if (this.m_QuickPoll != null)
            {
//                this.m_QuickPollResultCollectionHelper = new QuickPollResultCollectionHelper(this);
            }
        }

        #endregion

        #region IDisposable Members

        /// <summary>
        /// Finalizer
        /// </summary>
        ~QuickPollWebService()
        {
            this.Dispose(false);
        }

        /// <summary>
        /// Public disposer
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Private disposer
        /// </summary>
        /// <param name="disposing">True if user initiated</param>
        protected virtual void Dispose(bool disposing)
        {
            if (this.m_Disposed) return;
            if (disposing)
            {
//                if (this.m_QuickPollResultCollectionHelper != null)
//                {
//                    this.m_QuickPollResultCollectionHelper.Dispose();
//                }
            }
            this.m_Disposed = true;
        }

        #endregion

        #region QuickPollResultCollectionHelper

        /// <summary>
        /// Helper class that listens to when new results are added to the 
        /// collection and creates a listener for each of these results.
        /// These listeners are responsible for sending any changes to the
        /// results from the student to the instructor.
        /// </summary>
        private class QuickPollResultCollectionHelper : PropertyCollectionHelper
        {
            private readonly QuickPollWebService m_Service;

            public QuickPollResultCollectionHelper(QuickPollWebService service)
                : base(service.m_Sender, service.m_QuickPoll, "QuickPollResults")
            {
                this.m_Service = service;
                base.Initialize();
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    foreach (IDisposable disposable in this.Tags)
                        if (disposable != null)
                            disposable.Dispose();
                base.Dispose(disposing);
            }

            protected override object SetUpMember(int index, object member)
            {
//                QuickPollResultModel result = ((QuickPollResultModel)member);

//                Group receivers = Group.Submissions;
//                this.SendPollResultInformation(result, receivers);

//                return new QuickPollResultNetworkService(this.m_Service.m_Sender, this.m_Service.m_Presentation, this.m_Service.m_QuickPoll, result);
                return null;
            }

            protected override void TearDownMember(int index, object member, object tag)
            {
//                QuickPollResultNetworkService service = ((QuickPollResultNetworkService)tag);

//                if (service != null)
//                {
//                    QuickPollResultModel result = service.Result;
//                    service.Dispose();

                    // Note: Do not need to send a removed message here since there should be no 
                    // case where a result is removed by a student (the student can abstain from 
                    // voting)
//                }
            }
        }

        #endregion
    }
}
#endif