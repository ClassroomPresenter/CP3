// $Id: Message.cs 1871 2009-05-26 19:29:56Z cmprince $

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Runtime.Serialization;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Network.Groups;
using UW.ClassroomPresenter.Network.Messages.Presentation;

namespace UW.ClassroomPresenter.Network.Messages {
    [Serializable]
    public abstract class Message : IGenericSerializable  {
        /// <summary>
        /// The object being affected by the message, created on the receiving 
        /// side and stored temporarily in the targets table.
        /// </summary>
        [NonSerialized]
        public object Target;

        /// <summary>
        /// Extra summary information about the message contents which will be used
        /// initially for the purpose of optimizing TCP message sending
        /// </summary>
        [NonSerialized]
        public MessageTags Tags;

        /// <summary>
        /// An id representing which group this message is bound for
        /// </summary>
        public Group Group;

        /// <summary>
        /// A globally-unique identifier for the object being affected by the <see cref="Message"/>.
        /// </summary>
        public readonly ValueType TargetId;

        /// <summary>
        /// The common parent of a message and its <see cref="Predecessor"/>s.
        /// </summary>
        public Message Parent;

        /// <summary>
        /// The most recent child message in a linked list of messages and
        /// their <see cref="Predecessor"/>s.
        /// </summary>
        public Message Child;

        /// <summary>
        /// Another message sharing the same <see cref="Parent"/> which precedes this message
        /// chronologically.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <see cref="OnReceive"/> method of a predecessor is always invoked before
        /// its successor.
        /// </para>
        /// <para>
        /// This field forms a linked list with other sibling <see cref="Message"/>s
        /// (children of a common "parent" message).
        /// </para>
        /// </remarks>
        public Message Predecessor;

        /// <summary>
        /// A place for any additional information which may be added between major releases.
        /// </summary>
        /// <remarks>
        /// The goal is to allow for extending functionality without breaking message compatibility.
        /// Objects assigned to the extension property may be single objects, collections, or object graphs
        /// including any of the folowing:
        ///     1. .Net Framework types
        ///     2. Types that already existed at the release 
        ///     3. New types wrapped in ExtensionWrapper
        /// In this way the message will not except on deserialization, and the extension can be safely ignored 
        /// on earlier post-release receivers.
        /// At a new major release, or when a compatiblity break is deemed to be acceptable, features that have
        /// been impelemented using Extensions should be converted to use first-class message types.
        /// </remarks>
        public object Extension;

        /// <summary>
        /// NOTE: Added by CMPRINCE for performance testing
        /// This is a unique identifier for this packet, which is only set once
        /// </summary>
        public readonly Guid m_MessageIdentifier = Guid.NewGuid();

        /// <summary>
        /// Creates a new <see cref="Message"/>.
        /// </summary>
        protected Message(ValueType targetId) {
            this.TargetId = targetId;
        }

        /// <summary>
        /// Use this to add a network reference to local objects before sending messages
        /// This allows us to hear back modifications to objects that we had created.
        /// </summary>
        /// <param name="target">The target object</param>
        public void AddLocalRef( object target ) {
            FinalizableWeakReference reference = null;
            c_Targets.TryGetValue( this.TargetId, out reference );

            // Store the new target's value, or remove the weak reference to
            // and existing target if the new target is null
            if( target != null )
                if( reference == null )
                    c_Targets[this.TargetId] = new FinalizableWeakReference( target );
                else
                    reference.Target = target;
            else if( reference != null )
                c_Targets.Remove( this.TargetId );
        }

