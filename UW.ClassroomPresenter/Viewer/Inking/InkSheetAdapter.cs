// $Id: InkSheetAdapter.cs 834 2005-12-08 23:11:11Z cmprince $

using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Model.Viewer;

namespace UW.ClassroomPresenter.Viewer.Inking {

    public class InkSheetAdapter : IDisposable {
        private readonly SlideDisplayModel m_SlideDisplay;
        private readonly EventQueue.PropertyEventDispatcher m_SlideChangedDispatcher;
        private readonly IAdaptee m_Adaptee;
        private bool m_Disposed;

        #region Construction & Destruction

        public InkSheetAdapter(SlideDisplayModel display, IAdaptee adaptee) {
            this.m_SlideDisplay = display;
            this.m_Adaptee = adaptee;

            this.m_SlideChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_SlideDisplay.EventQueue, new PropertyEventHandler(this.HandleSlideChanged));
            this.m_SlideDisplay.Changed["Slide"].Add(this.m_SlideChangedDispatcher.Dispatcher);
            this.m_SlideChangedDispatcher.Dispatcher(this.m_SlideDisplay, null);
        }

        ~InkSheetAdapter() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            if(disposing) {
                this.m_SlideDisplay.Changed["Slide"].Remove(this.m_SlideChangedDispatcher.Dispatcher);
            }
            this.m_Disposed = true;
        }

        #endregion Construction & Destruction


        #region SlideDisplayModel Events

        private void HandleSlideChanged(object sender, PropertyEventArgs args) {
            using(Synchronizer.Lock(this)) {
                SlideModel slide;
                using(Synchronizer.Lock(this.m_SlideDisplay.SyncRoot)) {
                    slide = this.m_SlideDisplay.Slide;
                    // Release the reader lock immediately, because it is not possible (or at least easy)
                    // to guarantee consistent locking order between the SlideDisplayModel and the SlideModel.
                    // Most of the SheetRenderer classes will obtain a lock on the SlideModel *first*
                    // and the SlideDisplayModel *second* because they react to changes in the slide;
                    // but that order is not possible here.
                }

                if(slide == null) {
                    this.m_Adaptee.InkSheetModel = null;
                    this.m_Adaptee.RealTimeInkSheetModel = null;
                }

                else {
                    using(Synchronizer.Lock(slide.SyncRoot)) {
                        try {
                            InkSheetModel inks = null;
                            RealTimeInkSheetModel rti = null;

                            // TODO: This code is duplicated in SlideToolBarButtons.ClearInkSheetToolBarButton.  Extract to a "ActiveInkAnnotationSheet" property of the SlideModel.
                            // Find the *top-most* InkSheetModel and RealTimeInkSheetModel in the annotation layer.
                            foreach(SheetModel sheet in slide.AnnotationSheets) {
                                // Only consider local sheets.
                                if((sheet.Disposition & SheetDisposition.Remote) != 0) {
                                    continue;

                                // RealTimeInkSheetModels get priority.
                                } else if(sheet is RealTimeInkSheetModel) {
                                    inks = rti = ((RealTimeInkSheetModel) sheet);

                                // Regular InkSheetModels are our second choice.
                                } else if(sheet is InkSheetModel) {
                                    inks = ((InkSheetModel) sheet);
                                    rti = null;

                                // Only consider the *top-most* non-remote sheet (the last one in the collection).
                                } else {
                                    continue;
                                }
                            }

                            if(inks == null && rti == null) {
                                // If the slide does not have an ink annotation sheet, create one.
                                inks = rti = new RealTimeInkSheetModel(Guid.NewGuid(), SheetDisposition.All, Rectangle.Empty);

                                // Add it to the slide.
                                slide.AnnotationSheets.Add(rti);
                            }

                            // Start collecting ink into the InkSheetModel's Ink object
                            // (after the sheet is added to the slide, so renderers don't get out of sync).
                            // Also start sending events to InkSheetModel.RealTimeInk.
                            this.m_Adaptee.InkSheetModel = rti == null ? inks : rti;
                            this.m_Adaptee.RealTimeInkSheetModel = rti;
                        }

                        catch {
                            // We were unable to get an Ink annotation sheet, so disable inking.
                            this.m_Adaptee.InkSheetModel = null;
                            this.m_Adaptee.RealTimeInkSheetModel = null;
                            throw;
                        }
                    }
                }
            }
        }

        #endregion SlideDisplayModel Events

        public interface IAdaptee {
            InkSheetModel InkSheetModel { set; }
            RealTimeInkSheetModel RealTimeInkSheetModel { set; }
        }
    }
}
