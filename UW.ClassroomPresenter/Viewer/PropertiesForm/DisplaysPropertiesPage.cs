// $Id: GeneralPropertiesPage.cs 1474 2007-10-09 04:45:56Z linnell $

using System;
using System.Drawing;
using System.Windows.Forms;
using UW.ClassroomPresenter.Model.Viewer;

namespace UW.ClassroomPresenter.Viewer.PropertiesForm {

    /// <summary>
    /// Class that describes the Display Properties that can be applied to Presenter
    /// </summary>
    public class DisplayPropertiesPage : TabPage {
        /// <summary>
        /// Constructs a new Display Properties Page
        /// </summary>
        public DisplayPropertiesPage( ViewerStateModel vsm ) {
            this.SuspendLayout();

            this.Location = new System.Drawing.Point( 4, 22 );
            this.Name = "DisplayTabPage";
            this.Size = new System.Drawing.Size( 556, 248 );
            this.TabIndex = 6;
            this.Text = Strings.Displays;

            // Add the controls
            this.Controls.Add( new DualMonitorEnabledCheckBox( vsm, new Point( 16, 8 ), new Size( 522, 24 ), 0 ) );
            this.Controls.Add( new MonitorSwitchingGroup( vsm, new Point( 16, 32 ), new Size( 522, 196 ), 1 ) );

            this.ResumeLayout();
        }

        #region DualMonitorGroup

        /// <summary>
        /// The checkbox for enabling dual-monitor support
        /// </summary>
        public class DualMonitorEnabledCheckBox : CheckBox {
            private ViewerStateModel m_Model;

            /// <summary>
            /// Constructs the checkbox
            /// </summary>
            public DualMonitorEnabledCheckBox( ViewerStateModel vsm, Point location, Size size, int tabIndex ) {
                this.m_Model = vsm;

                this.SuspendLayout();

                this.FlatStyle = FlatStyle.System;
                this.Location = location;
                this.Size = size;
                this.TabIndex = tabIndex;
                this.Name = "enableSecondMonitorCheckBox";
                this.Text = Strings.EnableSecondMonitor;

                // Set the default value according to the model's current setting
                // NOTE: Split into two steps to avoid deadlock with updating the check state
                bool bShouldBeChecked;
                using( Synchronizer.Lock( this.m_Model.SyncRoot ) )
                    bShouldBeChecked = this.m_Model.SecondMonitorEnabled;
                this.Checked = bShouldBeChecked;
                this.CheckState = bShouldBeChecked ? CheckState.Checked : CheckState.Unchecked;

                this.ResumeLayout();
            }

            protected override void OnCheckedChanged( EventArgs e ) {
                base.OnCheckedChanged( e );

                // Update the model value
                if( this.m_Model != null ) {
                    using( Synchronizer.Lock( this.m_Model.SyncRoot ) ) {
                        this.m_Model.SecondMonitorEnabled = this.Checked;
                    }
                }
            }
        }

        #endregion

        #region MonitorSwitchingGroup

        /// <summary>
        /// This group has parameters which control how the monitors are automatically switched from 
        /// extended desktop to cloned desktop
        /// </summary>
        public class MonitorSwitchingGroup : GroupBox {
            /// <summary>
            /// Constructor for the group
            /// </summary>
            public MonitorSwitchingGroup( ViewerStateModel vsm, Point location, Size size, int tabIndex ) {

                this.SuspendLayout();

                this.FlatStyle = FlatStyle.System;
                this.Location = location;
                this.Size = size;
                this.TabIndex = tabIndex;
                this.Name = "MonitorSwitchingGroup";
                this.TabStop = false;
                this.Text = Strings.MultiMonitorSwitching;
                this.Enabled = true;

                // Add the controls
                this.Controls.Add( new MonitorWarningLabel( new Point(16,16), new Size( 500, 24), 2 ) );
                this.Controls.Add( new WindowsAPICheckBox( vsm, new Point( 16, 44 ), new Size( 168, 24 ), 3 ) );
                this.Controls.Add( new CustomCommandsCheckBox( vsm, new Point( 16, 70 ), new Size( 268, 24 ), 4 ) );
                this.Controls.Add( new RestoreSettingsButton( vsm, new Point( 304, 43 ), new Size( 164, 27 ), 5 ) );
                this.Controls.Add( new SpecialCommandsGroup( vsm, new Point( 12, 94 ), new Size( 445, 94 ), 6 ) );
                this.Controls.Add( new CloneButton( vsm, new Point( 184, 43 ), new Size( 60, 27 ), 13 ) );
                this.Controls.Add( new ExtendButton( vsm, new Point( 244, 43 ), new Size( 60, 27 ), 14 ) );

                this.ResumeLayout();
            }

