using System;
using System.Collections;
using System.Drawing;


namespace UW.ClassroomPresenter.Model.Background
{
    /// <summary>
    /// A line Primitive
    /// </summary>
    [Serializable]
    public class LinePrimitive: IBackgroundPrimitive
    {
        #region Properties

        /// <summary>
        /// The type(ray,segment,line) of line to draw
        /// </summary>
        private LineType m_LineType;
        public LineType LineType
        {
            get { return this.m_LineType; }
            set { this.m_LineType = value; }
        }

        /// <summary>
        /// An index into the parent template's PrimitivePens arraylist
        /// </summary>
        private int m_PenIndex;
        public int PenIndex
        {
            get { return this.m_PenIndex; }
            set { this.m_PenIndex = value; }
        }

        /// <summary>
        /// the points defining this line
        /// P1 is the start point, and P2 is the end point
        /// </summary>
        private PointF m_P1, m_P2;
        public PointF P1
        {
            get { return this.m_P1; }
            set { this.m_P1 = value; }
        }
        public PointF P2
        {
            get { return this.m_P2; }
            set { this.m_P2 = value; }
        }

        #endregion  Properties

        /// <summary>
        /// Construction
        /// </summary>
        public LinePrimitive(LineType type, int penIndex, PointF p1, PointF p2)
        {
            this.m_LineType = type;
            this.m_PenIndex = penIndex;
            this.m_P1 = p1;
            this.m_P2 = p2;
        }

        public IBackgroundPrimitive Clone()
        {
            LinePrimitive line = new LinePrimitive(this.LineType, this.PenIndex, this.P1, this.P2);
            return line;
        }
    }

    /// <summary>
    /// Enumerated type representing the flavor of line
    /// </summary>
    public enum LineType
    {
        RAY = 0,
        SEGMENT = 1,
        LINE = 2
    }
}
