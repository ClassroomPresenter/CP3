// $Id: TableOfContentsModel.cs 1604 2008-05-02 22:45:27Z fred $

using System;
using System.Collections;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;

namespace UW.ClassroomPresenter.Model.Presentation {
    /// <summary>
    /// Summary description for TableOfContentsModel.
    /// </summary>
    [Serializable]
    public class TableOfContentsModel : PropertyPublisher {

        private readonly EntryCollection m_Entries;
        [Published] public EntryCollection Entries {
            get { return this.m_Entries; }
        }

        private int m_SlideCount;
        [Published] public int SlideCount {
            get { return this.GetPublishedProperty("SlideCount", ref this.m_SlideCount); }
        }

        public TableOfContentsModel() {
            this.m_Entries = new EntryCollection(this, "Entries");
        }

        public Entry this[params int[] index] {
            get {
                // **************************** FIXME ******************************
                Entry currentEntry = this.m_Entries[0];
                for (int i = 1; i < index.Length; i++) {
                    currentEntry = currentEntry.Children[index[i]];
                }
                return currentEntry;
            }
        }

        internal void updateSlideCount() {
            int count = 0;
            for (int i = 0; i < this.m_Entries.Count; i++) {
                count += this.m_Entries[i].SlideCount;
            }
            this.SetPublishedProperty("SlideCount", ref this.m_SlideCount, count);
        }

        internal int getRelativeSlideIndexOfEntry(Presentation.TableOfContentsModel.Entry entry) {
            int index = this.m_Entries.IndexOf(entry);
            if (index != -1) {
                int count = 1;
                for (int i = 0; i < index; i++) {
                    count += this.m_Entries[i].SlideCount;
                }
                return count;
            } else {
                //We did not find that entry
                return -1;
            }
        }

        #region Lookup by Slide ID
        private Dictionary<Guid, Entry> m_SlideIdLookup = null;
        protected void addToSlideLookup(Entry newEntry) {
            if (m_SlideIdLookup == null) {
                buildSlideIdLookup();
            }
            newEntry.AddSlidesToTable(m_SlideIdLookup);
        }

        private void buildSlideIdLookup() {
            m_SlideIdLookup = new Dictionary<Guid, Entry>();
            foreach (Entry e in m_Entries) {
                e.AddSlidesToTable(m_SlideIdLookup);
            }
        }

        protected void removeFromSlideLookup(Entry removeEntry) { 
            if (m_SlideIdLookup != null) {
                removeEntry.RemoveSlidesFromTable(m_SlideIdLookup);
            }
        }

        internal Entry GetEntryBySlideId(Guid slideId) {
            if (m_SlideIdLookup == null) {
                buildSlideIdLookup();
            }
            if (m_SlideIdLookup.ContainsKey(slideId)) {
                return m_SlideIdLookup[slideId];
            }
            return null;
        }

        #endregion Lookup by Slide ID

        #region Entry

        [Serializable]
        public class Entry : PropertyPublisher {
            private Entry m_Parent;
            [Published("Entries")] public Entry Parent {
                get { return this.GetPublishedProperty("Parent", ref this.m_Parent); }
                // Setter is "internal"; see SetParent(Entry) below.
            }

            private readonly SlideModel m_Slide;
            public SlideModel Slide {
                get { return this.m_Slide; }
            }

            private readonly EntryCollection m_Children;
            [Published("Entries")] public EntryCollection Children {
                get { return this.m_Children; }
            }

            private readonly Guid m_Id;
            public Guid Id {
                get { return this.m_Id; }
            }

            private readonly TableOfContentsModel m_TOC;
            public TableOfContentsModel TableOfContents {
                get { return this.m_TOC; }
            }

            //This field is used on the student side to keep track of the slide's index in the instructor's deck
            //as we receive the broadcasted slides (in hash order) and as we insert and delete slides
            //on the student side
            public int index;

            public int SlideCount {
                get {
                    if (this.m_Slide != null) {
                        return this.m_Children.SlideCount + 1;
                    } else {
                        return this.m_Children.SlideCount;
                    }
                }
            }



