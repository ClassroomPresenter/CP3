// $Id: PlaybackForm.cs 774 2005-09-21 20:22:33Z pediddle $

using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using System.Data;
using System.Net;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UW.ClassroomPresenter.Test.Network.Common;
using System.Threading;

namespace UW.ClassroomPresenter.Test.Network.Playback {
    /// <summary>
    /// Summary description for Form1.
    /// </summary>
    public class PlaybackForm : System.Windows.Forms.Form {
        private System.Windows.Forms.Button moveUpButton;
        private System.Windows.Forms.Button moveDownButton;
        private System.Windows.Forms.Button deleteButton;
        private System.Windows.Forms.ComboBox classroomDropDown;
        private System.Windows.Forms.Button playButton;
        private System.Windows.Forms.Button stepButton;
        private System.Windows.Forms.Button stopButton;
        private System.Windows.Forms.Button rewindbutton;
        private System.Windows.Forms.MenuItem fileMenuItem;
        private System.Windows.Forms.MenuItem modeMenuItem;
        private System.Windows.Forms.MenuItem helpMenuItem;
        private System.Windows.Forms.MenuItem exitMenuItem;
        private System.Windows.Forms.MenuItem fileSeparatorMenuItem;
        private System.Windows.Forms.MenuItem chunksMenuItem;
        private System.Windows.Forms.MenuItem messagesMenuItem;
        private System.Windows.Forms.MenuItem aboutMenuItem;
        private System.Windows.Forms.MenuItem saveAsMenuItem;
        private System.Windows.Forms.ListBox networkEventsListBox;
        private System.ComponentModel.IContainer components;
        private System.Windows.Forms.MenuItem importMenuItem;
        private System.Windows.Forms.MenuItem fileMenuSeparator2;
        private System.Windows.Forms.MenuItem openMenuItem;
        private System.Windows.Forms.Button infoButton;
        private System.Windows.Forms.Timer timer;
        private System.Windows.Forms.MainMenu mainMenu;
        private System.Windows.Forms.Label statusLabel;

        private NetworkPlayback.NetworkPlaybackModeEnum m_Mode;
        private NetworkPlayback m_Playback;
        private RTPSendConnection m_Connection;
        private System.Windows.Forms.Button playAllButton;
        private NetworkEventInfoForm m_InfoForm;

