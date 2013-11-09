// $Id: PenStylusModel.cs 990 2006-07-06 00:53:49Z julenka $

using System;
using Microsoft.Ink;

namespace UW.ClassroomPresenter.Model.Stylus {
    public class PenStylusModel : StylusModel {



        public PenStylusModel(Guid id) : this(id, new DrawingAttributes()) {}

        public PenStylusModel(Guid id, DrawingAttributes atts) : base(id) {
            this.m_DrawingAttributes = atts;
        }


    }
}
