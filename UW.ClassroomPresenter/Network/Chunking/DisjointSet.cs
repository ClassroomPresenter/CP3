// $Id: DisjointSet.cs 1046 2006-07-21 03:35:50Z pediddle $

using System;
using System.Collections;
using System.Diagnostics;
using System.Text;

namespace UW.ClassroomPresenter.Network.Chunking {
    [Serializable]
    public struct Range {

        public readonly ulong Min;
        public readonly ulong Max;

        public Range(ulong bound1, ulong bound2) {
            if(bound1 < bound2) {
                this.Min = bound1;
                this.Max = bound2;
            } else {
                this.Max = bound1;
                this.Min = bound2;
            }
        }

        public static readonly Range UNIVERSE = new Range(ulong.MinValue, ulong.MaxValue);

        public static implicit operator Range (ulong singleton) {
            return new Range(singleton, singleton);
        }

        public static bool operator < (Range left, Range right) {
            left.AssertWellOrdered();
            right.AssertWellOrdered();
            return left.Max < right.Min;
        }

        public static bool operator > (Range left, Range right) {
            left.AssertWellOrdered();
            right.AssertWellOrdered();
            return left.Min > right.Max;
        }

        public static bool operator & (Range left, ulong right) {
            return left.Min <= right && right <= left.Max;
        }

        public override int GetHashCode() {
            return unchecked((int)(this.Min * this.Max));
        }

        public static bool operator == (Range left, Range right) {
            left.AssertWellOrdered();
            right.AssertWellOrdered();
            return left.Min == right.Min && left.Max == right.Max;
        }

        public static bool operator != (Range left, Range right) {
            left.AssertWellOrdered();
            right.AssertWellOrdered();
            return left.Min != right.Min || left.Max != right.Max;
        }

        public override bool Equals(object obj) {
            this.AssertWellOrdered();
            if(obj is Random) {
                ((Range) obj).AssertWellOrdered();
                return this == ((Range) obj);
            } else {
                return false;
            }
        }

        public override string ToString() {
            return "{" + this.Min + "-" + this.Max + "}";
        }

        [Conditional("DEBUG")]
        private void AssertWellOrdered() {
            Debug.Assert(this.Min <= this.Max, "Range is not well ordered.",
                string.Format("{0} > {1}", this.Min, this.Max));
        }
    }

    [Serializable]
    public sealed class DisjointSet : IEnumerable {
        private DisjointSet Parent;
        private DisjointSet Left;
        private Range Value;
        private DisjointSet Right;

        private DisjointSet(DisjointSet left, Range value, DisjointSet right) {
            this.Left = left;
            this.Value = value;
            this.Right = right;
        }

        public static implicit operator DisjointSet (ulong singleton) {
            return new DisjointSet(null, singleton, null);
        }

        public static implicit operator DisjointSet (Range value) {
            return new DisjointSet(null, value, null);
        }

        /// <summary>
        /// Tests whether the given value is a member of the <see cref="DisjointSet"/>.
        /// </summary>
        /// <param name="left">The <see cref="DisjointSet"/> to examine.</param>
        /// <param name="right">The value to test for membership.</param>
        /// <returns>
        /// <c>true</c> if the value is contained in any <see cref="Range"/>
        /// of the <see cref="DisjointSet"/>.
        /// </returns>
        public static bool Contains(ref DisjointSet left, ulong right) {
            return (left == null) ? false : (left = Splay(left.Find(right))).Value & right;
        }

        public static void Add(ref DisjointSet left, Range right) {
            // An add is a normal binary tree insertion,
            // followed by a splay of the inserted node to the root.
            left = (left == null) ? right : Splay(left.Insert(right));
            left.AssertWellOrdered();
        }

        public static void Add(ref DisjointSet left, DisjointSet right) {
            if(right != null) {
                if(right.Left != null)
                    Add(ref left, right.Left);
                if(right.Right != null)
                    Add(ref left, right.Right);
                Add(ref left, right.Value);
            }
        }