            /// <summary>
            /// The label warning users not to mess with these settings
            /// </summary>
            public class MonitorWarningLabel : Label {
                /// <summary>
                /// Constructor
                /// </summary>
                public MonitorWarningLabel( Point location, Size size, int tabIndex ) {
                    this.SuspendLayout();

                    this.FlatStyle = FlatStyle.System;
                    this.Location = location;
                    this.Size = size;
                    this.TabIndex = tabIndex;
                    this.Name = "MonitorWarningLabel";
                    this.Text = Strings.MonitorSetNote;
                    this.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

                    this.ResumeLayout();
                }
            }

            /// <summary>
            /// The checkbox for using windows APIs to change the windowing
            /// </summary>
            public class WindowsAPICheckBox : CheckBox {
                private ViewerStateModel m_Model;

                /// <summary>
                /// Constructs the checkbox
                /// </summary>
                public WindowsAPICheckBox( ViewerStateModel vsm, Point location, Size size, int tabIndex ) {
                    this.m_Model = vsm;

                    this.SuspendLayout();

                    this.FlatStyle = FlatStyle.System;
                    this.Location = location;
                    this.Size = size;
                    this.TabIndex = tabIndex;
                    this.Name = "windowsAPICheckBox";
                    this.Text = Strings.UseWindowAPIs;

                    // Set the default value according to the model's current setting
                    // NOTE: Split into two steps to avoid deadlock with updating the check state
                    bool bShouldBeChecked;
                    using( Synchronizer.Lock( this.m_Model.SyncRoot ) )
                        bShouldBeChecked = this.m_Model.SecondMonitorWindowsAPIsEnabled;
                    this.Checked = bShouldBeChecked;
                    this.CheckState = bShouldBeChecked ? CheckState.Checked : CheckState.Unchecked;

                    this.ResumeLayout();
                }

                protected override void OnCheckedChanged( EventArgs e ) {
                    base.OnCheckedChanged( e );

                    // Update the model value
                    if( this.m_Model != null ) {
                        using( Synchronizer.Lock( this.m_Model.SyncRoot ) ) {
                            this.m_Model.SecondMonitorWindowsAPIsEnabled = this.Checked;
                        }
                    }
                }
            }

            /// <summary>
            /// The checkbox for using windows APIs to change the windowing
            /// </summary>
            public class CustomCommandsCheckBox : CheckBox {
                private ViewerStateModel m_Model;

                /// <summary>
                /// Constructs the checkbox
                /// </summary>
                public CustomCommandsCheckBox( ViewerStateModel vsm, Point location, Size size, int tabIndex ) {
                    this.m_Model = vsm;

                    this.SuspendLayout();

                    this.FlatStyle = FlatStyle.System;
                    this.Location = location;
                    this.Size = size;
                    this.TabIndex = tabIndex;
                    this.Name = "customCommandsCheckBox";
                    this.Text = Strings.UseCustomCommands;

                    // Set the default value according to the model's current setting
                    // NOTE: Split into two steps to avoid deadlock with updating the check state
                    bool bShouldBeChecked;
                    using( Synchronizer.Lock( this.m_Model.SyncRoot ) )
                        bShouldBeChecked = this.m_Model.SecondMonitorCustomCommandsEnabled;
                    this.Checked = bShouldBeChecked;
                    this.CheckState = bShouldBeChecked ? CheckState.Checked : CheckState.Unchecked;

                    this.ResumeLayout();
                }

                protected override void OnCheckedChanged( EventArgs e ) {
                    base.OnCheckedChanged( e );

                    // Update the model value
                    if( this.m_Model != null ) {
                        using( Synchronizer.Lock( this.m_Model.SyncRoot ) ) {
                            this.m_Model.SecondMonitorCustomCommandsEnabled = this.Checked;
                        }
                    }
                }
            }

            /// <summary>
            /// Properties for the custom commands
            /// </summary>
            public class SpecialCommandsGroup : GroupBox {
                
                private DeviceDropDown deviceDropDown;
                private CloneTextBox cloneTextBox;
                private ExtendedTextBox extendedTextBox;

