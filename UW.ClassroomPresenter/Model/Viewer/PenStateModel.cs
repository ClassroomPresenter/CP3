using System;
using System.Drawing;
using Microsoft.Ink;

namespace UW.ClassroomPresenter.Model.Viewer
{
    public class PenStateModel : PropertyPublisher, ICloneable
    {
        private RasterOperation m_PenRasterOperation;
        private PenTip m_PenTip;
        private int m_PenColor;
        private int m_PenWidth;
        private int m_PenHeight;
        private RasterOperation m_HLRasterOperation;
        private PenTip m_HLTip;
        private int m_HLColor;
        private int m_HLWidth;
        private int m_HLHeight;

        [Published]
        [Saved]
        public RasterOperation PenRasterOperation
        {
            get { return this.GetPublishedProperty("PenRasterOperation", ref this.m_PenRasterOperation); }
            set { this.SetPublishedProperty("PenRasterOperation", ref this.m_PenRasterOperation, value); }
        }

        [Published]
        [Saved]
        public PenTip PenTip
        {
            get { return this.GetPublishedProperty("PenTip", ref this.m_PenTip); }
            set { this.SetPublishedProperty("PenTip", ref this.m_PenTip, value); }
        }

        [Published]
        [Saved]
        public int PenColor
        {
            get { return this.GetPublishedProperty("PenColor", ref this.m_PenColor); }
            set { this.SetPublishedProperty("PenColor", ref this.m_PenColor, value); }
        }

        [Published]
        [Saved]
        public int PenWidth
        {
            get { return this.GetPublishedProperty("PenWidth", ref this.m_PenWidth); }
            set { this.SetPublishedProperty("PenWidth", ref this.m_PenWidth, value); }
        }

        [Published]
        [Saved]
        public int PenHeight
        {
            get { return this.GetPublishedProperty("PenHeight", ref this.m_PenHeight); }
            set { this.SetPublishedProperty("PenHeight", ref this.m_PenHeight, value); }
        }

        [Published]
        [Saved]
        public RasterOperation HLRasterOperation
        {
            get { return this.GetPublishedProperty("HLRasterOperation", ref this.m_HLRasterOperation); }
            set { this.SetPublishedProperty("HLRasterOperation", ref this.m_HLRasterOperation, value); }
        }

        [Published]
        [Saved]
        public PenTip HLTip
        {
            get { return this.GetPublishedProperty("HLTip", ref this.m_HLTip); }
            set { this.SetPublishedProperty("HLTip", ref this.m_HLTip, value); }
        }

        [Published]
        [Saved]
        public int HLColor
        {
            get { return this.GetPublishedProperty("HLColor", ref this.m_HLColor); }
            set { this.SetPublishedProperty("HLColor", ref this.m_HLColor, value); }
        }

        [Published]
        [Saved]
        public int HLWidth
        {
            get { return this.GetPublishedProperty("HLWidth", ref this.m_HLWidth); }
            set { this.SetPublishedProperty("HLWidth", ref this.m_HLWidth, value); }
        }

        [Published]
        [Saved]
        public int HLHeight
        {
            get { return this.GetPublishedProperty("HLHeight", ref this.m_HLHeight); }
            set { this.SetPublishedProperty("HLHeight", ref this.m_HLHeight, value); }
        }

        public PenStateModel()
        {
            DrawingAttributes atts = new DrawingAttributes();

            this.m_PenRasterOperation = RasterOperation.CopyPen;
            this.m_PenTip = PenTip.Ball;
            this.m_PenColor = Color.Orange.ToArgb();
            this.m_PenWidth = (int)atts.Width;
            this.m_PenHeight = (int)atts.Height;
            this.m_HLRasterOperation = RasterOperation.MaskPen;
            this.m_HLTip = PenTip.Ball;
            this.m_HLColor = Color.Pink.ToArgb();
            this.m_HLWidth = (int)(6 * atts.Width);
            this.m_HLHeight = (int)(2 * atts.Width);
        }

        public void UpdateValues(object model)
        {
            // Make sure both objects exist and aren't the same
            if (model != null &&
                model is PenStateModel &&
                model != this) {

                PenStateModel m = (PenStateModel)model;

                using (Synchronizer.Lock(this.SyncRoot))
                {
                    using (Synchronizer.Lock(m.SyncRoot))
                    {
                        if (this.m_PenRasterOperation != m.m_PenRasterOperation)
                            this.m_PenRasterOperation = m.m_PenRasterOperation;
                        if (this.m_PenTip != m.m_PenTip)
                            this.m_PenTip = m.m_PenTip;
                        if (this.m_PenColor != m.m_PenColor)
                            this.m_PenColor = m.m_PenColor;
                        if (this.m_PenWidth != m.m_PenWidth)
                            this.m_PenWidth = m.m_PenWidth;
                        if (this.m_PenHeight != m.m_PenHeight)
                            this.m_PenHeight = m.m_PenHeight;
                        if (this.m_HLRasterOperation != m.m_HLRasterOperation)
                            this.m_HLRasterOperation = m.m_HLRasterOperation;
                        if (this.m_HLTip != m.m_HLTip)
                            this.m_HLTip = m.m_HLTip;
                        if (this.m_HLColor != m.m_HLColor)
                            this.m_HLColor = m.m_HLColor;
                        if (this.m_HLWidth != m.m_HLWidth)
                            this.m_HLWidth = m.m_HLWidth;
                        if (this.m_HLHeight != m.m_HLHeight)
                            this.m_HLHeight = m.m_HLHeight;
                    }
                }
            }
        }

        public object Clone()
        {
            PenStateModel clonemodel = new PenStateModel();

            clonemodel.m_PenRasterOperation = this.m_PenRasterOperation;
            clonemodel.m_PenTip = this.m_PenTip;
            clonemodel.m_PenColor = this.m_PenColor;
            clonemodel.m_PenWidth = this.m_PenWidth;
            clonemodel.m_PenHeight = this.m_PenHeight;
            clonemodel.m_HLRasterOperation = this.m_HLRasterOperation;
            clonemodel.m_HLTip = this.m_HLTip;
            clonemodel.m_HLColor = this.m_HLColor;
            clonemodel.m_HLWidth = this.m_HLWidth;
            clonemodel.m_HLHeight = this.m_HLHeight;

            return clonemodel;
        }
    }
}
