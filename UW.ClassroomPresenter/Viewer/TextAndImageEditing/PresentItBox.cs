using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;
using UW.ClassroomPresenter.Viewer.Slides;
using System.Drawing.Drawing2D;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Model.Viewer;
using UW.ClassroomPresenter.Model;

namespace UW.ClassroomPresenter.Viewer.TextAndImageEditing {
    public class PresentItBox : UserControl {
        #region Controls within this control
        protected Panel pnlTop;
        protected Button button1;
        protected Timer timer;
        /// <summary>
        /// the main control which this class wraps
        /// namely a text or image view.
        /// </summary>
        protected Control actual_control_;
        #endregion

        #region Data
        /// <summary>
        /// when we click and drag to change location, there is an offset 
        /// from where we click and where we want the box to move.
        /// to get the right behavior, we need to consider this offset
        /// </summary>
        protected Point offset;

        /// <summary>
        /// We need this in order to be able to correctly transform the textbox
        /// in its different modes.
        /// </summary>
        public SlideDisplayModel slide_display_;

        /// <summary>
        /// keeps track of the slide, particularly the annotationsheets
        /// </summary>
        protected SlideModel slide_;

        /// <summary>
        /// keeps track of our current sheet
        /// </summary>
        protected SheetModel sheet_;
        #endregion

        #region Transformation
        protected bool transforming_;
        ///The following code is needed in order to appropriately handle transformation.
        /// NOTE: 'relative to the slide' can also be interpreted as 'before transformation'
        /// <summary>
        /// the coordinates of the TEXT (not the control) relative to the slide.
        /// </summary>
        protected Point slide_coordinates_;
        /// <summary>
        /// The size of the textbox relative to the slide. 
        /// </summary>
        protected Size slide_size_;

        public MainSlideViewer viewer_;

        ///The following code is for handling locking
        /// <summary>
        /// Listener for the SlideDisplay.PixelTransform property.
        /// </summary>
        private readonly EventQueue.PropertyEventDispatcher pixel_transform_listener_;
        #endregion

        public PresentItBox(SheetModel sheet, MainSlideViewer viewer) {
            InitializeComponent();
            slide_display_ = viewer.SlideDisplay;
            slide_ = viewer.Slide;
            viewer_ = viewer;
            sheet_ = sheet;

            ///add event listeners
            ///we need to change how we transform our textbox every time the slidedisplay transform changes.
            pixel_transform_listener_ = new EventQueue.PropertyEventDispatcher(slide_display_.EventQueue,
                new PropertyEventHandler(this.HandleTransformChanged));
            ///add the dispatcher, not the actual method we want to execute
            slide_display_.Changed["PixelTransform"].Add(pixel_transform_listener_.Dispatcher);

            ///set the location and size of our controls relative to the slide by 
            ///using the sheet information.
            using (Synchronizer.Lock(sheet_.SyncRoot)) {
                slide_coordinates_ = new Point(sheet.Bounds.Location.X, sheet.Bounds.Location.Y);
                slide_size_ = sheet.Bounds.Size;
            }
        }


        #region Transformation
        protected virtual void HandleTransformChanged(object o, PropertyEventArgs args) {
            if (!timer.Enabled) {
                ///start the timer which will periodically update our transformation
                timer.Start();
            }
            transforming_ = true;
        }

        /// <summary>
        /// whenever we change the slide's size, we must rescale our control. This does just that.
        /// NOTE: the slide_coordinates give the coordinates relative to the slide, not the user. We
        /// need to ajust for this
        /// </summary>
        protected virtual void Transform() {

            ///get the amount we need to transform by
            Matrix transform = GetTransform();
            float usex = transform.Elements[0];
            float usey = transform.Elements[3];

            ///Change the size of the panel to reflect the transformation
            pnlTop.Height = (int)(/*Natalie20 10*/ 10* usey);
            ///change the location of the control to reflect the transformation
            ///adjusting for the panel height.
            this.Location = new Point((int)(slide_coordinates_.X * usex), (int)(slide_coordinates_.Y * usey - pnlTop.Height));
            ///update the control's size by looking at the slide_size and adjusting for height
            this.Size = new Size((int)(slide_size_.Width * usex), (int)(slide_size_.Height * usey));
            this.Height += pnlTop.Height;

            ///repaint our sheet
            PaintSheet(true);
        }

