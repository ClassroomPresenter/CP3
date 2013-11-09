// $Id: GeneralPropertiesPage.cs 2162 2010-01-13 23:01:56Z fred $

using System;
using System.Drawing;
using System.Windows.Forms;
using UW.ClassroomPresenter.Model.Viewer;

namespace UW.ClassroomPresenter.Viewer.PropertiesForm {

    /// <summary>
    /// Class that describes the General Properties that can be applied to Presenter
    /// </summary>
    public class GeneralPropertiesPage : TabPage {
        /// <summary>
        /// Constructs a new General Properties Page
        /// </summary>
        public GeneralPropertiesPage(ViewerStateModel vsm) {
            this.SuspendLayout();

            this.Location = new System.Drawing.Point(4, 22);
            this.Name = "GeneralTabPage";
            this.Size = new System.Drawing.Size(556, 248);
            this.TabIndex = 6;
            this.Text = Strings.General;

            // Add the controls
            this.Controls.Add(new DualMonitorGroup(vsm, new Point(16, 16), new Size(250, 40), 0));
            this.Controls.Add(new FilmstripPropertiesGroup(vsm, new Point(16, 62), new Size(250, 88), 2));
            this.Controls.Add(new LanguageGroup(vsm, new Point(16, 156), new Size(250, 70), 4));
            this.Controls.Add(new DefaultSettingsGroup(vsm, new Point(16, 232), new Size(250, 48), 3));
            this.Controls.Add(new SavingFilesGroup(vsm, new Point(290, 146), new Size(250, 39), 7));
            this.Controls.Add(new BroadcastIPGroup(vsm, new Point(290, 16), new Size(250, 40), 8));
            this.Controls.Add(new PenPropertiesGroup(vsm, new Point(290, 62), new Size(250, 83), 6));
            this.Controls.Add(new PrintingGroup(vsm, new Point(290, 232), new Size(250, 48), 9));
            this.Controls.Add(new StudentSubmissionTimerPropertiesGroup(vsm,new Point (290,186),new Size (250,43),10));
            this.ResumeLayout();
        }

        #region BroadcastIPGroup
        public class BroadcastIPGroup: GroupBox{
            public BroadcastIPGroup(ViewerStateModel model, Point location, Size size, int tabIndex) {

                this.SuspendLayout();

                this.FlatStyle = FlatStyle.System;
                this.Location = location;
                this.Size = size;
                this.TabIndex = tabIndex;
                this.Name = "groupBox1";
                this.TabStop = false;
                this.Text = Strings.BroadcastIP;

                // Add the controls
                this.Controls.Add(new BroadcastIPCheckBox(model, new Point(16, 12), new Size(220, 24), 0));

                this.ResumeLayout();
            }
            public class BroadcastIPCheckBox : CheckBox {
                private ViewerStateModel m_Model;

                /// <summary>
                /// Constructs the checkbox
                /// </summary>
                public BroadcastIPCheckBox(ViewerStateModel model, Point location, Size size, int tabIndex) {
                    this.m_Model = model;

                    this.SuspendLayout();

                    this.FlatStyle = FlatStyle.System;
                    this.Location = location;
                    this.Size = size;
                    this.TabIndex = tabIndex;
                    this.Name = "broadcastIPCheckBox";
                    this.Text = Strings.BroadcastIPAddress;

                    // Set the default value according to the model's current setting
                    // NOTE: Split into two steps to avoid deadlock with updating the check state
                    bool bShouldBeChecked;
                    using (Synchronizer.Lock(model.SyncRoot))
                        bShouldBeChecked = model.ShowIP;
                    this.Checked = bShouldBeChecked;
                    this.CheckState = bShouldBeChecked ? CheckState.Checked : CheckState.Unchecked;

                    this.ResumeLayout();
                }

                protected override void OnCheckedChanged(EventArgs e) {
                    base.OnCheckedChanged(e);

                    // Update the model value
                    if (this.m_Model != null) {
                        using (Synchronizer.Lock(this.m_Model.SyncRoot)) {
                            this.m_Model.ShowIP = this.Checked;
                        }
                    }
                }
            }
        }

        #endregion

        #region DualMonitorGroup

