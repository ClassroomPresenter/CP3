// $Id: RecorderForm.cs 774 2005-09-21 20:22:33Z pediddle $

using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using System.Data;
using System.Net;
using UW.ClassroomPresenter.Test.Network.Common;

namespace UW.ClassroomPresenter.Test.Network.Recorder {
    /// <summary>
    /// Summary description for Form1.
    /// </summary>
    public class RecorderForm : System.Windows.Forms.Form {
        private System.Windows.Forms.MainMenu mainMenu;
        private System.Windows.Forms.MenuItem fileMenuItem;
        private System.Windows.Forms.MenuItem helpMenuItem;
        private System.Windows.Forms.MenuItem aboutMenuItem;
        private System.Windows.Forms.MenuItem exitMenuItem;
        private System.Windows.Forms.Button startButton;
        private System.Windows.Forms.Button stopButton;
        private System.Windows.Forms.CheckBox showTrafficCheckbox;
        private TrafficDisplayForm tdf;
        private NetworkArchiver archiver = new NetworkArchiver();
        private System.Windows.Forms.ComboBox connectionComboBox;
        private RTPReceiveConnection connection;
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.Container components = null;

        public RecorderForm() {
            //
            // Required for Windows Form Designer support
            //
            InitializeComponent();

            //
            // TODO: Add any constructor code after InitializeComponent call
            //
            this.tdf = new TrafficDisplayForm();
            this.connectionComboBox.SelectedIndex = 0;
        }

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        protected override void Dispose( bool disposing ) {
            if( disposing ) {
                if (components != null) {
                    components.Dispose();
                }
            }
            base.Dispose( disposing );
        }

