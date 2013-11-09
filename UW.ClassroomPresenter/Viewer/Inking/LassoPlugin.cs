// $Id: LassoPlugin.cs 2227 2013-07-22 23:23:44Z fred $

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

    public class LassoPlugin : IDisposable, IStylusSyncPlugin, InkSheetAdapter.IAdaptee {
        private readonly ControlEventQueue m_EventQueue;
        private readonly EventQueue.PropertyEventDispatcher m_DisplayBoundsChangedDispatcher;
        private readonly EventQueue.PropertyEventDispatcher m_DisplayInkTransformChangedDispatcher;

        private readonly Control m_Control;
        private readonly SlideDisplayModel m_Display;
        private readonly Hashtable m_SelectionPointsTable; // int<Stylus.Id> -> ArrayList<Point<HIMETRIC>>
        private readonly Hashtable m_SelectionBoundaryTable; // int<Stylus.Id> -> GraphicsPath
        private readonly Hashtable m_SelectionPreviousPointTable; // int<Stylus.Id> -> PointF
        private readonly Renderer m_Renderer;
        private Rectangle m_DisplayBounds;
        private LassoStylusModel m_Lasso;
        private InkSheetModel m_Sheet;
        private bool m_Disposed;
        private bool m_TouchSupported = true;

        private static Pen c_SelectionBoundaryPenInner;
        private static Pen c_SelectionBoundaryPenOuter;

        #region Construction & Destruction

        public LassoPlugin(Control control, SlideDisplayModel display) {
            this.m_EventQueue = display.EventQueue;
            this.m_Control = control;
            this.m_Display = display;
            this.m_SelectionPointsTable = new Hashtable();
            this.m_SelectionBoundaryTable = new Hashtable();
            this.m_SelectionPreviousPointTable = new Hashtable();
            this.m_Renderer = new Renderer();

            this.m_Control.Paint += new PaintEventHandler(this.HandleControlPaint);

            this.m_DisplayBoundsChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandleDisplayBoundsChanged));
            this.m_DisplayInkTransformChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandleDisplayInkTransformChanged));
            this.m_Display.Changed["Bounds"].Add(this.m_DisplayBoundsChangedDispatcher.Dispatcher);
            this.m_Display.Changed["InkTransform"].Add(this.m_DisplayInkTransformChangedDispatcher.Dispatcher);

            this.m_DisplayBoundsChangedDispatcher.Dispatcher(this.m_Display, null);
            this.m_DisplayInkTransformChangedDispatcher.Dispatcher(this.m_Display, null);
        }

        ~LassoPlugin() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            if(disposing) {
                this.m_Control.Paint -= new PaintEventHandler(this.HandleControlPaint);

                this.m_Display.Changed["Bounds"].Remove(this.m_DisplayBoundsChangedDispatcher.Dispatcher);
                this.m_Display.Changed["InkTransform"].Remove(this.m_DisplayInkTransformChangedDispatcher.Dispatcher);
            }
            this.m_Disposed = true;
        }

        #endregion Construction & Destruction


        #region InkSheetAdapter.IAdaptee Members

        public InkSheetModel InkSheetModel {
            get {
                using(Synchronizer.Lock(this)) {
                    return this.m_Sheet;
                }
            }

            set {
                using(Synchronizer.Lock(this)) {
                    if(this.m_Sheet != null) {
                        using(Synchronizer.Lock(this.m_Sheet.SyncRoot)) {
                            this.m_Sheet.Selection = null;
                        }
                    }

                    this.m_Sheet = value;

                    if(this.m_Sheet != null) {
                        using(Synchronizer.Lock(this.m_Sheet.SyncRoot)) {
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

        protected LassoStylusModel Lasso {
            get { return this.m_Lasso; }
            set {
                using(Synchronizer.Lock(this)) {
                    // Right now, LassoStylusModel has no data, so no need to (un)register any listeners.
                    this.m_Lasso = value;

                    // Remove any existing selections.
                    // TODO: What happens if the user is in the process of making a selection?  Can this happen?
                    if(this.InkSheetModel != null) {
                        using(Synchronizer.Lock(this.InkSheetModel.SyncRoot)) {
                            this.InkSheetModel.Selection = null;
                        }
                    }
                    this.m_SelectionPointsTable.Clear();
                    this.m_SelectionBoundaryTable.Clear();
                    this.m_SelectionPreviousPointTable.Clear();
                }
            }
        }

        public bool Enabled {
            get {
                using(Synchronizer.Lock(this)) {
                    return ((this.Lasso != null) && (this.InkSheetModel != null));
                }
            }
        }

        private void HandleDisplayBoundsChanged(object sender, PropertyEventArgs args) {
            using(Synchronizer.Lock(this)) {
                using(Synchronizer.Lock(this.m_Display.SyncRoot)) {
                    this.m_DisplayBounds = this.m_Display.Bounds;
                }
            }
        }

        private void HandleDisplayInkTransformChanged(object sender, PropertyEventArgs args) {
            using(Synchronizer.Lock(this)) {
                using(Synchronizer.Lock(this.m_Display.SyncRoot)) {
                    this.m_Renderer.SetViewTransform(this.m_Display.InkTransform);
                }
            }
        }

        private void HandleControlPaint(object sender, PaintEventArgs e) {
            this.PaintSelectionBoundary(e.Graphics);
        }

        private void PaintSelectionBoundary(Graphics g) {
            using(Synchronizer.Lock(this)) {
                // Paint all of the active boundaries (usually just zero or one).
                foreach(GraphicsPath boundary in this.m_SelectionBoundaryTable.Values) {
                    // Redraw the selection boundary.
                    this.DrawSelectionBoundary(g, boundary);
                }
            }
        }

        private void HandlePackets(StylusDataBase data) {
            using(Synchronizer.Lock(this)) {
                GraphicsPath boundary = ((GraphicsPath) this.m_SelectionBoundaryTable[data.Stylus.Id]);
                ArrayList collected = ((ArrayList) this.m_SelectionPointsTable[data.Stylus.Id]);
                Debug.Assert((boundary == null) == (collected == null));
                if(boundary == null) return;

                using(Graphics g = this.m_Display.CreateGraphics()) {
                    g.IntersectClip(this.m_DisplayBounds);

                    Debug.Assert(data.Count % data.PacketPropertyCount == 0);
                    Point[] points = new Point[data.Count / data.PacketPropertyCount];
                    for(int i = 0, j = 0; i < data.Count; i += data.PacketPropertyCount, j++) {
                        // Create a Point using the X and Y coordinates of the data, which are defined to be at offsets [i] and [i+1].
                        points[j] = new Point(data[i], data[i+1]);
                    }

                    // Add the points to the list of collected points, *before* they are converted to pixel coordinates.
                    collected.AddRange(points);

                    this.m_Renderer.InkSpaceToPixel(g, ref points);

                    PointF previous;
                    int start;
                    if(this.m_SelectionPreviousPointTable.ContainsKey(data.Stylus.Id)) {
                        previous = ((PointF) this.m_SelectionPreviousPointTable[data.Stylus.Id]);
                        start = 0;
                    } else {
                        m_SelectionPreviousPointTable[data.Stylus.Id] = previous =
                            (boundary.PointCount > 0) ? Point.Round(boundary.PathPoints[boundary.PointCount - 1])
                            : points.Length > 0 ? points[0] : Point.Empty;
                        start = (boundary.PointCount > 0) ? 0 : 1;
                    }

                    RectangleF bounds = new RectangleF(previous, SizeF.Empty);

                    for(int i = start; i < points.Length; i++) {
                        PointF point = points[i];
                        boundary.AddLine(previous, (previous = point));
                        ExpandRectangle(ref bounds, previous);
                    }

                    if(points.Length > 0)
                        this.m_SelectionPreviousPointTable[data.Stylus.Id] = previous;

                    int inflate = ((int) Math.Ceiling(SelectionBoundaryPenOuter.Width));
                    bounds.Inflate(inflate, inflate);
                    g.IntersectClip(bounds);
                    DrawSelectionBoundary(g, boundary);
                }
            }
        }

        private static Pen SelectionBoundaryPenInner {
            get {
                if(c_SelectionBoundaryPenInner == null) {
                    c_SelectionBoundaryPenInner = new Pen(SystemColors.Highlight, 4.0f);
                    c_SelectionBoundaryPenInner.DashStyle = DashStyle.DashDot;
                    c_SelectionBoundaryPenInner.DashCap = DashCap.Round;
                }

                return c_SelectionBoundaryPenInner;
            }
        }

        private static Pen SelectionBoundaryPenOuter {
            get {
                if(c_SelectionBoundaryPenOuter == null) {
                    c_SelectionBoundaryPenOuter = ((Pen) SelectionBoundaryPenInner.Clone());
                    Color inverse = c_SelectionBoundaryPenOuter.Color;
                    c_SelectionBoundaryPenOuter.Color = Color.FromArgb(inverse.A,
                        255 - inverse.R, 255 - inverse.G, 255 - inverse.B);
                    // Make the outer pen wider, but use a scale transform to keep the length of the dashes the same.
                    c_SelectionBoundaryPenOuter.ScaleTransform(1.5f, 1.5f);
                }

                return c_SelectionBoundaryPenOuter;
            }
        }

        private static void ExpandRectangle(ref RectangleF bounds, PointF previous) {
            if(bounds.X > previous.X) {
                bounds.Width += (bounds.X - previous.X);
                bounds.X = previous.X;
            } else if(bounds.Right < previous.X) {
                bounds.Width += (previous.X - bounds.Right);
            }

            if(bounds.Y > previous.Y) {
                bounds.Height += (bounds.Y - previous.Y);
                bounds.Y = previous.Y;
            } else if(bounds.Bottom < previous.Y) {
                bounds.Height += (previous.Y - bounds.Bottom);
            }
        }

        private void DrawSelectionBoundary(Graphics g, GraphicsPath boundary) {
            using(Synchronizer.Lock(this)) {
                g.DrawPath(SelectionBoundaryPenOuter, boundary);
                g.DrawPath(SelectionBoundaryPenInner, boundary);
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
            using(Synchronizer.Lock(this)) {
                if(!this.Enabled) return;

                // An inverted stylus is reserved for the eraser.
                if(data.Stylus.Inverted) return;

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

                // Replace the existing selection boundary.
                this.m_SelectionPointsTable[data.Stylus.Id] = new ArrayList();
                this.m_SelectionBoundaryTable[data.Stylus.Id] = new GraphicsPath();
                this.m_SelectionPreviousPointTable.Remove(data.Stylus.Id);

                this.HandlePackets(data);
            }
        }

        /// <summary>
        /// Occurs when the stylus moves on the digitizer surface.
        /// </summary>
        /// <param name="sender">The real time stylus associated with the notification</param>
        /// <param name="data">The notification data</param>
        void IStylusSyncPlugin.Packets(RealTimeStylus sender, PacketsData data) {
            this.HandlePackets(data);
        }

        /// <summary>
        /// Occurs when the stylus leaves the digitizer surface.
        /// </summary>
        /// <param name="sender">The real time stylus associated with the notification</param>
        /// <param name="data">The notification data</param>
        void IStylusSyncPlugin.StylusUp(RealTimeStylus sender, StylusUpData data) {
            using(Synchronizer.Lock(this)) {
                this.HandlePackets(data);

                GraphicsPath boundary = ((GraphicsPath) this.m_SelectionBoundaryTable[data.Stylus.Id]);
                ArrayList collected = ((ArrayList) this.m_SelectionPointsTable[data.Stylus.Id]);
                Debug.Assert((boundary == null) == (collected == null));
                if(boundary != null) {
                    using(Synchronizer.Lock(this)) {
                        if(this.InkSheetModel != null) {
                            Point[] lasso = ((Point[]) collected.ToArray(typeof(Point)));

                            using(Synchronizer.Lock(this.InkSheetModel.SyncRoot)) {
                                if (boundary.GetBounds() == RectangleF.Empty || lasso.Length <= 2) {
                                    // Either of the above cases can cause Ink.HitTest to throw an ArgumentException.
                                    this.InkSheetModel.Selection = null;
                                } else {
                                    using (Synchronizer.Lock(this.InkSheetModel.Ink.Strokes.SyncRoot)) {
                                        this.InkSheetModel.Selection = this.InkSheetModel.Ink.HitTest(lasso, 50f);
                                    }
                                }
                            }
                        }
                    }

                    this.m_SelectionPointsTable.Remove(data.Stylus.Id);
                    this.m_SelectionBoundaryTable.Remove(data.Stylus.Id);
                    this.m_Display.Invalidate(Rectangle.Round(boundary.GetBounds(new Matrix(), SelectionBoundaryPenOuter)));
                }
            }
        }

        /// <summary>
        /// Defines the types of notifications the plugin is interested in.
        /// </summary>
        DataInterestMask IStylusSyncPlugin.DataInterest {
            get {
                return DataInterestMask.StylusDown
                    | DataInterestMask.Packets
                    | DataInterestMask.StylusUp
                    | DataInterestMask.CustomStylusDataAdded;
            }
        }

        /// <summary>
        /// Occurs when custom data is inserted into the <see cref="RealTimeStylus"/> queue by other plugins.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="data"></param>
        void IStylusSyncPlugin.CustomStylusDataAdded(RealTimeStylus sender, CustomStylusData data) {
            if(data.CustomDataId == StylusInputSelector.StylusData.Id) {
                using(Synchronizer.Lock(this)) {
                    this.Lasso = (((StylusInputSelector.StylusData) data.Data).Stylus as LassoStylusModel);
                }
            }
        }

        // The remaining interface methods are not used in this application.
        void IStylusSyncPlugin.RealTimeStylusDisabled(RealTimeStylus sender, RealTimeStylusDisabledData data) {}
        void IStylusSyncPlugin.RealTimeStylusEnabled(RealTimeStylus sender, RealTimeStylusEnabledData data) {}
        void IStylusSyncPlugin.StylusOutOfRange(RealTimeStylus sender, StylusOutOfRangeData data) {}
        void IStylusSyncPlugin.StylusInRange(RealTimeStylus sender, StylusInRangeData data) {}
        void IStylusSyncPlugin.StylusButtonDown(RealTimeStylus sender, StylusButtonDownData data) {}
        void IStylusSyncPlugin.StylusButtonUp(RealTimeStylus sender, StylusButtonUpData data) {}
        void IStylusSyncPlugin.SystemGesture(RealTimeStylus sender, SystemGestureData data) {}
        void IStylusSyncPlugin.InAirPackets(RealTimeStylus sender, InAirPacketsData data) {}
        void IStylusSyncPlugin.TabletAdded(RealTimeStylus sender, TabletAddedData data) {}
        void IStylusSyncPlugin.TabletRemoved(RealTimeStylus sender, TabletRemovedData data) {}
        void IStylusSyncPlugin.Error(RealTimeStylus sender, ErrorData data) {}

        #endregion IStylusSyncPlugin Members
    }
}
