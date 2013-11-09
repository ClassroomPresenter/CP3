// $Id: ToolsMenu.cs 796 2005-09-27 00:04:03Z shoat $
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Windows.Forms;
using UW.ClassroomPresenter.Decks;
using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Scripting;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Model.Presentation;

namespace UW.ClassroomPresenter.Viewer.Menus {
    /// <summary>
    /// The tools menu that includes miscellaneous tools for the system
    /// </summary>
    public class ToolsMenu : MenuItem {
        /// <summary>
        /// Constructs the tools menu
        /// </summary>
        /// <param name="model">The PresenterModel to operate on</param>
        public ToolsMenu( PresenterModel model ) {
            this.Text = Strings.Tools;
#if DEBUG_TOOLS
            this.MenuItems.Add( new DiagnosticsMenuItem( model ) );
            this.MenuItems.Add( new ScriptingMenuItem( model ) ); 
            this.MenuItems.Add( new MenuItem("-") ); // Text of "-" signifies a separator bar.
#endif

#if WEBSERVER
            this.MenuItems.Add(new WebPerformanceMenuItem(model));
            this.MenuItems.Add(new MenuItem("-")); // Text of "-" signifies a separator bar.
#endif
            // TODO CMPRINCE: In the future make this appear only for the instructor
            this.MenuItems.Add(new SSOrganizerMenuItem(model));
            this.MenuItems.Add(new OptionsMenuItem(model));
//            this.MenuItems.Add( new PollMenuItem( model ) );
            this.MenuItems.Add( new PollTypeMenuItem( model ) );
        }

#if DEBUG_TOOLS
        #region DiagnosticsMenuItem
        /// <summary>
        /// Contains all the menu items for running performance evaluations and diagnostics.
        /// </summary>
        public class DiagnosticsMenuItem : MenuItem {
            /// <summary>
            /// The PresenterModel object for the system
            /// </summary>
            private PresenterModel localModel;

            /// <summary>
            /// Default Constructor
            /// </summary>
            /// <param name="model">The PresenterModel to operate on</param>
            public DiagnosticsMenuItem( PresenterModel model ) {
                localModel = model;
                this.Text = "Diagnostics";
                this.MenuItems.Add( new SaveLogMenuItem( model ) );
                this.MenuItems.Add( new MenuItem( "-" ) );
                this.MenuItems.Add( new StartPingMenuItem( model ) );
            }

            #region SaveLogMenuItem

            /// <summary>
            /// Menu item that allows the saving of the log file
            /// </summary>
            public class SaveLogMenuItem : MenuItem {
                /// <summary>
                /// The PresenterModel to operate on
                /// </summary>
                private PresenterModel localModel;

                /// <summary>
                /// Construct this menu item
                /// </summary>
                /// <param name="model">The PresenterModel to operate on</param>
                public SaveLogMenuItem( PresenterModel model ) {
                    localModel = model;
                    this.Text = "Save NetLog...";
                }

                /// <summary>
                /// Save the log file from the DiagnosticsModel to a file
                /// </summary>
                /// <param name="e">The event parameters</param>
                protected override void OnClick( EventArgs e ) {
                    SaveFileDialog save = new SaveFileDialog();
                    save.Filter = "Network Log Files (*.nlg)|*.nlg|All Files (*.*)|*.*";
                    DateTime now = DateTime.Now;
                    string filename = System.Environment.MachineName + "_" +
                                      now.Year.ToString() + "-" + now.Month.ToString() + "-" + now.Day.ToString() + "_" +
                                      now.Hour.ToString() + now.Minute.ToString() + ".nlg";
                    save.FileName = filename;
                    if( save.ShowDialog( this.GetMainMenu().GetForm() ) == DialogResult.OK ) {
                        // Get the Participant Guid
                        Guid g = Guid.Empty;
                        using( Synchronizer.Lock( localModel.SyncRoot ) ) {
                            using( Synchronizer.Lock( localModel.Participant.SyncRoot ) ) {
                                g = localModel.Participant.Guid;
                            }
                        }

                        // Construct the log file
                        Diagnostics.NetworkPerformanceFile output = localModel.ViewerState.Diagnostic.GetLogFile( g );

                        // Serialize Out the Log File
                        FileStream fs = File.Open( save.FileName, FileMode.Create, FileAccess.Write );
                        fs.Flush();
                        try {
                            BinaryFormatter bf = new BinaryFormatter();
                            bf.Serialize( fs, output );
                        } finally {
                            fs.Close();
                        }
                    }
                }
            }

