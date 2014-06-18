using System;
using Microsoft.Win32;
using System.IO;
using UW.ClassroomPresenter.Model.Viewer;
using UW.ClassroomPresenter.Model;

namespace UW.ClassroomPresenter.Misc {
    public class LoggingService : IDisposable {
        #region Private Members

        /// <summary>
        /// True once the object is disposed, false otherwise.
        /// </summary>
        private bool m_bDisposed = false;

        /// <summary>
        /// Private reference to the <see cref="ViewerStateModel"/> to modify
        /// </summary>
        private ViewerStateModel m_ViewerState = null;

        /// <summary>
        /// True when we are logging to a file, false otherwise
        /// </summary>
        private bool m_bAttached;

        /// <summary>
        /// The writer responsible for writing to a file
        /// </summary>
        private System.Diagnostics.TextWriterTraceListener FileDebugWriter = null;
        private string m_LogPath = null;

        #endregion

        #region Attach/Detach

        /// <summary>
        /// Helper function that creates the log file and attaches it
        /// </summary>
        /// <param name="path">The path to place the log file</param>
        private void SetupLogFileHelper( string path ) {
            if( path != m_LogPath ) {
                m_LogPath = path;

                // Cleanup
                this.DetachLogger();

                // Create the FileDebugWriter
                if( !Directory.Exists( path ) )
                    Directory.CreateDirectory( path );
                // Create the log file without locking it and turning off buffering
                // NOTE: Stupid FileStream doesn't allow us to get rid of the buffering 
                // completely, so even with buffer size set to 1 it still uses an 8-byte buffer
                Stream myDebugFile = new FileStream( path + "\\DEBUG_" + System.Environment.MachineName + "_" + System.DateTime.Now.Ticks.ToString() + ".log", FileMode.Create, FileAccess.Write, FileShare.ReadWrite, 1 );
                this.FileDebugWriter = new System.Diagnostics.TextWriterTraceListener( myDebugFile );
                this.AttachLogger();
            }
        }

        /// <summary>
        /// Attach the file writer to the debugger
        /// </summary>
        private void AttachLogger() {
            if( FileDebugWriter != null && !m_bAttached) {
                System.Diagnostics.Debug.Listeners.Add( FileDebugWriter );
                m_bAttached = true;
            }
        }

        /// <summary>
        /// Detach the file writer from the debugger
        /// </summary>
        private void DetachLogger() {
            if( FileDebugWriter != null && m_bAttached ) {
                System.Diagnostics.Debug.Flush();
                System.Diagnostics.Debug.Listeners.Remove( FileDebugWriter );
                m_bAttached = false;
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Constructs a service to keep track of changes to the model objects and store them in the
        /// registry
        /// </summary>
        /// <param name="toSave">The model class with values we want to save in the registry</param>
        public LoggingService() {
            bool bEnabled = false;
            // Check in the registry for the logging value and if it's enabled
            RegistryKey properties = Registry.CurrentUser.CreateSubKey(RegistryService.m_szRegistryString);
            bEnabled = true;

            // Check if we are enabled
            string enabled = RegistryService.GetStringRegistryValue( properties, "LoggingEnabled" );
            if( enabled != null )
                bEnabled = bool.Parse( enabled );
            
            // Get the Path
            string path = RegistryService.GetStringRegistryValue( properties, "LoggingPath" );
            if( path == null )
                path = ".\\Logs";

            if( bEnabled )
                this.SetupLogFileHelper( path );
        }

        /// <summary>
        /// Attach to the ViewerStateModel and listen for changes
        /// </summary>
        /// <param name="viewer"></param>
        public void AttachToModel( ViewerStateModel viewer ) {
            this.m_ViewerState = viewer;

            // Add even listeners
            this.m_ViewerState.Changed["LoggingEnabled"].Add( new PropertyEventHandler( this.OnLoggingChanged ) );
            this.m_ViewerState.Changed["LoggingPath"].Add( new PropertyEventHandler( this.OnLoggingChanged ) );
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Event handler that's invoked there is a change in logging enabled or path
        /// </summary>
        /// <param name="sender">The object that was changed</param>
        /// <param name="args">Information about the changed property</param>
        private void OnLoggingChanged( object sender, PropertyEventArgs args ) {
            using(Synchronizer.Lock(this.m_ViewerState.SyncRoot)) {
                if( this.m_ViewerState.LoggingEnabled ) {
                    this.SetupLogFileHelper( this.m_ViewerState.LoggingPath );
                } else
                    this.DetachLogger();
            }
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// Dispose of this object
        /// </summary>
        public void Dispose() {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose of all event handlers that we created in the constructor
        /// </summary>
        /// <param name="bDisposing">True if we are in process of disposing using Dispose</param>
        private void Dispose( bool bDisposing ) {
            // Check to see if Dispose has already been called.
            if( !this.m_bDisposed && bDisposing ) {
                // TODO: Detach any listeners
                this.m_ViewerState.Changed["LoggingEnabled"].Remove( new PropertyEventHandler( this.OnLoggingChanged ) );
                this.m_ViewerState.Changed["LoggingPath"].Remove( new PropertyEventHandler( this.OnLoggingChanged ) );

                // Close and flush the debug streams
                System.Diagnostics.Debug.Flush();
                System.Diagnostics.Debug.Close();
            }
            this.m_bDisposed = true;
        }

        /// <summary>
        /// Destructs the object to ensure we do the cleanup, in case we don't call Dispose.
        /// </summary>
        ~LoggingService() {
            this.Dispose(false);
        }

        #endregion
    }
}
