
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Viewer.Slides;
using System.Drawing.Drawing2D;
using UW.ClassroomPresenter.Model.Viewer;
using UW.ClassroomPresenter.Model;
using System.IO;
using UW.ClassroomPresenter.Model.Stylus;
using UW.ClassroomPresenter.Viewer.TextAndImageEditing;




namespace UW.ClassroomPresenter.Viewer.Text
{
    public class TextItBox : PresentItBox
    {
        #region Controls within this control

        private TextItText textItText1;
        private Timer save_timer;

        #endregion

        private TextSheetModel text_sheet_;

        public static readonly Size default_size = new Size(150, 60);

        #region Transformation
        
        /// <summary>
        /// The size of the font relative to the slide.
        /// </summary>
        private Font slide_font_;

        #endregion

        #region Resize

        private bool within_resizing_margins_ = false;

        private bool within_x_margin_ = false;

        private bool within_y_margin_ = false;

        private bool resizing_ = false;

        private const int resizing_margin_ = 10;

        #endregion


        /// <summary>
        /// Constructor for loading from a TextSheetModel 
        /// </summary>
        /// <param name="sheet"></param>
        /// <param name="viewer"></param>
        public TextItBox(TextSheetModel sheet, MainSlideViewer viewer): base(sheet,viewer){
            ///Initialize Components
            InitializeComponent();
            text_sheet_ = sheet;
            actual_control_ = textItText1;
            using (Synchronizer.Lock(text_sheet_.SyncRoot)) {
                this.slide_font_ = text_sheet_.Font;
                this.textItText1.ForeColor = text_sheet_.Color;
                this.textItText1.LongestLineWidth = text_sheet_.LongestLineWidth;
                this.textItText1.WidthAccommodate = text_sheet_.WidthAccommodate;
                this.textItText1.Text = text_sheet_.Text;
            }

            ///we got slide coordinates from the textsheet, and now need to transform the control
            ///appropriately
            Transform();

            PaintSheet(true);
            save_timer.Start();
        }

        /// <summary>
        /// whenever we change the slide's size, we must rescale our textbox. This does just that.
        /// NOTE: the slide_coordinates give the coordinates of the actual TEXT, not the control. We
        /// need to ajust for this
        /// </summary>
        protected override void Transform() {
            ///get the amount we need to transform by
            Matrix transform = base.GetTransform();

            float scale_x = transform.Elements[0];
            float scale_y = transform.Elements[3];

            /// HACK: When Classroom Presenter is being minimized, the size of SlideViewer is 
            /// MinimumSize (16, 16), which cause the transform matrix unable to produce 
            /// precise transformation. This is a hack to check whether it is a minimize 
            /// operation, and don't do any transformation in case of minimizing.
            if (scale_x < 0.017)
                return;

            textItText1.NeedUpdate = false;
            textItText1.Font = new Font(slide_font_.Name, slide_font_.Size * scale_y, slide_font_.Style, slide_font_.Unit, slide_font_.GdiCharSet);
            textItText1.Size = new Size((int)(slide_size_.Width * scale_x - 9), (int)(slide_size_.Height * scale_y + 1));

            base.Transform();

            PaintSheet(true);
        }

        /// <summary>
        /// saves the current text that the textbox has to the sheet which it is attached to.
        /// Also saves editing information
        /// </summary>
        public override void SaveToSheet() {
            if (sheet_ == null) {
                return;
            }
            ///save information about the text
            using (Synchronizer.Lock(text_sheet_)) {
                text_sheet_.Font = slide_font_;
                text_sheet_.Color = textItText1.ForeColor;
                text_sheet_.WidthAccommodate = textItText1.WidthAccommodate;
                text_sheet_.LongestLineWidth = textItText1.LongestLineWidth;
                text_sheet_.Text = textItText1.Text;
            }

            ///get the amount we need to transform by
            Matrix transform = GetTransform();

            /// HACK: When Classroom Presenter is being minimized, the size of SlideViewer is 
            /// MinimumSize (16, 16), which cause the transform matrix unable to produce 
            /// precise transformation. This is a hack to check whether it is a minimize 
            /// operation, and don't do any transformation in case of minimizing.
            if (transform.Elements[0] < 0.017)
                return;

            transform.Invert();

            float scale_x = transform.Elements[0];
            float scale_y = transform.Elements[3];

            ///save slide size information by looking at the actual control (ie
            ///either image or text) size.
            slide_size_ = new Size((int)((actual_control_.Width + 10) * scale_x), (int)(actual_control_.Height * scale_y));
            ///save this to the sheet.
            using (Synchronizer.Lock(sheet_)) {
                Point p = new Point(sheet_.Bounds.X, sheet_.Bounds.Y);
                sheet_.Bounds = new Rectangle(p, this.slide_size_);
            }
        }

        protected override void PaintSheet(bool painting) {
            text_sheet_.Visible = painting;
        }
        /// <summary>
        /// saves the current text to the sheet. Used for realtime sending of text when the instructor hasn't
        /// changed anything else).
        /// </summary>
        public void SaveTextToSheet() {
            using (Synchronizer.Lock(text_sheet_.SyncRoot)) {
                text_sheet_.Text = textItText1.Text;
            }
        }

