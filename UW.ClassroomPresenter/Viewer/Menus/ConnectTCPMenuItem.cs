using System;
using System.Collections.Generic;
using System.Text;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Network.TCP;
using System.Windows.Forms;
using UW.ClassroomPresenter.Model;
using System.Drawing;

namespace UW.ClassroomPresenter.Viewer.Menus {
    public class ConnectTCPMenuItem : MenuItem {
        private readonly ControlEventQueue m_EventQueue;
        private readonly PresenterModel m_Model;
        private readonly ProtocolCollectionHelper m_ProtocolCollectionHelper;
        private readonly EventQueue.PropertyEventDispatcher m_RoleChangedDispatcher;
        private bool m_Connected;

        private readonly List<TCPClassroomManager> m_Classrooms;

        private bool m_Disposed;

        /// <summary>
        /// Creates a new <see cref="ClassroomMenu"/>.
        /// </summary>
        /// <param name="model">
        /// The lists of classrooms and presentations are gathered from the
        /// protocols in the <see cref="PresenterModel.Network"/>.
        /// </param>
        public ConnectTCPMenuItem(ControlEventQueue dispatcher, PresenterModel model) {
            this.m_EventQueue = dispatcher;
            this.m_Model = model;
            this.m_Connected = false;
            this.Text = "Connect to &TCP Server...";
            this.m_Classrooms = new List<TCPClassroomManager>();

            //Watch for Role changes and disable if role is Instructor.
            this.m_RoleChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandleRoleChanged));
            this.m_Model.Participant.Changed["Role"].Add(this.m_RoleChangedDispatcher.Dispatcher);
            using (Synchronizer.Lock(this.m_Model.Participant.SyncRoot)) {
                if (this.m_Model.Participant.Role is InstructorModel)
                    this.Enabled = false;
                else
                    this.Enabled = true;
            }

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
                    this.Text = "Connect to &TCP Server..."; //Also causes client disconnect, so set default text.
                    this.m_Connected = false;
                    this.Enabled = false;
                }
                else {
                    this.Enabled = true;
                }
            }
        }

        private void AddClassroom(TCPClassroomManager classroom) {
            this.m_Classrooms.Add(classroom);
            this.Enabled = true;
        }

        private void RemoveClassroom(TCPClassroomManager classroom) {
            this.m_Classrooms.Remove(classroom);
            this.Enabled = (this.m_Classrooms.Count > 0);
        }

        protected override void OnClick(EventArgs click) {
            base.OnClick(click);

            if (this.m_Classrooms.Count <= 0)
                return;

            TCPClassroomManager manager = this.m_Classrooms[0];
            if (!m_Connected) {

                Form form = new Form();
                form.Text = "Connect to TCP Server...";
                form.ClientSize = new Size(350, form.ClientSize.Height);
                form.FormBorderStyle = FormBorderStyle.FixedDialog;

                Label label = new Label();
                label.Text = "Server:";
                using (Graphics graphics = label.CreateGraphics())
                    label.ClientSize = Size.Add(new Size(10, 0),
                        graphics.MeasureString(label.Text, label.Font).ToSize());
                label.Location = new Point(5, 5);

                TextBox host = new TextBox();
                host.Location = new Point(label.Right + 10, label.Top);
                host.Width = form.ClientSize.Width - host.Left - 5;

                Button okay = new Button();
                okay.Text = Strings.OK;
                okay.Width = (host.Right - label.Left) / 2 - 5;
                okay.Location = new Point(label.Left, Math.Max(host.Bottom, label.Bottom) + 10);

                Button cancel = new Button();
                cancel.Text = Strings.Cancel;
                cancel.Width = okay.Width;
                cancel.Location = new Point(okay.Right + 10, okay.Top);

                EventHandler connect = delegate(object source, EventArgs args) {
                    try {
                        //manager.ClientConnect(host.Text); //obsolete
                        this.m_Connected = true;
                        this.Text = "Disconnect from &TCP Server...";
                        form.Dispose();
                    }
                    catch (Exception e) {
                        MessageBox.Show(
                            string.Format("Classroom Presenter 3 was unable to connect to\n\n"
                                + "\t\"{0}\"\n\nfor the following reason:\n\n{1}",
                                host.Text, e.Message),
                            "Connection Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                        this.m_Connected = false;
                    }
                };

                host.KeyPress += delegate(object source, KeyPressEventArgs args) {
                    if (args.KeyChar == '\r')
                        connect(source, EventArgs.Empty);
                };

                okay.Click += connect;

                cancel.Click += delegate(object source, EventArgs args) {
                    this.m_Connected = false;
                    form.Dispose();
                };

                form.ClientSize = new Size(form.ClientSize.Width, Math.Max(okay.Bottom, cancel.Bottom) + 5);

                form.Controls.Add(label);
                form.Controls.Add(host);
                form.Controls.Add(okay);
                form.Controls.Add(cancel);

                DialogResult dr = form.ShowDialog(this.GetMainMenu() != null ? this.GetMainMenu().GetForm() : null);
            }
            else {
                //manager.ClientDisconnect();
                this.Text = "Connect to &TCP Server...";
                m_Connected = false;
            }

        }

        protected override void Dispose(bool disposing) {
            if (this.m_Disposed) return;
            try {
                if (disposing) {
                    this.m_ProtocolCollectionHelper.Dispose();
                    this.m_Model.Participant.Changed["Role"].Remove(this.m_RoleChangedDispatcher.Dispatcher);
                }
            } finally {
                base.Dispose(disposing);
            }
            this.m_Disposed = true;
        }

        /// <summary>
        /// Manages the set of <see cref="ClassroomCollectionHelper"/>s, one for each
        /// <see cref="ProtocolModel"/> in the <see cref="NetworkModel"/>.
        /// </summary>
        private class ProtocolCollectionHelper : PropertyCollectionHelper {
            private readonly ConnectTCPMenuItem m_Parent;

            public ProtocolCollectionHelper(ConnectTCPMenuItem parent, NetworkModel network)
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
                    this.m_Parent.RemoveClassroom((TCPClassroomManager) tag);
            }
        }
    }
}
