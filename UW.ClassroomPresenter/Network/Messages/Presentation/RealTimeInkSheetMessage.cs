// $Id: RealTimeInkSheetMessage.cs 1871 2009-05-26 19:29:56Z cmprince $

using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.Serialization;
using System.Threading;

using Microsoft.Ink;
using Microsoft.StylusInput;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Model.Viewer;

namespace UW.ClassroomPresenter.Network.Messages.Presentation {
    [Serializable]
    public abstract class RealTimeInkSheetMessage : SheetMessage {
        public DrawingAttributesSerializer CurrentDrawingAttributes;

        public RealTimeInkSheetMessage(RealTimeInkSheetModel sheet, SheetMessage.SheetCollection collection) : base(sheet, collection) {
            using(Synchronizer.Lock(sheet.SyncRoot)) {
                this.CurrentDrawingAttributes = sheet.CurrentDrawingAttributes == null ? null
                    : new DrawingAttributesSerializer(sheet.CurrentDrawingAttributes);
            }
        }

        protected override bool UpdateTarget(ReceiveContext context) {
            RealTimeInkSheetModel sheet = this.Target as RealTimeInkSheetModel;
            if(sheet == null) {
                this.Target = sheet = new RealTimeInkSheetModel(((Guid) this.TargetId), this.Disposition | SheetDisposition.Remote, this.Bounds);

                using(Synchronizer.Lock(sheet.SyncRoot)) {
                    if(this.CurrentDrawingAttributes != null)
                        sheet.CurrentDrawingAttributes = this.CurrentDrawingAttributes.CreateDrawingAttributes();
                }
            } else {
                using(Synchronizer.Lock(sheet.SyncRoot)) {
                    if(this.CurrentDrawingAttributes != null) {
                        DrawingAttributes changed = sheet.CurrentDrawingAttributes;
                        if(changed == null || this.CurrentDrawingAttributes.NeedsUpdate(changed)) {
                            changed = this.CurrentDrawingAttributes.CreateDrawingAttributes();
                            sheet.CurrentDrawingAttributes = changed;
                        }
                    } else {
                        sheet.CurrentDrawingAttributes = null;
                    }
                }
            }

            base.UpdateTarget(context);

            return true;
        }

        [Serializable]
        public class DrawingAttributesSerializer : IGenericSerializable {
            public readonly bool AntiAliased;
            public readonly Color Color;
            public readonly ArrayList ExtendedProperties;
            public readonly bool FitToCurve;
            public readonly float Height;
            public readonly bool IgnorePressure;
            public readonly PenTip PenTip;
            public readonly RasterOperation RasterOperation;
            public readonly byte Transparency;
            public readonly float Width;

            public DrawingAttributesSerializer(DrawingAttributes atts) {
                this.AntiAliased = atts.AntiAliased;
                this.Color = atts.Color;
                this.ExtendedProperties = new ArrayList(atts.ExtendedProperties);
                this.FitToCurve = atts.FitToCurve;
                this.Height = atts.Height;
                this.IgnorePressure = atts.IgnorePressure;
                this.PenTip = atts.PenTip;
                this.RasterOperation = atts.RasterOperation;
                this.Transparency = atts.Transparency;
                this.Width = atts.Width;
            }

            public virtual DrawingAttributes CreateDrawingAttributes() {
                DrawingAttributes atts = new DrawingAttributes();
                this.UpdateDrawingAttributes(atts);
                return atts;
            }

            public virtual void UpdateDrawingAttributes(DrawingAttributes atts) {
                atts.AntiAliased = this.AntiAliased;
                atts.Color = this.Color;
                foreach(ExtendedProperty prop in this.ExtendedProperties)
                    atts.ExtendedProperties.Add(prop.Id, prop.Data);
                atts.FitToCurve = this.FitToCurve;
                atts.Height = this.Height;
                atts.IgnorePressure = this.IgnorePressure;
                atts.PenTip = this.PenTip;
                atts.RasterOperation = this.RasterOperation;
                atts.Transparency = this.Transparency;
                atts.Width = this.Width;
            }

            internal bool NeedsUpdate(DrawingAttributes d) {
                if(d.AntiAliased != this.AntiAliased) return true;
                if(!d.Color.Equals(this.Color)) return true;
                foreach(ExtendedProperty prop in this.ExtendedProperties)
                    if(!d.ExtendedProperties.DoesPropertyExist(prop.Id) || d.ExtendedProperties[prop.Id].Data.Equals(prop.Data)) return true;
                // FIXME: Verify that there are no extended properties in d that don't exist in this.ExtendedProperties (tough to do efficiently).
                if(d.FitToCurve != this.FitToCurve) return true;
                if(d.Height != this.Height) return true;
                if(d.IgnorePressure != this.IgnorePressure) return true;
                if(d.PenTip != this.PenTip) return true;
                if(d.RasterOperation != this.RasterOperation) return true;
                if(d.Transparency != this.Transparency) return true;
                if(d.Width != this.Width) return true;
                return false;
            }

