// $Id: DeckUndoService.cs 1022 2006-07-14 01:13:24Z pediddle $

using System;
using System.Collections;
using System.Diagnostics;
using System.Threading;

using Microsoft.Ink;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Model.Undo;

namespace UW.ClassroomPresenter.Undo {

    public class DeckUndoService : IDisposable {
        private readonly UndoModel m_Undo;
        private readonly DeckModel m_Deck;

        private readonly EventQueue m_EventQueue;
        private readonly EventQueue.PropertyEventDispatcher m_DeckChangedDispatcher;

        private readonly EntriesCollectionHelper m_EntriesCollectionHelper;

        private bool m_Disposed;


        #region Construction & Destruction

        public DeckUndoService(EventQueue dispatcher, UndoModel undo, DeckModel deck) {
            this.m_EventQueue = dispatcher;
            this.m_Undo = undo;
            this.m_Deck = deck;

            this.m_DeckChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandleDeckChanged));
            this.m_Deck.Changed["HumanName"].Add(this.m_DeckChangedDispatcher.Dispatcher);
            this.m_Deck.TableOfContents.Changed["Entries"].Add(this.m_DeckChangedDispatcher.Dispatcher);

            this.m_EntriesCollectionHelper = new EntriesCollectionHelper(this);
        }

        ~DeckUndoService() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            if(disposing) {
                this.m_Deck.Changed["HumanName"].Remove(this.m_DeckChangedDispatcher.Dispatcher);
                this.m_Deck.TableOfContents.Changed["Entries"].Remove(this.m_DeckChangedDispatcher.Dispatcher);

                this.m_EntriesCollectionHelper.Dispose();
            }
            this.m_Disposed = true;
        }

        #endregion Construction & Destruction


        public DeckModel Deck {
            get { return this.m_Deck; }
        }


        private void HandleDeckChanged(object sender, PropertyEventArgs args) {
            // FIXME: Create an IUndoer to undo the change.
        }


        private class EntriesCollectionHelper : PropertyCollectionHelper {
            private readonly DeckUndoService m_UndoService;

            public EntriesCollectionHelper(DeckUndoService watcher) : base(watcher.m_EventQueue, watcher.m_Deck.TableOfContents, "Entries") {
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
                TableOfContentsModel.Entry entry = ((TableOfContentsModel.Entry) member);
                if(entry.Slide != null)
                    return new SlideUndoService(this.m_UndoService.m_EventQueue, this.m_UndoService.m_Undo, this.m_UndoService.m_Deck, entry.Slide);
                else return null;
            }

            protected override void TearDownMember(int index, object member, object tag) {
                if(tag != null)
                    ((SlideUndoService) tag).Dispose();
            }
        }
    }
}