            #endregion

            #region StartPingMenuItem

            public class StartPingMenuItem : MenuItem {
                private PresenterModel localModel;

                public StartPingMenuItem( PresenterModel model ) {
                    localModel = model;
                    this.Text = "Ping Clients";
                    this.Enabled = true;

                    using( Synchronizer.Lock( localModel.ViewerState.Diagnostic.SyncRoot ) ) {
                        localModel.ViewerState.Diagnostic.Changed["ServerState"].Add( new PropertyEventHandler( this.HandleGenericChange ) );
                    }
                }

                protected override void Dispose( bool disposing ) {
                    if( disposing ) {
                        using( Synchronizer.Lock( localModel.ViewerState.Diagnostic.SyncRoot ) ) {
                            localModel.ViewerState.Diagnostic.Changed["ServerState"].Remove( new PropertyEventHandler( this.HandleGenericChange ) );
                        }
                    }
                    base.Dispose( disposing );
                }

                protected override void OnClick( EventArgs e ) {
                    using( Synchronizer.Lock( localModel.ViewerState.Diagnostic.SyncRoot ) ) {
                        localModel.ViewerState.Diagnostic.ServerState = Model.Viewer.DiagnosticModel.ServerSyncState.BEGIN;
                    }
                }

                protected void HandleGenericChange( object sender, PropertyEventArgs e ) {
                    using( Synchronizer.Lock( localModel.ViewerState.Diagnostic.SyncRoot ) ) {
                        if( localModel.ViewerState.Diagnostic.ServerState == UW.ClassroomPresenter.Model.Viewer.DiagnosticModel.ServerSyncState.START )
                            this.Enabled = true;
                        else
                            this.Enabled = false;
                    }
                }
            }

            #endregion
        }

        #endregion
#endif

#if WEBSERVER
        #region WebPerformanceMenuItem

        /// <summary>
        /// Contains all the menu items for running web performance tests.
        /// </summary>
        public class WebPerformanceMenuItem : MenuItem {
            /// <summary>
            /// The PresenterModel object for the system
            /// </summary>
            private PresenterModel localModel;

            /// <summary>
            /// Default Constructor
            /// </summary>
            /// <param name="model">The PresenterModel to operate on</param>
            public WebPerformanceMenuItem(PresenterModel model) {
                localModel = model;
                this.Text = "Web Performance";
                this.MenuItems.Add(new RequestSubmissionMenuItem(model));
                this.MenuItems.Add(new RequestLogsMenuItem(model));
            }

            #region RequestSubmissionMenuItem

            /// <summary>
            /// Menu item that requests that the web clients submit a student submission.
            /// </summary>
            public class RequestSubmissionMenuItem : MenuItem {
                /// <summary>
                /// The PresenterModel to operate on.
                /// </summary>
                private PresenterModel localModel;

                /// <summary>
                /// Construct this menu item.
                /// </summary>
                /// <param name="model">The PresenterModel to operate on</param>
                public RequestSubmissionMenuItem(PresenterModel model) {
                    localModel = model;
                    this.Text = "Request Submission";
                }

                /// <summary>
                /// Save the log file from the DiagnosticsModel to a file
                /// </summary>
                /// <param name="e">The event parameters</param>
                protected override void OnClick(EventArgs e) {
                    using (Synchronizer.Lock(localModel.ViewerState.WebPerformance.SyncRoot)) {
                        localModel.ViewerState.WebPerformance.RequestSubmissionSignal += 1;
                    }
                }
            }

            #endregion

            #region RequestLogsMenuItem

            /// <summary>
            /// Menu item that requests that web clients submit their performance log.
            /// </summary>
            public class RequestLogsMenuItem : MenuItem {
                /// <summary>
                /// The PresenterModel to operate on.
                /// </summary>
                private PresenterModel localModel;

                /// <summary>
                /// Construct the menu item.
                /// </summary>
                /// <param name="model">The PresenterModel to operate on.</param>
                public RequestLogsMenuItem(PresenterModel model) {
                    localModel = model;
                    this.Text = "Request Logs";
                }

                protected override void OnClick( EventArgs e ) {
                    using (Synchronizer.Lock(localModel.ViewerState.WebPerformance.SyncRoot)) {
                        localModel.ViewerState.WebPerformance.RequestLogSignal += 1;
                    }
                }
            }

