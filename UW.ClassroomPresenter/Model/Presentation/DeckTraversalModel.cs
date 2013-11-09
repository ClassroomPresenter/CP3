// $Id: DeckTraversalModel.cs 1882 2009-06-08 21:55:30Z jing $

using System;
using System.Collections;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

namespace UW.ClassroomPresenter.Model.Presentation {
    [Serializable]
    public abstract class DeckTraversalModel : PropertyPublisher, IDisposable {
        private readonly DeckModel m_Deck;
        private readonly Guid m_Id;

        public DeckTraversalModel(Guid id, DeckModel deck) {
            this.m_Id = id;
            this.m_Deck = deck;
        }

        public Guid Id {
            get { return this.m_Id; }
        }

        public DeckModel Deck {
            get { return this.m_Deck; }
        }

        [Published]
        public abstract TableOfContentsModel.Entry Current { get; set; }
        [Published]
        public abstract TableOfContentsModel.Entry Next { get; }
        [Published]
        public abstract TableOfContentsModel.Entry Previous { get; }
        [Published]
        public abstract int AbsoluteCurrentSlideIndex { get; }

        ~DeckTraversalModel() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) { }
    }

    [Serializable]
    public abstract class DefaultDeckTraversalModel : DeckTraversalModel {
        private readonly EntryFilterDelegate m_Filter;

        private readonly Stack m_Parents; // Members are of type TableOfContentsModel.Entry.

        private TableOfContentsModel.Entry m_Current;
        private TableOfContentsModel.Entry m_Next;
        private TableOfContentsModel.Entry m_Previous;
        private int m_AbsCurrentSlideIndex;

        private readonly MethodInvoker m_HandleEntriesChangedHelperDelegate;
        private readonly AsyncCallback m_HandleEntriesChangedCallbackDelegate;

        private bool m_Disposed;

        protected DefaultDeckTraversalModel(Guid id, DeckModel deck, EntryFilterDelegate filter)
            : base(id, deck) {
            this.m_HandleEntriesChangedHelperDelegate = new MethodInvoker(this.HandleEntriesChangedHelper);
            this.m_HandleEntriesChangedCallbackDelegate = new AsyncCallback(this.HandleEntriesChangedCallback);

            this.m_AbsCurrentSlideIndex = -1;

            this.m_Parents = new Stack();
            this.m_Filter = filter;

            this.Deck.TableOfContents.Changed["Entries"].Add(new PropertyEventHandler(this.HandleEntriesChanged));

            using (Synchronizer.Lock(this.SyncRoot)) {
                using (Synchronizer.Lock(this.Deck.TableOfContents.SyncRoot)) {
                    this.Reset();
                }
            }

            this.HandleEntriesChanged(this.Deck.TableOfContents, null);
        }

        protected override void Dispose(bool disposing) {
            if (this.m_Disposed) return;
            try {
                if (disposing) {
                    this.Deck.TableOfContents.Changed["Entries"].Remove(new PropertyEventHandler(this.HandleEntriesChanged));
                }
            } finally {
                base.Dispose(disposing);
            }
            this.m_Disposed = true;
        }


        [Published]
        public override TableOfContentsModel.Entry Current {
            get { return this.GetPublishedProperty("Current", ref this.m_Current); }
            set {
                this.SetPublishedProperty("Current", ref this.m_Current, value);

                // Update the Next and Previous entries.
                this.UpdateEntries();
            }
        }

        [Published]
        public override TableOfContentsModel.Entry Next {
            get { return this.GetPublishedProperty("Next", ref this.m_Next); }
            // The setter is "protected"; see SetNext(TableOfContentsModel.Entry) below.
        }

        [Published]
        public override TableOfContentsModel.Entry Previous {
            get { return this.GetPublishedProperty("Previous", ref this.m_Previous); }
            // The setter is "protected"; see SetPrevious(TableOfContentsModel.Entry) below.
        }

        [Published]
        public override int AbsoluteCurrentSlideIndex {
            get { return this.GetPublishedProperty("AbsoluteCurrentSlideIndex", ref this.m_AbsCurrentSlideIndex); }
        }


        /// <summary>
        /// Sets the <see cref="Current">current entry</see> to the first applicable entry
        /// in the deck's table of contents, or <c>null</c> if the table of contents is empty.
        /// </summary>
        /// <remarks>
        /// The caller is required to aquire a reader lock on the <see cref="DeckModel#TableOfContents"/>.
        /// </remarks>
        private void Reset() {
            if (this.Deck.TableOfContents.Entries.Count > 0) {
                TableOfContentsModel.Entry entry = this.Deck.TableOfContents[0];
                this.Current = this.m_Filter(entry) ? entry : this.FindNext(entry);
            }
        }

        protected void SetNext(TableOfContentsModel.Entry value) {
            this.SetPublishedProperty("Next", ref this.m_Next, value);
        }

        protected void SetPrevious(TableOfContentsModel.Entry value) {
            this.SetPublishedProperty("Previous", ref this.m_Previous, value);
        }


        private void HandleEntriesChanged(object sender, PropertyEventArgs args) {
            // This event handler needs to acquire a reader lock on the TableOfContentsModel,
            // but the order in which the locks are acquired cannot be reconciled with other
            // parts of the application.  Delegating to a different thread allows any
            // unexpected locks to be released, avoiding deadlocks.
            this.m_HandleEntriesChangedHelperDelegate.BeginInvoke(this.m_HandleEntriesChangedCallbackDelegate, null);
        }

        private void HandleEntriesChangedCallback(IAsyncResult result) {
            this.m_HandleEntriesChangedHelperDelegate.EndInvoke(result);
        }

        private void HandleEntriesChangedHelper() {
            using (Synchronizer.Lock(this.SyncRoot)) {
                using (Synchronizer.Lock(this.Deck.TableOfContents.SyncRoot)) {
                    if (this.Current == null) {
                        // We don't have a current entry to start from, so start from the beginning.
                        this.Reset();
                    }

                    if (this.Current != null && !this.m_Filter(this.Current)) {
                        // The current entry is no longer be accepted by the filter.
                        // Find the next entry relative to the current, or the previous entry if there is no next.
                        TableOfContentsModel.Entry replacement = this.FindNext(this.Current);
                        this.Current = (replacement != null) ? replacement : this.FindPrevious(this.Current);

                        // Setting the current entry will have updated the Next and Previous properties,
                        // so there's nothing more to do.
                        return;
                    }

                    // Update the Next and Previous entries, in case the current Next or Previous
                    // entries changed so that they are no longer accepted by the filter.
                    // TODO: Only try to update Next and Previous if the changed entries are adjacent
                    //   to the current entry or one of its parents.  This may be beneficial if the
                    //   tree is very broad.
                    this.UpdateEntries();
                }
            }
        }


        /// <summary>
        /// Updates the <see cref="Next"/>, <see cref="Previous"/>, and <see cref="AbsoluteCurrentSlideIndex"/>
        /// relative to the <see cref="Current"/> entry.
        /// </summary>
        /// <remarks>
        /// This method is invoked whenever the <see cref="Current"/> entry is set (by the <see cref="Current"/>
        /// property setter).
        /// </remarks>
        private void UpdateEntries() {
            // If there is no current entry, set everything to null.
            if (this.Current == null) {
                this.SetNext(null);
                this.SetPrevious(null);
                this.SetPublishedProperty("AbsoluteCurrentSlideIndex", ref this.m_AbsCurrentSlideIndex, -1);
                return;
            }

            // Debugging to check that the entry belongs to the deck.
            // This is in response to a lock failure in getAbsoluteSlideIndex()
            // observed during testing on January 12, 2007.
            Debug.Assert(this.Current.TableOfContents == this.Deck.TableOfContents,
                    "Current TableOfContents.Entry does not belong to the current Deck.",
                        string.Format("DeckTraversalModel.Deck.Id = {0}\nDeckTraversalModel.Current.Id = {1}\nDeckTraversalModel.Current.TableOfContents.GetHashCode() = {2}\nDeckTraversalModel.Deck.TableOfContents.GetHashCode() = {3}", this.Deck.Id, this.Current.Id, this.Current.TableOfContents.GetHashCode(), this.Deck.TableOfContents.GetHashCode()));
            Debug.Assert(this.Current.SyncRoot == this.Deck.TableOfContents.SyncRoot,
                        "Error: Current TableOfContents.Entry.SyncRoot does not match that of the current deck's TableOfContents!",
                        string.Format("DeckTraversalModel.Deck.Id = {0}\nDeckTraversalModel.Current.Id = {1}\nDeckTraversalModel.Current.TableOfContents.GetHashCode() = {2}\nDeckTraversalModel.Deck.TableOfContents.GetHashCode() = {3}", this.Deck.Id, this.Current.Id, this.Current.TableOfContents.GetHashCode(), this.Deck.TableOfContents.GetHashCode()));

            using (Synchronizer.Lock(this.Current.SyncRoot)) {
                //For instructor role, mark the current slide Visited.
                if ((this.Current.Slide.Disposition & SlideDisposition.Remote) == 0) {
                    using (Synchronizer.Lock(this.Current.Slide.SyncRoot)) {
                        this.Current.Slide.Visited = true;
                    }
                }
                this.SetNext(this.FindNext(this.Current));
                this.SetPrevious(this.FindPrevious(this.Current));

                // Update the absolute current slide index.
                this.SetPublishedProperty("AbsoluteCurrentSlideIndex", ref this.m_AbsCurrentSlideIndex, this.getAbsoluteSlideIndex(this.Current));
            }
        }


        /// <summary>
        /// Finds the next entry, in pre-order relative to the specified entry, which is accepted by the entry filter.
        /// </summary>
        /// <remarks>
        /// The caller is required to hold a reader lock on the entry's <see cref="TableOfContentsModel"/>.
        /// </remarks>
        public TableOfContentsModel.Entry FindNext(TableOfContentsModel.Entry entry) {
            if (entry == null)
                return null;

            using (Synchronizer.Lock(entry.SyncRoot)) {
                // Pre-order traversal: we've already visited the "current" entry, so visit it's children next.
                if (entry.Children.Count > 0) {
                    TableOfContentsModel.Entry child = entry.Children[0];
                    if (this.m_Filter(child)) {
                        return child;
                    } else {
                        return this.FindNext(child);
                    }
                }

                // If there are no children, look for a next sibling, parent's sibling, etc.
                else {
                    TableOfContentsModel.Entry parent = entry.Parent;
                    TableOfContentsModel.EntryCollection siblings = (parent == null)
                        ? entry.TableOfContents.Entries
                        : parent.Children;

                    while (siblings != null) {
                        int index = siblings.IndexOf(entry);

                        // Use the entry's sibling within the parent, if possible.
                        if (index >= 0 && index < siblings.Count - 1) {
                            TableOfContentsModel.Entry sibling = siblings[index + 1];
                            if (this.m_Filter(sibling)) {
                                return sibling;
                            } else {
                                return this.FindNext(sibling);
                            }
                        }

                        // If there is no sibling in the parent, go up another level.
                            // Do not consider parent nodes in the pre-order traversal.
                        else {
                            entry = parent;
                            parent = (entry == null) ? null : entry.Parent;
                            siblings = (parent == null) ? null : parent.Children;
                        }
                    }

                    // If we get here, then we've run out of parents and there is no next entry!
                    return null;
                }
            }
        }


        /// <summary>
        /// Finds the next entry, relative to the specified entry, which is accepted by the entry filter.
        /// </summary>
        /// <remarks>
        /// The caller is required to hold a reader lock on the entry's <see cref="TableOfContentsModel"/>.
        /// </remarks>
        public TableOfContentsModel.Entry FindPrevious(TableOfContentsModel.Entry entry) {
            if (entry == null)
                return null;

            // Pre-order traversal: we start with the last descendent of the entry's previous sibling.
            using (Synchronizer.Lock(entry.SyncRoot)) {
                TableOfContentsModel.Entry parent = entry.Parent;
                TableOfContentsModel.EntryCollection siblings = (parent == null)
                    ? entry.TableOfContents.Entries
                    : parent.Children;

                int index = siblings.IndexOf(entry);

                // If there exists a previous sibling, we're in good shape.
                // (This also handles the case when the given entry is not a child of its parent,
                // which can happen when entries are received over the network in a strange order.)
                if (index > 0) {
                    entry = siblings[index - 1];

                    // Find the *last* descendent of the sibling by going down as far as possible.
                    siblings = entry.Children;
                    while (siblings.Count > 0) {
                        entry = siblings[siblings.Count - 1];
                        siblings = entry.Children;
                    }

                    // Now, we've either got the original sibling or the last descendent of it.
                    // Use this as the previous entry, or start here if it's not accepted by the filter.
                    if (this.m_Filter(entry)) {
                        return entry;
                    } else {
                        return this.FindPrevious(entry);
                    }
                }

                // If there's no previous sibling and no parent, we're screwed.
                else if (parent == null) {
                    return null;
                }

                // Otherwise, if there's no previous sibling, use the parent.
                else {
                    if (this.m_Filter(parent)) {
                        return parent;
                    } else {
                        return this.FindPrevious(parent);
                    }
                }
            }
        }

        private int getAbsoluteSlideIndex(Presentation.TableOfContentsModel.Entry entry) {
            if (entry == null) return -1;

            Presentation.TableOfContentsModel.Entry currentEntry = entry;
            int count = 0;
            while (currentEntry.Parent != null) {
                //Goto the parent and calculate how many Entries are before this one
                count += currentEntry.Parent.getRelativeSlideIndexOfEntry(currentEntry);
                currentEntry = currentEntry.Parent;
            }
            //We are now at the top, do this on the TOC now
            count += currentEntry.TableOfContents.getRelativeSlideIndexOfEntry(currentEntry);
            //Done
            return count;
        }


        /// <summary>
        /// Tests whether a given entry is allowed to be traversed by the enclosing <see cref="DefaultDeckTraversalModel"/>.
        /// </summary>
        /// <remarks>
        /// The the caller is required to aquire a reader lock on the
        /// <see cref="TableOfContentsModel.Entry#TableOfContents">entry's table of contents</see>.
        /// </remarks>
        public delegate bool EntryFilterDelegate(TableOfContentsModel.Entry entry);
    }
    [Serializable]
    public class SlideDeckTraversalModel : DefaultDeckTraversalModel {
        public SlideDeckTraversalModel(Guid id, DeckModel deck)
            : base(id, deck, new DefaultDeckTraversalModel.EntryFilterDelegate(TestEntry)) { }

        private static bool TestEntry(TableOfContentsModel.Entry entry) {
            return entry.Slide != null;
        }
    }
}
