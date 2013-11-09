// $Id: EraserPlugin.cs 2227 2013-07-22 23:23:44Z fred $

using System;
using System.Collections;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

using Microsoft.Ink;
using Microsoft.StylusInput;
using Microsoft.StylusInput.PluginData;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Stylus;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Model.Viewer;

namespace UW.ClassroomPresenter.Viewer.Inking {

    public class EraserPlugin : IDisposable, IStylusSyncPlugin, InkSheetAdapter.IAdaptee {
        private readonly SlideDisplayModel m_Display;
        private readonly Renderer m_Renderer;
        private Rectangle m_DisplayBounds;
        private EraserStylusModel m_Eraser;
        private InkSheetModel m_Sheet;
        private bool m_Disposed;
        private bool m_TouchSupported = true;

        /// <summary>
        /// The radius from the eraser cursor in which strokes are deleted.
        /// This is in HIMETRIC units in ink-space (<i>before</i> ink-space is scaled 
        /// and transformed according <see cref="SlideDisplayModel.InkTransform"/>).
        /// </summary>
        private const float ERASER_RADIUS = 200;

        #region Construction & Destruction

        public EraserPlugin(SlideDisplayModel display) {
            this.m_Display = display;
            this.m_Renderer = new Renderer();

            this.m_Display.Changed["Bounds"].Add(new PropertyEventHandler(this.HandleDisplayBoundsChanged));
            this.m_Display.Changed["InkTransform"].Add(new PropertyEventHandler(this.HandleDisplayInkTransformChanged));

            this.HandleDisplayBoundsChanged(this.m_Display, null);
            this.HandleDisplayInkTransformChanged(this.m_Display, null);
        }

        ~EraserPlugin() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (this.m_Disposed) return;
            if (disposing) {
                this.m_Display.Changed["Bounds"].Remove(new PropertyEventHandler(this.HandleDisplayBoundsChanged));
                this.m_Display.Changed["InkTransform"].Remove(new PropertyEventHandler(this.HandleDisplayInkTransformChanged));
            }
            this.m_Disposed = true;
        }

        #endregion Construction & Destruction


        #region InkSheetAdapter.IAdaptee Members

        public InkSheetModel InkSheetModel {
            get {
                using (Synchronizer.Lock(this)) {
                    return this.m_Sheet;
                }
            }

            set {
                using (Synchronizer.Lock(this)) {
                    if (this.m_Sheet != null) {
                        using (Synchronizer.Lock(this.m_Sheet.SyncRoot)) {
                            this.m_Sheet.Selection = null;
                        }
                    }

                    this.m_Sheet = value;

                    if (this.m_Sheet != null) {
                        using (Synchronizer.Lock(this.m_Sheet.SyncRoot)) {
                            this.m_Sheet.Selection = null;
                        }
                    }
                }
            }
        }

        RealTimeInkSheetModel InkSheetAdapter.IAdaptee.RealTimeInkSheetModel {
            set {
                // Ignore, since we don't care about the RealTimeInkSheetModel's events.
            }
        }

        #endregion InkSheetAdapter.IAdaptee Members

        protected EraserStylusModel Eraser {
            get {
                using(Synchronizer.Lock(this)) {
                    return this.m_Eraser;
                }
            }

            set {
                using(Synchronizer.Lock(this)) {
                    // Right now, LassoStylusModel has no data, so no need to (un)register any listeners.
                    this.m_Eraser = value;
                }
            }
        }

        private void HandleDisplayBoundsChanged(object sender, PropertyEventArgs args) {
            using (Synchronizer.Lock(this.m_Display.SyncRoot)) {
                this.m_DisplayBounds = this.m_Display.Bounds;
            }
        }

        private void HandleDisplayInkTransformChanged(object sender, PropertyEventArgs args) {
            using (Synchronizer.Lock(this.m_Display.SyncRoot)) {
                this.m_Renderer.SetViewTransform(this.m_Display.InkTransform);
            }
        }

