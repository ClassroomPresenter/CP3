// $Id: TextSheetUndoService.cs 776 2005-09-21 20:57:14Z pediddle $

using System;
using System.Collections;
using System.Diagnostics;
using System.Threading;

using Microsoft.Ink;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Model.Undo;

namespace UW.ClassroomPresenter.Undo {

    public class TextSheetUndoService : SheetUndoService {
        private bool m_Disposed;

        private readonly EventQueue m_EventQueue;
        private EventQueue.PropertyEventDispatcher m_SheetChangedDispatcher;

        public TextSheetUndoService(EventQueue dispatcher, UndoModel undo, DeckModel deck, SlideModel slide, TextSheetModel sheet) : base(undo, deck, slide, sheet) {
            this.m_EventQueue = dispatcher;
            this.m_SheetChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandleSheetChanged));

            //Ignore any sheets with the Remote flag
            if ((sheet.Disposition & SheetDisposition.Remote) == 0) {
                this.Sheet.Changed["Text"].Add(this.m_SheetChangedDispatcher.Dispatcher);
                this.Sheet.Changed["Font"].Add(this.m_SheetChangedDispatcher.Dispatcher);
            }
        }

        protected override void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            try {
                if(disposing) {
                    this.Sheet.Changed["Text"].Remove(this.m_SheetChangedDispatcher.Dispatcher);
                    this.Sheet.Changed["Font"].Remove(this.m_SheetChangedDispatcher.Dispatcher);
                }
            } finally {
                base.Dispose(disposing);
            }
            this.m_Disposed = true;
        }

        private void HandleSheetChanged(object sender, PropertyEventArgs args) {
            // FIXME: Create an IUndoer to undo the change.
        }
    }
}
