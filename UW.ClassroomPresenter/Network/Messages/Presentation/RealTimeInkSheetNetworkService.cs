// $Id: RealTimeInkSheetNetworkService.cs 1364 2007-05-15 18:48:22Z fred $

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;

using Microsoft.Ink;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Model.Viewer;

namespace UW.ClassroomPresenter.Network.Messages.Presentation {

    public class RealTimeInkSheetNetworkService : InkSheetNetworkService {
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
        /// The time delay for flushing the packet buffer, in 100 nanosecond units.
        /// </summary>
        // TODO: Make this a configurable/published property of the NetworkModel.
        protected const long PacketBufferFlushTime = 2000000; // 200 milliseconds

        private readonly EventQueue.PropertyEventDispatcher m_CurrentDrawingAttributesChangedDispatcher;

        private bool m_Disposed;
        private Guid m_SlideID;

        public RealTimeInkSheetNetworkService(SendingQueue sender, PresentationModel presentation, DeckModel deck, SlideModel slide, RealTimeInkSheetModel sheet, SheetMessage.SheetCollection selector) : base(sender, presentation, deck, slide, sheet, selector) {
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
            deck.InsertChild(slide = new SlideInformationMessage(this.Slide));
            slide.InsertChild( sheet = SheetMessage.RemoteForSheet( this.Sheet, this.SheetCollectionSelector ) );
            message.Tags = new MessageTags();
            message.Tags.SlideID = m_SlideID;
            this.Sender.Send(message);
        }

        private void HandleStylusDown(object sender, int stylusId, int strokeId, int[] packets, TabletPropertyDescriptionCollection tabletProperties) {
            this.Sender.Post(delegate() {
                this.HandleStylusDownHelper(sender, stylusId, strokeId, packets, tabletProperties);
            });
        }

        private void HandleStylusDownHelper(object sender, int stylusId, int strokeId, int[] packets, TabletPropertyDescriptionCollection tabletProperties) {
            this.FlushPackets(stylusId, strokeId);

            if (ViewerStateModel.NonStandardDpi) {
                RealTimeInkSheetModel.ScalePackets(packets, ViewerStateModel.DpiNormalizationSendMatrix);
            }

            Message message = new RealTimeInkSheetStylusDownMessage(this.m_Sheet, stylusId, strokeId, packets, tabletProperties);
            message.Tags = new MessageTags();
            message.Tags.SlideID = m_SlideID;
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

            if (ViewerStateModel.NonStandardDpi) {
                RealTimeInkSheetModel.ScalePackets(packets, ViewerStateModel.DpiNormalizationSendMatrix);
            }

            Message message = new RealTimeInkSheetStylusUpMessage(this.m_Sheet, stylusId, strokeId, packets);
            message.Tags = new MessageTags();
            message.Tags.SlideID = m_SlideID;
            message.Tags.Priority = MessagePriority.RealTime;
            message.Tags.BridgePriority = MessagePriority.RealTime;
            this.Sender.Send(message, MessagePriority.RealTime);
        }

        private void BufferPackets(int stylusId, int strokeId, int[] packets) {
            using(Synchronizer.Lock(this)) {
                List<int> buffer;
                if(!this.PacketBuffers.TryGetValue(stylusId, out buffer))
                    this.PacketBuffers.Add(stylusId, buffer = new List<int>());

                buffer.AddRange(packets);

                long flushed;
                if (this.PacketFlushTimes.TryGetValue(stylusId, out flushed)) {
                    long difference = DateTime.Now.Ticks - flushed;
                    if (difference >= PacketBufferFlushTime) {
                        this.FlushPackets(stylusId, strokeId);
                    }
                }
            }
        }

        private void FlushPackets(int stylusId, int strokeId) {
            using(Synchronizer.Lock(this)) {
                List<int> buffer;
                if(this.PacketBuffers.TryGetValue(stylusId, out buffer) && buffer.Count != 0) {
                    int[] packets = buffer.ToArray();

                    buffer.Clear();

                    if (ViewerStateModel.NonStandardDpi) {
                        RealTimeInkSheetModel.ScalePackets(packets, ViewerStateModel.DpiNormalizationSendMatrix);
                    }

                    // FIXME: Dispatch to the SendingQueue's thread.
                    Message message = new RealTimeInkSheetPacketsMessage(this.m_Sheet, stylusId, strokeId, packets);
                    message.Tags = new MessageTags();
                    message.Tags.SlideID = m_SlideID;
                    message.Tags.Priority = MessagePriority.RealTime;
                    message.Tags.BridgePriority = MessagePriority.RealTime;
                    this.Sender.Send(message, MessagePriority.RealTime);
                }

                this.PacketFlushTimes[stylusId] = DateTime.Now.Ticks;
            }
        }
    }
}
