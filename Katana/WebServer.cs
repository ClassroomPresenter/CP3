using System;
using System.Text;
using System.Collections;
using System.Collections.Specialized;
using System.Runtime.InteropServices;
using System.Web;

using KatanaAppCSharp;

namespace Katana {
    /// <summary>
    /// Singleton class encapsulating the functions required by a
    /// Katana-enabled web server.
    /// 
    /// NOTE: Acts as a wrapper around the KatanaAppCSharp Library.
    /// </summary>
    public sealed class WebServer : IDisposable {
        #region Private Members

        /// <summary>
        /// Internal singleton instance of the web server.
        /// </summary>
        private static WebServer instance = null;

        /// <summary>
        /// Lock object to make this class threadsafe. Used to ensure
        /// only one thread is attempting to create a WebServer at once.
        /// </summary>
        private static readonly object padlock = new object();

        /// <summary>
        /// The context being used for Katana.
        /// </summary>
        private Context ctx = new Context();
        
        #endregion

        #region Construction

        /// <summary>
        /// Private constructor for creating the web server.
        /// </summary>
        private WebServer(string webRoot, string localRoot) {
            // TODO CMPRINCE: More robustly ensure that there is a Katana server running
            // before we attempt to connect to one.
            this.ctx.Create();
            this.ctx.Connect(0);
            this.ctx.SetRoot(webRoot, localRoot);

            // For now, we hardcode exactly two dynamic request handlers.
            // SendingHandler is for posts from the web client to the server.
            // ReceivingHandler is for push notifications from the server to
            // the web client.
            this.ctx.SetBindUri(webRoot + "/post", new Context.RequestCallback(SendingHandler));
            this.ctx.SetBindUri(webRoot + "/recv", new Context.RequestCallback(ReceivingHandler));
        }

        /// <summary>
        /// Create the web server if one isn't already created.
        /// 
        /// NOTE: We acquire the lock in order to ensure that two threads do
        /// not try to create a web server at the same time.
        /// </summary>
        /// <param name="webRoot">The web path root we want static files
        /// to be served from for this application.</param>
        /// <param name="localRoot">The local path were we want to serve
        /// static files from for this application.</param>
        /// <returns></returns>
        public static bool Create(string webRoot, string localRoot) {
            lock (WebServer.padlock) {
                if (WebServer.instance == null) {
                    WebServer.instance = new WebServer(webRoot, localRoot);
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Get the WebServer instance.
        /// 
        /// NOTE: We acquire the lock to ensure we don't try to use the
        /// WebServer in another thread while it is still being created.
        /// </summary>
        public static WebServer Instance {
            get {
                lock (WebServer.padlock) {
                    return WebServer.instance;
                }
            }
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// Dispose of all resources.
        /// </summary>
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Finalizer to ensure we dispose of all resources.
        /// </summary>
        ~WebServer() {
            Dispose(false);
        }

        /// <summary>
        /// Internal helper to dispose of all managed and unmanaged
        /// resources related to Katana.
        /// </summary>
        /// <param name="disposing">True if we are manually disposing</param>
        private void Dispose(bool disposing) {
            // Disconnect from the Katana server and release the context.
            this.ctx.Disconnect();
            this.ctx.Destroy();
        }

        #endregion

        #region Connections

        /// <summary>
        /// Delegate definition for the function for handling a request for update
        /// from a Katana web client.
        /// </summary>
        /// <param name="parameters">The query string parameters.</param>
        /// <returns>The response text.</returns>
        public delegate string WebRequestBuilder(NameValueCollection parameters);

        /// <summary>
        /// This static delegate instance should be set by Katana-enabled applications for
        /// handling requests for updates from Katana web clients.
        /// </summary>
        public static WebRequestBuilder RequestBuilder = null;

        /// <summary>
        /// Delegate definition for the function for handling a request from a
        /// Katana web client.
        /// </summary>
        /// <param name="parameters">The query string paramters.</param>
        /// <param name="postData">The post data.</param>
        /// <returns>The response text.</returns>
        public delegate string WebPostHandler(NameValueCollection parameters, string postData);

        /// <summary>
        /// This static delegate instance should be set by Katana-enabled applications for
        /// handling requests from Katana web clients.
        /// </summary>
        public static WebPostHandler PostHandler = null;

        #endregion

        #region Send/Recv

        /// <summary>
        /// Handle the client posting data to us.
        /// NOTE: This needs to be a static function so that we can pass a
        /// pointer to it into the Katana dll.
        /// </summary>
        /// <param name="info">The socket information.</param>
        /// <returns>Success code.</returns>
        private bool SendingHandler(RequestInfo info) {
            NameValueCollection parameters = HttpUtility.ParseQueryString(info.GetQueryString());
            string postData = Encoding.ASCII.GetString(info.GetPostString());

            // Let the application handle the request by invoking the static PostHandler.
            string responseText = "{}";
            if (WebServer.PostHandler != null) {
                responseText = WebServer.PostHandler(parameters, postData);
            }

            // Construct and return the response text.
            string output = "HTTP/1.0 200 OK\n" +
                            "Date: Sun, Feb 01 02:15:59 PST\n" +
                            "Content-Type: application/json\n\n" +
                            responseText;
            info.SetResponse(output);
            return true;
        }

        /// <summary>
        /// Handle the client periodically polling for server push notifications.
        /// NOTE: This needs to be a static function so that we can pass a
        /// pointer to it into the Katana dll.
        /// </summary>
        /// <param name="info">The socket information.</param>
        /// <returns>The success code.</returns>
        private bool ReceivingHandler(RequestInfo info) {
            NameValueCollection parameters = HttpUtility.ParseQueryString(info.GetQueryString());

            // Let the application handle the request by invoking the static RequestBuilder.
            string responseText = "{}";
            if (WebServer.RequestBuilder != null) {
                responseText = WebServer.RequestBuilder(parameters);
            }

            // Construct and return the response text.
            string output = "HTTP/1.0 200 OK\n" +
                            "Date: Sun, Feb 01 02:15:59 PST\n" +
                            "Content-Type: application/json\n\n" +
                            responseText;
            info.SetResponse(output);
            return true;
        }

        #endregion
    }
}