        /// <summary>
        /// Use this to add a network reference to local objects before sending messages
        /// This allows us to hear back modifications to objects that we had created. 
        /// Also we need to add objects upon receiving if create more than one object 
        /// in response to a single message.
        /// 
        /// TODO CMPRINCE: Shouldn't all objects in the model be addressable over the 
        /// network whether local or not?
        /// </summary>
        /// <param name="target">The target object</param>
        public static void AddLocalRef( ValueType t, object target ) {
            FinalizableWeakReference reference = null;
            c_Targets.TryGetValue( t, out reference );

            // Store the new target's value, or remove the weak reference to
            // and existing target if the new target is null
            if( target != null )
                if( reference == null )
                    c_Targets[t] = new FinalizableWeakReference( target );
                else
                    reference.Target = target;
            else if( reference != null )
                c_Targets.Remove( t );
        }

        /// <summary>
        /// Attempts to fetch an already existing target from the <see cref="TargetId"/>,
        /// recursively invokes <see cref="OnReceive"/> on predecessors and children,
        /// and invokes <see cref="UpdateTarget"/> on this message.
        /// </summary>
        /// <param name="context">The context in which the message is to be processed.</param>
        protected virtual internal void OnReceive(ReceiveContext context) {
            // Process predecessor methods
            if(this.Predecessor != null)
                this.Predecessor.OnReceive(context);

            // Check here if the context of this participant puts them in the same group as the message.
            using( Synchronizer.Lock( context.Model.SyncRoot ) ) {
                using( Synchronizer.Lock( context.Model.Participant.SyncRoot ) ) {
                    if( this.Group != null && !context.Model.Participant.Groups.Contains( this.Group ) )
                        return;
                }
            }

            Debug.WriteLine(string.Format("Processing message for {{{0}}}.", this.TargetId.ToString()), this.GetType().ToString());
            Debug.Indent();
            try {
                using(Synchronizer.Lock(c_Targets)) {
                    if(this.Target != null)
                        throw new InvalidOperationException("OnReceive cannot be invoked more than once.");

                    // Attempt to fetch an already existing target.
                    // If the TargetId has not been seen before or if the old target
                    // has been garbage-collected, this.Target will be set to null.
                    FinalizableWeakReference reference;
                    this.Target = (c_Targets.TryGetValue(this.TargetId, out reference))
                        ? reference.Target : null;

                    // Let the subclass update or create the target.
                    if (this.UpdateTarget(context)) {
                        // If requested by the subclass's UpdateTarget, store 
                        // the new target's value, or remove the WeakReference to
                        // an existing target if the new target is null.
                        if (this.Target != null)
                            if (reference == null)
                                c_Targets[this.TargetId] = new FinalizableWeakReference(this.Target);
                            else reference.Target = this.Target;
                        else if (reference != null)
                            c_Targets.Remove(this.TargetId);
                    }

                    // Finally, recursively process any child messages.
                    if(this.Child != null)
                        this.Child.OnReceive(context);
                }
            } finally {
                Debug.Unindent();
            }
        }

        /// <summary>
        /// Must be implemented by subclasses to create and/or update their <see cref="Target"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If a message with the same <see cref="TargetId"/> has already been processed
        /// and returned a non-null <see cref="Target"/>, and if the previous <see cref="Target"/>
        /// has not been garbage-collected, then the <c>this.<see cref="Target"/></c> will
        /// be set to the previous target before <see cref="OnReceive"/> is called.
        /// Otherwise, <c>this.<see cref="Target"/></c> will be <c>null</c>.
        /// </para>
        /// <para>
        /// The message may attempt to access its <see cref="Parent"/> hierarchy to obtain relevant
        /// context needed for processing, and may also use the context from the
        /// <see cref="ReceiveContext"/> instance.
        /// </para>
        /// </remarks>
        /// <param name="context">The context in which the message is to be processed.</param>
        /// <returns>
        /// A boolean indicating whether the targets table should be updated 
        /// with the new value of <code>Target</code>.
        /// </returns>
        protected abstract bool UpdateTarget(ReceiveContext context);

        /// <summary>
        /// Attempts to combine two forrests of messages.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public virtual void ZipInto(ref Message other) {
            if(this == other) {
                throw new ArgumentException("A message cannot be zipped into itself.", "other");
            }