            #region IGenericSerializable

            public SerializedPacket Serialize() {
                SerializedPacket p = new SerializedPacket( this.GetClassId() );
                p.Add( SerializedPacket.SerializeBool( this.AntiAliased ) );
                p.Add( SerializedPacket.SerializeColor( this.Color ) );
                // readonly ArrayList ExtendedProperties
                p.Add( SerializedPacket.SerializeBool( this.FitToCurve ) );
                p.Add( SerializedPacket.SerializeFloat( this.Height ) );
                p.Add( SerializedPacket.SerializeBool( this.IgnorePressure ) );
                p.Add( SerializedPacket.SerializeLong( (long)this.PenTip ) );
                p.Add( SerializedPacket.SerializeLong( (long)this.RasterOperation ) );
                p.Add( SerializedPacket.SerializeByte( this.Transparency ) );
                p.Add( SerializedPacket.SerializeFloat( this.Width ) );
                return p;
            }

            public DrawingAttributesSerializer( SerializedPacket p ) {
                this.AntiAliased = SerializedPacket.DeserializeBool( p.GetNextPart() );
                this.Color = SerializedPacket.DeserializeColor( p.GetNextPart() );
                this.ExtendedProperties = new ArrayList();
                this.FitToCurve = SerializedPacket.DeserializeBool( p.GetNextPart() );
                this.Height = SerializedPacket.DeserializeFloat( p.GetNextPart() );
                this.IgnorePressure = SerializedPacket.DeserializeBool( p.GetNextPart() );
                this.PenTip = (PenTip)SerializedPacket.DeserializeLong( p.GetNextPart() );
                this.RasterOperation = (RasterOperation)SerializedPacket.DeserializeLong( p.GetNextPart() );
                this.Transparency = SerializedPacket.DeserializeByte( p.GetNextPart() );
                this.Width = SerializedPacket.DeserializeFloat( p.GetNextPart() );
            }

            public int GetClassId() {
                return PacketTypes.DrawingAttributesSerializerId;
            }

            #endregion
        }

        #region IGenericSerializable

        public override SerializedPacket Serialize() {
            SerializedPacket p = base.Serialize();
            p.Add( (this.CurrentDrawingAttributes != null) ?
                this.CurrentDrawingAttributes.Serialize() : SerializedPacket.NullPacket( PacketTypes.DrawingAttributesSerializerId ) );
            return p;
        }

        public RealTimeInkSheetMessage( Message parent, SerializedPacket p ) : base( parent, p ) {
            this.CurrentDrawingAttributes = (!SerializedPacket.IsNullPacket( p.PeekNextPart() )) ?
                new DrawingAttributesSerializer( p.PeekNextPart() ) : null; p.GetNextPart();
        }

        public override int GetClassId() {
            return PacketTypes.RealTimeInkSheetMessageId;
        }

        #endregion
    }

    [Serializable]
    public sealed class RealTimeInkSheetInformationMessage : RealTimeInkSheetMessage {
        public RealTimeInkSheetInformationMessage(RealTimeInkSheetModel sheet, SheetMessage.SheetCollection collection) : base(sheet, collection) {}

        protected override MergeAction MergeInto(Message other) {
            return (other is RealTimeInkSheetInformationMessage) ? MergeAction.DiscardOther : base.MergeInto(other);
        }

        #region IGenericSerializable

        public RealTimeInkSheetInformationMessage( Message parent, SerializedPacket p ) : base( parent, p ) {
        }

        public override int GetClassId() {
            return PacketTypes.RealTimeInkSheetInformationMessageId;
        }

        #endregion
    }

    [Serializable]
    public abstract class RealTimeInkSheetDataMessage : Message {
        public int StylusId;
        public int StrokeId;
        public int[] Packets;

        public RealTimeInkSheetDataMessage(RealTimeInkSheetModel sheet, int stylusId, int strokeId, int[] packets) : base(sheet.Id) {
            this.StylusId = stylusId;
            this.StrokeId = strokeId;
            this.Packets = packets;
        }

        protected static int[] ConcatenatePackets(int[] older, int[] newer) {
            int[] packets = new int[newer.Length + older.Length];
            older.CopyTo(packets, 0);
            newer.CopyTo(packets, older.Length);
            return packets;
        }

