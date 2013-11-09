// $Id: SSRealTimeInkSheetNetworkService.cs 883 2006-02-14 04:57:19Z pediddle $

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;

using Microsoft.Ink;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;

namespace UW.ClassroomPresenter.Network.Messages.Presentation {
    /// <summary>
    /// Handles student submission messages
    /// </summary>
    public class SSRealTimeInkSheetNetworkService : SSInkSheetNetworkService {
        private readonly RealTimeInkSheetModel m_Sheet;

        /// <summary>
        /// Maps a stylus Id to an ArrayList of unsent packet data.
        /// </summary>
        private readonly Dictionary<int, List<int>> PacketBuffers; // int<StylusId> -> ArrayList<int<Packets>>

        /// <summary>
        /// Maps a stylus Id to the time that the <c>PacketBuffer</c> was last flushed.
        /// </summary>
        /// <remarks>
        /// <see cref="BufferPackets"/> invokes <see cref="FlushPackets"/> whenever the
        /// difference between the current time and the value in this table
        /// exceeds <see cref="PacketBufferFlushTime"/>.
        /// </remarks>
        /// <remarks>
        /// This table is updated by <see cref="FlushPackets"/>.
        /// </remarks>
        private readonly Dictionary<int,long> PacketFlushTimes; // int<StylusId> -> long<DateTime.Now.Ticks>

        /// <summary>
        /// The time delay for flushing the packet buffer, in 150 nanosecond units.
        /// </summary>
        // TODO: Make this a configurable/published property of the NetworkModel.
        protected const long PacketBufferFlushTime = 1500000;

        private readonly EventQueue.PropertyEventDispatcher m_CurrentDrawingAttributesChangedDispatcher;

        private bool m_Disposed;
        private Guid m_SlideID;

        public SSRealTimeInkSheetNetworkService(SendingQueue sender, PresentationModel presentation, DeckModel deck, SlideModel slide, RealTimeInkSheetModel sheet, SheetMessage.SheetCollection selector) : base(sender, presentation, deck, slide, sheet, selector) {
            this.m_Sheet = sheet;

            using (Synchronizer.Lock(slide.SyncRoot)) {
                m_SlideID = slide.Id;
            }

            this.PacketBuffers = new Dictionary<int, List<int>>();
            this.PacketFlushTimes = new Dictionary<int, long>();

            this.m_CurrentDrawingAttributesChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.Sender, new PropertyEventHandler(this.HandleCurrentDrawingAttributesChanged));
            this.m_Sheet.Changed["CurrentDrawingAttributes"].Add(this.m_CurrentDrawingAttributesChangedDispatcher.Dispatcher);
            this.m_Sheet.StylusUp += new RealTimeInkSheetModel.StylusUpEventHandler(this.HandleStylusUp);
            this.m_Sheet.Packets += new RealTimeInkSheetModel.PacketsEventHandler(this.HandlePackets);
            this.m_Sheet.StylusDown += new RealTimeInkSheetModel.StylusDownEventHandler(this.HandleStylusDown);
            this.m_CurrentDrawingAttributesChangedDispatcher.Dispatcher( this, null );
        }

