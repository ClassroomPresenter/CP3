using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Data;

namespace UW.ClassroomPresenter.Misc
{
    public partial class LicenseForm : Form
    {
        public LicenseForm()
        {
            InitializeComponent();
            try {
                string[] lines = File.ReadAllLines("LICENSE.txt");
                this.richTextBox1.Text = "";
                foreach (string l in lines) {
                    this.richTextBox1.Text += l + "\r\n";
                }
            }
            catch {
                this.richTextBox1.Text = "LICENSE.txt is not found.";
                return;
            }
        }

        private void OKbutton_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}