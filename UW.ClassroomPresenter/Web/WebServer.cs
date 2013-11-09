using System;

using System.Collections;
using System.Text;
using System.Runtime.InteropServices;

using UW.ClassroomPresenter.Web.Model;

namespace UW.ClassroomPresenter.Web
{
    /// <summary>
    /// Class encapsulating the functions required by the web server
    /// Internally uses the mongoose web server
    /// </summary>
    public class WebServer : IDisposable
    {
        #region Static Members

        /// <summary>
        /// Single global static representation of the model
        /// </summary>
        public static SimpleWebModel GlobalModel = new SimpleWebModel();
        
        #endregion

        #region Private Members

        /// <summary>
        /// The context being used by the server
        /// </summary>
        private IntPtr ServerContext = IntPtr.Zero;
        /// <summary>
        /// An array of bindings to handle special URIs
        /// </summary>
        private ArrayList Bindings = new ArrayList();

        /// <summary>
        /// Special structure that represents a binding
        /// NOTE: We need to keep this information around
        ///       so that we can clean up memory properly
        /// </summary>
        private struct PageBinding {
            /// <summary>
            /// The path that is being handled
            /// </summary>
            public string Path;
            /// <summary>
            /// The unmanaged memory that contains the path string
            /// </summary>
            public IntPtr ptrPath;
            /// <summary>
            /// A reference to the handler method so that we don't garbage collect it
            /// </summary>
            public mg_callback_t cbHandler;
//            public IntPtr cbHandler;
        }

        #endregion

        #region Construction

        /// <summary>
        /// Constructor for the web-server
        /// </summary>
        /// <param name="root">The full path to the directory to use as the root 
        /// of the web directory</param>
        /// <param name="port">The string representing the port number to listen on</param>
        public WebServer( string root, string port )
        {
            // Create the server
            this.ServerContext = mg_start();
            mg_set_option( this.ServerContext, "ports", port );
            mg_set_option( this.ServerContext, "root", root );

            // Create the binding
            this.CreateBinding("/vb", WebServer.UriHandler);
//            this.CreateBinding("/full", WebServer.UriHandler2);
        }

        #endregion

        #region HTML Building

        /// <summary>
        /// Build a string that represents the current model
        /// </summary>
        /// <param name="model">The model to build the HTML string representation of</param>
        /// <returns>The string that is built</returns>
        public static string BuildModelString(SimpleWebModel model)
        {
            string result = ""; 
            result += "<html>\n";
            result += "<body onload=\"parent.Presenter.Network.RecvFunc()\">\n";
            
            // Create the main div
            result += "<div id=\"S0\">\n";

        	result += "\t<div id=\"S0_PName\">" + model.Name + "</div>\n";
            result += "\t<div id=\"S0_IDeck\">" + model.CurrentDeck + "</div>\n";
	        result += "\t<div id=\"S0_ISlide\">" + (model.CurrentSlide-1) + "</div>\n";
	        result += "\t<div id=\"S0_Linked\">" + false + "</div>\n";
	        result += "\t<div id=\"S0_AllowSS\">" + true + "</div>\n";
	        result += "\t<div id=\"S0_Decks\">\n";
            for( int i=0; i<model.Decks.Count; i++ )
            {
                result += BuildDeckString( "S0_Decks", i, (SimpleWebDeck)model.Decks[i] );
            }
            result += "\t</div>\n";
            
            result += "</div>\n";

            result += "</body>\n";
            result += "</html>\n";

            return result;
        }

        protected static string BuildDeckString(string prefix, int index, SimpleWebDeck deck)
        {
            string newPrefix = prefix + "_" + index;
            string result = "";
            result += "\t\t<div id=\"" + newPrefix + "\">\n";
            result += "\t\t\t<div id=\"" + newPrefix + "_Index\">" + index + "</div>\n";
            result += "\t\t\t<div id=\"" + newPrefix + "_Name\">" + deck.Name + "</div>\n";
            result += "\t\t\t<div id=\"" + newPrefix + "_Slides\">\n";
            for (int i = 0; i < deck.Slides.Count; i++)
            {
                result += BuildSlideString(newPrefix, deck.Name, i, (SimpleWebSlide)deck.Slides[i]);
            }
            result += "\t\t\t</div>\n";
            result += "\t\t</div>\n";

            return result;
        }

        protected static string BuildSlideString(string prefix, string deckName, int index, SimpleWebSlide slide)
        {
            string newPrefix = prefix + "_" + index;
            string result = "";
            result += "\t\t\t\t<div id=\"" + newPrefix + "\">\n";
            result += "\t\t\t\t\t<div id=\"" + newPrefix + "_Index\">" + index + "</div>\n";
            result += "\t\t\t\t\t<div id=\"" + newPrefix + "_Name\">" + slide.Name + "</div>\n";
            result += "\t\t\t\t\t<div id=\"" + newPrefix + "_Image\">" + "./images/" + deckName + "/" + deckName + "/" + deckName + "_" + String.Format( "{0:000}", index+1 ) + ".png" + "</div>\n";
            result += "\t\t\t\t</div>\n";
            return result;
        }

