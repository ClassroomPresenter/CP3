// $Id: StudentSubmissionNetworkService.cs 606 2005-08-26 19:42:21Z pediddle $

using System;
using System.Diagnostics;
using System.Threading;

using Microsoft.Ink;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Model.Workspace;
using UW.ClassroomPresenter.Model.Presentation;

using UW.ClassroomPresenter.Network.Messages.Network;
using UW.ClassroomPresenter.Network.Messages.Presentation;
using UW.ClassroomPresenter.Model.Viewer;

namespace UW.ClassroomPresenter.Network.Messages {
    /// <summary>
    /// This component is responsible for listening to indications from the UI 
    /// that the current slide ink is supposed to be sent as a student submission
    /// </summary>
    public class StudentSubmissionNetworkService : IDisposable {
        #region Private Members

        /// <summary>
        /// The sending queue onto which all network messages are sent
        /// </summary>
        private readonly SendingQueue m_Sender;

        /// <summary>
        /// The presenter model
        /// </summary>
        private readonly PresenterModel m_Model;

        /// <summary>
        /// The event dispatcher that is used to marshal change events
        /// </summary>
        private readonly EventQueue.PropertyEventDispatcher m_SendChangeDispatcher;

        /// <summary>
        /// Flag indicating if the object has been collected or not
        /// </summary>
        private bool m_Disposed;

        /// <summary>
        /// Lock for the current sending
        /// </summary>
        private bool m_SendingLock;

        /// <summary>
        /// Timer for the current sending
        /// </summary>
        private System.Timers.Timer m_SendingLockTimer;

        /// <summary>
        /// The event dispatcher that is used to marshal change events
        /// </summary>
        private readonly EventQueue.PropertyEventDispatcher m_SubmissionIntervalDispatcher;

        #endregion

        #region Construction

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="sender">The network queue to send messages on</param>
        /// <param name="model">The presenter model</param>
        public StudentSubmissionNetworkService(SendingQueue sender, PresenterModel model) {
            this.m_Sender = sender;
            this.m_Model = model;
            this.m_SendingLock = false;
            this.m_SendingLockTimer = new System.Timers.Timer();
            if (model.ViewerState != null)
            {
                using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot))
                {
                    this.m_SendingLockTimer.Interval = this.m_Model.ViewerState.StudentSubmissionInterval*1000;
                }
            }
            this.m_SendingLockTimer.Elapsed += new System.Timers.ElapsedEventHandler(SendingLockTimer_Elapsed);
            this.m_SendingLockTimer.Enabled = false;

            // Setup the event listener for this 
            this.m_SendChangeDispatcher = new EventQueue.PropertyEventDispatcher( this.m_Sender, new PropertyEventHandler(this.HandleSendSubmission) );
            
            //FV: Not locking here resolves a lock order warning.
            //using( Synchronizer.Lock( this.m_Model.ViewerState.SyncRoot ) ) {
                this.m_Model.ViewerState.Changed["StudentSubmissionSignal"].Add( this.m_SendChangeDispatcher.Dispatcher );
            //}

