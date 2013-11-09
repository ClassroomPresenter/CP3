// $Id: UndoModel.cs 737 2005-09-09 00:07:19Z pediddle $

using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Threading;

using UW.ClassroomPresenter.Model;

namespace UW.ClassroomPresenter.Model.Undo {

    public class UndoModel {
        private readonly ArrayList m_UndoStack; // of IUndoers
        private readonly ArrayList m_RedoStack; // of IUndoers

        public UndoModel() {
            this.m_UndoStack = new ArrayList();
            this.m_RedoStack = new ArrayList();
        }

        public void Push(IUndoer undoer) {
            this.VerifyWriterLock();

            this.m_UndoStack.Add(undoer);

            // If action A is undone, and action B is performed, then action A is no longer valid.
            ClearStack(this.m_RedoStack, undoer.Actors);

            if(this.Update != null)
                this.Update(this, EventArgs.Empty);
        }

        public bool IsUndoable(object[] actors) {
            Synchronizer.AssertLockIsHeld(this.SyncRoot);

            for(int i = this.m_UndoStack.Count; --i >= 0; )
                if(ActorsMatch(actors, ((IUndoer) this.m_UndoStack[i]).Actors))
                    return true;
            return false;
        }

        public bool IsRedoable(object[] actors) {
            for(int i = this.m_RedoStack.Count; --i >= 0; )
                if(ActorsMatch(actors, ((IUndoer) this.m_RedoStack[i]).Actors))
                    return true;
            return false;
        }

        public void UndoOne(object[] actors) {
            this.VerifyWriterLock();

            try {
                for(int i = this.m_UndoStack.Count; --i >= 0; ) {
                    if(!ActorsMatch(actors, ((IUndoer) this.m_UndoStack[i]).Actors))
                        continue;

                    IUndoer undoer = ((IUndoer) this.m_UndoStack[i]);
                    this.m_UndoStack.RemoveAt(i);

                    try {
                        undoer.Undo();
                    } catch {
                        ClearStack(this.m_UndoStack, actors);
                        throw;
                    }

                    this.m_RedoStack.Add(undoer);

                    return;
                }
            } finally {
                if(this.Update != null)
                    this.Update(this, EventArgs.Empty);
            }

            throw new InvalidOperationException("Cannot undo unless IsUndoable returns true.");
        }

        public void RedoOne(object[] actors) {
            this.VerifyWriterLock();

            try {
                for(int i = this.m_RedoStack.Count; --i >= 0; ) {
                    if(!ActorsMatch(actors, ((IUndoer) this.m_RedoStack[i]).Actors))
                        continue;

                    IUndoer undoer = ((IUndoer) this.m_RedoStack[i]);
                    this.m_RedoStack.RemoveAt(i);

                    try {
                        undoer.Redo();
                    } catch {
                        ClearStack(this.m_RedoStack, actors);
                        throw;
                    }

                    this.m_UndoStack.Add(undoer);

                    return;
                }
            } finally {
                if(this.Update != null)
                    this.Update(this, EventArgs.Empty);
            }

            throw new InvalidOperationException("Cannot undo unless IsUndoable returns true.");
        }

        public object SyncRoot {
            get { return this; }
        }

        private void VerifyWriterLock() {
            Synchronizer.AssertLockIsHeld(this.SyncRoot, "A lock must be aquired on the UndoModel in order to execute any action.");
        }

        private static void ClearStack(ArrayList stack, object[] actors) {
            for(int i = stack.Count; --i >= 0; ) {
                if(ActorsMatch(actors, ((IUndoer) stack[i]).Actors))
                    stack.RemoveAt(i);
            }
        }

        private static bool ActorsMatch(object[] left, object[] right) {
            if(left == null || left.Length == 0) return true;
            if(right == null || right.Length == 0) return true;
            foreach(object actor in left)
                if(actor != null)
                    if(Array.IndexOf(right, actor) >= 0)
                        return true;
            return false;
        }

        public event EventHandler Update;
    }

    public interface IUndoer {
        void Undo();
        void Redo();

        /// <summary>
        /// Gets the set of objects the undoable action will modify,
        /// or on which the action depends.
        /// </summary>
        /// <remarks>
        /// The <see cref="UndoModel"/> can filter its list of <see cref="IUndoers"/>
        /// based on this property.
        /// </remarks>
        object[] Actors { get; }
    }

    public class PropertyChangeUndoer : IUndoer {
        private readonly object m_Sender;
        private readonly PropertyChangeEventArgs m_Change;

        public PropertyChangeUndoer(object sender, PropertyChangeEventArgs args) {
            this.m_Sender = sender;
            this.m_Change = args;
        }

        public void Undo() {
            this.SetProperty(this.m_Change.OldValue);
        }

        public void Redo() {
            this.SetProperty(this.m_Change.NewValue);
        }

        public object[] Actors {
            get { return new object[] { this.m_Sender, this.m_Change.NewValue, this.m_Change.OldValue }; }
        }

        private void SetProperty(object value) {
            Type t = this.m_Sender.GetType();
            PropertyInfo prop = t.GetProperty(this.m_Change.Property);
            prop.SetValue(this.m_Sender, value, null);
        }
    }
}
