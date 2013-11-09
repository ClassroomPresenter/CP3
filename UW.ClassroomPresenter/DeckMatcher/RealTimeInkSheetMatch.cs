// $Id: RealTimeInkSheetMatch.cs 975 2006-06-28 01:02:59Z pediddle $

using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Threading;

using Microsoft.Ink;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;

namespace UW.ClassroomPresenter.DeckMatcher {
    /// <summary>
    /// Creates a matching between two real time ink sheets and marshals events between
    /// the two sheets
    /// </summary>
    public class RealTimeInkSheetMatch : InkSheetMatch {
        private bool m_Disposed;
        private readonly RealTimeInkSheetModel m_SourceSheet;
        private readonly RealTimeInkSheetModel m_DestSheet;

        private readonly EventQueue.PropertyEventDispatcher m_CurrentDrawingAttributesChangedDispatcher;

        /// <summary>
        /// Create the matching between two real time ink sheets
        /// </summary>
        /// <param name="sender">The event queue to invoke async events on</param>
        /// <param name="srcSheet">The source sheet to marshal packets/events from</param>
        /// <param name="dstSheet">The destination sheet to marshal packets/events to</param>
        public RealTimeInkSheetMatch(EventQueue sender, RealTimeInkSheetModel srcSheet, RealTimeInkSheetModel dstSheet ) : base(sender, srcSheet, dstSheet) {
            // Save the parameters
            this.m_SourceSheet = srcSheet;
            this.m_DestSheet = dstSheet;

            // Setup event handlers for the source sheet
            this.m_CurrentDrawingAttributesChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_Sender, new PropertyEventHandler(this.HandleCurrentDrawingAttributesChanged));
            this.m_SourceSheet.Changed["CurrentDrawingAttributes"].Add(this.m_CurrentDrawingAttributesChangedDispatcher.Dispatcher);
            this.m_SourceSheet.StylusUp += new RealTimeInkSheetModel.StylusUpEventHandler(this.HandleStylusUp);
            this.m_SourceSheet.Packets += new RealTimeInkSheetModel.PacketsEventHandler(this.HandlePackets);
            this.m_SourceSheet.StylusDown += new RealTimeInkSheetModel.StylusDownEventHandler(this.HandleStylusDown);

            // Setup the initial values for the Drawing Attributes
            this.m_Sender.Post(delegate() {
                this.m_CurrentDrawingAttributesChangedDispatcher.Dispatcher(this, null);
            });
        }

        /// <summary>
        /// Dispose of all the resources and event handlers held by this object
        /// </summary>
        /// <param name="disposing">True if we are in the process of disposing</param>
        protected override void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            try {
                if(disposing) {
                    this.m_SourceSheet.StylusUp -= new RealTimeInkSheetModel.StylusUpEventHandler(this.HandleStylusUp);
                    this.m_SourceSheet.Packets -= new RealTimeInkSheetModel.PacketsEventHandler(this.HandlePackets);
                    this.m_SourceSheet.StylusDown -= new RealTimeInkSheetModel.StylusDownEventHandler(this.HandleStylusDown);
                    this.m_SourceSheet.Changed["CurrentDrawingAttributes"].Remove(this.m_CurrentDrawingAttributesChangedDispatcher.Dispatcher);
                }
            }
            finally {
                base.Dispose(disposing);
            }
            this.m_Disposed = true;
        }

        /// <summary>
        /// Event handler that is invoked when the Drawing Attributes of the source sheet changes
        /// </summary>
        /// <param name="sender">The object that sent the message</param>
        /// <param name="args">The event arguments</param></param>
        private void HandleCurrentDrawingAttributesChanged(object sender, PropertyEventArgs args) {
            using( Synchronizer.Lock( this.m_SourceSheet.SyncRoot ) )
                using( Synchronizer.Lock( this.m_DestSheet.SyncRoot ) )
                    this.m_DestSheet.CurrentDrawingAttributes = this.m_SourceSheet.CurrentDrawingAttributes;
        }

        /// <summary>
        /// Handle the stylus being pressed and marshal the event to the other ink sheet
        /// </summary>
        /// <param name="sender">The object that sent the message</param>
        /// <param name="stylusId">The id of the stylus that was pressed down</param>
        /// <param name="packets">The packets sent by the stylus</param>
        /// <param name="tabletProperties">The properties of the tablet</param>
        private void HandleStylusDown(object sender, int stylusId, int strokeId, int[] packets, TabletPropertyDescriptionCollection tabletProperties) {
            this.m_Sender.Post(delegate() {
                this.HandleStylusDownHelper(sender, stylusId, strokeId, packets, tabletProperties);
            });
        }
        /// <summary>
        /// Helper function to marshal stylus down events
        /// </summary>
        /// <param name="sender">The object that sent the message</param>
        /// <param name="stylusId">The id of the stylus that was pressed down</param>
        /// <param name="packets">The packets sent by the stylus</param>
        /// <param name="tabletProperties">The properties of the tablet</param>
        private void HandleStylusDownHelper(object sender, int stylusId, int strokeId, int[] packets, TabletPropertyDescriptionCollection tabletProperties) {
            if (this.m_DestSheet != null)
                this.m_DestSheet.OnStylusDown(stylusId, strokeId, packets, tabletProperties);
        }

        /// <summary>
        /// Handle new packets being sent from the stylus, add these objects to the destination
        /// real-time ink sheet
        /// </summary>
        /// <param name="sender">The object that sent the message</param>
        /// <param name="stylusId">The id of the stylus that was pressed down</param>
        /// <param name="packets">The packets sent by the stylus</param>
        private void HandlePackets(object sender, int stylusId, int strokeId, int[] packets) {
            this.m_Sender.Post(delegate() {
                this.HandlePacketsHelper(sender, stylusId, strokeId, packets);
            });
        }
        /// <summary>
        /// Helper function to marshal stylus packets
        /// </summary>
        /// <param name="sender">The object that sent the message</param>
        /// <param name="stylusId">The id of the stylus that was pressed down</param>
        /// <param name="packets">The packets sent by the stylus</param>
        private void HandlePacketsHelper(object sender, int stylusId, int strokeId, int[] packets) {
            if(this.m_DestSheet != null)
                this.m_DestSheet.OnPackets(stylusId, strokeId, packets);
        }

        /// <summary>
        /// Handle the stylus being raised and marshal the event to the other ink sheet
        /// </summary>
        /// <param name="sender">The object that sent the event</param>
        /// <param name="stylusId">The id of the stylus that was raised</param>
        /// <param name="packets">The packets sent by the stylus</param>
        private void HandleStylusUp(object sender, int stylusId, int strokeId, int[] packets) {
            this.m_Sender.Post(delegate() {
                this.HandleStylusUpHelper(sender, stylusId, strokeId, packets);
            });
        }
        /// <summary>
        /// Helper function to marshal stylus up events
        /// </summary>
        /// <param name="sender">The object that sent the event</param>
        /// <param name="stylusId">The id of the stylus that was raised</param>
        /// <param name="packets">The packets sent by the stylus</param>
        private void HandleStylusUpHelper(object sender, int stylusId, int strokeId, int[] packets) {
            if( this.m_DestSheet != null)
                this.m_DestSheet.OnStylusUp(stylusId, strokeId, packets);
        }
    }
}
