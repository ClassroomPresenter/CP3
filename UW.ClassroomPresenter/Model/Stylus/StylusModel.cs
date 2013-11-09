// $Id: StylusModel.cs 990 2006-07-06 00:53:49Z julenka $

using System;
using Microsoft.Ink;

namespace UW.ClassroomPresenter.Model.Stylus {
    public abstract class StylusModel : PropertyPublisher {

        private readonly Guid m_Id;
        protected DrawingAttributes m_DrawingAttributes;
        protected StylusModel(Guid id) {
            this.m_Id = id;
            this.m_DrawingAttributes = new DrawingAttributes();
        }

        public Guid Id {
            get { return this.m_Id; }
        }

        [Published]
        public DrawingAttributes DrawingAttributes {
            get { return this.GetPublishedProperty("DrawingAttributes", ref this.m_DrawingAttributes); }
            set { this.SetPublishedProperty("DrawingAttributes", ref this.m_DrawingAttributes, value); }
        }
    }
}
