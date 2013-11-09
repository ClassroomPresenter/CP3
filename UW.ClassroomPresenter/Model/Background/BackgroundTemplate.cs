using System;
using System.Collections;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace UW.ClassroomPresenter.Model.Background
{
    /// <summary>
    /// Background Template class
    /// </summary>
    [Serializable]
    public class BackgroundTemplate
    {
        #region Global Properties

        /// <summary>
        /// The Name for the template
        /// </summary>
        private string m_Name;
        public string Name
        {
            get { return this.m_Name; }
            set { this.m_Name = value; }
        }

        /// <summary>
        /// The chinese name for the template
        /// </summary>
        private string m_CNName;
        public string CNName
        {
            get { return this.m_CNName; }
            set { this.m_CNName = value; }
        }

        /// <summary>
        /// Franch name of the template
        /// </summary>
        private string m_FRName;
        public string FRName
        {
            get { return this.m_FRName; }
            set { this.m_FRName = value; }
        }

        private string m_ESName;
        public string ESName
        {
            get { return this.m_ESName; }
            set { this.m_ESName = value; }
        }

        private string m_PTName;
        public string PTName
        {
            get { return this.m_PTName; }
            set { this.m_PTName = value; }
        }

        /// <summary>
        /// The brush to use to paint the background
        /// </summary>
        private SerializableBrush m_BackgroundBrush;
        public SerializableBrush BackgroundBrush
        {
            get { return this.m_BackgroundBrush; }
            set { this.m_BackgroundBrush = value; }
        }

        /// <summary>
        /// The global transformation
        /// </summary>
        private SerializableMatrix m_GeometryTransform;
        public SerializableMatrix GeometryTransform
        {
            get { return this.m_GeometryTransform; }
            set { this.m_GeometryTransform = value; }
        }

        /// <summary>
        /// The set of pens used to draw lines
        /// </summary>
        private ArrayList m_PrimitivePens;
        public ArrayList PrimitivePens
        {
            get { return this.m_PrimitivePens; }
        }

        /// <summary>
        /// Geometry
        /// </summary>
        private ArrayList m_Geometry;
        public ArrayList Geometry
        {
            get { return this.m_Geometry; }
        }

        /// <summary>
        /// The Width for the Background Template
        /// </summary>
        private int m_Width;
        public int Width
        {
            get { return this.m_Width; }
            set { this.m_Width = value; }
        }

        /// <summary>
        /// The Height for Background Template
        /// </summary>
        private int m_Height;
        public int Height
        {
            get { return this.m_Height; }
            set { this.m_Height = value; }
        }

        #endregion Global Properties

        [NonSerialized]
        private string m_Language;

        public BackgroundTemplate()
        {
            this.m_PrimitivePens = new ArrayList();
            this.m_Geometry = new ArrayList();
        }

        public BackgroundTemplate(string language)
        {
            this.m_PrimitivePens = new ArrayList();
            this.m_Geometry = new ArrayList();
            this.m_Language = language;
        }

        public BackgroundTemplate Clone()
        {
            BackgroundTemplate bkgTemplate = new BackgroundTemplate();

            bkgTemplate.Name = this.Name;
            bkgTemplate.CNName = this.CNName;
            bkgTemplate.FRName = this.FRName;
            bkgTemplate.ESName = this.ESName;
            bkgTemplate.PTName = this.PTName;
            if (this.BackgroundBrush != null)
            {
                bkgTemplate.BackgroundBrush = (SerializableBrush)this.BackgroundBrush.Clone();
            }
            bkgTemplate.GeometryTransform = this.GeometryTransform;
            bkgTemplate.Height = this.Height;
            bkgTemplate.Width = this.Width;

            for (int i = 0; i < this.PrimitivePens.Count; i++)
                bkgTemplate.PrimitivePens.Add(((SerializablePen)this.PrimitivePens[i]).Clone());

            for (int i = 0; i < this.Geometry.Count; i++)
                bkgTemplate.Geometry.Add(((IBackgroundPrimitive)this.Geometry[i]).Clone());

            return bkgTemplate;
        }

        public void UpdateValue(BackgroundTemplate template)
        {
            if (this == template) return;

            if (template.BackgroundBrush != null)
            {
                this.BackgroundBrush = template.BackgroundBrush.Clone();
            }

            this.Name = template.Name;
            this.CNName = template.CNName;
            this.ESName = template.ESName;
            this.FRName = template.FRName;
            this.PTName = template.PTName;
            this.Height = template.Height;
            this.Width = template.Width;
            this.GeometryTransform = template.GeometryTransform;

            this.PrimitivePens.Clear();
            for (int i = 0; i < template.PrimitivePens.Count; i++)
                this.PrimitivePens.Add(((SerializablePen)template.PrimitivePens[i]).Clone());

            this.Geometry.Clear();
            for (int i = 0; i < template.Geometry.Count; i++)
                this.Geometry.Add(((IBackgroundPrimitive)template.Geometry[i]).Clone());
        }

        public override string ToString()
        {
            string displayName = null;

            switch (this.m_Language)
            {
                case "zh-CN": displayName = this.m_CNName; break;
                case "fr-FR": displayName = this.m_FRName; break;
                case "es-ES": displayName = this.m_ESName; break;
                case "pt-BR": displayName = this.m_PTName; break;
            }
            
            if(this.Name==null)
                this.Name = "unnamed template";

            if (displayName == null)
                displayName = this.Name;

            return displayName;
        }

        public SerializablePen GetPrimitivePenByIndex(int index)
        {
            for (int i = 0; i < this.m_PrimitivePens.Count; i++)
            {
                SerializablePen pen = (SerializablePen)this.m_PrimitivePens[i];
                if (pen.Index == index)
                    return pen;
            }

            if (index < this.m_PrimitivePens.Count)
                return (SerializablePen)this.m_PrimitivePens[index];

            return null;
        }
    }
}
