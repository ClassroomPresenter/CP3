#if WEBSERVER
using System;

using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;
using System.IO;

using UW.ClassroomPresenter.Web.Model;
using Katana;

namespace UW.ClassroomPresenter.Web {
    /// <summary>
    /// Singleton class the provides the interface between Katana and the rest
    /// of Presenter. 
    /// 
    /// This class is responsible for keeping track of the current state of
    /// Presenter so that it can keep the various web clients updated 
    /// and respond to their requests. Also, it is responsible for
    /// handling student submissions, quick poll events, pings, and log dumps
    /// from those clients and making them appear to be coming from native
    /// clients.
    /// </summary>
    public class WebService : IDisposable {
        #region Static Members

        /// <summary>
        /// Static singleton instance of this class for use by various
        /// static methods.
        /// </summary>
        public static WebService Instance = null;

        /// <summary>
        /// The local directory to use as the web root for this Katana
        /// application.
        /// </summary>
        public static string WebRoot = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "UW CSE\\Classroom Presenter 3\\Website\\");

        #endregion
        
        #region Properties

        // These constants represent the different post actions that we might receive.
        private const string STUDENT_SUBMISSION_REQUEST = "0";
        private const string QUICK_POLL_REQUEST = "1";
        private const string PING_REQUEST = "2";
        private const string LOG_DUMP_REQUEST = "3";

        /// <summary>
        /// Single global static representation of the current model.
        /// NOTE: This also acts as a lock to control access between the various
        ///     threads.
        /// </summary>
        public SimpleWebModel GlobalModel = new SimpleWebModel();

        /// <summary>
        /// A history of the changes to the SimpleWebModel so 
        /// that we can provide deltas to clients instead of copies of 
        /// the entire model.
        /// </summary>
        public SimpleModelHistory History = new SimpleModelHistory();

        /// <summary>
        /// Lock object used only to synchronize access for updating the model.
        /// </summary>
        public Object UpdateModelLock = new Object();

        /// <summary>
        /// Lock object for threads waiting for the history to update.
        /// </summary>
        public Object RequestSignalLock = new Object();

