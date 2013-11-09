#if RTP_BUILD

using System;
using System.Collections.Generic;
using System.Text;
using MSR.LST.ConferenceXP;

namespace UW.ClassroomPresenter.Viewer {
    /// <summary>
    /// There exists a mode of interoperability with ConferenceXP (a platform for real-time collaborative applications) 
    /// referred to as ConferenceXP Capability.  A ConferenceXP Capability can be launched from within the ConferenceXP
    /// Client conferencing application.  It leverages the existing network channel, and is launched automatically on
    /// remote clients as needed. A ConferenceXP Capability is an application packaged in a DLL with class
    /// that has certain attributes, inheritance, and interfaces, and most likely a form which is a subclass of 
    /// CapabilityForm.  This is the class that provides the attributes and has a couple of the many possible 
    /// overrides which influence the behavior.  Viewer.cs is the form that inherits from CapabilityForm.
    /// </summary>
    [Capability.Name("Classroom Presenter 3")]
    [Capability.PayloadType(PayloadType.dynamicPresentation)]
    [Capability.FormType(typeof(ViewerForm))]
    [Capability.Channel(true)]
    public class PresenterCapability : CapabilityWithWindow, ICapabilitySender, ICapabilityViewer {
        public bool IsPlayingAndSending = false;

        // Required ctor for ICapabilitySender
        public PresenterCapability() : base() {}

        // Required ctor for ICapabilityViewer
        public PresenterCapability(DynamicProperties dynaProps) : base(dynaProps) { }

        #region Stream Add/Remove

        public override void StreamAdded(MSR.LST.Net.Rtp.RtpStream rtpStream) {
            base.StreamAdded(rtpStream);
            if (OnStreamAdded != null) {
                OnStreamAdded(rtpStream);
            }
        }

        public override void StreamRemoved(MSR.LST.Net.Rtp.RtpStream rtpStream) {
            base.StreamRemoved(rtpStream);
            if (OnStreamRemoved != null) {
                OnStreamRemoved(rtpStream);
            }
        }

        public event OnStreamAddedHandler OnStreamAdded;
        public event OnStreamRemovedHandler OnStreamRemoved;
        public event OnPlayHandler OnPlay;

        public delegate void OnStreamAddedHandler(MSR.LST.Net.Rtp.RtpStream rtpStream);
        public delegate void OnStreamRemovedHandler(MSR.LST.Net.Rtp.RtpStream rtpStream);
        public delegate void OnPlayHandler();

        #endregion Stream Add/Remove
        
        /// <summary>
        /// This is a 2-way capability (it is always a sender and receiver)
        /// So when we are initialized to Play, make sure we Send also
        /// </summary>
        public override void Play()
        {
            base.Play ();
            Send();

            //This is the point at which it is safe for the Viewer to begin sending and receiving.
            //We raise an event to indicate this.
            bool callOnPlay = false;
            lock (this) { //Synchronize with viewer.cs
                IsPlayingAndSending = true;
                if (OnPlay != null) {
                    callOnPlay = true;
                }
            }
            if (callOnPlay) { 
                OnPlay();
            }
        }

        public override void StopPlaying()
        {
            base.StopPlaying ();
            StopSending();
        }
    }
}
#endif