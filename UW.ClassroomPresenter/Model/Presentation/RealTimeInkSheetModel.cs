// $Id: RealTimeInkSheetModel.cs 1319 2007-03-22 18:04:56Z fred $

using System;
using System.Threading;
using System.Drawing;
using Microsoft.Ink;
using System.Runtime.CompilerServices;
using System.Drawing.Drawing2D;

namespace UW.ClassroomPresenter.Model.Presentation {
    [Serializable]
    public class RealTimeInkSheetModel : InkSheetModel {

        #region Static

        /// <summary>
        /// Utility function for scaling ink packet arrays directly
        /// </summary>
        /// <param name="packets"></param>
        /// <param name="xScale"></param>
        /// <param name="yScale"></param>
        public static void ScalePackets(int[] packets, Matrix transformMatrix) {
            float xScale = transformMatrix.Elements[0];
            float yScale = transformMatrix.Elements[3];
            for (int i = 0; i < packets.Length; i++) {
                if (i % 2 == 0) {
                    packets[i] = (int)Math.Round((float)packets[i] * xScale);
                }
                else {
                    packets[i] = (int)Math.Round((float)packets[i] * yScale);
                }
            }
        }

        #endregion Static

        // Published properties:
        [NonSerialized] private DrawingAttributes m_DrawingAttributes;

        public RealTimeInkSheetModel(Guid id, SheetDisposition disposition, Rectangle bounds) : base(id, disposition, bounds) {}
        public RealTimeInkSheetModel( Guid id, SheetDisposition disposition, Rectangle bounds, Ink ink ) : base( id, disposition, bounds, ink ) {}

        [Published] public DrawingAttributes CurrentDrawingAttributes {
            get { return this.GetPublishedProperty("CurrentDrawingAttributes", ref this.m_DrawingAttributes); }
            set {
                this.SetPublishedProperty("CurrentDrawingAttributes", ref this.m_DrawingAttributes, value);
            }
        }

        [NonSerialized] private StylusDownEventHandler m_StylusDownDelegate;
        public event StylusDownEventHandler StylusDown {
            [MethodImpl(MethodImplOptions.Synchronized)]
            add { this.m_StylusDownDelegate = (StylusDownEventHandler)Delegate.Combine(this.m_StylusDownDelegate, value); }
            [MethodImpl(MethodImplOptions.Synchronized)]
            remove { this.m_StylusDownDelegate = (StylusDownEventHandler)Delegate.Remove(this.m_StylusDownDelegate, value); }
        }

        [NonSerialized] private StylusUpEventHandler m_StylusUpDelegate;
        public event StylusUpEventHandler StylusUp {
            [MethodImpl(MethodImplOptions.Synchronized)]
            add { this.m_StylusUpDelegate = (StylusUpEventHandler)Delegate.Combine(this.m_StylusUpDelegate, value); }
            [MethodImpl(MethodImplOptions.Synchronized)]
            remove { this.m_StylusUpDelegate = (StylusUpEventHandler)Delegate.Combine(this.m_StylusUpDelegate, value); }
        }

        [NonSerialized] private PacketsEventHandler m_PacketsDelegate;
        public event PacketsEventHandler Packets {
            [MethodImpl(MethodImplOptions.Synchronized)]
            add { this.m_PacketsDelegate = (PacketsEventHandler)Delegate.Combine(this.m_PacketsDelegate, value); }
            [MethodImpl(MethodImplOptions.Synchronized)]
            remove { this.m_PacketsDelegate = (PacketsEventHandler)Delegate.Combine(this.m_PacketsDelegate, value); }
        }

        public void OnStylusDown(int stylusId, int strokeId, int[] packets, TabletPropertyDescriptionCollection tabletProperties) {
            StylusDownEventHandler handler;
            using(Synchronizer.Lock(this)) {
                handler = (this.m_DrawingAttributes != null) ? this.m_StylusDownDelegate : null;
            }

            if(handler != null)
                handler(this, stylusId, strokeId, packets, tabletProperties);
        }

        public void OnStylusUp(int stylusId, int strokeId, int[] packets) {
            StylusUpEventHandler handler;
            using(Synchronizer.Lock(this)) {
                handler = (this.m_DrawingAttributes != null) ? this.m_StylusUpDelegate : null;
            }

            if(handler != null)
                handler(this, stylusId, strokeId, packets);
        }

        public void OnPackets(int stylusId, int strokeId, int[] packets) {
            PacketsEventHandler handler;
            using(Synchronizer.Lock(this)) {
                handler = (this.m_DrawingAttributes != null) ? this.m_PacketsDelegate : null;
            }

            if(handler != null)
                handler(this, stylusId, strokeId, packets);
        }

        public delegate void StylusDownEventHandler(object sender, int stylusId, int strokeId, int[] packets, TabletPropertyDescriptionCollection tabletProperties);
        public delegate void StylusUpEventHandler(object sender, int stylusId, int strokeId, int[] packets);
        public delegate void PacketsEventHandler(object sender, int stylusId, int strokeId, int[] packets);
    }
}
