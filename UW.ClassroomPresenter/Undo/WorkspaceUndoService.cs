// $Id: WorkspaceUndoService.cs 975 2006-06-28 01:02:59Z pediddle $

using System;
using System.Collections;
using System.Diagnostics;
using System.Threading;

using Microsoft.Ink;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Model.Undo;
using UW.ClassroomPresenter.Model.Workspace;

namespace UW.ClassroomPresenter.Undo {

    public class WorkspaceUndoService : IDisposable {
        private readonly PresenterModel m_Model;
        private readonly DeckTraversalsCollectionHelper m_DeckTraversalsCollectionHelper;

        private readonly EventQueue m_EventQueue;

        private bool m_Disposed;


        #region Construction & Destruction

        public WorkspaceUndoService(EventQueue dispatcher, PresenterModel model) {
            this.m_EventQueue = dispatcher;
            this.m_Model = model;
            this.m_DeckTraversalsCollectionHelper = new DeckTraversalsCollectionHelper(this);
        }

        ~WorkspaceUndoService() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            if(disposing) {
                this.m_DeckTraversalsCollectionHelper.Dispose();
            }
            this.m_Disposed = true;
        }

        #endregion Construction & Destruction


        private class DeckTraversalsCollectionHelper : CollectionMirror<DeckTraversalModel, DeckUndoService> {
            private readonly WorkspaceUndoService m_Service;

            public DeckTraversalsCollectionHelper(WorkspaceUndoService service) : base(service.m_EventQueue, service.m_Model.Workspace.DeckTraversals) {
                this.m_Service = service;

                base.Initialize(true);
            }

            protected override void Dispose(bool disposing) {
                if(disposing)
                    foreach (DeckUndoService disposable in this.Tags)
                        disposable.Dispose();
                base.Dispose(disposing);
            }

            protected override bool SetUp(int index, DeckTraversalModel member, out DeckUndoService tag) {
                // TODO: Create a DeckTraversalUndoService if it is desirable to undo any properties of DeckTraversalModel.
                tag = new DeckUndoService(this.m_Service.m_EventQueue, this.m_Service.m_Model.Undo, member.Deck);
                return true;
            }

            protected override void TearDown(int index, DeckTraversalModel member, DeckUndoService tag) {
                tag.Dispose();
            }
        }
    }
}