        public static void Remove(ref DisjointSet left, Range right) {
            checked {
                // If the set is already empty, do nothing.
                if(left == null) return;

                // We must first ensure that the range we want to remove is present
                // in the set as a single, contiguous range.  (This could be done
                // without this step but would be much more complicated.)  Fortunately,
                // the Add operation already does this for us via Insert.
                // Add also splays the new range to the root of the tree.
                Add(ref left, right);
                Debug.Assert(left.Value.Min <= right.Min && left.Value.Max >= right.Max, "The Add operation did not leave the new range at the root.");

                // If the range at the root is now completely equal to the range to remove,
                // all we have to do is then delete it with a standard Splay Tree deletion.
                if(right == left.Value) {
                    if(left.Left != null) {
                        // First completely remove the existing root from the tree.
                        if(left.Left != null) left.Left.Parent = null;
                        if(left.Right != null) left.Right.Parent = null;

                        // Then find the root's predecessor, which will have no right subtree.
                        DisjointSet pred = Splay(left.Left.FindMax());
                        Debug.Assert(pred.Right == null);
                        Debug.Assert(pred.Parent == null);

                        // Then attach the old right subtree.
                        pred.Right = left.Right;
                        if(pred.Right != null) pred.Right.Parent = pred;

                        // Finally, "return" the new root.
                        left = pred;
                    }

                    else {
                        // If there is no predecessor, the right subtree (if any) becomes the root,
                        // and the old root is thrown away completely.
                        left = left.Right;
                        if(left != null)
                            left.Parent = null;
                    }
                }

                // Otherwise, the range to remove is a subset of the existing contiguous range,
                // which we have to split depending on which portion is affected.
                else if(left.Value.Min == right.Min) {
                    // If one of the endpoints of the range to remove is the same as the root
                    // range, all we have to do is adjust the range.
                    Debug.Assert(right.Max < left.Value.Max);
                    left.Value = new Range(right.Max + 1, left.Value.Max);
                }

                else if(left.Value.Max == right.Max) {
                    // This is the same case but for the other endpoint.
                    Debug.Assert(left.Value.Min < right.Min);
                    left.Value = new Range(left.Value.Min, right.Min - 1);
                }

                else {
                    // If neither endpoint is equal, we have to make a "hole" in the middle,
                    // which creates an additional node in the tree.
                    Debug.Assert(left.Value.Max > right.Max);
                    Debug.Assert(left.Value.Min < right.Min);
                    Range existing = left.Value;

                    // The new node will be the existing node's successor, so we can insert
                    // it manually as the new right subtree of the existing node.
                    left.Value = new Range(existing.Min, right.Min - 1);
                    left.Right = new DisjointSet(null, new Range(right.Max + 1, existing.Max), left.Right);
                    left.Right.Parent = left;
                    if(left.Right.Right != null) left.Right.Right.Parent = left.Right;

                    // Finally, splay the new node to the root and "return" it.
                    left = Splay(left.Right);
                }

                if(left != null) {
                    Debug.Assert(left.Parent == null);
                    left.AssertWellOrdered();
                }
            }
        }

        public static void Remove(ref DisjointSet left, DisjointSet right) {
            if(right != null) {
                if (right.Left != null)
                    Remove(ref left, right.Left);
                if(right.Right != null)
                    Remove(ref left, right.Right);
                Remove(ref left, right.Value);
            }
        }

        private static DisjointSet Splay(DisjointSet found) {
            for(DisjointSet grandparent; found.Parent != null; ) {
                found.AssertWellOrdered();

                grandparent = found.Parent.Parent;
                if(grandparent == null)
                    if(found.Parent.Right == found)
                        ZigFromRight(found.Parent);
                    else ZigFromLeft(found.Parent);
                else if(grandparent.Right != null && grandparent.Right.Right == found)
                    ZigZigFromRight(grandparent);
                else if(grandparent.Left != null && grandparent.Left.Left == found)
                    ZigZigFromLeft(grandparent);
                else if(grandparent.Right != null && grandparent.Right.Left == found)
                    ZigZagFromRight(grandparent);
                else if(grandparent.Left != null && grandparent.Left.Right == found)
                    ZigZagFromLeft(grandparent);
                else throw new ApplicationException("Splay tree parent/child relationships violated.");
            }

            Debug.Assert(found.Parent == null);
            found.AssertWellOrdered();
            return found;
        }

