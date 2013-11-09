using System;
using System.Collections;
using System.Text;

namespace UW.ClassroomPresenter.Diagnostics {
    /// <summary>
    /// File format to store logs from each machine
    /// </summary>
    [Serializable]
    public class NetworkPerformanceFile {
        /// <summary>
        /// Information we want to store in the log
        /// </summary>
        public readonly Guid ParticipantID;
        public readonly ArrayList LatencyEvents;
        public readonly ArrayList SkewEvents;
        public readonly ArrayList NetworkLogMessages;
        public readonly string MachineName;
        public readonly string UserName;

        /// <summary>
        /// Construct the log file
        /// </summary>
        /// <param name="id">The participant id for this machine</param>
        /// <param name="lat">The latency events</param>
        /// <param name="skew">The skew entries</param>
        /// <param name="log">The trace of network messages</param>
        public NetworkPerformanceFile( Guid id, ArrayList lat, ArrayList skew, ArrayList log ) {
            this.ParticipantID = id;
            this.LatencyEvents = lat;
            this.SkewEvents = skew;
            this.NetworkLogMessages = log;
            this.MachineName = System.Environment.MachineName;
            this.UserName = System.Environment.UserName;
        }
    }
}
