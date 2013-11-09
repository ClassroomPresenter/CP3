// $Id: InkAnnotationCollector.cs 2227 2013-07-22 23:23:44Z fred $

using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

using Microsoft.Ink;
using Microsoft.StylusInput;
using Microsoft.StylusInput.PluginData;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Model.Viewer;
using UW.ClassroomPresenter.Viewer.Slides;

namespace UW.ClassroomPresenter.Viewer.Inking {

    public class InkAnnotationCollector : IStylusAsyncPlugin, IDisposable, InkSheetAdapter.IAdaptee {
        private readonly Dictionary<int, List<int>> m_PacketsTable;
        private readonly Dictionary<int, DrawingAttributes> m_DrawingAttributesTable;
        private readonly Dictionary<int, int> m_StrokeIdTable;
        
        private InkSheetModel m_InkSheetModel;
        private RealTimeInkSheetModel m_RealTimeInkSheetModel;
        private DrawingAttributes m_DrawingAttributes;
        private bool m_TouchSupported = true;

        #region Construction & Destruction

        public InkAnnotationCollector() {
            this.m_PacketsTable = new Dictionary<int, List<int>>();
            this.m_DrawingAttributesTable = new Dictionary<int, DrawingAttributes>();
            this.m_StrokeIdTable = new Dictionary<int, int>();
        }

        ~InkAnnotationCollector() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
        }

        #endregion Construction & Destruction


        public InkSheetModel InkSheetModel {
            get {
                using(Synchronizer.Lock(this)) {
                    return this.m_InkSheetModel;
                }
            }

            set {
                using(Synchronizer.Lock(this)) {
                    this.m_InkSheetModel = value;
                }
            }
        }

        public RealTimeInkSheetModel RealTimeInkSheetModel {
            get {
                using(Synchronizer.Lock(this)) {
                    return this.m_RealTimeInkSheetModel;
                }
            }

            set {
                using(Synchronizer.Lock(this)) {
                    this.m_RealTimeInkSheetModel = value;

                    // Make sure the drawing attributes are in sync with ours.
                    // This lets any attached dynamic renderers update themselves,
                    // since the dynamic renderers probably aren't listening to the StylusInputSelector.
                    if(this.m_RealTimeInkSheetModel != null) {
                        using(Synchronizer.Lock(this.m_RealTimeInkSheetModel.SyncRoot)) {
                            this.m_RealTimeInkSheetModel.CurrentDrawingAttributes = this.m_DrawingAttributes;
                        }
                    }
                }
            }
        }


        #region IStylusAsyncPlugin Members
        // Copied from the RealTimeStylus InkCollection example from
        // the Microsoft Tablet PC API Sample Applications collection.

        /// <summary>
        /// Occurs when the stylus touches the digitizer surface.
        /// Allocate a new array to store the packet data for this stylus.
        /// </summary>
        /// <param name="sender">The real time stylus associated with the notification</param>
        /// <param name="data">The notification data</param>
        void IStylusAsyncPlugin.StylusDown(RealTimeStylus sender, StylusDownData data) {
            // An inverted stylus is reserved for the eraser.
            if(data.Stylus.Inverted)
                return;
            // Ignore if a touch input;
            if (m_TouchSupported) {
                try {
                    if (sender.GetTabletFromTabletContextId(data.Stylus.TabletContextId).DeviceKind == TabletDeviceKind.Touch)
                        return;
                }
                catch {
                    m_TouchSupported = false;
                }
            }

            int stylusId = data.Stylus.Id;


            // Allocate an empty array to store the packet data that will be
            // collected for this stylus.
            List<int> collectedPackets = new List<int>();

            // Add the packet data from StylusDown to the array
            collectedPackets.AddRange(data.GetData());

            // Insert the array into a hashtable using the stylus id as a key.
            this.m_PacketsTable[stylusId] = collectedPackets;

            // Also store the current DrawingAttributes.  This is necessary because the default
            // DynamicRenderer (which will be used in conjunction with the ink stored by this collector)
            // only updates its DrawingAttributes on StylusDown.
            this.m_DrawingAttributesTable[stylusId] = this.m_DrawingAttributes;

            // Increment the current stroke id.
            int strokeId;
            if (!this.m_StrokeIdTable.TryGetValue(stylusId, out strokeId))
                this.m_StrokeIdTable[stylusId] = (strokeId = 0);
            else this.m_StrokeIdTable[stylusId] = ++strokeId;

            // And send the packets to anybody who's listening to the RealTimeInk.
            using(Synchronizer.Lock(this)) {
                if(this.RealTimeInkSheetModel != null) {
                    TabletPropertyDescriptionCollection tabletProperties =
                        sender.GetTabletPropertyDescriptionCollection(data.Stylus.TabletContextId);
                    this.RealTimeInkSheetModel.OnStylusDown(stylusId, strokeId, data.GetData(), tabletProperties);
                }
            }
        }

