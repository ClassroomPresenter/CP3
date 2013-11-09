// $Id: InkTransformFilter.cs 2178 2010-04-12 23:48:03Z cmprince $

using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;

using Microsoft.StylusInput;
using Microsoft.StylusInput.PluginData;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Viewer;
using UW.ClassroomPresenter.Viewer.Slides;

namespace UW.ClassroomPresenter.Viewer.Inking {

    /// <summary>
    /// A synchronous <see cref="RealTimeStylus"/> plugin which adjusts ink coordinates
    /// by the inverse of the <see cref="SlideDisplayModel.InkTransform"/> property.
    /// </summary>
    /// <remarks>
    /// When collecting or displaying ink input to a <see cref="RealTimeStylus"/>
    /// which is attached to a <see cref="SlideViewer"/>, it is desirable for the
    /// ink's coordinates to be relative to the <c>SlideViewer</c>'s <i>slide</i>,
    /// rather than the screen on which the <c>SlideViewer</c> is displayed.
    /// <para>
    /// If you are using a "normal" <see cref="DynamicRenderer"/> to display ink
    /// drawn on the source <see cref="SlideViewer"/>, then the <see cref="DynamicRenderer"/>
    /// should be added to the <see cref="RealTimeStylus.SyncPluginCollection"/>
    /// <i>before</i> an instance of <see cref="InkTransformFilterScaledFromSlideViewer"/>.
    /// </para>
    /// <para>
    /// On the other hand, an instance of <see cref="TransformableDynamicRenderer"/>
    /// should be added to the <see cref="RealTimeStylus.SyncPluginCollection"/>
    /// <i>after</i> an instance of <see cref="InkTransformFilter"/>,
    /// so that the two transforms (being the inverse of each other) will cancel.
    /// </para>
    /// </remarks>
    public class InkTransformFilter : IStylusSyncPlugin, IDisposable {
        private readonly SlideDisplayModel m_SlideDisplay;
        private Matrix m_InverseTransform;
        private bool m_Disposed;

        #region Construction & Destruction

        public InkTransformFilter(SlideDisplayModel display) {
            this.m_SlideDisplay = display;

            this.m_SlideDisplay.Changed["InkTransform"].Add(new PropertyEventHandler(this.HandleInkTransformChanged));
            this.HandleInkTransformChanged(this.m_SlideDisplay, null);
        }

        ~InkTransformFilter() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            if(disposing) {
                this.m_SlideDisplay.Changed["InkTransform"].Remove(new PropertyEventHandler(this.HandleInkTransformChanged));
                this.m_InverseTransform.Dispose();
            }
            this.m_Disposed = true;
        }

        #endregion Construction & Destruction

        /// <summary>
        /// Modifies packet data according to the current <see cref="Transform">transform matrix</see>.
        /// </summary>
        /// <param name="data">The data to modify.</param>
        private void ModifyPacketData(StylusDataBase data) {
            // Create an array of points, one for each packet.
            Point[] points = new Point[data.Count / data.PacketPropertyCount];
            int i, j; // Declare outside the loop for the Debug.Assert below.

            // Convert the X and Y coordinates from the packet data into Points.
            for(i = 0, j = 0; i < data.Count; i += data.PacketPropertyCount, j++) {
                // X and Y are always the first two elements in a packet.
                points[j] = new Point(data[i], data[i+1]);
            }

            // We should have exactly as many points as there were packets in the array.
            Debug.Assert(i == data.Count);
            Debug.Assert(j == points.Length);

            // Apply the transformation to the points.
            this.m_InverseTransform.TransformPoints(points);

            // Store the modified X and Y coordinates back into the data.
            // The new values will be seen by all subsequent IStylus(A)SyncPlugins.
            for(i = 0, j = 0; i < data.Count; i += data.PacketPropertyCount, j++) {
                data[i] = points[j].X;
                data[i+1] = points[j].Y;
            }
        }

