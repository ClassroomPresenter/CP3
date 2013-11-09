using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Ink;
using System.Drawing;

namespace UW.ClassroomPresenter.Model.Stylus {
    /// <summary>
    /// this is an empty class which I am using like an enum so that we can 
    /// determine when we are in "adding image" mode.
    /// </summary>
    [Serializable]
    public class ImageStylusModel : StylusModel {


        public ImageStylusModel(Guid id)
            : base(id) {
        }


    }
}
