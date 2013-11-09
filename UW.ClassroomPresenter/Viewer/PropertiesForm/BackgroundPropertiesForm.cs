using System;
using System.Collections;
using System.Drawing;
using System.Windows.Forms;

using UW.ClassroomPresenter.Misc;
using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Background;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Model.Viewer;
using UW.ClassroomPresenter.Viewer.Background;

namespace UW.ClassroomPresenter.Viewer.PropertiesForm
{
    /// <summary>
    /// Configuration Form for background template
    /// </summary>
    public class BackgroundPropertiesForm:Form
    {
        private PresenterModel m_Model;
        private BackgroundTemplate m_Template;

        private BkgTemplatePreviewGroup m_BkgTemplatePreviewGroup;
        private BkgTemplateSelectionGroup m_BkgTemplateSelectionGroup;

        private bool m_ApplyToCurrentSlideOnly;
        public bool ApplyToCurrentSlideOnly
        {
            get { return this.m_ApplyToCurrentSlideOnly; }
            set { this.m_ApplyToCurrentSlideOnly = value; }
        }

        /// <summary>
        /// Construction for Form
        /// </summary>
        public BackgroundPropertiesForm(PresenterModel model)
        {
            this.m_Model = model;

            this.SuspendLayout();
            this.ClientSize = new Size(320, 320);
            this.Font = ViewerStateModel.FormFont;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.Name = "BackgroundPropertiesForm";
            this.Text = Strings.BackgroundPropertiesFormTitle;

            //Get the BackgroundTemplate Model
            using (this.m_Model.Workspace.Lock())
            {
                DeckTraversalModel traversal = this.m_Model.Workspace.CurrentDeckTraversal;
                if (traversal != null)
                {
                    using (Synchronizer.Lock(traversal.SyncRoot))
                    {
                        if (this.m_ApplyToCurrentSlideOnly)
                        {
                            SlideModel current = traversal.Current.Slide;
                            if (current != null)
                            {
                                using (Synchronizer.Lock(traversal.Current.Slide.SyncRoot))
                                {
                                    if (traversal.Current.Slide.BackgroundTemplate != null)
                                    {
                                        this.m_Template = traversal.Current.Slide.BackgroundTemplate.Clone();
                                    }
                                }
                            }
                        }
                        else
                        {
                            DeckModel deck = traversal.Deck;
                            using (Synchronizer.Lock(deck.SyncRoot))
                            {
                                if (deck.DeckBackgroundTemplate != null)
                                    this.m_Template = deck.DeckBackgroundTemplate.Clone();
                            }
                        }
                    }
                }
            }

            this.m_BkgTemplateSelectionGroup = new BkgTemplateSelectionGroup(this, this.m_Template, new Point(20, 10), new Size(280, 50), 0);
            this.Controls.Add(this.m_BkgTemplateSelectionGroup);

            this.m_BkgTemplatePreviewGroup = new BkgTemplatePreviewGroup(this, new Point(20, 70), new Size(280, 210), 1);
            this.Controls.Add(this.m_BkgTemplatePreviewGroup);

            this.Controls.Add(new PropertiesOKButton(this, this.m_Model, new Point(80, 285), 2));
            this.Controls.Add(new PropertiesCancelButton(this, this.m_Model, this.m_Template, new Point(180, 285), 3));

            this.ResumeLayout();
        }

        public override void Refresh()
        {
            if(this.m_BkgTemplatePreviewGroup!=null)
                this.m_BkgTemplatePreviewGroup.Refresh();
            base.Refresh();
        }

        #region Template Selection Group

        /// <summary>
        /// GroupBox for Background Template Selection
        /// </summary>
        public class BkgTemplateSelectionGroup : GroupBox
        {
            private BkgTemplateSelectionComboBox m_BkgTemplateSelectionComboBox;
            public BkgTemplateSelectionGroup(BackgroundPropertiesForm parent, BackgroundTemplate template, Point location, Size size, int tabIndex)
            {
                this.SuspendLayout();

                this.FlatStyle = FlatStyle.System;
                this.Location = location;
                this.Size = size;
                this.TabIndex = tabIndex;
                this.Name = "BkgTemplateSelectionGroup";
                this.TabStop = false;
                this.Text = Strings.BkgChooseTemplate;

                this.m_BkgTemplateSelectionComboBox = new BkgTemplateSelectionComboBox(parent, new Point(20, 20), new Size(180, 30), 0);
                this.Controls.Add(this.m_BkgTemplateSelectionComboBox);

                this.ResumeLayout();
            }

