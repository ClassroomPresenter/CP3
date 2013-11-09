// $Id: SlideMatch.cs 975 2006-06-28 01:02:59Z pediddle $

using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Threading;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;

namespace UW.ClassroomPresenter.DeckMatcher {
    /// <summary>
    /// Class that represents a match between slides. The class marshals changes to the Source Slide
    /// onto the Destination Slide
    /// </summary>
    public class SlideMatch : IDisposable {
        // Private variables
        private bool m_Disposed;
        private readonly EventQueue m_Sender;
        private readonly EventQueue.PropertyEventDispatcher m_ChangeDispatcher;

        private readonly SlideModel m_SrcSlide;
        private readonly SlideModel m_DestSlide;

        //private readonly SheetsCollectionHelper m_ContentSheetsCollectionHelper;
        private readonly SheetsCollectionHelper m_AnnotationSheetsCollectionHelper;

        private ArrayList m_SheetMatches = new ArrayList();

        // Accessors for the private variables
        public SlideModel SourceSlide {
            get { return this.m_SrcSlide; }
        }
        public SlideModel DestinationSlide {
            get { return this.m_DestSlide; }
        }

        /// <summary>
        /// Construct a slide match between two slides
        /// </summary>
        /// <param name="sender">The event queue for async calls</param>
        /// <param name="srcSlide">The source slide</param>
        /// <param name="destSlide">The destination slide</param>
        public SlideMatch( EventQueue sender, SlideModel srcSlide, SlideModel destSlide ) {
            this.m_Sender = sender;
            this.m_SrcSlide = srcSlide;
            this.m_DestSlide = destSlide;

            // Listen for changes
            this.m_ChangeDispatcher = new EventQueue.PropertyEventDispatcher(this.m_Sender, new PropertyEventHandler(this.HandleChange));
            this.m_Sender.Post(delegate() {
                this.m_ChangeDispatcher.Dispatcher(this, null);
            });
            this.m_SrcSlide.Changed["Bounds"].Add(this.m_ChangeDispatcher.Dispatcher);
            this.m_SrcSlide.Changed["Zoom"].Add(this.m_ChangeDispatcher.Dispatcher);
            this.m_SrcSlide.Changed["SubmissionSlideGuid"].Add( this.m_ChangeDispatcher.Dispatcher );
            this.m_SrcSlide.Changed["SubmissionStyle"].Add( this.m_ChangeDispatcher.Dispatcher );

            // Listen for changes to the sheets
            // NOTE: Content sheets cannot change, so we don't need to listen to changes in them
            // this.m_ContentSheetsCollectionHelper = new SheetsCollectionHelper(this, "ContentSheets");
            this.m_AnnotationSheetsCollectionHelper = new SheetsCollectionHelper(this, "AnnotationSheets");
        }

        #region IDisposable Members

        /// <summary>
        /// Finalize this object
        /// </summary>
        ~SlideMatch() {
            this.Dispose(false);
        }

        /// <summary>
        /// Dispose this object
        /// </summary>
        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose of all resources used by this class
        /// </summary>
        /// <param name="disposing">True if we are in the process of disposing, false otherwise</param>
        protected virtual void Dispose( bool disposing ) {
            if( this.m_Disposed ) return;
            if( disposing ) {
                this.m_SrcSlide.Changed["Bounds"].Remove(this.m_ChangeDispatcher.Dispatcher);
                this.m_SrcSlide.Changed["Zoom"].Remove(this.m_ChangeDispatcher.Dispatcher);
                this.m_SrcSlide.Changed["SubmissionSlideGuid"].Remove( this.m_ChangeDispatcher.Dispatcher );
                this.m_SrcSlide.Changed["SubmissionStyle"].Remove( this.m_ChangeDispatcher.Dispatcher );

                //this.m_ContentSheetsCollectionHelper.Dispose();
                this.m_AnnotationSheetsCollectionHelper.Dispose();

                foreach( SheetMatch m in this.m_SheetMatches )
                    if( m != null )
                        m.Dispose();
            }
            this.m_Disposed = true;
        }

        #endregion IDisposable Members

        /// <summary>
        /// Marshals changes regarding the zoom or bounds of the slide between the linked slide decks
        /// </summary>
        /// <param name="sender">The object that sent the message</param>
        /// <param name="args">The arguments</param>
        private void HandleChange(object sender, PropertyEventArgs args) {
            using( Synchronizer.Lock( this.m_SrcSlide.SyncRoot ) ) {
                using( Synchronizer.Lock( this.m_DestSlide.SyncRoot ) ) {
                    this.m_DestSlide.Zoom = this.m_SrcSlide.Zoom;
                    this.m_DestSlide.Bounds = new Rectangle( this.m_SrcSlide.Bounds.Location, this.m_SrcSlide.Bounds.Size );
                    this.m_DestSlide.SubmissionSlideGuid = this.m_SrcSlide.SubmissionSlideGuid;
                    this.m_DestSlide.SubmissionStyle = this.m_SrcSlide.SubmissionStyle;
                }
            }
        }

        private class SheetsCollectionHelper : PropertyCollectionHelper {
            private readonly SlideMatch m_Owner;
//            private readonly SheetMessage.SheetCollection m_Selector;

            public SheetsCollectionHelper(SlideMatch owner, string property ) : base( owner.m_Sender, owner.m_SrcSlide, property ) {
                this.m_Owner = owner;
                base.Initialize();
            }

            protected override object SetUpMember(int index, object member) {
                SheetModel sheet = ((SheetModel) member);

                // Add the SheetMatch to the collection of matches
                using( Synchronizer.Lock( sheet.SyncRoot ) ) {
                    using (Synchronizer.Lock( this.m_Owner.m_DestSlide.SyncRoot ) ) {
                        if( sheet is RealTimeInkSheetModel ) {
                            // Add the sheet
                            RealTimeInkSheetModel m = new RealTimeInkSheetModel( Guid.NewGuid(), sheet.Disposition, sheet.Bounds );
                            this.m_Owner.m_DestSlide.AnnotationSheets.Add( m );

                            // Add the sheet match
                            SheetMatch toAdd = SheetMatch.ForSheet(this.m_Owner.m_Sender, sheet, m );
                            this.m_Owner.m_SheetMatches.Add( toAdd );
                        }
                        else if( sheet is InkSheetModel ) {
                            // Add the sheet
                            InkSheetModel m = new InkSheetModel( Guid.NewGuid(), sheet.Disposition, sheet.Bounds );
                            this.m_Owner.m_DestSlide.AnnotationSheets.Add( m );

                            // Add the sheet match
                            SheetMatch toAdd = SheetMatch.ForSheet(this.m_Owner.m_Sender, sheet, m );
                            this.m_Owner.m_SheetMatches.Add( toAdd );
                        }
                    }
                }

                return null;
            }

            protected override void TearDownMember(int index, object member, object tag) {
                if(tag == null) return;
                SheetModel sheet = ((SheetModel) member);

                ArrayList toRemove = new ArrayList();
                foreach( SheetMatch m in this.m_Owner.m_SheetMatches ) {
                    if( m.SourceSheet == sheet )
                        toRemove.Add( m );
                }
                foreach( SheetMatch m in toRemove ) {
                    this.m_Owner.m_SheetMatches.Remove( m );
                    m.Dispose();
                }
            }
        }
    }
}
