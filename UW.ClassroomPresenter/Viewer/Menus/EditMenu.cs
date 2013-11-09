// $Id: EditMenu.cs 1766 2008-09-14 16:54:14Z lining $

using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Undo;
using UW.ClassroomPresenter.Model.Presentation;

using UW.ClassroomPresenter.Viewer.Slides;

namespace UW.ClassroomPresenter.Viewer.Menus {
    public class EditMenu : MenuItem {
        public EditMenu(ControlEventQueue dispatcher, PresenterModel model) {
            this.Text = Strings.Edit;

            this.MenuItems.Add(new UndoMenuItem(dispatcher, model));
            this.MenuItems.Add(new RedoMenuItem(dispatcher, model));
        }

        private class UndoMenuItem : MenuItem, DeckTraversalModelAdapter.IAdaptee {
            protected readonly PresenterModel m_Model;
            private readonly EventQueue m_EventQueue;
            private bool m_Disposed;
            protected object[] m_Actors;
            private readonly WorkspaceModelAdapter m_WorkspaceModelAdapter;

            public UndoMenuItem(ControlEventQueue dispatcher, PresenterModel model) {
                this.m_Model = model;
                this.m_EventQueue = dispatcher;

                this.Text = Strings.Undo;
                this.Shortcut = Shortcut.CtrlZ;
                this.ShowShortcut = true;

                this.m_Model.Undo.Update += new EventHandler(this.HandleUndoableChanged);
                this.m_WorkspaceModelAdapter = new WorkspaceModelAdapter(dispatcher, this, this.m_Model);
            }

            protected override void Dispose(bool disposing) {
                if(this.m_Disposed) return;
                try {
                    if(disposing) {
                        this.m_Model.Undo.Update -= new EventHandler(this.HandleUndoableChanged);
                        this.m_WorkspaceModelAdapter.Dispose();
                    }
                } finally {
                    base.Dispose(disposing);
                }
                this.m_Disposed = true;
            }

            protected override void OnClick(EventArgs e) {
                base.OnClick(e);
                this.OnClickImpl();
            }

            protected virtual void OnClickImpl() {
                using(Synchronizer.Lock(this.m_Model.Undo.SyncRoot)) {
                    if(this.m_Model.Undo.IsUndoable(this.m_Actors))
                        this.m_Model.Undo.UndoOne(this.m_Actors);
                }
            }

            private void HandleUndoableChanged(object sender, EventArgs e) {
                this.m_EventQueue.Post(new EventQueue.EventDelegate(this.HandleUndoStackChanged));
            }

            protected virtual void HandleUndoStackChanged() {
                using(Synchronizer.Lock(this.m_Model.Undo.SyncRoot)) {
                    this.Enabled = this.m_Model.Undo.IsUndoable(this.m_Actors);
                }
            }

            SlideModel DeckTraversalModelAdapter.IAdaptee.Slide {
                set {
                    using(Synchronizer.Lock(this.m_Model.Undo.SyncRoot)) {
                        this.m_Actors = new object[] { value };
                        this.HandleUndoableChanged(this, EventArgs.Empty);
                    }
                }
            }

            //Unused IAdaptee member
            System.Drawing.Color DeckTraversalModelAdapter.IAdaptee.DefaultDeckBGColor {
                set {}
            }
            UW.ClassroomPresenter.Model.Background.BackgroundTemplate DeckTraversalModelAdapter.IAdaptee.DefaultDeckBGTemplate {
                set { }
            }
        }

        private class RedoMenuItem : UndoMenuItem {
            public RedoMenuItem(ControlEventQueue dispatcher, PresenterModel model) : base(dispatcher, model) {
                this.Text = Strings.Redo;
                this.Shortcut = Shortcut.CtrlY;
                this.ShowShortcut = true;
            }

            protected override void OnClickImpl() {
                using(Synchronizer.Lock(this.m_Model.Undo.SyncRoot)) {
                    if(this.m_Model.Undo.IsRedoable(this.m_Actors))
                        this.m_Model.Undo.RedoOne(this.m_Actors);
                }
            }

            protected override void HandleUndoStackChanged() {
                using(Synchronizer.Lock(this.m_Model.Undo.SyncRoot)) {
                    this.Enabled = this.m_Model.Undo.IsRedoable(this.m_Actors);
                }
            }
        }
    }
}
