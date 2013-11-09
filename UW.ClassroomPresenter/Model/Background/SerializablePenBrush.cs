using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace UW.ClassroomPresenter.Model.Background
{
    #region SerializableBrush

    /// <summary>
    /// Brush can not be marked as serializable, so SerializableBrush is defined.
    /// </summary>
    [Serializable]
    public class SerializableBrush
    {
        #region Properties

        /// <summary>
        /// BrushType
        /// </summary>
        private BrushType m_BrushType;
        public BrushType BrushType
        {
            get { return this.m_BrushType; }
            set { this.m_BrushType = value; }
        }

        /// <summary>
        /// Color for SolidBrush
        /// </summary>
        private Color m_SolidBrushColor;
        public Color SolidBrushColor
        {
            get{return this.m_SolidBrushColor;}
            set{this.m_SolidBrushColor=value;}
        }

        /// <summary>
        /// Color1 for LinearGradientBrush
        /// </summary>
        private Color m_LinearGradientBrushColor1;
        public Color LinearGradientBrushColor1
        {
            get { return this.m_LinearGradientBrushColor1; }
            set { this.m_LinearGradientBrushColor1 = value; }
        }
        
        /// <summary>
        /// Color2 for LinearGradientBrush
        /// </summary>
        private Color m_LinearGradientBrushColor2;
        public Color LinearGradientBrushColor2
        {
            get{return this.m_LinearGradientBrushColor2;}
            set{this.m_LinearGradientBrushColor2=value;}
        }

        /// <summary>
        /// Point1 for LinearGradientBrush
        /// </summary>
        private PointF m_LinearGradientPoint1;
        public PointF LinearGradientPoint1
        {
            get { return this.m_LinearGradientPoint1; }
            set { this.m_LinearGradientPoint1 = value; }
        }

        /// <summary>
        /// Point2 for LinearGradientBrush
        /// </summary>
        private PointF m_LinearGradientPoint2;
        public PointF LinearGradientPoint2
        {
            get { return this.m_LinearGradientPoint2; }
            set{this.m_LinearGradientPoint2=value;}
        }

        #endregion Properties

        #region Construction

        /// <summary>
        /// Construction for SolidBrush
        /// </summary>
        public SerializableBrush(Color color)
        {
            this.BrushType = BrushType.SolidBrush;
            this.SolidBrushColor = color;

            this.m_LinearGradientBrushColor1 = Color.White;
            this.m_LinearGradientBrushColor2 = Color.White;
            this.LinearGradientPoint1 = new PointF(0, 0);
            this.LinearGradientPoint2 = new PointF(1, 1);
        }

        /// <summary>
        /// Construction for LinearGradientBrush
        /// </summary>
        public SerializableBrush(PointF point1, PointF point2, Color color1, Color color2)
        {
            this.BrushType = BrushType.LinearGradientBrush;
            this.m_LinearGradientPoint1 = point1;
            this.m_LinearGradientPoint2 = point2;
            this.m_LinearGradientBrushColor1 = color1;
            this.m_LinearGradientBrushColor2 = color2;

            this.SolidBrushColor = Color.White;
        }

        #endregion Construction

        /// <summary>
        /// Get Brush Object
        /// </summary>
        public Brush GetBrush()
        {
            Brush brush=null;

            if (m_BrushType == BrushType.SolidBrush)
            {
                brush = new SolidBrush(this.SolidBrushColor);
            }
            else if (m_BrushType == BrushType.LinearGradientBrush)
            {
                brush = new LinearGradientBrush(this.m_LinearGradientPoint1, this.m_LinearGradientPoint2, this.m_LinearGradientBrushColor1, this.m_LinearGradientBrushColor2);
            }
            return brush;
        }

        /// <summary>
        /// Get Brush Object
        /// </summary>
        /// <param name="width"> The width of display rectangle area</param>
        /// <param name="height"> The height of display rectangle area</param>
        /// <returns>Brush</returns>
        public Brush GetBrush(float width, float height)
        {
            Brush brush = null;

            if (m_BrushType == BrushType.SolidBrush)
            {
                brush = new SolidBrush(this.SolidBrushColor);
            }
            else if (m_BrushType == BrushType.LinearGradientBrush)
            {
                brush = new LinearGradientBrush(new PointF(this.m_LinearGradientPoint1.X * width, this.m_LinearGradientPoint1.Y * height),
                                                new PointF(this.m_LinearGradientPoint2.X * width, this.m_LinearGradientPoint2.Y * height),
                                                this.m_LinearGradientBrushColor1, this.m_LinearGradientBrushColor2);
            }
            return brush;
        }

        public SerializableBrush Clone()
        {
            SerializableBrush sbrush=null;

            if (this.BrushType == BrushType.SolidBrush)
            {
                sbrush = new SerializableBrush(this.SolidBrushColor);
            }
            else if (this.BrushType == BrushType.LinearGradientBrush)
            {
                sbrush = new SerializableBrush(this.m_LinearGradientPoint1,this.m_LinearGradientPoint2, this.LinearGradientBrushColor1, this.LinearGradientBrushColor2);
            }
            return sbrush;
        }
    }

    public enum BrushType
    {
        SolidBrush = 0, 
        LinearGradientBrush = 1
    }

    #endregion 

    #region SerializableMatrix

    /// <summary>
    /// System.Drawing.Drawing2D.Matrix can not be marked as serializable, so SerializableMatrix is defined.
    /// </summary>
    [Serializable]
    public class SerializableMatrix
    {
        private float m_Matrix11;
        private float m_Matrix12;
        private float m_Matrix21;
        private float m_Matrix22;

        private float m_OffsetX;
        public float OffsetX
        {
            get { return this.m_OffsetX; }
            set { this.m_OffsetX = value; }
        }

        private float m_OffsetY;
        public float OffsetY
        {
            get { return this.m_OffsetY; }
            set { this.m_OffsetY = value; }
        }

        public float ScaleX
        {
            get { return (float)Math.Sqrt(this.m_Matrix11 * this.m_Matrix11 + this.m_Matrix21 * this.m_Matrix21); }
        }

        public float ScaleY
        {
            get { return (float)Math.Sqrt(this.m_Matrix12 * this.m_Matrix12 + this.m_Matrix22 * this.m_Matrix22); }
        }

        public float RotateAngle
        {
            get
            {
                float angle1 = (float)Math.Asin(this.m_Matrix12 / this.ScaleY);
                if (this.m_Matrix12 * this.m_Matrix11 < 0) angle1 = (float)(Math.PI - angle1);
                angle1 = (float)(angle1 / Math.PI * 180);
                return angle1;
            }
        }

        /// <summary>
        /// Matrix Construction
        /// </summary>
        public SerializableMatrix(float m11, float m12, float m21, float m22, float offsetX, float offsetY)
        {
            this.m_Matrix11 = m11;
            this.m_Matrix12 = m12;
            this.m_Matrix21 = m21;
            this.m_Matrix22 = m22;
            this.m_OffsetX = offsetX;
            this.m_OffsetY = offsetY;
        }

        /// <summary>
        /// Get Matrix Object
        /// </summary>
        public Matrix GetMatrix()
        {
            return new Matrix(this.m_Matrix11, this.m_Matrix12, this.m_Matrix21, this.m_Matrix22, this.m_OffsetX, this.m_OffsetY);            
        }

        /// <summary>
        /// Scale the Matrix
        /// </summary>
        /// <param name="scaleX"></param>
        /// <param name="scaleY"></param>
        public void Scale(float scaleX, float scaleY)
        {
            this.m_Matrix11 *= scaleX;
            this.m_Matrix22 *= scaleY;
            this.m_Matrix21 *= scaleX;
            this.m_Matrix12 *= scaleY;
        }        
    }

    #endregion

    #region SerializablePen

    [Serializable]
    public class SerializablePen
    {
        #region Properties

        /// <summary>
        /// Pen color
        /// </summary>
        private Color m_Color;
        public Color Color
        {
            get { return this.m_Color; }
            set { this.m_Color = value; }
        }

        /// <summary>
        /// DashStyle for the Pen
        /// </summary>
        private DashStyle m_DashStyle;
        public DashStyle DashStyle
        {
            get { return this.m_DashStyle; }
            set { this.m_DashStyle = value; }
        }

        /// <summary>
        /// Pen weight
        /// </summary>
        private float m_Width;
        public float Width
        {
            get { return this.m_Width; }
            set { this.m_Width = value; }
        }

        private int m_Index;
        public int Index
        {
            get { return this.m_Index; }
            set { this.m_Index = value; }
        }

        #endregion 

        public SerializablePen(Color color)
        {
            this.m_Color = color;
            this.m_DashStyle = DashStyle.Solid;
            this.m_Width = 1;
        }

        /// <summary>
        /// Get Pen Object
        /// </summary>
        public Pen GetPen()
        {
            Pen pen = new Pen(this.m_Color);
            pen.Width=this.m_Width;
            pen.DashStyle=this.m_DashStyle;
            return pen;
        }

        public SerializablePen Clone()
        {
            SerializablePen pen = new SerializablePen(this.Color);
            pen.DashStyle = this.DashStyle;
            pen.Width = this.Width;
            pen.Index = this.Index;
            return pen;
        }
    }

    #endregion
}
