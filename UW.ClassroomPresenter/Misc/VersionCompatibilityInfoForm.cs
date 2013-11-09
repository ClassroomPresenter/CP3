using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Network.Messages.Network;

namespace UW.ClassroomPresenter.Misc {
    public partial class VersionCompatibilityInfoForm : Form {
        public VersionCompatibilityInfoForm() {
            InitializeComponent();
            this.Text = Strings.VersionCompatibilityInformation;
            this.labelMoreInfo.Text = Strings.ForMoreInformation;
            this.button1.Text = Strings.Close;
        }

        /// <summary>
        /// Load the remote node compatibility information.
        /// </summary>
        private void LoadDataGridView() {
            //This seems to be a simple approach:  Put the data into
            //a DataTable, then bind the DataTable to the DataGridView.
            DataTable dt = new DataTable();
            DataColumn dc;
            DataRow dr;

            dc = new DataColumn();
            dc.DataType = Type.GetType("System.String");
            dc.ColumnName = Strings.RemoteNode;
            dt.Columns.Add(dc);

            dc = new DataColumn();
            dc.DataType = Type.GetType("System.String");
            dc.ColumnName = Strings.Version;
            dt.Columns.Add(dc);

            dc = new DataColumn();
            dc.DataType = Type.GetType("System.String");
            dc.ColumnName = Strings.Compatibility;
            dt.Columns.Add(dc);

            dc = new DataColumn();
            dc.DataType = Type.GetType("System.String");
            dc.ColumnName = Strings.Notes;
            dt.Columns.Add(dc);

            using (Synchronizer.Lock(PresenterModel.TheInstance.VersionExchange.SyncRoot)) {
                foreach (VersionExchange ve in PresenterModel.TheInstance.VersionExchange.VersionExchanges) {
                    if (!ve.DisplayVersionInfo) {
                        continue;
                    }

                    dr = dt.NewRow();
                    if ((ve.RemoteHumanName == null) || (ve.RemoteHumanName == "")) {
                        dr[Strings.RemoteNode] = Strings.UnknownName;             
                    }
                    else { 
                        dr[Strings.RemoteNode] = ve.RemoteHumanName;                  
                    }
                    if (ve.RemoteVersion == null) {
                        dr[Strings.Version] = Strings.UnknownVersion;
                        dr[Strings.Notes] = Strings.VersionCompatibilityNoResponse;
                    }
                    else {
                        dr[Strings.Version] = ve.RemoteVersion.ToString();
                    }

                    dr[Strings.Compatibility] = Enum.GetName(typeof(VersionCompatibility),ve.RemoteCompatibility);

                    if ((ve.WarningMessage != null ) && (ve.WarningMessage != "")) { 
                        dr[Strings.Notes] = ve.WarningMessage;                       
                    }

                    dt.Rows.Add(dr);
                }
            }

            this.dataGridView1.DataSource = dt;
            this.dataGridView1.Columns[Strings.Notes].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        }

        private void button1_Click(object sender, EventArgs e) {
            this.Close();
        }

        private void VersionCompatibilityInfoForm_Load(object sender, EventArgs e) {
            this.linkLabelURL.Text = VersionExchangeModel.CompatiblityInfoURL;
            this.linkLabelURL.Links.Add(0, VersionExchangeModel.CompatiblityInfoURL.Length,VersionExchangeModel.CompatiblityInfoURL);
            this.labelThisCPVersion.Text = Strings.ThisClassroomPresenterVersion + VersionExchangeModel.LocalVersion.ToString();
            LoadDataGridView();
        }

        private void linkLabelURL_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            System.Diagnostics.Process.Start(e.Link.LinkData.ToString());
        }
    }
}