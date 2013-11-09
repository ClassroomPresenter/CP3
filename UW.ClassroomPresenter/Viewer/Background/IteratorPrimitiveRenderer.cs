using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

using UW.ClassroomPresenter.Model.Background;

namespace UW.ClassroomPresenter.Viewer.Background
{
    public class IteratorPrimitiveRenderer : PrimitiveRenderer
    {
        /// <summary>
        /// IteratorPrimitive Model for the current rendering
        /// </summary>
        private IteratorPrimitive m_Iterator;
        private float m_Zoom;

        public IteratorPrimitiveRenderer(IteratorPrimitive iterator, float zoom)
        {
            this.m_Iterator = iterator;
            this.m_Zoom = zoom;
        }

        /// <summary>
        /// Draw the iterator primitives
        /// </summary>
        public override void Draw(BackgroundTemplate parent, Graphics g, Rectangle r)
        {
            Matrix old = g.Transform.Clone();

            //if the repeatAmount==0 (infinitely), then compute the repeat times for both directions

            if (this.m_Iterator.RepeatAmount== 0)
            {
                int totalRepeatTimes = this.GetRepeatTimesForInfinite((int)(parent.Width / this.m_Zoom), (int)(parent.Height / this.m_Zoom));

                //draw as offset direction
                for (int i = 0; i < totalRepeatTimes; i++)
                {
                    Matrix current = g.Transform.Clone();

                    foreach (IBackgroundPrimitive p in this.m_Iterator.Geometry)
                    {
                        PrimitiveRenderer render = PrimitiveRenderer.GetRenderer(p, this.m_Zoom);
                        if (render == null) continue;

                        //For Line, compute the Transform when rendering
                        if (render is LinePrimitiveRenderer)
                        {
                            ((LinePrimitiveRenderer)render).HasIterator = true;
                            ((LinePrimitiveRenderer)render).IteratorIndex = i;
                            ((LinePrimitiveRenderer)render).OffsetTransform = this.m_Iterator.OffsetTransform.GetMatrix();
                            g.Transform = old;
                            render.Draw(parent, g, r);
                            continue;
                        }
                        else render.Draw(parent, g, r);
                    }

                    //Compute new offset transform                    
                    current.Multiply(this.m_Iterator.OffsetTransform.GetMatrix());
                    g.Transform = current;
                }

                //Get reverse matrix of offsetTransform
                g.Transform = old.Clone();
                Matrix reverseTransform = this.m_Iterator.OffsetTransform.GetMatrix();

                if (reverseTransform.IsInvertible)
                    reverseTransform.Invert();

                //draw as reverse offset direction
                for (int i = 0; i < totalRepeatTimes; i++)
                {
                    Matrix current = g.Transform.Clone();

                    foreach (IBackgroundPrimitive p in this.m_Iterator.Geometry)
                    {
                        PrimitiveRenderer render = PrimitiveRenderer.GetRenderer(p, this.m_Zoom);
                        if (render == null) continue;

                        //For Line, compute the Transform when rendering
                        if (render is LinePrimitiveRenderer)
                        {
                            ((LinePrimitiveRenderer)render).HasIterator = true;
                            ((LinePrimitiveRenderer)render).IteratorIndex = i;
                            ((LinePrimitiveRenderer)render).OffsetTransform = reverseTransform;
                            g.Transform = old;
                            render.Draw(parent, g, r);
                            continue;
                        }
                        else render.Draw(parent, g, r);
                    }

                    //Compute new offset transform                    
                    current.Multiply(reverseTransform);
                    g.Transform = current;
                }
            }
            else
            {
                //the repeat number is non indifinited, ao draw primitive as defined
                for (int i = 0; i < this.m_Iterator.RepeatAmount; i++)
                {
                    foreach (IBackgroundPrimitive p in this.m_Iterator.Geometry)
                    {
                        PrimitiveRenderer.GetRenderer(p, this.m_Zoom).Draw(parent, g, r);
                    }

                    Matrix current = g.Transform.Clone();
                    current.Multiply(this.m_Iterator.OffsetTransform.GetMatrix());
                    g.Transform = current;
                }
            }
            g.Transform = old;
        }

        /// <summary>
        /// If the repeat amout is infinite, then compute the required times for cover all the rect
        /// </summary>
        private int GetRepeatTimesForInfinite(int width, int height)
        {
            int repeatNum = 0;

            float offsetX=this.m_Iterator.OffsetTransform.OffsetX;
            float offsetY = this.m_Iterator.OffsetTransform.OffsetY;

            if (offsetX == 0 && offsetY == 0)
            {
                repeatNum = 1;
                return repeatNum;
            }

            //computing the repeat time if offsetx and offsety both non-zero
            else if (offsetX != 0 && offsetY != 0)
            {
                int repeatX = (int)Math.Abs(width / offsetX);
                int repeatY = (int)Math.Abs(height / offsetY);

                //Compute the repeat time according to the diagonal
                int repeatDX = (int)Math.Abs((width * width + height * height) / (offsetX * width));
                int repeatDY = (int)Math.Abs((width * width + height * height) / (offsetY * height));

                repeatNum = Math.Max(Math.Min(repeatX, repeatY), Math.Min(repeatDX, repeatDY));
            }

            
            else if (offsetX == 0)
                repeatNum = (int)Math.Abs(height / offsetY);
            else if (offsetY == 0)
                repeatNum = (int)Math.Abs(width / offsetX);
            return repeatNum;
        }
    }
}
