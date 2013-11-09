// $Id: SlideNetworkService.cs 907 2006-03-18 22:27:43Z cmprince $

using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;

namespace UW.ClassroomPresenter.Network.Messages.Presentation {
    public class SSSlideNetworkService : IDisposable {
        private readonly SendingQueue m_Sender;
//        private readonly EventQueue.PropertyEventDispatcher m_SubmissionSlideGuidChangeDispatcher;
        private readonly EventQueue.PropertyEventDispatcher m_SubmissionStyleChangeDispatcher;
        private readonly PresentationModel m_Presentation;
        private readonly DeckModel m_Deck;
        private readonly SlideModel m_Slide;

        private SSSheetsCollectionHelper m_AnnotationSheetsCollectionHelper;

        private bool m_Disposed;

        public SSSlideNetworkService(SendingQueue sender, PresentationModel presentation, DeckModel deck, SlideModel slide) {
            this.m_Sender = sender;
            this.m_Presentation = presentation;
            this.m_Deck = deck;
            this.m_Slide = slide;

            this.m_SubmissionStyleChangeDispatcher = new EventQueue.PropertyEventDispatcher( this.m_Sender, new PropertyEventHandler( this.HandleChange ) );
            this.m_Slide.Changed["SubmissionStyle"].Add( this.m_SubmissionStyleChangeDispatcher.Dispatcher );

            this.m_AnnotationSheetsCollectionHelper = new SSSheetsCollectionHelper(this, "AnnotationSheets", SheetMessage.SheetCollection.AnnotationSheets);
        }

        #region IDisposable Members

        ~SSSlideNetworkService() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            if(disposing) {
                this.m_Slide.Changed["SubmissionStyle"].Remove( this.m_SubmissionStyleChangeDispatcher.Dispatcher );

                this.m_AnnotationSheetsCollectionHelper.Dispose();
            }
            this.m_Disposed = true;
        }

        #endregion IDisposable Members

        public SlideModel.StudentSubmissionStyle GetSubmissionStyle() {
            if( this.m_Slide == null )
                return SlideModel.StudentSubmissionStyle.Normal;

            Synchronizer.AssertLockIsHeld( this.m_Slide.SyncRoot );
            return this.m_Slide.SubmissionStyle;
        }

        public Guid GetSubmissionSlideGuid() {
            if( this.m_Slide == null )
                return Guid.Empty;

            Synchronizer.AssertLockIsHeld( this.m_Slide.SyncRoot );
            return this.m_Slide.SubmissionSlideGuid;
        }

        public void SetSubmissionSlideGuid( Guid g ) {
            if( this.m_Slide == null ) return;

            using( Synchronizer.Lock( this.m_Slide.SyncRoot ) ) {
                this.m_Slide.SubmissionSlideGuid = g;
            }
        }

        private void HandleChange( object sender, PropertyEventArgs args ) {
            // Send a generic update with information about the slide (including the new bounds/zoom).
            this.m_AnnotationSheetsCollectionHelper.Dispose();
            this.m_AnnotationSheetsCollectionHelper = new SSSheetsCollectionHelper( this, "AnnotationSheets", SheetMessage.SheetCollection.AnnotationSheets );
        }

        private class SSSheetsCollectionHelper : PropertyCollectionHelper {
            private readonly SSSlideNetworkService m_Service;
            private readonly SheetMessage.SheetCollection m_Selector;

            public SSSheetsCollectionHelper(SSSlideNetworkService service, string property, SheetMessage.SheetCollection selector) : base(service.m_Sender, service.m_Slide, property) {
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
                SSSheetNetworkService service = null;
                using( Synchronizer.Lock( m_Service.m_Slide.SyncRoot ) ) {
                    if( this.m_Service.GetSubmissionStyle() != SlideModel.StudentSubmissionStyle.Normal ) {
                        using( Synchronizer.Lock( sheet.SyncRoot ) ) {
                            if( (sheet.Disposition & SheetDisposition.Remote) == 0 && (sheet is RealTimeInkSheetModel) ) {
                                if( this.m_Service.GetSubmissionStyle() == SlideModel.StudentSubmissionStyle.Shared ) {
                                    Message.AddLocalRef(this.m_Service.GetSubmissionSlideGuid(), this.m_Service.m_Slide);
                                }
                                //System.Windows.Forms.MessageBox.Show( sheet.GetType().ToString() );
                                service = new SSRealTimeInkSheetNetworkService( this.m_Service.m_Sender,
                                                                                this.m_Service.m_Presentation,
                                                                                this.m_Service.m_Deck,
                                                                                this.m_Service.m_Slide,
                                                                                (RealTimeInkSheetModel)sheet,
                                                                                this.m_Selector );
                            }
                        }
                    }
                }
                return service;
            }

            protected override void TearDownMember(int index, object member, object tag) {
                if(tag == null) return;
                ((SSSheetNetworkService) tag).Dispose();
            }
        }
    }
}
