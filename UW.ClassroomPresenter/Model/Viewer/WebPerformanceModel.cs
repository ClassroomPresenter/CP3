using System;
using System.Threading;
using System.Collections;
using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Misc;

namespace UW.ClassroomPresenter.Model.Viewer {
    /// <summary>
    /// Encapsulates all of the properties needed to conduct diagnoses and testing of the 
    /// application
    /// </summary>
    public class WebPerformanceModel : PropertyPublisher, ICloneable {
        private int m_RequestSubmissionSignal;
        private int m_RequestLogSignal;

        /// <summary>
        /// Request a submission from web clients.
        /// </summary>
        [Published]
        public int RequestSubmissionSignal {
            get { return this.GetPublishedProperty("RequestSubmissionSignal", ref this.m_RequestSubmissionSignal); }
            set { this.SetPublishedProperty("RequestSubmissionSignal", ref this.m_RequestSubmissionSignal, value); }
        }

        /// <summary>
        /// Request a log from web clients.
        /// </summary>
        [Published]
        public int RequestLogSignal {
            get { return this.GetPublishedProperty("RequestLogSignal", ref this.m_RequestLogSignal); }
            set { this.SetPublishedProperty("RequestLogSignal", ref this.m_RequestLogSignal, value); }
        }

        /// <summary>
        /// Instantiates a new <see cref="WebPerformanceModel"/>.
        /// </summary>
        public WebPerformanceModel() {
            this.m_RequestSubmissionSignal = 0;
            this.m_RequestLogSignal = 0;
        }

        #region ICloneable Members

        /// <summary>
        /// Clones this model object.
        /// </summary>
        /// <returns>A deep copy of this object.</returns>
        public object Clone() {
            WebPerformanceModel clonedModel = new WebPerformanceModel();
            clonedModel.m_RequestSubmissionSignal = this.m_RequestSubmissionSignal;
            clonedModel.m_RequestLogSignal = this.m_RequestLogSignal;

            return clonedModel;
        }

        #endregion
    }
}
