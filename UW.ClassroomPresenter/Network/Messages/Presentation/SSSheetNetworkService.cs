// $Id: SSSheetNetworkService.cs 656 2005-08-30 06:51:53Z cmprince $

using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;

namespace UW.ClassroomPresenter.Network.Messages.Presentation {
    public class SSSheetNetworkService : IDisposable {
        private readonly SendingQueue m_Sender;
        private readonly PresentationModel m_Presentation;
        private readonly DeckModel m_Deck;
        private readonly SlideModel m_Slide;
        private readonly SheetModel m_Sheet;
        private readonly SheetMessage.SheetCollection m_Selector;

        private bool m_Disposed;

        public SSSheetNetworkService(SendingQueue sender, PresentationModel presentation, DeckModel deck, SlideModel slide, SheetModel sheet, SheetMessage.SheetCollection selector) {
            this.m_Sender = sender;
            this.m_Presentation = presentation;
            this.m_Deck = deck;
            this.m_Slide = slide;
            this.m_Sheet = sheet;
            this.m_Selector = selector;
        }

        #region IDisposable Members

        ~SSSheetNetworkService() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            if(disposing) {
            }
            this.m_Disposed = true;
        }

        #endregion IDisposable Members

        protected SendingQueue Sender {
            get { return this.m_Sender; }
        }

        protected PresentationModel Presentation {
            get { return this.m_Presentation; }
        }

        protected DeckModel Deck {
            get { return this.m_Deck; }
        }

        protected SlideModel Slide {
            get { return this.m_Slide; }
        }
        protected Guid NewSlideGuid = Guid.Empty;

        protected SheetModel Sheet {
            get { return this.m_Sheet; }
        }

        protected SheetMessage.SheetCollection SheetCollectionSelector {
            get { return this.m_Selector; }
        }

        protected SlideModel GetSubmissionSlideModel() {
            SlideModel m = this.Slide;

            if( this.Slide != null ) {
                using( Synchronizer.Lock( this.Slide.SyncRoot ) ) {
                    // Determine if we need to create a guid ourselves or not
                    if( this.NewSlideGuid == Guid.Empty &&
                        this.Slide.SubmissionStyle == SlideModel.StudentSubmissionStyle.Realtime ) {
                        // Send the student submission and
                        // Set the NewSlideGuid to the slide value we used
                        this.NewSlideGuid = SendStudentSubmission();
                    }

                    if( this.Slide.SubmissionStyle == SlideModel.StudentSubmissionStyle.Realtime )
                        m = new SlideModel( this.NewSlideGuid, new LocalId(), SlideDisposition.Remote, this.Slide.Bounds );
                    else
                        m = new SlideModel( this.Slide.SubmissionSlideGuid, new LocalId(), SlideDisposition.Remote, this.Slide.Bounds );

                    using( Synchronizer.Lock( m.SyncRoot ) ) {
                        m.SubmissionSlideGuid = this.Slide.SubmissionSlideGuid;
                        m.SubmissionStyle = this.Slide.SubmissionStyle;
                    }
                }
            }

            return m;
        }

        #region Student Submission

        protected Guid SendStudentSubmission() {
            Guid newSlideGuid = Guid.Empty;

            UW.ClassroomPresenter.Network.Messages.Message pres, deck, slide, sheet;
            // Add the presentation
            if( this.Presentation == null )
                return Guid.Empty;
            pres = new PresentationInformationMessage( this.Presentation );
            pres.Group = Groups.Group.Submissions;

            //Add the current deck model that corresponds to this slide deck at the remote location
            deck = new DeckInformationMessage( this.Deck );
            deck.Group = Groups.Group.Submissions;
            pres.InsertChild( deck );

            // Add the Slide Message
            newSlideGuid = Guid.NewGuid();
            slide = new StudentSubmissionSlideInformationMessage( this.Slide, newSlideGuid, Guid.NewGuid() );
            slide.Group = Groups.Group.Submissions;
            deck.InsertChild( slide );

            // Find the correct user ink layer to send
            RealTimeInkSheetModel m_Sheet = null;
            using( Synchronizer.Lock( this.Slide.SyncRoot ) ) {
                foreach( SheetModel s in this.Slide.AnnotationSheets ) {
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

            // Add the Sheet Message for the existing
            sheet = new InkSheetStrokesAddedMessage( newSheet, (Guid)slide.TargetId, SheetMessage.SheetCollection.AnnotationSheets, extracted );
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

        #endregion
    }
}