            else if(other == null) {
                other = this;
                return;
            }

            else if(this.TargetId.Equals(other.TargetId)) {
                if(this.Parent != other.Parent && this.Parent != null && other.Parent != null)
                    throw new ArgumentException("Two child messages cannot be zipped if they have different parents.", "other");

                // Attempt to copy this message's information into the other one.
                MergeAction result = this.MergeInto(other);

                switch(result) {
                    case MergeAction.KeepBothInOrder:
                        // The default action: Insert this message chronologically after the other,
                        // and keep all children and information separately from both messages.
                        // (This is the same as if the messages were unrelated, below.)
                        this.AddOldestPredecessor(other);
                        other = this;
                        break;

                    case MergeAction.DiscardOther:
                        // The information in this packet completely replaces that in the other.
                        // The other packet is now useless, so we steal its predecessors.
                        this.AddOldestPredecessor(other.Predecessor);
                        other.Predecessor = null;

                        // We can also recursively merge the children, being careful to copy
                        // from this message into the other to preserve chronological order.
                        this.ZipChildrenInto(other);

                        // Then move the children back to be children of this message.
                        // Disconnect the new children's parent first so InsertChild doesn't complain.
                        if(other.Child != null) {
                            for(Message child = other.Child; child != null; child = child.Predecessor)
                                child.Parent = null;
                            this.InsertChild(other.Child);
                            other.Child = null;
                        }

                        // Having copied all children, predecessors, and information,
                        // replace the useless message with this one.
                        other = this;
                        break;

                    case MergeAction.DiscardThis:
                        // The information in the other packet is "better" than in this packet,
                        // or the subclass has copied all relavent information to the other.
                        // This packet is now useless.  We can also recursively merge the children.
                        other.AddOldestPredecessor(this.Predecessor);
                        this.Predecessor = null;
                        this.ZipChildrenInto(other);
                        break;

                    default:
                        throw new ArgumentException("Invalid MergedAction value returned by MergeInto.");
                }
            } else {
                // If the messages are unrelated, simply arrange them in chronological order,
                // and keep all children and information separately from both messages.
                // (This is the same as MergeAction.KeepBothInOrder, above.)
                this.AddOldestPredecessor(other);
                other = this;
            }
        }

        /// <summary>
        /// The set of possible actions to be performed by <see cref="ZipInto"/> after a
        /// <see cref="Message"/> subclass's implementation of <see cref="MergeInto"/> completes.
        /// </summary>
        protected enum MergeAction {
            /// <summary>
            /// <see cref="MergeInto"/> should return <see cref="KeepBothInOrder"/> when it desires
            /// that both this message and the other message should be kept in chronological order
            /// (the other message being "older" than this one), and that children should not be merged.
            /// </summary>
            /// <remarks>
            /// The other message will become the "oldest" predecessor of this message.
            /// </remarks>
            KeepBothInOrder,

            /// <summary>
            /// <see cref="MergeInto"/> should return <see cref="DiscardOther"/> when this message
            /// completely supplants all relevant information from the other message.
            /// </summary>
            /// <remarks>
            /// This message's <see cref="Child"/> will still be recursively merged into that of
            /// the other message, and the other message's predecessors will become the "oldest"
            /// predecessors of this message.
            /// </remarks>
            DiscardOther,

            /// <summary>
            /// <see cref="MergeInto"/> should return <see cref="DiscardThis"/> when all relevant
            /// information contained in this message has been copied into the other message,
            /// implying that this message is now useless.
            /// </summary>
            /// <remarks>
            /// This message's <see cref="Child"/> will still be recursively merged into that of
            /// the other message, and this message's predecessors will become the "newest"
            /// predecessors of the other message.
            /// </remarks>
            DiscardThis,
        }

