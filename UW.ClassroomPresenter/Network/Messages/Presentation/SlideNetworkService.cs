// $Id: SlideNetworkService.cs 1766 2008-09-14 16:54:14Z lining $

using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Network.Groups;

namespace UW.ClassroomPresenter.Network.Messages.Presentation {
    public class SlideNetworkService : INetworkService, IDisposable {
        private readonly SendingQueue m_Sender;
        private readonly EventQueue.PropertyEventDispatcher m_ChangeDispatcher;
        private readonly EventQueue.PropertyEventDispatcher m_StyleChangeDispatcher;
        private readonly PresentationModel m_Presentation;
        private readonly DeckModel m_Deck;
        private readonly SlideModel m_Slide;

        private readonly SheetsCollectionHelper m_ContentSheetsCollectionHelper;
        private readonly SheetsCollectionHelper m_AnnotationSheetsCollectionHelper;

        private bool m_Disposed;

        public SlideNetworkService(SendingQueue sender, PresentationModel presentation, DeckModel deck, SlideModel slide) {
            this.m_Sender = sender;
            this.m_Presentation = presentation;
            this.m_Deck = deck;
            this.m_Slide = slide;

            this.m_ChangeDispatcher = new EventQueue.PropertyEventDispatcher(this.m_Sender, new PropertyEventHandler(this.HandleChange));
            this.m_StyleChangeDispatcher = new EventQueue.PropertyEventDispatcher( this.m_Sender, new PropertyEventHandler( this.HandleSubmissionStyleChange ) );
            this.m_Slide.Changed["Bounds"].Add( this.m_ChangeDispatcher.Dispatcher );
            this.m_Slide.Changed["Zoom"].Add(this.m_ChangeDispatcher.Dispatcher);
            this.m_Slide.Changed["BackgroundColor"].Add(this.m_ChangeDispatcher.Dispatcher);
            this.m_Slide.Changed["BackgroundTemplate"].Add(this.m_ChangeDispatcher.Dispatcher);
            this.m_Slide.Changed["SubmissionStyle"].Add( this.m_StyleChangeDispatcher.Dispatcher );

            this.m_ContentSheetsCollectionHelper = new SheetsCollectionHelper(this, "ContentSheets", SheetMessage.SheetCollection.ContentSheets);
            this.m_AnnotationSheetsCollectionHelper = new SheetsCollectionHelper(this, "AnnotationSheets", SheetMessage.SheetCollection.AnnotationSheets);
        }

        #region IDisposable Members

        ~SlideNetworkService() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            if(disposing) {
                this.m_Slide.Changed["Bounds"].Remove(this.m_ChangeDispatcher.Dispatcher);
                this.m_Slide.Changed["Zoom"].Remove(this.m_ChangeDispatcher.Dispatcher);
                this.m_Slide.Changed["BackgroundColor"].Remove(this.m_ChangeDispatcher.Dispatcher);
                this.m_Slide.Changed["BackgroundTemplate"].Remove(this.m_ChangeDispatcher.Dispatcher);
                this.m_Slide.Changed["SubmissionStyle"].Remove( this.m_StyleChangeDispatcher.Dispatcher );

                this.m_ContentSheetsCollectionHelper.Dispose();
                this.m_AnnotationSheetsCollectionHelper.Dispose();
            }
            this.m_Disposed = true;
        }

        #endregion IDisposable Members

        private void HandleChange( object sender, PropertyEventArgs args ) {
            this.SendGenericChange(Group.AllParticipant);
        }

        private void SendGenericChange(Group receivers) {
            // Send a generic update with information about the slide (including the new bounds/zoom).
            Message message, deck;
            message = new PresentationInformationMessage( this.m_Presentation );
            message.Group = receivers;
            message.InsertChild( deck = new DeckInformationMessage( this.m_Deck ) );
            deck.InsertChild( new SlideInformationMessage( this.m_Slide ) );
            this.m_Sender.Send( message );
        }

