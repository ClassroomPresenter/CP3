using System;
using System.Drawing;
using System.Windows.Forms;

using UW.ClassroomPresenter.Model.Viewer;

namespace UW.ClassroomPresenter.Viewer.PropertiesForm {

    /// <summary>
    /// Properties page for the logging service
    /// </summary>
    public class LoggingPropertiesPage : TabPage {
        /// <summary>
        /// Constructs a new Logging Properties page
        /// </summary>
        public LoggingPropertiesPage( ViewerStateModel model ) {

            this.SuspendLayout();

            this.Location = new System.Drawing.Point( 4, 22 );
            this.Name = "LoggingTabPage";
            this.Size = new System.Drawing.Size( 556, 248 );
            this.TabIndex = 9;
            this.Text = Strings.Logging;

            // Add the controls
            this.Controls.Add( new LoggingGroup( model, new Point( 16, 24 ), new Size( 524, 88 ), 3 ) );

            this.ResumeLayout();
        }

        /// <summary>
        /// The group box containing the logging properties
        /// </summary>
        public class LoggingGroup : GroupBox {
            /// <summary>
            /// Constructor for the group box
            /// </summary>
            public LoggingGroup( ViewerStateModel model, Point location, Size size, int tabIndex ) {

                this.SuspendLayout();

                this.Location = location;
                this.FlatStyle = FlatStyle.System;
                this.Size = size;
                this.TabIndex = tabIndex;
                this.Name = "grpLogging";
                this.TabStop = false;
                this.Text = Strings.LoggingOptions;
                this.Enabled = true;

                // Add the controls
                this.Controls.Add( new LoggingEnabledCheckBox( model, new Point( 10, 18 ), new Size( 260, 24 ), 32 ) );
                this.Controls.Add( new LoggingPathLabel( new Point( 10, 50 ), new Size( 155, 24 ), 68 ) );
                this.Controls.Add( new LoggingPathTextBox( model, new Point( 169, 50 ), new Size( 341, 21 ), 31 ) );

                this.ResumeLayout();
            }

            /// <summary>
            /// The check box specifying if logging is enabled
            /// </summary>
            public class LoggingEnabledCheckBox : CheckBox {
                private ViewerStateModel m_ViewerState;

                /// <summary>
                /// Constructor for the check box
                /// </summary>
                public LoggingEnabledCheckBox( ViewerStateModel model, Point location, Size size, int tabIndex ) {
                    this.m_ViewerState = model;

                    this.SuspendLayout();

                    this.FlatStyle = FlatStyle.System;
                    this.Location = location;
                    this.Size = size;
                    this.TabIndex = tabIndex;
                    this.Enabled = true;
                    this.Name = "LoggingEnabledCheckBox";
                    this.Text = Strings.EnableLogging;

                    // Set the default value according to the model's current setting
                    // NOTE: Split into two steps to avoid deadlock with updating the check state
                    bool bShouldBeChecked = true;

                    if( model != null ) {
                        using( Synchronizer.Lock( model.SyncRoot ) ) {
                            bShouldBeChecked = model.LoggingEnabled;
                        }
                    }

                    if( bShouldBeChecked ) {
                        this.Checked = true;
                        this.CheckState = CheckState.Checked;
                    } else {
                        this.Checked = false;
                        this.CheckState = CheckState.Unchecked;
                    }

                    this.ResumeLayout();
                }

                protected override void OnCheckedChanged( EventArgs e ) {
                    base.OnCheckedChanged( e );

                    // Update the model value
                    if( this.m_ViewerState != null ) {
                        using( Synchronizer.Lock( this.m_ViewerState.SyncRoot ) ) {
                            this.m_ViewerState.LoggingEnabled = this.Checked;
                        }
                    }
                }
            }

            /// <summary>
            /// The Label for the Log Path Textbox
            /// </summary>
            public class LoggingPathLabel : Label {
                public LoggingPathLabel( Point location, Size size, int tabIndex ) {
                    this.SuspendLayout();

                    this.FlatStyle = FlatStyle.System;
                    this.Location = location;
                    this.Size = size;
                    this.TabIndex = tabIndex;
                    this.Name = "LoggingPathLabel";
                    this.Text = Strings.LogFilePath;
                    this.Enabled = true;

                    this.ResumeLayout();
                }
            }

            /// <summary>
            /// The text box containing the path
            /// </summary>
            public class LoggingPathTextBox : TextBox {
                private ViewerStateModel m_ViewerState;

                public LoggingPathTextBox( ViewerStateModel model, Point location, Size size, int tabIndex ) {
                    this.m_ViewerState = model;

                    this.SuspendLayout();

                    this.Location = location;
                    this.Size = size;
                    this.TabIndex = tabIndex;
                    this.Name = "LoggingPathTextBox";
                    this.ReadOnly = false;
                    this.Text = "";
                    this.Enabled = true;

                    if( model != null ) {
                        string text = "";
                        using( Synchronizer.Lock( model.SyncRoot ) ) {
                            text = model.LoggingPath;
                        }

                        this.Text = text;
                    }

                    this.ResumeLayout();
                }

                protected override void OnTextChanged( EventArgs e ) {
                    base.OnTextChanged( e );

                    // Update the model value
                    if( this.m_ViewerState != null ) {
                        using( Synchronizer.Lock( this.m_ViewerState.SyncRoot ) ) {
                            this.m_ViewerState.LoggingPath = this.Text;
                        }
                    }
                }
            }
        }
    }
}