            this.m_SubmissionIntervalDispatcher = new EventQueue.PropertyEventDispatcher(this.m_Sender, new PropertyEventHandler(this.HandleSubmissionIntervalChanged));
            this.m_Model.ViewerState.Changed["StudentSubmissionInterval"].Add(this.m_SubmissionIntervalDispatcher.Dispatcher);
        }

        #endregion

        #region Submissions

        /// <summary>
        /// Send the current ink as a student submission slide
        /// </summary>
        /// <param name="sender">The object which sent this event, i.e. this class</param>
        /// <param name="args">The parameters for the property</param>
        private void HandleSendSubmission( object sender, PropertyEventArgs args ) {
            if (this.m_SendingLock) return;

            using (Synchronizer.Lock(SubmissionStatusModel.GetInstance().SyncRoot)) {
                SubmissionStatusModel.GetInstance().SubmissionStatus = SubmissionStatusModel.Status.NotReceived;
            }
            
            
            ///declare variables we will be using
            UW.ClassroomPresenter.Network.Messages.Message pres, deck, slide, sheet;
            // Construct the message to send
            using( this.m_Model.Workspace.Lock() ) {
                // Add the presentation
                if( this.m_Model.Workspace.CurrentPresentation == null )
                    return;
                ///the presentation message we will be sending
                pres = new PresentationInformationMessage( this.m_Model.Workspace.CurrentPresentation );
                pres.Group = Groups.Group.Submissions;

                //Add the current deck model that corresponds to this slide deck at the remote location
                if( (~this.m_Model.Workspace.CurrentDeckTraversal) == null )
                    return;
                using( Synchronizer.Lock( (~this.m_Model.Workspace.CurrentDeckTraversal).SyncRoot ) ) {
                    DeckModel dModel = (~this.m_Model.Workspace.CurrentDeckTraversal).Deck;
                    foreach( DeckPairModel match in this.m_Model.Workspace.DeckMatches ) {
                        ///check to see if the decks are the same
                        if( match.LocalDeckTraversal.Deck == (~this.m_Model.Workspace.CurrentDeckTraversal).Deck )
                            dModel = match.RemoteDeckTraversal.Deck;
                    }
                    ///create the deck message from this matched deck
                    deck = new DeckInformationMessage( dModel );
                    ///make the deck a submission type deck.
                    deck.Group = Groups.Group.Submissions;
                    ///tack this message onto the end.
                    pres.InsertChild( deck );

                    ///add the particular slide we're on the to message.
                    if( (~this.m_Model.Workspace.CurrentDeckTraversal).Current == null )
                        return;
                    using( Synchronizer.Lock( (~this.m_Model.Workspace.CurrentDeckTraversal).Current.Slide.SyncRoot ) ) {
                        // Add the Slide Message
                        slide = new StudentSubmissionSlideInformationMessage( (~this.m_Model.Workspace.CurrentDeckTraversal).Current.Slide, Guid.NewGuid(), Guid.NewGuid() );
                        slide.Group = Groups.Group.Submissions;
                        deck.InsertChild( slide );

                        // Find the correct user ink layer to send
                        RealTimeInkSheetModel m_Sheet = null;
                        int count = 0;
                        foreach( SheetModel s in (~this.m_Model.Workspace.CurrentDeckTraversal).Current.Slide.AnnotationSheets ) {
                            if( s is RealTimeInkSheetModel && (s.Disposition & SheetDisposition.Remote) == 0 ) {
                                m_Sheet = (RealTimeInkSheetModel)s;
                                count++;
                            }
                        }
                       
                        // DEBUGGING
                        if( count > 1 )
                            Debug.Assert( true, "Bad Count", "Bad" );

                        // Find the existing ink on the slide
                        Ink extracted;
                        using( Synchronizer.Lock( m_Sheet.Ink.Strokes.SyncRoot ) ) {
                            // Ensure that each stroke has a Guid which will uniquely identify it on the remote side
                            foreach( Stroke stroke in m_Sheet.Ink.Strokes ) {
                                if( !stroke.ExtendedProperties.DoesPropertyExist( InkSheetMessage.StrokeIdExtendedProperty ) )
                                    stroke.ExtendedProperties.Add( InkSheetMessage.StrokeIdExtendedProperty, Guid.NewGuid().ToString() );
                            }
                            // Extract all of the strokes
                            extracted = m_Sheet.Ink.ExtractStrokes( m_Sheet.Ink.Strokes, ExtractFlags.CopyFromOriginal );
                        }


                        // Find the Realtime ink on the slide
                        RealTimeInkSheetModel newSheet = null;
                        using( Synchronizer.Lock( m_Sheet.SyncRoot ) ) {
                            newSheet = new RealTimeInkSheetModel( Guid.NewGuid(), m_Sheet.Disposition | SheetDisposition.Remote, m_Sheet.Bounds );
                            using( Synchronizer.Lock( newSheet.SyncRoot ) ) {
                                newSheet.CurrentDrawingAttributes = m_Sheet.CurrentDrawingAttributes;
                            }
                        }

                        // Add a message to *create* the student's RealTimeInkSheetModel on the instructor client (without any ink).
                        sheet = SheetMessage.ForSheet(newSheet, SheetMessage.SheetCollection.AnnotationSheets);
                        sheet.Group = Groups.Group.Submissions;
                        slide.InsertChild(sheet);

                        //Scale the ink if necessary
                        if (ViewerStateModel.NonStandardDpi) {
                            extracted.Strokes.Transform(ViewerStateModel.DpiNormalizationSendMatrix);
                        }

                        // Add a message to copy the ink from the student's RealTimeInkSheetModel to the just-created sheet on the instructor.
                        sheet = new InkSheetStrokesAddedMessage(newSheet, (Guid)slide.TargetId, SheetMessage.SheetCollection.AnnotationSheets, extracted);
                        sheet.Group = Groups.Group.Submissions;
                        slide.InsertChild( sheet );

                        ///Add each text and image sheet into the message as children of the ink sheet if it is public
                        foreach( SheetModel s in (~this.m_Model.Workspace.CurrentDeckTraversal).Current.Slide.AnnotationSheets ) {
                            if( s is TextSheetModel && !(s is StatusLabel) && (s.Disposition & SheetDisposition.Remote) == 0) {
                                TextSheetModel text_sheet = (TextSheetModel) s;
                                text_sheet = (TextSheetModel) text_sheet.Clone();
                                ///some ugly code here due to synchronization
                                bool sheet_is_public;
                                using (Synchronizer.Lock(text_sheet.SyncRoot)) {
                                    sheet_is_public = text_sheet.IsPublic;
                                }
                                if (sheet_is_public) {
                                    TextSheetMessage t_message = new TextSheetMessage(text_sheet, SheetMessage.SheetCollection.AnnotationSheets);
                                    t_message.Group = Groups.Group.Submissions;
                                    slide.InsertChild(t_message);
                                }
                            }
                            if (s is ImageSheetModel && !(s is StatusLabel) && (s.Disposition & SheetDisposition.Remote) == 0) {
                                ImageSheetModel image_sheet = (ImageSheetModel)s;
                                image_sheet = (ImageSheetModel) image_sheet.Clone();
                                ImageSheetMessage i_message = new ImageSheetMessage(image_sheet, SheetMessage.SheetCollection.AnnotationSheets);
                                i_message.Group = Groups.Group.Submissions;
                                slide.InsertChild(i_message);
                            
                            }
                        }
                        //Lock the current sending.
                        this.LockSending();

                        // Send the message
                        this.m_Sender.Send(pres);
                    }
                }
            }
        }

        /// <summary>
        /// Update the timer interval
        /// </summary>
        /// <param name="sender">The object which sent this event, i.e. this class</param>
        /// <param name="args">The parameters for the property</param>
        private void HandleSubmissionIntervalChanged(object sender, PropertyEventArgs args)
        {
            using (Synchronizer.Lock(this.m_Model.ViewerState.SyncRoot))
            {
                this.m_SendingLockTimer.Interval = this.m_Model.ViewerState.StudentSubmissionInterval * 1000;
            }
        }

        /// <summary>
        /// Lock the current sending
        /// </summary>
        private void LockSending()
        {
            this.m_SendingLock = true;
            this.m_SendingLockTimer.Enabled = true;

            //disable the student submission toolbar
            this.SetStudentSubmissionToolbar(true);
            this.m_SendingLockTimer.Start();
        }

        /// <summary>
        /// Sending Lock Timer elapses
        /// </summary>
        private void SendingLockTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            this.m_SendingLockTimer.Stop();
            this.m_SendingLockTimer.Enabled = false;            
            this.m_SendingLock = false;
            //Enable the student submission toolbar
            this.SetStudentSubmissionToolbar(false);
        }

        /// <summary>
        /// Set the disable status for student submission button
        /// </summary>
        /// <param name="disabled"></param>
        private void SetStudentSubmissionToolbar(bool disabled)
        {
            ParticipantModel participant = null;
            using (Synchronizer.Lock(this.m_Model.Workspace.CurrentPresentation.SyncRoot))
            {
                if (this.m_Model.Workspace.CurrentPresentation.Value != null)
                {
                    using (Synchronizer.Lock(this.m_Model.Workspace.CurrentPresentation.Value.SyncRoot))
                    {
                        participant = this.m_Model.Workspace.CurrentPresentation.Value.Owner;
                    }
                }
            }
            if (participant != null)
            {
                using (Synchronizer.Lock(participant.SyncRoot))
                {
                    using (Synchronizer.Lock(participant.Role.SyncRoot))
                    {
                        ((InstructorModel)(participant.Role)).AcceptingStudentSubmissions = !disabled;
                    }
                }
            }

        }

        #endregion

        #region IDisposable Members

        /// <summary>
        /// Destructor
        /// </summary>
        ~StudentSubmissionNetworkService() {
            this.Dispose(false);
        }

        /// <summary>
        /// Disposes of the resources associated with this component
        /// </summary>
        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Internal version of dispose that does all the real work
        /// </summary>
        /// <param name="disposing">True if we are in the process of disposing</param>
        protected virtual void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            if(disposing) {
                using( Synchronizer.Lock( this.m_Model.ViewerState.SyncRoot ) ) {
                    this.m_Model.ViewerState.Changed["StudentSubmissionSignal"].Remove( this.m_SendChangeDispatcher.Dispatcher );
                }
            }
            this.m_Disposed = true;
        }

        #endregion
    }
}
