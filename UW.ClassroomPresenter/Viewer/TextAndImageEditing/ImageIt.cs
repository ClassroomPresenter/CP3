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

namespace UW.ClassroomPresenter.Viewer.TextAndImageEditing {
    
    public partial class ImageIt : PresentItBox {
        private ImageSheetModel image_sheet_;

        #region Resizing
        /// <summary>
        /// whether or not we are resizing
        /// </summary>
        private bool resizing_ = false;
        /// <summary>
        /// whether or not we are within the resizing margins.
        /// </summary>
        private bool within_resizing_margins_ = false;
        private bool within_x_margin_ = false;
        private bool within_y_margin_ = false;
        /// <summary>
        /// how close you need to be to the edge of a control in order to 
        /// resize it
        /// </summary>
        private const int resizing_margin_ = 8;

        private float image_ratio_;

        #endregion

        /// <summary>
        /// pre: image_sheet is an image that is to be on the slide (not a background image),
        /// and therefore it's .Image property is not null
        /// </summary>
        /// <param name="image_sheet"></param>
        /// <param name="viewer"></param>
        /// <param name="image"></param>
        public ImageIt(ImageSheetModel image_sheet, MainSlideViewer viewer): base(image_sheet, viewer){
            InitializeComponent();

            this.Parent = viewer;

            //HACK: I'm adding a control that doesn't exist on the ImageIt
            //because imageit has no control
            Control c = new Control();
            actual_control_ = c;

            using (Synchronizer.Lock(image_sheet.SyncRoot)) {
                image_sheet_ = image_sheet;
                c.Size = image_sheet.Bounds.Size;
            }
            image_ratio_ = (float)(c.Width) / (float)(c.Height);

            Transform();
            c.Location = new Point(0, pnlTop.Height);
            PaintSheet(true);
        }

        #region Resizing

        /// <summary>
        /// This method either
        /// a) decides whether the mouse is within the resizing margins, and changes the cursor
        /// appropriately
        /// or
        /// b) if we are resizing, resizes this control.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnMouseMove(MouseEventArgs e) {
            base.OnMouseMove(e);
            if (!resizing_) {
                ///we are not resizing, so we need to check whether we 
                ///are within the resizing margins.
                within_resizing_margins_ = true;
                within_x_margin_ = e.X >= this.Width - resizing_margin_;
                within_y_margin_ = e.Y >= this.Height - resizing_margin_;
                
                ///if we are within the resizing margins, change the cursor 
                ///appropriately
                if (within_x_margin_ && within_y_margin_) {
                    this.Cursor = Cursors.SizeNWSE;
                } else {
                    ///we are not within the resizing margins.
                    within_resizing_margins_ = false;
                    this.Cursor = Cursors.Arrow;
                }
                 
            } else {
                ///we are resizing, and need to change the size
                ///according to how we are resizing. This is dictated by 
                ///the cursors.
                int height = ((MainSlideViewer)this.Parent).SlideHeight - this.Location.Y;
                int width = ((MainSlideViewer)this.Parent).SlideWidth - this.Location.X - 10;

                if (within_y_margin_ && within_x_margin_) {
                    int w, h;
                    if (e.Y > height)
                        h = height;
                    else
                        h = e.Y;
                    if (e.X > width)
                        w = width;
                    else
                        w = e.X;
                    if (w > h * image_ratio_)
                        this.Size = new Size((int)((h - pnlTop.Height) * image_ratio_), h);
                    else
                        this.Size = new Size(w, (int)(w / image_ratio_) + pnlTop.Height);
                }
            }
        }

        /// <summary>
        /// This method sets whether or not we are resizing based on 
        /// whether we are within the resizing margins
        /// </summary>
        /// <param name="e"></param>
        protected override void OnMouseDown(MouseEventArgs e) {
            base.OnMouseDown(e);
            if (within_resizing_margins_) {
                resizing_ = true;
            } else {
                resizing_ = false;
            }
        }

        /// <summary>
        /// If we are resizing, this method toggles the resizing button,
        /// and sets the image size to be whatever this control's size is.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnMouseUp(MouseEventArgs e) {
            base.OnMouseUp(e);
            if (resizing_) {
                resizing_ = false;
                SetImageSize(new Size(this.Width, this.Height - pnlTop.Height));
            }
        }

        /// <summary>
        /// This method sets and saves image_ so that relative
        /// to the user it will be the size given.
        /// </summary>
        /// <param name="s">the size the new image will be relative to the user.</param>
        private void SetImageSize(Size s) {
            ///since the image will be transformed, we first need to inversely
            ///transform its size
            int width = Scale(s.Width, true, true);
            int height = Scale(s.Height, true, false);
            ///save new bounds to sheet and control
            this.actual_control_.Size = new Size(width, height);
            this.SaveToSheet();
            using (Synchronizer.Lock(image_sheet_.SyncRoot)) {
                image_sheet_.Bounds = new Rectangle(image_sheet_.Bounds.Location, new Size(width, height));
            }
            ///repaint our new image!
            this.Invalidate();
        }

        #endregion

        protected override void HandleTransformChanged(object o, UW.ClassroomPresenter.Model.PropertyEventArgs args) {
            this.Invalidate();
            base.HandleTransformChanged(o, args);
        }

        /// <summary>
        /// Paints the image we have, taking note of our current transformation
        /// </summary>
        /// <param name="e"></param>
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e);
            using (Synchronizer.Lock(viewer_.SlideDisplay.SyncRoot)) {
                Graphics g = e.Graphics;
                /// we need to draw the image below the panel
                int panel_height = this.Scale(pnlTop.Height, true, false);
                ///Lock the graphics object so it doesn't get used elsewhere.
                using (Synchronizer.Lock(g)) {
                    g.Transform = viewer_.SlideDisplay.PixelTransform;
                    using (Synchronizer.Lock(image_sheet_.SyncRoot)) {
                        using (Synchronizer.Lock(image_sheet_.Image)) {
                            g.DrawImage(image_sheet_.Image, -4, panel_height - 3, this.actual_control_.Width, this.actual_control_.Height);
                        }
                    }
                }
            }
        }

        protected override void PaintSheet(bool painting) {
            image_sheet_.Visible = painting;
        }

        #region Designer Stuff
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

        
        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            this.SuspendLayout();

            // 
            // TextItBox
            // 
            this.Name = "ImageIt";
            this.ResumeLayout(false);
            this.PerformLayout();

        }
        #endregion
        #endregion
    }
}