        public override void ZipInto(ref Message other) {
            // These three types of messages are normally parents of an
            // InkSheetStrokes(Added|Deleting)Message.  Override the default ZipInto algorithm
            // to always insert the RealTimeInkSheetDataMessage as a child of these messages
            // so it can potentially be merged with a strokes messages.
            if(this.Parent == null && (other is PresentationInformationMessage || other is DeckInformationMessage || other is SlideInformationMessage)) {
                // Recursively zip into the other message as if this message were a child.
                // If we ever encounter an InkSheetStrokes(Added|Deleting)Message,
                // this message will be discarded.
                this.ZipInto(ref other.Child);
            } else {
                // Otherwise it will be inserted chronologically as a predecessor.
                base.ZipInto(ref other);
            }
        }

        protected override MergeAction MergeInto(Message other) {
            if(other is InkSheetStrokesAddedMessage || other is InkSheetStrokesDeletingMessage) {
                // If added/deleted strokes have not yet been processed, then it's pointless
                // to process the RealTimeInkSheetDataMessage because it will just get
                // painted over anyway.
                return MergeAction.DiscardThis;
            } else {
                return base.MergeInto(other);
            }
        }

        #region IGenericSerializable

        public override SerializedPacket Serialize() {
            SerializedPacket p = base.Serialize();
            p.Add( SerializedPacket.SerializeInt( this.StylusId ) );
            p.Add( SerializedPacket.SerializeInt( this.StrokeId ) );
            p.Add( SerializedPacket.SerializeIntArray( this.Packets ) );
            return p;
        }

        public RealTimeInkSheetDataMessage( Message parent, SerializedPacket p ) : base( parent, p ) {
            this.StylusId = SerializedPacket.DeserializeInt( p.GetNextPart() );
            this.StrokeId = SerializedPacket.DeserializeInt( p.GetNextPart() );
            this.Packets = SerializedPacket.DeserializeIntArray( p.GetNextPart() );
        }

        public override int GetClassId() {
            return PacketTypes.RealTimeInkSheetDataMessageId;
        }

        #endregion
    }

    [Serializable]
    public sealed class RealTimeInkSheetPacketsMessage : RealTimeInkSheetDataMessage {
        public RealTimeInkSheetPacketsMessage(RealTimeInkSheetModel sheet, int stylusId, int strokeId, int[] packets) : base(sheet, stylusId, strokeId, packets) {}

        protected override bool UpdateTarget(ReceiveContext context) {

            RealTimeInkSheetModel sheet = this.Target as RealTimeInkSheetModel;
            if (sheet != null) {
                if (ViewerStateModel.NonStandardDpi) {
                    RealTimeInkSheetModel.ScalePackets(this.Packets, ViewerStateModel.DpiNormalizationReceiveMatrix);
                }
                sheet.OnPackets(this.StylusId, this.StrokeId, this.Packets);
            }
            return false;
        }


        protected override MergeAction MergeInto(Message other_) {
            if(other_ is RealTimeInkSheetPacketsMessage || other_ is RealTimeInkSheetStylusDownMessage) {
                RealTimeInkSheetDataMessage other = ((RealTimeInkSheetDataMessage) other_);
                if (this.StylusId == other.StylusId && this.StrokeId == other.StrokeId) {
                    other.Packets = ConcatenatePackets(other.Packets, this.Packets);
                    return MergeAction.DiscardThis;
                }
            }

            return base.MergeInto(other_);
        }

        #region IGenericSerializable

        public RealTimeInkSheetPacketsMessage( Message parent, SerializedPacket p ) : base( parent, p ) {
        }

        public override int GetClassId() {
            return PacketTypes.RealTimeInkSheetPacketsMessageId;
        }

        #endregion
    }

    [Serializable]
    public sealed class RealTimeInkSheetStylusUpMessage : RealTimeInkSheetDataMessage {
        public RealTimeInkSheetStylusUpMessage(RealTimeInkSheetModel sheet, int stylusId, int strokeId, int[] packets) : base(sheet, stylusId, strokeId, packets) { }

        protected override bool UpdateTarget(ReceiveContext context) {
            RealTimeInkSheetModel sheet = this.Target as RealTimeInkSheetModel;
            if (sheet != null) {
                if (ViewerStateModel.NonStandardDpi) {
                    RealTimeInkSheetModel.ScalePackets(this.Packets, ViewerStateModel.DpiNormalizationReceiveMatrix);
                }
                sheet.OnStylusUp(this.StylusId, this.StrokeId, this.Packets);
            }
            return false;
        }

