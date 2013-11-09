// $Id: NetworkPropertiesPage.cs 2164 2010-01-19 19:58:04Z fred $

using System;
using System.Drawing;
using System.Windows.Forms;
using UW.ClassroomPresenter.Model.Viewer;

namespace UW.ClassroomPresenter.Viewer.PropertiesForm {

    /// <summary>
    /// Summary description for NetworkPropertiesPage.
    /// </summary>
    public class NetworkPropertiesPage : TabPage {
        
        /// <summary>
        /// Constructs a new Network Properties page
        /// </summary>
        public NetworkPropertiesPage(ViewerStateModel vsm) {
            this.SuspendLayout();

            this.Location = new System.Drawing.Point(4, 22);
            this.Name = "NetworkTabPage";
            this.Size = new System.Drawing.Size(556, 248);
            this.TabIndex = 1;
            this.Text = Strings.Network;

            // Add the controls
            this.Controls.Add(new BeaconIntervalLabel(new Point(16, 16), new Size(170, 32), 0));
            this.Controls.Add(new BeaconIntervalUpDown(vsm, new Point(188, 16), new Size(56, 20), 1));
            this.Controls.Add(new BeaconIntervalUnitsLabel(new Point(252, 16), new Size(68, 23), 2));
            this.Controls.Add(new PacketDelayLabel(new Point(16, 56), new Size(170, 32), 3));
            this.Controls.Add(new PacketDelayUpDown(vsm, new Point(188, 56), new Size(56, 20), 4));
            this.Controls.Add(new PacketDelayUnitsLabel(new Point(252, 56), new Size(24, 23), 5));
            this.Controls.Add(new FECLabel(new Point(16, 88), new Size(170, 32), 6));
            this.Controls.Add(new FECUpDown(vsm, new Point(188, 88), new Size(56, 20), 7));
            this.Controls.Add(new FECUnitsLabel(new Point(252, 88), new Size(78, 23), 8));
            this.Controls.Add(new BroadcastDisabledCheckBox(vsm, new Point(16, 128), new Size(350, 20), 9));

            this.ResumeLayout();
        }

        #region FECControls

        /// <summary>
        /// The Label for the FEC Setting
        /// </summary>
        public class FECLabel : Label {
            public FECLabel( Point location, Size size, int tabIndex ) {
                this.SuspendLayout();

                this.FlatStyle = FlatStyle.System;
                this.Location = location;
                this.Size = size;
                this.TabIndex = tabIndex;
                this.Name = "FECLabel";
                this.Text = Strings.ForwardErrorCorrection;
                this.Enabled = true;

                this.ResumeLayout();
            }
        }
        /// <summary>
        /// The Units Label for the FEC Setting
        /// </summary>
        public class FECUnitsLabel : Label {
            public FECUnitsLabel( Point location, Size size, int tabIndex ) {
                this.SuspendLayout();

                this.FlatStyle = FlatStyle.System;
                this.Location = location;
                this.Size = size;
                this.TabIndex = tabIndex;
                this.Name = "FECUnitLabel";
                this.Text = Strings.Percent;
                this.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
                this.Enabled = true;

                this.ResumeLayout();
            }
        }
        /// <summary>
        /// The Units Label for the FEC Setting
        /// </summary>
        public class FECUpDown : NumericUpDown {
            private ViewerStateModel m_VSModel;

            public FECUpDown( ViewerStateModel vsm, Point location, Size size, int tabIndex ) {
                this.m_VSModel = vsm;
                this.SuspendLayout();

                this.Location = location;
                this.Size = size;
                this.TabIndex = tabIndex;
                this.Name = "FECUpDown";
                this.Maximum = 100;
                this.Minimum = 0;
                this.Enabled = true;

                //Retrive the current value from the model
                int currValue;
                using (Synchronizer.Lock(vsm.SyncRoot)) {
                    currValue = vsm.ForwardErrorCorrection;
                }
                this.Value = currValue;

                this.ResumeLayout();
            }

            protected override void OnValueChanged(EventArgs e) {
                using (Synchronizer.Lock(this.m_VSModel.SyncRoot)) {
                    if (this.m_VSModel.ForwardErrorCorrection != this.Value) {
                        this.m_VSModel.ForwardErrorCorrection = (int)this.Value;
                    }
                }
                base.OnValueChanged (e);
            }
        }

        #endregion

        #region BeaconPeriodControls

        /// <summary>
        /// The Label for the BeaconPeriod Setting
        /// </summary>
        public class BeaconIntervalLabel : Label {
            public BeaconIntervalLabel( Point location, Size size, int tabIndex ) {
                this.SuspendLayout();

                this.FlatStyle = FlatStyle.System;
                this.Location = location;
                this.Size = size;
                this.TabIndex = tabIndex;
                this.Name = "BeaconIntervalLabel";
                this.Text = Strings.RTPBeaconInterval;
                this.Enabled = true;

                this.ResumeLayout();
            }
        }
        /// <summary>
        /// The Units Label for the BeaconPeriod Setting
        /// </summary>
        public class BeaconIntervalUnitsLabel : Label {
            public BeaconIntervalUnitsLabel( Point location, Size size, int tabIndex ) {
                this.SuspendLayout();

                this.FlatStyle = FlatStyle.System;
                this.Location = location;
                this.Size = size;
                this.TabIndex = tabIndex;
                this.Name = "BeaconIntervalUnitsLabel";
                this.Text = Strings.Second;
                this.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
                this.Enabled = true;

                this.ResumeLayout();
            }
        }
        /// <summary>
        /// The control to set the BeaconPeriod for slides
        /// </summary>
        public class BeaconIntervalUpDown : NumericUpDown {
            private readonly ViewerStateModel m_VSModel;