        private static void ZigFromLeft(DisjointSet grandparent) {
            DisjointSet parent = grandparent.Left;
            grandparent.Left = parent.Right;
            if(grandparent.Left != null)
                grandparent.Left.Parent = grandparent;
            parent.Parent = grandparent.Parent;
            if(parent.Parent != null)
                if(parent.Parent.Right == grandparent)
                    parent.Parent.Right = parent;
                else parent.Parent.Left = parent;
            grandparent.Parent = parent;
            parent.Right = grandparent;
        }

        private static void ZigFromRight(DisjointSet grandparent) {
            DisjointSet parent = grandparent.Right;
            grandparent.Right = parent.Left;
            if(grandparent.Right != null)
                grandparent.Right.Parent = grandparent;
            parent.Parent = grandparent.Parent;
            if(parent.Parent != null)
                if(parent.Parent.Left == grandparent)
                    parent.Parent.Left = parent;
                else parent.Parent.Right = parent;
            grandparent.Parent = parent;
            parent.Left = grandparent;
        }

        private static void ZigZigFromLeft(DisjointSet grandparent) {
            DisjointSet parent = grandparent.Left;
            ZigFromLeft(grandparent);
            ZigFromLeft(parent);
        }

        private static void ZigZigFromRight(DisjointSet grandparent) {
            DisjointSet parent = grandparent.Right;
            ZigFromRight(grandparent);
            ZigFromRight(parent);
        }

        private static void ZigZagFromLeft(DisjointSet grandparent) {
            ZigFromRight(grandparent.Left);
            ZigFromLeft(grandparent);
        }

        private static void ZigZagFromRight(DisjointSet grandparent) {
            ZigFromLeft(grandparent.Right);
            ZigFromRight(grandparent);
        }

        private DisjointSet FindMax() {
            for(DisjointSet right = this; ; right = right.Right)
                if(right.Right == null)
                    return right;
        }

        private DisjointSet Find(ulong value) {
            checked {
                for(DisjointSet p = this, q; ; p = q) {
                    p.AssertWellOrdered();
                    if(value < p.Value.Min) {
                        q = p.Left;
                        if(q == null) return p;
                    } else if(value > p.Value.Max) {
                        q = p.Right;
                        if(q == null) return p;
                    } else {
                        return p;
                    }
                }
            }
        }

