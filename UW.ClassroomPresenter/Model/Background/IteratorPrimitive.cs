using System;
using System.Collections;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace UW.ClassroomPresenter.Model.Background
{
    /// <summary>
    /// An iterator primitive that allows us to repeat a set of primitives  
    /// </summary>
    [Serializable]
    public class IteratorPrimitive :IBackgroundPrimitive
    {
        #region Properties

        /// <summary>
        /// The child primitive
        /// </summary>
        private ArrayList m_Geometry;
        public ArrayList Geometry
        {
            get { return this.m_Geometry; }
        }

        /// <summary>
        /// Matrix multiplied before each set of child primitives is drawn
        /// </summary>
        private SerializableMatrix m_OffsetTransform;
        public SerializableMatrix OffsetTransform
        {
            get { return this.m_OffsetTransform; }
            set { this.m_OffsetTransform = value; }
        }

        /// <summary>
        /// How should the repetition occur? if 0 repeat infinitely,
        /// else repeat the given number of times
        /// </summary>
        private int m_RepeatAmount;
        public int RepeatAmount
        {
            get { return this.m_RepeatAmount; }
            set { this.m_RepeatAmount = value; }
        }

        #endregion Properties

        #region Construction

        /// <summary>
        /// Construction
        /// </summary>
        public IteratorPrimitive(SerializableMatrix offset, int repeatAmount)
        {
            this.m_OffsetTransform = offset;
            this.m_RepeatAmount = repeatAmount;
            this.m_Geometry = new ArrayList();
        }

        #endregion Construction        

        public IBackgroundPrimitive Clone()
        {
            IteratorPrimitive iterator = new IteratorPrimitive(this.OffsetTransform, this.RepeatAmount);
            for (int i = 0; i < this.Geometry.Count; i++)
            {
                iterator.Geometry.Add(((IBackgroundPrimitive)this.Geometry[i]).Clone());
            }
            return iterator; 
        }
    }
}
