
#if RTP_BUILD
using System;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Network.Chunking;

namespace UW.ClassroomPresenter.Network.RTP {
    /// <summary>
    /// Handles sending and receiving NACK messages, ensuring that missing message chunks
    /// are not requested more than once, and implementing a traffic control scheme to avoid
    /// flooding a busy classroom network.
    /// </summary>
    /// <remarks>
    /// The traffic control scheme works as follows:
    /// <list type="unordered">
    /// <item>
    /// The <see cref="RTPNackManager"/> keeps two <see cref="DisjointSet"/> instances:
    /// the "stable" set, which always contains the <emph>complete</emph> set of missing
    /// sequence numbers, and the "current" set (a subset of the "stable" set), which
    /// is periodically broadcast as part of a <see cref="RtpNackMessage"/>.
    /// </item>
    /// <item>
    /// Whenever an <see cref="RTPMessageReceiver"/> notices that a range of frame sequence
    /// numbers has been dropped, <see cref="Nack"/> is invoked, passing the missing range.
    /// </item>
    /// <item>
    /// The new missing range is immediately added to both the reference set and the nack set.
    /// </item>
    /// <item>
    /// Whenever the reference set is not empty, a timer is activated, which invokes <see cref="Flush"/>
    /// at random intervals.  The random distribution scales linearly with the number of
    /// participants connected to the classroom, so as to avoid flooding the classroom with
    /// NACKs when frames are dropped globally.
    /// </item>
    /// <item>
    /// When the timer fires (invoking <see cref="Flush"/>), the current nack set is broadcast
    /// if it is not empty, and then the current set is cleared.
    /// </item>
    /// <item>
    /// Even before the timer fires, if another NACK is received from another client connected
    /// to the classroom, and if that NACK is addressed to the same sender as our NACK would be,
    /// then its set of sequence numbers is <emph>subtracted</emph>from our nack set.
    /// This, along with the random waiting period before sending NACKs in the first place,
    /// ensures that duplicate NACKs will be rare.  This avoids flooding the network.
    /// </item>
    /// <item>
    /// Whenver new chunks are received, or when the <see cref="RTPMessageReceiver"/> "gives up"
    /// on a message because the sender no longer holds the message's chunks in its buffer,
    /// those chunks' sequence numbers are removed from both the stable and current sets.
    /// </item>
    /// <item>
    /// Periodically (on a scale several times longer than the "nack" timer), a second timer
    /// copies the stable set to the nack set.  Eventually (if another client does not 
    /// re-send the NACK first), the sequences remaining in the reference set are thus re-NACKed.
    /// This mitigates the problem when NACK messages themselves are also dropped by the network,
    /// or when the chunks rebroadcast by the original sender are lost a second time.  
    /// </item>
    /// </list>
    /// </remarks>
    public class RTPNackManager : IDisposable {
        /// <summary>
        /// A table mapping SSRCs to <see cref="Sets"/>, with one entry for each participant 
        /// in the classroom which has pending NACKs that we might send.
        /// </summary>
        private Dictionary<uint, Sets> m_Sets = new Dictionary<uint, Sets>();

        /// <summary>
        /// Each timer cycle pauses a random time between <c>0</c> and <c>(n+1) * TIMER_SCALE</c>,
        /// where <c>n</c> is the number of REMOTE participants in the classroom.
        /// </summary>
        private const int TIMER_SCALE = 250;
        private int m_MaxRandomWait = TIMER_SCALE;

        /// <summary>
        /// Generates random numbers for the timer.
        /// </summary>
        private readonly Random m_Random = new Random();

        /// <summary>
        /// All non-thread-safe operations should lock on this mutex.
        /// </summary>
        private readonly object m_Lock = new object();

        // TODO: tune REFRESH_SCALE after testing in a classroom environment.
        /// <summary>
        /// The stable set is copied to the current set after <c>REFRESH_SCALE</c> iterations of the timer.
        /// </summary>
        private const int REFRESH_SCALE = 3;

        /// <summary>
        /// The RTP sender used to send NACK messages.
        /// </summary>
        private readonly IRTPMessageSender m_Sender;

        private readonly ClassroomModel m_Classroom;

        public RTPNackManager(IRTPMessageSender sender, ClassroomModel classroom) {
            this.m_Sender = sender;

            this.m_Classroom = classroom;
            this.m_Classroom.Changed["Participants"].Add(new PropertyEventHandler(this.HandleClassroomParticipantsChanged));

            Thread thread = new Thread(new ThreadStart(this.FlusherThread));
            thread.Name = "RTPNackManager.FlusherThread";
            thread.IsBackground = true;
            thread.Start();
        }

        #region IDisposable Members

        private bool m_Disposed;

        ~RTPNackManager() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
        }

        protected virtual void Dispose(bool disposing) {
            this.m_Disposed = true;
            if(disposing) {
                using(Synchronizer.Cookie cookie = Synchronizer.Lock(this.m_Lock)) {
                    if(this.m_Disposed) return;
                    this.m_Disposed = true;

                    this.m_Classroom.Changed["Participants"].Remove(new PropertyEventHandler(this.HandleClassroomParticipantsChanged));

                    // Setting m_Disposed to true will cause the thread to stop, once we pulse the monitor.
                    Monitor.Pulse(this.m_Lock);
                }
            }
        }

        #endregion

        private void HandleClassroomParticipantsChanged(object sender, PropertyEventArgs args) {
            using(Synchronizer.Lock(this.m_Classroom.SyncRoot)) {
                // Scale the timer wait linearly by the number of participants in the classroom.
                // (Add 1 because the count does not include the local participant, to avoid zero delay.)
                int count = this.m_Classroom.Participants.Count + 1;
                this.m_MaxRandomWait = TIMER_SCALE * count;
            }
        }