        protected override void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            try {
                if(disposing) {
                    this.m_Sheet.StylusUp -= new RealTimeInkSheetModel.StylusUpEventHandler(this.HandleStylusUp);
                    this.m_Sheet.Packets -= new RealTimeInkSheetModel.PacketsEventHandler(this.HandlePackets);
                    this.m_Sheet.StylusDown -= new RealTimeInkSheetModel.StylusDownEventHandler(this.HandleStylusDown);
                    this.m_Sheet.Changed["CurrentDrawingAttributes"].Remove(this.m_CurrentDrawingAttributesChangedDispatcher.Dispatcher);
                }
            } finally {
                base.Dispose(disposing);
            }
            this.m_Disposed = true;
        }

        private void HandleCurrentDrawingAttributesChanged(object sender, PropertyEventArgs args) {
            Message message, deck, slide, sheet;
            message = new PresentationInformationMessage(this.Presentation);
            message.InsertChild(deck = new DeckInformationMessage(this.Deck));
            deck.InsertChild(slide = new SlideInformationMessage( GetSubmissionSlideModel(), false ));
            slide.InsertChild( sheet = SheetMessage.RemoteForSheet( this.Sheet, this.SheetCollectionSelector ) );
            this.Sender.Send(message);
        }

        private void HandleStylusDown(object sender, int stylusId, int strokeId, int[] packets, TabletPropertyDescriptionCollection tabletProperties) {
            this.Sender.Post(delegate() {
                this.HandleStylusDownHelper(sender, stylusId, strokeId, packets, tabletProperties);
            });
        }

        private void HandleStylusDownHelper(object sender, int stylusId, int strokeId, int[] packets, TabletPropertyDescriptionCollection tabletProperties) {
            this.FlushPackets(stylusId, strokeId);

            Message message = new RealTimeInkSheetStylusDownMessage(this.m_Sheet, stylusId, strokeId, packets, tabletProperties);
            message.Tags = new MessageTags();
            message.Tags.SlideID = this.m_SlideID;
            message.Tags.Priority = MessagePriority.RealTime;
            message.Tags.BridgePriority = MessagePriority.RealTime;
            this.Sender.Send(message, MessagePriority.RealTime);
        }

        private void HandlePackets(object sender, int stylusId, int strokeId, int[] packets) {
            this.Sender.Post(delegate() {
                this.HandlePacketsHelper(sender, stylusId, strokeId, packets);
            });
        }

        private void HandlePacketsHelper(object sender, int stylusId, int strokeId, int[] packets) {
            this.BufferPackets(stylusId, strokeId, packets);
        }

        private void HandleStylusUp(object sender, int stylusId, int strokeId, int[] packets) {
            this.Sender.Post(delegate() {
                this.HandleStylusUpHelper(sender, stylusId, strokeId, packets);
            });
        }

        private void HandleStylusUpHelper(object sender, int stylusId, int strokeId, int[] packets) {
            this.FlushPackets(stylusId, strokeId);

            Message message = new RealTimeInkSheetStylusUpMessage(this.m_Sheet, stylusId, strokeId, packets);
            message.Tags = new MessageTags();
            message.Tags.SlideID = this.m_SlideID;
            message.Tags.Priority = MessagePriority.RealTime;
            this.Sender.Send(message, MessagePriority.RealTime);
        }

        private void BufferPackets(int stylusId, int strokeId, int[] packets) {
            using(Synchronizer.Lock(this)) {
                List<int> buffer;
                if(!this.PacketBuffers.TryGetValue(stylusId, out buffer))
                    this.PacketBuffers.Add(stylusId, buffer = new List<int>());

                buffer.AddRange(packets);

                long flushed;
                if( this.PacketFlushTimes.TryGetValue( stylusId, out flushed ) ) {
                    long difference = DateTime.Now.Ticks - flushed;
                    if( difference >= PacketBufferFlushTime ) {
                        this.FlushPackets( stylusId, strokeId );
                    }
                } else {
                    this.PacketFlushTimes[stylusId] = DateTime.Now.Ticks;
                }
            }
        }

        private void FlushPackets(int stylusId, int strokeId) {
            using(Synchronizer.Lock(this)) {
                List<int> buffer;
                if(this.PacketBuffers.TryGetValue(stylusId, out buffer) && buffer.Count != 0) {
                    int[] packets = buffer.ToArray();

                    buffer.Clear();

                    // FIXME: Dispatch to the SendingQueue's thread.
                    Message message = new RealTimeInkSheetPacketsMessage(this.m_Sheet, stylusId, strokeId, packets);
                    message.Tags = new MessageTags();
                    message.Tags.SlideID = this.m_SlideID;
                    message.Tags.Priority = MessagePriority.RealTime;
                    message.Tags.BridgePriority = MessagePriority.RealTime;
                    this.Sender.Send(message, MessagePriority.RealTime);
                }

                this.PacketFlushTimes[stylusId] = DateTime.Now.Ticks;
            }
        }
    }
}
