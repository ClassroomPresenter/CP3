// $Id: PenPropertiesPage.cs 2164 2010-01-19 19:58:04Z fred $

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Ink;
using Microsoft.StylusInput;
using UW.ClassroomPresenter.Model.Viewer;

namespace UW.ClassroomPresenter.Viewer.PropertiesForm {

    /// <summary>
    /// Summary description for NetworkPropertiesPage.
    /// </summary>
    public class PenPropertiesPage : Form {
        private readonly bool isPen;
        private readonly PenStateModel psm;
        /// <summary>
        /// Constructs a new Network Properties page
        /// </summary>
        public PenPropertiesPage(PenStateModel psm, DrawingAttributes atts, bool isPen) {
            this.DrawingAttributes = atts;
            this.psm = psm;
            this.isPen = isPen;

            this.SuspendLayout();
            
            this.Name = "PenPropertiesPage";
            this.ClientSize = new Size(510, 238);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.Font = ViewerStateModel.FormFont;
            this.Text = Strings.Pen;

            // Add the controls
            this.Controls.Add(new PenTypeGroupBox(this, new Point(5, 8), new Size(174, 64), 0));
            this.Controls.Add(new PenShapeGroupBox(this, new Point(5, 72), new Size(174, 64), 1));
            this.Controls.Add(new PenColorGroupBox(this, new Point(5, 136), new Size(174, 48), 2));
            this.Controls.Add(new TipPropertiesGroupBox(this, new Point(184, 8), new Size(160, 176), 3));
            this.Controls.Add(new ScribbleGroupBox(this, new Point(352, 8), new Size(144, 224), 4));
            this.Controls.Add(new UndoChangesButton(this, new Point(185, 200), new Size(154, 23), 5));
            this.Controls.Add(new OKButton(this, new Point(16, 200), new Size(96, 23), 6));

            this.FireDrawingAttributesChanged(this);

            this.ResumeLayout();
        }

        public readonly DrawingAttributes DrawingAttributes;

        protected event EventHandler DrawingAttributesChanged;

        protected void FireDrawingAttributesChanged(object sender) {
            using (Synchronizer.Lock(this.psm.SyncRoot)) {
                if (this.isPen) {
                    this.psm.PenRasterOperation = this.DrawingAttributes.RasterOperation;
                    this.psm.PenTip = this.DrawingAttributes.PenTip;
                    this.psm.PenColor = this.DrawingAttributes.Color.ToArgb();
                    this.psm.PenWidth = (int)this.DrawingAttributes.Width;
                    this.psm.PenHeight = (int)this.DrawingAttributes.Height;
                } else {
                    this.psm.HLRasterOperation = this.DrawingAttributes.RasterOperation;
                    this.psm.HLTip = this.DrawingAttributes.PenTip;
                    this.psm.HLColor = this.DrawingAttributes.Color.ToArgb();
                    this.psm.HLWidth = (int)this.DrawingAttributes.Width;
                    this.psm.HLHeight = (int)this.DrawingAttributes.Height;
                }
            }

            if (this.DrawingAttributesChanged != null)
                this.DrawingAttributesChanged(sender, EventArgs.Empty);
        }

        #region PenTypeGroupBox

        class PenTypeGroupBox : GroupBox {
            private readonly PenPropertiesPage Owner;

            public PenTypeGroupBox(PenPropertiesPage owner, Point location, Size size, int tabIndex) {
                this.Owner = owner;
                this.SuspendLayout();

                this.FlatStyle = FlatStyle.System;
                this.Font = ViewerStateModel.StringFont;
                this.Location = location;
                this.Size = size;
                this.TabIndex = tabIndex;
                this.Name = "PenTypeGroupBox";
                this.TabStop = false;
                this.Text = Strings.PenType;
                this.Enabled = true;

                // Add the controls
                this.Controls.Add(new PenButton(this.Owner, new Point(3, 16), new Size(74, 40), 0));
                this.Controls.Add(new HighlighterButton(this.Owner, new Point(79, 16), new Size(92, 40), 1));

                this.ResumeLayout();
            }