            public BeaconIntervalUpDown(ViewerStateModel vsm, Point location, Size size, int tabIndex ) {
                this.m_VSModel = vsm;

                this.SuspendLayout();

                this.Location = location;
                this.Size = size;
                this.TabIndex = tabIndex;
                this.Name = "BeaconIntervalUpDown";

                //Retrive the current value from the model
                int currValue;
                using (Synchronizer.Lock(vsm.SyncRoot)) {
                    currValue = vsm.BeaconInterval;
                }
                this.Value = currValue;

                this.Maximum = 300.0M;
                this.Minimum = 1M;
                this.Increment = 1M;
                this.DecimalPlaces = 0;
                this.Enabled = true;

                this.ResumeLayout();
            }

            protected override void OnValueChanged(EventArgs e) {
                using (Synchronizer.Lock(this.m_VSModel.SyncRoot)) {
                    if (this.m_VSModel.BeaconInterval != this.Value) {
                        this.m_VSModel.BeaconInterval = (int)this.Value;
                    }
                }
                base.OnValueChanged(e);
            }
        }

        #endregion

        #region PacketDelayControls

        /// <summary>
        /// The Label for the PacketDelay Setting
        /// </summary>
        public class PacketDelayLabel : Label {
            public PacketDelayLabel( Point location, Size size, int tabIndex ) {
                this.SuspendLayout();

                this.FlatStyle = FlatStyle.System;
                this.Location = location;
                this.Size = size;
                this.TabIndex = tabIndex;
                this.Name = "PacketDelayLabel";
                this.Text = Strings.InterpacketDelay;
                this.Enabled = true;

                this.ResumeLayout();
            }
        }
        /// <summary>
        /// The Units Label for the PacketDelay Setting
        /// </summary>
        public class PacketDelayUnitsLabel : Label {
            public PacketDelayUnitsLabel( Point location, Size size, int tabIndex ) {
                this.SuspendLayout();

                this.FlatStyle = FlatStyle.System;
                this.Location = location;
                this.Size = size;
                this.TabIndex = tabIndex;
                this.Name = "PacketDelayUnitLabel";
                this.Text = "ms";
                this.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
                this.Enabled = true;

                this.ResumeLayout();
            }
        }
        /// <summary>
        /// The control to set the PacketDelay for slides
        /// </summary>
        public class PacketDelayUpDown : NumericUpDown {
            private ViewerStateModel m_VSModel;

            public PacketDelayUpDown( ViewerStateModel vsm, Point location, Size size, int tabIndex ) {
                this.m_VSModel = vsm;
                this.SuspendLayout();

                this.Location = location;
                this.Size = size;
                this.TabIndex = tabIndex;
                this.Name = "PacketDelayUpDown";
                this.Maximum = 30;
                this.Minimum = 0;
                this.Enabled = true;

                //Retrive the current value from the model
                int currValue;
                using (Synchronizer.Lock(vsm.SyncRoot)) {
                    currValue = vsm.InterPacketDelay;
                }
                this.Value = currValue;

                this.ResumeLayout();
            }

            protected override void OnValueChanged(EventArgs e) {
                using (Synchronizer.Lock(this.m_VSModel.SyncRoot)) {
                    if (this.m_VSModel.InterPacketDelay != this.Value) {
                        this.m_VSModel.InterPacketDelay = (int)this.Value;
                    }
                }
                base.OnValueChanged (e);
            }
        }

        #endregion

        #region Broadcast Disable Checkbox



        /// <summary>
        /// The check box specifying if broadcast is disabled
        /// </summary>
        public class BroadcastDisabledCheckBox : CheckBox {
            private ViewerStateModel m_ViewerState;

            /// <summary>
            /// Constructor for the check box
            /// </summary>
            public BroadcastDisabledCheckBox(ViewerStateModel model, Point location, Size size, int tabIndex) {
                this.m_ViewerState = model;

                this.SuspendLayout();

                this.FlatStyle = FlatStyle.System;
                this.Location = location;
                this.Size = size;
                this.TabIndex = tabIndex;
                this.Enabled = true;
                this.Name = "BroadcastDisabledCheckBox";
                this.Text = Strings.DisableAdvertisementBroadcast;

                bool bShouldBeChecked = false;

                if (model != null) {
                    using (Synchronizer.Lock(model.SyncRoot)) {
                        bShouldBeChecked = model.BroadcastDisabled;
                    }
                }

                if (bShouldBeChecked) {
                    this.Checked = true;
                    this.CheckState = CheckState.Checked;
                }
                else {
                    this.Checked = false;
                    this.CheckState = CheckState.Unchecked;
                }

                this.ResumeLayout();
            }

            protected override void OnCheckedChanged(EventArgs e) {
                base.OnCheckedChanged(e);

                // Update the model value
                if (this.m_ViewerState != null) {
                    using (Synchronizer.Lock(this.m_ViewerState.SyncRoot)) {
                        this.m_ViewerState.BroadcastDisabled = this.Checked;
                    }
                }
            }
        }


        #endregion Broadcast Disable Checkbox
    }
}
