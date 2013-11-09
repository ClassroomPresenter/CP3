// $Id: AboutForm.cs 2233 2013-09-27 22:17:28Z fred $
using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;

namespace UW.ClassroomPresenter.Misc {
    /// <summary>
    /// Summary description for AboutForm.
    /// </summary>
    public class AboutForm : System.Windows.Forms.Form {
        private System.Windows.Forms.Button okButton;
        private System.Windows.Forms.LinkLabel aboutLinkLabel;
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.Container components = null;

        public AboutForm() : this("") {}

        public AboutForm(string buildDate) {
            //
            // Required for Windows Form Designer support
            //
            InitializeComponent();

            //
            // TODO: Add any constructor code after InitializeComponent call
            //

            string appTitle = "";
            System.Reflection.AssemblyTitleAttribute[] array = (System.Reflection.AssemblyTitleAttribute[])System.Reflection.Assembly.GetEntryAssembly().GetCustomAttributes(typeof(System.Reflection.AssemblyTitleAttribute), false);
            if (array.Length > 0) {
                appTitle = array[0].Title;
                this.Text = appTitle;
            }

            string versionName = "";
            int uwClassroomPresenterDllRev = System.Reflection.Assembly.GetAssembly(typeof(UW.ClassroomPresenter.Model.PresenterModel)).GetName().Version.Build;

            string[] versionSplit = Application.ProductVersion.Split('.');

            string version = versionSplit[0] + "." + versionSplit[1] + " (Exe Revision " + versionSplit[2] +
                "; UW.ClassroomPresenter Revision " + uwClassroomPresenterDllRev.ToString() +")";

#if RTP_BUILD
            string rtpVersion = "RTP version " +
                System.Reflection.Assembly.GetAssembly(typeof(MSR.LST.Net.Rtp.RtpSender)).GetName().Version;

            this.aboutLinkLabel.Text = appTitle + "\n" + versionName + "\n" +  version + "\n" +
                rtpVersion + "\n" + buildDate + "\n" + "For more information, please visit" + "\n";
#else
            //version += "\n2008 Premier Award for Excellence in Engineering Education Courseware"; // Temporary - remove after Premier Award!

            this.aboutLinkLabel.Text = appTitle + "\n" + versionName + "\n" + version + "\n" +
                 "\n" + Strings.MoreInfo + "\n";
 #endif
            int textLengthWOURL = this.aboutLinkLabel.Text.Length;

            string URL = "http://classroompresenter.cs.washington.edu/";

            this.aboutLinkLabel.Text += URL;
            this.aboutLinkLabel.Links.Add(textLengthWOURL, URL.Length, URL);
        }


        private void okButton_Click(object sender, System.EventArgs e) {
            this.Close();
        }

        private void aboutLinkLabel_LinkClicked(object sender, System.Windows.Forms.LinkLabelLinkClickedEventArgs e) {
            System.Diagnostics.Process.Start(e.Link.LinkData.ToString());
        }

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        protected override void Dispose( bool disposing ) {
            if( disposing ) {
                if(components != null) {
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
            this.okButton = new System.Windows.Forms.Button();
            this.aboutLinkLabel = new System.Windows.Forms.LinkLabel();
            this.SuspendLayout();
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            //
            // okButton
            //
            this.okButton.FlatStyle = FlatStyle.System;
            this.okButton.Font = Model.Viewer.ViewerStateModel.StringFont;
            this.okButton.Location = new System.Drawing.Point(120, 136);
            this.okButton.Name = "okButton";
            this.okButton.TabIndex = 0;
            this.okButton.Text = "OK";
            this.okButton.Click += new System.EventHandler(this.okButton_Click);
            //
            // aboutLinkLabel
            //
            this.aboutLinkLabel.Font = Model.Viewer.ViewerStateModel.StringFont;
            this.aboutLinkLabel.Location = new System.Drawing.Point(16, 16);
            this.aboutLinkLabel.Name = "aboutLinkLabel";
            this.aboutLinkLabel.Size = new System.Drawing.Size(296, 120);
            this.aboutLinkLabel.TabIndex = 1;
            this.aboutLinkLabel.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.aboutLinkLabel_LinkClicked);
            //
            // AboutForm
            //
            this.AcceptButton = this.okButton;
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.ClientSize = new System.Drawing.Size(320, 170);
            this.Controls.Add(this.aboutLinkLabel);
            this.Controls.Add(this.okButton);
            this.Font = Model.Viewer.ViewerStateModel.FormFont;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "AboutForm";
            this.ShowInTaskbar = false;
            this.Text = "AboutForm";
            this.ResumeLayout(false);
        }
        #endregion
    }
}