        /// <summary>
        /// scales bounds information to "slide" proportions, or to "transformed" proportions.
        /// </summary>
        /// <param name="to_scale">number you want to scale</param>
        /// <param name="inverted">true means you want slide proportions, false otherwise</param>
        /// <param name="is_x_axis">whether we are scaling alont the x axis or y axis</param>
        /// <returns></returns>
        protected int Scale(int to_scale, bool inverted, bool is_x_axis) {
            ///get the amount we need to transform by
            Matrix transform = GetTransform();
            if (inverted) {
                transform.Invert();
            }
           
            float usex = transform.Elements[0];
            float usey = transform.Elements[3];
            
            float scale;
            if (is_x_axis) {
                scale = usex;//transform.Elements[0];
            } else {
                scale = usey;//transform.Elements[3];
            }
            return (int)(to_scale * scale);
        }

        /// <summary>
        /// gets the transformation matrix of the slide. This is used
        /// quite often, and so is in a seperate method.
        /// </summary>
        /// <param name="p"></param>
        protected Matrix GetTransform() {
            Matrix transform;
            using (Synchronizer.Lock(slide_display_.SyncRoot)) {
                transform = slide_display_.PixelTransform.Clone();
            }

            return transform;
        }

        #endregion

        #region Sheet Stuff
        /// <summary>
        /// saves the control's current data to the sheet it's attached to.
        /// </summary>
        public virtual void SaveToSheet() {
            if (sheet_ == null) {
                return;
            }

            ///save slide size information by looking at the actual control (ie
            ///either image or text) size.
            slide_size_ = actual_control_.Size;

            ///save this to the sheet.
            using (Synchronizer.Lock(sheet_)) {
                Point p = new Point(sheet_.Bounds.X, sheet_.Bounds.Y);
                sheet_.Bounds = new Rectangle(p, this.slide_size_);
            }

        }

        /// <summary>
        /// In inherited members, this method essentially changes
        /// the visibility property of a sheet, depending on painting.
        /// if Painting is true, visibility is true.
        /// </summary>
        /// <param name="painting"></param>
        protected virtual void PaintSheet(bool painting) {

        }
        #endregion

        /// <summary>
        /// When we lose focus we need to refocus the entire parent form
        /// (the viewer). This does not happen automatically, for some reason.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnLostFocus(EventArgs e) {
            base.OnLostFocus(e);
            if (!actual_control_.Focused) {
                MainSlideViewer viewer = (MainSlideViewer)this.Parent;
                if (viewer != null) {
                    viewer.Focus();
                }
            }
        }

        /// <summary>
        /// when we change the Location, we need to save our changes so they reflect in the realtime sending 
        /// of
        /// 
        /// </summary>
        /// <param name="e"></param>

        protected override void OnLocationChanged(EventArgs e) {
            base.OnLocationChanged(e);
            PaintSheet(false);
        }