        protected override MergeAction MergeInto(Message other_) {
            if(other_ is RealTimeInkSheetPacketsMessage) {
                RealTimeInkSheetPacketsMessage other = ((RealTimeInkSheetPacketsMessage) other_);
                if (this.StylusId == other.StylusId && this.StrokeId == other.StrokeId) {
                    this.Packets = ConcatenatePackets(other.Packets, this.Packets);
                    return MergeAction.DiscardOther;
                }
            }

            return base.MergeInto(other_);
        }

        #region IGenericSerializable

        public RealTimeInkSheetStylusUpMessage( Message parent, SerializedPacket p ) : base( parent, p ) {
        }

        public override int GetClassId() {
            return PacketTypes.RealTimeInkSheetStylusUpMessageId;
        }

        #endregion
    }

    [Serializable]
    public sealed class RealTimeInkSheetStylusDownMessage : RealTimeInkSheetDataMessage {
        public readonly TabletPropertyDescriptionCollectionInformation TabletProperties;

        public RealTimeInkSheetStylusDownMessage(RealTimeInkSheetModel sheet, int stylusId, int strokeId, int[] packets, TabletPropertyDescriptionCollection tabletProperties) : base(sheet, stylusId, strokeId, packets) {
            this.TabletProperties = new TabletPropertyDescriptionCollectionInformation(tabletProperties);
        }

        protected override bool UpdateTarget(ReceiveContext context) {
            RealTimeInkSheetModel sheet = this.Target as RealTimeInkSheetModel;
            if (sheet != null) {
                if (ViewerStateModel.NonStandardDpi) {
                    RealTimeInkSheetModel.ScalePackets(this.Packets, ViewerStateModel.DpiNormalizationReceiveMatrix);
                }

                sheet.OnStylusDown(this.StylusId, this.StrokeId, this.Packets, this.TabletProperties.CreateTabletPropertyDescriptionCollection());
            }
            return false;
        }

        protected override MergeAction MergeInto(Message other) {
            // A RealTimeInkSheetStylusDownMessage cannot be merged with any
            // other types of RealTimeInkSheetDataMessages.
            return base.MergeInto(other);
        }

        [Serializable]
        public class TabletPropertyDescriptionCollectionInformation : IGenericSerializable {
            protected readonly float InkToDeviceScaleX, InkToDeviceScaleY;
            protected readonly ArrayList TabletPropertyDescriptions;

            public TabletPropertyDescriptionCollectionInformation(TabletPropertyDescriptionCollection tp) {
                this.InkToDeviceScaleX = tp.InkToDeviceScaleX;
                this.InkToDeviceScaleY = tp.InkToDeviceScaleY;

                this.TabletPropertyDescriptions = new ArrayList(tp.Count);
                foreach(TabletPropertyDescription d in tp)
                    this.TabletPropertyDescriptions.Add(new TabletPropertyDescriptionInformation(d));
            }

            public TabletPropertyDescriptionCollection CreateTabletPropertyDescriptionCollection() {
                TabletPropertyDescriptionCollection tp = new TabletPropertyDescriptionCollection(this.InkToDeviceScaleX, this.InkToDeviceScaleY);
                foreach(TabletPropertyDescriptionInformation info in this.TabletPropertyDescriptions)
                    tp.Add(info.CreateTabletPropertyDescription());
                return tp;
            }

            [Serializable]
            protected struct TabletPropertyDescriptionInformation : IGenericSerializable {
                public readonly Guid PacketPropertyId;
                public readonly int TabletPropertyMetrics_Maximum, TabletPropertyMetrics_Minimum;
                public readonly float TabletPropertyMetrics_Resolution;
                public readonly TabletPropertyMetricUnit TabletPropertyMetrics_Units;

                public TabletPropertyDescriptionInformation(TabletPropertyDescription d) {
                    this.PacketPropertyId = d.PacketPropertyId;
                    this.TabletPropertyMetrics_Maximum = d.TabletPropertyMetrics.Maximum;
                    this.TabletPropertyMetrics_Minimum = d.TabletPropertyMetrics.Minimum;
                    this.TabletPropertyMetrics_Resolution = d.TabletPropertyMetrics.Resolution;
                    this.TabletPropertyMetrics_Units = d.TabletPropertyMetrics.Units;
                }

