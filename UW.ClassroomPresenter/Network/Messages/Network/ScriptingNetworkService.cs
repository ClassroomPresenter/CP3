// $Id: InstructorNetworkService.cs 913 2006-03-21 04:40:16Z cmprince $

using System;
using System.Diagnostics;
using System.Threading;
using System.Collections;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Model.Viewer;
using UW.ClassroomPresenter.Network.Messages;
using UW.ClassroomPresenter.Network.Messages.Network;
using UW.ClassroomPresenter.Network.Messages.Presentation;

namespace UW.ClassroomPresenter.Network.Messages.Network {
    /// <summary>
    /// This class is responsible for sending the ExecuteRemoteScript message
    /// </summary>
    public class ScriptingNetworkService {
        // Protected Members
        protected readonly SendingQueue Sender;
        protected readonly PresenterModel m_Model;
        protected readonly DiagnosticModel m_Diagnostic;
        private bool m_Disposed;

        // Event Dispatchers
        private readonly EventQueue.PropertyEventDispatcher m_GenericChangeDispatcher;
        
        /// <summary>
        /// Constructor for the scripting network service
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="model"></param>
        /// <param name="diag"></param>
        public ScriptingNetworkService(SendingQueue sender, PresenterModel model) {
            this.Sender = sender;
            this.m_Model = model;
            this.m_Diagnostic = model.ViewerState.Diagnostic;

            // Set up the change listeners
            this.m_GenericChangeDispatcher = new EventQueue.PropertyEventDispatcher(this.Sender, new PropertyEventHandler(this.HandleGenericChange));
            this.m_Diagnostic.Changed["ExecuteRemoteScript"].Add(this.m_GenericChangeDispatcher.Dispatcher);
        }

        #region IDisposable Members

        ~ScriptingNetworkService() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            try {
                if(disposing) {
                    this.m_Diagnostic.Changed["ExecuteRemoteScript"].Remove( this.m_GenericChangeDispatcher.Dispatcher );
                }
            } finally {
            }
            this.m_Disposed = true;
        }

        #endregion

        /// <summary>
        /// Handle changes to the ExecuteRemoteScript variable
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void HandleGenericChange(object sender, PropertyEventArgs args) {
            using( Synchronizer.Lock( this.m_Diagnostic.SyncRoot ) ) {
                if( this.m_Diagnostic.ExecuteRemoteScript == true ) {
                    this.Sender.Send( new ExecuteScriptMessage() );
                    this.m_Diagnostic.ExecuteRemoteScript = false;
                }
            }
        }
   }
}