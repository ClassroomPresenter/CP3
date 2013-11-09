using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Network.TCP;
using System.Windows.Forms;
using UW.ClassroomPresenter.Model;
using System.Drawing;
using System;
using System.Collections.Generic;
using Microsoft.Ink;
using System.Net;
using System.Diagnostics;

namespace UW.ClassroomPresenter.Viewer.Classrooms {
    public class ConnectTCPRadioButton : RadioButton {
        private readonly ControlEventQueue m_EventQueue;
        private readonly PresenterModel m_Model;
        private readonly ProtocolCollectionHelper m_ProtocolCollectionHelper;
        private readonly EventQueue.PropertyEventDispatcher m_RoleChangedDispatcher;
        private bool m_Connected;
        private TCPClassroomManager m_ClassroomManager;

        private bool m_Disposed;
        ManualConnectionPanel m_Manual;
        int sizeConst = 70;

        public ConnectTCPRadioButton(ControlEventQueue dispatcher, PresenterModel model, ManualConnectionPanel manual, Point location, int width) {
            this.m_EventQueue = dispatcher;
            this.m_Model = model;
            this.m_Manual = manual; 
            this.m_Connected = false;
            this.FlatStyle = FlatStyle.System;
            this.Font = Model.Viewer.ViewerStateModel.StringFont1;
            this.Text = Strings.ConnectToTCPServer;
            this.Location = location;
            this.Size = new Size(width, this.Font.Height + 5);
            this.m_ClassroomManager = null;

            //Watch for Role changes and disable if role is Instructor.
            this.m_RoleChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandleRoleChanged));
            this.m_Model.Participant.Changed["Role"].Add(this.m_RoleChangedDispatcher.Dispatcher);
            using (Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                if (this.m_Model.Participant.Role is InstructorModel)
                    this.Enabled = false;
                else
                    this.Enabled = true;
            }

            //should probably make this a listener
            //Do persistence in a more intelligent way - this doesn't make the popup pop up.
            /*using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot)) {
                if (this.Text == this.m_Model.ViewerState.ManualConnectionButtonName) {
                    this.Checked = true;
                    }
                }*/


            this.CheckedChanged += new EventHandler(OnClick);


