// $Id: SlideMessages.cs 765 2005-09-19 23:58:46Z shoat $

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.Serialization;
using System.Threading;

using UW.ClassroomPresenter.Model.Presentation;

namespace UW.ClassroomPresenter.Network.Messages.Presentation {
    /// <summary>
    /// Class representing a new student submission slide.
    /// </summary>
    [Serializable]
    public abstract class StudentSubmissionSlideMessage : SlideMessage {
        #region Public Members

        /// <summary>
        /// The guid to use for this slide, should be unique.
        /// </summary>
        public Guid SlideGuid;
        /// <summary>
        /// The guid to use for this entry in the TOC, should be unique.
        /// </summary>
        public Guid TOCEntryGuid;

        #endregion

        #region Construction

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="slide">The slide model associated with this slide</param>
        /// <param name="sGuid">The new guid for the student submissions</param>
        /// <param name="tGuid">The new TOC guid for this student submission</param>
        public StudentSubmissionSlideMessage(SlideModel slide, Guid sGuid, Guid tGuid)
            : base(slide) {
            this.SlideGuid = sGuid;
            this.TOCEntryGuid = tGuid;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Creates the student submissions deck and deck traversal if one doesn't already exist.
        /// Otherwise returns the existing student submission deck
        /// </summary>
        /// <returns>The student submissions deck for this presentation, if one does not 
        /// already exist this function creates one</returns>
        protected DeckModel GetPresentationStudentSubmissionsDeck() {
            // Sanity Checks to ensure that the message hierarchy is as we expect
            if (this.Parent.Parent != null &&
                this.Parent.Parent.Target != null &&
                this.Parent.Parent.Target is PresentationModel) {
                // Try to get an existing student submissions deck
                PresentationModel pres = this.Parent.Parent.Target as PresentationModel;
                DeckModel deck = pres.GetStudentSubmissionDeck();





                if (deck != null)
                    return deck;
                else {
                    // Create the student submissions deck
                    // TODO CMPRINCE: We currently hardcode values for the student submissions deck and
                    // deck traversal. Is there a better way to do this?
                    // Natalie - Apparently the reason that these are hardcoded has to do with the 
                    //public display of student submissions - when I switched these guids to normally 
                    //generated guids the public display lost the ability to display student submissions.
                    Guid ssGuid = new Guid("{78696D29-AA11-4c5b-BCF8-8E6406077FD4}");
                    Guid ssTraversalGuid = new Guid("{4884044B-DAE1-4249-AEF2-3A2304F52E97}");
                    deck = new DeckModel(ssGuid, DeckDisposition.StudentSubmission, "Student Submissions");
                    deck.Group = Groups.Group.Submissions;
                    deck.current_subs = true;
                    AddLocalRef(ssGuid, deck);
                    DeckTraversalModel traversal = new SlideDeckTraversalModel(ssTraversalGuid, deck);
                    AddLocalRef(ssTraversalGuid, traversal);

                    // Add the new student submission deck to the presentation
                    using (Synchronizer.Lock(pres.SyncRoot)) {
                        pres.DeckTraversals.Add(traversal);
                    }

                    return deck;
                }
            }
            return null;
        }

        /// <summary>
        /// Performs a deep copy of the given SheetModel.
        /// If the given sheetmodel is not an InkSheet, then returns itself.
        /// </summary>
        /// <param name="s">The SheetModel to copy</param>
        /// <returns>The given sheetmodel if not an InkSheetModel, otherwise a deep copy of the InkSheetModel</returns>
        protected SheetModel InkSheetDeepCopyHelper(SheetModel s) {
            using (Synchronizer.Lock(s.SyncRoot)) {
                InkSheetModel t;
                // Only copy InkSheetModels
                if (s is InkSheetModel) {
                    if (s is RealTimeInkSheetModel) {
                        // Make a deep copy of the SheetModel
                        t = new RealTimeInkSheetModel(Guid.NewGuid(), s.Disposition | SheetDisposition.Remote, s.Bounds, ((RealTimeInkSheetModel)s).Ink.Clone());
                        using (Synchronizer.Lock(t.SyncRoot)) {
                            ((RealTimeInkSheetModel)t).CurrentDrawingAttributes = ((RealTimeInkSheetModel)s).CurrentDrawingAttributes;
                        }
                    } else {
                        // Make a deep copy of the SheetModel
                        t = new InkSheetModel(Guid.NewGuid(), s.Disposition, s.Bounds, ((InkSheetModel)s).Ink.Clone());
                    }
                    // This is a new object so add it to the local references
                    AddLocalRef(t.Id, t);
                    return t;
                }
            }

            return s;
        }

        #endregion

        #region Receiving

        /// <summary>
        /// Handle the receipt of this message
        /// </summary>
        /// <param name="context">The context of the receiver from which the message was sent</param>
        protected override bool UpdateTarget(ReceiveContext context) {

#if DEBUG
            // Add logging of slide change events
            string machine_name = "";
            string machine_guid = "";
            using( Synchronizer.Lock( context.Participant.SyncRoot ) ) {
                machine_name = context.Participant.HumanName;
                machine_guid = context.Participant.Guid.ToString();
            }

            Debug.WriteLine( string.Format( "SUBMISSION RCVD ({0}): Machine -- {1}, GUID -- {2}", System.DateTime.Now.Ticks, machine_name, machine_guid ) );
#endif

            SlideModel slide = this.Target as SlideModel;
            // Only create student submissions from slides that exist on the client machine
            // TODO CMPRINCE: Should we create these anyway???
            if (slide != null) {
                // Get the student submissions deck
                DeckModel ssDeck = GetPresentationStudentSubmissionsDeck();
                if (ssDeck != null) {
                    // Check if this entry already exists
                    using (Synchronizer.Lock(ssDeck.TableOfContents.SyncRoot)) {
                        foreach (TableOfContentsModel.Entry ent in ssDeck.TableOfContents.Entries) {
                            if (ent.Id == this.TOCEntryGuid) {
                                this.Target = ent.Slide;
                                return false;
                            }
                        }
                    }

                    // Create the new slide to add
                    SlideModel newSlide = new SlideModel(this.SlideGuid, this.LocalId, SlideDisposition.Remote | SlideDisposition.StudentSubmission, this.Bounds, (Guid)this.TargetId);
                    this.Target = newSlide;

                    // Make a list of image content sheets that need to be added to the deck.
                    List<ImageSheetModel> images = new List<ImageSheetModel>();

                    // Update the fields of the slide
                    using (Synchronizer.Lock(newSlide.SyncRoot)) {
                        using (Synchronizer.Lock(slide.SyncRoot)) {
                            newSlide.Title = this.Title;
                            newSlide.Bounds = this.Bounds;
                            newSlide.Zoom = this.Zoom;
                            newSlide.BackgroundColor = this.SlideBackgroundColor;
                            newSlide.BackgroundTemplate = this.SlideBackgroundTemplate;
                            newSlide.SubmissionSlideGuid = this.SubmissionSlideGuid;
                            newSlide.SubmissionStyle = this.SubmissionStyle;

                            //If the slide background is null, then update the slide background with deck setting
                            if (this.SlideBackgroundColor == Color.Empty && this.Parent is DeckInformationMessage)
                                newSlide.BackgroundColor = ((DeckInformationMessage)this.Parent).DeckBackgroundColor;
                            if (this.SlideBackgroundTemplate == null && this.Parent is DeckInformationMessage)
                                newSlide.BackgroundTemplate = ((DeckInformationMessage)this.Parent).DeckBackgroundTemplate;

                            // Copy all of the content sheets.
                            // Because ContentSheets do not change, there is no 
                            // need to do a deep copy (special case for ImageSheetModels).
                            foreach (SheetModel s in slide.ContentSheets) {
                                newSlide.ContentSheets.Add(s);

                                // Queue up any image content to be added the deck below.
                                ImageSheetModel ism = s as ImageSheetModel;
                                if (ism != null)
                                    images.Add(ism);
                            }

                            // Make a deep copy of all the ink sheets
                            foreach (SheetModel s in slide.AnnotationSheets) {
                                SheetModel newSheet = UW.ClassroomPresenter.Model.Presentation.SheetModel.SheetDeepRemoteCopyHelper(s);
                                newSlide.AnnotationSheets.Add(newSheet);

                                // Queue up any image content to be added the deck below.
                                ImageSheetModel ism = s as ImageSheetModel;
                                if (ism != null)
                                    images.Add(ism);
                            }
                        }
                    }

                    using (Synchronizer.Lock(ssDeck.SyncRoot)) {
                        // Add the slide content to the deck.
                        foreach (ImageSheetModel ism in images) {
                            Image image = ism.Image;
                            if (image == null)
                                using (Synchronizer.Lock(ism.Deck.SyncRoot))
                                using (Synchronizer.Lock(ism.SyncRoot))
                                    image = ism.Deck.GetSlideContent(ism.MD5);
                            if (image != null)
                                ssDeck.AddSlideContent(ism.MD5, image);
                        }

                        // Add the slide to the deck.
                        ssDeck.InsertSlide(newSlide);
                        AddLocalRef(newSlide.Id, newSlide);
                    }

                    // Add an entry to the deck traversal so that we can navigate to the slide
                    using (Synchronizer.Lock(ssDeck.TableOfContents.SyncRoot)) {
                        TableOfContentsModel.Entry e = new TableOfContentsModel.Entry(this.TOCEntryGuid, ssDeck.TableOfContents, newSlide);
                        ssDeck.TableOfContents.Entries.Add(e);
                        AddLocalRef(e.Id, e);
                    }
                }
            }

            // Fix Bug 803: "Erased ink and text still shows up in student submissions".
            // We've updated our target with 'newSheet', which is NOT the real target!
            // Child messages should be able to access this target, but we don't want to
            // save the 'newSheet' in the targets table.  We want to keep the original sheet.
            return false;
        }

        #endregion

        #region IGenericSerializable

        public override SerializedPacket Serialize() {
            SerializedPacket p = base.Serialize();
            p.Add( SerializedPacket.SerializeGuid( this.SlideGuid ) );
            p.Add( SerializedPacket.SerializeGuid( this.TOCEntryGuid ) );
            return p;
        }

        public StudentSubmissionSlideMessage( Message parent, SerializedPacket p ) : base( parent, p ) {
            this.SlideGuid = SerializedPacket.DeserializeGuid( p.GetNextPart() );
            this.TOCEntryGuid = SerializedPacket.DeserializeGuid( p.GetNextPart() );
        }

        public override int GetClassId() {
            return PacketTypes.StudentSubmissionSlideMessageId;
        }

        #endregion
    }

    [Serializable]
    public sealed class StudentSubmissionSlideInformationMessage : StudentSubmissionSlideMessage {
        public StudentSubmissionSlideInformationMessage(SlideModel slide, Guid sGuid, Guid tGuid) : base(slide, sGuid, tGuid) { }

        protected override MergeAction MergeInto(Message other) {
            //Because of the way we reset the Target to a new SlideModel, we can't merge these messages.  Attempting to merge
            //them can sometimes cause multiple submissions to appear on one slide.
            return (other is StudentSubmissionSlideInformationMessage) ? MergeAction.KeepBothInOrder : base.MergeInto(other);
        }

        #region IGenericSerializable

        public StudentSubmissionSlideInformationMessage( Message parent, SerializedPacket p ) : base( parent, p ) {
        }

        public override int GetClassId() {
            return PacketTypes.StudentSubmissionSlideInformationMessageId;
        }

        #endregion
    }
}