        /// <summary>
        /// The Dual Monitor Properties
        /// </summary>
        public class DualMonitorGroup : GroupBox {
            /// <summary>
            /// Constructs the Dual-Monitor Properties
            /// </summary>
            public DualMonitorGroup(ViewerStateModel model, Point location, Size size, int tabIndex) {

                this.SuspendLayout();

                this.FlatStyle = FlatStyle.System;
                this.Location = location;
                this.Size = size;
                this.TabIndex = tabIndex;
                this.Name = "groupBox1";
                this.TabStop = false;
                this.Text = Strings.DualMonitor;

                // Add the controls
                this.Controls.Add(new DualMonitorEnabledCheckBox(model, new Point(16, 12), new Size(220, 24), 0));

                this.ResumeLayout();
            }

            /// <summary>
            /// The checkbox for enabling dual-monitor support
            /// </summary>
            public class DualMonitorEnabledCheckBox : CheckBox {
                private ViewerStateModel m_Model;

                /// <summary>
                /// Constructs the checkbox
                /// </summary>
                public DualMonitorEnabledCheckBox(ViewerStateModel model, Point location, Size size, int tabIndex) {
                    this.m_Model = model;

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
                    using (Synchronizer.Lock(model.SyncRoot))
                        bShouldBeChecked = model.SecondMonitorEnabled;
                    this.Checked = bShouldBeChecked;
                    this.CheckState = bShouldBeChecked ? CheckState.Checked : CheckState.Unchecked;

                    this.ResumeLayout();
                }

                protected override void OnCheckedChanged(EventArgs e) {
                    base.OnCheckedChanged(e);

                    // Update the model value
                    if (this.m_Model != null) {
                        using (Synchronizer.Lock(this.m_Model.SyncRoot)) {
                            this.m_Model.SecondMonitorEnabled = this.Checked;
                        }
                    }
                }
            }
        }

        #endregion

        #region FilmstripPropertiesGroup

        /// <summary>
        ///
        /// </summary>
        public class FilmstripPropertiesGroup : GroupBox {
            /// <summary>
            ///
            /// </summary>
            public FilmstripPropertiesGroup( ViewerStateModel vsm, Point location, Size size, int tabIndex ) {

                this.SuspendLayout();

                this.FlatStyle = FlatStyle.System;
                this.Location = location;
                this.Size = size;
                this.TabIndex = tabIndex;
                this.Name = "FilmstripPropertiesGroup";
                this.TabStop = false;
                this.Text = Strings.FilmStrip;
                this.Enabled = true;

                // Add the controls
                this.Controls.Add( new SlidePreviewCheckBox( vsm, new Point(16, 36), new Size(200, 20), 1 ) );
                this.Controls.Add( new SlidePreviewWidthLabel( new Point(16, 64), new Size(120, 20), 2) );
                this.Controls.Add( new SlidePreviewWidthUpDown( vsm, new Point(141, 63), new Size(50, 20), 3) );
                this.Controls.Add(new AutoScrollCheckBox( vsm, new Point(16, 14), new Size(200, 20), 6) );

                this.ResumeLayout();
            }

            public class AutoScrollCheckBox : CheckBox {
                private ViewerStateModel m_VSModel;

                public AutoScrollCheckBox( ViewerStateModel vsm, Point location, Size size, int tabIndex ) {
                    this.m_VSModel = vsm;
                    this.SuspendLayout();

                    this.FlatStyle = FlatStyle.System;
                    this.Location = location;
                    this.Size = size;
                    this.TabIndex = tabIndex;
                    this.Name = "AutoScrollCheckBox";
                    this.Text = Strings.AutoScroll;

                    using(Synchronizer.Lock(this.m_VSModel.SyncRoot)) {
                        this.Checked = this.m_VSModel.AutoScrollEnabled;
                    }

                    this.ResumeLayout();
                }

                protected override void OnCheckedChanged(EventArgs e) {
                    using (Synchronizer.Lock(this.m_VSModel.SyncRoot)) {
                        this.m_VSModel.AutoScrollEnabled = this.Checked;
                    }

                    base.OnCheckedChanged (e);
                }

            }

