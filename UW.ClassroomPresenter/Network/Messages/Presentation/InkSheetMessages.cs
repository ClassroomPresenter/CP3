// $Id: InkSheetMessages.cs 1871 2009-05-26 19:29:56Z cmprince $

using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.Serialization;
using System.Threading;

using Microsoft.Ink;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Model.Viewer;

namespace UW.ClassroomPresenter.Network.Messages.Presentation {
    [Serializable]
    public abstract class InkSheetMessage : SheetMessage {
        /// <summary>
        /// Identifies the <see cref="ExtendedProperty"/> which identifies strokes after they are sent across the network.
        /// </summary>
        /// <remarks>
        /// This is a fixed <see cref="Guid"/>, since all clients must agree on the value in order to communicate.
        /// <em>DO NOT CHANGE THIS VALUE, OR YOU WILL BREAK INTEROPERABILITY BETWEEN VERSIONS OF CLASSROOM PRESENTER.</em>
        /// </remarks>
        public static readonly Guid StrokeIdExtendedProperty = new Guid("fa8aebeb-55f3-4f98-a743-a3a6e0e6dad9");

        public InkSheetMessage(InkSheetModel sheet, SheetCollection selector) : base(sheet, selector) {}

        #region IGenericSerializable

        public InkSheetMessage( Message parent, SerializedPacket p ) : base( parent, p ) {
        }

        public override int GetClassId() {
            return PacketTypes.InkSheetMessageId;
        }

        #endregion
    }

    [Serializable]
    public sealed class InkSheetInformationMessage : InkSheetMessage {
        public InkSheetInformationMessage(InkSheetModel sheet, SheetCollection selector) : base(sheet, selector) {}

        protected override MergeAction MergeInto(Message other) {
            return (other is InkSheetInformationMessage) ? MergeAction.DiscardOther : base.MergeInto(other);
        }

        #region IGenericSerializable

        public InkSheetInformationMessage( Message parent, SerializedPacket p ) : base( parent, p ) {
        }

        public override int GetClassId() {
            return PacketTypes.InkSheetInformationMessageId;
        }

        #endregion
    }

    [Serializable]
    public sealed class InkSheetStrokesAddedMessage : InkSheetMessage {
        public byte[][] SavedInks;

        //This is redundant in some cases, but is needed, or at least it is very convenient in some late-joining archiver scenarios:
        public Guid SlideId;

        public InkSheetStrokesAddedMessage(InkSheetModel sheet, Guid slideId, SheetCollection selector, Ink ink) : base(sheet, selector) {
            this.SavedInks = new byte[][] { ink.Save() };
            SlideId = slideId;
        }

        protected override bool UpdateTarget(ReceiveContext context) {

            bool update = base.UpdateTarget(context);

            InkSheetModel sheet = this.Target as InkSheetModel;
            if(sheet == null)
                return update;

            using(Synchronizer.Lock(sheet.Ink.Strokes.SyncRoot)) {
                int[] newIds;
                if(this.SavedInks.Length == 1) {
                    this.LoadInkIntoTarget(sheet, this.SavedInks[0], out newIds);
                } else {
                    ArrayList ids = new ArrayList();
                    foreach(byte[] saved in this.SavedInks) {
                        this.LoadInkIntoTarget(sheet, saved, out newIds);
                        ids.AddRange(newIds);
                    }
                    newIds = ((int[]) ids.ToArray(typeof(int)));
                }

                // Notify the renderers that a new set of strokes have been created.
                sheet.OnInkAdded(new StrokesEventArgs(newIds));
            }

            return update;
        }

        private void LoadInkIntoTarget(InkSheetModel sheet, byte[] saved, out int[] ids) {
            Ink restored = new Ink();
            restored.Load(saved);
            

            using(Synchronizer.Lock(sheet.Ink.Strokes.SyncRoot)) {
                ids = new int[restored.Strokes.Count];

                for(int i = 0; i < ids.Length; i++) {
                    Stroke stroke = restored.Strokes[i];

                    // Remove any strokes that have the same remote Id as the new one.
                    // Unfortunately, because the InkSheetUndoService cannot preserve stroke referential identity,
                    // we must do a full search each time and cannot simply keep a table.
                    if(stroke.ExtendedProperties.DoesPropertyExist(StrokeIdExtendedProperty)) {
                        object id = stroke.ExtendedProperties[StrokeIdExtendedProperty].Data;
                        foreach(Stroke existing in sheet.Ink.Strokes) {
                            if(existing.ExtendedProperties.DoesPropertyExist(StrokeIdExtendedProperty)) {
                                if(id.Equals(existing.ExtendedProperties[StrokeIdExtendedProperty].Data)) {
                                    StrokesEventArgs args = new StrokesEventArgs(new int[] { existing.Id });
                                    sheet.OnInkDeleting(args);
                                    sheet.Ink.DeleteStroke(existing);
                                    sheet.OnInkDeleted(args);
                                }
                            }
                        }
                    }

                    // The stroke has no association with the current Ink object.
                    // Therefore, we have to recreate it by copying the raw packet data.
                    // This first requires recreating the TabletPropertyDescriptionCollection,
                    // which, for some stupid reason, must be done manually for lack of a better API.
                    TabletPropertyDescriptionCollection properties = new TabletPropertyDescriptionCollection();
                    foreach(Guid property in stroke.PacketDescription)
                        properties.Add(new TabletPropertyDescription(property, stroke.GetPacketDescriptionPropertyMetrics(property)));

                    // Create a new stroke from the raw packet data.
                    Stroke created = sheet.Ink.CreateStroke(stroke.GetPacketData(), properties);

                    // Copy the DrawingAttributes and all application data
                    // (especially the StrokesIdExtendedProperty) to the new stroke.
                    created.DrawingAttributes = stroke.DrawingAttributes;
                    foreach(ExtendedProperty prop in stroke.ExtendedProperties)
                        created.ExtendedProperties.Add(prop.Id, prop.Data);

                    ids[i] = created.Id;

                    if (ViewerStateModel.NonStandardDpi)
                        created.Transform(ViewerStateModel.DpiNormalizationReceiveMatrix);

                }
            }
        }