        public PlaybackForm() {
            //
            // Required for Windows Form Designer support
            //
            InitializeComponent();

            //
            // TODO: Add any constructor code after InitializeComponent call
            //
            this.classroomDropDown.SelectedIndex = 0;
            this.m_Mode = NetworkPlayback.NetworkPlaybackModeEnum.Chunk;
            this.timer.Tick += new EventHandler(this.timer_Tick);
            this.m_InfoForm = new NetworkEventInfoForm();
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
            this.components = new System.ComponentModel.Container();
            this.classroomDropDown = new System.Windows.Forms.ComboBox();
            this.networkEventsListBox = new System.Windows.Forms.ListBox();
            this.moveUpButton = new System.Windows.Forms.Button();
            this.moveDownButton = new System.Windows.Forms.Button();
            this.deleteButton = new System.Windows.Forms.Button();
            this.playButton = new System.Windows.Forms.Button();
            this.stepButton = new System.Windows.Forms.Button();
            this.stopButton = new System.Windows.Forms.Button();
            this.rewindbutton = new System.Windows.Forms.Button();
            this.mainMenu = new System.Windows.Forms.MainMenu();
            this.fileMenuItem = new System.Windows.Forms.MenuItem();
            this.openMenuItem = new System.Windows.Forms.MenuItem();
            this.saveAsMenuItem = new System.Windows.Forms.MenuItem();
            this.fileSeparatorMenuItem = new System.Windows.Forms.MenuItem();
            this.importMenuItem = new System.Windows.Forms.MenuItem();
            this.fileMenuSeparator2 = new System.Windows.Forms.MenuItem();
            this.exitMenuItem = new System.Windows.Forms.MenuItem();
            this.modeMenuItem = new System.Windows.Forms.MenuItem();
            this.chunksMenuItem = new System.Windows.Forms.MenuItem();
            this.messagesMenuItem = new System.Windows.Forms.MenuItem();
            this.helpMenuItem = new System.Windows.Forms.MenuItem();
            this.aboutMenuItem = new System.Windows.Forms.MenuItem();
            this.statusLabel = new System.Windows.Forms.Label();
            this.timer = new System.Windows.Forms.Timer(this.components);
            this.infoButton = new System.Windows.Forms.Button();
            this.playAllButton = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // classroomDropDown
            // 
            this.classroomDropDown.Items.AddRange(new object[] {
                                                                   "Not Connected",
                                                                   "Classroom 1 (234.3.0.1)",
                                                                   "Classroom 2 (234.3.0.2)",
                                                                   "Classroom 3 (234.3.0.3)",
                                                                   "Classroom 4 (234.3.0.4)",
                                                                   "Classroom 5 (234.3.0.5)"});
            this.classroomDropDown.Location = new System.Drawing.Point(16, 24);
            this.classroomDropDown.Name = "classroomDropDown";
            this.classroomDropDown.Size = new System.Drawing.Size(200, 21);
            this.classroomDropDown.TabIndex = 0;
            this.classroomDropDown.SelectedIndexChanged += new System.EventHandler(this.onClassroomDropDownSelectedIndexChanged);
            // 
            // networkEventsListBox
            // 
            this.networkEventsListBox.Location = new System.Drawing.Point(16, 64);
            this.networkEventsListBox.Name = "networkEventsListBox";
            this.networkEventsListBox.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.networkEventsListBox.Size = new System.Drawing.Size(456, 173);
            this.networkEventsListBox.TabIndex = 1;
            this.networkEventsListBox.SelectedIndexChanged += new System.EventHandler(this.onNetworkEventsListBoxSelectedIndexChanged);
            // 
            // moveUpButton
            // 
            this.moveUpButton.Enabled = false;
            this.moveUpButton.Location = new System.Drawing.Point(16, 256);
            this.moveUpButton.Name = "moveUpButton";
            this.moveUpButton.TabIndex = 2;
            this.moveUpButton.Text = "Move Up";
            this.moveUpButton.Click += new System.EventHandler(this.onMoveUpButtonClick);
            // 
            // moveDownButton
            // 
            this.moveDownButton.Enabled = false;
            this.moveDownButton.Location = new System.Drawing.Point(104, 256);
            this.moveDownButton.Name = "moveDownButton";
            this.moveDownButton.TabIndex = 3;
            this.moveDownButton.Text = "Move Down";
            this.moveDownButton.Click += new System.EventHandler(this.onMoveDownButtonClick);
            // 
            // deleteButton
            // 
            this.deleteButton.Enabled = false;
            this.deleteButton.Location = new System.Drawing.Point(192, 256);
            this.deleteButton.Name = "deleteButton";
            this.deleteButton.TabIndex = 4;
            this.deleteButton.Text = "Delete";
            this.deleteButton.Click += new System.EventHandler(this.onDeleteButtonClick);
            // 
            // playButton
            // 
            this.playButton.Enabled = false;
            this.playButton.Location = new System.Drawing.Point(16, 296);
            this.playButton.Name = "playButton";
            this.playButton.TabIndex = 5;
            this.playButton.Text = "Play";
            this.playButton.Click += new System.EventHandler(this.onPlayButtonClick);
            // 
            // stepButton
            // 
            this.stepButton.Enabled = false;
            this.stepButton.Location = new System.Drawing.Point(104, 296);
            this.stepButton.Name = "stepButton";
            this.stepButton.TabIndex = 6;
            this.stepButton.Text = "Step";
            this.stepButton.Click += new System.EventHandler(this.onStepButtonClick);
            // 
            // stopButton
            // 
            this.stopButton.Enabled = false;
            this.stopButton.Location = new System.Drawing.Point(280, 296);
            this.stopButton.Name = "stopButton";
            this.stopButton.TabIndex = 7;
            this.stopButton.Text = "Stop";
            this.stopButton.Click += new System.EventHandler(this.onStopButtonClick);
            // 
            // rewindbutton
            // 
            this.rewindbutton.Enabled = false;
            this.rewindbutton.Location = new System.Drawing.Point(192, 296);
            this.rewindbutton.Name = "rewindbutton";
            this.rewindbutton.TabIndex = 8;
            this.rewindbutton.Text = "Rewind";
            this.rewindbutton.Click += new System.EventHandler(this.onRewindbuttonClick);
            // 
            // mainMenu
            // 
            this.mainMenu.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
                                                                                     this.fileMenuItem,
                                                                                     this.modeMenuItem,
                                                                                     this.helpMenuItem});
            // 
            // fileMenuItem
            // 
            this.fileMenuItem.Index = 0;
            this.fileMenuItem.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
                                                                                         this.openMenuItem,
                                                                                         this.saveAsMenuItem,
                                                                                         this.fileSeparatorMenuItem,
                                                                                         this.importMenuItem,
                                                                                         this.fileMenuSeparator2,
                                                                                         this.exitMenuItem});
            this.fileMenuItem.Text = "File";
            // 
            // openMenuItem
            // 
            this.openMenuItem.Index = 0;
            this.openMenuItem.Text = "&Open...";
            this.openMenuItem.Click += new System.EventHandler(this.onOpenMenuItemClick);
            // 
            // saveAsMenuItem
            // 
            this.saveAsMenuItem.Index = 1;
            this.saveAsMenuItem.Text = "Save &as...";
            this.saveAsMenuItem.Click += new System.EventHandler(this.onSaveAsMenuItemClick);
            // 
            // fileSeparatorMenuItem
            // 
            this.fileSeparatorMenuItem.Index = 2;
            this.fileSeparatorMenuItem.Text = "-";
            // 
            // importMenuItem
            // 
            this.importMenuItem.Index = 3;
            this.importMenuItem.Text = "&Import...";
            this.importMenuItem.Click += new System.EventHandler(this.onImportMenuItemClick);
            // 
            // fileMenuSeparator2
            // 
            this.fileMenuSeparator2.Index = 4;
            this.fileMenuSeparator2.Text = "-";
            // 
            // exitMenuItem
            // 
            this.exitMenuItem.Index = 5;
            this.exitMenuItem.Text = "E&xit";
            this.exitMenuItem.Click += new System.EventHandler(this.onExitMenuItemClick);
            // 
            // modeMenuItem
            // 
            this.modeMenuItem.Index = 1;
            this.modeMenuItem.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
                                                                                         this.chunksMenuItem,
                                                                                         this.messagesMenuItem});
            this.modeMenuItem.Text = "Mode";
            // 
            // chunksMenuItem
            // 
            this.chunksMenuItem.Checked = true;
            this.chunksMenuItem.Index = 0;
            this.chunksMenuItem.Text = "&Chunks";
            this.chunksMenuItem.Click += new System.EventHandler(this.onChunksMenuItemClick);
            // 
            // messagesMenuItem
            // 
            this.messagesMenuItem.Index = 1;
            this.messagesMenuItem.Text = "&Messages";
            this.messagesMenuItem.Click += new System.EventHandler(this.onMessagesMenuItemClick);
            // 
            // helpMenuItem
            // 
            this.helpMenuItem.Index = 2;
            this.helpMenuItem.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
                                                                                         this.aboutMenuItem});
            this.helpMenuItem.Text = "Help";
            // 
            // aboutMenuItem
            // 
            this.aboutMenuItem.Index = 0;
            this.aboutMenuItem.Text = "&About...";
            this.aboutMenuItem.Click += new System.EventHandler(this.onAboutMenuItemClick);
            // 
            // statusLabel
            // 
            this.statusLabel.Location = new System.Drawing.Point(232, 24);
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Size = new System.Drawing.Size(240, 23);
            this.statusLabel.TabIndex = 9;
            this.statusLabel.Text = "Status: Disconnected";
            this.statusLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // infoButton
            // 
            this.infoButton.Location = new System.Drawing.Point(280, 256);
            this.infoButton.Name = "infoButton";
            this.infoButton.TabIndex = 10;
            this.infoButton.Text = "Show Info";
            this.infoButton.Click += new System.EventHandler(this.onInfoButtonClick);
            // 
            // playAllButton
            // 
            this.playAllButton.Enabled = false;
            this.playAllButton.Location = new System.Drawing.Point(368, 296);
            this.playAllButton.Name = "playAllButton";
            this.playAllButton.TabIndex = 11;
            this.playAllButton.Text = "Play All";
            this.playAllButton.Click += new System.EventHandler(this.onPlayAllButtonClick);
            // 
            // PlaybackForm
            // 
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.ClientSize = new System.Drawing.Size(496, 342);
            this.Controls.Add(this.playAllButton);
            this.Controls.Add(this.infoButton);
            this.Controls.Add(this.statusLabel);
            this.Controls.Add(this.rewindbutton);
            this.Controls.Add(this.stopButton);
            this.Controls.Add(this.stepButton);
            this.Controls.Add(this.playButton);
            this.Controls.Add(this.deleteButton);
            this.Controls.Add(this.moveDownButton);
            this.Controls.Add(this.moveUpButton);
            this.Controls.Add(this.networkEventsListBox);
            this.Controls.Add(this.classroomDropDown);
            this.Menu = this.mainMenu;
            this.Name = "PlaybackForm";
            this.Text = "Classroom Playback";
            this.ResumeLayout(false);

        }
        #endregion

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main() {
            Application.Run(new PlaybackForm());
        }

        #region Event Handlers

        #region File Menu

        private void onImportMenuItemClick(object sender, System.EventArgs e) {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "CP3 Network Capture (*.cnc)|*.cnc";
            if (ofd.ShowDialog() == DialogResult.OK) {
                this.m_Playback = new NetworkPlayback(ofd.FileName);
                this.updateModeMenu();
                this.updateUI(true);
            }
        }

        private void onOpenMenuItemClick(object sender, System.EventArgs e) {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Classroom Playback Files (*.cpf)|*.cpf";
            if (ofd.ShowDialog() == DialogResult.OK) {
                BinaryFormatter bf = new BinaryFormatter();
                FileStream fs = new FileStream(ofd.FileName, FileMode.Open, FileAccess.Read);
                try {
                    this.m_Playback = (NetworkPlayback)bf.Deserialize(fs);
                } catch (Exception) {
                    MessageBox.Show("Error: File format is outdated, corrupted or invalid", "Classroom Playback", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
                    return;
                } finally {
                    fs.Flush();
                    fs.Close();
                }
                this.updateModeMenu();
                this.updateUI(true);
            }
        }

        private void onSaveAsMenuItemClick(object sender, System.EventArgs e) {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "Classroom Playback Files (*.cpf)|*.cpf";
            if (sfd.ShowDialog() == DialogResult.OK) {
                BinaryFormatter bf = new BinaryFormatter();
                FileStream fs = new FileStream(sfd.FileName, FileMode.Create, FileAccess.ReadWrite);
                try {
                    bf.Serialize(fs, this.m_Playback);
                } catch (Exception) {
                    MessageBox.Show("Error: A serializtion error has occured.", "Classroom Playback", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
                } finally {
                    fs.Flush();
                    fs.Close();
                }
            }
        }

        private void onExitMenuItemClick(object sender, System.EventArgs e) {
            this.Close();
        }

        private void updateUI(bool state) {
            //this.classroomDropDown.Enabled = state;
            this.moveDownButton.Enabled = state;
            this.moveUpButton.Enabled = state;
            this.deleteButton.Enabled = state;
            this.playButton.Enabled = state;
            this.stopButton.Enabled = state;
            this.rewindbutton.Enabled = state;
            this.stepButton.Enabled = state;
            this.infoButton.Enabled = state;
            this.playAllButton.Enabled = state;
        }

        #endregion

        #region Mode Menu

        private void onChunksMenuItemClick(object sender, System.EventArgs e) {
            this.m_Mode = NetworkPlayback.NetworkPlaybackModeEnum.Chunk;
            this.updateModeMenu();
        }

        private void onMessagesMenuItemClick(object sender, System.EventArgs e) {
            this.m_Mode = NetworkPlayback.NetworkPlaybackModeEnum.Message;
            this.updateModeMenu();
        }

        private void updateModeMenu() {
            if (this.m_Playback != null) {
                this.networkEventsListBox.Items.Clear();
                this.networkEventsListBox.Items.AddRange(this.m_Playback.GetEvents(this.m_Mode));
            }
            if (this.m_Mode == NetworkPlayback.NetworkPlaybackModeEnum.Chunk) {
                this.chunksMenuItem.Checked = true;
                this.messagesMenuItem.Checked = false;
            } else {
                this.chunksMenuItem.Checked = false;
                this.messagesMenuItem.Checked = true;
            }
            //Select the first item
            if (this.networkEventsListBox.Items.Count > 0) {
                this.networkEventsListBox.SetSelected(0, true);
            }
        }

        #endregion

        #region Help Menu

        private void onAboutMenuItemClick(object sender, System.EventArgs e) {
            Misc.AboutForm af = new UW.ClassroomPresenter.Misc.AboutForm();
            af.ShowDialog();
        }

        #endregion

        #region Buttons

        private void onMoveUpButtonClick(object sender, System.EventArgs e) {
            if (this.networkEventsListBox.SelectedIndices.Count > 0) {
                ArrayList al = this.arrayListGenerator();
                this.m_Playback.MoveUp(al, this.m_Mode);
                this.updateModeMenu();
                //Reselect the moved items
                for (int i = 0; i < al.Count; i++) {
                    int index = (int)al[i] - 1;
                    if (index < 0) {
                        index = 0;
                    }
                    this.networkEventsListBox.SetSelected(index, true);
                }
            }
        }

        private void onMoveDownButtonClick(object sender, System.EventArgs e) {
            if (this.networkEventsListBox.SelectedIndices.Count > 0) {
                ArrayList al = this.arrayListGenerator();
                this.m_Playback.MoveDown(al, this.m_Mode);
                this.updateModeMenu();
                //Reselect the moved items
                for (int i = 0; i < al.Count; i++) {
                    int index = (int)al[i] + 1;
                    if (index >= this.networkEventsListBox.Items.Count) {
                        index = this.networkEventsListBox.Items.Count - 1;
                    }
                    this.networkEventsListBox.SetSelected(index, true);
                }
            }
        }

        private void onDeleteButtonClick(object sender, System.EventArgs e) {
            if (this.networkEventsListBox.SelectedIndices.Count > 0) {
                ArrayList al = this.arrayListGenerator();
                this.m_Playback.Delete(al, this.m_Mode);
                this.updateModeMenu();
            }
        }

        private void onPlayButtonClick(object sender, System.EventArgs e) {
            this.timer.Enabled = true;
        }

        private void onStepButtonClick(object sender, System.EventArgs e) {
            //Get the current selected position (first one you find if multiple are selected)
            if (this.m_Connection != null) {
                if (this.networkEventsListBox.SelectedIndices.Count > 0) {
                    int currpos = this.networkEventsListBox.SelectedIndices[0];
                    NetworkEvent ne = this.m_Playback.GetEvent(currpos, this.m_Mode);
                    if (ne is NetworkChunkEvent) {
                        this.m_Connection.m_Queue.Send(((NetworkChunkEvent)ne).chunk);
                    } else if (ne is NetworkMessageEvent) {
                        this.m_Connection.m_Queue.Send(((NetworkMessageEvent)ne).message);
                    } else if (ne is NetworkNACKMessageEvent) {
                        this.m_Connection.m_Queue.Send(((NetworkNACKMessageEvent)ne).message);
                    }
                    //Increment the selected item, if we are not at the end
                    if (currpos + 1 < this.networkEventsListBox.Items.Count) {
                        this.networkEventsListBox.SetSelected(currpos, false);
                        this.networkEventsListBox.SetSelected(currpos + 1, true);
                    }
                }
            }
        }

        private void onRewindbuttonClick(object sender, System.EventArgs e) {
            if (this.networkEventsListBox.Items.Count > 0) {
                while (this.networkEventsListBox.SelectedIndices.Count > 0) {
                    int removeThis = this.networkEventsListBox.SelectedIndices[0];
                    this.networkEventsListBox.SetSelected(removeThis, false);
                }
                this.networkEventsListBox.SetSelected(0, true);
            }
        }

        private void onStopButtonClick(object sender, System.EventArgs e) {
            this.timer.Enabled = false;
        }

        private void onInfoButtonClick(object sender, System.EventArgs e) {
            if (this.m_InfoForm.Visible == true) {
                this.m_InfoForm.Hide();
                this.infoButton.Text = "Show Info";
            } else {
                this.m_InfoForm.Show();
                this.infoButton.Text = "Hide Info";
            }
        }

        private ArrayList arrayListGenerator() {
            ListBox.SelectedIndexCollection sic = this.networkEventsListBox.SelectedIndices;
            ArrayList al = new ArrayList();
            for (int i = 0; i < sic.Count; i++) {
                al.Add(sic[i]);
            }
            return al;
        }

        private void onPlayAllButtonClick(object sender, System.EventArgs e) {
            //Play everything as fast as possible
            if (this.m_Connection != null) {
                this.updateUI(false);
                int currpos = 0;
                NetworkEvent ne = this.m_Playback.GetEvent(currpos, this.m_Mode);
                while (ne != null) {
                    if (ne is NetworkChunkEvent) {
                        this.m_Connection.m_Queue.Send(((NetworkChunkEvent)ne).chunk);
                    } else if (ne is NetworkMessageEvent) {
                        this.m_Connection.m_Queue.Send(((NetworkMessageEvent)ne).message);
                    } else if (ne is NetworkNACKMessageEvent) {
                        this.m_Connection.m_Queue.Send(((NetworkNACKMessageEvent)ne).message);
                    }
                    currpos++;
                    ne = this.m_Playback.GetEvent(currpos, this.m_Mode);
                    Thread.Sleep(5);
                }
                this.updateUI(true);
            }
        }

        #endregion

        #region DropDown

        private void onClassroomDropDownSelectedIndexChanged(object sender, System.EventArgs e) {
            IPEndPoint ipe = null;
            IPAddress ipa = null;
            switch (this.classroomDropDown.SelectedIndex) {
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
                    break;
            }
            if (this.m_Connection != null) {
                //Need to disconnect first
                this.m_Connection.Dispose();
                this.m_Connection = null;
                this.statusLabel.Text = "Status: Disconnected";
            }
            if (ipa != null) {
                this.statusLabel.Text = "Status: Connecting...";
                ipe = new IPEndPoint(ipa, 5004);
                this.m_Connection = new RTPSendConnection(ipe);
                this.statusLabel.Text = "Status: Connected to " + this.classroomDropDown.SelectedItem.ToString();
            } else {
                //We don't connect..
            }
        }

        #endregion

        #region Timer

        private void timer_Tick(object sender, EventArgs e) {
            this.onStepButtonClick(this, e);
        }

        #endregion

        #region ListBox

        private void onNetworkEventsListBoxSelectedIndexChanged(object sender, System.EventArgs e) {
            //We want to show the first item of this ListBox in the NetworkEventInfoForm
            if (this.networkEventsListBox.SelectedIndices.Count > 0) {
                this.m_InfoForm.DisplayText(this.m_Playback.GetEvent(this.networkEventsListBox.SelectedIndices[0], this.m_Mode));
            }
        }

        #endregion

        #endregion
    }
}