            public class SlidePreviewCheckBox : CheckBox {
                private ViewerStateModel m_VSModel;

                public SlidePreviewCheckBox( ViewerStateModel vsm, Point location, Size size, int tabIndex ) {
                    this.m_VSModel = vsm;

                    this.SuspendLayout();

                    this.FlatStyle = FlatStyle.System;
                    this.Location = location;
                    this.Size = size;
                    this.TabIndex = tabIndex;
                    this.Name = "SlidePreviewCheckBox";
                    this.Text = Strings.EnablePreviews;

                    using(Synchronizer.Lock(this.m_VSModel.SyncRoot)) {
                        this.Checked = this.m_VSModel.SlidePreviewEnabled;
                    }

                    this.ResumeLayout();
                }

                protected override void OnCheckedChanged(EventArgs e) {
                    using (Synchronizer.Lock(this.m_VSModel.SyncRoot)) {
                        this.m_VSModel.SlidePreviewEnabled = this.Checked;
                    }
                    base.OnCheckedChanged (e);
                }

            }

            /// <summary>
            /// The Label for the SlidePreviewWidthUpDown
            /// </summary>
            public class SlidePreviewWidthLabel : Label
            {
                public SlidePreviewWidthLabel(Point location, Size size, int tabIndex)
                {
                    this.SuspendLayout();

                    this.FlatStyle = FlatStyle.System;
                    this.Location = location;
                    this.Size = size;
                    this.TabIndex = tabIndex;
                    this.Name = "SlidePreviewWidthLabel";
                    this.Text = Strings.WidthOfPreview;
                    this.Enabled = true;

                    this.ResumeLayout();
                }
            }

            /// <summary>
            /// The Slide Preview Width
            /// </summary>
            public class SlidePreviewWidthUpDown : NumericUpDown
            {
                private ViewerStateModel m_VSModel;

                public SlidePreviewWidthUpDown(ViewerStateModel vsm, Point location, Size size, int tabIndex)
                {
                    this.m_VSModel = vsm;
                    this.SuspendLayout();

                    this.Location = location;
                    this.Size = size;
                    this.TabIndex = tabIndex;
                    this.Name = "SlidePreviewWidthUpDown";
                    this.Minimum = 0;
                    this.Maximum = 1024;
                    this.Enabled = true;

                    int currValue;
                    using (Synchronizer.Lock(this.m_VSModel.SyncRoot))
                    {
                        currValue = this.m_VSModel.SlidePreviewWidth;
                    }
                    this.Value = currValue;

                    this.ResumeLayout();
                }

                protected override void OnValueChanged(EventArgs e)
                {
                    using (Synchronizer.Lock(this.m_VSModel.SyncRoot))
                    {
                        this.m_VSModel.SlidePreviewWidth = (int)this.Value;
                        this.m_VSModel.SlidePreviewHeight = (int)(this.Value * 0.75M);
                    }
                    base.OnValueChanged(e);
                }
            }
        }

        #endregion

        #region DefaultSettingsGroup

        /// <summary>
        ///
        /// </summary>
        public class DefaultSettingsGroup : GroupBox {
            /// <summary>
            ///
            /// </summary>
            public DefaultSettingsGroup(ViewerStateModel vsm, Point location, Size size, int tabIndex ) {

                this.SuspendLayout();

                this.FlatStyle = FlatStyle.System;
                this.Location = location;
                this.Size = size;
                this.TabIndex = tabIndex;
                this.Name = "DefaultSettingsGroup";
                this.TabStop = false;
                this.Text = Strings.DefaultSettings;
                this.Enabled = true;

                // Add the controls
                this.Controls.Add( new RestoreSettingsButton(vsm, new Point(43,18), new Size(164,23), 1 ) );

                this.ResumeLayout();
            }

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