        /// <summary>
        /// Copies the details of one individual message into another, or replaces the other completely.
        /// </summary>
        /// <remarks>
        /// The default implementation simply returns <see cref="MergeAction.KeepBothInOrder"/>.
        /// </remarks>
        /// <param name="other">The message into which information is to be copied.</param>
        protected virtual MergeAction MergeInto(Message other) {
            return MergeAction.KeepBothInOrder;
        }

        /// <summary>
        /// Appends a predecessor to the end of the linked list of sibling <see cref="Message"/>s,
        /// making it a child of this message's <see cref="Parent"/> (if any).
        /// </summary>
        /// <remarks>
        /// The new predecessor conceptually comes <em>before</em> all other predecessors in chronological order.
        /// </remarks>
        /// <param name="newOldestPredecessor">The <see cref="Message"/> to be appended.</param>
        public void AddOldestPredecessor(Message newOldestPredecessor) {
            // Walk to the end of the linked list and append the new predecessor.
            Message last = this;
            while(last.Predecessor != null)
                last = last.Predecessor;
            last.Predecessor = newOldestPredecessor;

            // Set all of the predecessors' parents to this message.
            while(newOldestPredecessor != null) {
                newOldestPredecessor.Parent = this.Parent;
                newOldestPredecessor = newOldestPredecessor.Predecessor;
            }
        }

        /// <summary>
        /// Recursively zips the children of two related messages.
        /// </summary>
        /// <param name="other">
        /// The message which will become the new parent of each of this message's children.
        /// </param>
        private void ZipChildrenInto(Message other) {
            if(this.Child != null) {
                // Change the parent of each child to the other message so they can be zipped.
                for(Message child = this.Child; child != null; child = child.Predecessor)
                    child.Parent = other;

                // Recursively zip the children.
                this.Child.ZipInto(ref other.Child);

                // Our children don't belong to us anymore, so clean up.
                this.Child = null;
            }
        }

        /// <summary>
        /// Inserts a <see cref="Message"/> as the first child of a parent message.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The child's <see cref="Parent"/> field will be set to this message,
        /// as will that of its predecessors.
        /// </para>
        /// <para>
        /// If this message already has a child, the existing child will be appended
        /// as the "oldest" predecessor of the new child. <seealso cref="AddOldestPredecessor"/>
        /// </para>
        /// </remarks>
        /// <param name="newFirstChild">The child to insert.</param>
        public void InsertChild(Message newFirstChild) {
            if(newFirstChild.Parent != null)
                throw new ArgumentException("A child Message cannot be inserted if it already has a parent Message.", "newFirstChild");

            // Preserve the existing child.
            Message existing = this.Child;

            // Set the new first child and update its Parent references.
            this.Child = newFirstChild;
            for(Message sibling = newFirstChild; sibling != null; sibling = sibling.Predecessor)
                sibling.Parent = this;

            // Preserve the existing child (after the call to SetChildAndUpdateParents
            // because its Parent reference should already point to this message.
            newFirstChild.AddOldestPredecessor(existing);
        }


        /// <summary>
        /// A mapping from <see cref="Guid"/>s to <see cref="WeakReference"/>s
        /// containing the targets processed by <see cref="OnReceive"/>.
        /// </summary>
        private static readonly FinalizableDictionary<ValueType, FinalizableWeakReference> c_Targets
            = new FinalizableDictionary<ValueType, FinalizableWeakReference>();

        private static TargetsCleaner c_NextTargetsCleaner;

        static Message() {
            // Create two target cleaners.  The second one is immediately eligible
            // for garbage collection.  When its finalizer runs, the first one is
            // replaced and becomes eligible for garbage collection itself.
            c_NextTargetsCleaner = new TargetsCleaner();
            new TargetsCleaner();
        }