        #region Events
        /// <summary>
        /// Closes the textbox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button1_Click(object sender, EventArgs e) {
            Exit();
        }

        /// <summary>
        /// the user has indicates thathe no longer wants this textbox, so we must
        /// remove the corresponding textsheet from our sheets.
        /// </summary>
        private void Exit() {
            ///remove the textsheet.
            using (Synchronizer.Lock(slide_.SyncRoot)) {
                slide_.AnnotationSheets.Remove(sheet_);
            }

            this.Dispose();
        }


        /// <summary>
        /// brings the control on top, toggles mouse is down variable
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void pnlTop_MouseDown(object sender, MouseEventArgs e) {
            offset = e.Location;
        }


        /// <summary>
        /// Usually this happens when we've finished dragging our textitbox, so 
        /// we need to save its location.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void pnlTop_MouseUp(object sender, MouseEventArgs e) {
            ///update our slide coordinates
            Matrix i_transform = GetTransform();
            i_transform.Invert();

            slide_coordinates_ = new Point((int)(Location.X * i_transform.Elements[0]), (int)((Location.Y + actual_control_.Location.Y) * i_transform.Elements[3]));

            using (Synchronizer.Lock(viewer_.Slide.SyncRoot)) {
                viewer_.Slide.AnnotationSheets.MoveToTop(sheet_);
            }
            
            ///don't actually change the slide coordinates, because this will cause the control to shift incorectly.
            Point p = new Point((int)(Location.X * i_transform.Elements[0]), (int)((Location.Y + actual_control_.Location.Y) * i_transform.Elements[3]));

            ///save this to the sheet.  This is very similar to what is done in SaveToSheet; the difference is that we need 
            ///to change the sheet bounds' point.
            using (Synchronizer.Lock(sheet_)) {
                sheet_.Bounds = new Rectangle(p, this.slide_size_);
            }

            PaintSheet(true);
        }

        /// <summary>
        /// lets us drag our textitbox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void pnlTop_MouseMove(object sender, MouseEventArgs e) {
            ///get the location of the mouse relative to the mainslideviewer
            Point mouse_location = new Point(this.Location.X + e.Location.X - offset.X,
                this.Location.Y + e.Location.Y - offset.Y);
            if (!(e.Button == MouseButtons.None)) {
                int slide_width = ((MainSlideViewer)this.Parent).SlideWidth - 10;
                int slide_height = ((MainSlideViewer)this.Parent).SlideHeight;
                ///set the new location of this control based on the mouse location,
                ///and whether or not we are out of bounds
                Point new_location = this.Location;
                if(mouse_location.X + this.Width >= 10 && mouse_location.X <= (slide_width - 10)){
                    ///we are within the X-bounds
                    new_location.X = mouse_location.X;
                }
                if(mouse_location.Y >= 0 && mouse_location.Y <= (slide_height - 10)){
                    ///we are within the Y-bounds
                    new_location.Y = mouse_location.Y;
                }
                this.Location = new_location;
            }
        }

        void timer_Tick(object sender, EventArgs e) {
            if (transforming_) {
                transforming_ = !transforming_;
                Transform();
            } else {
                timer.Stop();
            }
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
            ///remove our event listeners
            SaveToSheet();
            slide_display_.Changed["PixelTransform"].Remove(this.pixel_transform_listener_.Dispatcher);

            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.pnlTop = new System.Windows.Forms.Panel();
            this.button1 = new System.Windows.Forms.Button();
            this.timer = new Timer();
            this.pnlTop.SuspendLayout();
            this.SuspendLayout();
            
            //
            //timer
            //
            this.timer.Interval = 50;
            this.timer.Tick += new EventHandler(timer_Tick);
            // 
            // pnlTop
            // 
            this.pnlTop.BackColor = System.Drawing.SystemColors.ScrollBar;
            this.pnlTop.Controls.Add(this.button1);
            this.pnlTop.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlTop.Location = new System.Drawing.Point(0, 0);
            this.pnlTop.Name = "pnlTop";
            this.pnlTop.Size = new System.Drawing.Size(150, /*20*/10);//NATALIE
            this.pnlTop.TabIndex = 1;
            this.pnlTop.Cursor = Cursors.Arrow;
            this.pnlTop.MouseDown += new System.Windows.Forms.MouseEventHandler(this.pnlTop_MouseDown);
            this.pnlTop.MouseUp += new System.Windows.Forms.MouseEventHandler(this.pnlTop_MouseUp);
            this.pnlTop.MouseMove += new System.Windows.Forms.MouseEventHandler(this.pnlTop_MouseMove);
            // 
            // button1
            // 
            this.button1.Dock = System.Windows.Forms.DockStyle.Right;
            this.button1.FlatAppearance.BorderSize = 0;
            this.button1.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.button1.Location = new System.Drawing.Point(100, 0);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(10, 10);
            this.button1.Image = UW.ClassroomPresenter.Properties.Resources.x;
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Cursor = Cursors.Arrow;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // PresentItBox
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BorderStyle = BorderStyle.FixedSingle;
            this.BackColor = Color.White;
            
            this.Controls.Add(this.pnlTop);
            this.Size = new System.Drawing.Size(150, 60);
            this.pnlTop.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }
        #endregion
    }
}