        /// <summary>
        /// Creates the student submissions deck and deck traversal if one doesn't already exist.
        /// Otherwise returns the existing student submission deck
        /// </summary>
        /// <returns>The student submissions deck for this presentation, if one does not 
        /// already exist this function creates one</returns>
        protected DeckModel GetPresentationStudentSubmissionsDeck() {
            if( this.m_Presentation != null ) {
                DeckModel deck = this.m_Presentation.GetStudentSubmissionDeck();

                if( deck != null )
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
                    deck = new DeckModel( ssGuid, DeckDisposition.StudentSubmission, "Student Submissions" );
                    deck.Group = Groups.Group.Submissions;
                    deck.current_subs = true;
                    Message.AddLocalRef(ssGuid, deck);
                    DeckTraversalModel traversal = new SlideDeckTraversalModel( ssTraversalGuid, deck );
                    Message.AddLocalRef(ssTraversalGuid, traversal);

                    // Add the new student submission deck to the presentation
                    using( Synchronizer.Lock( this.m_Presentation.SyncRoot ) ) {
                        this.m_Presentation.DeckTraversals.Add( traversal );
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
        protected SheetModel InkSheetDeepCopyHelper( SheetModel s ) {
            using( Synchronizer.Lock( s.SyncRoot ) ) {
                InkSheetModel t;
                // Only copy InkSheetModels
                if( s is InkSheetModel ) {
                    if( s is RealTimeInkSheetModel ) {
                        // Make a deep copy of the SheetModel
                        t = new RealTimeInkSheetModel( Guid.NewGuid(), s.Disposition | SheetDisposition.Remote, s.Bounds, ((RealTimeInkSheetModel)s).Ink.Clone() );
                        using( Synchronizer.Lock( t.SyncRoot ) ) {
                            ((RealTimeInkSheetModel)t).CurrentDrawingAttributes = ((RealTimeInkSheetModel)s).CurrentDrawingAttributes;
                        }
                    } else {
                        // Make a deep copy of the SheetModel
                        t = new InkSheetModel( Guid.NewGuid(), s.Disposition, s.Bounds, ((InkSheetModel)s).Ink.Clone() );
                    }
                    // This is a new object so add it to the local references
                    Message.AddLocalRef(t.Id, t);
                    return t;
                }
            }

            return s;
        }

        protected void LocalAddStudentSubmission( Guid g, Guid TOCEntryGuid ) {
            using( Synchronizer.Lock( this.m_Slide.SyncRoot ) ) {
                // Only create student submissions from slides that exist on the client machine
                SlideModel newSlide = new SlideModel( g, new LocalId(), SlideDisposition.Remote, new Rectangle(0,0,0,0) );

                // Update the fields of the slide
                using( Synchronizer.Lock( newSlide.SyncRoot ) ) {
                    using( Synchronizer.Lock( this.m_Slide.SyncRoot ) ) {
                        newSlide.Title = this.m_Slide.Title;
                        newSlide.Bounds = new Rectangle( this.m_Slide.Bounds.Location, this.m_Slide.Bounds.Size );
                        newSlide.Zoom = this.m_Slide.Zoom;
                        newSlide.BackgroundColor = this.m_Slide.BackgroundColor;
                        newSlide.BackgroundTemplate = this.m_Slide.BackgroundTemplate;
                        newSlide.SubmissionSlideGuid = this.m_Slide.SubmissionSlideGuid;
                        newSlide.SubmissionStyle = this.m_Slide.SubmissionStyle;

                        // TODO CMPRINCE: Copy all the sheets over (Do a deep copy?)
                        foreach( SheetModel s in this.m_Slide.ContentSheets ) {
                            newSlide.ContentSheets.Add( s );
                        }

                        // Make a deep copy of all the ink sheets
                        foreach( SheetModel s in this.m_Slide.AnnotationSheets ) {
                            newSlide.AnnotationSheets.Add( UW.ClassroomPresenter.Model.Presentation.SheetModel.SheetDeepCopyHelper( s ) );
                        }
                    }
                }

                // Add the slide to the deck
                DeckModel ssDeck = GetPresentationStudentSubmissionsDeck();
                if( ssDeck != null ) {
                    // Add the slide itself
                    using( Synchronizer.Lock( ssDeck.SyncRoot ) ) {
                        ssDeck.InsertSlide( newSlide );
                        Message.AddLocalRef(newSlide.Id, newSlide);
                    }

                    // Add an entry to the deck traversal so that we can navigate to the slide
                    using( Synchronizer.Lock( ssDeck.TableOfContents.SyncRoot ) ) {
                        TableOfContentsModel.Entry e = new TableOfContentsModel.Entry( TOCEntryGuid, ssDeck.TableOfContents, newSlide );
                        ssDeck.TableOfContents.Entries.Add( e );
                        Message.AddLocalRef(e.Id, e);
                    }
                }
            }
        }

        protected Guid SendStudentSubmission( Guid TOCEntryGuid ) {
            Guid newSlideGuid = Guid.Empty;

            UW.ClassroomPresenter.Network.Messages.Message pres, deck, slide, sheet;
            // Add the presentation
            if( this.m_Presentation == null )
                return Guid.Empty;
            pres = new PresentationInformationMessage( this.m_Presentation );
            pres.Group = Groups.Group.Submissions;

            //Add the current deck model that corresponds to this slide deck at the remote location
            deck = new DeckInformationMessage( this.m_Deck );
            deck.Group = Groups.Group.Submissions;
            pres.InsertChild( deck );

            // Add the Slide Message
            newSlideGuid = Guid.NewGuid();
            TOCEntryGuid = Guid.NewGuid();
            slide = new StudentSubmissionSlideInformationMessage( this.m_Slide, newSlideGuid, TOCEntryGuid );
            slide.Group = Groups.Group.Submissions;
            deck.InsertChild( slide );

            // Find the correct user ink layer to send
            RealTimeInkSheetModel m_Sheet = null;
            using( Synchronizer.Lock( this.m_Slide.SyncRoot ) ) {
                foreach( SheetModel s in this.m_Slide.AnnotationSheets ) {
                    if( s is RealTimeInkSheetModel && (s.Disposition & SheetDisposition.Remote) == 0 ) {
                        m_Sheet = (RealTimeInkSheetModel)s;
                        break;
                    }
                }
            }

            // Find the existing ink on the slide
            Microsoft.Ink.Ink extracted;
            using( Synchronizer.Lock( m_Sheet.Ink.Strokes.SyncRoot ) ) {
                // Ensure that each stroke has a Guid which will uniquely identify it on the remote side
                foreach( Microsoft.Ink.Stroke stroke in m_Sheet.Ink.Strokes ) {
                    if( !stroke.ExtendedProperties.DoesPropertyExist( InkSheetMessage.StrokeIdExtendedProperty ) )
                        stroke.ExtendedProperties.Add( InkSheetMessage.StrokeIdExtendedProperty, Guid.NewGuid().ToString() );
                }
                // Extract all of the strokes
                extracted = m_Sheet.Ink.ExtractStrokes( m_Sheet.Ink.Strokes, Microsoft.Ink.ExtractFlags.CopyFromOriginal );
            }

            // Find the Realtime ink on the slide
            RealTimeInkSheetModel newSheet = null;
            using( Synchronizer.Lock( m_Sheet.SyncRoot ) ) {
                newSheet = new RealTimeInkSheetModel( Guid.NewGuid(), m_Sheet.Disposition | SheetDisposition.Remote, m_Sheet.Bounds );
                using( Synchronizer.Lock( newSheet.SyncRoot ) ) {
                    newSheet.CurrentDrawingAttributes = m_Sheet.CurrentDrawingAttributes;
                }
            }

            // Add the Sheet Message for the existing ink
            sheet = new InkSheetStrokesAddedMessage(newSheet, (Guid)slide.TargetId, SheetMessage.SheetCollection.AnnotationSheets, extracted);
            sheet.Group = Groups.Group.Submissions;
            slide.InsertChild( sheet );

            // Add the Sheet Message for the real-time ink
            sheet = SheetMessage.ForSheet( newSheet, SheetMessage.SheetCollection.AnnotationSheets );
            sheet.Group = Groups.Group.Submissions;
            slide.AddOldestPredecessor( sheet );

            // Send the message
            this.m_Sender.Send( pres );

            return newSlideGuid;
        }

        protected void HandleSubmissionStyleChange( object sender, PropertyEventArgs args ) {
            // Send the Student Submission Message
            using( Synchronizer.Lock( this.m_Slide.SyncRoot ) ){
                if( this.m_Slide.SubmissionStyle == SlideModel.StudentSubmissionStyle.Shared ) {
                    Guid TOCEntryGuid = Guid.NewGuid();
                    this.m_Slide.SubmissionSlideGuid = SendStudentSubmission( TOCEntryGuid );
                    LocalAddStudentSubmission( this.m_Slide.SubmissionSlideGuid, TOCEntryGuid );
                }
            }

            // Send a generic update with information about the slide (including the new bounds/zoom).
            Message message, deck;
            message = new PresentationInformationMessage( this.m_Presentation );
            message.InsertChild( deck = new DeckInformationMessage( this.m_Deck ) );
            deck.InsertChild( new SlideInformationMessage( this.m_Slide ) );
            this.m_Sender.Send( message );
        }

        public void SendSheetInformation( SheetModel sheet, SheetMessage.SheetCollection selector, Group receivers ) {
            // TODO: Also pass the index so the sheet can be inserted into the correct index on the remote side.
            Message message, deck, slide;
            message = new PresentationInformationMessage( this.m_Presentation );
            message.Group = receivers;
            message.InsertChild( deck = new DeckInformationMessage( this.m_Deck ) );
            deck.InsertChild( slide = new SlideInformationMessage( this.m_Slide ) );
            Message sm = SheetMessage.RemoteForSheet(sheet, selector);
            //if sm is null that means that the sheet is an instructor note and shouldn't be sent.
            if (sm != null) {
                slide.InsertChild(sm);
            }
            using( Synchronizer.Lock( this.m_Slide.SyncRoot ) ) {
                message.Tags = new MessageTags();
                message.Tags.SlideID = this.m_Slide.Id;
            }
            if (sheet is ImageSheetModel && ((ImageSheetModel)sheet).Image != null) {
                using (Synchronizer.Lock(sheet.SyncRoot)) {
                    this.m_Sender.Send( message );
                }
            }
            else
                this.m_Sender.Send(message);
        }

        private class SheetsCollectionHelper : PropertyCollectionHelper, INetworkService {
            private readonly SlideNetworkService m_Service;
            private readonly SheetMessage.SheetCollection m_Selector;

            public SheetsCollectionHelper(SlideNetworkService service, string property, SheetMessage.SheetCollection selector) : base(service.m_Sender, service.m_Slide, property) {
                this.m_Service = service;
                this.m_Selector = selector;

                base.Initialize();
            }

            protected override void Dispose(bool disposing) {
                if(disposing)
                    foreach(IDisposable disposable in this.Tags)
                        // The entry might be null if SheetNetworkService.ForSheet returned null.
                        if(disposable != null)
                            disposable.Dispose();
                base.Dispose(disposing);
            }

            protected override object SetUpMember(int index, object member) {
                SheetModel sheet = ((SheetModel) member);

                using (Synchronizer.Lock(sheet.SyncRoot)) {
                    // Don't create a network service for the sheet if it is remote,
                    // UNLESS this is a student submission slide in which case we're responsible for rebroadcasting its ink.
                    if ((sheet.Disposition & SheetDisposition.Remote) != 0 && (this.m_Service.m_Slide.Disposition & SlideDisposition.StudentSubmission) == 0)
                        return null;   
                }


                Group receivers = Group.AllParticipant;
                if ((this.m_Service.m_Slide.Disposition & SlideDisposition.StudentSubmission) != 0) {
                    receivers = Group.Submissions;
                }

                this.m_Service.SendSheetInformation(sheet, this.m_Selector, receivers);

                SheetNetworkService service = SheetNetworkService.ForSheet(this.m_Service.m_Sender, this.m_Service.m_Presentation, this.m_Service.m_Deck, this.m_Service.m_Slide, sheet, this.m_Selector);
                return service;
            }

            protected override void TearDownMember(int index, object member, object tag) {
                if(tag == null) return;

                SheetModel sheet = ((SheetModel) member);

                Message message, deck, slide;
                message = new PresentationInformationMessage(this.m_Service.m_Presentation);
                message.InsertChild(deck = new DeckInformationMessage(this.m_Service.m_Deck));
                deck.InsertChild(slide = new SlideInformationMessage(this.m_Service.m_Slide));
                slide.InsertChild(new SheetRemovedMessage(sheet, this.m_Selector));

                using (Synchronizer.Lock(this.m_Service.m_Slide.SyncRoot)) {
                    message.Tags = new MessageTags();
                    message.Tags.SlideID = this.m_Service.m_Slide.Id;
                }

                this.m_Service.m_Sender.Send(message);

                ((SheetNetworkService) tag).Dispose();
            }

            #region INetworkService Members

            public void ForceUpdate(Group receivers) {
                using (Synchronizer.Lock(this.Owner.SyncRoot)) {
                    foreach (SheetNetworkService service_ in this.Tags) {
                        SheetNetworkService service = service_; // For access by delayed delgate.
                        if (service != null) {
                            this.m_Service.m_Sender.Post(delegate() {
                                service.ForceUpdate(receivers);
                            });
                        }
                    }
                }
            }

            #endregion
        }

        #region INetworkService Members

        public void ForceUpdate(Group receivers) {
            // Don't send a GenericChange, since DeckNetworkService.ForceUpdate
            // will already have sent the basic slide information.

            // Send all the slides
            using( Synchronizer.Lock( this.m_Presentation.SyncRoot ) ) {
                using (Synchronizer.Lock(this.m_Deck.SyncRoot)) {
                    using (Synchronizer.Lock(this.m_Slide.SyncRoot)) {
                        foreach (SheetModel sheet in this.m_Slide.ContentSheets) {
                            using (Synchronizer.Lock(sheet.SyncRoot)) {
                                // Don't create a network service for the sheet if it is remote,
                                // UNLESS this is a student submission slide in which case we're responsible for rebroadcasting its ink.
                                if ((sheet.Disposition & SheetDisposition.Remote) == 0 || (this.m_Slide.Disposition & SlideDisposition.StudentSubmission) != 0)
                                    this.SendSheetInformation(sheet, SheetMessage.SheetCollection.ContentSheets, receivers);
                            }
                        }

                        foreach (SheetModel sheet in this.m_Slide.AnnotationSheets) {
                            using (Synchronizer.Lock(sheet.SyncRoot)) {
                                // Don't create a network service for the sheet if it is remote,
                                // UNLESS this is a student submission slide in which case we're responsible for rebroadcasting its ink.
                                if ((sheet.Disposition & SheetDisposition.Remote) == 0 || (this.m_Slide.Disposition & SlideDisposition.StudentSubmission) != 0)
                                    this.SendSheetInformation(sheet, SheetMessage.SheetCollection.AnnotationSheets, receivers);
                            }
                        }
                    }
                }
            }

            // Also don't send any Student Submissions.

            this.m_ContentSheetsCollectionHelper.ForceUpdate(receivers);
            this.m_AnnotationSheetsCollectionHelper.ForceUpdate(receivers);
        }

        #endregion
    }
}