            public Entry(Guid id, TableOfContentsModel toc, SlideModel slide) : base(toc) {
                this.m_Id = id;
                this.m_Children = new EntryCollection(this, "Entries");
                this.m_TOC = toc;
                this.m_Slide = slide;
            }


            public int IndexInParent {
                get {
                    if(this.Parent == null) {
                        // The following will handle the case when the entry is at the root of the
                        // table of contents, and will also return -1 if the entry has not been
                        // added to the table of contents at all.
                        return this.TableOfContents.Entries.IndexOf(this);
                    } else {
                        return this.Parent.Children.IndexOf(this);
                    }
                }
            }

            public int[] PathFromRoot {
                get {
                    Entry currentEntry = this;
                    ArrayList al = new ArrayList();
                    while(currentEntry != null) {
                        al.Insert(0, currentEntry.IndexInParent);
                        currentEntry = currentEntry.Parent;
                    }
                    return ((int[]) al.ToArray(typeof(int)));
                }
            }

            internal void updateSlideCount() {
                this.m_Children.updateSideCount();
                if (this.m_Parent != null) {
                    ((Entry)this.m_Parent).updateSlideCount();
                } else {
                    this.TableOfContents.updateSlideCount();
                }
            }

            internal void SetParent(Entry value) {
                this.SetPublishedProperty("Parent", ref this.m_Parent, value);
            }

            internal int getRelativeSlideIndexOfEntry(Entry entry) {
                int index = this.m_Children.IndexOf(entry);
                if (index != 1) {
                    int count = 0;
                    for (int i = 0; i < index; i++) {
                        count += this.m_Children[i].SlideCount;
                    }
                    return count;
                } else {
                    //We did not find that entry
                    return -1;
                }
            }

            internal void AddSlidesToTable(Dictionary<Guid, Entry> slideIdLookupTable) {
                if (this.m_Slide != null) {
                    if (slideIdLookupTable.ContainsKey(this.m_Slide.Id)) {
                        slideIdLookupTable.Remove(this.m_Slide.Id);
                    }
                    slideIdLookupTable.Add(this.m_Slide.Id, this);
                }
                if (this.m_Children != null) {
                    foreach (Entry e in this.m_Children) {
                        e.AddSlidesToTable(slideIdLookupTable);
                    }
                }
            }

            internal void RemoveSlidesFromTable(Dictionary<Guid, Entry> slideIdLookupTable) {
                if (this.m_Slide != null) {
                    if (slideIdLookupTable.ContainsKey(this.m_Slide.Id)) {
                        slideIdLookupTable.Remove(this.m_Slide.Id);
                    }
                }
                if (this.m_Children != null) {
                    foreach (Entry e in this.m_Children) {
                        e.RemoveSlidesFromTable(slideIdLookupTable);
                    }
                }                
            }

        }

        [Serializable]
        public class EntryCollection : PropertyCollectionBase {
            private object m_Parent;

            private int m_SlideCount = 0;
            public int SlideCount {
                get { return this.m_SlideCount; }
            }


            public int Len {
                get { return List.Count; }
            }


            internal EntryCollection(PropertyPublisher owner, string property) : base(owner, property) {
                this.m_Parent = owner;
            }

            public Entry this[int index] {
                get { return ((Entry) List[index]); }
                set { List[index] = value; }
            }

            public int Add(Entry value) {
                value.index = -1;
                addToSlideLookup(value);
                return List.Add(value);
            }

            public void BroadcastInsert( int index, Entry value) {
                value.index = index;
                int i = 0;
                while (List.Count > i && ((Entry)(List[i])).index < value.index) { i++; }
                if (List.Count > i && ((Entry)(List[i])).index == value.index) {
                    for (int j = i; j < List.Count; j++) {
                        if (((Entry)(List[j])).index > -1) {
                            ((Entry)(List[j])).index += 1;
                        }
                    }
                }
                List.Insert(i, value);
                addToSlideLookup(value);
            }

            public void Insert(int index, Entry value) {
                value.index = -1;//so it'll always be < things with real indices
                List.Insert(index, value);
                addToSlideLookup(value);
            }