        /// <summary>
        /// Erases strokes that overlap the cursor.
        /// </summary>
        /// <param name="sender">The real time stylus associated with the notification</param>
        /// <param name="data">The notification data</param>
        /// <seealso cref="EraserPlugin.StylusDown"/>
        /// <seealso cref="EraserPlugin.Packets"/>
        /// <seealso cref="EraserPlugin.StylusUp"/>
        private void HandlePackets(RealTimeStylus sender, StylusDataBase data) {
            using (Synchronizer.Lock(this)) {
                // Ignore the strokes if the eraser stylus is not selected,
                // and if the stylus is not inverted.
                if (this.Eraser == null && !data.Stylus.Inverted)
                    return;

                // Ignore if a touch input
                if (m_TouchSupported) {
                    try {
                        if (sender.GetTabletFromTabletContextId(data.Stylus.TabletContextId).DeviceKind == TabletDeviceKind.Touch)
                            return;
                    }
                    catch {
                        m_TouchSupported = false;
                    }
                }

                // Ignore the strokes if no ink sheet is selected.
                InkSheetModel sheet = this.InkSheetModel;
                if (sheet == null)
                    return;

                // Convert the X and Y coordinates of the data,
                // which are defined to be at offsets [i] and [i+1],
                // to an array of Points.
                Debug.Assert(data.Count % data.PacketPropertyCount == 0);
                Point[] points = new Point[data.Count / data.PacketPropertyCount];
                for (int i = 0, j = 0, il = data.Count, inc = data.PacketPropertyCount; i < il; i += inc, j++)
                    points[j] = new Point(data[i], data[i + 1]);

                // Convert the ink points to pixels so we can 
                // ignore the ones that are outside the slide view area.
                // This is done all at once to conserve resources used by the Graphics object.
                Point[] pixels = points;
                using (Synchronizer.Lock(this.m_Display.SyncRoot))
                    using (Graphics g = this.m_Display.CreateGraphics())
                        this.m_Renderer.InkSpaceToPixel(g, ref pixels);

                // Prevent anyone else from accessing the ink concurrently.
                using (Synchronizer.Lock(this.InkSheetModel.Ink.Strokes.SyncRoot)) {
                    // Iterate through each point through which the cursor has passed.
                    for(int i = 0, il = points.Length; i < il; i++) {
                        // Don't erase anything when the cursor is outside of the
                        // slide viewing area.  This prevents users from accidentally
                        // erasing strokes they can't see, especially when using the
                        // slide zoom feature.
                        if (!this.m_DisplayBounds.Contains(pixels[i]))
                            continue;

                        // Find all strokes within some radius from the cursor.
                        Strokes erased = sheet.Ink.HitTest(points[i], ERASER_RADIUS);

                        // If any strokes were found, erase them.
                        if (erased.Count > 0) {
                            // Get the list of stroke IDs in order to send an event
                            int[] ids = new int[erased.Count];
                            for (int j = 0; j < ids.Length; j++)
                                ids[j] = erased[j].Id;

                            // We must first warn listeners that the strokes are about to
                            // be deleted, because after they're deleted, no information
                            // about them can be recovered.  This is used to send
                            // network events and to store undo information.
                            sheet.OnInkDeleting(new StrokesEventArgs(ids));

                            // Delete the erased strokes.
                            sheet.Ink.DeleteStrokes(erased);

                            // Inform listeners that the strokes have actually been deleted.
                            // This causes slide displays to refresh.
                            sheet.OnInkDeleted(new StrokesEventArgs(ids));
                        }
                    }
                }
            }
        }

        #region IStylusSyncPlugin Members
        // Based on the RealTimeStylus examples from the Microsoft Tablet PC API Sample Applications collection.

        /// <summary>
        /// Occurs when the stylus touches the digitizer surface.
        /// </summary>
        /// <param name="sender">The real time stylus associated with the notification</param>
        /// <param name="data">The notification data</param>
        void IStylusSyncPlugin.StylusDown(RealTimeStylus sender, StylusDownData data) {
            this.HandlePackets(sender, data);
        }

        /// <summary>
        /// Occurs when the stylus moves on the digitizer surface.
        /// </summary>
        /// <param name="sender">The real time stylus associated with the notification</param>
        /// <param name="data">The notification data</param>
        void IStylusSyncPlugin.Packets(RealTimeStylus sender, PacketsData data) {
            this.HandlePackets(sender, data);
        }

        /// <summary>
        /// Occurs when the stylus leaves the digitizer surface.
        /// </summary>
        /// <param name="sender">The real time stylus associated with the notification</param>
        /// <param name="data">The notification data</param>
        void IStylusSyncPlugin.StylusUp(RealTimeStylus sender, StylusUpData data) {
            this.HandlePackets(sender, data);
        }

        /// <summary>
        /// Defines the types of notifications the plugin is interested in.
        /// </summary>
        DataInterestMask IStylusSyncPlugin.DataInterest {
            get {
                return DataInterestMask.StylusDown
                    | DataInterestMask.Packets
                    | DataInterestMask.StylusUp
                    | DataInterestMask.CustomStylusDataAdded
                    | DataInterestMask.StylusInRange
                    | DataInterestMask.StylusOutOfRange
                    | DataInterestMask.InAirPackets;
            }
        }

        /// <summary>
        /// Occurs when custom data is inserted into the <see cref="RealTimeStylus"/> queue by other plugins.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="data"></param>
        void IStylusSyncPlugin.CustomStylusDataAdded(RealTimeStylus sender, CustomStylusData data) {
            if (data.CustomDataId == StylusInputSelector.StylusData.Id) {
                using (Synchronizer.Lock(this)) {
                    this.Eraser = (((StylusInputSelector.StylusData)data.Data).Stylus as EraserStylusModel);
                }
            }
        }

        // The remaining interface methods are not used in this application.
        void IStylusSyncPlugin.RealTimeStylusDisabled(RealTimeStylus sender, RealTimeStylusDisabledData data) { }
        void IStylusSyncPlugin.RealTimeStylusEnabled(RealTimeStylus sender, RealTimeStylusEnabledData data) { }
        void IStylusSyncPlugin.StylusButtonDown(RealTimeStylus sender, StylusButtonDownData data) { }
        void IStylusSyncPlugin.StylusButtonUp(RealTimeStylus sender, StylusButtonUpData data) { }
        void IStylusSyncPlugin.StylusInRange(RealTimeStylus sender, StylusInRangeData data) { }
        void IStylusSyncPlugin.StylusOutOfRange(RealTimeStylus sender, StylusOutOfRangeData data) { }
        void IStylusSyncPlugin.SystemGesture(RealTimeStylus sender, SystemGestureData data) { }
        void IStylusSyncPlugin.Error(RealTimeStylus sender, ErrorData data) { }
        void IStylusSyncPlugin.TabletAdded(RealTimeStylus sender, TabletAddedData data) { }
        void IStylusSyncPlugin.TabletRemoved(RealTimeStylus sender, TabletRemovedData data) { }
        void IStylusSyncPlugin.InAirPackets(RealTimeStylus sender, InAirPacketsData data) { }

        #endregion IStylusSyncPlugin Members
    }
}