                /// <summary>
                ///
                /// </summary>
                public SpecialCommandsGroup( ViewerStateModel vsm, Point location, Size size, int tabIndex ) {

                    this.SuspendLayout();

                    this.FlatStyle = FlatStyle.System;
                    this.Location = location;
                    this.Size = size;
                    this.TabIndex = tabIndex;
                    this.Name = "SpecialCommandsGroup";
                    this.TabStop = false;
                    this.Text = Strings.CustomCommands;
                    this.Enabled = true;

                    // Add the controls
                    this.Controls.Add( new DeviceInfoLabel( new Point( 16, 17 ), new Size( 100, 24 ), 7 ) );
                    deviceDropDown = new DeviceDropDown( vsm, new Point( 118, 16 ), new Size( 308, 21 ), 8 );
                    this.Controls.Add( deviceDropDown );
                    this.Controls.Add( new CloneInfoLabel( new Point( 30, 41 ), new Size( 148, 24 ), 9 ) );
                    cloneTextBox = new CloneTextBox( vsm, new Point( 180, 40 ), new Size( 246, 21 ), 10 );
                    this.Controls.Add( cloneTextBox );
                    this.Controls.Add( new ExtendedInfoLabel( new Point( 30, 65 ), new Size( 138, 24 ), 11 ) );
                    extendedTextBox = new ExtendedTextBox( vsm, new Point( 180, 64 ), new Size( 246, 21 ), 12 );
                    this.Controls.Add( extendedTextBox );

                    this.ResumeLayout();
                }

                #region Helper Functions

                public void SetCustomCommandType( string command ) {
                    if( cloneTextBox != null && extendedTextBox != null && deviceDropDown != null ) {
                        switch( command ) {
                            case "<none>":
                                cloneTextBox.Text = "";
                                extendedTextBox.Text = "";
                                break;
                            case "NVIDIA nView -- nvcpl.dll":
                                cloneTextBox.Text = Misc.DispalyControlHelpers.nViewCloneCommand;
                                extendedTextBox.Text = "";
                                deviceDropDown.SelectedItem = "NVIDIA nView -- nvcpl.dll";
                                break;
                            default:
                                // Do Nothing
                                break;
                        }
                    }
                }

                public void ChangedClonedCommand( string command ) {
                    if( cloneTextBox != null && extendedTextBox != null && deviceDropDown != null ) {
                        // If both boxes are empty, make <none>, else make type <custom>
                        if( cloneTextBox.Text == "" && extendedTextBox.Text == "" ) {
                            deviceDropDown.SelectedItem = "<none>";
                        } else {
                            deviceDropDown.SelectedItem = "<custom>";
                        }
                    }
                }

                public void ChangedExtendedCommand( string command ) {
                    if( cloneTextBox != null && extendedTextBox != null && deviceDropDown != null ) {
                        // If both boxes are empty, make <none>, else make type <custom>
                        if( cloneTextBox.Text == "" && extendedTextBox.Text == "" ) {
                            deviceDropDown.SelectedItem = "<none>";
                        } else {
                            deviceDropDown.SelectedItem = "<custom>";
                        }
                    }
                }

                #endregion

                /// <summary>
                /// The label for the device drop down
                /// </summary>
                public class DeviceInfoLabel : Label {
                    /// <summary>
                    /// Constructor
                    /// </summary>
                    public DeviceInfoLabel( Point location, Size size, int tabIndex ) {
                        this.SuspendLayout();

                        this.FlatStyle = FlatStyle.System;
                        this.Location = location;
                        this.Size = size;
                        this.TabIndex = tabIndex;
                        this.Name = "DeviceInfoLabel";
                        this.Text = Strings.AdapterType;
                        this.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

                        this.ResumeLayout();
                    }
                }

                /// <summary>
                /// A drop-down with preselected settings
                /// </summary>
                public class DeviceDropDown : System.Windows.Forms.ComboBox {
                    private ViewerStateModel m_VSModel;

