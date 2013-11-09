// $Id: PreviewTraversalModelAdater.cs 975 2006-06-28 01:02:59Z pediddle $

using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Windows.Forms;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Workspace;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Viewer.Slides;

namespace UW.ClassroomPresenter.Viewer.FilmStrips {
    public class PreviewTraversalModelAdapter : IDisposable {
        private readonly EventQueue m_EventQueue;
        private readonly IDisposable m_CurrentSlidePreviewDeckTraversalChangedDispatcher;
        private readonly PresenterModel m_Model;
        private readonly SlideViewer m_Viewer;

        private DeckTraversalModel m_CurrentSlidePreviewDeckTraversal;
        private DeckTraversalModelAdapter m_Adapter;
        private bool m_Disposed;

        public PreviewTraversalModelAdapter(ControlEventQueue dispatcher, SlideViewer viewer, PresenterModel model) {
            this.m_EventQueue = dispatcher;
            this.m_Model = model;
            this.m_Viewer = viewer;

            this.m_CurrentSlidePreviewDeckTraversalChangedDispatcher =
                this.m_Model.Workspace.CurrentSlidePreviewDeckTraversal.ListenAndInitialize(dispatcher,
                delegate(Property<DeckTraversalModel>.EventArgs args) {
                    this.CurrentSlidePreviewDeckTraversal = args.New;
                });
        }

        ~PreviewTraversalModelAdapter() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            if(disposing) {
                this.m_CurrentSlidePreviewDeckTraversalChangedDispatcher.Dispose();
                // Unregister event listeners via the CurrentSlidePreviewDeckTraversal setter.
                this.CurrentSlidePreviewDeckTraversal = null;
            }
            this.m_Disposed = true;
        }

        private DeckTraversalModel CurrentSlidePreviewDeckTraversal {
            get { return this.m_CurrentSlidePreviewDeckTraversal; }
            set {
                if(this.m_CurrentSlidePreviewDeckTraversal != null) {
                    Debug.Assert(this.m_Adapter != null);
                    this.m_Adapter.Dispose();
                    this.m_Adapter = null;
                }

                this.m_CurrentSlidePreviewDeckTraversal = value;

                if(this.m_CurrentSlidePreviewDeckTraversal != null) {
                    Debug.Assert(this.m_Adapter == null);
                    this.m_Adapter = new DeckTraversalModelAdapter(this.m_EventQueue, this.m_Viewer, this.m_CurrentSlidePreviewDeckTraversal);
                }
                else {
                    this.m_Viewer.Slide = null;
                }
            }
        }
    }
}