            class PenButton : Button {
                private readonly PenPropertiesPage Owner;

                public PenButton(PenPropertiesPage owner, Point location, Size size, int tabIndex) {
                    this.SuspendLayout();
                    this.Owner = owner;
                    owner.DrawingAttributesChanged += new EventHandler(this.OnDrawingAttributesChanged);

                    this.Location = location;
                    this.Size = size;
                    this.TabIndex = tabIndex;
                    // this.ImageIndex = 11;
                    // this.ImageList = this.imageList40;
                    this.Name = "PenButton";
                    this.Text = Strings.Pen;
                    this.FlatStyle = FlatStyle.Flat;

                    this.ResumeLayout();
                }

                protected virtual void OnDrawingAttributesChanged(object sender, EventArgs args) {
                    this.BackColor = (this.Owner.DrawingAttributes.RasterOperation == RasterOperation.CopyPen)
                        ? SystemColors.ButtonHighlight : SystemColors.ButtonFace;
                }

                protected override void OnClick(EventArgs e) {
                    base.OnClick(e);
                    this.Owner.DrawingAttributes.RasterOperation = RasterOperation.CopyPen;
                    this.Owner.FireDrawingAttributesChanged(this);
                }
            }

            class HighlighterButton : Button {
                private readonly PenPropertiesPage Owner;

                public HighlighterButton(PenPropertiesPage owner, Point location, Size size, int tabIndex) {
                    this.SuspendLayout();
                    this.Owner = owner;
                    owner.DrawingAttributesChanged += new EventHandler(this.OnDrawingAttributesChanged);

                    this.Location = location;
                    this.Size = size;
                    this.TabIndex = tabIndex;
                    // this.ImageIndex = 11;
                    // this.ImageList = this.imageList40;
                    this.Name = "HighlighterButton";
                    this.Text = Strings.Highlighter;
                    this.FlatStyle = FlatStyle.Flat;

                    this.ResumeLayout();
                }

                protected virtual void OnDrawingAttributesChanged(object sender, EventArgs args) {
                    this.BackColor = (this.Owner.DrawingAttributes.RasterOperation == RasterOperation.MaskPen)
                        ? SystemColors.ButtonHighlight : SystemColors.ButtonFace;
                }

                protected override void OnClick(EventArgs e) {
                    base.OnClick(e);
                    this.Owner.DrawingAttributes.RasterOperation = RasterOperation.MaskPen;
                    this.Owner.FireDrawingAttributesChanged(this);
                }
            }
        }

        #endregion

        #region PenShapeGroupBox

        class PenShapeGroupBox : GroupBox {
            private readonly PenPropertiesPage Owner;
            
            public PenShapeGroupBox(PenPropertiesPage owner, Point location, Size size, int tabIndex) {
                this.SuspendLayout();
                this.Owner = owner;

                this.Font = ViewerStateModel.StringFont;
                this.Location = location;
                this.Size = size;
                this.TabIndex = tabIndex;
                this.Name = "PenShapeGroupBox";
                this.TabStop = false;
                this.Text = Strings.PenShape;
                this.Enabled = true;

                // Add the controls
                this.Controls.Add(new RoundShapeButton(this.Owner, new Point(6, 16), new Size(75, 40), 0));
                this.Controls.Add(new RectangleShapeButton(this.Owner, new Point(83, 16), new Size(85, 40), 1));

                this.ResumeLayout();
            }

            class RoundShapeButton : Button {
                private readonly PenPropertiesPage Owner;

                public RoundShapeButton(PenPropertiesPage owner, Point location, Size size, int tabIndex) {
                    this.SuspendLayout();
                    this.Owner = owner;
                    owner.DrawingAttributesChanged += new EventHandler(this.OnDrawingAttributesChanged);

                    this.Location = location;
                    this.Size = size;
                    this.TabIndex = tabIndex;
                    // this.ImageIndex = 11;
                    // this.ImageList = this.imageList40;
                    this.Name = "RoundShapeButton";
                    this.Text = Strings.PenRoundShape;
                    this.FlatStyle = FlatStyle.Flat;

                    this.ResumeLayout();
                }