                    public DeviceDropDown( ViewerStateModel vsm, Point location, Size size, int tabIndex ) {
                        this.m_VSModel = vsm;
                        this.SuspendLayout();

                        this.FlatStyle = FlatStyle.System;
                        this.Location = location;
                        this.Size = size;
                        this.TabIndex = tabIndex;
                        this.DropDownStyle = ComboBoxStyle.DropDownList;
                        this.Items.Add( "<none>" );
                        this.Items.Add( "NVIDIA nView -- nvcpl.dll" );
                        this.Items.Add( "<custom>" );

                        using( Synchronizer.Lock( vsm.SyncRoot ) ) {
                            if( this.m_VSModel.SecondMonitorCustomCommandType == "" ) {
                                this.SelectedItem = "<none>";
                            } else {
                                this.SelectedItem = this.m_VSModel.SecondMonitorCustomCommandType;
                            }
                        }

                        this.ResumeLayout();
                    }

                    protected override void OnSelectedValueChanged( EventArgs e ) {
                        using( Synchronizer.Lock( this.m_VSModel.SyncRoot ) ) {
                            this.m_VSModel.SecondMonitorCustomCommandType = (string)this.SelectedItem;
                        }
                        base.OnSelectedValueChanged( e );
                        if( this.Parent != null ) {
                            ((SpecialCommandsGroup)this.Parent).SetCustomCommandType( (string)this.SelectedItem );
                        }
                    }
                }

                /// <summary>
                /// The label for the clone text box
                /// </summary>
                public class CloneInfoLabel : Label {
                    /// <summary>
                    /// Constructor
                    /// </summary>
                    public CloneInfoLabel( Point location, Size size, int tabIndex ) {
                        this.SuspendLayout();

                        this.FlatStyle = FlatStyle.System;
                        this.Location = location;
                        this.Size = size;
                        this.TabIndex = tabIndex;
                        this.Name = "CloneInfoLabel";
                        this.Text = Strings.CloneCommand;
                        this.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

                        this.ResumeLayout();
                    }
                }

                /// <summary>
                /// The text box containing the path
                /// </summary>
                public class CloneTextBox : TextBox {
                    private ViewerStateModel m_ViewerState;

                    public CloneTextBox( ViewerStateModel vsm, Point location, Size size, int tabIndex ) {
                        this.m_ViewerState = vsm;

                        this.SuspendLayout();

                        this.Location = location;
                        this.Size = size;
                        this.TabIndex = tabIndex;
                        this.Name = "CloneTextBox";
                        this.ReadOnly = false;
                        this.Text = "";
                        this.Enabled = true;

                        if( this.m_ViewerState != null ) {
                            string text = "";
                            using( Synchronizer.Lock( this.m_ViewerState.SyncRoot ) ) {
                                text = this.m_ViewerState.SecondMonitorCloneCommand;
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
                                this.m_ViewerState.SecondMonitorCloneCommand = this.Text;
                            }
                        }

                        if( this.Parent != null ) {
                            ((SpecialCommandsGroup)this.Parent).ChangedClonedCommand( this.Text );
                        }
                    }
                }

                /// <summary>
                /// The label for the extended text box
                /// </summary>
                public class ExtendedInfoLabel : Label {
                    /// <summary>
                    /// Constructor
                    /// </summary>
                    public ExtendedInfoLabel( Point location, Size size, int tabIndex ) {
                        this.SuspendLayout();

                        this.FlatStyle = FlatStyle.System;
                        this.Location = location;
                        this.Size = size;
                        this.TabIndex = tabIndex;
                        this.Name = "ExtendedInfoLabel";
                        this.Text = Strings.ExtendedCommand;
                        this.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

                        this.ResumeLayout();
                    }
                }

                /// <summary>
                /// The text box containing the path
                /// </summary>
                public class ExtendedTextBox : TextBox {
                    private ViewerStateModel m_ViewerState;

                    public ExtendedTextBox( ViewerStateModel vsm, Point location, Size size, int tabIndex ) {
                        this.m_ViewerState = vsm;

                        this.SuspendLayout();

                        this.Location = location;
                        this.Size = size;
                        this.TabIndex = tabIndex;
                        this.Name = "ExtendedTextBox";
                        this.ReadOnly = false;
                        this.Text = "";
                        this.Enabled = true;

                        if( this.m_ViewerState != null ) {
                            string text = "";
                            using( Synchronizer.Lock( this.m_ViewerState.SyncRoot ) ) {
                                text = this.m_ViewerState.SecondMonitorExtendCommand;
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
                                this.m_ViewerState.SecondMonitorExtendCommand = this.Text;
                            }
                        }

                        if( this.Parent != null ) {
                            ((SpecialCommandsGroup)this.Parent).ChangedExtendedCommand( this.Text );
                        }
                    }
                }
            }

