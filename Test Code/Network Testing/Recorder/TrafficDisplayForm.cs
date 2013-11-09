// $Id: TrafficDisplayForm.cs 774 2005-09-21 20:22:33Z pediddle $

using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace UW.ClassroomPresenter.Test.Network.Recorder {
    /// <summary>
    /// Summary description for TrafficDisplayForm.
    /// </summary>
    public class TrafficDisplayForm : System.Windows.Forms.Form {
        private System.Windows.Forms.RichTextBox outputTextBox;
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.Container components = null;
        private ArrayList al = new ArrayList(200);
        private long count = 0;

        public TrafficDisplayForm() {
            //
            // Required for Windows Form Designer support
            //
            InitializeComponent();

            //
            // TODO: Add any constructor code after InitializeComponent call
            //
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

        public void AppendText(string text) {
            this.al.Add(this.count.ToString() + ". " + text);
            if (this.al.Count >= this.al.Capacity) {
                this.al.RemoveAt(0);
            }
            this.updateTextBox();
            this.count++;
        }

        private void updateTextBox() {
            string[] toUpdate = (string[])this.al.ToArray(typeof(string));
            this.SuspendLayout();
            this.outputTextBox.Lines = toUpdate;
            //Make it scroll to the bottom
            uint WM_VSCROLL = 0x0115;
            uint SB_BOTTOM = 7;
            TrafficDisplayForm.SendMessage(this.outputTextBox.Handle, WM_VSCROLL, SB_BOTTOM, 0);
            this.ResumeLayout();
        }

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hwnd, uint wMsg, uint wParam, int lParam);

        #region Windows Form Designer generated code
        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            this.outputTextBox = new System.Windows.Forms.RichTextBox();
            this.SuspendLayout();
            // 
            // outputTextBox
            // 
            this.outputTextBox.Location = new System.Drawing.Point(8, 8);
            this.outputTextBox.Name = "outputTextBox";
            this.outputTextBox.ReadOnly = true;
            this.outputTextBox.Size = new System.Drawing.Size(552, 448);
            this.outputTextBox.TabIndex = 0;
            this.outputTextBox.Text = "";
            // 
            // TrafficDisplayForm
            // 
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.ClientSize = new System.Drawing.Size(570, 464);
            this.ControlBox = false;
            this.Controls.Add(this.outputTextBox);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "TrafficDisplayForm";
            this.ShowInTaskbar = false;
            this.Text = "Network Traffic";
            this.ResumeLayout(false);

        }
        #endregion
    }
}
