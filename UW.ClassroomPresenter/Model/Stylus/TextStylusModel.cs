using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Ink;
using System.Drawing;

namespace UW.ClassroomPresenter.Model.Stylus
{
    /// <summary>
    /// this is an empty class which I am using like an enum so that we can 
    /// determine when we are in "adding text" mode.
    /// </summary>
    [Serializable]
    public class TextStylusModel: StylusModel
    {
        private Font font_ = new Font("Arial", 20);
        private static TextStylusModel text_model_;

        private TextStylusModel(Guid id): base(id)
        {
        }

        public static TextStylusModel GetInstance() {
            if (text_model_ == null) {
                text_model_ = new TextStylusModel(Guid.NewGuid());
            } 
                return text_model_;
            
        }

        [Published]
        public Font Font {
            get { return this.GetPublishedProperty("Font", ref this.font_); }
            set { this.SetPublishedProperty("Font", ref this.font_, value); }
        }

        /// <summary>
        /// the non-published version
        /// </summary>
        public Font DefaultFont {
            get { return this.font_; }
        }

    }
}