            #endregion
        }

        #endregion
#endif

        #region PollTypeMenuItem

        /// <summary>
        /// The Options Menu for specifying various network options
        /// </summary>
        public class PollTypeMenuItem : MenuItem {
            /// <summary>
            /// The PresenterModel to operate over
            /// </summary>
            private PresenterModel m_Model;

            /// <summary>
            /// Constructs this menu item
            /// </summary>
            /// <param name="model">The PresenterModel to operate over</param>
            public PollTypeMenuItem( PresenterModel model ) {
                m_Model = model;
                this.Text = Strings.PollType;

                this.MenuItems.Add( new PollTypeOptionMenuItem( model, Strings.QuickPollABC, QuickPollModel.QuickPollStyle.ABC ) );
                this.MenuItems.Add( new PollTypeOptionMenuItem( model, Strings.QuickPollABCD, QuickPollModel.QuickPollStyle.ABCD ) );
                this.MenuItems.Add( new PollTypeOptionMenuItem( model, Strings.QuickPollABCDE, QuickPollModel.QuickPollStyle.ABCDE ) );
                this.MenuItems.Add( new PollTypeOptionMenuItem( model, Strings.QuickPollYes+"/"+Strings.QuickPollNo, QuickPollModel.QuickPollStyle.YesNo ) );
                this.MenuItems.Add(new PollTypeOptionMenuItem(model, Strings.QuickPollYes + "/" + Strings.QuickPollNo+"/" + Strings.QuickPollBoth, QuickPollModel.QuickPollStyle.YesNoBoth));
                this.MenuItems.Add(new PollTypeOptionMenuItem(model, Strings.QuickPollYes + "/" + Strings.QuickPollNo + "/" + Strings.QuickPollNeither, QuickPollModel.QuickPollStyle.YesNoNeither));

                using( Synchronizer.Lock( this.m_Model.Participant.SyncRoot ) ) {
                    this.m_Model.Participant.Changed["Role"].Add( new PropertyEventHandler( this.HandleRoleChanged ) );
                }

                // Initialize the state of the menu items.
                this.HandleRoleChanged( this, null );
            }

            protected void HandleRoleChanged( object sender, PropertyEventArgs e ) {
                using( Synchronizer.Lock( this.m_Model.Participant.SyncRoot ) ) {
                    this.Enabled = this.Visible = this.m_Model.Participant.Role is InstructorModel;
                }
            }

            protected override void Dispose( bool disposing ) {
                if( disposing ) {
                    using( Synchronizer.Lock( this.m_Model.Participant.SyncRoot ) ) {
                        this.m_Model.Participant.Changed["Role"].Remove( new PropertyEventHandler( this.HandleRoleChanged ) );
                    }
                }
                base.Dispose( disposing );
            }
        }

        #region PollTypeOptionMenuItem

        /// <summary>
        /// The Options Menu for specifying various network options
        /// </summary>
        public class PollTypeOptionMenuItem : MenuItem {
            /// <summary>
            /// The PresenterModel to operate over
            /// </summary>
            private PresenterModel localModel;

            private QuickPollModel.QuickPollStyle m_Style;

            /// <summary>
            /// Constructs this menu item
            /// </summary>
            /// <param name="model">The PresenterModel to operate over</param>
            public PollTypeOptionMenuItem( PresenterModel model, string text, QuickPollModel.QuickPollStyle style ) {
                localModel = model;
                this.m_Style = style;
                this.Text = text;

                using( Synchronizer.Lock( this.localModel.ViewerState.SyncRoot ) ) {
                    this.localModel.ViewerState.Changed["PollStyle"].Add( new PropertyEventHandler( this.HandleStyleChanged ) );
                }

                // Initialize the state of the menu items.
                this.HandleStyleChanged( this, null );
            }

            protected override void Dispose( bool disposing ) {
                if( disposing ) {
                    using( Synchronizer.Lock( this.localModel.ViewerState.SyncRoot ) ) {
                        this.localModel.ViewerState.Changed["PollStyle"].Remove( new PropertyEventHandler( this.HandleStyleChanged ) );
                    }
                }
                base.Dispose( disposing );
            }

            protected void HandleStyleChanged( object sender, PropertyEventArgs e ) {
                using( Synchronizer.Lock( this.localModel.ViewerState.SyncRoot ) ) {
                    this.Checked = (this.localModel.ViewerState.PollStyle == this.m_Style);
                }
            }

