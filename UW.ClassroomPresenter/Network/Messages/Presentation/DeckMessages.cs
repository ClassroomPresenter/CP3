// $Id: DeckMessages.cs 1871 2009-05-26 19:29:56Z cmprince $

using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.Serialization;
using System.Threading;

using UW.ClassroomPresenter.Misc;
using UW.ClassroomPresenter.Model.Background;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Network;

namespace UW.ClassroomPresenter.Network.Messages.Presentation {
    [Serializable]
    public abstract class DeckMessage : Message {
        public readonly string HumanName;
        public readonly DeckDisposition Disposition;
        public readonly Color DeckBackgroundColor;
        public readonly BackgroundTemplate DeckBackgroundTemplate;

        public DeckMessage(DeckModel deck) : base(deck.Id) {
            this.AddLocalRef( deck );
            using(Synchronizer.Lock(deck.SyncRoot)) {
                this.Group = deck.Group;
                this.HumanName = deck.HumanName;
                this.Disposition = deck.Disposition;
                this.DeckBackgroundColor = deck.DeckBackgroundColor;
                this.DeckBackgroundTemplate = deck.DeckBackgroundTemplate;
            }
        }

        protected override bool UpdateTarget(ReceiveContext context) {
            DeckModel deck = this.Target as DeckModel;
            if(deck == null) {
                this.Target = deck = new DeckModel(((Guid) this.TargetId), this.Disposition | DeckDisposition.Remote, this.HumanName);
            } else {
                using(Synchronizer.Lock(deck.SyncRoot)) {
                    deck.HumanName = this.HumanName;
                }
            }
            if ((this.Disposition & DeckDisposition.Remote) == 0) {
                using (Synchronizer.Lock(deck.SyncRoot)) {
                    deck.DeckBackgroundColor = this.DeckBackgroundColor;
                    deck.DeckBackgroundTemplate = this.DeckBackgroundTemplate;
                }
                //if the background template is not exist in BackgroundTemplate.xml, then save it to the xml file
                if (this.DeckBackgroundTemplate != null) {
                    BackgroundTemplateXmlService xmlservice = new BackgroundTemplateXmlService();
                    if (!xmlservice.IsTemplateExist(this.DeckBackgroundTemplate.Name))
                        xmlservice.SaveTemplate(this.DeckBackgroundTemplate);
                }
            }
            return true;
        }

        #region IGenericSerializable

        public override SerializedPacket Serialize() {
            SerializedPacket p = base.Serialize();
            p.Add( SerializedPacket.SerializeString( this.HumanName ) );
            p.Add( SerializedPacket.SerializeLong( (long)this.Disposition ) );
            p.Add( SerializedPacket.SerializeColor( this.DeckBackgroundColor ) );
            return p;
        }

        public DeckMessage( Message parent, SerializedPacket p ) : base( parent, p ) {
            this.HumanName = SerializedPacket.DeserializeString( p.GetNextPart() );
            this.Disposition = (DeckDisposition)SerializedPacket.DeserializeLong( p.GetNextPart() );
            this.DeckBackgroundColor = SerializedPacket.DeserializeColor( p.GetNextPart() );
        }

        public override int GetClassId() {
            return PacketTypes.DeckMessageId;
        }

        #endregion
    }

    [Serializable]
    public sealed class DeckInformationMessage : DeckMessage {
        public DeckInformationMessage(DeckModel deck) : base(deck) {}

        protected override MergeAction MergeInto(Message other) {
            return (other is DeckInformationMessage) ? MergeAction.DiscardOther : base.MergeInto(other);
        }

        #region IGenericSerializable

        public DeckInformationMessage( Message parent, SerializedPacket p ) : base( parent, p ) {
        }

        public override int GetClassId() {
            return PacketTypes.DeckInformationMessageId;
        }

        #endregion
    }
}
