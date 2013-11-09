using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;
using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Stylus;
using UW.ClassroomPresenter.Viewer.TextAndImageEditing;

namespace UW.ClassroomPresenter.Viewer.Text
{
    public partial class TextItText : TextBox
    {
        #region Variables
        /// <summary>
        /// Right hand margin in the text box, leaves room so that people can sort of see
        /// where they are typing. It's also more aesthetically pleasing.
        /// </summary>
        private const int kXMargin_ = 20;
        /// <summary>
        /// The smallest width the textbox can have
        /// </summary>
        private const int kMinWidth_ = 150;
        /// <summary>
        /// The smallest height the textbox can have
        /// </summary>
        private const int kMinHeight_ = 60;

        /// <summary>
        /// keeps track of width of longest line
        /// </summary>
        private int longest_line_width_;
        /// <summary>
        /// index in Text.Lines of longest line
        /// used for finding the longest line quickly
        /// </summary>
        private int index_of_longest_line_;

        private readonly EventQueue.PropertyEventDispatcher m_LightColorListener;
        /// <summary>
        /// is a update needed for longest line
        /// </summary>
        private bool update_longest_line_ = false;
        /// <summary>
        /// should the with of text box accommodate to typing
        /// </summary>
        private bool width_accommodate_ = false;

        #endregion

        #region Constructors

        /// <summary>
        /// Constructs a new TextItBox at the specified point.
        /// </summary>
        /// <param name="p"></param>
        public TextItText(Point p, TextItBox par)
        {
            InitializeComponent();
            this.Location = p;
            this.BorderStyle = BorderStyle.None;
            this.MaxLength = 2000;
            this.Parent = par;

            //Attatch property listeners to the LightColor NATALIE - had to make viewer and presenter model public
            this.m_LightColorListener = new EventQueue.PropertyEventDispatcher(new ControlEventQueue(this)/*NATALIEthis.m_EventQueue*/,
                new PropertyEventHandler(this.OnLightColorChanged));
            using (Synchronizer.Lock(((PresentItBox)(par)).viewer_.presenter_model_.SyncRoot)) {
                using (Synchronizer.Lock(((PresentItBox)(par)).viewer_.presenter_model_.ViewerState.SyncRoot)) {
                    ((PresentItBox)(par)).viewer_.presenter_model_.ViewerState.Changed["UseLightColorSet"].Add(this.m_LightColorListener.Dispatcher);
                }
            }
            this.m_LightColorListener.Dispatcher(this, null);

            this.Focus();
        }

        #endregion

        private void OnLightColorChanged(object sender, PropertyEventArgs args) {
            bool lightcolor = false;

            using (Synchronizer.Lock(((PresentItBox)(this.Parent)).viewer_.presenter_model_.ViewerState.SyncRoot)) {
                lightcolor = ((PresentItBox)(this.Parent)).viewer_.presenter_model_.ViewerState.UseLightColorSet;
            }

            if (lightcolor == false) {
                this.BackColor = Color.White;
                this.Parent.BackColor = Color.White;
            }
            else {
                this.BackColor = Color.DarkGray;
                this.Parent.BackColor = Color.DarkGray;
            }
        }

        #region Overrides

        /// <summary>
        /// when we change text, we need to check if we need to change the dimensions of the box.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            ///change size of the TextItBox to reflect the change to the textut
            TextItBox parent = (TextItBox)this.Parent;
            if (parent == null)
                return;

            ///update the width of text box when first typing
            UpdateWidth();
            ///update the height of text box when typing and font changing
            UpdateHeight();

            parent.SaveTextToSheet();
        }

