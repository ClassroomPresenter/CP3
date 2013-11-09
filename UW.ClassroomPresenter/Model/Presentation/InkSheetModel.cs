// $Id: InkSheetModel.cs 1588 2008-04-05 17:35:59Z cmprince $

using System;
using System.Drawing;
using Microsoft.Ink;
using System.Runtime.Serialization;
using System.Runtime.CompilerServices;

namespace UW.ClassroomPresenter.Model.Presentation {
    [Serializable]
    public class InkSheetModel : SheetModel {
        private readonly SerializableInk m_SerializableInk;

        [NonSerialized]
        private Strokes m_Selection;

        public InkSheetModel(Guid id, SheetDisposition disposition, Rectangle bounds) : base(id, disposition, bounds, 0) {
            this.m_SerializableInk = new SerializableInk();
        }

        public InkSheetModel( Guid id, SheetDisposition disposition, Rectangle bounds, Ink ink )
            : base( id, disposition, bounds, 0 ) {
            this.m_SerializableInk = new SerializableInk( ink );
        }

        public Ink Ink {
            get { return this.m_SerializableInk.m_Ink; }
        }

        [Published] public Strokes Selection {
            get { return this.GetPublishedProperty("Selection", ref this.m_Selection); }
            set { this.SetPublishedProperty("Selection", ref this.m_Selection, value); }
        }

        [NonSerialized] private StrokesEventHandler m_InkAddedDelegate;
        public event StrokesEventHandler InkAdded {
            [MethodImpl(MethodImplOptions.Synchronized)]
            add { this.m_InkAddedDelegate = (StrokesEventHandler)Delegate.Combine(this.m_InkAddedDelegate, value); }
            [MethodImpl(MethodImplOptions.Synchronized)]
            remove { this.m_InkAddedDelegate = (StrokesEventHandler)Delegate.Remove(this.m_InkAddedDelegate, value); }
        }

        [NonSerialized] private StrokesEventHandler m_InkDeletingDelegate;
        public event StrokesEventHandler InkDeleting {
            [MethodImpl(MethodImplOptions.Synchronized)]
            add { this.m_InkDeletingDelegate = (StrokesEventHandler)Delegate.Combine(this.m_InkDeletingDelegate, value); }
            [MethodImpl(MethodImplOptions.Synchronized)]
            remove { this.m_InkDeletingDelegate = (StrokesEventHandler)Delegate.Remove(this.m_InkDeletingDelegate, value); }
        }

        [NonSerialized] private StrokesEventHandler m_InkDeletedDelegate;
        public event StrokesEventHandler InkDeleted {
            [MethodImpl(MethodImplOptions.Synchronized)]
            add { this.m_InkDeletedDelegate = (StrokesEventHandler)Delegate.Combine(this.m_InkDeletedDelegate, value); }
            [MethodImpl(MethodImplOptions.Synchronized)]
            remove { this.m_InkDeletedDelegate = (StrokesEventHandler)Delegate.Remove(this.m_InkDeletedDelegate, value); }
        }

        public void OnInkAdded(StrokesEventArgs args) {
            // The default event add/remove methods lock(this), so we need to do the same to make sure the delegates don't change.
            using(Synchronizer.Lock(this)) {
                if(this.m_InkAddedDelegate != null) {
                    this.m_InkAddedDelegate(this, args);
                }
            }
        }

        public void OnInkDeleting(StrokesEventArgs args) {
            // The default event add/remove methods lock(this), so we need to do the same to make sure the delegates don't change.
            using(Synchronizer.Lock(this)) {
                if(this.m_InkDeletingDelegate != null) {
                    this.m_InkDeletingDelegate(this, args);
                }
            }
        }

        public void OnInkDeleted(StrokesEventArgs args) {
            // The default event add/remove methods lock(this), so we need to do the same to make sure the delegates don't change.
            using(Synchronizer.Lock(this)) {
                if(this.m_InkDeletedDelegate != null) {
                    this.m_InkDeletedDelegate(this, args);
                }
            }
        }

        /// <summary>
        /// Performs a deep copy of the given SheetModel.
        /// If the given sheetmodel is not an InkSheet, then returns itself.
        /// </summary>
        /// <param name="s">The SheetModel to copy</param>
        /// <returns>The given sheetmodel if not an InkSheetModel, otherwise a deep copy of the InkSheetModel</returns>
        public static SheetModel InkSheetDeepCopyHelper( SheetModel s ) {
            using( Synchronizer.Lock( s.SyncRoot ) ) {
                InkSheetModel t;
                // Only copy InkSheetModels
                if( s is InkSheetModel ) {
                    if( s is RealTimeInkSheetModel ) {
                        // Make a deep copy of the SheetModel
                        t = new RealTimeInkSheetModel( Guid.NewGuid(), s.Disposition | SheetDisposition.Remote, s.Bounds, ((RealTimeInkSheetModel)s).Ink.Clone() );
                        using( Synchronizer.Lock( t.SyncRoot ) ) {
                            ((RealTimeInkSheetModel)t).CurrentDrawingAttributes = ((RealTimeInkSheetModel)s).CurrentDrawingAttributes;
                        }
                    } else {
                        // Make a deep copy of the SheetModel
                        t = new InkSheetModel( Guid.NewGuid(), s.Disposition, s.Bounds, ((InkSheetModel)s).Ink.Clone() );
                    }
                    // This is a new object so add it to the local references
                    // TODO CMPRINCE: Make this a callback or something
                    UW.ClassroomPresenter.Network.Messages.Message.AddLocalRef( t.Id, t );
                    return t;
                }
            }

            return s;
        }
    }

    [Serializable]
    public class SerializableInk : ISerializable {
        [NonSerialized] public readonly Ink m_Ink;

        public SerializableInk() {
            this.m_Ink = new Ink();
        }
        public SerializableInk( Ink ink ) {
            this.m_Ink = ink;
        }

        protected SerializableInk(SerializationInfo info, StreamingContext context) {
            byte[] serializedInk = (byte[])info.GetValue("Ink", typeof(byte[]));
            this.m_Ink = new Ink();
            this.m_Ink.Load(serializedInk);
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context) {
            byte[] serializedInk = this.m_Ink.Save();
            info.AddValue("Ink", serializedInk);
        }
    }
}
