// $Id: SlideUndoService.cs 1022 2006-07-14 01:13:24Z pediddle $

using System;
using System.Collections;
using System.Diagnostics;
using System.Threading;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Model.Undo;

namespace UW.ClassroomPresenter.Undo {
    public class SlideUndoService : IDisposable {
        private readonly UndoModel m_Undo;
        private readonly DeckModel m_Deck;
        private readonly SlideModel m_Slide;

        private readonly EventQueue m_EventQueue;
        private readonly EventQueue.PropertyEventDispatcher m_SlideChangedDispatcher;

        private readonly SheetsCollectionHelper m_ContentSheetsCollectionHelper;
        private readonly SheetsCollectionHelper m_AnnotationSheetsCollectionHelper;

        private bool m_Disposed;


        #region Construction & Destruction

        public SlideUndoService(EventQueue dispatcher, UndoModel undo, DeckModel deck, SlideModel slide) {
            this.m_EventQueue = dispatcher;
            this.m_Undo = undo;
            this.m_Deck = deck;
            this.m_Slide = slide;

            this.m_SlideChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandleSlideChanged));
            this.m_Slide.Changed["Title"].Add(this.m_SlideChangedDispatcher.Dispatcher);
            this.m_Slide.Changed["Bounds"].Add(this.m_SlideChangedDispatcher.Dispatcher);
            this.m_Slide.Changed["ContentSheets"].Add(this.m_SlideChangedDispatcher.Dispatcher);
            this.m_Slide.Changed["AnnotationSheets"].Add(this.m_SlideChangedDispatcher.Dispatcher);

            this.m_ContentSheetsCollectionHelper = new SheetsCollectionHelper(this, "ContentSheets");
            this.m_AnnotationSheetsCollectionHelper = new SheetsCollectionHelper(this, "AnnotationSheets");
        }

        ~SlideUndoService() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            if(disposing) {
                this.m_Slide.Changed["Title"].Remove(this.m_SlideChangedDispatcher.Dispatcher);
                this.m_Slide.Changed["Bounds"].Remove(this.m_SlideChangedDispatcher.Dispatcher);
                this.m_Slide.Changed["ContentSheets"].Remove(this.m_SlideChangedDispatcher.Dispatcher);
                this.m_Slide.Changed["AnnotationSheets"].Remove(this.m_SlideChangedDispatcher.Dispatcher);

                this.m_ContentSheetsCollectionHelper.Dispose();
                this.m_AnnotationSheetsCollectionHelper.Dispose();
            }
            this.m_Disposed = true;
        }

        #endregion Construction & Destruction


        private void HandleSlideChanged(object sender, PropertyEventArgs args) {
            // FIXME: Create an IUndoable to undo the change.
        }


        private class SheetsCollectionHelper : PropertyCollectionHelper {
            private readonly SlideUndoService m_UndoService;

            public SheetsCollectionHelper(SlideUndoService watcher, string property) : base(watcher.m_EventQueue, watcher.m_Slide, property) {
                this.m_UndoService = watcher;

                base.Initialize();
            }

            protected override void Dispose(bool disposing) {
                if (disposing)
                    foreach (IDisposable disposable in this.Tags)
                        if (disposable != null)
                            disposable.Dispose();
                base.Dispose(disposing);
            }

            protected override object SetUpMember(int index, object member) {
                if(member is ImageSheetModel) {
                    return new ImageSheetUndoService(this.m_UndoService.m_EventQueue, this.m_UndoService.m_Undo, this.m_UndoService.m_Deck, this.m_UndoService.m_Slide, ((ImageSheetModel) member));
                }

                else if(member is InkSheetModel) {
                    return new InkSheetUndoService(this.m_UndoService.m_EventQueue, this.m_UndoService.m_Undo, this.m_UndoService.m_Deck, this.m_UndoService.m_Slide, ((InkSheetModel) member));
                }

                else if(member is TextSheetModel) {
                    return new TextSheetUndoService(this.m_UndoService.m_EventQueue, this.m_UndoService.m_Undo, this.m_UndoService.m_Deck, this.m_UndoService.m_Slide, ((TextSheetModel) member));
                }

                else return null;
            }

            protected override void TearDownMember(int index, object member, object tag) {
                if(tag != null)
                    ((IDisposable) tag).Dispose();
            }
        }
    }
}