            public override void Refresh()
            {
                this.m_BkgTemplateSelectionComboBox.Refresh();
                base.Refresh();
            }

            /// <summary>
            /// ComboBox for background template selection
            /// </summary>
            public class BkgTemplateSelectionComboBox : ComboBox
            {
                private BackgroundPropertiesForm m_Form;

                public BkgTemplateSelectionComboBox(BackgroundPropertiesForm form, Point location, Size size, int tabIndex)
                {
                    this.m_Form = form;

                    this.SuspendLayout();
                    this.Location = location;
                    this.Size = size;
                    this.TabIndex = tabIndex;
                    this.FlatStyle = FlatStyle.System;

                    this.DropDownStyle = ComboBoxStyle.DropDownList;

                    this.Items.Clear();

                    int initialSelect=-1;

                    //Add the templates from xml file into the list
                    using (BackgroundTemplateXmlService service = new BackgroundTemplateXmlService(this.m_Form.m_Model.ViewerState))
                    {
                        ArrayList templates=new ArrayList ();
                        service.GetTemplates(templates);
                        for (int i = 0; i < templates.Count; i++)
                        {
                            this.Items.Add(templates[i]);
                            if (this.m_Form.m_Template != null && this.m_Form.m_Template.Name != null && this.m_Form.m_Template.Name.Equals(((BackgroundTemplate)templates[i]).Name))
                                initialSelect = i;
                        }
                    }

                    //if the current deck/slide template is not in the xml file,
                    if (this.m_Form.m_Template != null && initialSelect == -1)
                    {
                        this.Items.Add(m_Form.m_Template.Clone());
                        initialSelect = this.Items.Count - 1;
                    }

                    //Add no background template selection
                    this.Items.Add(Strings.NoBackgroundTemplate);
                    if (this.m_Form.m_Template == null)
                        initialSelect = this.Items.Count - 1;

                    this.SelectedIndex = initialSelect;

                    this.ResumeLayout();
                }

                /// <summary>
                /// Refresh all the properties
                /// </summary>
                protected override void OnSelectedIndexChanged(EventArgs e)
                {
                    //if the current select is no template, then set m_Template null
                    //else if the current select is a specified template, and m_Template  is null, then clone a new template;
                    //else the current select is a specified template, and m_Template is not null, then update the m_Template
                    if (this.SelectedIndex == this.Items.Count - 1)
                        this.m_Form.m_Template = null;

                    else if (this.m_Form.m_Template == null && this.SelectedIndex != this.Items.Count - 1)
                        this.m_Form.m_Template = ((BackgroundTemplate)this.Items[this.SelectedIndex]).Clone();

                    else
                        this.m_Form.m_Template.UpdateValue((BackgroundTemplate)this.Items[this.SelectedIndex]);

                    this.m_Form.Refresh();
                    base.OnSelectedIndexChanged(e);
                }
            }
        }

        #endregion Template Selection Group

        #region Template Preview Group

        /// <summary>
        /// GroupBox for Background Template Selection
        /// </summary>
        public class BkgTemplatePreviewGroup : GroupBox
        {
            /// <summary>
            /// The panel for preview
            /// </summary>
            private BkgTemplatePreviewPanel m_PreviewPanel;

            /// <summary>
            /// Construction
            /// </summary>
            /// <param name="template">BackgroundTemplate to show up</param>
            /// <param name="location">Location fo the Preview group</param>
            /// <param name="size">Size of the Group</param>
            /// <param name="tabIndex">tab index</param>
            public BkgTemplatePreviewGroup(BackgroundPropertiesForm form, Point location, Size size, int tabIndex)
            {
                this.SuspendLayout();

                this.FlatStyle = FlatStyle.System;
                this.Location = location;
                this.Size = size;
                this.TabIndex = tabIndex;
                this.Name = "BkgTemplatePreviewGroup";
                this.TabStop = false;
                this.Text = Strings.Preview;

                this.m_PreviewPanel = new BkgTemplatePreviewPanel(form, new Point(20, 20), new Size(240, 180));
                this.Controls.Add(this.m_PreviewPanel);

                this.ResumeLayout();
            }