        private sealed class TargetsCleaner {
            ~TargetsCleaner() {
                // If the table has already been finalized, then we're
                // in the final death throws of the application and there's
                // no point in continuing.
                if(c_Targets.IsFinalized)
                    return;

                // We don't want to do this expensive cleanup too frequently,
                // so wait until the existing c_NextTargetsCleaner is a little bit old
                // by asking the garbage collector which generation it's in.
                int generation = System.GC.GetGeneration(c_NextTargetsCleaner);
                if(generation <= 1) {
                    // Create a new TargetsCleaner that's immediately eligible for collection.
                    // Don't replace c_NextTargetsCleaner so it will get older.
                    new TargetsCleaner();
                    return;
                }

                try {
                    // Otherwise begin cleaning up expired table entries.
                    lock(c_Targets) {
                        List<ValueType> toRemove = null;

                        foreach(ValueType key in c_Targets.Keys) {
                            FinalizableWeakReference reference = c_Targets[key];

                            // If the reference has already been finalized, then we're
                            // in the final death throws of the application and there's
                            // no point in continuing.
                            if(reference.IsFinalized)
                                return;

                            // Otherwise remove the entry if the target has been garbage-collected.
                            if(!reference.IsAlive) {
                                if(toRemove == null)
                                    toRemove = new List<ValueType>();
                                toRemove.Add(key);
                            }
                        }

                        // The entries can't be removed above during the foreach enumerator, so remove them now.
                        if(toRemove != null)
                            foreach(ValueType key in toRemove)
                                c_Targets.Remove(key);
                    }
                } catch {
                    // The garbage collector will ignore all exceptions anyway,
                    // so catch them so we can create a new TargetsCleaner.
                }

                // Create the next cleaner.  This replaces the reference to the existing "next" cleaner,
                // which then becomes eligible for finalization.
                c_NextTargetsCleaner = new TargetsCleaner();
            }
        }

        /// <summary>
        /// A utility class which enables the <see cref="TargetsCleaner"/> to abort
        /// if its finalizer is running because the application is terminating.
        /// </summary>
        private sealed class FinalizableDictionary<TKey, TValue> : Dictionary<TKey, TValue> {
            public bool IsFinalized;

            public FinalizableDictionary() {}

            ~FinalizableDictionary() {
                this.IsFinalized = true;
            }
        }

        /// <summary>
        /// A utility class which enables the <see cref="TargetsCleaner"/> to abort
        /// if its finalizer is running because the application is terminating.
        /// </summary>
        private sealed class FinalizableWeakReference : WeakReference {
            public bool IsFinalized;

            public FinalizableWeakReference(object target) : base(target) {}

            ~FinalizableWeakReference() {
                this.IsFinalized = true;
            }
        }

        public override string ToString() {
            string fn = this.GetType().FullName;
            string s = fn.Substring(fn.LastIndexOf('.') + 1) + ";";
            if (this.Predecessor != null) {
                s = s+ "pred={" + this.Predecessor.ToString() + "};";
            }
            if (this.Child != null) {
                s = s+"child={" + this.Child.ToString() + "};";
            }
            return s;
        }

        public Message Clone() {
            Message clone = (Message)this.MemberwiseClone();
            if (this.Predecessor != null) {
                clone.Predecessor = this.Predecessor.Clone();
            }
            if (this.Child != null) {
                clone.Child = this.Child.Clone();
            }
            return clone;
        }

        #region IGenericSerializable

