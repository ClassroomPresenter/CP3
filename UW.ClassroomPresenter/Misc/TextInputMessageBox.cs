// $Id: TextInputMessageBox.cs 1598 2008-04-30 00:49:50Z lining $

using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;

namespace UW.ClassroomPresenter.Misc {
    /// <summary>
    /// TextInputMessageBox is a standard dialog box that requests text from
    /// users.
    /// </summary>
    public class TextInputMessageBox : System.Windows.Forms.Form {
        private System.Windows.Forms.TextBox inputTextBox;
        private System.Windows.Forms.Button okButton;
        private System.Windows.Forms.Button cancelButton;
        private System.Windows.Forms.Label promptLabel;
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.Container components = null;

        private DialogResult result = DialogResult.Cancel;
        public DialogResult Result {
            get { return this.result; }
        }

        public string InputText {
            get { return this.inputTextBox.Text; }
        }

        /// <summary>
        /// Constructor for TextInputMessageBox
        /// </summary>
        /// <param name="prompt">Text that details what information is requested from the user</param>
        /// <param name="title">Text for the titlebar of the dialog</param>
        /// <param name="initialInput">Text that is prepopulated in the input box</param>
        public TextInputMessageBox(string prompt, string title, string initialInput){
            InitializeComponent();
            this.Text = title;
            this.promptLabel.Text = prompt + ":";
            this.inputTextBox.Text = initialInput;
        }

        private void onOKButtonClick(object sender, System.EventArgs e) {
            this.result = DialogResult.OK;
            this.Close();
        }

        private void onCancelButtonClick(object sender, System.EventArgs e) {
            this.Close();
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
            this.promptLabel = new System.Windows.Forms.Label();
            this.inputTextBox = new System.Windows.Forms.TextBox();
            this.okButton = new System.Windows.Forms.Button();
            this.cancelButton = new System.Windows.Forms.Button();
            this.SuspendLayout();
            //
            // promptLabel
            //
            this.promptLabel.Location = new System.Drawing.Point(8, 16);
            this.promptLabel.Name = "promptLabel";
            this.promptLabel.Size = new System.Drawing.Size(100, 24);
            this.promptLabel.TabIndex = 0;
            this.promptLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // inputTextBox
            //
            this.inputTextBox.Location = new System.Drawing.Point(112, 16);
            this.inputTextBox.Name = "inputTextBox";
            this.inputTextBox.Size = new System.Drawing.Size(192, 20);
            this.inputTextBox.TabIndex = 1;
            this.inputTextBox.Text = "";
            //
            // okButton
            //
            this.okButton.Location = new System.Drawing.Point(144, 48);
            this.okButton.Name = "okButton";
            this.okButton.TabIndex = 2;
            this.okButton.Text = Strings.OK;
            this.okButton.Click += new System.EventHandler(this.onOKButtonClick);
            //
            // cancelButton
            //
            this.cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancelButton.Location = new System.Drawing.Point(232, 48);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.TabIndex = 3;
            this.cancelButton.Text = Strings.Cancel;
            this.cancelButton.Click += new System.EventHandler(this.onCancelButtonClick);
            //
            // TextInputMessageBox
            //
            this.AcceptButton = this.okButton;
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.CancelButton = this.cancelButton;
            this.ClientSize = new System.Drawing.Size(314, 80);
            this.Controls.Add(this.cancelButton);
            this.Controls.Add(this.okButton);
            this.Controls.Add(this.inputTextBox);
            this.Controls.Add(this.promptLabel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "TextInputMessageBox";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.TopMost = true;
            this.ResumeLayout(false);
        }
        #endregion
    }
}