            /// <summary>
            /// Refresh the preview
            /// </summary>
            public override void Refresh()
            {
                this.m_PreviewPanel.Invalidate();
                base.Refresh();
            }

            /// <summary>
            /// Preview Panel to draw the BackgroundTemplate
            /// </summary>
            public class BkgTemplatePreviewPanel : Panel
            {
                private BackgroundPropertiesForm m_Form;

                public BkgTemplatePreviewPanel(BackgroundPropertiesForm form, Point location, Size size)
                {
                    this.m_Form = form;
                    this.SuspendLayout();

                    this.Location = location;
                    this.Size = size;

                    this.ResumeLayout();
                }

                /// <summary>
                /// Override the OnPaint to render the template
                /// </summary>
                protected override void OnPaint(PaintEventArgs e)
                {
                    base.OnPaint(e);
                    using (BackgroundTemplateRenderer render = new BackgroundTemplateRenderer(this.m_Form.m_Template))
                    {
                        render.DrawAll(e.Graphics, this.ClientRectangle);
                    }
                }
            }
        }

        #endregion Template Preview Group

        #region BottomButtons

        /// <summary>
        /// The OK Button for the Background Properties Form
        /// </summary>
        public class PropertiesOKButton : Button
        {
            private PresenterModel m_Model;
            private BackgroundPropertiesForm m_Parent;

            public PropertiesOKButton(BackgroundPropertiesForm parent, PresenterModel model, Point location, int tabIndex)
            {
                this.m_Model = model;
                this.m_Parent = parent;

                this.DialogResult = System.Windows.Forms.DialogResult.OK;
                this.FlatStyle = FlatStyle.System;
                this.Font = ViewerStateModel.StringFont;
                this.Location = location;
                this.Name = "propertiesOKButton";
                this.TabIndex = tabIndex;
                this.Text = Strings.OK;
            }

            protected override void OnClick(EventArgs e)
            {
                //Set the BackgroundTemplate to Slide or Deck accroding to the setting
                using (this.m_Model.Workspace.Lock())
                {
                    DeckTraversalModel traversal = this.m_Model.Workspace.CurrentDeckTraversal;
                    if (traversal != null)
                    {
                        using (Synchronizer.Lock(traversal.SyncRoot))
                        {
                            if (this.m_Parent.ApplyToCurrentSlideOnly)
                            {
                                using (Synchronizer.Lock(traversal.Current.Slide.SyncRoot))
                                {
                                   traversal.Current.Slide.BackgroundTemplate = this.m_Parent.m_Template;
                                }
                            }
                            else
                            {
                                DeckModel deck = traversal.Deck;
                                using (Synchronizer.Lock(deck.SyncRoot))
                                {
                                    deck.DeckBackgroundTemplate = this.m_Parent.m_Template;
                                }
                            }
                        }
                    }
                }

                this.m_Parent.Close();
                base.OnClick(e);
            }
        }

        /// <summary>
        /// The Cancel Button for the Background Properties Form
        /// </summary>
        public class PropertiesCancelButton : Button
        {
            private BackgroundPropertiesForm m_Parent;
            public PropertiesCancelButton(BackgroundPropertiesForm parent, PresenterModel model, BackgroundTemplate template, Point location, int tabIndex)
            {
                this.m_Parent = parent;
                this.DialogResult = System.Windows.Forms.DialogResult.Cancel;
                this.FlatStyle = FlatStyle.System;
                this.Font = ViewerStateModel.StringFont;
                this.Location = location;
                this.Name = "propertiesCancelButton";
                this.TabIndex = tabIndex;
                this.Text = Strings.Cancel;
            }

            protected override void OnClick(EventArgs e)
            {
                this.m_Parent.Close();
                base.OnClick(e);
            }
        }

        #endregion
            
    }
}
