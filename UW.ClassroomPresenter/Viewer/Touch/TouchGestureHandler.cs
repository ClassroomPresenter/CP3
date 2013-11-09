using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

using Microsoft.Ink;
using Microsoft.StylusInput;
using Microsoft.StylusInput.PluginData;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Model.Viewer;

namespace UW.ClassroomPresenter.Viewer.Touch {
    /// <summary>
    /// A class to interpret touch strokes and turn them into gestures
    /// </summary>
    public class TouchGestureHandler : IStylusSyncPlugin {
        #region Private Members

        private const int HIMETRIC_WINDOW_WIDTH = 9000;
        private const int HIMETRIC_WINDOW_HEIGHT = 5000;

        /// <summary>
        /// The presenter model
        /// </summary>
        private PresenterModel model;

        /// <summary>
        /// The current deck traversal
        /// </summary>
        private DeckTraversalModel currentDeckTraversal;

        /// <summary>
        /// The event queue
        /// </summary>
        private EventQueue eventQueue;

        /// <summary>
        /// Event handler
        /// </summary>
        private readonly IDisposable currentDeckTraversalDispatcher;

        /// <summary>
        /// The stroke packets
        /// </summary>
        private readonly Dictionary<int, List<int>> collectedPacketsTable;

        private readonly Dictionary<int, TabletPropertyDescriptionCollection> tabletPropertiesTable;

        private readonly Ink ink;

        #endregion

        #region Construction & Destruction

        /// <summary>
        /// Constructs a new <see cref="TouchGestureHandler"/> instance, which will
        /// keep track of the touch input and convert it into a gesture.
        /// </summary>
        public TouchGestureHandler(PresenterModel m, EventQueue q ) {
            this.model = m;
            this.currentDeckTraversal = null;

            // Listen for model changes
            this.eventQueue = q;
            this.currentDeckTraversalDispatcher =
                this.model.Workspace.CurrentDeckTraversal.ListenAndInitialize(this.eventQueue,
                    delegate(Property<DeckTraversalModel>.EventArgs args) {
                        this.currentDeckTraversal = args.New;
                    });

            // Keep track of the various touch strokes
            this.collectedPacketsTable = new Dictionary<int, List<int>>();
            this.tabletPropertiesTable = new Dictionary<int, TabletPropertyDescriptionCollection>();

            // Create a helper ink object
            this.ink = new Ink();
        }

        #endregion

        #region IStylusSyncPlugin Members

        /// <summary>
        /// Occurs when the stylus touches the digitizer surface.
        /// </summary>
        /// <param name="sender">The real time stylus associated with the notification</param>
        /// <param name="data">The notification data</param>
        void IStylusSyncPlugin.StylusDown(RealTimeStylus sender, StylusDownData data) {
            //Debug.WriteLine("StylusDown");
            Tablet currentTablet = sender.GetTabletFromTabletContextId(data.Stylus.TabletContextId);
            if (currentTablet.DeviceKind == TabletDeviceKind.Touch) {
                // The stylus id
                int stylusId = data.Stylus.Id;

                // Store the packets in the collected packets
                List<int> collected = new List<int>(data.GetData());
                this.collectedPacketsTable[stylusId] = collected;

                // Store the tablet properties
                this.tabletPropertiesTable[stylusId] = sender.GetTabletPropertyDescriptionCollection(data.Stylus.TabletContextId);
            }
        }

        /// <summary>
        /// Occurs when the stylus moves on the digitizer surface.
        /// </summary>
        /// <param name="sender">The real time stylus associated with the notification</param>
        /// <param name="data">The notification data</param>
        void IStylusSyncPlugin.Packets(RealTimeStylus sender, PacketsData data) {
            int stylusId = data.Stylus.Id;

            // Add the packets to the stroke
            if (this.collectedPacketsTable.ContainsKey(stylusId)) {
                List<int> collected = new List<int>(data.GetData());
                this.collectedPacketsTable[stylusId].AddRange(collected);
            }
        }

        /// <summary>
        /// Occurs when the stylus leaves the digitizer surface.
        /// </summary>
        /// <param name="sender">The real time stylus associated with the notification</param>
        /// <param name="data">The notification data</param>
        void IStylusSyncPlugin.StylusUp(RealTimeStylus sender, StylusUpData data) {
            int stylusId = data.Stylus.Id;
            
            // Handle the final stroke
            if (this.collectedPacketsTable.ContainsKey(stylusId)) {
                this.HandleStroke(stylusId);
                this.RemoveStroke(stylusId);
            }
        }

        /// <summary>
        /// Clean up all the internal data structures
        /// </summary>
        /// <param name="stylusId">The stylus id</param>
        private void RemoveStroke(int stylusId) {
            // Remove the entries from the hash tables when they are no longer needed.
            this.collectedPacketsTable.Remove(stylusId);
            this.tabletPropertiesTable.Remove(stylusId);
        }