        /// <summary>
        /// Occurs when the stylus moves on the digitizer surface.
        /// Add new packet data into the packet array for this stylus.
        /// </summary>
        /// <param name="sender">The real time stylus associated with the notification</param>
        /// <param name="data">The notification data</param>
        void IStylusAsyncPlugin.Packets(RealTimeStylus sender, PacketsData data) {
            int stylusId = data.Stylus.Id;
            List<int> collected;
            if (!this.m_PacketsTable.TryGetValue(stylusId, out collected)) {
                // If ink collection was disabled on StylusDown or if the user is erasing,
                // then ignore these packets.
                return;
            }

            int strokeId = this.m_StrokeIdTable[stylusId];
            int[] packets = data.GetData();

            // Use the stylus id as a key to retrieve the packet array for the
            // stylus.  Insert the new packet data into this array.
            collected.AddRange(packets);

            using(Synchronizer.Lock(this)) {
                if(this.RealTimeInkSheetModel != null) {
                    // Send the packets to anybody who's listening to the RealTimeInk.
                    this.RealTimeInkSheetModel.OnPackets(stylusId, strokeId, packets);
                }
            }
        }

        /// <summary>
        /// Occurs when the stylus leaves the digitizer surface.
        /// Retrieve the packet array for this stylus and use it to create
        /// a new stoke.
        /// </summary>
        /// <param name="sender">The real time stylus associated with the notification</param>
        /// <param name="data">The notification data</param>
        void IStylusAsyncPlugin.StylusUp(RealTimeStylus sender, StylusUpData data) {
            // Retrieve the packet array from the hashtable using the cursor id as a key.
            int stylusId = data.Stylus.Id;
            List<int> collected;
            if (!this.m_PacketsTable.TryGetValue(stylusId, out collected)) {
                // If ink collection was disabled on StylusDown or if the user is erasing,
                // then ignore these packets.
                return;
            }

            int strokeId = this.m_StrokeIdTable[stylusId];

            // Also get the DrawingAttributes which were in effect on StylusDown.
            DrawingAttributes atts = this.m_DrawingAttributesTable[stylusId];

            // Remove this entry from the hash tables since it is no longer needed.
            this.m_PacketsTable.Remove(stylusId);
            this.m_DrawingAttributesTable.Remove(stylusId);

            // Add the newly collected packet data from StylusUp to the array.
            collected.AddRange(data.GetData());

            // Assemble the completed information we'll need to create the stroke.
            int[] packets = collected.ToArray();
            TabletPropertyDescriptionCollection tabletProperties =
                sender.GetTabletPropertyDescriptionCollection(data.Stylus.TabletContextId);

            // Now that we have the data, we're ready to create the stroke and add it to our Ink object.
            using(Synchronizer.Lock(this)) { // Ensure that this.(RealTime)InkSheetModel aren't changed unexpectedly.
                // If there is no Ink object, then probably the SlideViewer is not looking at a slide,
                // so discard the stroke.  Otherwise, create the stroke from the collected packets.
                // Also discard the stroke if we have no DrawingAttributes.

                if(this.InkSheetModel != null && atts != null) {
                    int inkStrokeId;
                    using (Synchronizer.Lock(this.InkSheetModel.Ink.Strokes.SyncRoot)) {
                        Stroke stroke = this.InkSheetModel.Ink.CreateStroke(packets, tabletProperties);
                        stroke.DrawingAttributes = atts.Clone();
                        inkStrokeId = stroke.Id;
                    }
                    // Note that this ink stroke's Id is different from the strokeId used by the RealTimeInkSheetModel.
                    this.InkSheetModel.OnInkAdded(new StrokesEventArgs(new int[] { inkStrokeId }));
                }

                if(this.RealTimeInkSheetModel != null) {
                    this.RealTimeInkSheetModel.OnStylusUp(stylusId, strokeId, data.GetData());
                }
            }
        }

