// $Id: TableOfContentsEntryMessages.cs 1871 2009-05-26 19:29:56Z cmprince $

using System;
using System.Collections;
using System.Diagnostics;
using System.Threading;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;

namespace UW.ClassroomPresenter.Network.Messages.Presentation {
    [Serializable]
    public class TableOfContentsEntryMessage : Message {
        public readonly int[] PathFromRoot;

        public TableOfContentsEntryMessage(TableOfContentsModel.Entry entry) : base(entry.Id) {
            this.AddLocalRef( entry );
            using(Synchronizer.Lock(entry.SyncRoot)) {
                int[] path = entry.PathFromRoot;
                if(path.Length < 1)
                    throw new ArgumentException("The entry's path is invalid; it must contain at least one index.", "path");
                this.PathFromRoot = ((int[]) path.Clone());
            }
        }

        protected override bool UpdateTarget(ReceiveContext context) {
            SlideModel slide = this.Parent != null ? this.Parent.Target as SlideModel : null;
            if(slide == null)
                return false;

            DeckModel deck = this.Parent.Parent != null ? this.Parent.Parent.Target as DeckModel : null;
            if(deck == null)
                return false;

            TableOfContentsModel.Entry entry = this.Target as TableOfContentsModel.Entry;
            if(entry == null)
                this.Target = entry = new TableOfContentsModel.Entry(((Guid) this.TargetId), deck.TableOfContents, slide);

            this.UpdatePathFromRoot(entry);

            return true;
        }

        /// <summary>
        /// If the entry's <see cref="Entry.PathFromRoot"><c>PathFromRoot</c></see> differs from that of
        /// the <see cref="EntryInformation"/> record, this method removes and re-adds the entry into
        /// the correct location in its <see cref="TableOfContentsModel"/>.
        /// </summary>
        /// <remarks>
        /// This method is invoked by <see cref="UpdateEntry"/>, but <em>not</em> by <see cref="CreateEntry"/>.
        /// The reasoning is that <see cref="CreateEntry"/> does nothing other than instantiating the
        /// <see cref="TableOfContentsModel.Entry"/> object, allowing for other processing between
        /// the time that the entry is instantiated and when it is inserted into the <see cref="TableOfContentsModel"/>.
        /// </remarks>
        /// <param name="entry">The entry to update.</param>
        public void UpdatePathFromRoot(TableOfContentsModel.Entry entry) {
            using(Synchronizer.Lock(entry.SyncRoot)) {
                int[] old = entry.PathFromRoot;
                int[] path = this.PathFromRoot;

                bool changed = (old.Length != path.Length);
                for(int i = 0; !changed && i < path.Length; i++)
                    changed = (old[i] != path[i]);

                if(changed) {
                    // Remove the entry from its current parent, if any.
                    if(entry.Parent == null) {
                        if(entry.TableOfContents.Entries.Contains(entry)) {
                            entry.TableOfContents.Entries.Remove(entry);
                        }
                    } else {
                        entry.Parent.Children.Remove(entry);
                    }

                    // Insert the entry as a child of its new parent,
                    // whether that be as a root entry of the TableOfContents
                    // or as a child of some other entry.
                    Debug.Assert(path.Length >= 1);

                    if(path.Length == 1) {
                        int index = Math.Min(entry.TableOfContents.Entries.Count, Math.Max(0, path[0]));
                        //Natalie
//   entry.TableOfContents.Entries.Insert(index, entry);
                        //entry.index = path[0];
                        entry.TableOfContents.Entries.BroadcastInsert(path[0], entry);
                    } else {
                        int[] parentPath = new int[path.Length - 1];
                        Array.Copy(path, 0, parentPath, 0, parentPath.Length);
                        TableOfContentsModel.Entry parent = entry.TableOfContents[parentPath];
                        // FIXME: Handle the case when no parent at the specified index exists.
                        int index = Math.Min(parent.Children.Count, Math.Max(0, path[path.Length - 1]));
                        //Natalie - what is this?
                        parent.Children.Insert(index, entry);
                    }
                }
            }
        }

        #region IGenericSerializable

        public override SerializedPacket Serialize() {
            SerializedPacket p = base.Serialize();
            p.Add( (this.PathFromRoot != null) ?
                SerializedPacket.SerializeIntArray( this.PathFromRoot ) : SerializedPacket.NullPacket( PacketTypes.IntArrayId ) );
            return p;
        }

        public TableOfContentsEntryMessage( Message parent, SerializedPacket p ) : base( parent, p ) {
            this.PathFromRoot = (!SerializedPacket.IsNullPacket( p.PeekNextPart() )) ?
                SerializedPacket.DeserializeIntArray( p.PeekNextPart() ) : null; p.GetNextPart();
        }

        public override int GetClassId() {
            return PacketTypes.TableOfContentsEntryMessageId;
        }

        #endregion
    }

    [Serializable]
    public class TableOfContentsEntryRemovedMessage : Message {
        public TableOfContentsEntryRemovedMessage(TableOfContentsModel.Entry entry) : base(entry.Id) {}

        protected override bool UpdateTarget(ReceiveContext context) {
            TableOfContentsModel.Entry entry = this.Target as TableOfContentsModel.Entry;
            if(entry == null)
                return false;

            ///get the deckmodel we'll be dealing with 
            ///I think this is a very ugly way of getting it.
            DeckModel deck =  (DeckModel)this.Parent.Parent.Target;

            // Locking the TableOfContents locks the entry, its parent, and everything else in the TOC.
            using(Synchronizer.Lock(deck.TableOfContents.SyncRoot)) {
                // We created the entry on the local side, so if the TOCs don't match, some message's OnReceive screwed up.
                // Similarly, if the entry is not a child of its parent, then it's the local OnReceive that screwed up.
                Debug.Assert(entry.TableOfContents == deck.TableOfContents);

                TableOfContentsModel.Entry parent = entry.Parent;
                // Note: The entry might not be a child of its parent if network messages are processed out of order,
                // or if weird things happen like a DeckTraversalMessage causing an entry to get created before it's
                // added to the table of contents.
                if(parent != null) {
                    using(Synchronizer.Lock(parent.SyncRoot)) {
                        foreach(TableOfContentsModel.Entry child in parent.Children)
                            if(child.Id.Equals(this.TargetId))
                                parent.Children.BroadcastRemove(child);
                    }
                } else if(deck != null) {
                    ///remove the TOC entry we need to remove by iterating through all the TOC entries
                    ///and comparing using a backwards loop.
                    using(Synchronizer.Lock(deck.TableOfContents.SyncRoot)) {
                        for (int i = deck.TableOfContents.Entries.Count - 1; i >= 0; i--) {
                            TableOfContentsModel.Entry child = deck.TableOfContents.Entries[i];
                            if(child.Id.Equals(this.TargetId))
                                deck.TableOfContents.Entries.BroadcastRemoveAt(i);
                            
                        }
                    }
                }
            }

            return false;
        }

        #region IGenericSerializable

        public TableOfContentsEntryRemovedMessage( Message parent, SerializedPacket p ) : base( parent, p ) {
        }

        public override int GetClassId() {
            return PacketTypes.TableOfContentsEntryRemovedMessageId;
        }

        #endregion
    }
}
