using System;
using System.Collections;
using System.Drawing;
using System.Drawing.Drawing2D;

using UW.ClassroomPresenter.Model.Background;

namespace UW.ClassroomPresenter.Viewer.Background
{   
    public class LinePrimitiveRenderer : PrimitiveRenderer
    {
        #region  Properties

        /// <summary>
        /// Whether the line is the element of a IteratorPrimitive
        /// </summary>
        private bool m_HasIterator;
        public bool HasIterator
        {
            get { return this.m_HasIterator; }
            set { this.m_HasIterator = value; }
        }

        /// <summary>
        /// If the line is element of iteratorPrimitive, the current number  of repeat
        /// </summary>
        private int m_IteratorIndex;
        public int IteratorIndex
        {
            get { return this.m_IteratorIndex; }
            set { this.m_IteratorIndex = value; }
        }

        /// <summary>
        /// If the line is element of iteratorPrimitive, the offset transform matrix for the line repeat
        /// </summary>
        private Matrix m_OffsetTransform;
        public Matrix OffsetTransform
        {
            get { return this.m_OffsetTransform; }
            set { this.m_OffsetTransform = value; }
        }

        private float m_Zoom;

        /// <summary>
        /// LinePrimitive Model to render
        /// </summary>
        private LinePrimitive m_Line;

        #endregion 

        public LinePrimitiveRenderer(LinePrimitive line, float zoom)
        {
            this.m_Line = line;
            this.m_HasIterator = false;
            this.m_Zoom = zoom;
        }

        /// <summary>
        /// Draw the line
        /// </summary>
        public override void Draw(BackgroundTemplate parent, Graphics g, Rectangle r)
        {   
            PointF newS, newD, oldP1,oldP2;            

            oldP1 = this.m_Line.P1;
            oldP2 = this.m_Line.P2;

            //Update P1 P2 if the line is repeated
            this.CheckForIterater();

            newS = this.m_Line.P1;
            newD = this.m_Line.P2;           

            switch (this.m_Line.LineType)
            {
                case LineType.LINE: GetLineRectIntersections(parent, ref newS, ref newD); break;
                case LineType.RAY: GetRayRectIntersections(parent, ref newD); break;
                case LineType.SEGMENT: break;
            }
            
            SerializablePen pen = parent.GetPrimitivePenByIndex(this.m_Line.PenIndex);
            if(pen!=null)
                g.DrawLine(pen.GetPen(), newS, newD);

            this.m_Line.P1 = oldP1;
            this.m_Line.P2 = oldP2;
        }

        /// <summary>
        /// Compute the intersections between line and template boundary rect.
        /// </summary>
        private void GetLineRectIntersections(BackgroundTemplate parent, ref PointF point1, ref PointF point2)
        {
            float recWidth, recHeight;

            PointF p1, p2;
            p1 = this.m_Line.P1;
            p2 = this.m_Line.P2;

            recWidth = parent.Width/this.m_Zoom;
            recHeight = parent.Height/this.m_Zoom;            

            ArrayList points = new ArrayList();
            if (p1.X == p2.X)
            {
                point1 = new PointF(p1.X, 0);
                point2 = new PointF(p1.X, recHeight - 1);
                return;
            }
            else if (p1.Y == p2.Y)
            {
                point1 = new PointF(0, p1.Y);
                point2 = new PointF(recWidth - 1, p1.Y);
                return;
            }
            else
            {
                float slope = (p1.Y - p2.Y) / (p1.X - p2.X);

                float leftY = p1.Y - slope * p1.X;
                if (leftY >= 0 && leftY <= recHeight - 1)
                    points.Add(new PointF(0, leftY));

                float rightY = p1.Y + slope * (recWidth - 1 - p1.X);
                if (rightY >= 0 && rightY <= recHeight - 1)
                    points.Add(new PointF(recWidth-1, rightY));

                float topX = p1.X - p1.Y / slope;
                if (topX >= 0 && topX <= recWidth - 1)
                    points.Add(new PointF(topX, 0));

                float bottomX = p1.X + (recHeight - 1 - p1.Y) / slope;
                if (bottomX >= 0 && bottomX <= recWidth - 1)
                    points.Add(new PointF(bottomX, recHeight - 1));
            }
            if (points.Count >= 2)
            {
                point1 = (PointF)points[0];
                point2 = (PointF)points[1];
                if (point2.Equals(point1) && points.Count > 2)
                    point2 = (PointF)points[2];
            }
        }

        /// <summary>
        /// Compute the intersections between ray and template boundary rect.
        /// </summary>
        private void GetRayRectIntersections(BackgroundTemplate parent, ref PointF dstPoint)
        {
            float recWidth, recHeight;

            PointF p1, p2;
            p1 = this.m_Line.P1;
            p2 = this.m_Line.P2;

            recWidth = parent.Width;
            recHeight = parent.Height;

            ArrayList points = new ArrayList();
            if (p1.X == p2.X)
            {
                dstPoint = (p1.Y - p2.Y > 0) ? new PointF(p1.X, 0) : new PointF(p1.X, recHeight - 1);
                return;
            }
            else if (p1.Y == p2.Y)
            {
                dstPoint = (p1.X - p2.X > 0) ? new PointF(0, p1.Y) : new PointF(parent.Width - 1, p1.Y);
                return;
            }
            else
            {
                float slope = (p1.Y - p2.Y) / (p1.X - p2.X);

                if (p1.X > p2.X)
                {
                    float leftY = p1.Y - slope * p1.X;
                    if (leftY >= 0 && leftY <= recHeight - 1)
                        points.Add(new PointF(0, leftY));
                }
                else
                {
                    float rightY = p1.Y + slope * (recWidth - 1 - p1.X);
                    if (rightY >= 0 && rightY <= recHeight - 1)
                        points.Add(new PointF(recWidth - 1, rightY));
                }

                if (p1.Y > p2.Y)
                {
                    float topX = p1.X - p1.Y / slope;
                    if (topX >= 0 && topX <= recWidth - 1)
                        points.Add(new PointF(topX, 0));
                }
                else
                {
                    float bottomX = p1.X + (recHeight - 1 - p1.Y) / slope;
                    if (bottomX >= 0 && bottomX <= recWidth - 1)
                        points.Add(new PointF(bottomX, recHeight - 1)); 
                }
            }
            if (points.Count > 0)
            {
                dstPoint = (PointF)points[0];
            }
        }

        /// <summary>
        /// Check if the line is a Iterator element. if true, reset the new P1, P2 by OffsetTransform
        /// The new P1, P2 will be used to compute the intersections
        /// </summary>
        private void CheckForIterater()
        {
            if (this.m_HasIterator)
            {
                Matrix totalOffset = new Matrix(1, 0, 0, 1, 0, 0);
                for (int i = 0; i < this.m_IteratorIndex; i++)
                    totalOffset.Multiply(this.m_OffsetTransform);

                PointF[] points={this.m_Line.P1,this.m_Line.P2};
                totalOffset.TransformPoints(points);

                this.m_Line.P1 = points[0];
                this.m_Line.P2 = points[1];
            }
        }
    }
}