                protected override void OnClick(EventArgs e) {
                    base.OnClick(e);

                    Control parent = this.Parent;
                    while (parent != null && !(parent is PropertiesForm))
                        parent = parent.Parent;
                    PropertiesForm form = parent as PropertiesForm;

                    if (form != null) {
                        DialogResult ok = MessageBox.Show(Strings.ConfirmRestoreDialog, Strings.ConfirmRestoreDefaults, MessageBoxButtons.OKCancel);
                        if (ok == DialogResult.OK) {
                            // Copy default values to the application's ViewerStateModel.
                            // Then close the PropertiesForm immediately.  This is necessary
                            // because the PropertiesForm's controls don't update their values
                            // except when they're first created.
                            form.applicationModel.UpdateValues(new ViewerStateModel());
                            form.Close();
                        }
                    }
                }
            }
        }

        #endregion

        #region SavingFilesGroup

        /// <summary>
        ///
        /// </summary>
        public class SavingFilesGroup : GroupBox {
            /// <summary>
            ///
            /// </summary>
            public SavingFilesGroup(ViewerStateModel vsm, Point location, Size size, int tabIndex) {

                this.SuspendLayout();

                this.FlatStyle = FlatStyle.System;
                this.Location = location;
                this.Size = size;
                this.TabIndex = tabIndex;
                this.Name = "SavingFilesGroup";
                this.TabStop = false;
                this.Text = Strings.SavingFiles;
                this.Enabled = true;

                // Add the controls
                this.Controls.Add(new ClosePromptCheckBox(vsm, new Point(16, 12), new Size(220, 24), 3));

                this.ResumeLayout();
            }

            public class ClosePromptCheckBox : CheckBox {
                private readonly ViewerStateModel m_Model;

                public ClosePromptCheckBox(ViewerStateModel model, Point location, Size size, int tabIndex) {
                    this.m_Model = model;

                    this.SuspendLayout();

                    this.FlatStyle = FlatStyle.System;
                    this.Location = location;
                    this.Size = size;
                    this.TabIndex = tabIndex;
                    this.Name = "ClosePromptCheckBox";
                    this.Text = Strings.PromptOnClose;

                    // Set the default value according to the model's current setting
                    // NOTE: Split into two steps to avoid deadlock with updating the check state
                    bool bShouldBeChecked;
                    using (Synchronizer.Lock(model.SyncRoot))
                        bShouldBeChecked = model.SaveOnClose;
                    this.Checked = bShouldBeChecked;
                    this.CheckState = bShouldBeChecked ? CheckState.Checked : CheckState.Unchecked;

                    this.ResumeLayout();
                }

                protected override void OnCheckedChanged(EventArgs e) {
                    base.OnCheckedChanged(e);

                    // Update the model value
                    if (this.m_Model != null) {
                        using (Synchronizer.Lock(this.m_Model.SyncRoot)) {
                            this.m_Model.SaveOnClose = this.Checked;
                        }
                    }
                }
            }
        }

        #endregion

        #region PenPropertiesGroup

        /// <summary>
        ///
        /// </summary>
        public class PenPropertiesGroup : GroupBox {
            /// <summary>
            ///
            /// </summary>
            public PenPropertiesGroup( ViewerStateModel vsm, Point location, Size size, int tabIndex ) {

                this.SuspendLayout();

                this.FlatStyle = FlatStyle.System;
                this.Location = location;
                this.Size = size;
                this.TabIndex = tabIndex;
                this.Name = "PenPropertiesGroup";
                this.TabStop = false;
                this.Text = Strings.Pen;
                this.Enabled = true;

                this.Controls.Add(new UseLightColorCheckBox(vsm, new Point(16, 14), new Size(210, 20), 1));
                this.Controls.Add(new PenWidthLabel(new Point(16, 38), new Size(110, 20), 2));
                this.Controls.Add(new PenWidthUpDown(vsm, new Point(130, 36), new Size(50, 20), 3));
                this.Controls.Add(new HLWidthLabel(new Point(16, 61), new Size(110, 20), 4));
                this.Controls.Add(new HLWidthUpDown(vsm, new Point(130, 59), new Size(50, 20), 5));

                this.ResumeLayout();
            }

            public class UseLightColorCheckBox : CheckBox {
                private readonly ViewerStateModel m_Model;