        private DisjointSet Insert(Range value) {
            checked {
                for(DisjointSet p = this, q; ; p = q) {
                    // If the new range is strictly less than or greater than the
                    // existing range and is not *adjacent*, descend down the left
                    // or right subtree like a normal binary-tree search.  If there
                    // is no appropriate subtree, insert a brand new node.
                    if(value.Max < p.Value.Min - (p.Value.Min > ulong.MinValue ? 1ul : 0ul)) {
                        q = p.Left;
                        if(q == null) {
                            p.Left = new DisjointSet(null, value, null);
                            p.Left.Parent = p;
                            return p.Left;
                        }
                    } else if(value.Min > p.Value.Max + (p.Value.Max < ulong.MaxValue ? 1ul : 0ul)) {
                        q = p.Right;
                        if(q == null) {
                            p.Right = new DisjointSet(null, value, null);
                            p.Right.Parent = p;
                            return p.Right;
                        }
                    }

                        // But if the ranges overlap or are adjacent, we've found our mark.
                    else {
                        // Get the left and right subtrees.
                        DisjointSet left = p.Left, right = p.Right;

                        // Eliminate all subtrees that overlap or are adjacent to the new range.
                        // Detach the subtrees so we can splay them.
                        // Splay each subtree so the value for comparison will be at the top.
                        // Each time, adjust the new range if the existing ranges extend the bounds.
                        if (left != null) {
                            do {
                                DisjointSet parent = left.Parent;
                                left.Parent = null;
                                left = Splay(left.Find(value.Min));
                                left.Parent = Parent;
                                if (value.Min <= left.Value.Max + (left.Value.Max < ulong.MaxValue ? 1ul : 0ul)) {
                                    value = new Range(Math.Min(value.Min, left.Value.Min), value.Max);
                                    left = left.Left;
                                } else break;
                            } while (left != null);
                        }
                        if (right != null) {
                            do {
                                DisjointSet parent = right.Parent;
                                right.Parent = null;
                                right = Splay(right.Find(value.Max));
                                right.Parent = parent;
                                if (right.Value.Min - (right.Value.Min > ulong.MinValue ? 1ul : 0ul) <= value.Max) {
                                    value = new Range(value.Min, Math.Max(value.Max, right.Value.Max));
                                    right = right.Right;
                                } else break;
                            } while (right != null);
                        }

                        // Now extend the bounds of the range at the root of the tree.
                        p.Value = new Range(Math.Min(value.Min, p.Value.Min), Math.Max(value.Max, p.Value.Max));

                        // Reattach the eliminated subtrees' branches, which are now not adjacent to the new value.
                        p.Left = left;
                        p.Right = right;

                        // Reattach their parents.
                        if (left != null) {
                            left.Parent = p;
                            left.AssertWellOrdered();
                        }
                        if (right != null) {
                            right.Parent = p;
                            right.AssertWellOrdered();
                        }

                        // Return the "inserted" node.
                        p.AssertWellOrdered();
                        return p;
                    }
                }
            }
        }

        [Conditional("DEBUG")]
        private void AssertWellOrdered() {
            checked {
                if(this.Parent != null)
                    if(this.Parent.Left == this)
                        Debug.Assert(this.Parent.Value.Min > this.Value.Max + (this.Value.Max < ulong.MaxValue ? 1ul : 0ul));
                    else if(this.Parent.Right == this)
                        Debug.Assert(this.Value.Max > this.Parent.Value.Min - (this.Parent.Value.Min > ulong.MinValue ? 1ul : 0ul));
                    else Debug.Fail("Child is not a descendent of parent.");

                if(this.Right != null)
                    Debug.Assert(this.Right.Value.Min > this.Value.Max + (this.Value.Max < ulong.MaxValue ? 1ul : 0ul));

                if(this.Left != null)
                    Debug.Assert(this.Left.Value.Max < this.Value.Min - (this.Value.Min > ulong.MinValue ? 1ul : 0ul));
            }
        }

        #region IEnumerable Members

        IEnumerator IEnumerable.GetEnumerator() {
            return new Enumerator(this);
        }

        private class Enumerator : IEnumerator {
            private readonly DisjointSet m_Root;
            private DisjointSet m_CurrentRange;
            private object m_CurrentObject;
            private ulong m_CurrentValue;

            public Enumerator(DisjointSet root) {
                this.m_Root = root;
                this.Reset();
            }

            #region IEnumerator Members

            public void Reset() {
                this.m_CurrentObject = null;
                this.m_CurrentValue = ulong.MinValue;
                this.m_CurrentRange = this.m_Root;
            }

            public object Current {
                get {
                    if(this.m_CurrentObject == null)
                        throw new InvalidOperationException("The enumerator is positioned before the first element of the collection or after the last element.");
                    else return this.m_CurrentObject;
                }
            }

