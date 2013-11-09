// $Id: TextSheetMessages.cs 1871 2009-05-26 19:29:56Z cmprince $

using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.Serialization;
using System.Threading;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;

namespace UW.ClassroomPresenter.Network.Messages.Presentation {
    [Serializable]
    public class TextSheetMessage : SheetMessage {
        public readonly string Text;
        public readonly Font font_;
        public readonly Color color_;
        public readonly bool is_public_;

        public TextSheetMessage(TextSheetModel sheet, SheetCollection collection) : base(sheet, collection) {
            using(Synchronizer.Lock(sheet.SyncRoot)) {
                this.Text = sheet.Text;
                this.font_ = sheet.Font;
                this.color_ = sheet.Color;
                this.is_public_ = sheet.IsPublic;

            }
        }

        protected override bool UpdateTarget(ReceiveContext context) {
            
            TextSheetModel sheet = this.Target as TextSheetModel;
            if (sheet == null) {
                ///if we sent it, this means that by definition the sheet is public
                ///edit: made is_editable true so that instructors can copy and paste student code
                
                this.Target = sheet = new TextSheetModel(((Guid)this.TargetId), this.Text, this.Disposition | SheetDisposition.Remote, true, true, this.Bounds, this.color_, this.font_);
            }

            using (Synchronizer.Lock(sheet.SyncRoot)) {
                sheet.Text = this.Text;
                sheet.Font = this.font_;
                sheet.IsEditable = false;
                ///if we sent it, this means that by definition the sheet is public
                sheet.IsPublic = true;
                sheet.Color = this.color_;
            }

            base.UpdateTarget(context);

            return true;
        }

        #region IGenericSerializable

        public override SerializedPacket Serialize() {
            SerializedPacket p = base.Serialize();
            p.Add( SerializedPacket.SerializeString( this.Text ) );
            p.Add( SerializedPacket.SerializeString( this.font_.Name ) );
            p.Add( SerializedPacket.SerializeColor( this.color_ ) );
            p.Add( SerializedPacket.SerializeBool( this.is_public_ ) );
            return p;
        }

        public TextSheetMessage( Message parent, SerializedPacket p ) : base( parent, p ) {
            this.Text = SerializedPacket.DeserializeString( p.GetNextPart() );
            this.font_ = new Font( SerializedPacket.DeserializeString( p.GetNextPart() ), 12.0f );
            this.color_ = SerializedPacket.DeserializeColor( p.GetNextPart() );
            this.is_public_ = SerializedPacket.DeserializeBool( p.GetNextPart() );
        }

        public override int GetClassId() {
            return PacketTypes.TextSheetMessageId;
        }

        #endregion
    }
}