                protected virtual void OnDrawingAttributesChanged(object sender, EventArgs args) {
                    this.BackColor = (this.Owner.DrawingAttributes.PenTip == PenTip.Ball)
                        ? SystemColors.ButtonHighlight : SystemColors.ButtonFace;
                }

                protected override void OnClick(EventArgs e) {
                    base.OnClick(e);
                    this.Owner.DrawingAttributes.PenTip = PenTip.Ball;
                    this.Owner.FireDrawingAttributesChanged(this);
                }
            }

            class RectangleShapeButton : Button {
                private readonly PenPropertiesPage Owner;

                public RectangleShapeButton(PenPropertiesPage owner, Point location, Size size, int tabIndex) {
                    this.SuspendLayout();
                    this.Owner = owner;
                    owner.DrawingAttributesChanged += new EventHandler(this.OnDrawingAttributesChanged);

                    this.Location = location;
                    this.Size = size;
                    this.TabIndex = tabIndex;
                    // this.ImageIndex = 11;
                    // this.ImageList = this.imageList40;
                    this.Name = "RectangleShapeButton";
                    this.Text = Strings.PenSquareShape;
                    this.FlatStyle = FlatStyle.Flat;

                    this.ResumeLayout();
                }

                protected virtual void OnDrawingAttributesChanged(object sender, EventArgs args) {
                    this.BackColor = (this.Owner.DrawingAttributes.PenTip == PenTip.Rectangle)
                        ? SystemColors.ButtonHighlight : SystemColors.ButtonFace;
                }

                protected override void OnClick(EventArgs e) {
                    base.OnClick(e);
                    this.Owner.DrawingAttributes.PenTip = PenTip.Rectangle;
                    this.Owner.FireDrawingAttributesChanged(this);
                }
            }
        }

        #endregion

        #region PenColorGroupBox

        class PenColorGroupBox : GroupBox {
            private readonly PenPropertiesPage Owner;

            public PenColorGroupBox(PenPropertiesPage owner, Point location, Size size, int tabIndex) {
                this.SuspendLayout();
                this.Owner = owner;

                this.FlatStyle = FlatStyle.System;
                this.Font = ViewerStateModel.StringFont;
                this.Location = location;
                this.Size = size;
                this.TabIndex = tabIndex;
                this.Name = "PenColorGroupBox";
                this.TabStop = false;
                this.Text = Strings.PenCustomColor;
                this.Enabled = true;

                // Add the controls
                this.Controls.Add(new ColorChangeButton(this.Owner, new Point(16, 16), new Size(132, 23), 1));

                this.ResumeLayout();
            }

            /// <summary>
            ///
            /// </summary>
            class ColorChangeButton : Button {
                private readonly PenPropertiesPage Owner;

                public ColorChangeButton(PenPropertiesPage owner, Point location, Size size, int tabIndex) {
                    this.SuspendLayout();
                    this.Owner = owner;
                    owner.DrawingAttributesChanged += new EventHandler(this.OnDrawingAttributesChanged);

                    this.Location = location;
                    this.Size = size;
                    this.TabIndex = tabIndex;
                    this.Name = "ColorChangeButton";
                    this.FlatStyle = FlatStyle.Flat;

                    this.ResumeLayout();
                }

                protected virtual void OnDrawingAttributesChanged(object sender, EventArgs args) {
                    this.BackColor = this.Owner.DrawingAttributes.Color;
                }

                protected override void OnClick(EventArgs e) {
                    base.OnClick(e);

                    ColorDialog picker = new ColorDialog();
                    picker.Color = this.Owner.DrawingAttributes.Color;
                    DialogResult result = picker.ShowDialog(this);

                    if (result == DialogResult.OK) {
                        this.Owner.DrawingAttributes.Color = picker.Color;
                        this.Owner.FireDrawingAttributesChanged(this);
                    }
                }
            }
        }