            this.m_ProtocolCollectionHelper = new ProtocolCollectionHelper(this, this.m_Model.Network);
        }

        /// <summary>
        /// The menu item is disabled if the role is Instructor.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args_"></param>
        private void HandleRoleChanged(object sender, PropertyEventArgs args_) {
            PropertyChangeEventArgs args = args_ as PropertyChangeEventArgs;
            if (args != null) {
                if (args.NewValue is InstructorModel) {
                    if (m_ClassroomManager != null) {
                        using (Synchronizer.Lock(m_ClassroomManager.Classroom.SyncRoot))
                            m_ClassroomManager.Classroom.Connected = false;
                    }
                    this.Text = "Connect to TCP Server";
                    this.m_Connected = false;
                    this.Enabled = false;
                }
                else {
                    this.Enabled = true;
                }
            }
        }

        private void AddClassroom(TCPClassroomManager classroom) {
            this.m_ClassroomManager = classroom;
            this.Enabled = true;
        }

        private void RemoveClassroom() {
            this.m_ClassroomManager = null;
            this.Enabled = false;
        }

        public void OnClick(object obj, EventArgs click) {

            if (!(this.Checked)) {
                if (this.m_Connected) {
                    using (Synchronizer.Lock(this.m_ClassroomManager.Classroom.SyncRoot))
                        this.m_ClassroomManager.Classroom.Connected = false;
                    this.Text = Strings.ConnectToTCPServer;
                    this.m_Connected = false;

                }
                return;
            }

            if (this.m_ClassroomManager == null)
                return;

            using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot)) {
                this.m_Model.ViewerState.ManualConnectionButtonName = this.Text;
            }
            if (this.m_Connected) {
                using (Synchronizer.Lock(this.m_ClassroomManager.Classroom.SyncRoot))
                    this.m_ClassroomManager.Classroom.Connected = false;
                this.Text = Strings.ConnectToTCPServer;
                this.m_Connected = false;
            }

            Label label = new Label();
            label.FlatStyle = FlatStyle.System;
            label.Font = Model.Viewer.ViewerStateModel.StringFont2;
            label.Text = Strings.ServerNameAddress;
            using (Graphics graphics = label.CreateGraphics())
                label.ClientSize = Size.Add(new Size(10, 0),
                    graphics.MeasureString(label.Text, label.Font).ToSize());
            label.Location = new Point(5, 5);

            Form form = new Form();
            form.Font = Model.Viewer.ViewerStateModel.FormFont;
            form.Text = Strings.ConnectToTCPServer;
            form.ClientSize = new Size(label.Width * 3, 2 * form.ClientSize.Height);
            form.FormBorderStyle = FormBorderStyle.FixedDialog;

            
            TextBox addressInput = new TextBox();
            addressInput.Font = Model.Viewer.ViewerStateModel.StringFont2;
            addressInput.Size = new Size(((int)(form.ClientSize.Width - (label.Width * 1.2))), this.Font.Height);
            addressInput.Location = new Point(((int)(label.Right * 1.1)), label.Top);

           
            using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot)) {
                if (this.m_Model.ViewerState.TCPaddress != "" && this.m_Model.ViewerState.TCPaddress.Length != 0) {
                    string addr = this.m_Model.ViewerState.TCPaddress;
                    addressInput.Text = addr;
                }
            }
           
            Label label2 = new Label();
            label2.FlatStyle = FlatStyle.System;
            label2.Font = Model.Viewer.ViewerStateModel.StringFont2;
            label2.Text = Strings.Port;
            using (Graphics graphics = label.CreateGraphics())
                label2.ClientSize = Size.Add(new Size(10, 0),
                    graphics.MeasureString(label2.Text, label2.Font).ToSize());
            label2.Location = new Point(5, Math.Max(addressInput.Bottom, label.Bottom) + 10);

            TextBox portInput = new TextBox();
            portInput.Font = Model.Viewer.ViewerStateModel.StringFont2;
            portInput.Size = new Size((4 / 3) * sizeConst, sizeConst / 2);
            portInput.Location = new Point(addressInput.Left, label2.Top);
            portInput.Text = TCPServer.DefaultPort.ToString();

            Button okay = new Button();
            okay.FlatStyle = FlatStyle.System;
            okay.Font = Model.Viewer.ViewerStateModel.StringFont;
            okay.Text = Strings.OK;
            okay.Width = (addressInput.Right - label.Left) / 2 - 5;
            okay.Location = new Point(label.Left, Math.Max(portInput.Bottom, label2.Bottom) + 10);

            Button cancel = new Button();
            cancel.FlatStyle = FlatStyle.System;
            cancel.Font = Model.Viewer.ViewerStateModel.StringFont;
            cancel.Text = Strings.Cancel;
            cancel.Width = okay.Width;
            cancel.Location = new Point(okay.Right + 10, okay.Top);

            EventHandler connect = delegate(object source, EventArgs args) {
                
                    string address = addressInput.Text;

                using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot)) {
                    this.m_Model.ViewerState.TCPaddress = address;
                }
                try {
                    IPAddress ipaddress;
                    if (!IPAddress.TryParse(address, out ipaddress)) { 
                        IPHostEntry entry = Dns.GetHostEntry(address);
                        ipaddress = entry.AddressList[0];
                        if (entry.AddressList.Length > 1) {
                            //This happened once on the UW network.  I think it was a case of broken DNS.
                            Trace.WriteLine("Warning: Multiple Server IP addresses found. CP3 is arbitrarily choosing one.");
                        }
                    }
                    int port = 0;
                    if (!Int32.TryParse(portInput.Text, out port)) {
                        port = TCPServer.DefaultPort;
                    }
                    IPEndPoint ep = new IPEndPoint(ipaddress, port);
                    this.m_ClassroomManager.RemoteServerEndPoint = ep;
                    using (Synchronizer.Lock(m_ClassroomManager.Classroom.SyncRoot))
                        m_ClassroomManager.Classroom.Connected = true;
                    this.m_Connected = true;
                    form.Visible = false;
                }
                catch (Exception e) {
                    MessageBox.Show(
                        string.Format("Classroom Presenter was unable to connect to\n\n"
                            + "\t\"{0}\"\n\nfor the following reason:\n\n{1}",
                            address, e.Message),
                        "Connection Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    this.m_Connected = false;
                }
            };

            /*host.KeyPress += delegate(object source, KeyPressEventArgs args) {
                if (args.KeyChar == '\r')
                    connect(source, EventArgs.Empty);
                }*/

            okay.Click += connect;
            okay.DialogResult = DialogResult.OK;

            form.CancelButton = cancel;
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Click += delegate(object source, EventArgs args) {
                this.m_Connected = false;
                form.Visible = false;
            };

            form.ClientSize = new Size(form.ClientSize.Width, Math.Max(okay.Bottom, cancel.Bottom) + 5);

            form.Controls.Add(label);
            form.Controls.Add(addressInput);
            form.Controls.Add(label2);
            form.Controls.Add(portInput);
            form.Controls.Add(okay);
            form.Controls.Add(cancel);

            //Find the startup form which is the proper owner of the modal dialog
            Control c = this;
            while (c.Parent != null) {
                c = c.Parent;
            }

            DialogResult dr = form.ShowDialog(c);
            form.Dispose();
            if (dr == DialogResult.Cancel) {
                //This causes another call to OnClick but it should return without doing anything.
                this.Checked = false;
            }

        }

        protected override void Dispose(bool disposing) {
            if (this.m_Disposed) return;
            try {
                if (disposing) {
                    this.m_ProtocolCollectionHelper.Dispose();
                    this.m_Model.Participant.Changed["Role"].Remove(this.m_RoleChangedDispatcher.Dispatcher);
                }
            }
            finally {
                base.Dispose(disposing);
            }
            this.m_Disposed = true;
        }

        /// <summary>
        /// Manages the set of <see cref="ClassroomCollectionHelper"/>s, one for each
        /// <see cref="ProtocolModel"/> in the <see cref="NetworkModel"/>.
        /// </summary>
        private class ProtocolCollectionHelper : PropertyCollectionHelper {
            private readonly ConnectTCPRadioButton m_Parent;

            public ProtocolCollectionHelper(ConnectTCPRadioButton parent, NetworkModel network)
                : base(parent.m_EventQueue, network, "Protocols") {
                this.m_Parent = parent;
                base.Initialize();
            }

            protected override void Dispose(bool disposing) {
                if (disposing)
                    foreach (IDisposable disposable in this.Tags)
                        if (disposable != null)
                            disposable.Dispose();
                base.Dispose(disposing);
            }

            protected override object SetUpMember(int index, object member) {
                ProtocolModel protocol = member as ProtocolModel;
                if (protocol == null)
                    return null;

                TCPConnectionManager connection = protocol.ConnectionManager as TCPConnectionManager;
                if (connection == null)
                    return null;

                this.m_Parent.AddClassroom(connection.ClassroomManager);

                return connection.ClassroomManager;
            }

            protected override void TearDownMember(int index, object member, object tag) {
                if (tag != null)
                    this.m_Parent.RemoveClassroom();
            }
        }
    }
    public class ReenterServerButton : Button {
        ConnectTCPRadioButton myTCPButton;
        public ReenterServerButton(ConnectTCPRadioButton TCPButton) {
            this.FlatStyle = FlatStyle.System;
            this.Font = Model.Viewer.ViewerStateModel.StringFont;
            this.Text = Strings.Reenter;
            this.myTCPButton = TCPButton;
            this.myTCPButton.CheckedChanged += new EventHandler(myTCPButton_CheckedChanged);
            this.Enabled = this.myTCPButton.Checked;

        }

        void myTCPButton_CheckedChanged(object sender, EventArgs e) {
            this.Enabled = this.myTCPButton.Checked;
        }
        protected override void OnClick(EventArgs e) {
            base.OnClick(e);
            this.myTCPButton.OnClick(null, null);

        }
    }

}