        /// <summary>
        /// Used by <see cref="Packets"/> and <see cref="StylusUp"/> to render ink.
        /// </summary>
        private void HandleStroke(int stylusId) {
            // Also add the packets to the collected packets list.
            List<int> collected = this.collectedPacketsTable[stylusId];
            TabletPropertyDescriptionCollection tabletProperties = this.tabletPropertiesTable[stylusId];

            // Construct the final stroke
            Stroke finalStroke = this.ink.CreateStroke(collected.ToArray(), tabletProperties);

            // Now interpret the stroke
            Point[] pts = finalStroke.GetPoints();
            Point firstPoint = pts[0];
            Point lastPoint = pts[pts.Length - 1];
            Rectangle bounding = finalStroke.GetBoundingBox();

            // Ensure we sort of draw a horizontal shape
            if (bounding.Width > HIMETRIC_WINDOW_WIDTH && 
                bounding.Height < HIMETRIC_WINDOW_HEIGHT) {
                // Ensure that the first and last points are far enough apart
                if (Math.Abs(firstPoint.X - lastPoint.X) > HIMETRIC_WINDOW_WIDTH) {
                    // Go forward or back depending on the direction
                    if (firstPoint.X < lastPoint.X)
                        this.NextSlide();
                    else
                        this.PrevSlide();
                }
            }
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
                    | DataInterestMask.Error;
            }
        }

        // The remaining interface methods are not used in this application.
        void IStylusSyncPlugin.RealTimeStylusDisabled(RealTimeStylus sender, RealTimeStylusDisabledData data) { }
        void IStylusSyncPlugin.RealTimeStylusEnabled(RealTimeStylus sender, RealTimeStylusEnabledData data) { }
        void IStylusSyncPlugin.StylusOutOfRange(RealTimeStylus sender, StylusOutOfRangeData data) { }
        void IStylusSyncPlugin.StylusInRange(RealTimeStylus sender, StylusInRangeData data) { }
        void IStylusSyncPlugin.StylusButtonDown(RealTimeStylus sender, StylusButtonDownData data) { }
        void IStylusSyncPlugin.StylusButtonUp(RealTimeStylus sender, StylusButtonUpData data) { }
        void IStylusSyncPlugin.SystemGesture(RealTimeStylus sender, SystemGestureData data) { }
        void IStylusSyncPlugin.InAirPackets(RealTimeStylus sender, InAirPacketsData data) { }
        void IStylusSyncPlugin.TabletAdded(RealTimeStylus sender, TabletAddedData data) { }
        void IStylusSyncPlugin.TabletRemoved(RealTimeStylus sender, TabletRemovedData data) { }
        void IStylusSyncPlugin.CustomStylusDataAdded(RealTimeStylus sender, CustomStylusData data) { }

        #endregion

        #region Slide Navigation
    
        /// <summary>
        /// Advance to the next slide
        /// <remarks>
        /// Code taken from the next slide toolbar button
        /// </remarks>
        /// </summary>
        protected void NextSlide() {
            using( Synchronizer.Lock( this ) ) {
                if( this.currentDeckTraversal == null ) return;
                using( Synchronizer.Lock( this.currentDeckTraversal.SyncRoot ) ) {
                    if( this.currentDeckTraversal.Next != null )
                        // Advance the slide
                        this.currentDeckTraversal.Current = this.currentDeckTraversal.Next;
                    else {
                        // Add a whiteboard slide if we are at the end of the deck
                        using (Synchronizer.Lock(this.currentDeckTraversal.Deck.SyncRoot)) {
                            if (((this.currentDeckTraversal.Deck.Disposition & DeckDisposition.Whiteboard) != 0) && 
                                ((this.currentDeckTraversal.Deck.Disposition & DeckDisposition.Remote) == 0)) {
                                // Add the new slide
                                SlideModel slide = new SlideModel(Guid.NewGuid(), new LocalId(), SlideDisposition.Empty, UW.ClassroomPresenter.Viewer.ViewerForm.DEFAULT_SLIDE_BOUNDS);
                                this.currentDeckTraversal.Deck.InsertSlide(slide);
                                using (Synchronizer.Lock(this.currentDeckTraversal.Deck.TableOfContents.SyncRoot)) {
                                    // Add the new table of contents entry
                                    TableOfContentsModel.Entry entry = new TableOfContentsModel.Entry(Guid.NewGuid(), this.currentDeckTraversal.Deck.TableOfContents, slide);
                                    this.currentDeckTraversal.Deck.TableOfContents.Entries.Add(entry);

                                    // Move to the slide
                                    this.currentDeckTraversal.Current = entry;
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Advance to the previous slide
        /// <remarks>
        /// Code taken from the previous slide toolbar button
        /// </remarks>
        /// </summary>
        protected void PrevSlide() {
            if (this.currentDeckTraversal == null) return;
            using (Synchronizer.Lock(this.currentDeckTraversal.SyncRoot)) {
                if (this.currentDeckTraversal.Previous != null)
                    this.currentDeckTraversal.Current = this.currentDeckTraversal.Previous;
            }
        }
 
        #endregion
    }
}