        #endregion

        #region TipPropertiesGroupBox

        class TipPropertiesGroupBox : GroupBox {
            private readonly PenPropertiesPage Owner;

            public TipPropertiesGroupBox(PenPropertiesPage owner, Point location, Size size, int tabIndex) {
                this.SuspendLayout();
                this.Owner = owner;

                this.FlatStyle = FlatStyle.System;
                this.Font = ViewerStateModel.StringFont;
                this.Location = location;
                this.Size = size;
                this.TabIndex = tabIndex;
                this.Name = "TipPropertiesGroupBox";
                this.TabStop = false;
                this.Text = Strings.PenTip;
                this.Enabled = true;

                // Add the controls
                this.Controls.Add(new WidthSelectorLabel(this.Owner, new Point(16, 16), new Size(80, 16), 0));
                this.Controls.Add(new WidthSelector(this.Owner, new Point(8, 32), new Size(144, 45), 1));
                this.Controls.Add(new HeightSelectorLabel(this.Owner, new Point(16, 80), new Size(80, 16), 2));
                this.Controls.Add(new HeightSelector(this.Owner, new Point(8, 96), new Size(144, 45), 3));

                this.ResumeLayout();
            }

            /// <summary>
            ///
            /// </summary>
            class WidthSelectorLabel : Label {
                private readonly PenPropertiesPage Owner;

                public WidthSelectorLabel(PenPropertiesPage owner, Point location, Size size, int tabIndex) {
                    this.SuspendLayout();
                    this.Owner = owner;

                    this.FlatStyle = FlatStyle.System;
                    this.Location = location;
                    this.Size = size;
                    this.TabIndex = tabIndex;
                    this.Name = "WidthSelectorLabel";
                    this.Text = Strings.PenWidth;

                    this.ResumeLayout();
                }
            }

            /// <summary>
            ///
            /// </summary>
            class WidthSelector : TrackBar {
                private readonly PenPropertiesPage Owner;
                private const int DEFAULT_MAX_WIDTH = 2000;

                public WidthSelector(PenPropertiesPage owner, Point location, Size size, int tabIndex) {
                    this.SuspendLayout();
                    this.Owner = owner;
                    owner.DrawingAttributesChanged += new EventHandler(this.OnDrawingAttributesChanged);

                    int width = (int)(this.Owner.DrawingAttributes.Width);
                    this.Location = location;
                    this.Size = size;
                    this.TabIndex = tabIndex;
                    this.Name = "WidthSelector";
                    this.LargeChange = 50;
                    this.Maximum = Math.Max(DEFAULT_MAX_WIDTH, width);
                    this.TickFrequency = this.Maximum / 8;
                    this.Value = width;

                    this.ResumeLayout();
                }

                protected virtual void OnDrawingAttributesChanged(object sender, EventArgs args) {
                    int width = (int)(this.Owner.DrawingAttributes.Width);
                    if (this.Maximum < width) {
                        this.Maximum = width;
                        this.TickFrequency = this.Maximum / 8;
                    }
                    if (this.Value != width)
                        this.Value = width;
                }

                protected override void OnValueChanged(EventArgs e) {
                    base.OnValueChanged(e);
                    this.Owner.DrawingAttributes.Width = this.Value;
                    this.Owner.FireDrawingAttributesChanged(this);
                }
            }

            /// <summary>
            ///
            /// </summary>
            class HeightSelectorLabel : Label {
                private readonly PenPropertiesPage Owner;

                public HeightSelectorLabel(PenPropertiesPage owner, Point location, Size size, int tabIndex) {
                    this.SuspendLayout();
                    this.Owner = owner;

                    this.FlatStyle = FlatStyle.System;
                    this.Location = location;
                    this.Size = size;
                    this.TabIndex = tabIndex;
                    this.Name = "HeightSelectorLabel";
                    this.Text = Strings.PenHeight;

                    this.ResumeLayout();
                }
            }

            /// <summary>
            ///
            /// </summary>
            class HeightSelector : TrackBar {
                private readonly PenPropertiesPage Owner;
                private const int DEFAULT_MAX_HEIGHT = 3000;

