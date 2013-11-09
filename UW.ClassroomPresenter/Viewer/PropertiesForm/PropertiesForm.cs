// $Id: PropertiesForm.cs 1698 2008-08-12 00:00:06Z lamphare $

using System;
using System.Drawing;
using System.Windows.Forms;
using UW.ClassroomPresenter.Model.Viewer;

namespace UW.ClassroomPresenter.Viewer.PropertiesForm {
    /// <summary>
    /// The properties form allow the user to modify the properties of the machine and apply those
    /// to Presenter
    /// </summary>
    public class PropertiesForm : Form {

        // The application model
        internal ViewerStateModel applicationModel;
        internal ViewerStateModel localModel;

        /// <summary>
        /// Constructs a properties form and hooks it into the model
        /// </summary>
        public PropertiesForm(ViewerStateModel model) {

            // Save the model
            applicationModel = model;
            localModel = (ViewerStateModel)model.Clone();

            // Setup the display of the form
            this.SuspendLayout();

            this.AutoScaleBaseSize = new Size(5, 13);
            this.ClientSize = new System.Drawing.Size(564, 342);
            this.Font = ViewerStateModel.FormFont;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.Name = "PropertiesForm";
            this.Text = Strings.Properties;

            // Add the child controls
            this.Controls.Add(new PropertiesTabControl(localModel, new Point(0, 0), new Size(564, 304), 0));
            this.Controls.Add(new PropertiesOKButton(applicationModel, localModel, new Point(122, 310), 1));
            this.Controls.Add(new PropertiesCancelButton(applicationModel, localModel, new Point(241, 310), 2));
            this.Controls.Add(new PropertiesApplyButton(applicationModel, localModel, new Point(360, 310), 3));

            this.ResumeLayout();
        }

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);
        }

        /// <summary>
        /// The OK Button for the Properties Form
        /// </summary>
        public class PropertiesOKButton : Button {
            private ViewerStateModel applicationModel;
            private ViewerStateModel localModel;

            public PropertiesOKButton(ViewerStateModel oldModel, ViewerStateModel newModel, Point location, int tabIndex) {
                this.applicationModel = oldModel;
                this.localModel = newModel;

                this.DialogResult = System.Windows.Forms.DialogResult.OK;
                this.FlatStyle = FlatStyle.System;
                this.Font = ViewerStateModel.StringFont;
                this.Location = location;
                this.Name = "propertiesOKButton";
                this.TabIndex = tabIndex;
                this.Text = Strings.OK;
            }

            protected override void OnClick(EventArgs e) {
                // Update the model
                if (applicationModel != null)
                    applicationModel.UpdateValues(localModel);

                base.OnClick(e);
            }
        }

        /// <summary>
        /// The Cancel Button for the Properties Form
        /// </summary>
        public class PropertiesCancelButton : Button {
            public PropertiesCancelButton(ViewerStateModel oldModel, ViewerStateModel newModel, Point location, int tabIndex) {
                this.DialogResult = System.Windows.Forms.DialogResult.Cancel;
                this.FlatStyle = FlatStyle.System;
                this.Font = ViewerStateModel.StringFont;
                this.Location = location;
                this.Name = "propertiesCancelButton";
                this.TabIndex = tabIndex;
                this.Text = Strings.Cancel;
            }

            protected override void OnClick(EventArgs e) {
                base.OnClick(e);
            }
        }

        /// <summary>
        /// The Apply Button for the Properties Form
        /// </summary>
        public class PropertiesApplyButton : Button {
            private ViewerStateModel applicationModel;
            private ViewerStateModel localModel;

            public PropertiesApplyButton(ViewerStateModel oldModel, ViewerStateModel newModel, Point location, int tabIndex) {
                this.applicationModel = oldModel;
                this.localModel = newModel;

                this.FlatStyle = FlatStyle.System;
                this.Font = ViewerStateModel.StringFont;
                this.Location = location;
                this.Name = "propertiesApplyButton";
                this.TabIndex = tabIndex;
                this.Text = Strings.Apply;
            }

            protected override void OnClick(EventArgs e) {
                // Update the model
                if (applicationModel != null)
                    applicationModel.UpdateValues(localModel);

                base.OnClick(e);
            }
        }

        /// <summary>
        /// The Tab Control that holds the pages for the Properties From
        /// </summary>
        public class PropertiesTabControl : TabControl {
            public PropertiesTabControl(ViewerStateModel model, Point location, Size size, int tabIndex) {
                this.SuspendLayout();

                this.Font = ViewerStateModel.StringFont;
                this.ItemSize = new System.Drawing.Size(52, 18);
                this.Location = location;
                this.Name = "tabControl";
                this.SelectedIndex = 0;
                this.Size = size;
                this.TabIndex = tabIndex;

                ///
                /// NOTE: Unused property forms and controls were removed in r1313.
                /// They were deleted rather than commented out to avoid code cruftification.
                /// If you want to restore the InkPlaybackPropertiesPage, ReflectorPropertiesPage,
                /// or NamesProperiesPage in the future, just go take a look at r1312.
                /// 

                this.Controls.Add( new GeneralPropertiesPage( model ) );
                this.Controls.Add( new NetworkPropertiesPage( model ) );
                this.Controls.Add( new DisplayPropertiesPage( model ) );
#if DEBUG && LOGGING
                this.Controls.Add( new LoggingPropertiesPage( model ) );
#endif

                this.ResumeLayout();
            }
        }
    }
}