        public void Nack(uint ssrc, DisjointSet missingSequences) {
            using(Synchronizer.Lock(this.m_Lock)) {
                Sets sets;
                if(!this.m_Sets.TryGetValue(ssrc, out sets))
                    this.m_Sets.Add(ssrc, sets = new Sets(ssrc));
                sets.Nack(missingSequences);
            }
        }

        public void Delay(uint ssrc, DisjointSet nackedBySomeoneElse) {
            using(Synchronizer.Lock(this.m_Lock)) {
                Sets sets;
                if(this.m_Sets.TryGetValue(ssrc, out sets))
                    sets.Delay(nackedBySomeoneElse);
            }
        }

        public void Discard(uint ssrc, DisjointSet receivedOrGivenUpSequences) {
            using(Synchronizer.Lock(this.m_Lock)) {
                Sets sets;
                if(this.m_Sets.TryGetValue(ssrc, out sets)) {
                    sets.Discard(receivedOrGivenUpSequences);
                    if(sets.Stable == null)
                        this.m_Sets.Remove(ssrc);
                }
            }
        }

        protected void FlusherThread() {
            using(Synchronizer.Cookie cookie = Synchronizer.Lock(this.m_Lock)) {
                for(int refresh = 0;;) {
                    try {
                        if(this.m_Disposed) return;

                        // Wait a random amount of time (a different period each time).
                        cookie.Wait(this.m_Random.Next(this.m_MaxRandomWait));

                        if(this.m_Disposed) return;

                        foreach(Sets sets in this.m_Sets.Values) {
                            // Send an RtpNackMessage containing the current nack set (if not empty).
                            if(sets.Current != null) {
                                this.m_Sender.SendNack(new RtpNackMessage(sets.SSRC, sets.Current));
                                sets.Flush();
                            }
                        }

                        // After a certain amount of time, copy the entire reference set
                        // to the nack set.  However, do this *after* flushing, so we wait
                        // a little while to see if other clients will send the same NACK.
                        if(++refresh > REFRESH_SCALE) {
                            refresh = 0;
                            foreach(Sets sets in this.m_Sets.Values)
                                sets.Refresh();
                        }

                    } catch(Exception e) {
                        // Report the error, but do not break out of the thread.
                        // TODO: Log the error to the system event log.
                        Trace.Fail("RTPNackManager encountered an error: " + e.ToString(), e.StackTrace);
                    }
                }
            }
        }

        private class Sets {
            public DisjointSet Stable;
            public DisjointSet Current;
            public readonly uint SSRC;

            public Sets(uint ssrc) {
                this.SSRC = ssrc;
            }

            public void Refresh() {
                // Copy the reference set to the nack set, causing any chunks in the
                // reference set that have already been NACKed, but whose NACKs were lost,
                // to be re-NACKed.
                this.Current = this.Stable == null ? null : this.Stable.Clone();

                this.Debugger("Refreshed", this.Stable);
            }

            public void Nack(DisjointSet missingSequences) {
                // Add the set of missing sequence numbers to our "reference" set.
                DisjointSet.Add(ref this.Stable, missingSequences);

                // Immediately enqueue these sequence numbers to be sent as part of the next NACK
                // (if we don't receive any other NACK containing them from another client first).
                DisjointSet.Add(ref this.Current, missingSequences);

                this.Debugger("Nacked", missingSequences);
            }

            public void Delay(DisjointSet nackedBySomeoneElse) {
                // Remove the sequences from the current set so as not to duplicate someone
                // else's NACK that we eavesdropped.  But keep it in the stable set so
                // we'll re-nack if we never see a reply.
                DisjointSet.Remove(ref this.Current, nackedBySomeoneElse);

                this.Debugger("Delayed", nackedBySomeoneElse);
            }

            public void Discard(DisjointSet receivedOrGivenUpSequences) {
                // Remove the discarded sequence numbers from both sets (ensuring that
                // the nack set remains a subset of the reference set).
                DisjointSet.Remove(ref this.Stable, receivedOrGivenUpSequences);
                DisjointSet.Remove(ref this.Current, receivedOrGivenUpSequences);

                this.Debugger("Discarded", receivedOrGivenUpSequences);
            }

            public void Flush() {
#if DEBUG
                DisjointSet clone = this.Current.Clone();
#endif

                this.Current = null;

#if DEBUG
                this.Debugger("Flushed", clone);
#endif
            }

            [Conditional("DEBUG")]
            private void Debugger(String type, DisjointSet delta) {
                DisjointSet clone = this.Current == null ? null : this.Current.Clone();
                DisjointSet.Remove(ref clone, this.Stable);
                Debug.Assert(clone == null, "Stable set is not a superset of the Current set",
                    (clone == null ? null : string.Format("After {0} {1}, difference is {2}\nCurrent set is {3}\nStable set is {4}",
                    type, delta, clone.ToString(), this.Current.ToString(), this.Stable.ToString())));

                clone = this.Stable == null ? null : this.Stable.Clone();
                DisjointSet.Remove(ref clone, this.Current);
                Debug.WriteLine(string.Format("SSRC {0}: {1} {2} => current is {3}, stable adds {4}",
                    this.SSRC, type, delta, 
                    this.Current == null ? "{}" : this.Current.ToString(),
                    clone == null ? "{}" : clone.ToString()), this.GetType().ToString());
            }
        }
    }
}
#endif