                public HeightSelector(PenPropertiesPage owner, Point location, Size size, int tabIndex) {
                    this.SuspendLayout();
                    this.Owner = owner;
                    owner.DrawingAttributesChanged += new EventHandler(this.OnDrawingAttributesChanged);

                    int height = (int)(this.Owner.DrawingAttributes.Height);
                    this.Location = location;
                    this.Size = size;
                    this.TabIndex = tabIndex;
                    this.Name = "HeightSelector";
                    this.LargeChange = 50;
                    this.Maximum = Math.Max(DEFAULT_MAX_HEIGHT, height);
                    this.TickFrequency = this.Maximum / 8;
                    this.Value = height;

                    this.ResumeLayout();
                }

                protected virtual void OnDrawingAttributesChanged(object sender, EventArgs args) {
                    int height = (int)(this.Owner.DrawingAttributes.Height);
                    if (this.Maximum < height) {
                        this.Maximum = height;
                        this.TickFrequency = this.Maximum / 8;
                    }
                    if(this.Value != height)
                        this.Value = height;
                    this.Enabled = (this.Owner.DrawingAttributes.PenTip == PenTip.Rectangle);
                }

                protected override void OnValueChanged(EventArgs e) {
                    base.OnValueChanged(e);
                    this.Owner.DrawingAttributes.Height = this.Value;
                    this.Owner.FireDrawingAttributesChanged(this);
                }
            }
        }

        #endregion

        #region ScribbleGroupBox

        class ScribbleGroupBox : GroupBox {
            private readonly PenPropertiesPage Owner;

            public ScribbleGroupBox(PenPropertiesPage owner, Point location, Size size, int tabIndex) {
                this.SuspendLayout();
                this.Owner = owner;

                this.FlatStyle = FlatStyle.System;
                this.Font = ViewerStateModel.StringFont;
                this.Location = location;
                this.Size = size;
                this.TabIndex = tabIndex;
                this.Name = "ScribbleGroupBox";
                this.TabStop = false;
                this.Text = Strings.ScribbleArea;
                this.Enabled = true;

                // Add the controls
                this.Controls.Add(new ScribblePanel(this.Owner, new Point(8, 16), new Size(128, 200), 0));

                this.ResumeLayout();
            }

            class ScribblePanel : Panel {
                private readonly PenPropertiesPage Owner;
                private readonly RealTimeStylus RealTimeStylus;
                private readonly DynamicRenderer DynamicRenderer;

                public ScribblePanel(PenPropertiesPage owner, Point location, Size size, int tabIndex) {
                    this.SuspendLayout();
                    this.Owner = owner;
                    owner.DrawingAttributesChanged += new EventHandler(this.OnDrawingAttributesChanged);

                    this.Location = location;
                    this.Size = size;
                    this.TabIndex = tabIndex;
                    this.Name = "ScribbleArea";
                    this.BackColor = Color.White;
                    this.BorderStyle = BorderStyle.FixedSingle;

                    // Add the controls
                    this.Controls.Add(new ScribbleClearButton(this, new Point(40, 168), new Size(80, 23), 0));

                    this.RealTimeStylus = new RealTimeStylus(this);
                    this.DynamicRenderer = new DynamicRenderer(this);
                    this.DynamicRenderer.Enabled = true;
                    //this.DynamicRenderer.EnableDataCache = true;
                    this.RealTimeStylus.SyncPluginCollection.Add(this.DynamicRenderer);
                    this.RealTimeStylus.Enabled = true;

                    this.ResumeLayout();
                }

                protected virtual void OnDrawingAttributesChanged(object sender, EventArgs args) {
                    this.DynamicRenderer.DrawingAttributes = this.Owner.DrawingAttributes.Clone();
                }

                class ScribbleClearButton : Button {
                    private readonly ScribblePanel Owner;