            public bool MoveNext() {
                // If we have just been reset or have just switched to a new CurrentRange...
                if(this.m_CurrentObject == null) {
                    if(this.m_CurrentRange == null)
                        // There are no more ranges and we are done!
                        return false;

                    // Find the minimum range that has not yet been visited.
                    // (m_CurrentValue retains its old value.)
                    while(this.m_CurrentRange.Left != null && this.m_CurrentRange.Left.Value.Min >= this.m_CurrentValue)
                        this.m_CurrentRange = this.m_CurrentRange.Left;

                    // Set the current value to the minimum value of the new range.
                    this.m_CurrentValue = this.m_CurrentRange.Value.Min;
                    this.m_CurrentObject = this.m_CurrentValue;
                    return true;
                }

                // The usual case is to attempt to use the next greater value in the current range.
                else {
                    this.m_CurrentValue++;

                    // If we're still within the current range, use this value and return.
                    if(this.m_CurrentValue <= this.m_CurrentRange.Value.Max) {
                        this.m_CurrentObject = this.m_CurrentValue;
                        return true;
                    }

                    // Otherwise we must traverse the tree to find another range.
                    else {
                        // We've already exhausted all of the left subtrees so try to descend to the right.
                        if(this.m_CurrentRange.Right != null)
                            this.m_CurrentRange = this.m_CurrentRange.Right;

                        // If there is no right subtree, find the first parent that has not already been visited.
                        else do {
                                 this.m_CurrentRange = this.m_CurrentRange.Parent;
                             } while(this.m_CurrentRange != null && this.m_CurrentRange.Value.Min < this.m_CurrentValue);

                        // Unset the current object to signal MoveNext to use the minimum value of the new range.
                        // (This recursive call will not go any deeper than one level.)
                        this.m_CurrentObject = null;
                        return this.MoveNext();
                    }
                }
            }

            #endregion
        }

        #endregion

        public DisjointSet Clone() {
            DisjointSet clone = new DisjointSet(this.Left == null ? null : this.Left.Clone(),
                this.Value, this.Right == null ? null : this.Right.Clone());
            if(clone.Left != null) clone.Left.Parent = clone;
            if(clone.Right != null) clone.Right.Parent = clone;
            return clone;
        }

        public override String ToString() {
            StringBuilder acc = new StringBuilder("{");
            this.ToStringImpl(acc);
            return acc.Append('}').ToString();
        }

        private void ToStringImpl(StringBuilder acc) {
            if(this.Left != null) {
                this.Left.ToStringImpl(acc);
                acc.Append(',');
            }

            acc.Append(this.Value.Min);
            acc.Append('-');
            acc.Append(this.Value.Max);

            if(this.Right != null) {
                acc.Append(',');
                this.Right.ToStringImpl(acc);
            }
        }

#if DEBUG
        static DisjointSet() {
            Test();
        }

