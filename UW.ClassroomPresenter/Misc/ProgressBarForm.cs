// $Id: ProgressBarForm.cs 1824 2009-03-10 23:47:34Z lining $

using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;

namespace UW.ClassroomPresenter.Misc {
    public class ProgressBarForm : Form {
        private readonly ProgressBar m_ProgressBar;
        private readonly Label m_PercentLabel;
        private readonly BackgroundWorker m_BackgroundWorker;

        public ProgressBarForm(string title) {
            this.Text = title;

            this.m_ProgressBar = new ProgressBar();
            this.m_PercentLabel = new Label();

            this.m_BackgroundWorker = new BackgroundWorker();
            this.m_BackgroundWorker.WorkerReportsProgress = true;
            this.m_BackgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(this.HandleBackgroundWorkerCompleted);
            this.m_BackgroundWorker.ProgressChanged += new ProgressChangedEventHandler(this.HandleBackgroundWorkerProgressChanged);
            
            this.SuspendLayout();

            this.m_ProgressBar.Location = new Point(8, 8);
            this.m_ProgressBar.Size = new Size(320, 23);

            this.m_PercentLabel.FlatStyle = FlatStyle.System;
            this.m_PercentLabel.Font = Model.Viewer.ViewerStateModel.StringFont2;
            this.m_PercentLabel.Location = new Point(328, 8);
            this.m_PercentLabel.Size = new Size(140, 24);
            this.m_PercentLabel.Text = "0%";
            this.m_PercentLabel.TextAlign = ContentAlignment.MiddleLeft;

            this.AutoScaleBaseSize = new Size(5, 13);
            this.ClientSize = new Size(474, 38);
            this.ControlBox = false;
            this.Font = Model.Viewer.ViewerStateModel.FormFont;
            this.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.TopMost = true;

            this.Controls.Add(this.m_PercentLabel);
            this.Controls.Add(this.m_ProgressBar);

            this.ResumeLayout(false);
        }

        #region Events

        private void HandleBackgroundWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            this.Close();
        }

        private void  HandleBackgroundWorkerProgressChanged(object sender, ProgressChangedEventArgs e) {
 	        this.m_ProgressBar.Value = e.ProgressPercentage;
            this.m_PercentLabel.Text = e.UserState as String;
        }

        public event DoWorkEventHandler DoWork {
            add { this.m_BackgroundWorker.DoWork += value; }
            remove { this.m_BackgroundWorker.DoWork -= value; }
        }

        #endregion

        #region Methods

        public void StartWork(object argument) {
            this.m_BackgroundWorker.RunWorkerAsync(argument);
        }

        public void UpdateProgress(int min, int max, int index) {
            this.m_ProgressBar.Maximum = max;
            this.m_ProgressBar.Minimum = min;
            this.m_ProgressBar.Value = index;
            this.m_PercentLabel.Text = "  " + (index * 100 / max)+"%";
        }
        #endregion

        #region IDisposable Members

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        protected override void Dispose(bool disposing) {
            try {
                if(disposing) {
                    this.m_BackgroundWorker.Dispose();
                    this.m_PercentLabel.Dispose();
                    this.m_ProgressBar.Dispose();
                }
            } finally {
                base.Dispose(disposing);
            }
        }

        #endregion
    }
}