        protected override MergeAction MergeInto(Message other_) {
            if(other_ is InkSheetStrokesAddedMessage) {
                InkSheetStrokesAddedMessage other = ((InkSheetStrokesAddedMessage) other_);
                byte[][] merged = new byte[other.SavedInks.Length + this.SavedInks.Length][];
                other.SavedInks.CopyTo(merged, 0);
                this.SavedInks.CopyTo(merged, other.SavedInks.Length);
                other.SavedInks = merged;
                return MergeAction.DiscardThis;
            }

            return base.MergeInto(other_);
        }

        #region IGenericSerializable

        public override SerializedPacket Serialize() {
            SerializedPacket p = base.Serialize();
            p.Add( SerializedPacket.SerializeInt( this.SavedInks.Length ) );
            for( int i = 0; i < this.SavedInks.Length; i++ ) {
                p.Add( SerializedPacket.SerializeByteArray( this.SavedInks[i] ) );
            }
            return p;
        }

        public InkSheetStrokesAddedMessage( Message parent, SerializedPacket p ) : base( parent, p ) {
            this.SavedInks = new byte[SerializedPacket.DeserializeInt( p.GetNextPart() )][];
            for( int i = 0; i < this.SavedInks.Length; i++ ) {
                this.SavedInks[i] = SerializedPacket.DeserializeByteArray( p.GetNextPart() );
            }
        }

        public override int GetClassId() {
            return PacketTypes.InkSheetStrokesAddedMessageId;
        }

        #endregion
    }

    [Serializable]
    public sealed class InkSheetStrokesDeletingMessage : InkSheetMessage {
        public string[] StrokeIds;

        public InkSheetStrokesDeletingMessage(InkSheetModel sheet, SheetCollection selector, string[] strokeIds) : base(sheet, selector) {
            this.StrokeIds = ((string[]) strokeIds.Clone());
        }

        protected override bool UpdateTarget(ReceiveContext context) {
            bool update = base.UpdateTarget(context);

            InkSheetModel sheet = this.Target as InkSheetModel;
            if(sheet == null)
                return update;

            ArrayList deleting = new ArrayList(this.StrokeIds.Length);

            foreach(Stroke existing in sheet.Ink.Strokes) {
                if(existing.ExtendedProperties.DoesPropertyExist(StrokeIdExtendedProperty)) {
                    if(Array.IndexOf(this.StrokeIds, existing.ExtendedProperties[StrokeIdExtendedProperty].Data) >= 0) {
                        deleting.Add(existing.Id);
                    }
                }
            }

            int[] ids = ((int[]) deleting.ToArray(typeof(int)));
            using(Strokes strokes = sheet.Ink.CreateStrokes(ids)) {
                StrokesEventArgs args = new StrokesEventArgs(ids);
                sheet.OnInkDeleting(args);
                sheet.Ink.DeleteStrokes(strokes);
                sheet.OnInkDeleted(args);
            }

            return update;
        }

        protected override MergeAction MergeInto(Message other_) {
            if(other_ is InkSheetStrokesDeletingMessage) {
                InkSheetStrokesDeletingMessage other = ((InkSheetStrokesDeletingMessage) other_);
                string[] merged = new string[this.StrokeIds.Length + other.StrokeIds.Length];
                other.StrokeIds.CopyTo(merged, 0);
                this.StrokeIds.CopyTo(merged, other.StrokeIds.Length);
                this.StrokeIds = merged;
                return MergeAction.DiscardOther;
            } else {
                return base.MergeInto(other_);
            }
        }

        #region IGenericSerializable

        public override SerializedPacket Serialize() {
            SerializedPacket p = base.Serialize();
            p.Add( SerializedPacket.SerializeInt( this.StrokeIds.Length ) );
            for( int i = 0; i < this.StrokeIds.Length; i++ ) {
                p.Add( SerializedPacket.SerializeString( this.StrokeIds[i] ) );
            }
            return p;
        }

        public InkSheetStrokesDeletingMessage( Message parent, SerializedPacket p ) : base( parent, p ) {
            this.StrokeIds = new string[SerializedPacket.DeserializeInt( p.GetNextPart() )];
            for( int i = 0; i < this.StrokeIds.Length; i++ ) {
                this.StrokeIds[i] = SerializedPacket.DeserializeString( p.GetNextPart() );
            }
        }

        public override int GetClassId() {
            return PacketTypes.InkSheetStrokesDeletingMessageId;
        }

        #endregion
    }
}
