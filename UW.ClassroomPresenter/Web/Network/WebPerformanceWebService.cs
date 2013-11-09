#if WEBSERVER
using System;
using System.Collections.Generic;
using System.Text;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Viewer;
using UW.ClassroomPresenter.Network.Messages;

namespace UW.ClassroomPresenter.Web.Network {
    /// <summary>
    /// Handles changes done to the 
    /// </summary>
    public class WebPerformanceWebService : IDisposable {
        #region Members

        /// <summary>
        /// The event queue to use.
        /// </summary>
        private readonly SendingQueue m_Sender;

        /// <summary>
        /// The web performance model to listen to.
        /// </summary>
        private readonly WebPerformanceModel m_WebPerformance;

        /// <summary>
        /// Whether or not this class has been disposed yet.
        /// </summary>
        private bool m_Disposed;

        /// <summary>
        /// Helper for when the RequestSubmissionSignal changes.
        /// </summary>
        private readonly EventQueue.PropertyEventDispatcher m_RequestSubmissionChangedDispatcher;

        /// <summary>
        /// Helper for when the RequestLogSignal changes.
        /// </summary>
        private readonly EventQueue.PropertyEventDispatcher m_RequestLogChangedDispatcher;

        #endregion

        #region Constructors

        /// <summary>
        /// Constructor for the web service.
        /// </summary>
        /// <param name="sender">The event queue to use.</param>
        /// <param name="presentation">The web performance model to listen to.</param>
        public WebPerformanceWebService(SendingQueue sender, WebPerformanceModel performance) {
            this.m_Sender = sender;
            this.m_WebPerformance = performance;

            this.m_RequestSubmissionChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_Sender, new PropertyEventHandler(this.HandleRequestSubmissionChanged));
            this.m_WebPerformance.Changed["RequestSubmissionSignal"].Add(this.m_RequestSubmissionChangedDispatcher.Dispatcher);
            this.m_RequestLogChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_Sender, new PropertyEventHandler(this.HandleRequestLogChanged));
            this.m_WebPerformance.Changed["RequestLogSignal"].Add(this.m_RequestLogChangedDispatcher.Dispatcher);
        }

        #endregion

        #region IDisposable Members

        /// <summary>
        /// Finalizer
        /// </summary>
        ~WebPerformanceWebService() {
            this.Dispose(false);
        }

        /// <summary>
        /// Public disposer
        /// </summary>
        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Private disposer
        /// </summary>
        /// <param name="disposing">True if user initiated</param>
        protected virtual void Dispose(bool disposing) {
            if (this.m_Disposed) return;
            if (disposing) {
                this.m_WebPerformance.Changed["RequestSubmissionSignal"].Remove(this.m_RequestSubmissionChangedDispatcher.Dispatcher);
                this.m_WebPerformance.Changed["RequestLogSignal"].Remove(this.m_RequestLogChangedDispatcher.Dispatcher);
            }
            this.m_Disposed = true;
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handle the request submission signal changing.
        /// </summary>
        /// <param name="sender">The object sending this event.</param>
        /// <param name="args">The arguments for this event.</param>
        private void HandleRequestSubmissionChanged(object sender, PropertyEventArgs args) {
            int requestSubmissionSignalValue = 0;
            if (this.m_WebPerformance != null) {
                using (Synchronizer.Lock(this.m_WebPerformance.SyncRoot)) {
                    requestSubmissionSignalValue = this.m_WebPerformance.RequestSubmissionSignal;
                }
            }

            lock (WebService.Instance.GlobalModel) {
                WebService.Instance.GlobalModel.RequestSubmissionSignal = requestSubmissionSignalValue;
            }
            WebService.Instance.UpdateModel();
        }

        /// <summary>
        /// Handle the request log signal changing.
        /// </summary>
        /// <param name="sender">The object sending this event.</param>
        /// <param name="args">The arguments for this event.</param>
        private void HandleRequestLogChanged(object sender, PropertyEventArgs args) {
            int requestLogSignalValue = 0;
            if (this.m_WebPerformance != null) {
                using (Synchronizer.Lock(this.m_WebPerformance.SyncRoot)) {
                    requestLogSignalValue = this.m_WebPerformance.RequestLogSignal;
                }
            }

            lock (WebService.Instance.GlobalModel) {
                WebService.Instance.GlobalModel.RequestLogSignal = requestLogSignalValue;
            }
            WebService.Instance.UpdateModel();
        }

        #endregion
    }
}
#endif