        [STAThread]
        private static void Test() {
            // Make a Random instance with a fixed seed so that tests are repeatable.
            Random random = new Random(0);
            DisjointSet test = null;

            // Add a single range.
            DisjointSet.Add(ref test, new Range(5, 10));

            // Verify that DisjointSet.Contains works for each value in the range.
            for(ulong i = 0; i <= 15; i++) {
                Debug.Assert(!(DisjointSet.Contains(ref test, i) ^ (5 <= i && i <= 10)),
                    "DisjointSet.Contains does not work (simple case).");
            }

            // Verify that it works even when the set is splayed randomly.
            for(ulong j = 0; j <= 100; j++) {
                ulong i = (ulong) random.Next(0, 15);
                Debug.Assert(!(DisjointSet.Contains(ref test, i) ^ (5 <= i && i <= 10)),
                    "DisjointSet.Contains does not work (simple case, random splaying).");
            }

            // Add new ranges that overlap the existing range(s).
            DisjointSet.Add(ref test, new Range(4, 9));
            DisjointSet.Add(ref test, new Range(15, 20));
            DisjointSet.Add(ref test, new Range(10, 15));

            // Verify that the ranges were merged.
            Debug.Assert(test.Left == null && test.Right == null,
                "DisjointSet.Add did not merge overlapping ranges.");
            Debug.Assert(test.Value == new Range(4, 20),
                "DisjointSet.Add did not merge overlapping ranges.");

            // Add ranges adjacent to the ends of the existing range.
            DisjointSet.Add(ref test, new Range(21, 25));
            DisjointSet.Add(ref test, 3ul);

            // Verify that they were also merged.
            Debug.Assert(test.Left == null && test.Right == null,
                "DisjointSet.Add did not merge adjacent ranges.");
            Debug.Assert(test.Value == new Range(3, 25),
                "DisjointSet.Add did not merge adjacent ranges.");

            // Remove a range.
            DisjointSet.Remove(ref test, new Range(10, 15));

            // Verify that the ranges still work.
            for(ulong i = 0; i <= 30; i++) {
                Debug.Assert(!(DisjointSet.Contains(ref test, i)
                    ^ ((3 <= i && i <= 9) || (16 <= i && i <= 25))),
                    "DisjointSet.Contains does not work after removal.");
            }

            // Verify that it works even when the set is splayed randomly.
            for(ulong j = 0; j <= 300; j++) {
                ulong i = (ulong) random.Next(0, 30);
                Debug.Assert(!(DisjointSet.Contains(ref test, i)
                    ^ ((3 <= i && i <= 9) || (16 <= i && i <= 25))),
                    "DisjointSet.Contains does not work after removal (random splaying).");
            }

            // Remove the entire split ranges (on a cloned copy so we can do it several times).
            DisjointSet empty;
            empty = test.Clone();
            DisjointSet.Remove(ref empty, Range.UNIVERSE);
            Debug.Assert(empty == null,
                "DisjointSet.Remove(UNIVERSE) didn't result in an empty set.");

            empty = test.Clone();
            DisjointSet.Remove(ref empty, new Range(3, 25));
            Debug.Assert(empty == null,
                "DisjointSet.Remove of entire range didn't result in an empty set.");

            empty = test.Clone();
            DisjointSet.Remove(ref empty, new Range(3, 26));
            Debug.Assert(empty == null,
                "DisjointSet.Remove of entire range didn't result in an empty set.");

            empty = test.Clone();
            DisjointSet.Remove(ref empty, new Range(2, 25));
            Debug.Assert(empty == null,
                "DisjointSet.Remove of entire range didn't result in an empty set.");

            /////////////////////////////////////////////////////////////////////////

            // Now reset the set so we can do some stress tests (mostly to make
            // sure the splaying code works correctly).
            test = null;

            // Insert all integers divisible by 5, 7, or 11.  This makes the ranges
            // easily testable, and results in a set with some singletons as well
            // as some contiguous ranges.
            for(ulong i = 0; i < 1000; i++) {
                if(i % 5 == 0 || i % 7 == 0 || i % 11 == 0) {
                    DisjointSet.Add(ref test, i);
                }
            }

            // Verify the contents of the set.
            for(ulong i = 0; i < 1000; i++) {
                Debug.Assert(!(DisjointSet.Contains(ref test, i) ^ (i % 5 == 0 || i % 7 == 0 || i % 11 == 0)),
                    "DisjointSet.Contains failed the stress test.");
            }

            // Verify the contents of the set, even with random splaying.
            for(ulong j = 0; j < 10000; j++) {
                ulong i = (ulong) random.Next(0, 1000);
                Debug.Assert(!(DisjointSet.Contains(ref test, i) ^ (i % 5 == 0 || i % 7 == 0 || i % 11 == 0)),
                    "DisjointSet.Contains failed the stress test (random splaying).");
            }

            // Now remove all singletons divisible by 7.
            for(ulong i = 0; i < 1000; i++) {
                if(i % 7 == 0) {
                    DisjointSet.Remove(ref test, i);
                }
            }

            // Verify the contents of the set.
            for(ulong i = 0; i < 1000; i++) {
                Debug.Assert(!(DisjointSet.Contains(ref test, i) ^ ((i % 5 == 0 || i % 11 == 0) && (i % 7 != 0))),
                    "DisjointSet.Contains failed the stress test.");
            }

            // Verify the contents of the set, even with random splaying.
            for(ulong j = 0, i; j < 10000; j++) {
                i = (ulong) random.Next(0, 1000);
                Debug.Assert(!(DisjointSet.Contains(ref test, i) ^ ((i % 5 == 0 || i % 11 == 0) && (i % 7 != 0))),
                    "DisjointSet.Contains failed the stress test (random splaying).");
            }

            // Test the IEnumerator implementation.
            ArrayList enums = new ArrayList();
            ulong prev = 0;
            foreach(ulong i in test) {
                Debug.Assert(i >= prev, "The enumeration is not well ordered.");
                enums.Add(prev = i);
            }

            // Verify that the output of the enumerator is exactly what should be in the set.
            for(ulong i = 0; i < 1000; i++) {
                Debug.Assert(!(enums.Contains(i) ^ ((i % 5 == 0 || i % 11 == 0) && (i % 7 != 0))),
                    "The enumeration did not properly enumerate the set.");
            }

            // Test removing from a big tree.
            DisjointSet.Remove(ref test, Range.UNIVERSE);
            Debug.Assert(test == null, "DisjointSet.Remove(UNIVERSE) didn't yield an empty set.");


            /////////////////////////////////////////////////////////////////////////

            // Reset the test and add a lot of small random ranges.  This simulates
            // the kind of behavior experienced by RTPNetworkReceiver.

            test = null;
            ArrayList values = new ArrayList(10000);
            for(ulong i = 0; i < 10000; i++) {
                Range r = i;

                // Expand the range by some random number of indices.
                while(random.Next(2) == 1)
                    r = new Range(r.Min, ++i);

                // Add all the indices in the range to both sets.
                DisjointSet.Add(ref test, r);
                for(ulong v = r.Min; v <= r.Max; v++)
                    values.Add(v);

                // Skip some random number of indices.
                while(random.Next(2) == 1)
                    i++;
            }

            // Test removing all of the values from the set in randomized ascending order,
            // just like what happens when receiving responses to a NACK (ie., some frames
            // might be dropped twice (or more times), and then NACKed again).
            DisjointSet copy = test.Clone();
            for(ArrayList compare = (ArrayList) values.Clone(); compare.Count > 0;) {
                for(int i = 0; i < compare.Count; ) {
                    Debug.Assert(copy != null, "DisjointSet is empty but not all entries have been removed.");

                    // Skip some random number of entries.
                    while(random.Next(2) == 1) i++;
                    if(i >= compare.Count) break;

                    // Remove the entries one at a time.
                    ulong v = (ulong) compare[i];
                    DisjointSet.Remove(ref copy, v);
                    compare.RemoveAt(i);
                }
            }
            Debug.Assert(copy == null, "All entries were removed but DisjointSet is not empty.");

            // Test that all of the entries were actually added to the set.
            foreach(ulong v in test)
                Debug.Assert(values.Contains(v), "DisjointSet contains a value that it shouldn't.");
            foreach(ulong v in values)
                Debug.Assert(DisjointSet.Contains(ref test, v), "DisjointSet doesn't contain a value that it should.");

            /////////////////////////////////////////////////////////////////////////

            // Simulate an error-case discovered during actual testing.
            // See Bug 821, attachment #45 (comment #3).

            DisjointSet current = null;
            DisjointSet.Add(ref current, new Range(1, 4));
            DisjointSet.Add(ref current, new Range(6, 16));
            DisjointSet.Add(ref current, new Range(18, 20));
            DisjointSet.Add(ref current, new Range(22, 33));
            DisjointSet.Add(ref current, new Range(35, 55));
            DisjointSet.Add(ref current, new Range(57, 61));
            DisjointSet.Add(ref current, 64);
            DisjointSet.Add(ref current, 67);

            DisjointSet stable = null;
            DisjointSet.Add(ref stable, new Range(1, 16));
            DisjointSet.Add(ref stable, new Range(18, 61));
            DisjointSet.Add(ref stable, 64);
            DisjointSet.Add(ref stable, 67);
            copy = stable.Clone();

            test = new Range(1, 100);
            DisjointSet.Remove(ref test, current);
            foreach (ulong i in test)
                DisjointSet.Remove(ref copy, i);
            DisjointSet.Remove(ref copy, 0);

            DisjointSet.Remove(ref copy, stable);
            Debug.Assert(copy == null, "Stable set is not a superset of the Current set.");
        }
#endif
    }
}
