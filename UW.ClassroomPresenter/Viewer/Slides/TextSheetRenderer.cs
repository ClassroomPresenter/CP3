// $Id: TextSheetRenderer.cs 1739 2008-09-04 17:01:44Z lamphare $

using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Model.Viewer;
using UW.ClassroomPresenter.Viewer.Text;
using UW.ClassroomPresenter.Viewer;

namespace UW.ClassroomPresenter.Viewer.Slides {
    public class TextSheetRenderer : SheetRenderer {
        private readonly TextSheetModel m_Sheet;
        private bool m_Disposed;
        
        ///The following code is for handling locking
        /// <summary>
        /// Listener for the SlideDisplay.PixelTransform property.
        /// </summary>
        private readonly EventQueue.PropertyEventDispatcher repaint_dispatcher_;

        public TextSheetRenderer(SlideDisplayModel display, TextSheetModel sheet) : base(display, sheet) {
            
            this.m_Sheet = sheet;

            repaint_dispatcher_ = new EventQueue.PropertyEventDispatcher(SlideDisplay.EventQueue, this.Repaint);
            /// Add event listeners
            this.m_Sheet.Changed["Text"].Add(new PropertyEventHandler(this.repaint_dispatcher_.Dispatcher));
            this.m_Sheet.Changed["Font"].Add(new PropertyEventHandler(this.repaint_dispatcher_.Dispatcher));
            this.m_Sheet.Changed["Bounds"].Add(new PropertyEventHandler(this.repaint_dispatcher_.Dispatcher));
            this.m_Sheet.Changed["Color"].Add(new PropertyEventHandler(this.repaint_dispatcher_.Dispatcher));
            this.SlideDisplay.Changed["Slide"].Add(new PropertyEventHandler(this.repaint_dispatcher_.Dispatcher));
            
        }

        #region Disposing
        protected override void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            try {
                if(disposing) {
                    this.m_Sheet.Changed["Text"].Remove(new PropertyEventHandler(this.repaint_dispatcher_.Dispatcher));
                    this.m_Sheet.Changed["Font"].Remove(new PropertyEventHandler(this.repaint_dispatcher_.Dispatcher));
                    this.m_Sheet.Changed["Bounds"].Remove(new PropertyEventHandler(this.repaint_dispatcher_.Dispatcher));
                    this.m_Sheet.Changed["Color"].Remove(new PropertyEventHandler(this.repaint_dispatcher_.Dispatcher));
                    this.m_Sheet.Changed["IsPublic"].Remove(new PropertyEventHandler(this.repaint_dispatcher_.Dispatcher));
                    this.SlideDisplay.Changed["Slide"].Remove(new PropertyEventHandler(this.repaint_dispatcher_.Dispatcher));
                    this.SlideDisplay.Invalidate();
                }
            } finally {
                base.Dispose(disposing);
            }
            this.m_Disposed = true;
        }
        #endregion

        #region Painting
        /// <summary>
        /// Paints the text of the textsheetmodel onto the slide using the DrawString method.
        /// </summary>
        /// <param name="args"></param>
        public override void Paint(PaintEventArgs args) {
            using (Synchronizer.Lock(this.SlideDisplay.SyncRoot)) {
                if (!m_Sheet.Visible) {
                    return;
                }
            }
            Graphics g = args.Graphics;
            using (Synchronizer.Lock(this.SlideDisplay.SyncRoot)) {
                ///transform what we will paint so that it will fit nicely into our slideview
                g.Transform = this.SlideDisplay.PixelTransform;
            }
            using (Synchronizer.Lock(this.m_Sheet.SyncRoot)) {
                Font f = m_Sheet.Font;
                Brush b = new SolidBrush(m_Sheet.Color);
                bool sheet_is_editable = m_Sheet.IsEditable;

                g.DrawString(this.m_Sheet.Text, f, b, this.m_Sheet.Bounds);
            }
        }
        #endregion

        #region Event Listening
        private void Repaint(object sender, PropertyEventArgs args)
        {
            this.SlideDisplay.Invalidate();
        }
        #endregion
    }
}