        /// <summary>
        /// Called when the current plugin or the ones previous in the list
        /// threw an exception.
        /// </summary>
        /// <param name="sender">The real time stylus</param>
        /// <param name="data">Error data</param>
        void IStylusAsyncPlugin.Error(RealTimeStylus sender, ErrorData data) {
            Debug.Assert(false, null, "An error occurred while collecting ink.  Details:\n"
                + "DataId=" + data.DataId + "\n"
                + "Exception=" + data.InnerException + "\n"
                + "StackTrace=" + data.InnerException.StackTrace);
        }

        /// <summary>
        /// Defines the types of notifications the plugin is interested in.
        /// </summary>
        DataInterestMask IStylusAsyncPlugin.DataInterest {
            get {
                return DataInterestMask.StylusDown
                    | DataInterestMask.Packets
                    | DataInterestMask.StylusUp
                    | DataInterestMask.Error
                    | DataInterestMask.CustomStylusDataAdded;
            }
        }

        void IStylusAsyncPlugin.CustomStylusDataAdded(RealTimeStylus sender, CustomStylusData data) {
            if(data.CustomDataId == StylusInputSelector.StylusData.Id) {
                using(Synchronizer.Lock(this)) {
                    // The new DrawingAttributes will take effect after the next StylusDown event.
                    this.m_DrawingAttributes =
                        ((StylusInputSelector.StylusData) data.Data).DrawingAttributes;

                    if(this.RealTimeInkSheetModel != null) {
                        using(Synchronizer.Lock(this.RealTimeInkSheetModel.SyncRoot)) {
                            this.RealTimeInkSheetModel.CurrentDrawingAttributes = this.m_DrawingAttributes;
                        }
                    }
                }
            }
        }

        // The remaining interface methods are not used in this sample application.
        void IStylusAsyncPlugin.RealTimeStylusDisabled(RealTimeStylus sender, RealTimeStylusDisabledData data) {}
        void IStylusAsyncPlugin.RealTimeStylusEnabled(RealTimeStylus sender, RealTimeStylusEnabledData data){}
        void IStylusAsyncPlugin.StylusOutOfRange(RealTimeStylus sender, StylusOutOfRangeData data) {}
        void IStylusAsyncPlugin.StylusInRange(RealTimeStylus sender, StylusInRangeData data) {}
        void IStylusAsyncPlugin.StylusButtonDown(RealTimeStylus sender, StylusButtonDownData data) {}
        void IStylusAsyncPlugin.StylusButtonUp(RealTimeStylus sender, StylusButtonUpData data) {}
        void IStylusAsyncPlugin.SystemGesture(RealTimeStylus sender, SystemGestureData data) {}
        void IStylusAsyncPlugin.InAirPackets(RealTimeStylus sender, InAirPacketsData data) {}
        void IStylusAsyncPlugin.TabletAdded(RealTimeStylus sender, TabletAddedData data) {}
        void IStylusAsyncPlugin.TabletRemoved(RealTimeStylus sender, TabletRemovedData data) {}

        #endregion IStylusAsyncPlugin Members
    }
}