                public UseLightColorCheckBox(ViewerStateModel model, Point location, Size size, int tabIndex) {
                    this.m_Model = model;

                    this.SuspendLayout();

                    this.FlatStyle = FlatStyle.System;
                    this.Location = location;
                    this.Size = size;
                    this.TabIndex = tabIndex;
                    this.Name = "UseLightColorCheckBox";
                    this.Text = Strings.UseLightColorSet;

                    bool bShouldBeChecked;
                    using (Synchronizer.Lock(model.SyncRoot))
                        bShouldBeChecked = model.UseLightColorSet;
                    this.Checked = bShouldBeChecked;
                    this.CheckState = bShouldBeChecked ? CheckState.Checked : CheckState.Unchecked;

                    this.ResumeLayout();
                }

                protected override void OnCheckedChanged(EventArgs e) {
                    base.OnCheckedChanged(e);

                    // Update the model value
                    if (this.m_Model != null) {
                        using (Synchronizer.Lock(this.m_Model.SyncRoot)) {
                            this.m_Model.UseLightColorSet = this.Checked;
                        }
                    }
                }
            }
            
            /// <summary>
            /// The Label for the PenWidthUpDown
            /// </summary>
            public class PenWidthLabel : Label {
                public PenWidthLabel(Point location, Size size, int tabIndex) {
                    this.SuspendLayout();

                    this.FlatStyle = FlatStyle.System;
                    this.Location = location;
                    this.Size = size;
                    this.TabIndex = tabIndex;
                    this.Name = "PenWidthLabel";
                    this.Text = Strings.DefaultPenWidth;
                    this.Enabled = true;

                    this.ResumeLayout();
                }
            }

            /// <summary>
            /// The Pen Width
            /// </summary>
            public class PenWidthUpDown : NumericUpDown {
                private ViewerStateModel m_VSModel;

                public PenWidthUpDown(ViewerStateModel vsm, Point location, Size size, int tabIndex) {
                    this.m_VSModel = vsm;
                    this.SuspendLayout();

                    this.Location = location;
                    this.Size = size;
                    this.TabIndex = tabIndex;
                    this.Name = "PenWidthUpDown";
                    this.Minimum = 0;
                    this.Maximum = 1024;
                    this.Enabled = true;

                    int currValue;
                    using (Synchronizer.Lock(this.m_VSModel.SyncRoot)) {
                        currValue = this.m_VSModel.DefaultPenWidth;
                    }
                    this.Value = currValue;

                    this.ResumeLayout();
                }

                protected override void OnValueChanged(EventArgs e) {
                    using (Synchronizer.Lock(this.m_VSModel.SyncRoot)) {
                        this.m_VSModel.DefaultPenWidth = (int)this.Value;
                    }
                    base.OnValueChanged(e);
                }
            }

            /// <summary>
            /// The Label for the HLWidthUpDown
            /// </summary>
            public class HLWidthLabel : Label
            {
                public HLWidthLabel(Point location, Size size, int tabIndex)
                {
                    this.SuspendLayout();

                    this.FlatStyle = FlatStyle.System;
                    this.Location = location;
                    this.Size = size;
                    this.TabIndex = tabIndex;
                    this.Name = "HLWidthLabel";
                    this.Text = Strings.DefaultHLWidth;
                    this.Enabled = true;

                    this.ResumeLayout();
                }
            }

            /// <summary>
            /// The HL Width
            /// </summary>
            public class HLWidthUpDown : NumericUpDown
            {
                private ViewerStateModel m_VSModel;

                public HLWidthUpDown(ViewerStateModel vsm, Point location, Size size, int tabIndex)
                {
                    this.m_VSModel = vsm;
                    this.SuspendLayout();

                    this.Location = location;
                    this.Size = size;
                    this.TabIndex = tabIndex;
                    this.Name = "HLWidthUpDown";
                    this.Minimum = 0;
                    this.Maximum = 1024;
                    this.Enabled = true;

                    int currValue;
                    using (Synchronizer.Lock(this.m_VSModel.SyncRoot))
                    {
                        currValue = this.m_VSModel.DefaultHLWidth;
                    }
                    this.Value = currValue;

                    this.ResumeLayout();
                }

                protected override void OnValueChanged(EventArgs e)
                {
                    using (Synchronizer.Lock(this.m_VSModel.SyncRoot))
                    {
                        this.m_VSModel.DefaultHLWidth = (int)this.Value;
                    }
                    base.OnValueChanged(e);
                }
            }
        }