        /// <summary>
        /// when we change font, we need to again trim our textbox to fit the longest line (which might be different
        /// now that we've added font).
        /// </summary>
        /// <param name="e"></param>
        protected override void OnFontChanged(EventArgs e) {
            base.OnFontChanged(e);
            if (update_longest_line_)
                OnTextChanged(e);
            else
                update_longest_line_ = true;
        }


        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            TextItBox parent = (TextItBox)this.Parent;
            parent.BringToFront();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            if (width_accommodate_)
            {
                this.width_accommodate_ = false;
            }
        }

        #endregion

        #region Properties

        public bool WidthAccommodate {
            get { return this.width_accommodate_; }
            set { this.width_accommodate_ = value; }
        }

        public bool NeedUpdate {
            get { return this.update_longest_line_; }
            set { this.update_longest_line_ = value; }
        }

        public int LongestLineWidth {
            get { return this.longest_line_width_; }
            set { this.longest_line_width_ = value; }
        }

        #endregion

        #region Private Helper

        /// <summary>
        /// updates longest_line_width_ to show the longest line width by iterating through
        /// all of the lines and finding the maximum. It is inefficient, but is also the safest method, and allows
        /// for the smallest amount of books. Since we won't be writing epics here, efficiency isn't incredibly important 
        /// anyway.
        /// </summary>
        private void UpdateLongestLine()
        {
            ///change size of the TextItBox to reflect the change to the textut
            TextItBox parent = (TextItBox)this.Parent;
            if (parent == null)
                return;

            int limit = parent.viewer_.SlideWidth - kXMargin_ - parent.Location.X;

            for (int i = 0; i < this.Lines.Length; i++)
            {
                int width = TextRenderer.MeasureText(this.Lines[i], this.Font).Width;
                if (width > longest_line_width_)
                {
                    if (width < limit)
                        longest_line_width_ = width;
                    else
                        longest_line_width_ = limit;
                    index_of_longest_line_ = i;
                }
            }
            parent.SaveLongestWidthToSheet();
        }

        private void UpdateWidth()
        {
            ///change size of the TextItBox to reflect the change to the textut
            TextItBox parent = (TextItBox)this.Parent;
            if (parent == null)
                return;

            if (width_accommodate_)
            {
                ///get the new length of the longest line.
                UpdateLongestLine();
                ///the largest line width in the textbox
                int largest_width = longest_line_width_ + kXMargin_;

                if (largest_width > this.Width)
                {
                    this.Width = largest_width;
                    parent.Width = this.Width;
                }
            }
        }

        private void UpdateHeight()
        {
            ///change size of the TextItBox to reflect the change to the textut
            TextItBox parent = (TextItBox)this.Parent;
            if (parent == null)
                return;
            
            /// longest_line_with_ should be a positive value.
            if (longest_line_width_ <= 0)
                return;

            ///adapt height of box
            int new_height = this.FontHeight * (this.Lines.Length + 1);
            // deal with word wrap
            int si = 0, sp = 0, nsp = 0, width = 0, w = 0;
            for (int i = 0; i < this.Lines.Length; i++)
            {
                while (si < this.Lines[i].Length && (nsp = this.Lines[i].IndexOf(" ", si)) != -1)
                {
                    while (width > longest_line_width_)
                    {
                        width -= longest_line_width_;
                        new_height += this.FontHeight;
                    }
                    w = TextRenderer.MeasureText(this.Lines[i].Substring(si, nsp - si + 1), this.Font).Width;
                    if (width + w > longest_line_width_)
                    {
                        new_height += this.FontHeight;
                        width = w;
                        sp = nsp + 1;
                    }
                    else
                    {
                        width = TextRenderer.MeasureText(this.Lines[i].Substring(sp, nsp - sp + 1), this.Font).Width;
                    }

                    si = nsp + 1;
                }
                if (sp < this.Lines[i].Length)
                {
                    width = TextRenderer.MeasureText(this.Lines[i].Substring(sp), this.Font).Width;
                    while (width > longest_line_width_)
                    {
                        width -= longest_line_width_;
                        new_height += this.FontHeight;
                    }
                }
            }

            if (new_height > this.Height)
            {
                this.Height = new_height + 20;
                parent.Height = this.Height;
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
            if (disposing && (components != null))
            {
                components.Dispose();

                using (Synchronizer.Lock(((PresentItBox)(this.Parent)).viewer_.presenter_model_.ViewerState.SyncRoot)) {
                    ((PresentItBox)(this.Parent)).viewer_.presenter_model_.ViewerState.Changed["UseLightColorSet"].Remove(this.m_LightColorListener.Dispatcher);
                }


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
            this.SuspendLayout();
            // 
            // TextItBox
            // 
            this.AcceptsReturn = true;
            this.AcceptsTab = false;
            this.Multiline = true;
            this.ResumeLayout(false);
            this.AutoSize = true;
            this.Font = TextStylusModel.GetInstance().DefaultFont;

        }

        #endregion
        #endregion

    }
}
