// $Id: TextSheetModel.cs 1865 2009-05-19 23:09:01Z anderson $

using System;
using System.Drawing;
using UW.ClassroomPresenter.Viewer.Text;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using UW.ClassroomPresenter.Model.Stylus;

namespace UW.ClassroomPresenter.Model.Presentation {
    [Serializable]
    public class TextSheetModel : EditableSheetModel
    {
        #region Published Properties

        private Font font_;
        /// <summary>
        /// The text of the box
        /// </summary>
        private string text_;

        /// <summary>
        /// whether we can send this over the network
        /// </summary>
        private bool public_;
        /// <summary>
        /// color of the text
        /// </summary>
        private Color color_;

        #endregion

        #region Other Properties

        private bool visible_ = true;

        private bool width_accommodate_ = true;

        private int longest_line_width_ = 150;

        #endregion

        #region Constructors

        /// <summary>
        /// constructs a default textsheet at the origin, which is by default editable
        /// </summary>
        /// <param name="id"></param>
        /// <param name="bounds"></param>
        public TextSheetModel(Guid id, SheetDisposition disp, Color color) : this(id, null, disp, true,true, new Rectangle(), color, TextStylusModel.GetInstance().DefaultFont) {}

        /// <summary>
        /// Constructs a default textsheetmodel which has been scaled to fit the current transform
        /// </summary>
        /// <param name="id"></param>
        /// <param name="text"></param>
        /// <param name="editable"></param>
        /// <param name="is_public"></param>
        /// <param name="c"></param>
        /// <param name="untransformed_bounds"></param>
        /// <param name="scale_x"></param>
        /// <param name="scale_y"></param>
        public TextSheetModel(Guid id, string text, SheetDisposition disp, bool editable, bool is_public, Color c, Rectangle untransformed_bounds, 
            float i_scale_x, float i_scale_y ): base(Guid.NewGuid(), editable, 0) {
            
            Rectangle new_bounds = new Rectangle((int)(untransformed_bounds.X * i_scale_x),(int)( untransformed_bounds.Y * i_scale_y), 
                (int)(untransformed_bounds.Width * i_scale_x), (int)(untransformed_bounds.Height * i_scale_y));
            using (Synchronizer.Lock(this.SyncRoot)) {
                this.Bounds = new_bounds;
            }
            text_ = text;
            public_ = is_public;
            color_ = c;
            font_ = new Font(TextStylusModel.GetInstance().DefaultFont.Name, (int)(TextStylusModel.GetInstance().DefaultFont.Size));
            
        }

        public override object Clone() {
            Rectangle bounds;
            using(Synchronizer.Lock(this.SyncRoot)){
                bounds = this.Bounds;
            }
            return new TextSheetModel(Guid.NewGuid(), text_, this.Disposition, is_editable_, public_, bounds, color_, font_);
        }

        public override object CloneToRemote() {
            Rectangle bounds;
            using (Synchronizer.Lock(this.SyncRoot)) {
                bounds = this.Bounds;
            }
            TextSheetModel tsm = new TextSheetModel(Guid.NewGuid(), text_, this.Disposition | SheetDisposition.Remote, is_editable_, public_, bounds, color_, font_);
            tsm.is_editable_ = false;
            return tsm;
        }


        /// <summary>
        /// Constructs a TextSheetModel from a message
        /// </summary>
        /// <param name="id"></param>
        /// <param name="text"></param>
        /// <param name="editable"></param>
        /// <param name="is_public"></param>
        /// <param name="bounds"></param>
        /// <param name="color"></param>
        public TextSheetModel(Guid id, string text, SheetDisposition disp, bool editable, bool is_public, Rectangle bounds, Color color, Font font) : base(id, disp, bounds, editable, 0) {
            text_ = text;
            is_editable_ = editable;
            font_ = font;
            color_ = color;
            public_ = is_public;
            
        }

        #endregion

        #region Publishing

        [Published] public string Text {
            get { return this.GetPublishedProperty("Text", ref this.text_); }
            set { this.SetPublishedProperty("Text", ref this.text_, value); }
        }

        [Published]
        public Font Font {
            get { return this.GetPublishedProperty("Font", ref this.font_); }
            set { this.SetPublishedProperty("Font", ref this.font_, value); }
        }

        [Published]
        public Color Color {
            get { return this.GetPublishedProperty("Color", ref this.color_); }
            set { this.SetPublishedProperty("Color", ref this.color_, value); }
        }

        [Published]
        public bool IsPublic {
            get { return this.GetPublishedProperty("IsPublic", ref this.public_); }
            set { this.SetPublishedProperty("IsPublic", ref this.public_, value); }
        }

        #endregion

        #region None Publishing

        /// <summary>
        /// whether we should paint the sheet or not (when we are moving textboxes but haven't
        /// updated the sheet's location, we want the sheet to dissapear, so we don't repaint it).
        /// </summary>
        public bool Visible {
            get { return visible_; }
            set { visible_ = value; }
        }

        public bool WidthAccommodate {
            get { return width_accommodate_; }
            set { width_accommodate_ = value; }
        }

        public int LongestLineWidth {
            get { return this.longest_line_width_; }
            set { this.longest_line_width_ = value; }
        }

        #endregion
    }

    [Serializable]
    internal class StatusLabel : TextSheetModel {
        private static Dictionary<SlideModel, StatusLabel> dictionary_;

        private StatusLabel(SlideModel slide, string text)
            : base(Guid.NewGuid(), text, SheetDisposition.All, false, true,
            new Rectangle(10, 0, text.Length * 20, 20), Color.Red,
            new Font("Arial", 12)) {

        }

        public static void ChangeLabelForSlide(SlideModel slide, string text) {
            if (dictionary_ == null) {
                dictionary_ = new Dictionary<SlideModel, StatusLabel>();
            }
            if (!dictionary_.ContainsKey(slide)) {
                StatusLabel label = new StatusLabel(slide, text);
                dictionary_[slide] = label;
                using (Synchronizer.Lock(slide.SyncRoot)) {
                    slide.AnnotationSheets.Add(label);
                }
            }else{
                StatusLabel label = dictionary_[slide];
                using (Synchronizer.Lock(label.SyncRoot)) {
                    label.Text = text;
                }
            }
        }

        /// <summary>
        /// Remove the label from the slide.  Note that we prefer to do this instead of setting the lable to empty string
        /// so that the lable does not become a permanent part of the deck.
        /// </summary>
        /// <param name="slide"></param>
        public static void RemoveLabelForSlide(SlideModel slide) {
            if ((dictionary_ != null) && (dictionary_.ContainsKey(slide))) {
                using (Synchronizer.Lock(slide.SyncRoot)) {
                    slide.AnnotationSheets.Remove(dictionary_[slide]);
                }
                dictionary_.Remove(slide);
            }
        }

    }
}
