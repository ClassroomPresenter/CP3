namespace UW.ClassroomPresenter.Misc {
    partial class VersionCompatibilityInfoForm {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            this.button1 = new System.Windows.Forms.Button();
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.labelThisCPVersion = new System.Windows.Forms.Label();
            this.labelMoreInfo = new System.Windows.Forms.Label();
            this.linkLabelURL = new System.Windows.Forms.LinkLabel();
            this.panelTop = new System.Windows.Forms.Panel();
            this.panel1 = new System.Windows.Forms.Panel();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            this.panelTop.SuspendLayout();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // button1
            // 
            this.button1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.button1.Location = new System.Drawing.Point(379, 51);
            this.button1.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.button1.Font = Model.Viewer.ViewerStateModel.StringFont;
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 23);
            this.button1.TabIndex = 0;
            this.button1.Text = "Close";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // dataGridView1
            // 
            this.dataGridView1.AllowUserToAddRows = false;
            this.dataGridView1.AllowUserToDeleteRows = false;
            this.dataGridView1.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.AllCells;
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView1.Font = Model.Viewer.ViewerStateModel.StringFont;
            this.dataGridView1.Location = new System.Drawing.Point(20, 67);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.ReadOnly = true;
            this.dataGridView1.RowHeadersVisible = false;
            this.dataGridView1.Size = new System.Drawing.Size(456, 241);
            this.dataGridView1.TabIndex = 6;
            // 
            // labelThisCPVersion
            // 
            this.labelThisCPVersion.AutoSize = true;
            this.labelThisCPVersion.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.labelThisCPVersion.Font = Model.Viewer.ViewerStateModel.StringFont2;
            this.labelThisCPVersion.Location = new System.Drawing.Point(0, 9);
            this.labelThisCPVersion.Name = "labelThisCPVersion";
            this.labelThisCPVersion.Size = new System.Drawing.Size(35, 13);
            this.labelThisCPVersion.TabIndex = 2;
            this.labelThisCPVersion.Text = "label1";
            // 
            // labelMoreInfo
            // 
            this.labelMoreInfo.AutoSize = true;
            this.labelMoreInfo.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.labelMoreInfo.Font = Model.Viewer.ViewerStateModel.StringFont;
            this.labelMoreInfo.Location = new System.Drawing.Point(0, 17);
            this.labelMoreInfo.Name = "labelMoreInfo";
            this.labelMoreInfo.Size = new System.Drawing.Size(105, 13);
            this.labelMoreInfo.TabIndex = 1;
            this.labelMoreInfo.Text = "For more information:";
            // 
            // linkLabelURL
            // 
            this.linkLabelURL.AutoSize = true;
            this.linkLabelURL.Font = Model.Viewer.ViewerStateModel.StringFont;
            this.linkLabelURL.Location = new System.Drawing.Point(this.labelMoreInfo.Right + 16, 17);
            this.linkLabelURL.Name = "linkLabelURL";
            this.linkLabelURL.Size = new System.Drawing.Size(55, 13);
            this.linkLabelURL.TabIndex = 2;
            this.linkLabelURL.TabStop = true;
            this.linkLabelURL.Text = "linkLabel1";
            this.linkLabelURL.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabelURL_LinkClicked);
            // 
            // panelTop
            // 
            this.panelTop.Controls.Add(this.labelThisCPVersion);
            this.panelTop.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelTop.Location = new System.Drawing.Point(20, 20);
            this.panelTop.Name = "panelTop";
            this.panelTop.Size = new System.Drawing.Size(456, 47);
            this.panelTop.TabIndex = 4;
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.linkLabelURL);
            this.panel1.Controls.Add(this.labelMoreInfo);
            this.panel1.Controls.Add(this.button1);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panel1.Location = new System.Drawing.Point(20, 308);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(456, 76);
            this.panel1.TabIndex = 5;
            // 
            // VersionCompatibilityInfoForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(496, 404);
            this.Controls.Add(this.dataGridView1);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.panelTop);
            this.Font = Model.Viewer.ViewerStateModel.FormFont;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "VersionCompatibilityInfoForm";
            this.Padding = new System.Windows.Forms.Padding(20);
            this.Text = "Version Compatibility Information";
            this.Load += new System.EventHandler(this.VersionCompatibilityInfoForm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.panelTop.ResumeLayout(false);
            this.panelTop.PerformLayout();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.Label labelThisCPVersion;
        private System.Windows.Forms.Label labelMoreInfo;
        private System.Windows.Forms.LinkLabel linkLabelURL;
        private System.Windows.Forms.Panel panelTop;
        private System.Windows.Forms.Panel panel1;
    }
}