        /// <summary>
        /// Stream for logging for server state changes.
        /// </summary>
        /// TODO(cmprince): Figure out the best way to do server-side logging.
        private StreamWriter sw = new StreamWriter(
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CP_ServerPerformanceLog.txt"));

        #endregion

        #region Events

        /// <summary>
        /// Event for when a student submission is received from a web client
        /// </summary>
        public event SSEventHandler SubmissionReceived;

        /// <summary>
        /// Delegate for the handler for the SubmissionReceived event.
        /// </summary>
        /// <param name="source">The source of the student submission.</param>
        /// <param name="deck">The deck index that the student submission was from.</param>
        /// <param name="slide">The slide index that the student submission was from.</param>
        /// <param name="strokes">List of strokes that make up the student submission,
        /// as SimpleWebInk objects.</param>
        public delegate void SSEventHandler(object source, int deck, int slide, ArrayList strokes);

        /// <summary>
        /// Event for when a quick poll submission is received from a
        /// web client
        /// </summary>
        public event QPEventHandler QuickPollReceived;

        /// <summary>
        /// Delegate for tha handler for the QuickPollReceived event.
        /// </summary>
        /// <param name="source">THe source of the quick poll.</param>
        /// <param name="ownerId">The ID of the client</param>
        /// <param name="val">The value of the quick poll.</param>
        public delegate void QPEventHandler(object source, Guid ownerId, string val);

        #endregion

        #region Construction

        /// <summary>
        /// Constructor for this singleton web service.
        /// </summary>
        /// <param name="root">The web root where we want the web pages to be
        /// served from.</param>
        /// <param name="port">UNUSED: The port where we want clients to access
        /// Presenter from.</param>
        public WebService(string root, string port) {
            if (WebService.Instance == null) {
                // TODO(cmprince): Catch exceptions here and handle them appropriately.

                // Get the source web directory.
                string webDir = //System.IO.Path.Combine(
                //    System.Windows.Forms.Application.StartupPath, 
                //    "Web\\Website\\");
                    System.IO.Path.GetFullPath(
                    "C:\\Users\\cmprince\\prog\\school\\www\\school\\cp3client\\");
                DirectoryInfo sourceDir = new DirectoryInfo(webDir);

                // Get the target web directory.
                if (root != null) {
                    WebService.WebRoot = root;
                }
                string localWebDir = WebService.WebRoot;
                DirectoryInfo targetDir = new DirectoryInfo(localWebDir);

                // Copy over the local files if they are newer.
                CopyAllIfNewer(sourceDir, targetDir);

                // Create the Katana server.
                WebServer.Create("/cp", WebService.WebRoot);
                WebServer.RequestBuilder = this.RequestBuilder;
                WebServer.PostHandler = this.PostHandler;

                // Save the singleton instance.
                WebService.Instance = this;
            } else {
                throw new Exception("Attempting to create multiple instances of the singleton WebService.");
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Recursively copy all files and directories between source and
        /// target, if and only if the file in the source are newer.
        /// </summary>
        /// <param name="source">The source directory.</param>
        /// <param name="target">The target directory.</param>
        private static void CopyAllIfNewer(DirectoryInfo source, DirectoryInfo target) {
            // Create the target if it doesn't exist
            if (Directory.Exists(target.FullName) == false) {
                Directory.CreateDirectory(target.FullName);
            }

            // Copy each file into the new directory
            if (Directory.Exists(source.FullName)) {
                foreach (FileInfo file in source.GetFiles()) {
                    // Get the destination file name
                    string destinationFilename = Path.Combine(target.ToString(), file.Name);

                    // Get the destination file access time
                    DateTime targetFiletime = (File.Exists(destinationFilename)) ?
                        File.GetLastWriteTime(destinationFilename) :
                        DateTime.MinValue;

                    // Overwrite the target file if it is older than the new file
                    if (targetFiletime < file.LastWriteTime) {
                        file.CopyTo(destinationFilename, true);
                    }
                }

                // Recursively copy the subdirectories
                foreach (DirectoryInfo nextSource in source.GetDirectories()) {
                    DirectoryInfo nextTarget = target.CreateSubdirectory(nextSource.Name);
                    CopyAllIfNewer(nextSource, nextTarget);
                }
            }
        }

        /// <summary>
        /// Helper method to convert from windows DateTime object to unix time.
        /// </summary>
        /// <param name="date">DateTime object to convert</param>
        /// <returns>The unix timestamp representing the converted object.</returns>
        private static long ToUnixTime(DateTime date) {
            DateTime current = date.ToUniversalTime();
            return Convert.ToInt64((current.Ticks - 621355968000000000) / 10000);
            //DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).ToLocalTime();
            //return Convert.ToInt64((date - epoch).TotalMilliseconds);
        }

        #endregion

        #region Send and Receive

        /// <summary>
        /// Update the model history with this model. This method should be called after modifying
        /// the global model in order to create another entry in the model history. We chose to
        /// have this be a separate method so that multiple changes to the model could be batched
        /// up into a single history entry.
        /// </summary>
        public void UpdateModel() {
            int sequenceNumber;
            lock (WebService.Instance.UpdateModelLock) {
                // Add the model diff.
                SimpleWebModel clonedModel;
                lock (WebService.Instance.GlobalModel) {
                    clonedModel = (SimpleWebModel)WebService.Instance.GlobalModel.Clone();
                }
                sequenceNumber = this.History.AddModel(clonedModel);
            }

            // Logging the timestamp that the model was updated.
            string m = this.History.GetModelDiff(sequenceNumber - 1, sequenceNumber);
            this.LogModelDiff(sequenceNumber - 1, sequenceNumber, m);

            // We pulse the monitor here because one or more threads could be waiting for model
            // updates.
            // TODO(cmprince): If this doesn't work we can use the main event queue to pulse the monitor only after we are idle.
            Monitor.Enter(WebService.Instance.RequestSignalLock);
            try {
                Monitor.PulseAll(WebService.Instance.RequestSignalLock);
            }
            finally {
                Monitor.Exit(WebService.Instance.RequestSignalLock);
            }
        }

        /// <summary>
        /// Helper server push requests from the web clients.
        /// </summary>
        /// <param name="parameters">The query string parameters.</param>
        /// <returns>The response text.</returns>
        private string RequestBuilder(NameValueCollection parameters) {
            string json = "{}";

            // Only service requests that contain a sequence number.
            if (parameters["e"] != null) {
                int startSeqNum = Convert.ToInt32(parameters["e"]);

                if (startSeqNum >= this.History.SequenceNumber) {
                    // Wait if there is not a newer version of the model available (but only up to 5 seconds).
                    Monitor.Enter(this.RequestSignalLock);
                    try {
                        Monitor.Wait(this.RequestSignalLock, 5000);
                    }
                    finally {
                        Monitor.Exit(this.RequestSignalLock);
                    }
                    // Sleep to allow other threads to exit the Monitor as well.
                    Thread.Sleep(0);
                }

                // Create the model diff.
                int endSeqNum = this.History.SequenceNumber;
                try {
                    json = this.History.GetModelDiff(startSeqNum, endSeqNum);
                } catch (ArgumentException) {
                    json = "{}";
                }

                // Logging
                int client = -1;
                if (parameters["id"] != null) {
                    client = Convert.ToInt32(parameters["id"]);
                }
                this.LogModelSend(client, startSeqNum, endSeqNum, json);
            }
            return json;
        }

        /// <summary>
        /// Handle data posted to up by th web client.
        /// </summary>
        /// <param name="parameters">The query string parameters.</param>
        /// <param name="postData">The post data string.</param>
        /// <returns>The response text.</returns>
        private string PostHandler(NameValueCollection parameters, string postData) {
            // Get the parts of the post data.
            string[] items = postData.Split(new char[] { '|' });

            if (items[0] == STUDENT_SUBMISSION_REQUEST) { // Handle student submission.
                // Deserialize the Array of SimpleStrokeModel from the post data.
                int clientId = Convert.ToInt32(items[1]);
                int deck = Convert.ToInt32(items[2]);
                int slide = Convert.ToInt32(items[3]);
                object strokes = JSON.Decode(items[4]);
                if (strokes is List<object>) {
                    ArrayList deserializedStrokes = new ArrayList();

                    List<object> strokesArray = (List<object>)strokes;
                    foreach (object stroke in strokesArray) {
                        if (stroke is List<KeyValuePair<string, object>>) {
                            SimpleWebInk deserializedStroke = new SimpleWebInk();

                            List<KeyValuePair<string, object>> strokeObject = (List<KeyValuePair<string, object>>)stroke;
                            foreach (KeyValuePair<string, object> field in strokeObject) {
                                switch (field.Key) {
                                    case "c": // Color
                                        if (field.Value is List<KeyValuePair<string, object>>) {
                                            List<KeyValuePair<string, object>> color = (List<KeyValuePair<string, object>>)field.Value;
                                            foreach (KeyValuePair<string, object> channel in color) {
                                                if (channel.Key == "r") {
                                                    deserializedStroke.R = (byte)Math.Round((double)channel.Value);
                                                }
                                                else if (channel.Key == "g") {
                                                    deserializedStroke.G = (byte)Math.Round((double)channel.Value);
                                                }
                                                else if (channel.Key == "b") {
                                                    deserializedStroke.B = (byte)Math.Round((double)channel.Value);
                                                }
                                            }
                                        }
                                        break;
                                    case "w": // Width
                                        if (field.Value is double) {
                                            deserializedStroke.Width = (float)((double)field.Value);
                                        }
                                        break;
                                    case "o": // Opacity
                                        if (field.Value is double) {
                                            deserializedStroke.Opacity = (byte)Math.Round((double)field.Value);
                                        }
                                        break;
                                    case "k": // Points
                                        if (field.Value is List<object>) {
                                            List<object> points = (List<object>)field.Value;

                                            int numPoints = points.Count / 2;
                                            System.Drawing.Point[] pts = new System.Drawing.Point[numPoints];
                                            for (int i = 0; i < numPoints; i++) {
                                                pts[i].X = (int)((double)points[(i*2)] * 26.37f);
                                                pts[i].Y = (int)((double)points[(i*2)+1] * 26.37f);
                                            }

                                            deserializedStroke.Pts = pts;
                                        }
                                        break;
                                }
                            }

                            deserializedStrokes.Add(deserializedStroke);
                        }
                    }

                    // Notify listeners that a student submission was received.
                    WebService.Instance.SubmissionReceived(WebServer.Instance, deck, slide, deserializedStrokes);
                }
                // Log that we responded to a pong.
                this.LogSubmissionReceived(clientId, deck, slide);
            } else if (items[0] == QUICK_POLL_REQUEST) { // Handle quick poll.
                // Notify listeners that a quick poll update was received.
                WebService.Instance.QuickPollReceived(WebServer.Instance,
                    new Guid(items[1]), items[2]);
            } else if (items[0] == PING_REQUEST) { // Handle ping request.
                String clientId = items[1];
                String clientTimestamp = items[2];
                String serverTimestamp = WebService.ToUnixTime(System.DateTime.Now).ToString();

                // We should respond with the current timestamp.
                String pong = "{\"u\":" + clientId +
                    ",\"c\":" + clientTimestamp +
                    ",\"s\":" + serverTimestamp + "}";

                // Log that we responded to a pong.
                this.LogPong(Convert.ToInt32(clientId), pong);
                return pong;
            } else if (items[0] == LOG_DUMP_REQUEST) { // Handle a client log dump.
                String clientId = items[1];
                String logDump = items[2];

                // Print the dumped log to a file.
                StreamWriter logFile = new StreamWriter(
                    System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                    "CP_ClientPerformanceLog_" + clientId.ToString() + ".txt"));
                logFile.Write(logDump);
                logFile.Close();
            }
            // By default, return an empty json object.
            return "{}";
        }

        #endregion

        #region Logging

        /// <summary>
        /// Add a log entry for when the model is updated.
        /// </summary>
        /// <param name="first">The old sequence number.</param>
        /// <param name="second">The new sequence number.</param>
        /// <param name="data">The model diff data.</param>
        private void LogModelDiff(int first, int second, String data) {
            // Log the model update to file so that we can calculate performance metrics later.
            // NOTE: Log format is: <SERVER_TIME>, <CLIENT>, <TYPE>, <START_SEQ>, <END_SEQ>, <DATA>
            StringBuilder logEntry = new StringBuilder();
            logEntry.Append(WebService.ToUnixTime(System.DateTime.Now).ToString());
            logEntry.Append(",");
            logEntry.Append(0);
            logEntry.Append(",");
            logEntry.Append("'MODEL_DIFF'");
            logEntry.Append(",");
            logEntry.Append(first);
            logEntry.Append(",");
            logEntry.Append(second);
            logEntry.Append(",");
            logEntry.Append("'");
            logEntry.Append(data);
            logEntry.Append("'");

            if (sw != null) {
                lock (sw) {
                    sw.WriteLine(logEntry.ToString());
                }
            }
        }

        /// <summary>
        /// Add a log entry for when an update is sent to a client.
        /// </summary>
        /// <param name="client">The id of the client.</param>
        /// <param name="first">The first sequence number requested.</param>
        /// <param name="second">The current sequence number.</param>
        /// <param name="data">The model diff.</param>
        private void LogModelSend(int client, int first, int second, String data) {
            // Log the model update to file so that we can calculate performance metrics later.
            // NOTE: Log format is: <SERVER_TIME>, <CLIENT>, <TYPE>, <START_SEQ>, <END_SEQ>, <DATA>
            StringBuilder logEntry = new StringBuilder();
            logEntry.Append(WebService.ToUnixTime(System.DateTime.Now).ToString());
            logEntry.Append(",");
            logEntry.Append(client);
            logEntry.Append(",");
            logEntry.Append("'MODEL_SEND'");
            logEntry.Append(",");
            logEntry.Append(first);
            logEntry.Append(",");
            logEntry.Append(second);
            logEntry.Append(",");
            logEntry.Append("'");
            logEntry.Append(data);
            logEntry.Append("'");

            if (sw != null) {
                lock (sw) {
                    sw.WriteLine(logEntry.ToString());
                }
            }
        }

        /// <summary>
        /// Add a log entry for when we send a pong back to a client.
        /// </summary>
        /// <param name="client">The id of the client.</param>
        /// <param name="data">The response data.</param>
        private void LogPong(int client, String data) {
            // Log the model update to file so that we can calculate performance metrics later.
            // NOTE: Log format is: <SERVER_TIME>, <CLIENT>, <TYPE>, <START_SEQ>, <END_SEQ>, <DATA>
            StringBuilder logEntry = new StringBuilder();
            logEntry.Append(WebService.ToUnixTime(System.DateTime.Now).ToString());
            logEntry.Append(",");
            logEntry.Append(client);
            logEntry.Append(",");
            logEntry.Append("'PONG'");
            logEntry.Append(",");
            logEntry.Append(-1);
            logEntry.Append(",");
            logEntry.Append(-1);
            logEntry.Append(",");
            logEntry.Append("'");
            logEntry.Append(data);
            logEntry.Append("'");

            if (sw != null) {
                lock (sw) {
                    sw.WriteLine(logEntry.ToString());
                }
            }
        }

        /// <summary>
        /// Add a log entry for when we receive a submission from clients.
        /// </summary>
        /// <param name="client">The id of the client.</param>
        /// <param name="deck">The deck index.</param>
        /// <param name="slide">The slide index.</param>
        private void LogSubmissionReceived(int client, int deck, int slide) {
            // NOTE: Log format is: <SERVER_TIME>, <CLIENT>, <DECK_INDEX>, <SLIDE_INDEX>
            StringBuilder logEntry = new StringBuilder();
            logEntry.Append(WebService.ToUnixTime(System.DateTime.Now).ToString());
            logEntry.Append(",");
            logEntry.Append(client);
            logEntry.Append(",");
            logEntry.Append("'SUBMISSION'");
            logEntry.Append(",");
            logEntry.Append(deck);
            logEntry.Append(",");
            logEntry.Append(slide);

            if (sw != null) {
                lock (sw) {
                    sw.WriteLine(logEntry.ToString());
                }
            }
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// Dispose of this object.
        /// </summary>
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Finalizer to ensure resources are disposed.
        /// </summary>
        ~WebService() {
            Dispose(false);
        }

        /// <summary>
        /// Internal helper to dispose of all managed and unmanaged
        /// resources.
        /// </summary>
        /// <param name="disposing">True if manually disposing.</param>
        protected virtual void Dispose(bool disposing) {
            if (disposing) {
                // Free mananged resources (if any)
                WebServer.Instance.Dispose();
                lock (sw) {
                    sw.Flush();
                    sw.Close();
                }
                sw = null;
            }
        }

        #endregion
    }
}
#endif