        private void HandleInkTransformChanged(object sender, PropertyEventArgs args) {
            using(Synchronizer.Lock(this.m_SlideDisplay.SyncRoot)) {

                Matrix transform = this.m_SlideDisplay.InkTransform.Clone();
                if (transform.IsInvertible) {
                    transform.Invert();
                    this.m_InverseTransform = transform;
                }
                else {
                    // If not invertible set to the identity matrix
                    this.m_InverseTransform = new Matrix();
                }
            }
        }

        #region IStylusSyncPlugin Members
        // Based on the RealTimeStylus Packet Filter example from
        // the Microsoft Tablet PC API Sample Applications collection.

        /// <summary>
        /// Occurs when the stylus touches the digitizer surface.
        /// Allocate a new array to store the packet data for this stylus.
        /// </summary>
        /// <param name="sender">The real time stylus associated with the notification</param>
        /// <param name="data">The notification data</param>
        void IStylusSyncPlugin.StylusDown(RealTimeStylus sender, StylusDownData data) {
            this.ModifyPacketData(data);
        }

        /// <summary>
        /// Occurs when the stylus moves on the digitizer surface.
        /// Add new packet data into the packet array for this stylus.
        /// </summary>
        /// <param name="sender">The real time stylus associated with the notification</param>
        /// <param name="data">The notification data</param>
        void IStylusSyncPlugin.Packets(RealTimeStylus sender, PacketsData data) {
            this.ModifyPacketData(data);
        }

        void IStylusSyncPlugin.InAirPackets(RealTimeStylus sender, InAirPacketsData data) {
            this.ModifyPacketData(data);
        }

        /// <summary>
        /// Occurs when the stylus leaves the digitizer surface.
        /// Retrieve the packet array for this stylus and use it to create
        /// a new stoke.
        /// </summary>
        /// <param name="sender">The real time stylus associated with the notification</param>
        /// <param name="data">The notification data</param>
        void IStylusSyncPlugin.StylusUp(RealTimeStylus sender, StylusUpData data) {
            this.ModifyPacketData(data);
        }

        /// <summary>
        /// Called when the current plugin or the ones previous in the list
        /// threw an exception.
        /// </summary>
        /// <param name="sender">The real time stylus</param>
        /// <param name="data">Error data</param>
        void IStylusSyncPlugin.Error(RealTimeStylus sender, ErrorData data) {
            Debug.Assert(false, null, "An error occurred while collecting ink.  Details:\n"
                + "DataId=" + data.DataId + "\n"
                + "Exception=" + data.InnerException + "\n"
                + "StackTrace=" + data.InnerException.StackTrace);
        }

        /// <summary>
        /// Defines the types of notifications the plugin is interested in.
        /// </summary>
        DataInterestMask IStylusSyncPlugin.DataInterest {
            get {
                return DataInterestMask.StylusDown
                    | DataInterestMask.Packets
                    | DataInterestMask.StylusUp
                    | DataInterestMask.Error
                    | DataInterestMask.InAirPackets;
            }
        }


        // The remaining interface methods are not used in this sample application.
        void IStylusSyncPlugin.RealTimeStylusDisabled(RealTimeStylus sender, RealTimeStylusDisabledData data) {}
        void IStylusSyncPlugin.RealTimeStylusEnabled(RealTimeStylus sender, RealTimeStylusEnabledData data){}
        void IStylusSyncPlugin.CustomStylusDataAdded(RealTimeStylus sender, CustomStylusData data) {}
        void IStylusSyncPlugin.StylusOutOfRange(RealTimeStylus sender, StylusOutOfRangeData data) {}
        void IStylusSyncPlugin.StylusInRange(RealTimeStylus sender, StylusInRangeData data) {}
        void IStylusSyncPlugin.StylusButtonDown(RealTimeStylus sender, StylusButtonDownData data) {}
        void IStylusSyncPlugin.StylusButtonUp(RealTimeStylus sender, StylusButtonUpData data) {}
        void IStylusSyncPlugin.SystemGesture(RealTimeStylus sender, SystemGestureData data) {}
        void IStylusSyncPlugin.TabletAdded(RealTimeStylus sender, TabletAddedData data) {}
        void IStylusSyncPlugin.TabletRemoved(RealTimeStylus sender, TabletRemovedData data) {}

        #endregion IStylusSyncPlugin Members
    }
}