                    public ScribbleClearButton(ScribblePanel owner, Point location, Size size, int tabIndex) {
                        this.SuspendLayout();
                        this.Owner = owner;

                        this.Location = location;
                        this.Size = size;
                        this.TabIndex = tabIndex;
                        this.Name = "ScribbleClearButton";
                        this.Text = Strings.Clear;
                        this.BackColor = SystemColors.Control;
                        this.FlatStyle = FlatStyle.Popup;

                        this.ResumeLayout();
                    }

                    protected override void OnClick(EventArgs e) {
                        base.OnClick(e);
                        //this.Owner.DynamicRenderer.ReleaseCachedData();
                        this.Owner.Refresh();
                    }
                }
            }
        }

        #endregion

        #region UndoChangesButton

        class UndoChangesButton : Button {
            private readonly PenPropertiesPage Owner;

            private readonly float DefaultWidth;
            private readonly float DefaultHeight;
            private readonly Color DefaultColor;
            private readonly PenTip DefaultPenTip;
            private readonly byte DefaultTransparency;
            private readonly RasterOperation DefaultRasterOperation;

            public UndoChangesButton(PenPropertiesPage owner, Point location, Size size, int tabIndex) {
                this.DefaultWidth = owner.DrawingAttributes.Width;
                this.DefaultHeight = owner.DrawingAttributes.Height;
                this.DefaultColor = owner.DrawingAttributes.Color;
                this.DefaultPenTip = owner.DrawingAttributes.PenTip;
                this.DefaultTransparency = owner.DrawingAttributes.Transparency;
                this.DefaultRasterOperation = owner.DrawingAttributes.RasterOperation;

                this.SuspendLayout();
                this.Owner = owner;
                owner.DrawingAttributesChanged += new EventHandler(this.OnDrawingAttributesChanged);

                this.FlatStyle = FlatStyle.System;
                this.Font = ViewerStateModel.StringFont;
                this.Location = location;
                this.Size = size;
                this.TabIndex = tabIndex;
                this.Name = "UndoChangesButton";
                this.Text = Strings.UndoChanges;
                this.Enabled = true;

                this.ResumeLayout();
            }

            protected virtual void OnDrawingAttributesChanged(object sender, EventArgs args) {
                this.Enabled = false
                    || this.DefaultWidth != this.Owner.DrawingAttributes.Width
                    || this.DefaultHeight != this.Owner.DrawingAttributes.Height
                    || this.DefaultColor != this.Owner.DrawingAttributes.Color
                    || this.DefaultPenTip != this.Owner.DrawingAttributes.PenTip
                    || this.DefaultTransparency != this.Owner.DrawingAttributes.Transparency
                    || this.DefaultRasterOperation != this.Owner.DrawingAttributes.RasterOperation;
            }

            protected override void OnClick(EventArgs e) {
                base.OnClick(e);
                this.Owner.DrawingAttributes.Width = this.DefaultWidth;
                this.Owner.DrawingAttributes.Height = this.DefaultHeight;
                this.Owner.DrawingAttributes.Color = this.DefaultColor;
                this.Owner.DrawingAttributes.PenTip = this.DefaultPenTip;
                this.Owner.DrawingAttributes.Transparency = this.DefaultTransparency;
                this.Owner.DrawingAttributes.RasterOperation = this.DefaultRasterOperation;

                this.Owner.FireDrawingAttributesChanged(this);
            }
        }

        #endregion

        #region OKButton

        class OKButton : Button {
            private readonly PenPropertiesPage Owner;

            public OKButton(PenPropertiesPage owner, Point location, Size size, int tabIndex) {

                this.SuspendLayout();
                this.Owner = owner;

                this.FlatStyle = FlatStyle.System;
                this.Font = ViewerStateModel.StringFont;
                this.Location = location;
                this.Size = size;
                this.TabIndex = tabIndex;
                this.Name = "OKButton";
                this.Text = Strings.OK;
                this.Enabled = true;

                this.ResumeLayout();
            }

           

            protected override void OnClick(EventArgs e) {
                base.OnClick(e);
                this.Owner.Close();
                

            }
        }

        #endregion
    }
}