                public TabletPropertyDescription CreateTabletPropertyDescription() {
                    TabletPropertyMetrics metrics = new TabletPropertyMetrics();
                    metrics.Maximum = this.TabletPropertyMetrics_Maximum;
                    metrics.Minimum = this.TabletPropertyMetrics_Minimum;
                    metrics.Resolution = this.TabletPropertyMetrics_Resolution;
                    metrics.Units = this.TabletPropertyMetrics_Units;

                    return new TabletPropertyDescription(this.PacketPropertyId, metrics);
                }

                #region IGenericSerializable

                public SerializedPacket Serialize() {
                    SerializedPacket p = new SerializedPacket( this.GetClassId() );
                    p.Add( SerializedPacket.SerializeGuid( this.PacketPropertyId ) );
                    p.Add( SerializedPacket.SerializeInt( this.TabletPropertyMetrics_Maximum ) );
                    p.Add( SerializedPacket.SerializeInt( this.TabletPropertyMetrics_Minimum ) );
                    p.Add( SerializedPacket.SerializeFloat( this.TabletPropertyMetrics_Resolution ) );
                    p.Add( SerializedPacket.SerializeLong( (long)this.TabletPropertyMetrics_Units ) );
                    return p;
                }

                public TabletPropertyDescriptionInformation( SerializedPacket p ) {
                    this.PacketPropertyId = Guid.Empty;
                    this.TabletPropertyMetrics_Maximum = 0;
                    this.TabletPropertyMetrics_Minimum = 0;
                    this.TabletPropertyMetrics_Resolution = 0.0f;
                    this.TabletPropertyMetrics_Units = TabletPropertyMetricUnit.Default;
                    SerializedPacket.VerifyPacket( p, this.GetClassId() );
                    this.PacketPropertyId = SerializedPacket.DeserializeGuid( p.GetNextPart() );
                    this.TabletPropertyMetrics_Maximum = SerializedPacket.DeserializeInt( p.GetNextPart() );
                    this.TabletPropertyMetrics_Minimum = SerializedPacket.DeserializeInt( p.GetNextPart() );
                    this.TabletPropertyMetrics_Resolution = SerializedPacket.DeserializeFloat( p.GetNextPart() );
                    this.TabletPropertyMetrics_Units = (TabletPropertyMetricUnit)SerializedPacket.DeserializeLong( p.GetNextPart() );
                }

                public int GetClassId() {
                    return PacketTypes.TabletPropertyDescriptionInformationId;
                }

                #endregion
            }

            #region IGenericSerializable

            public SerializedPacket Serialize() {
                SerializedPacket p = new SerializedPacket( this.GetClassId() );
                p.Add( SerializedPacket.SerializeFloat( this.InkToDeviceScaleX ) );
                p.Add( SerializedPacket.SerializeFloat( this.InkToDeviceScaleY ) );
                p.Add( SerializedPacket.SerializeInt( this.TabletPropertyDescriptions.Count ) );
                foreach( TabletPropertyDescriptionInformation d in this.TabletPropertyDescriptions ) {
                    p.Add( d.Serialize() );
                }
                return p;
            }

            public TabletPropertyDescriptionCollectionInformation( SerializedPacket p ) {
                SerializedPacket.VerifyPacket( p, this.GetClassId() );
                this.InkToDeviceScaleX = SerializedPacket.DeserializeFloat( p.GetNextPart() );
                this.InkToDeviceScaleY = SerializedPacket.DeserializeFloat( p.GetNextPart() );
                int cnt = SerializedPacket.DeserializeInt( p.GetNextPart() );
                this.TabletPropertyDescriptions = new ArrayList(cnt);
                for( int i = 0; i < cnt; i++ ) {
                    this.TabletPropertyDescriptions.Add( new TabletPropertyDescriptionInformation( p.GetNextPart() ) );
                }
            }

            public int GetClassId() {
                return PacketTypes.TabletPropertyDescriptionCollectionInformationId;
            }

            #endregion
        }

        #region IGenericSerializable

        public override SerializedPacket Serialize() {
            SerializedPacket p =  base.Serialize();
            p.Add( (this.TabletProperties != null) ?
                this.TabletProperties.Serialize() : SerializedPacket.NullPacket( PacketTypes.TabletPropertyDescriptionCollectionInformationId ) );
            return p;
        }

        public RealTimeInkSheetStylusDownMessage( Message parent, SerializedPacket p ) : base( parent, p ) {
            this.TabletProperties = (!SerializedPacket.IsNullPacket( p.PeekNextPart() )) ?
                new TabletPropertyDescriptionCollectionInformation( p.PeekNextPart() ) : null; p.GetNextPart();
        }

        public override int GetClassId() {
            return PacketTypes.RealTimeInkSheetStylusDownMessageId;
        }

        #endregion
    }
}
