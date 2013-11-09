// $Id: SheetRenderer.cs 1824 2009-03-10 23:47:34Z lining $

using System;
using System.Collections;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Model.Viewer;

namespace UW.ClassroomPresenter.Viewer.Slides {
    public abstract class SheetRenderer : IDisposable {
        private readonly SlideDisplayModel m_SlideDisplay;
        private readonly SheetModel m_Sheet;
        private bool m_Disposed;

        protected SheetRenderer(SlideDisplayModel display, SheetModel sheet) {
            this.m_SlideDisplay = display;
            this.m_Sheet = sheet;
        }

        ~SheetRenderer() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            if(disposing) {
            }
            this.m_Disposed = true;
        }

        public static SheetRenderer ForStaticSheet( SlideDisplayModel display, SheetModel sheet ) {
            // TODO: Make this pluggable by dynamically loading available renderers, mapped by the types they support.
            if(sheet is InkSheetModel) {
                return new InkSheetRenderer(display, (InkSheetModel) sheet);
            } else if(sheet is ImageSheetModel) {
                return new ImageSheetRenderer(display, (ImageSheetModel) sheet);
            } else if(sheet is TextSheetModel) {
                return new TextSheetRenderer(display, (TextSheetModel) sheet);
            } else if( sheet is QuickPollSheetModel ) {
                return new QuickPollSheetRenderer( display, (QuickPollSheetModel)sheet );
            } else if (sheet is XPSPageSheetModel)  {
                return new XPSPageRenderer(display, (XPSPageSheetModel)sheet);
            }
            else
            {
                return null;
            }
        }

        public static SheetRenderer ForSheet(SlideDisplayModel display, SheetModel sheet) {
            // TODO: Make this pluggable by dynamically loading available renderers, mapped by the types they support.
            if(sheet is InkSheetModel) {
                if(sheet is RealTimeInkSheetModel) {
                    return new RealTimeInkSheetRenderer(display, (RealTimeInkSheetModel) sheet);
                } else {
                    return new InkSheetRenderer(display, (InkSheetModel) sheet);
                }
            } else if(sheet is ImageSheetModel) {
                return new ImageSheetRenderer(display, (ImageSheetModel) sheet);
            } else if(sheet is TextSheetModel) {
                return new TextSheetRenderer(display, (TextSheetModel) sheet);
            } else if( sheet is QuickPollSheetModel ) {
                return new QuickPollSheetRenderer( display, (QuickPollSheetModel)sheet );
            } else if(sheet is XPSPageSheetModel) {
                return new XPSPageRenderer(display,(XPSPageSheetModel) sheet);
            } else {
                return null;
            }
        }

        public virtual SheetModel Sheet {
            get { return this.m_Sheet; }
        }

        public SlideDisplayModel SlideDisplay {
            get { return this.m_SlideDisplay; }
        }


        public abstract void Paint(PaintEventArgs args);
    }


    public class SheetRenderersCollection : CollectionBase {
        public SheetRenderersCollection() {}


        public SheetRenderer this[int index] {
            get { return ((SheetRenderer) List[index]); }
            set { List[index] = value; }
        }

        public int Add(SheetRenderer value) {
            
            return List.Add(value);
        }

        public int IndexOf(SheetRenderer value) {
            return List.IndexOf(value);
        }

        public void Insert(int index, SheetRenderer value) {
            List.Insert(index, value);
        }

        public void Remove(SheetRenderer value) {
            List.Remove(value);
        }

        public bool Contains(SheetRenderer value) {
            return List.Contains(value);
        }

        protected override void OnValidate(Object value) {
            if(!typeof(SheetRenderer).IsInstanceOfType(value))
                throw new ArgumentException("Value must be of type SheetRenderer.", "value");
        }
    }
}