        public string BuildModelDiffString(SimpleWebModel oldModel, SimpleWebModel newModel) {
            // Go recursive, only add if there is a change within
            return "";
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// Dispose simply called Dispose(true)
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// NOTE: Leave out the finalizer altogether if this class doesn't 
        /// own unmanaged resources itself, but leave the other methods
        /// exactly as they are. 
        ~WebServer() 
        {
            // Finalizer calls Dispose(false)
            Dispose(false);
        }

        /// <summary>
        /// The bulk of the clean-up code is implemented here
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing) 
            {
                // Free mananged resources (if any)
            }

            // Clean up all allocated memory
            foreach( PageBinding b in this.Bindings )
            {
                Marshal.FreeHGlobal(b.ptrPath);
            }
            this.Bindings.Clear();

            // Stop the web server
            mg_stop(this.ServerContext);
            this.ServerContext = IntPtr.Zero;
        }

        #endregion

        #region Bindings

        /// <summary>
        /// Create a binding between a special web page and the server result
        /// </summary>
        /// <param name="pathRegex">A regular expression that is matched with
        /// the URI path</param>
        /// <param name="handler">The function that is called when a match is 
        /// found</param>
        public void CreateBinding(string pathRegex, mg_callback_t handler)
        {
            // Create the binding
            PageBinding binding;
            binding.Path = pathRegex;
            binding.ptrPath = Marshal.StringToHGlobalAnsi(pathRegex);
            binding.cbHandler = new mg_callback_t(handler);
//            binding.cbHandler = Marshal.GetFunctionPointerForDelegate( new mg_callback_t(handler) );

            // Add the binding to the array
            this.Bindings.Add(binding);
            // Add the binding to mongoose
            mg_bind_to_uri(this.ServerContext, binding.ptrPath, binding.cbHandler, IntPtr.Zero);
        }

        /// <summary>
        /// Handle a special URI path
        /// </summary>
        /// <param name="conn">The connection that we are working with</param>
        /// <param name="request_info">The request information</param>
        /// <param name="user_data">Custom data that we passed in</param>
        public static void UriHandler( IntPtr conn, /*mg_request_info*/IntPtr request_info, IntPtr user_data ) {
/*            string output = "HTTP/1.0 200 OK\n" +
                            "Date: Sun, Feb 01 02:15:59 PST\n" +
                            "Content-Type: text/html\n\n" +
                            "<html>\n" +
                            "<body>\n";
            output += "Hello from VB!<br>";
            output += "Method: " + request_info.request_method + "<br>";
            output += "URI: " + request_info.uri + "<br>";
            output += "HTTP headers:<br>";
            for( int i=0; i < request_info.num_headers; i++ ) {
                output += "  " + request_info.http_headers[i].name + ": " + request_info.http_headers[i].value + "<br>";
            }
            output += "Query: " + request_info.query_string + "<br>";
            output += "</body>\n</html>\n";
            int result = mg_write( conn, output, output.Length );
            System.Diagnostics.Debug.WriteLine(result);
*/
            string output = "HTTP/1.0 200 OK\n" +
                            "Date: Sun, Feb 01 02:15:59 PST\n" +
                            "Content-Type: text/html\n\n";
            if (WebServer.GlobalModel != null)
            {
                output += BuildModelString(WebServer.GlobalModel);
            }
            mg_write(conn, output, output.Length);
        }

        /// <summary>
        /// Handle a special URI path
        /// </summary>
        /// <param name="conn">The connection that we are working with</param>
        /// <param name="request_info">The request information</param>
        /// <param name="user_data">Custom data that we passed in</param>
        public static void UriHandler2(IntPtr conn, ref mg_request_info request_info, IntPtr user_data)
        {
            string output = "HTTP/1.0 200 OK\n" +
                            "Date: Sun, Feb 01 02:15:59 PST\n" +
                            "Content-Type: text/html\n\n";
            if( WebServer.GlobalModel != null ) {
                output += BuildModelString( WebServer.GlobalModel );
            }
            mg_write(conn, output, output.Length);
        }

        #endregion

        #region Mongoose Interop

        // Structure representing a part of the HTTP header
        [StructLayout(LayoutKind.Sequential, Pack=1)]
        public struct mg_http_header
        {
	        public string name;
	        public string value;
        }

        // Structure representing an HTTP request
        [StructLayout(LayoutKind.Sequential, Pack=1)]
        public struct mg_request_info
        {
	        public string request_method;
	        public string uri;
	        public string query_string;
	        public string post_data;
	        public string remote_user;
	        public int remote_ip;
	        public int remote_port;
	        public int post_data_len;
	        public int http_version_minor;
	        public int http_version_major;
	        public int status_code;
            public int num_headers;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst=64)]
            public mg_http_header[] http_headers;
        }

        // Function delegate for handling the callback when a special path has been entered
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void mg_callback_t( IntPtr ctx, IntPtr request_info, IntPtr user_data );

        // Start the mongoose web server
        [DllImport("mongoose.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr mg_start();
        // Stop the mongoose web server
        [DllImport("mongoose.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void mg_stop( IntPtr ctx );
        // Set a mongoose server option
        [DllImport("mongoose.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int mg_set_option( IntPtr ctx, string opt_name, string opt_value );
        // Bind a given url to a handler
        [DllImport("mongoose.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void mg_bind_to_uri(IntPtr ctx, IntPtr uri_regex, mg_callback_t func, IntPtr user_data );
       // Write to the page output stream
        [DllImport("mongoose.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int mg_write( IntPtr conn, [MarshalAs(UnmanagedType.LPStr)]string fmt, int len );

        #endregion
    }
}