            public int IndexOf(Entry value) {
                return List.IndexOf(value);
            }


            public void Remove(Entry value) {
                List.Remove(value);
                removeFromSlideLookup(value);
            }
            public void BroadcastRemove(Entry value) {
                int index = ((Entry)(List[List.IndexOf(value)])).index;
                int i = 0;
                while (List.Count > i && ((Entry)(List[i])).index < index) { i++; }
                for (int j = i; j < List.Count; j++) {
                    if (((Entry)(List[j])).index > -1) {
                        ((Entry)(List[j])).index -= 1;
                    }
                }
                List.Remove(value);
                removeFromSlideLookup(value);
            }
            public void BroadcastRemoveAt(int index){
                for (int j = index; j < List.Count; j++) {
                    if (((Entry)(List[j])).index > -1) {
                        ((Entry)(List[j])).index -= 1;
                    }
                }
                removeFromSlideLookup((Entry)List[index]);
                base.RemoveAt(index);
            }

            public bool Contains(Entry value) {
                return List.Contains(value);
            }

            protected override void OnValidate(Object value) {
                if(!typeof(Entry).IsInstanceOfType(value))
                    throw new ArgumentException("Value must be of type TableOfContentsModel.Entry.", "value");
            }

            protected override void OnInsert(int index, object value) {
                SetParent((Entry) value, this.m_Parent as Entry);
                base.OnInsert(index, value);
            }

            protected override void OnInsertComplete(int index, object value) {
                if (this.m_Parent is TableOfContentsModel) {
                    ((TableOfContentsModel)this.m_Parent).updateSlideCount();
                } else {
                    ((Entry)this.m_Parent).updateSlideCount();
                }
                base.OnInsertComplete(index, value);
            }

            protected override void OnSet(int index, object oldValue, object newValue) {
                SetParent((Entry) oldValue, null);
                SetParent((Entry) newValue, this.m_Parent as Entry);
                base.OnSet(index, oldValue, newValue);
            }

            protected override void OnSetComplete(int index, object oldValue, object newValue) {
                if (this.m_Parent is TableOfContentsModel) {
                    ((TableOfContentsModel)this.m_Parent).updateSlideCount();
                } else {
                    ((Entry)this.m_Parent).updateSlideCount();
                }
                base.OnSetComplete (index, oldValue, newValue);
            }


            protected override void OnRemove(int index, object value) {
                SetParent((Entry) value, null);
                base.OnRemove (index, value);
            }

            protected override void OnRemoveComplete(int index, object value) {
                if (this.m_Parent is TableOfContentsModel) {
                    ((TableOfContentsModel)this.m_Parent).updateSlideCount();
                } else {
                    ((Entry)this.m_Parent).updateSlideCount();
                }
                base.OnRemoveComplete (index, value);
            }


            private static void SetParent(Entry value, Entry newParent) {
                object parent = value.Parent;
                if(parent != null && parent != newParent) {
                    throw new InvalidOperationException("An entry can only have one parent.");
                } else {
                    value.SetParent(newParent);
                }
            }

            internal void updateSideCount() {
                int count = 0;
                for (int i = 0; i < this.List.Count; i++) {
                    count += ((Entry)this.List[i]).SlideCount;
                }
                this.m_SlideCount = count;
            }

            private void addToSlideLookup(Entry newEntry) {
                TableOfContentsModel toc = getRootTOC();
                if (toc != null) {
                    toc.addToSlideLookup(newEntry);
                }
            }

            private void removeFromSlideLookup(Entry removeEntry) { 
                TableOfContentsModel toc = getRootTOC();
                if (toc != null) {
                    toc.removeFromSlideLookup(removeEntry);
                }            
            }

            private TableOfContentsModel getRootTOC() { 
                TableOfContentsModel toc = m_Parent as TableOfContentsModel;
                if (m_Parent == null) {
                    Entry entry = m_Parent as Entry;
                    if (entry != null) {
                        toc = entry.TableOfContents;
                    }
                    else {
                        Debug.Fail("Unexpected TOC Entry parent type");
                    }
                }
                return toc;
            }

        }

        #endregion Entry
    }
}