        public void SaveLongestWidthToSheet() {
            using (Synchronizer.Lock(text_sheet_.SyncRoot)) {
                text_sheet_.LongestLineWidth = textItText1.LongestLineWidth;
            }
        }

        /// <summary>
        /// used for changing the font color of the textitbox
        /// </summary>
        /// <param name="c"></param>
        public void ChangeColor(Color c) {
            using (Synchronizer.Lock(text_sheet_.SyncRoot)) {
                text_sheet_.Color = c;
            }
            textItText1.ForeColor = c;
        }

        #region Overrides

        /// <summary>
        /// When we set the font, we also want to save these changes, and to transform the new font so
        /// that it will look scaled on the viewer.
        /// </summary>
        public override Font Font {
            get {
                return slide_font_;
            }
            set {
                slide_font_ = value;
                ///set the fault of the textstylus to the new font.
                using (Synchronizer.Lock(TextStylusModel.GetInstance().SyncRoot)) {
                    TextStylusModel.GetInstance().Font = value;
                }

                Matrix transform = GetTransform();
                textItText1.Font = new Font(slide_font_.Name, slide_font_.Size * transform.Elements[3], slide_font_.Style, slide_font_.Unit, slide_font_.GdiCharSet);
                SaveToSheet();
            }
        }


        #region Resize

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            resizing_ = within_resizing_margins_;
        }


        /// <summary>
        /// This method either
        /// a) decides whether the mouse is within the resizing margins, and changes the cursor
        /// appropriately
        /// or
        /// b) if we are resizing, resizes this control.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!resizing_)
            {
                ///we are not resizing, so we need to check whether we 
                ///are within the resizing margins.
                within_resizing_margins_ = true;
                within_x_margin_ = e.X >= this.Width - resizing_margin_;
                within_y_margin_ = e.Y >= this.Height - resizing_margin_;

                ///if we are within the resizing margins, change the cursor 
                ///appropriately
                if (within_x_margin_ && within_y_margin_)
                    this.Cursor = Cursors.SizeNWSE;
                else if (within_x_margin_)
                    this.Cursor = Cursors.SizeWE;
                else if (within_y_margin_)
                    this.Cursor = Cursors.SizeNS;
                else
                {
                    ///we are not within the resizing margins.
                    within_resizing_margins_ = false;
                    this.Cursor = Cursors.Arrow;
                }
            }
            else
            {
                ///we are resizing, and need to change the size
                ///according to how we are resizing. This is dictated by 
                ///the cursors.
                int height = ((MainSlideViewer)this.Parent).SlideHeight - this.Location.Y;
                int width = ((MainSlideViewer)this.Parent).SlideWidth - this.Location.X - 10;

                if (within_y_margin_)
                {
                    if (e.Y > height)
                        this.Height = height;
                    else
                        this.Height = e.Y;
                }
                if (within_x_margin_)
                {
                    if (e.X > width)
                        this.Width = width;
                    else
                        this.Width = e.X;
                }
            }
        }

        /// <summary>
        /// If we are resizing, this method toggles the resizing button,
        /// and sets the control's size.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (resizing_)
            {
                resizing_ = false;
                actual_control_.Size = new Size(this.Width, this.Height - pnlTop.Height);
                textItText1.LongestLineWidth = actual_control_.Width - 20;
                SaveToSheet();
            }
        }

        #endregion

        #endregion

        #region Events
        /// <summary>
        /// Save the sheet every few seconds in order to make sure it's the same.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void timer_Tick(object sender, EventArgs e) {
            SaveToSheet();
        }
 
        /// <summary>
        /// brings the control on top
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void textItText1_MouseDown(object sender, MouseEventArgs e)
        {
            this.BringToFront();
        }

        #endregion


        #region Designer Stuff
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            //dispose all the added controls manually.
            save_timer.Dispose();
            timer.Dispose();
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.textItText1 = new TextItText(new Point(0,(int)(this.pnlTop.Height)), this);
            this.save_timer = new Timer();
            this.SuspendLayout();
            //
            // Timer
            //
            save_timer.Interval = 3000;
            save_timer.Tick += new EventHandler(timer_Tick);
            // 
            // textItText1
            // 
            this.textItText1.AcceptsReturn = true;
            this.textItText1.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textItText1.Multiline = true;
            this.textItText1.Name = "textItText1";
            this.textItText1.Size = new System.Drawing.Size(147, 30);
            this.textItText1.TabIndex = 0;
            this.textItText1.Font = TextStylusModel.GetInstance().DefaultFont;
            this.textItText1.MouseDown += new System.Windows.Forms.MouseEventHandler(this.textItText1_MouseDown);
            
            // 
            // TextItBox
            // 
            this.Controls.Add(this.textItText1);
            this.Name = "TextItBox";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        #endregion
    }
}

