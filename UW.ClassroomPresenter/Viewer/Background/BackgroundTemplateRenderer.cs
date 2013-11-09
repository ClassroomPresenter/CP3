using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

using UW.ClassroomPresenter.Model.Background;

namespace UW.ClassroomPresenter.Viewer.Background
{
    /// <summary>
    /// Render for the background template
    /// </summary>
    public class BackgroundTemplateRenderer :IDisposable
    {
        private BackgroundTemplate m_BackgroundTemplate;
        private bool m_Disposed;

        private float m_Zoom;
        public float Zoom
        {
            get { return this.m_Zoom; }
            set { this.m_Zoom = value; }
        }

        /// <summary>
        /// Construction
        /// </summary>
        public BackgroundTemplateRenderer(BackgroundTemplate backgroundTemplate)
        {
            this.m_BackgroundTemplate = backgroundTemplate;
            this.m_Zoom = 1f;
        }

        #region Methods

        /// <summary>
        /// Draw all geometry elements
        /// </summary>
        public void DrawAll(Graphics g, Rectangle r)
        {
            if (this.m_BackgroundTemplate == null)
                return;

            //Get a copy of the current transform for recovery after  drawing background
            Matrix oldTransfrom = g.Transform.Clone();

            //Compute the radio 
            float radioX = 1, radioY = 1;
            if (this.m_BackgroundTemplate.Width != 0)
                radioX = (float)(r.Width * 1.0 / this.m_BackgroundTemplate.Width);
            else
                this.m_BackgroundTemplate.Width = r.Width;
            if (this.m_BackgroundTemplate.Height != 0)
                radioY = (float)(r.Height * 1.0 / this.m_BackgroundTemplate.Height);
            else
                this.m_BackgroundTemplate.Height = r.Height;            
            
            //Paint the background with BackgroundBrush
            if (this.m_BackgroundTemplate.BackgroundBrush != null)
            {
                Brush brush = this.m_BackgroundTemplate.BackgroundBrush.GetBrush(this.m_BackgroundTemplate.Width,this.m_BackgroundTemplate.Height);

                if (this.m_BackgroundTemplate.BackgroundBrush.BrushType==BrushType.LinearGradientBrush)
                {
                    ((LinearGradientBrush)brush).MultiplyTransform(new Matrix(radioX, 0, 0, radioY, 0, 0));
                    g.FillRectangle(brush, r);
                }
                else 
                    g.FillRectangle(brush, r);
            }

            //compute the transform matrix
            Matrix newTransform = g.Transform.Clone();
            newTransform.Multiply(new Matrix(radioX, 0, 0, radioY, 0, 0));

            if (this.m_BackgroundTemplate.GeometryTransform != null)
                newTransform.Multiply(this.m_BackgroundTemplate.GeometryTransform.GetMatrix());

            newTransform.Multiply(new Matrix(this.m_Zoom, 0, 0, this.m_Zoom, 0,0));
            g.Transform = newTransform;

            //Draw each primitive
            foreach (IBackgroundPrimitive p in this.m_BackgroundTemplate.Geometry)
            {
                PrimitiveRenderer.GetRenderer(p, this.m_Zoom).Draw(this.m_BackgroundTemplate, g, r);
            }

            //recovery the transform
            g.Transform = oldTransfrom;
        }

        public void Dispose()
        {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.m_Disposed) return;
            this.m_Disposed = true;
        }

        #endregion Methods
    }
}
