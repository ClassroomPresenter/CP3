// $Id: BeaconService.cs 1158 2006-08-30 21:32:00Z pediddle $

using System;
using System.Diagnostics;
using System.Threading;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;

using UW.ClassroomPresenter.Network.Messages;
using UW.ClassroomPresenter.Network.Messages.Network;
using UW.ClassroomPresenter.Network.Messages.Presentation;

namespace UW.ClassroomPresenter.Network.Beacons {
    public abstract class BeaconService : IDisposable {
        private readonly SendingQueue m_Sender;
        private TimeSpan m_BeaconInterval = new TimeSpan(0, 0, 0, 0, Timeout.Infinite);
        private bool m_Disposed;

        public BeaconService(SendingQueue sender) {
            this.m_Sender = sender;

            Thread thread = new Thread(new ThreadStart(this.BeaconSenderThreadStart));
            thread.IsBackground = true;
            thread.Start();
        }

        ~BeaconService() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if(disposing) {
                using(Synchronizer.Lock(this)) {
                    this.m_Disposed = true;
                    Monitor.PulseAll(this);
                }
            } else {
                this.m_Disposed = true;
            }
        }

        protected TimeSpan BeaconInterval {
            get { return this.m_BeaconInterval; }
            set {
                using(Synchronizer.Lock(this)) {
                    this.m_BeaconInterval = value;
                    Monitor.PulseAll(this);
                }
            }
        }
        protected abstract Message MakeBeaconMessage();

        private void BeaconSenderThreadStart() {
            while(true) {
                try {
                    using(Synchronizer.Cookie cookie = Synchronizer.Lock(this)) {
                        if(this.m_Disposed)
                            return;

                        // Get the time remaining before sending the next beacon.
                        TimeSpan interval = this.BeaconInterval;
                        long remaining = interval.Ticks;

                        // Get the current time so we can see how much time we've actually waited.
                        long start = DateTime.Now.Ticks;
                        do {
                            // Wait until either the time expired, or until Monitor.Pulse(this)
                            // indicates that the BeaconService's state has changed.
                            cookie.Wait(new TimeSpan(remaining));

                            // Return immediately if we've been disposed.
                            if(this.m_Disposed)
                                return;

                            // If the interval was previously infinite,
                            // restart the timer from scratch with the new interval.
                            if(remaining < 0) {
                                remaining = this.BeaconInterval.Ticks;
                                continue;
                            }

                            // Subtract the actual time elapsed while waiting
                            // (this will be less than "remaining" if we were Pulsed).
                            remaining -= (DateTime.Now.Ticks - start);

                            // In case the interval has changed, get the new interval.
                            TimeSpan replaced = this.BeaconInterval;
                            if(replaced.Ticks < 0) {
                                // Special case if the new interval is infinite;
                                // otherwise the math is wrong since -1 milliseconds is a special value.
                                remaining = replaced.Ticks;
                                continue;
                            } else {
                                // Add or subtract the difference between the new and old intervals.
                                // If the new interval is longer, this increases the time remaining.
                                // if the new interval is shorter, this decreases the time remaining.
                                remaining += (replaced.Ticks - interval.Ticks);
                                interval = replaced;
                            }

                            // As long as we've got more time before the next beacon, repeat.
                        } while(remaining > 0);

                        Message message = this.MakeBeaconMessage();
                        if(message != null)
                            // Ideally, the beacon message wouldn't be subject to rebroadcast if dropped.
                            // It's periodic, so subsequent beacons will replace any dropped frames.
                            // But we want beacon messages to trigger rebroadcast of *other* dropped frames.
                            // Also, the beacon messages should be processed in the same order as other
                            // messages dealing with the state of the presentation, to avoid timing issues.
                            // Therefore, beacon messages are sent with Default priority instead of RealTime.
                            this.m_Sender.Send(message, MessagePriority.Default);
                    }
                } catch(Exception e) {
                    Trace.WriteLine("BeaconService encountered an error: "
                        + e.ToString() + "\r\n" + e.StackTrace);
                }
            }
        }
    }
}