        #endregion

        #region PrintingGroup

        /// <summary>
        ///
        /// </summary>
        public class PrintingGroup : GroupBox {
            /// <summary>
            ///
            /// </summary>
            public PrintingGroup( ViewerStateModel vsm, Point location, Size size, int tabIndex ) {

                this.SuspendLayout();

                this.FlatStyle = FlatStyle.System;
                this.Location = location;
                this.Size = size;
                this.TabIndex = tabIndex;
                this.Name = "PrintingGroup";
                this.TabStop = false;
                this.Text = Strings.Printing;
                this.Enabled = true;

                // Add the controls
                this.Controls.Add( new PagesDropDown( vsm, new Point( 16, 18 ), new Size( 40, 20 ), 0 ) );
                this.Controls.Add( new PagesInfoLabel( new Point( 60, 20), new Size( 170, 24 ), 1 ) );

                this.ResumeLayout();
            }

            public class PagesDropDown : System.Windows.Forms.ComboBox {
                private ViewerStateModel m_VSModel;

                public PagesDropDown( ViewerStateModel vsm, Point location, Size size, int tabIndex ) {
                    this.m_VSModel = vsm;
                    this.SuspendLayout();

                    this.FlatStyle = FlatStyle.System;
                    this.Location = location;
                    this.Size = size;
                    this.TabIndex = tabIndex;
                    this.Items.Add( 1 );
                    this.Items.Add( 2 );
                    this.Items.Add( 4 );
                    this.Items.Add( 6 );

                    using(Synchronizer.Lock( vsm.SyncRoot ) )
                        this.SelectedItem = vsm.SlidesPerPage;

                    this.ResumeLayout();
                }

                protected override void  OnSelectedValueChanged(EventArgs e) {
                    using( Synchronizer.Lock( this.m_VSModel.SyncRoot ) ) {
                        this.m_VSModel.SlidesPerPage = (int)this.SelectedItem;
                    }
                 	base.OnSelectedValueChanged(e);
                }
            }

            public class PagesInfoLabel : Label {
                public PagesInfoLabel( Point location, Size size, int tabIndex ) {
                    this.SuspendLayout();

                    this.FlatStyle = FlatStyle.System;
                    this.Location = location;
                    this.Size = size;
                    this.TabIndex = tabIndex;
                    this.Name = "PagesInfoLabel";
                    this.Text = Strings.PagesPerSheet;

                    this.ResumeLayout();
                }
            }


        }

        #endregion

        #region LanguageGroup
        
        /// <summary>
        ///
        /// </summary>
        public class LanguageGroup : GroupBox {
            /// <summary>
            /// Constructor a LanguageGroup
            /// </summary>
            public LanguageGroup( ViewerStateModel vsm, Point location, Size size, int tabIndex ) {

                this.SuspendLayout();

                this.FlatStyle = FlatStyle.System;
                this.Location = location;
                this.Size = size;
                this.TabIndex = tabIndex;
                this.Name = "LanguageGroup";
                this.TabStop = false;
                this.Text = Strings.Language;
                this.Enabled = true;

                this.Controls.Add(new LanguageComboBox(vsm, new Point(16, 44), new Size(180, 21), 0));
                this.Controls.Add(new LanguageLabel(new Point(16, 16), new Size(220, 28), 1));

                this.ResumeLayout();
            }

            public class LanguageComboBox : ComboBox {
                private ViewerStateModel m_VSModel;
                private string[] m_Language = { "English", "简体中文", "Português", "Español", "Français", "繁體中文", "Eλληνικά" };
                private string[] m_Local = { "en", "zh-CN", "pt-BR", "es-ES", "fr-FR", "zh-TW", "el-GR" };