        #region Windows Form Designer generated code
        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            this.mainMenu = new System.Windows.Forms.MainMenu();
            this.fileMenuItem = new System.Windows.Forms.MenuItem();
            this.exitMenuItem = new System.Windows.Forms.MenuItem();
            this.helpMenuItem = new System.Windows.Forms.MenuItem();
            this.aboutMenuItem = new System.Windows.Forms.MenuItem();
            this.startButton = new System.Windows.Forms.Button();
            this.stopButton = new System.Windows.Forms.Button();
            this.showTrafficCheckbox = new System.Windows.Forms.CheckBox();
            this.connectionComboBox = new System.Windows.Forms.ComboBox();
            this.SuspendLayout();
            // 
            // mainMenu
            // 
            this.mainMenu.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
                                                                                     this.fileMenuItem,
                                                                                     this.helpMenuItem});
            // 
            // fileMenuItem
            // 
            this.fileMenuItem.Index = 0;
            this.fileMenuItem.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
                                                                                         this.exitMenuItem});
            this.fileMenuItem.Text = "File";
            // 
            // exitMenuItem
            // 
            this.exitMenuItem.Index = 0;
            this.exitMenuItem.Text = "E&xit";
            this.exitMenuItem.Click += new System.EventHandler(this.onExitMenuItemClick);
            // 
            // helpMenuItem
            // 
            this.helpMenuItem.Index = 1;
            this.helpMenuItem.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
                                                                                         this.aboutMenuItem});
            this.helpMenuItem.Text = "&Help";
            // 
            // aboutMenuItem
            // 
            this.aboutMenuItem.Index = 0;
            this.aboutMenuItem.Text = "&About...";
            this.aboutMenuItem.Click += new System.EventHandler(this.onAboutMenuItemClick);
            // 
            // startButton
            // 
            this.startButton.Location = new System.Drawing.Point(32, 192);
            this.startButton.Name = "startButton";
            this.startButton.TabIndex = 0;
            this.startButton.Text = "Start";
            this.startButton.Click += new System.EventHandler(this.onStartButtonClick);
            // 
            // stopButton
            // 
            this.stopButton.Enabled = false;
            this.stopButton.Location = new System.Drawing.Point(176, 192);
            this.stopButton.Name = "stopButton";
            this.stopButton.TabIndex = 1;
            this.stopButton.Text = "Stop";
            this.stopButton.Click += new System.EventHandler(this.onStopButtonClick);
            // 
            // showTrafficCheckbox
            // 
            this.showTrafficCheckbox.Location = new System.Drawing.Point(72, 232);
            this.showTrafficCheckbox.Name = "showTrafficCheckbox";
            this.showTrafficCheckbox.Size = new System.Drawing.Size(144, 24);
            this.showTrafficCheckbox.TabIndex = 2;
            this.showTrafficCheckbox.Text = "Show Network Traffic";
            this.showTrafficCheckbox.CheckedChanged += new System.EventHandler(this.onShowTrafficCheckboxCheckedChanged);
            // 
            // connectionComboBox
            // 
            this.connectionComboBox.Items.AddRange(new object[] {
                                                                    "Not Connected",
                                                                    "Classroom 1 (234.3.0.1)",
                                                                    "Classroom 2 (234.3.0.2)",
                                                                    "Classroom 3 (234.3.0.3)",
                                                                    "Classroom 4 (234.3.0.4)",
                                                                    "Classroom 5 (234.3.0.5)"});
            this.connectionComboBox.Location = new System.Drawing.Point(32, 80);
            this.connectionComboBox.Name = "connectionComboBox";
            this.connectionComboBox.Size = new System.Drawing.Size(224, 21);
            this.connectionComboBox.TabIndex = 3;
            // 
            // RecorderForm
            // 
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.ClientSize = new System.Drawing.Size(292, 266);
            this.Controls.Add(this.connectionComboBox);
            this.Controls.Add(this.showTrafficCheckbox);
            this.Controls.Add(this.stopButton);
            this.Controls.Add(this.startButton);
            this.Menu = this.mainMenu;
            this.Name = "RecorderForm";
            this.Text = "Classroom Recorder";
            this.ResumeLayout(false);

        }
        #endregion

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main() {
            Application.Run(new RecorderForm());
        }

        #region Event Handlers

        #region Menu Items

        private void onExitMenuItemClick(object sender, System.EventArgs e) {
            this.Close();
        }

        private void onAboutMenuItemClick(object sender, System.EventArgs e) {
            Misc.AboutForm af = new UW.ClassroomPresenter.Misc.AboutForm();
            af.ShowDialog();
        }

        #endregion

        #region Checkboxes

        private void onShowTrafficCheckboxCheckedChanged(object sender, System.EventArgs e) {
            if (this.showTrafficCheckbox.Checked) {
                tdf.Show();
            } else {
                tdf.Hide();
            }
        }

        #endregion

        #region Buttons

        private void onStartButtonClick(object sender, System.EventArgs e) {
            //Check to see if we selected a classroom first
            IPEndPoint ipe;
            IPAddress ipa;
            switch (this.connectionComboBox.SelectedIndex) {
                case (1) :
                    //Classroom 1
                    ipa = IPAddress.Parse("234.3.0.1");
                    break;
                case (2) :
                    //Classroom 2
                    ipa = IPAddress.Parse("234.3.0.2");
                    break;
                case (3) :
                    //Classroom 3
                    ipa = IPAddress.Parse("234.3.0.3");
                    break;
                case (4) :
                    //Classroom 4
                    ipa = IPAddress.Parse("234.3.0.4");
                    break;
                case (5) :
                    //Classroom 5
                    ipa = IPAddress.Parse("234.3.0.5");
                    break;
                default :
                    //Not connected
                    MessageBox.Show("Error: A classroom must be selected!", "Classroom Recorder", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
                    return;
            }
            ipe = new IPEndPoint(ipa, 5004);
            //Prompt to save
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "CP3 Network Capture (*.cnc)|*.cnc";
            if (sfd.ShowDialog() == DialogResult.OK) {
                this.archiver.NewArchive(sfd.FileName);
                this.connectionComboBox.Enabled = false;
                this.startButton.Enabled = false;
                this.stopButton.Enabled = true;
                
                this.connection = new RTPReceiveConnection(ipe, this.archiver);
                this.connection.EventArchived += new EventArchivedDelegate(this.HandleNetworkEventReceived);
            }
        }

        private void onStopButtonClick(object sender, System.EventArgs e) {
            this.connection.EventArchived -= new EventArchivedDelegate(this.HandleNetworkEventReceived);
            this.connection.Dispose();
            this.archiver.CloseArchive();
            this.connectionComboBox.Enabled = true;
            this.stopButton.Enabled = false;
            this.startButton.Enabled = true;
        }

        #endregion

        #region RTP Events

        public void HandleNetworkEventReceived(object sender, NetworkEvent e) {
            this.tdf.AppendText(e.ToString() + " " + e.source);
        }

        #endregion

        #endregion

    }
}
