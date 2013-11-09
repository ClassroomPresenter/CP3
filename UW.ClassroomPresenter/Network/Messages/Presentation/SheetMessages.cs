// $Id: SheetMessages.cs 1871 2009-05-26 19:29:56Z cmprince $

using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.Serialization;
using System.Threading;

using Microsoft.Ink;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;

namespace UW.ClassroomPresenter.Network.Messages.Presentation {
    [Serializable]
    public abstract class SheetMessage : Message {
        public readonly SheetCollection SheetCollectionSelector;
        public readonly SheetDisposition Disposition;
        public Rectangle Bounds;
        protected int Height;

        public SheetMessage(SheetModel sheet, SheetCollection collection) : base(sheet.Id) {
            this.AddLocalRef( sheet );
            this.Disposition = sheet.Disposition;
            this.SheetCollectionSelector = collection;
            using(Synchronizer.Lock(sheet.SyncRoot)) {
                this.Bounds = sheet.Bounds;
            }
            this.Height = sheet.Height;
        }

        public static Message ForSheet(SheetModel sheet, SheetCollection collection) {
            if (sheet is ImageSheetModel) {
                //If it's an instructor note, return null.
                if (sheet.Disposition == SheetDisposition.Instructor)
                    return null;
                else
                    return new ImageSheetMessage((ImageSheetModel)sheet, collection);
            }
            if(sheet is RealTimeInkSheetModel)
                return new RealTimeInkSheetInformationMessage((RealTimeInkSheetModel) sheet, collection);
            if(sheet is InkSheetModel)
                return new InkSheetInformationMessage((InkSheetModel) sheet, collection);
            if(sheet is TextSheetModel)
                return new TextSheetMessage((TextSheetModel) sheet, collection);
            if( sheet is QuickPollSheetModel ) {
                Message poll;
                using( Synchronizer.Lock( sheet.SyncRoot ) ) {
                    poll = new QuickPollInformationMessage( ((QuickPollSheetModel)sheet).QuickPoll );
                }
                poll.InsertChild( new QuickPollSheetMessage( (QuickPollSheetModel)sheet, collection ) );
                return poll;
            }
            if (sheet is XPSPageSheetModel)
                return new XPSPageSheetMessage((XPSPageSheetModel)sheet, collection);
            throw new ArgumentException("Unknown sheet type: " + sheet.GetType().ToString());
        }

        public static Message RemoteForSheet( SheetModel sheet, SheetCollection collection) {
            SheetModel newModel = null;
            if( sheet is ImageSheetModel )
                newModel = sheet;
            else if( sheet is RealTimeInkSheetModel ) {
                using (Synchronizer.Lock(sheet.SyncRoot)) {
                    newModel = new RealTimeInkSheetModel(sheet.Id, sheet.Disposition | SheetDisposition.Remote, sheet.Bounds);
                    using (Synchronizer.Lock(newModel.SyncRoot))
                        ((RealTimeInkSheetModel)newModel).CurrentDrawingAttributes = ((RealTimeInkSheetModel)sheet).CurrentDrawingAttributes;
                }
            }
            else if ( sheet is InkSheetModel )
                newModel = sheet;
            else if ( sheet is TextSheetModel )
                newModel = sheet;
            else if ( sheet is QuickPollSheetModel )
                newModel = sheet;
            else if ( sheet is XPSPageSheetModel )
                newModel = sheet;
            return SheetMessage.ForSheet( newModel, collection );
        }

        protected override bool UpdateTarget(ReceiveContext context) {
            SheetModel sheet = this.Target as SheetModel;
            if(sheet == null)
                return false;

            using(Synchronizer.Lock(sheet)) {
                sheet.Bounds = this.Bounds;
            }

            // Find a parent SlideModel message
            SlideModel slide = null;
            Message parent = this.Parent;
            while( parent != null && slide == null ) {
                if( parent.Target is SlideModel ) {
                    slide = parent.Target as SlideModel;
                } else {
                    parent = parent.Parent;
                }
            }
            if(slide != null) {
                SlideModel.SheetCollection destination;
                switch(this.SheetCollectionSelector) {
                    case SheetCollection.ContentSheets: destination = slide.ContentSheets; break;
                    case SheetCollection.AnnotationSheets: destination = slide.AnnotationSheets; break;
                    default: throw new ArgumentException("Unknown enumeration value.", "collection");
                }

                using(Synchronizer.Lock(slide.SyncRoot)) {
                    if(!destination.Contains(sheet)) {
                        // TODO: Insert the sheet at the correct index.
                        destination.Add(sheet);
                    }
                }
            }

            return true;
        }

        [Serializable]
        public enum SheetCollection {
            ContentSheets = 0,
            AnnotationSheets = 1,
        }

        #region IGenericSerializable

        public override SerializedPacket Serialize() {
            SerializedPacket p = base.Serialize();
            p.Add( SerializedPacket.SerializeLong( (long)this.SheetCollectionSelector ) );
            p.Add( SerializedPacket.SerializeLong( (long)this.Disposition ) );
            p.Add( SerializedPacket.SerializeRectangle( this.Bounds ) );
            p.Add( SerializedPacket.SerializeInt( this.Height ) );
            return p;
        }

        public SheetMessage( Message parent, SerializedPacket p ) : base( parent, p ) {
            this.SheetCollectionSelector = (SheetCollection)SerializedPacket.DeserializeLong( p.GetNextPart() );
            this.Disposition = (SheetDisposition)SerializedPacket.DeserializeLong( p.GetNextPart() );
            this.Bounds = SerializedPacket.DeserializeRectangle( p.GetNextPart() );
            this.Height = SerializedPacket.DeserializeInt( p.GetNextPart() );
        }

        public override int GetClassId() {
            return PacketTypes.SheetMessageId;
        }

        #endregion
    }

    [Serializable]
    public sealed class SheetRemovedMessage : SheetMessage {
        public SheetRemovedMessage(SheetModel sheet, SheetCollection collection) : base(sheet, collection) {}

        protected override bool UpdateTarget(ReceiveContext context) {
            SheetModel sheet = this.Target as SheetModel;
            if(sheet == null)
                return false;

            SlideModel slide = this.Parent != null ? this.Parent.Target as SlideModel : null;
            if(slide == null)
                return false;

            using(Synchronizer.Lock(slide.SyncRoot)) {
                // Find the collection to which the sheet belongs.
                SlideModel.SheetCollection collection;
                switch(this.SheetCollectionSelector) {
                    case SheetCollection.ContentSheets: collection = slide.ContentSheets; break;
                    case SheetCollection.AnnotationSheets: collection = slide.AnnotationSheets; break;
                    default: throw new ArgumentException("Unknown enumeration value.", "SheetCollectionSelector");
                }

                // Remove it.
                if(collection.Contains(sheet)){
                    collection.Remove(sheet);
                }
            }

            return false;
        }

        protected override MergeAction MergeInto(Message other) {
            return (other is SheetMessage || other is SheetRemovedMessage) ? MergeAction.DiscardOther : base.MergeInto(other);
        }

        #region IGenericSerializable

        public SheetRemovedMessage( Message parent, SerializedPacket p ) : base( parent, p ) {
        }

        public override int GetClassId() {
            return PacketTypes.SheetRemovedMessageId;
        }

        #endregion
    }
}