                public LanguageComboBox(ViewerStateModel vsm, Point location, Size size, int tabIndex) {
                    this.m_VSModel = vsm;
                    
                    this.SuspendLayout();

                    this.Location = location;
                    this.Size = size;
                    this.TabIndex = tabIndex;
                    this.Name = "LanguageComboBox";

                    this.Items.AddRange(this.m_Language);

                    string lang;

                    using (Synchronizer.Lock(this.m_VSModel.SyncRoot)) {
                        lang = this.m_VSModel.Language;
                    }

                    for (int i = 0; i < this.m_Local.Length; i++) {
                        if (lang.Equals(this.m_Local[i]))
                        {
                            this.SelectedIndex = i;
                            break;
                        }
                    }
                    
                    if (this.SelectedIndex == -1)
                        this.SelectedIndex = 0;

                    this.ResumeLayout();
                }

                protected override void OnSelectedIndexChanged(EventArgs e) {
                    using (Synchronizer.Lock(this.m_VSModel.SyncRoot)) {
                        this.m_VSModel.Language = this.m_Local[this.SelectedIndex];
                    }
                    base.OnSelectedIndexChanged(e);
                }
            }

            /// <summary>
            /// The Label for the Language
            /// </summary>
            public class LanguageLabel : Label {
                public LanguageLabel(Point location, Size size, int tabIndex) {
                    this.SuspendLayout();

                    this.FlatStyle = FlatStyle.System;
                    this.Location = location;
                    this.Size = size;
                    this.TabIndex = tabIndex;
                    this.Name = "LanguageLabel";
                    this.Text = Strings.NeedRestart;
                    this.Enabled = true;

                    this.ResumeLayout();
                }
            }

        }

        #endregion

        #region StudentSubmissionTimerPropertiesGroup

        /// <summary>
        ///
        /// </summary>
        public class StudentSubmissionTimerPropertiesGroup : GroupBox
        {
            /// <summary>
            ///
            /// </summary>
            public StudentSubmissionTimerPropertiesGroup(ViewerStateModel vsm, Point location, Size size, int tabIndex)
            {

                this.SuspendLayout();

                this.FlatStyle = FlatStyle.System;
                this.Location = location;
                this.Size = size;
                this.TabIndex = tabIndex;
                this.Name = "StudentSubmissionTimerPropertiesGroup";
                this.TabStop = false;
                this.Text = Strings.StudentSubmissionTimerProperties;
                this.Enabled = true;

                this.Controls.Add(new SubmissionIntervalLabel(new Point(16, 14), new Size(110, 25), 2));
                this.Controls.Add(new SubmissionIntervalUpDown(vsm, new Point(130, 16), new Size(50, 20), 3));

                this.ResumeLayout();
            }

            
            /// <summary>
            /// The Label for the SubmissionInterval
            /// </summary>
            public class SubmissionIntervalLabel : Label
            {
                public SubmissionIntervalLabel(Point location, Size size, int tabIndex)
                {
                    this.SuspendLayout();

                    this.FlatStyle = FlatStyle.System;
                    this.Location = location;
                    this.Size = size;
                    this.TabIndex = tabIndex;
                    this.Name = "SubmissionIntervalLabel";
                    this.Text = Strings.SubmissionIntervalLabel;
                    this.Enabled = true;

                    this.ResumeLayout();
                }
            }

            /// <summary>
            /// Submission Interval
            /// </summary>
            public class SubmissionIntervalUpDown : NumericUpDown
            {
                private ViewerStateModel m_VSModel;

                public SubmissionIntervalUpDown(ViewerStateModel vsm, Point location, Size size, int tabIndex)
                {
                    this.m_VSModel = vsm;
                    this.SuspendLayout();

                    this.Location = location;
                    this.Size = size;
                    this.TabIndex = tabIndex;
                    this.Name = "SubmissionIntervalUpDown";                  

                    int currValue;
                    using (Synchronizer.Lock(this.m_VSModel.SyncRoot))
                    {
                        currValue = this.m_VSModel.StudentSubmissionInterval;
                    }
                    this.Value = currValue;

                    this.Minimum = 1;
                    this.Maximum = 20; 
                    this.Enabled = true;

                    this.ResumeLayout();
                }

                protected override void OnValueChanged(EventArgs e)
                {
                    using (Synchronizer.Lock(this.m_VSModel.SyncRoot))
                    {
                        this.m_VSModel.StudentSubmissionInterval = (int)this.Value;
                    }
                    base.OnValueChanged(e);
                }
            }

        }

        #endregion
    }
}
