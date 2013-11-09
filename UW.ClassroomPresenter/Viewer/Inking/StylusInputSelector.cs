// $Id: StylusInputSelector.cs 564 2005-08-24 20:42:26Z pediddle $

using System;
using System.Threading;

using Microsoft.Ink;
using Microsoft.StylusInput;
using Microsoft.StylusInput.PluginData;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Stylus;

namespace UW.ClassroomPresenter.Viewer.Inking {
    public class StylusInputSelector : IStylusSyncPlugin, IDisposable {

        private readonly PresenterModel m_Model;
        private readonly RealTimeStylus m_RealTimeStylus;
        private DrawingAttributes m_DrawingAttributes;
        private StylusModel m_Stylus;
        private bool m_Disposed;
        private bool m_Enabled;


        #region Construction & Destruction

        public StylusInputSelector(PresenterModel model, RealTimeStylus rts) {
            this.m_Model = model;
            this.m_RealTimeStylus = rts;

            this.m_Model.Changed["Stylus"].Add(new PropertyEventHandler(this.HandleStylusChanged));

            using(Synchronizer.Lock(this.m_Model)) {
                this.HandleStylusChanged(this.m_Model, null);
            }
        }

        ~StylusInputSelector() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            if(disposing) {
                this.m_Model.Changed["Stylus"].Remove(new PropertyEventHandler(this.HandleStylusChanged));
                // Unregister the stylus event listeners via the Stylus setter.
                this.Stylus = null;
            }
            this.m_Disposed = true;
        }

        #endregion Construction & Destruction


        /// <summary>
        /// Sets the current stylus.
        /// </summary>
        /// <remarks>
        /// This method is not thread-safe.
        /// </remarks>
        protected StylusModel Stylus {
            get { return this.m_Stylus; }
            set {
                if(this.m_Stylus is PenStylusModel) {
                    this.m_Stylus.Changed["DrawingAttributes"].Remove(new PropertyEventHandler(this.HandleDrawingAttributesChanged));
                }

                this.m_Stylus = value;

                if(this.m_Stylus is PenStylusModel) {
                    this.m_Stylus.Changed["DrawingAttributes"].Add(new PropertyEventHandler(this.HandleDrawingAttributesChanged));

                    using(Synchronizer.Lock(this.m_Stylus.SyncRoot)) {
                        this.HandleDrawingAttributesChanged(this.m_Stylus, null);
                    }
                } else {
                    // HandleDrawingAttributesChanged will enqueue a StylusData packet regardless of whether the stylus is a PenModel.
                    this.HandleDrawingAttributesChanged(null, null);
                }
            }
        }

        private void HandleStylusChanged(object sender, PropertyEventArgs args) {
            using(Synchronizer.Lock(this.m_Model.SyncRoot)) {
                this.Stylus = this.m_Model.Stylus;
            }
        }

        private void HandleDrawingAttributesChanged(object sender, PropertyEventArgs args) {
            using(Synchronizer.Lock(this)) { // Ensure that this.Stylus can't change.
                DrawingAttributes atts;

                if(this.Stylus != null) {
                    using(Synchronizer.Lock(this.m_Stylus.SyncRoot)) {
                        atts = (this.Stylus is PenStylusModel)
                            ? ((PenStylusModel) this.Stylus).DrawingAttributes : null;
                    }
                } else {
                    atts = null;
                }

                this.m_DrawingAttributes = atts;

                // Enqueue a StylusData packet whether or not there are any DrawingAttributes.
                this.EnqueueStylusCustomData();
            }
        }

        private void EnqueueStylusCustomData() {
            using(Synchronizer.Lock(this)) {
                if(this.m_Enabled)
                    this.m_RealTimeStylus.AddCustomStylusDataToQueue(StylusQueues.Input,
                        StylusData.Id, new StylusData(this.m_Stylus, this.m_DrawingAttributes));
            }
        }

        public class StylusData {
            public static readonly Guid Id = Guid.NewGuid();

            private readonly StylusModel m_Stylus;
            private readonly DrawingAttributes m_DrawingAttributes;

            internal StylusData(StylusModel stylus, DrawingAttributes atts) {
                this.m_Stylus = stylus;
                this.m_DrawingAttributes = atts;
            }

            public StylusModel Stylus {
                get { return this.m_Stylus; }
            }

            public DrawingAttributes DrawingAttributes {
                get { return this.m_DrawingAttributes; }
            }
        }


        #region IStylusSyncPlugin Members

        void IStylusSyncPlugin.RealTimeStylusEnabled(RealTimeStylus sender, RealTimeStylusEnabledData data) {
            using(Synchronizer.Lock(this)) {
                this.m_Enabled = true;
                this.EnqueueStylusCustomData();
            }
        }

        void IStylusSyncPlugin.RealTimeStylusDisabled(RealTimeStylus sender, RealTimeStylusDisabledData data) {
            using(Synchronizer.Lock(this)) {
                this.m_Enabled = false;
            }
        }

        DataInterestMask IStylusSyncPlugin.DataInterest {
            get {
                return DataInterestMask.RealTimeStylusEnabled
                    | DataInterestMask.RealTimeStylusDisabled;
            }
        }

        void IStylusSyncPlugin.CustomStylusDataAdded(RealTimeStylus sender, CustomStylusData data) {}
        void IStylusSyncPlugin.TabletRemoved(RealTimeStylus sender, TabletRemovedData data) {}
        void IStylusSyncPlugin.StylusOutOfRange(RealTimeStylus sender, StylusOutOfRangeData data) {}
        void IStylusSyncPlugin.StylusButtonUp(RealTimeStylus sender, StylusButtonUpData data) {}
        void IStylusSyncPlugin.StylusInRange(RealTimeStylus sender, StylusInRangeData data) {}
        void IStylusSyncPlugin.SystemGesture(RealTimeStylus sender, SystemGestureData data) {}
        void IStylusSyncPlugin.StylusDown(RealTimeStylus sender, StylusDownData data) {}
        void IStylusSyncPlugin.TabletAdded(RealTimeStylus sender, TabletAddedData data) {}
        void IStylusSyncPlugin.Error(RealTimeStylus sender, ErrorData data) {}
        void IStylusSyncPlugin.InAirPackets(RealTimeStylus sender, InAirPacketsData data) {}
        void IStylusSyncPlugin.Packets(RealTimeStylus sender, PacketsData data) {}
        void IStylusSyncPlugin.StylusUp(RealTimeStylus sender, StylusUpData data) {}
        void IStylusSyncPlugin.StylusButtonDown(RealTimeStylus sender, StylusButtonDownData data) {}

        #endregion IStylusSyncPlugin Members
    }
}
