// $Id: SheetUndoService.cs 737 2005-09-09 00:07:19Z pediddle $

using System;
using System.Collections;
using System.Diagnostics;
using System.Threading;

using Microsoft.Ink;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Model.Undo;

namespace UW.ClassroomPresenter.Undo {

    public abstract class SheetUndoService : IDisposable {
        private readonly UndoModel m_Undo;
        private readonly DeckModel m_Deck;
        private readonly SlideModel m_Slide;
        private readonly SheetModel m_Sheet;

        private bool m_Disposed;


        #region Construction & Destruction

        protected SheetUndoService(UndoModel undo, DeckModel deck, SlideModel slide, SheetModel sheet) {
            this.m_Undo = undo;
            this.m_Deck = deck;
            this.m_Slide = slide;
            this.m_Sheet = sheet;

            this.m_Sheet.Changed["Bounds"].Add(new PropertyEventHandler(this.HandleSheetChanged));
        }

        ~SheetUndoService() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            if(disposing) {
                this.m_Sheet.Changed["Bounds"].Remove(new PropertyEventHandler(this.HandleSheetChanged));
            }
            this.m_Disposed = true;
        }

        #endregion Construction & Destruction


        protected SheetModel Sheet {
            get { return this.m_Sheet; }
        }

        protected DeckModel Deck {
            get { return this.m_Deck; }
        }

        protected SlideModel Slide {
            get { return this.m_Slide; }
        }

        protected UndoModel Undo {
            get { return this.m_Undo; }
        }

        private void HandleSheetChanged(object sender, PropertyEventArgs args) {
            // FIXME: Create an IUndoer to undo the change.
        }
    }
}