            /// <summary>
            /// Handle displaying the PropertiesForm when the menu item is clicked on
            /// </summary>
            /// <param name="e">The event arguments </param>
            protected override void OnClick( EventArgs e ) {
                using( Synchronizer.Lock( this.localModel.ViewerState.SyncRoot ) ) {
                    this.localModel.ViewerState.PollStyle = this.m_Style;
                }
            }
        }

        #endregion

        #endregion

        #region PollMenuItem

        /// <summary>
        /// The Options Menu for specifying various network options
        /// </summary>
        public class PollMenuItem : MenuItem {
            /// <summary>
            /// The PresenterModel to operate over
            /// </summary>
            private PresenterModel localModel;

            /// <summary>
            /// Constructs this menu item
            /// </summary>
            /// <param name="model">The PresenterModel to operate over</param>
            public PollMenuItem( PresenterModel model ) {
                localModel = model;
                this.Text = Strings.Poll;
            }

            /// <summary>
            /// Handle displaying the PropertiesForm when the menu item is clicked on
            /// </summary>
            /// <param name="e">The event arguments </param>
            protected override void OnClick( EventArgs e ) {
                string result = "";

                using( this.localModel.Workspace.Lock() ) {
                    if( (~this.localModel.Workspace.CurrentPresentation) != null ) {
                        using( Synchronizer.Lock( (~this.localModel.Workspace.CurrentPresentation).SyncRoot ) ) {
                            if( (~this.localModel.Workspace.CurrentPresentation).QuickPoll != null ) {
                                using( Synchronizer.Lock( (~this.localModel.Workspace.CurrentPresentation).QuickPoll.SyncRoot) ) {
                                    System.Collections.Hashtable table = (~this.localModel.Workspace.CurrentPresentation).QuickPoll.GetVoteCount();
                                    foreach( string s in table.Keys ) {
                                        result += QuickPollModel.GetLocalizedQuickPollString(s) + " - " + table[s].ToString() + System.Environment.NewLine;
                                    }
                                }
                            }
                        }
                    }
                }

                MessageBox.Show( result );
            }
        }

        #endregion

        #region OptionsMenuItem

        /// <summary>
        /// The Options Menu for specifying various network options
        /// </summary>
        public class OptionsMenuItem : MenuItem {
            /// <summary>
            /// The PresenterModel to operate over
            /// </summary>
            private PresenterModel localModel;

            /// <summary>
            /// Constructs this menu item
            /// </summary>
            /// <param name="model">The PresenterModel to operate over</param>
            public OptionsMenuItem( PresenterModel model ) {
                localModel = model;
                this.Text = Strings.Options;
            }

            /// <summary>
            /// Handle displaying the PropertiesForm when the menu item is clicked on
            /// </summary>
            /// <param name="e">The event arguments </param>
            protected override void OnClick(EventArgs e) {
                UW.ClassroomPresenter.Viewer.PropertiesForm.PropertiesForm props =
                    new UW.ClassroomPresenter.Viewer.PropertiesForm.PropertiesForm(localModel.ViewerState);
                props.ShowDialog();
            }
        }

        #endregion

        #region SSOrganizerMenuItem

        /// <summary>
        /// The menu item that displays the student submissions organizer panel.
        /// </summary>
        public class SSOrganizerMenuItem : MenuItem {
            /// <summary>
            /// The PresenterModel to operate over
            /// </summary>
            private PresenterModel localModel;

            /// <summary>
            /// Constructs this menu item
            /// </summary>
            /// <param name="model">The PresenterModel to operate over</param>
            public SSOrganizerMenuItem(PresenterModel model) {
                localModel = model;
                this.Text = "Student Submission Organizer";
            }

            /// <summary>
            /// Handle displaying the PropertiesForm when the menu item is clicked on
            /// </summary>
            /// <param name="e">The event arguments </param>
            protected override void OnClick(EventArgs e) {
                UW.ClassroomPresenter.Viewer.SSOrganizer.SSOrganizerForm organizer =
                    new UW.ClassroomPresenter.Viewer.SSOrganizer.SSOrganizerForm(localModel);
                organizer.ShowDialog();
            }
        }

        #endregion

#if DEBUG_TOOLS
        #region ScriptingMenuItem