        /// <summary>
        /// Serialize this object
        /// </summary>
        /// <returns>The serialized packet</returns>
        public virtual SerializedPacket Serialize() {
            SerializedPacket p = new SerializedPacket( this.GetClassId() );
            p.Add( (this.Group != null) ? 
                this.Group.Serialize() : SerializedPacket.NullPacket( PacketTypes.GroupId ) );
            if( this.TargetId is Guid && this.TargetId != null ) {
                p.Add( SerializedPacket.SerializeGuid( (Guid)this.TargetId ) );
            } else if( this.TargetId is Model.Presentation.ByteArray && this.TargetId != null ) {
                p.Add( SerializedPacket.SerializeGuid( ((Model.Presentation.ByteArray)this.TargetId).ToGuid() ) );
            } else {
                p.Add( SerializedPacket.NullPacket( PacketTypes.GuidId ) );
            }
            // Don't Serialize Parent
            p.Add( (this.Child != null) ?
                this.Child.Serialize() : SerializedPacket.NullPacket( PacketTypes.MessageId ) );
            p.Add( (this.Predecessor != null) ?
                this.Predecessor.Serialize() : SerializedPacket.NullPacket( PacketTypes.MessageId ) );
            p.Add( SerializedPacket.SerializeGuid( this.m_MessageIdentifier ) );
            return p;
        }

        /// <summary>
        /// Construct the message
        /// </summary>
        /// <param name="p">The packet</param>
        public Message( Message parent, SerializedPacket p ) {
            SerializedPacket.VerifyPacket( p, this.GetClassId() );
            this.Group = (!SerializedPacket.IsNullPacket( p.PeekNextPart() )) ?
                new Group( p.PeekNextPart() ) : null; p.GetNextPart();
            if( SerializedPacket.IsNullPacket( p.PeekNextPart() ) ) {
                this.TargetId = null;
            } else if( p.PeekNextPart().Type == PacketTypes.GuidId ) {
                this.TargetId = (ValueType)SerializedPacket.DeserializeGuid( p.PeekNextPart() );
            } else if( p.PeekNextPart().Type == PacketTypes.ByteArrayClassId ) {
                this.TargetId = (ValueType)new Model.Presentation.ByteArray( SerializedPacket.DeserializeGuid(p.PeekNextPart()) );
            } else {
                throw new Exception( "Unknown ValueType" );
            }
            p.GetNextPart();
            this.Parent = parent;
            this.Child = (!SerializedPacket.IsNullPacket( p.PeekNextPart() )) ?
                (Message)PacketTypes.DecodeMessage( this, p.PeekNextPart() ): null; p.GetNextPart();
            this.Predecessor = (!SerializedPacket.IsNullPacket( p.PeekNextPart() )) ?
                (Message)PacketTypes.DecodeMessage( this, p.PeekNextPart() ): null; p.GetNextPart();
            this.m_MessageIdentifier = SerializedPacket.DeserializeGuid( p.GetNextPart() );
        }

        /// <summary>
        /// Get the class Id for this object
        /// </summary>
        /// <returns>The class id</returns>
        public virtual int GetClassId() {
            return PacketTypes.MessageId;
        }

        #endregion

    }

    /// <summary>
    /// A place to put metadata about a message which we will use, initially to facilitate
    /// optimizations to the TCP Server sender.
    /// </summary>
    public class MessageTags {
        /// <summary>
        /// Identifies the slide to which the message pertains.  The value of Guid.Empty indicates
        /// a message of significance to the whole presentation, and has priority over messages tagged for
        /// a particular slide.  Messages tagged for a particular slide get priority when that slide
        /// is the current slide.
        /// </summary>
        public Guid SlideID;

        /// <summary>
        /// Used to indicate whether a message has only real-time significance, such as intermediate ink.
        /// Such messages may be dropped in constrained network conditions.
        /// </summary>
        public MessagePriority Priority;

        /// <summary>
        /// Used to set message ordering when bridging to multicast for CXP archive integration.
        /// </summary>
        public MessagePriority BridgePriority;

        /// <summary>
        /// Default values indicate a message has presentation global significance, and non-real-time priority.
        /// </summary>
        public MessageTags() {
            SlideID = Guid.Empty;
            Priority = MessagePriority.Default;
            BridgePriority = MessagePriority.Default;
        }

        public override string ToString() {
            return "MessageTags: Guid=" + SlideID.ToString() + "; Priority=" + Enum.GetName(typeof(MessagePriority), Priority);
        }
    }
}