            /// <summary>
            /// This button will eventually restore the settings for the machine to the "best" mode
            /// for this machine
            /// </summary>
            public class RestoreSettingsButton : Button {
                private readonly ViewerStateModel m_VSM;

                public RestoreSettingsButton( ViewerStateModel vsm, Point location, Size size, int tabIndex ) {
                    this.m_VSM = vsm;

                    this.SuspendLayout();

                    this.FlatStyle = FlatStyle.System;
                    this.Location = location;
                    this.Size = size;
                    this.TabIndex = tabIndex;
                    this.Name = "RestoreSettingsButton";
                    this.Text = Strings.RestoreDefaults;

                    this.ResumeLayout();
                }

                protected override void OnClick( EventArgs e ) {
                    base.OnClick( e );

                    MessageBox.Show( Strings.NotYetImplemented );
/*
                    Control parent = this.Parent;
                    while( parent != null && !(parent is PropertiesForm) )
                        parent = parent.Parent;
                    PropertiesForm form = parent as PropertiesForm;

                    if( form != null ) {
                        DialogResult ok = MessageBox.Show( "Reset all properties to their default values?\n\nChanges will take effect immediately.", "Confirm Restore Defaults", MessageBoxButtons.OKCancel );
                        if( ok == DialogResult.OK ) {
                            // Copy default values to the application's ViewerStateModel.
                            // Then close the PropertiesForm immediately.  This is necessary
                            // because the PropertiesForm's controls don't update their values
                            // except when they're first created.
                            form.applicationModel.UpdateValues( new ViewerStateModel() );
                            form.Close();
                        }
                    }
*/                }
            }

            /// <summary>
            /// Test Button to enter into cloned display mode
            /// </summary>
            public class CloneButton : Button {
                private readonly ViewerStateModel m_VSM;

                public CloneButton( ViewerStateModel vsm, Point location, Size size, int tabIndex ) {
                    this.m_VSM = vsm;

                    this.SuspendLayout();

                    this.FlatStyle = FlatStyle.System;
                    this.Location = location;
                    this.Size = size;
                    this.TabIndex = tabIndex;
                    this.Name = "CloneButton";
                    this.Text = Strings.Clone;

                    this.ResumeLayout();
                }

                protected override void OnClick( EventArgs e ) {
                    base.OnClick( e );
                    using( Synchronizer.Lock( this.m_VSM.SyncRoot ) ) {
                        if( this.m_VSM.SecondMonitorEnabled ) {
                            if( this.m_VSM.SecondMonitorWindowsAPIsEnabled ) {
                                Misc.DispalyControlHelpers.RemoveAttachedDisplayDeviceFromDesktop();
                            }
                            if( this.m_VSM.SecondMonitorCustomCommandsEnabled && this.m_VSM.SecondMonitorCloneCommand != "" ) {
                                Misc.DispalyControlHelpers.InvokeRunDLL32Command( this.m_VSM.SecondMonitorCloneCommand );
                            }
                        }
                    }
                }
            }

            /// <summary>
            /// Test button to enter into extended desktop mode
            /// </summary>
            public class ExtendButton : Button {
                private readonly ViewerStateModel m_VSM;

                public ExtendButton( ViewerStateModel vsm, Point location, Size size, int tabIndex ) {
                    this.m_VSM = vsm;

                    this.SuspendLayout();

                    this.FlatStyle = FlatStyle.System;
                    this.Location = location;
                    this.Size = size;
                    this.TabIndex = tabIndex;
                    this.Name = "ExtendButton";
                    this.Text = Strings.Extend;

                    this.ResumeLayout();
                }

                protected override void OnClick( EventArgs e ) {
                    base.OnClick( e );
                    using( Synchronizer.Lock( this.m_VSM.SyncRoot ) ) {
                        if( this.m_VSM.SecondMonitorEnabled ) {
                            if( this.m_VSM.SecondMonitorWindowsAPIsEnabled ) {
                                Misc.DispalyControlHelpers.AddUnattachedDisplayDeviceToDesktop();
                            }
                            if( this.m_VSM.SecondMonitorCustomCommandsEnabled && this.m_VSM.SecondMonitorExtendCommand != "" ) {
                                Misc.DispalyControlHelpers.InvokeRunDLL32Command( this.m_VSM.SecondMonitorExtendCommand );
                            }
                        }
                    }
                }
            }
        }

        #endregion
    }
}