        /// <summary>
        /// Contains all the menu items corresponding to loading and 
        /// executing scripts on the system.
        /// </summary>
        public class ScriptingMenuItem : MenuItem {
            /// <summary>
            /// The PresenterModel object for the system
            /// </summary>
            private PresenterModel localModel;
            /// <summary>
            /// The LoadScriptMenuItem
            /// </summary>
            private LoadScriptMenuItem load;
            /// <summary>
            /// The ExecuteScriptMenuItem
            /// </summary>
            private ExecuteScriptMenuItem exec;

            /// <summary>
            /// Represents the current script that has been loaded by the UI
            /// </summary>
            private Script m_CurrentScript = null;
            public Script CurrentScript {
                get { return m_CurrentScript; }
                set {
                    this.m_CurrentScript = value;
                    if( this.m_CurrentScript != null )
                        this.exec.Enabled = true;
                    else
                        this.exec.Enabled = false;
                }

            }

            /// <summary>
            /// Default Constructor
            /// </summary>
            /// <param name="model">The PresenterModel to operate on</param>
            public ScriptingMenuItem( PresenterModel model ) {
                localModel = model;
                this.Text = "Scripting";

                load = new LoadScriptMenuItem( model );
                exec = new ExecuteScriptMenuItem( model );
                this.MenuItems.Add( load );
                this.MenuItems.Add( exec );
                this.MenuItems.Add( new MenuItem("-") );
                this.MenuItems.Add( new ExecuteRemoteScriptMenuItem( model ) );
            }

        #region LoadScriptMenuItem

            public class LoadScriptMenuItem : MenuItem {
                private PresenterModel localModel;

                public LoadScriptMenuItem( PresenterModel model ) {
                    localModel = model;
                    this.Text = "Open...";
                }

                protected override void OnClick( EventArgs e ) {
                    OpenFileDialog open = new OpenFileDialog();
                    open.Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*";
                    if( open.ShowDialog( this.GetMainMenu().GetForm() ) == DialogResult.OK ) {
                        Script s = new Script( open.FileName );
                        if( (s != null) && (this.Parent is ScriptingMenuItem) )
                            ((ScriptingMenuItem)this.Parent).CurrentScript = s;
                    }
                }
            }

        #endregion

        #region ExecuteScriptMenuItem

            public class ExecuteScriptMenuItem : MenuItem {
                private PresenterModel localModel;
                private ScriptExecutionService service = null;

                public ExecuteScriptMenuItem( PresenterModel model ) {
                    localModel = model;
                    this.Text = "Run Script";
                    this.Enabled = false;

                    using( Synchronizer.Lock( localModel.ViewerState.Diagnostic.SyncRoot ) ) {
                        localModel.ViewerState.Diagnostic.Changed["ExecuteLocalScript"].Add( new PropertyEventHandler( this.HandleGenericChange ) );
                    }
                }

                protected override void Dispose( bool disposing ) {
                    if( disposing ) {
                        using( Synchronizer.Lock( localModel.ViewerState.Diagnostic.SyncRoot ) ) {
                            localModel.ViewerState.Diagnostic.Changed["ExecuteLocalScript"].Remove( new PropertyEventHandler( this.HandleGenericChange ) );
                        }
                    }
                    base.Dispose( disposing );
                }

                protected override void OnClick( EventArgs e ) {
                    if( this.Parent is ScriptingMenuItem &&
                        ((ScriptingMenuItem)this.Parent).CurrentScript != null ) {
                        service = new ScriptExecutionService( localModel, this.GetMainMenu().GetForm() );
                        System.Threading.Thread scriptThread = new System.Threading.Thread( new System.Threading.ParameterizedThreadStart( service.ExecuteScriptThreadEntry ) );
                        scriptThread.Start( ((ScriptingMenuItem)this.Parent).CurrentScript );
                    }
                }

                protected void HandleGenericChange( object sender, PropertyEventArgs e ) {
                    this.PerformClick();
                }
            }

        #endregion

        #region ExecuteRemoteScriptMenuItem

            public class ExecuteRemoteScriptMenuItem : MenuItem {
                private PresenterModel localModel;

                public ExecuteRemoteScriptMenuItem( PresenterModel model ) {
                    localModel = model;
                    this.Text = "Run Remote Script";
                    this.Enabled = true;
                }

                protected override void OnClick( EventArgs e ) {
                    using( Synchronizer.Lock( localModel.ViewerState.Diagnostic.SyncRoot ) ) {
                        localModel.ViewerState.Diagnostic.ExecuteRemoteScript = true;
                    }
                }
            }

        #endregion
        }
        #endregion
#endif
    }
}
