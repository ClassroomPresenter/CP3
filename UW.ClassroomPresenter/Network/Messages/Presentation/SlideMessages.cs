// $Id: SlideMessages.cs 2165 2010-01-20 22:59:06Z fred $

using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.Serialization;
using System.Threading;

using UW.ClassroomPresenter.Model.Background;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Misc;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Model;


namespace UW.ClassroomPresenter.Network.Messages.Presentation {
    [Serializable]
    public abstract class SlideMessage : Message {
        public readonly LocalId LocalId;
        public readonly string Title;
        public readonly float Zoom;
        public Rectangle Bounds;
        public readonly Color SlideBackgroundColor;
        public readonly bool Visited;
        public readonly BackgroundTemplate SlideBackgroundTemplate;
        public readonly System.Guid SubmissionSlideGuid;
        public readonly SlideModel.StudentSubmissionStyle SubmissionStyle;
        public readonly SlideDisposition Disposition;

        /// <summary>
        /// If this is a Student Submission slide, the AssociationSlideId indicates the original slide.  This is only used
        /// for integration with CXP archiving.
        /// </summary>
        public readonly Guid AssociationSlideId;

        public SlideMessage(SlideModel slide) : base(slide.Id) {
            this.AddLocalRef( slide );
            this.LocalId = slide.LocalId;
            using(Synchronizer.Lock(slide.SyncRoot)) {
                this.Zoom = slide.Zoom;
                this.Bounds = slide.Bounds;
                this.Title = slide.Title;
                this.SlideBackgroundColor = slide.BackgroundColor;
                this.SlideBackgroundTemplate = slide.BackgroundTemplate;
                this.SubmissionSlideGuid = slide.SubmissionSlideGuid;
                this.Visited = slide.Visited;
                this.SubmissionStyle = slide.SubmissionStyle;
                this.Disposition = slide.Disposition;
                this.AssociationSlideId = slide.AssociationId;
                if (!this.AssociationSlideId.Equals(Guid.Empty)) {
                    AddAssociationExtension(slide);
                }
            }
        }

        public SlideMessage( SlideModel slide, bool localRef ) : base( slide.Id ) {
            if( localRef )
                this.AddLocalRef( slide );
            this.LocalId = slide.LocalId;
            using( Synchronizer.Lock( slide.SyncRoot ) ) {
                this.Zoom = slide.Zoom;
                this.Bounds = slide.Bounds;
                this.Title = slide.Title;
                this.SlideBackgroundColor = slide.BackgroundColor;
                this.SlideBackgroundTemplate = slide.BackgroundTemplate;
                this.SubmissionSlideGuid = slide.SubmissionSlideGuid;
                this.SubmissionStyle = slide.SubmissionStyle;
                this.Visited = slide.Visited;
                this.AssociationSlideId = slide.AssociationId; 
                if (!this.AssociationSlideId.Equals(Guid.Empty)) {
                    AddAssociationExtension(slide);
                }
            }
            
        }

        /// <summary>
        /// This should be used only if local node is instructor and if this slide is a student submission which is being
        /// sent to public displays.  In this case we want to add a few details about the slide and deck which are the basis
        /// for the student submission.  This will allow a presentation archive to be post-processed and reused much more easily.
        /// </summary>
        /// <param name="guid"></param>
        private void AddAssociationExtension(SlideModel slide) {
            SlideAssociationExtension ext = new SlideAssociationExtension(slide.AssociationId);
            ext.SlideIndex = slide.AssociationSlideIndex;
            ext.DeckID = slide.AssociationDeckId;
            ext.DeckType = slide.AssociationDeckDisposition;
            this.Extension = new ExtensionWrapper(ext, SlideAssociationExtension.ExtensionId);
        }

        protected override bool UpdateTarget(ReceiveContext context) {
            SlideModel slide = this.Target as SlideModel;
            if(slide == null) {
                this.Target = slide = new SlideModel(((Guid) this.TargetId), this.LocalId, this.Disposition | SlideDisposition.Remote, this.Bounds);
            }

            using(Synchronizer.Lock(slide.SyncRoot)) {
                slide.Title = this.Title;
                slide.Bounds = this.Bounds;
                slide.Zoom = this.Zoom;
                slide.BackgroundColor = this.SlideBackgroundColor;
                slide.BackgroundTemplate = this.SlideBackgroundTemplate;
                //Force the FilmStrip refresh, when slide.Visited change from false to true.
                if (slide.Visited == false && this.Visited == true) {
                    slide.Visited = this.Visited;
                    using (Synchronizer.Lock(PresenterModel.TheInstance.ViewerState.SyncRoot)) {
                        if (PresenterModel.TheInstance.ViewerState.StudentNavigationType == LinkedDeckTraversalModel.NavigationSelector.Visited && PresenterModel.TheInstance.ViewerState.FilmStripEnabled == true) {
                            PresenterModel.TheInstance.ViewerState.FilmStripEnabled = false;
                            PresenterModel.TheInstance.ViewerState.FilmStripEnabled = true;
                        }
                    }
                }
                slide.Visited = this.Visited;              
                slide.SubmissionSlideGuid = this.SubmissionSlideGuid;
                if( slide.SubmissionStyle != this.SubmissionStyle )
                    slide.SubmissionStyle = this.SubmissionStyle;
            }

            DeckModel deck = this.Parent != null ? this.Parent.Target as DeckModel : null;
            if(deck != null) {
                using(Synchronizer.Lock(deck.SyncRoot)) {
                    deck.InsertSlide(slide);
                }
            }

            //if the background template is not exist in BackgroundTemplate.xml, then save it to the xml file
            if (this.SlideBackgroundTemplate != null)
            {
                BackgroundTemplateXmlService xmlservice = new BackgroundTemplateXmlService();

                if (!xmlservice.IsTemplateExist(this.SlideBackgroundTemplate.Name))
                    xmlservice.SaveTemplate(this.SlideBackgroundTemplate);
            }
            return true;
        }

        #region IGenericSerializable

        public override SerializedPacket Serialize() {
            SerializedPacket p = base.Serialize();
            p.Add( (this.LocalId != null) ?
                this.LocalId.Serialize() : SerializedPacket.NullPacket( PacketTypes.LocalIdId ) );
            p.Add( SerializedPacket.SerializeString( this.Title ) );
            p.Add( SerializedPacket.SerializeFloat( this.Zoom ) );
            p.Add( SerializedPacket.SerializeRectangle( this.Bounds ) );
            p.Add( SerializedPacket.SerializeColor( this.SlideBackgroundColor ) );
            p.Add( SerializedPacket.SerializeGuid( this.SubmissionSlideGuid ) );
            p.Add( SerializedPacket.SerializeLong( (long)this.SubmissionStyle ) );
            p.Add( SerializedPacket.SerializeLong( (long)this.Disposition ) );
            p.Add( SerializedPacket.SerializeGuid( this.AssociationSlideId ) );
            return p;
        }

        public SlideMessage( Message parent, SerializedPacket p ) : base( parent, p ) {
            this.LocalId = (!SerializedPacket.IsNullPacket( p.PeekNextPart() )) ?
                new LocalId( p.PeekNextPart() ) : null; p.GetNextPart();
            this.Title = SerializedPacket.DeserializeString( p.GetNextPart() );
            this.Zoom = SerializedPacket.DeserializeFloat( p.GetNextPart() );
            this.Bounds = SerializedPacket.DeserializeRectangle( p.GetNextPart() );
            this.SlideBackgroundColor = SerializedPacket.DeserializeColor( p.GetNextPart() );
            this.SubmissionSlideGuid = SerializedPacket.DeserializeGuid( p.GetNextPart() );
            this.SubmissionStyle = (SlideModel.StudentSubmissionStyle)SerializedPacket.DeserializeLong( p.GetNextPart() );
            this.Disposition = (SlideDisposition)SerializedPacket.DeserializeLong( p.GetNextPart() );
            this.AssociationSlideId = SerializedPacket.DeserializeGuid( p.GetNextPart() );
        }

        public override int GetClassId() {
            return PacketTypes.SlideMessageId;
        }

        #endregion
    }

    [Serializable]
    public class SlideAssociationExtension {
        public static Guid ExtensionId = new Guid("{67E9610E-5889-4f09-8D05-37C8D4DB9DE0}");
        public SlideAssociationExtension(Guid slideId) {
            SlideID = slideId;
            DeckID = Guid.Empty;
            DeckType = DeckDisposition.Empty;
            SlideIndex = -1;
        }
        public int SlideIndex;
        public Guid DeckID;
        public DeckDisposition DeckType;
        public Guid SlideID;
    }

    [Serializable]
    public sealed class SlideInformationMessage : SlideMessage {
        public SlideInformationMessage(SlideModel slide) : base(slide) {}
        public SlideInformationMessage( SlideModel slide, bool localRef ) : base( slide, localRef ) { }

        protected override MergeAction MergeInto(Message other) {
            return (other is SlideInformationMessage) ? MergeAction.DiscardOther : base.MergeInto(other);
        }

        #region IGenericSerializable

        public SlideInformationMessage( Message parent, SerializedPacket p ) : base( parent, p ) {
        }

        public override int GetClassId() {
            return PacketTypes.SlideInformationMessageId;
        }

        #endregion
    }

    [Serializable]
    public sealed class SlideDeletedMessage : Message {
        public SlideDeletedMessage(SlideModel slide) : base(slide.Id) {}

        protected override bool UpdateTarget(ReceiveContext context) {
            // FIXME: Currently the DeckModel has no facility to remove unused slides.
            return false;
        }

        protected override MergeAction MergeInto(Message other) {
            return (other is SlideInformationMessage || other is SlideDeletedMessage) ? MergeAction.DiscardOther : base.MergeInto(other);
        }

        #region IGenericSerializable

        public SlideDeletedMessage( Message parent, SerializedPacket p ) : base( parent, p ) {
        }

        public override int GetClassId() {
            return PacketTypes.SlideDeletedMessageId;
        }

        #endregion
    }
}
