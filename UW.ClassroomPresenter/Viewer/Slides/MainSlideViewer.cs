// $Id: MainSlideViewer.cs 2051 2009-08-19 18:20:16Z lining $

using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Windows.Forms;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Background;
using UW.ClassroomPresenter.Model.Workspace;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Model.Stylus;
using UW.ClassroomPresenter.Viewer.Text;
using UW.ClassroomPresenter.Model.Network;
using UW.ClassroomPresenter.Viewer.Background;
using UW.ClassroomPresenter.Viewer.TextAndImageEditing;

namespace UW.ClassroomPresenter.Viewer.Slides {
    public class MainSlideViewer : SlideViewer {

        

        private static string NO_SLIDE_MESSAGE = Strings.NoSlide;
        private static string UNVISITED_SLIDE_MESSAGE = Strings.UnvisitedSlide;
        public PresenterModel presenter_model_;

        /// <summary>
        /// when we receive a message, this keeps the message up for a little before 
        /// removing it.
        /// </summary>
        private System.Windows.Forms.Timer submission_status_timer_;
        /// <summary>
        /// timer that marks at which point a student submission has failed.
        /// </summary>
        private System.Windows.Forms.Timer submission_failed_timer_;

        // The width of the 3D border drawn by ControlPaint.DrawBorder3D.
        private const int BORDER_WIDTH = 2;

        /// <summary>
        /// indicates whether we are editing text or not;
        /// </summary>
        private bool editing_text_ = false;

        /// <summary>
        /// indicates whether our page CAN be edited when the text editing
        /// option is on (i.e. if it was a preview or filmstrip, this would be false);
        /// </summary>
        private readonly bool editable_page_;

        /// <summary>
        /// Whether Dispose() has been invoked.
        /// </summary>
        private bool disposed_;

        ///The following code handles locking issues
        /// <summary>
        /// Event listener for the PresenterModel.Stylus property.
        /// </summary>
        private readonly EventQueue.PropertyEventDispatcher stylus_listener_;

        /// <summary>
        /// Event listener for the ViewerStateModel.StudentSubmissionsSignal property.
        /// </summary>
        private readonly EventQueue.PropertyEventDispatcher student_submissions_signal_listener_;


        //Listen to see when the dirty bit changes so that we can know if we 
        //need to set the dirty bit on a mousedown
        private readonly EventQueue.PropertyEventDispatcher m_DirtyDispatcher;

        /// <summary>
        /// Property dispatcher
        /// </summary>
        private readonly IDisposable m_CurrentDeckTraversalDispatcher;


        /// <summary>
        /// Event listener for the StylusModel.DrawingAttributes property.
        /// </summary>
        private readonly EventQueue.PropertyEventDispatcher stylus_color_listener_;

        private readonly EventQueue.PropertyEventDispatcher submission_status_listener_;

        /// <summary>
        /// The most recent value of PresenterModel.Stylus.
        /// </summary>
        private static StylusModel current_stylus_;


        ///for the textitboxcollectionhelper class
        private readonly ControlEventQueue control_queue_;
        private static TextItBoxCollectionHelper current_text_helper_;

        ///for the imageitcollectionhelper class
        private static ImageItCollectionHelper current_image_helper_;

        private readonly EventQueue.PropertyEventDispatcher m_PrimaryMonitorFullScreenListener;
 

        #region Constructors
        /// <summary>
        /// null constructor for use with deckbuilder
        /// </summary>
        public MainSlideViewer() {
            this.Name = "MainSlideViewer";
        }

        /// <summary>
        /// constructor for use with presenter
        /// </summary>
        /// <param name="model">the presenter model we're working with</param>
        /// <param name="is_editable">whether text editing mode is enabled in this view (i.e. in filmstrips it is not).</param>
        public MainSlideViewer(PresenterModel model, bool is_editable) {
            ///Initialize variables
            this.Name = "MainSlideViewer";
            presenter_model_ = model;
            editable_page_ = is_editable;

            ///Initialize timers
            submission_status_timer_ = new System.Windows.Forms.Timer();
            submission_failed_timer_ = new System.Windows.Forms.Timer();
            ///leave the message received message up for 4 seconds before erasing.
            submission_status_timer_.Interval = 4000;
            ///wait 10 seconds before declaring a submission to have failed.
            submission_failed_timer_.Interval = 15000;
            submission_status_timer_.Tick += new EventHandler(submission_status_timer__Tick);
            submission_failed_timer_.Tick += new EventHandler(submission_failed_timer__Tick);

            ///we need to know when we go into text editing mode, so we need to monitor
            ///when the stylus changes
            stylus_listener_ = new EventQueue.PropertyEventDispatcher(this.SlideDisplay.EventQueue,
                new PropertyEventHandler(this.HandleStylusChanged));
            presenter_model_.Changed["Stylus"].Add(stylus_listener_.Dispatcher);

            ///let's us know when the student has clicked student submissions
            student_submissions_signal_listener_ = new EventQueue.PropertyEventDispatcher(this.SlideDisplay.EventQueue,
                new PropertyEventHandler(this.HandleStudentSubmission));
            presenter_model_.ViewerState.Changed["StudentSubmissionSignal"].Add(student_submissions_signal_listener_.Dispatcher);
            
            if (editable_page_) {
                submission_status_listener_ = new EventQueue.PropertyEventDispatcher(this.SlideDisplay.EventQueue,
                    new PropertyEventHandler(this.SubmissionStatusChanged));
                SubmissionStatusModel.GetInstance().Changed["SubmissionStatus"].Add(submission_status_listener_.Dispatcher);


                ///for the textitboxcolletionhelper
                control_queue_ = new ControlEventQueue(this);



                //Attach property listeners to the Student Submission filmstrip's width
                if (is_editable) {
                    this.m_DirtyDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue,
                        new PropertyEventHandler(this.OnDirtyChanged));
                    /*this.presenter_model_.Workspace.CurrentDeckTraversal.Value.Deck.Changed["Dirty"].Add(this.m_DirtyListener.Dispatcher);
                    this.m_DirtyListener.Dispatcher(this, null);
                    this.MouseDown += new MouseEventHandler(Dirty_MouseDown);*/

                    this.m_CurrentDeckTraversalDispatcher =
        this.presenter_model_.Workspace.CurrentDeckTraversal.Listen(control_queue_/*not sure it's ok to use this queue, normally use "dispatcher" which is passed in*/,
        delegate(Property<DeckTraversalModel>.EventArgs args) {
            if (args.Old != null) {
                // ((InstructorModel)(((PresentationModel)(args.Old)).Owner.Role)).Changed["AcceptingStudentSubmissions"].Remove(this.m_AcceptingSSubsChangedDispatcher.Dispatcher);
                args.Old.Deck.Changed["Dirty"].Remove(this.m_DirtyDispatcher.Dispatcher);
            }

            DeckTraversalModel dtm = null;
            using (Synchronizer.Lock(this.presenter_model_.Workspace.CurrentDeckTraversal.SyncRoot)) {
                dtm = this.presenter_model_.Workspace.CurrentDeckTraversal.Value;
            }
            if (dtm != null) {
                using (Synchronizer.Lock(dtm.SyncRoot)) {
                    using (Synchronizer.Lock(dtm.Deck.SyncRoot)) {
                        //((InstructorModel)(this.m_Model.Workspace.CurrentPresentation.Value.Owner.Role)).Changed["AcceptingStudentSubmissions"].Add(this.m_AcceptingSSubsChangedDispatcher.Dispatcher);
                        //this.HandleAcceptingSSubsChanged(null, null);
                        dtm.Deck.Changed["Dirty"].Add(this.m_DirtyDispatcher.Dispatcher);

                    }
                }
            }
            
            this.OnDirtyChanged(null, null);
        });


                }
            }

            // Create the event listener for the stylus's drawing attributes.
            // It's not used until the Stylus property changes.
            stylus_color_listener_ = new EventQueue.PropertyEventDispatcher(this.SlideDisplay.EventQueue,
                new PropertyEventHandler(this.HandleStylusColorChanged));

            ///handles adding textboxes
            this.MouseClick += new MouseEventHandler(MainSlideViewer_MouseClick);

            //Set up fullscreen listener so that when in fullscreen we can set it up so mouse down events send us out of fullscreen
            this.m_PrimaryMonitorFullScreenListener = new EventQueue.PropertyEventDispatcher(this.m_EventQueue,
                new PropertyEventHandler(this.HandleFullScreenChanged));
            this.presenter_model_.ViewerState.Changed["PrimaryMonitorFullScreen"].Add(this.m_PrimaryMonitorFullScreenListener.Dispatcher);
            this.m_PrimaryMonitorFullScreenListener.Dispatcher(this, null);

        }


        protected void Initialize() {
            using (Synchronizer.Lock(this.presenter_model_.Workspace.CurrentDeckTraversal.SyncRoot)) {
                using (Synchronizer.Lock(this.presenter_model_.Workspace.CurrentDeckTraversal.Value.SyncRoot)) {
                    using (Synchronizer.Lock(this.presenter_model_.Workspace.CurrentDeckTraversal.Value.Deck.SyncRoot)) {
                        //((InstructorModel)(this.m_Model.Workspace.CurrentPresentation.Value.Owner.Role)).Changed["AcceptingStudentSubmissions"].Add(this.m_AcceptingSSubsChangedDispatcher.Dispatcher);
                        //this.HandleAcceptingSSubsChanged(null, null);
                        this.presenter_model_.Workspace.CurrentDeckTraversal.Value.Deck.Changed["Dirty"].Add(this.m_DirtyDispatcher.Dispatcher);

                    }
                }
            }
            this.OnDirtyChanged(null, null);
            
        }

        #endregion


        //If we are in fullscreen mode, any mouse down event should send us out of fullscreen
        private void HandleFullScreenChanged(object sender, PropertyEventArgs e) {

            using (Synchronizer.Lock(this.presenter_model_.ViewerState.SyncRoot)) {
                using (Synchronizer.Lock(this.presenter_model_.Participant.SyncRoot)) {
                    if (this.presenter_model_.Participant.Role != null) {
                        using (Synchronizer.Lock(this.presenter_model_.Participant.Role.SyncRoot)) {
                            if (this.presenter_model_.Participant.Role is PublicModel &&
                                this.presenter_model_.ViewerState.PrimaryMonitorFullScreen) {
                                this.MouseDown += new MouseEventHandler(FullScreen_MouseDown);
                            }
                            else {
                                this.MouseDown -= new MouseEventHandler(FullScreen_MouseDown);
                            }
                        }
                    }
                }
            }
        }
        

        

        void FullScreen_MouseDown(object sender, EventArgs e)
            {
            using (Synchronizer.Lock(this.presenter_model_.ViewerState.SyncRoot))
                {
                this.presenter_model_.ViewerState.PrimaryMonitorFullScreen = false;
                }
            }

        protected void OnDirtyChanged(object sender, PropertyEventArgs args) {
            using (Synchronizer.Lock(this.presenter_model_.Workspace.CurrentDeckTraversal.SyncRoot)) {
                if (this.presenter_model_.Workspace.CurrentDeckTraversal.Value != null) {
                    using (Synchronizer.Lock(this.presenter_model_.Workspace.CurrentDeckTraversal.Value.SyncRoot)) {
                        using (Synchronizer.Lock(this.presenter_model_.Workspace.CurrentDeckTraversal.Value.Deck.SyncRoot)) {
                            if (this.presenter_model_.Workspace.CurrentDeckTraversal.Value.Deck.Dirty) {
                                this.MouseDown -= new MouseEventHandler(Dirty_MouseDown);
                            }
                            else {
                                this.MouseDown += new MouseEventHandler(Dirty_MouseDown);
                            }
                        }
                    }
                }
            }
        }
        void Dirty_MouseDown(object sender, EventArgs e) {
            using (Synchronizer.Lock(this.presenter_model_.Workspace.CurrentDeckTraversal.SyncRoot)) {
                if (this.presenter_model_.Workspace.CurrentDeckTraversal != null &&
                    this.presenter_model_.Workspace.CurrentDeckTraversal.Value != null) {
                    using (Synchronizer.Lock(this.presenter_model_.Workspace.CurrentDeckTraversal.Value.SyncRoot)) {
                        using (Synchronizer.Lock(this.presenter_model_.Workspace.CurrentDeckTraversal.Value.Deck.SyncRoot)) {
                            this.presenter_model_.Workspace.CurrentDeckTraversal.Value.Deck.Dirty = true;
                        }
                    }
                }
            }
            this.MouseDown -= new MouseEventHandler(Dirty_MouseDown);

        }
            
        
        /*protected void HandleDeckChanged(object sender, PropertyEventArgs ea) {
            ea.Property.o
            this.presenter_model_.Workspace.CurrentDeckTraversal.Value.Deck.Changed["Dirty"].Add(this.m_DirtyListener.Dispatcher);
            this.m_DirtyListener.Dispatcher(this, null);
            this.MouseDown += new MouseEventHandler(Dirty_MouseDown);
        }*/

        #region Submission Status
        void submission_status_timer__Tick(object sender, EventArgs e) {
            ///Remove the label from the slide.
            StatusLabel.RemoveLabelForSlide(this.Slide);
            submission_status_timer_.Stop();
        }
        void submission_failed_timer__Tick(object sender, EventArgs e) {
            using (Synchronizer.Lock(SubmissionStatusModel.GetInstance().SyncRoot)) {
                SubmissionStatusModel.GetInstance().SubmissionStatus = SubmissionStatusModel.Status.Failed;
            }
            submission_failed_timer_.Stop();
        }
        #endregion



        #region TextItBoxes By Julia Schwarz

        /// <summary>
        /// when the user clicks on the slide in text edit mode,
        /// he should get a TextItBox that he can write in. This method
        /// creates a textsheet which the textsheethelper later adds a 
        /// textitbox to that the user can type in.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void MainSlideViewer_MouseClick(object sender, MouseEventArgs e) {
            // return if Slide is null.
            if (this.Slide == null)
                return;

            ///get info about our stylus
            StylusModel stylus;
            using (Synchronizer.Lock(presenter_model_.SyncRoot)) {
                stylus = presenter_model_.Stylus;
            }

            if (stylus is TextStylusModel) {
                ///to get the right locations for the transformed sheet,
                ///we need to inversely transform the mouse location.
                Matrix transform;
                using (Synchronizer.Lock(m_SlideDisplay.SyncRoot)) {
                    transform = m_SlideDisplay.PixelTransform.Clone();
                }
                ///scaling according to the transform
                float scale_x = transform.Elements[0];
                float scale_y = transform.Elements[3];
                transform.Invert();

                ///the inverse scalings
                float i_scale_x = transform.Elements[0];
                float i_scale_y = transform.Elements[3];

                AddTextSheet(e.Location, i_scale_x, i_scale_y);
            }
        }

        /// <summary>
        /// Adds a text sheet to the annotationsheets at a point relative
        /// to the user's view
        /// </summary>
        /// <param name="p">where the text sheet will be located</param>
        /// <param name="scale_x"></param>
        /// <param name="scale_y"></param>
        /// <param name="i_scale_x"></param>
        /// <param name="i_scale_y"></param>
        private void AddTextSheet(Point p, float i_scale_x, float i_scale_y) {
            TextSheetModel t;

            using (Synchronizer.Lock(current_stylus_.SyncRoot)) {
                ///create a textsheetmodel so that it will appear where we clicked in a good size
                t = new TextSheetModel(Guid.NewGuid(), "", SheetDisposition.All, true, true, current_stylus_.DrawingAttributes.Color,
                    new Rectangle(new Point(Math.Min(p.X, SlideWidth - 10 - TextItBox.default_size.Width), Math.Min(p.Y, SlideHeight - TextItBox.default_size.Height)), TextItBox.default_size),
                    i_scale_x, i_scale_y);
            }
            //Remove the sheets that just represent empty text boxes.
            for (int i = Slide.AnnotationSheets.Count - 1; i >= 0; i--) {
                bool remove = false;
                SheetModel s = Slide.AnnotationSheets[i];
                using (Synchronizer.Lock(s.SyncRoot)) {
                    if (s is TextSheetModel) {
                        using (Synchronizer.Lock(((TextSheetModel)(s)).Text)) {
                            if (((TextSheetModel)(s)).Text == "") {
                                remove = true;
                            }
                        }
                    }
                }
                if (remove) {
                    using (Synchronizer.Lock(Slide.SyncRoot)) {
                        using (Synchronizer.Lock(Slide.AnnotationSheets)) {
                            Slide.AnnotationSheets.Remove(s);
                        }
                    }
                }
            }


            ///add the sheet to our annotationsheets.
            ///
            using (Synchronizer.Lock(this.Slide.SyncRoot)) {
                    this.Slide.AnnotationSheets.Add(t);
            }
        }

        /// <summary>
        /// when we're sending a student submission, we need to save all of the information in our
        /// TextItBoxes to our textsheets
        /// </summary>
        /// <param name="o"></param>
        /// <param name="args"></param>
        private void HandleStudentSubmission(object o, PropertyEventArgs args) {
            for (int i = 0; i < this.Controls.Count; i++) {
                if (this.Controls[i] is TextItBox) {
                    TextItBox t = (TextItBox)this.Controls[i];
                    t.SaveToSheet();
                }
            }
        }

        private void SubmissionStatusChanged(object o, PropertyEventArgs args) {
            UW.ClassroomPresenter.Model.Network.SubmissionStatusModel.Status status;
            using (Synchronizer.Lock(SubmissionStatusModel.GetInstance().SyncRoot)) {
                status = SubmissionStatusModel.GetInstance().SubmissionStatus;
            }
            if (status == SubmissionStatusModel.Status.NotReceived) {
                StatusLabel.ChangeLabelForSlide(this.Slide, Strings.SendSubmission);
                submission_status_timer_.Stop();
                submission_failed_timer_.Start();
            } else if (status == SubmissionStatusModel.Status.Received) {
                StatusLabel.ChangeLabelForSlide(this.Slide, Strings.SubmissionReceived);
                submission_status_timer_.Start();
                submission_failed_timer_.Stop();
            } else if (status == SubmissionStatusModel.Status.Failed) {
                StatusLabel.ChangeLabelForSlide(this.Slide, Strings.SubmissionFailed);
                submission_status_timer_.Start();
            }
        }

        /// <summary>
        /// If the stylus's color changes and we are in text mode, make
        /// sure to change the font of the current textbox realtime.
        /// </summary>
        /// <param name="o"></param>
        /// <param name="args"></param>
        private void HandleStylusColorChanged(object o, PropertyEventArgs args) {
            if (!editing_text_ || this.Slide == null || current_stylus_ == null) { return; }
            if (current_stylus_ is TextStylusModel && this.ActiveControl is TextItBox) {
                TextItBox t = (TextItBox)this.ActiveControl;
                Color color;
                using (Synchronizer.Lock(current_stylus_.SyncRoot))
                    color = current_stylus_.DrawingAttributes.Color;
                t.ChangeColor(color);
            }
        }

        /// <summary>
        /// if the font of the stylus changes, make sure to change this
        /// in our current textbox that's focused, if one is in focus.
        /// </summary>
        /// <param name="o"></param>
        /// <param name="args"></param>
        private void HandleStylusFontChanged(object o, PropertyEventArgs args) {
            if (!editing_text_ || this.Slide == null || current_stylus_ == null) { return; }
            
            if (this.ActiveControl is TextItBox) {
                TextItBox t = (TextItBox) this.ActiveControl;
                Font f;
                using (Synchronizer.Lock(TextStylusModel.GetInstance().SyncRoot)) {
                    f = TextStylusModel.GetInstance().Font;
                }
                t.Font = f;
            }
        }
        /// <summary>
        /// when the stylus changed into a text editing stylus, we need to be able to add and edit textboxes
        /// this method enables us to do this.
        /// </summary>
        private void HandleStylusChanged(object o, PropertyEventArgs e) {
            using (Synchronizer.Lock(presenter_model_.SyncRoot))
                this.SetCurrentStylus(presenter_model_.Stylus);
        }

        /// <summary>
        /// Updates the current stylus, registers and unregisters event listeners, and 
        /// creates the edit textboxes when the current stylus is a TextStylusModel.
        /// </summary>
        private void SetCurrentStylus(StylusModel stylus) {
            // Unregister any existing event listeners.
            if (current_stylus_ != null)
                current_stylus_.Changed["DrawingAttributes"].Remove(stylus_color_listener_.Dispatcher);
            current_stylus_ = stylus;
            if( current_stylus_ != null && stylus_color_listener_ != null )
                current_stylus_.Changed["DrawingAttributes"].Add( stylus_color_listener_.Dispatcher );

            ///only change text settings if our page can be edited.
            ///and if we actually have a slide :P
            ///
            if (!editable_page_ || this.Slide == null) {
                return;
            }
            if (current_text_helper_ != null) {
                current_text_helper_.Dispose();
            }
            if (current_image_helper_ != null) {
                current_image_helper_.Dispose();
            }
            
            // Change UI if we are dealing with Images (change cursor, add collection helper
            if (stylus is ImageStylusModel) {
                this.Cursor = Cursors.Default;
                current_image_helper_ = new ImageItCollectionHelper(this, this.Slide);
            } else if (stylus is EraserStylusModel) {
                // Use an eraser cursor when the eraser is selected.
                this.Cursor = Cursors.Cross;
            } else {
                this.Cursor = Cursors.Default;
            }
            
            // Change UI if we are going to be adding text.
            if (stylus is TextStylusModel) {
                ///we are in text editing mode, so we need to change the stylus and editing mode
                editing_text_ = true;
                this.Cursor = Cursors.IBeam;

                //Remove the sheets that just represent empty text boxes.
                for (int i = Slide.AnnotationSheets.Count - 1; i >= 0; i--) {
                    bool remove = false;
                    SheetModel s = Slide.AnnotationSheets[i];
                    using (Synchronizer.Lock(s.SyncRoot)) {
                        if (s is TextSheetModel) {
                            using (Synchronizer.Lock(((TextSheetModel)(s)).Text)) {
                                if (((TextSheetModel)(s)).Text == "") {
                                    remove = true;
                                }
                            }
                        }
                    }

                    if (remove) {
                        using (Synchronizer.Lock(Slide.SyncRoot)) {
                            using (Synchronizer.Lock(Slide.AnnotationSheets)) {
                                Slide.AnnotationSheets.Remove(s);
                            }
                        }
                    }
                }
                current_text_helper_ = new TextItBoxCollectionHelper(this, this.Slide);
                TextStylusModel.GetInstance().Changed["Font"].Add(this.HandleStylusFontChanged);
            } else {
                editing_text_ = false;
            } 
        }

        /// <summary>
        /// if we are changing slides, we need to refresh the textboxes to reflect which slide we're on.
        /// </summary>
        public override SlideModel Slide {
            set {
                ///only do stuff if we're changing 
                if (value != base.Slide) {
                    base.Slide = value;

                    ///only refresh our controls if our page is editable (i.e. it is not the slidepreview :P)
                    if (editable_page_ && value != null) {
                        if (current_text_helper_ != null) {
                            current_text_helper_.Dispose();
                        }
                        if (current_image_helper_ != null) {
                            current_image_helper_.Dispose();
                        }

                        if( current_stylus_ is TextStylusModel ) {
                            this.Cursor = Cursors.IBeam;
                            current_text_helper_ = new TextItBoxCollectionHelper(this, value);
                        } else if( current_stylus_ is ImageStylusModel ) {
                            current_image_helper_ = new ImageItCollectionHelper(this, value);
                        }
                    }
                }

            }
        }


        /// <summary>
        /// This helper class helps us manage the UI side of adding textsheedmodels. Whenever
        /// a textsheetmodel is added, this class handles the UI of actually adding the textitbox.
        /// This is nice because it fits with the general UI-model connection of the rest of the code.
        /// whenever a textsheetmodel is deleted, this class deletes the textitbox attached. 
        /// Also, when we dispose this class, it kills the UI side of the textsheets without actually killing
        /// the sheets (this lets us toggle from pen to text mode).
        /// It also lets us nicely refresh all of the textitboxes when we change slides.
        /// </summary>
        private class TextItBoxCollectionHelper : PropertyCollectionHelper {
            private readonly MainSlideViewer slide_viewer_;

            /// <summary>
            /// constructor sets slide viewer variable so we can access the UI
            /// </summary>
            /// <param name="slide_viewer"></param>
            /// <param name="slide"></param>
            public TextItBoxCollectionHelper(MainSlideViewer slide_viewer, SlideModel slide) : base(slide_viewer.control_queue_, slide, "AnnotationSheets"){
                slide_viewer_ = slide_viewer;
                base.Initialize();
            }

            /// <summary>
            /// When we create a TextSheetModel, make sure to add the appropriate TextItBox,
            /// assuming the textsheetmodel is editable.
            /// </summary>
            /// <param name="index"></param>
            /// <param name="member"></param>
            /// <returns></returns>
            protected override object  SetUpMember(int index, object member)
            {
                if(member is TextSheetModel){
                    ///we are creating a textsheetmodel
                    TextSheetModel t_sheet = (TextSheetModel) member;

                    ///Initialize the Textitbox
                    bool is_editable;
                    
                    using (Synchronizer.Lock(t_sheet)) {
                         is_editable= t_sheet.IsEditable;
                    }
                    if (is_editable) {
                        TextItBox t;
                        ///the textsheet is editable, so we need to add a control.
                        t = new TextItBox(t_sheet, slide_viewer_);
                        slide_viewer_.SuspendLayout();
                        slide_viewer_.Controls.Add(t);
                        t.BringToFront();
                        slide_viewer_.ResumeLayout();
                        t.Focus();
                        return t;
                    }
                }
                return null;
            }

            /// <summary>
            /// If our member is connected to a textitbox (that is, if tag != null)
            /// we need to delete the corresponding textitbox
            /// </summary>
            /// <param name="index"></param>
            /// <param name="member"></param>
            /// <param name="tag"></param>
            protected override void  TearDownMember(int index, object member, object tag)
            {
                if(tag != null){
                    TextItBox t_box = (TextItBox) tag;
 	                slide_viewer_.Controls.Remove(t_box);
                    t_box.Dispose();
                }
                            
            }

            /// <summary>
            /// Destroys all the controls connected to the textsheetmodels without killing the sheets.
            /// </summary>
            /// <param name="disposing"></param>
            protected override void Dispose(bool disposing) {
                for (int i = slide_viewer_.Controls.Count - 1; i >= 0; i--) {
                    if (slide_viewer_.Controls[i] is TextItBox) {
                        TextItBox t_box = (TextItBox)slide_viewer_.Controls[i];
                        slide_viewer_.Controls.RemoveAt(i);
                        t_box.Dispose();
                        
                    }
                }
                slide_viewer_.Focus();
                base.Dispose(disposing);

            }

            
        }

        /// <summary>
        /// Handles the UI control of image sheets. Whenever an image sheet is added, and ImageIt
        /// box will appear to represent the image, if we are in image editing mode.
        /// </summary>
        private class ImageItCollectionHelper: PropertyCollectionHelper{
            private readonly MainSlideViewer slide_viewer_;

            /// <summary>
            /// constructor sets slide viewer variable so we can access the UI
            /// </summary>
            /// <param name="slide_viewer"></param>
            /// <param name="slide"></param>
            public ImageItCollectionHelper(MainSlideViewer slide_viewer, SlideModel slide) : base(slide_viewer.control_queue_, slide, "AnnotationSheets"){
                slide_viewer_ = slide_viewer;
                base.Initialize();
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="index"></param>
            /// <param name="member"></param>
            /// <returns></returns>
            protected override object  SetUpMember(int index, object member)
            {
                if(member is ImageSheetModel){
                    ///we are creating an imagesheetmodel
                    ImageSheetModel i_sheet = (ImageSheetModel) member;
                    if (i_sheet.IsEditable) {
                        ///the image sheet is editable, so we must add the control
                        ///Initialize the ImageIt
                        ImageIt i = new ImageIt(i_sheet, slide_viewer_);

                        slide_viewer_.SuspendLayout();
                        slide_viewer_.Controls.Add(i);
                        i.BringToFront();
                        slide_viewer_.ResumeLayout();
                        i.Focus();
                        return i;
                    }
                }
                return null;

            }

            /// <summary>
            /// If our member is connected to a textitbox (that is, if tag != null)
            /// we need to delete the corresponding textitbox
            /// </summary>
            /// <param name="index"></param>
            /// <param name="member"></param>
            /// <param name="tag"></param>
            protected override void  TearDownMember(int index, object member, object tag)
            {
                if(tag != null){
                    Control c = (Control) tag;
 	                slide_viewer_.Controls.Remove(c);
                    c.Dispose();
                }
                            
            }

            /// <summary>
            /// Destroys all the controls connected to the textsheetmodels without killing the sheets.
            /// </summary>
            /// <param name="disposing"></param>
            protected override void Dispose(bool disposing) {
                for (int i = slide_viewer_.Controls.Count - 1; i >= 0; i--) {
                    if (slide_viewer_.Controls[i] is ImageIt) {
                        slide_viewer_.Controls[i].Dispose();//NOTE: this is different from the dispose method in the TextItBoxCollectionHelper - make sure there aren't problems because of this.  Also, I added the focus thing below to match the text case, but haven't tested it. - Natalie
                    }
                }
                slide_viewer_.Focus();
                base.Dispose(disposing);

            }

            

        }
        #endregion

        protected override void OnPaint( PaintEventArgs args ) {
            // Determine the active area of the control
            Rectangle clip;
            using( Synchronizer.Lock( this.SlideDisplay.SyncRoot) ) {
                clip = new Rectangle( this.SlideDisplay.Bounds.Location, this.SlideDisplay.Bounds.Size );
            }
            // Expand to include the border
            clip.Inflate( BORDER_WIDTH, BORDER_WIDTH );

            // Set the region
            this.Region = new Region( clip );

            // Paint
            base.OnPaint( args );
        }

        protected override void OnPaintBackground(PaintEventArgs args) {
            Graphics g = args.Graphics;
            RoleModel role = null;
            LinkedDeckTraversalModel.NavigationSelector studentNavigationType = LinkedDeckTraversalModel.NavigationSelector.Full;
            if (PresenterModel.TheInstance != null) {
                using (Synchronizer.Lock(PresenterModel.TheInstance.Participant.SyncRoot)) {
                    role = PresenterModel.TheInstance.Participant.Role;
                }                
                using (Synchronizer.Lock(PresenterModel.TheInstance.ViewerState.SyncRoot)) {
                    studentNavigationType = PresenterModel.TheInstance.ViewerState.StudentNavigationType;
                }
            }
           

            using(Synchronizer.Lock(this.SlideDisplay.SyncRoot)) {
                Rectangle slide = this.SlideDisplay.Bounds;

                // Draw the 3D border just outside the slide bounds.
                Rectangle border = slide;
                border.Inflate(BORDER_WIDTH, BORDER_WIDTH);
                ControlPaint.DrawBorder3D(g, border, Border3DStyle.Sunken, Border3DSide.All);


                if(this.Slide == null) {
                    using(Font font = new Font(this.Font.FontFamily, this.Font.Size * 6)) {
                        using(Brush brush = new SolidBrush(Color.FromArgb(this.BackColor.A / 6, SystemColors.WindowText))) {
                            SizeF size = g.MeasureString(NO_SLIDE_MESSAGE, font, slide.Width);
                            PointF location = new PointF(
                                Math.Max(0, slide.X + ((slide.Width - size.Width) / 2)),
                                Math.Max(0, slide.Y + ((slide.Height - size.Height) / 2)));
                            g.DrawString(NO_SLIDE_MESSAGE, font, brush, new RectangleF(location, size));
                        }
                    }
                }

                else {
                    using(Synchronizer.Lock(this.Slide.SyncRoot)) {
                             // Paint the unvisited slides.
                            if ( role!=null && role is StudentModel && (!this.Slide.Visited) && studentNavigationType == LinkedDeckTraversalModel.NavigationSelector.Visited)
                            using(Font font = new Font(this.Font.FontFamily, this.Font.Size * 6)) {
                                using(Brush brush = new SolidBrush(Color.FromArgb(this.BackColor.A / 6, SystemColors.WindowText))) {
                                    SizeF size = g.MeasureString(UNVISITED_SLIDE_MESSAGE, font, slide.Width);
                                    PointF location = new PointF(
                                        Math.Max(0, slide.X + ((slide.Width - size.Width) / 2)),
                                        Math.Max(0, slide.Y + ((slide.Height - size.Height) / 2)));
                                    g.DrawString(UNVISITED_SLIDE_MESSAGE, font, brush, new RectangleF(location, size));
                                }
                            }
                            // Paint the slide's background to be something prettier than the default control color.
                            //Retrieve the slide's background color
                            else {
                                Color c;
                                using(Synchronizer.Lock(this.Slide.SyncRoot)) {
                                    c = this.Slide.BackgroundColor;
                                }
                                if(c.IsEmpty) {
                                    //Slide color undefined, use deck's default color
                                    c = this.DefaultDeckBGColor;
                                }
                            
                                SolidBrush sb = new SolidBrush(c);
                                //Some really cool brushes that we might consider offering...
                                //HatchBrush hb = new HatchBrush(HatchStyle.DiagonalCross, Color.Gold, Color.Purple);
                                //LinearGradientBrush lgb = new LinearGradientBrush(slide, Color.Purple, Color.Gold, 0f, true);

                                g.FillRectangle(sb, slide);
                                //g.FillRectangle(SystemBrushes.Window, slide);
                                BackgroundTemplate backgroundTemplate;
                                float slidezoom = 1f;
                                using (Synchronizer.Lock(this.Slide.SyncRoot)) {
                                    backgroundTemplate = this.Slide.BackgroundTemplate;
                                    slidezoom = this.Slide.Zoom;
                                }
                                if (backgroundTemplate == null) {
                                    backgroundTemplate = this.DefaultDeckBGTemplate;
                                }
                                if (backgroundTemplate != null) {
                                    using (BackgroundTemplateRenderer render = new BackgroundTemplateRenderer(backgroundTemplate)) {
                                        render.Zoom = slidezoom;
                                        render.DrawAll(g, slide);
                                    }
                                }
                        }
                    }

                    // Paint the default background in the region outside the slide.
                    Region inverse = g.Clip;
                    inverse.Exclude(border);
                    g.Clip = inverse;
                    base.OnPaintBackground(args);
                }
            }
        }

        protected override void OnSizeChanged(EventArgs e) {
            base.OnSizeChanged(e);

            // Don't update the bounds if the control is not loaded (ie., if we're in the process of disposing).
            if(this.IsHandleCreated)
                this.LayoutSlide();
        }

        protected override void LayoutSlide() {
            Rectangle bounds = new Rectangle(Point.Empty, this.Size);

            bounds.Inflate(-BORDER_WIDTH, -BORDER_WIDTH);

            Rectangle slide;
            float zoom;
            if(this.Slide != null) {
                using(Synchronizer.Lock(this.Slide.SyncRoot)) {
                    slide = this.Slide.Bounds;
                    zoom = this.Slide.Zoom;
                }
            } else {
                slide = this.ClientRectangle;
                zoom = 1f;
            }

            Matrix pixel, ink;
            this.SlideDisplay.FitSlideToBounds(DockStyle.Fill, bounds, zoom, ref slide, out pixel, out ink);

            using(Synchronizer.Lock(this.SlideDisplay.SyncRoot)) {
                this.SlideDisplay.Bounds = slide;
                this.SlideDisplay.PixelTransform = pixel;
                this.SlideDisplay.InkTransform = ink;
            }
            this.SlideWidth = slide.Size.Width;
            this.SlideHeight = slide.Size.Height;
            // Now that the slide's bounds have changed, we must repaint the slide.
            this.Invalidate();
        }

        protected override void Dispose(bool disposing) {
            ///only remove event listeners if we are using presenter, not deckbuilder
            ///pre: presenter_model_ != null <=> we are using presenter
            if (!this.disposed_ && disposing && presenter_model_ != null) {
                ///dispose all of our event listeners
                this.SetCurrentStylus(null);
                presenter_model_.Changed["Stylus"].Remove(this.stylus_listener_.Dispatcher);
                presenter_model_.ViewerState.Changed["StudentSubmissionSignal"].Remove(this.student_submissions_signal_listener_.Dispatcher);
                TextStylusModel.GetInstance().Changed["Font"].Remove(this.HandleStylusFontChanged);
            }

            this.disposed_ = true;

            base.Dispose(disposing);
        }
    }

    // TODO: Either make this more versatile for different "linking" methods, or move this functionality directly to MainSlideViewer.
    public class DeckTraversalModelAdapter : IDisposable {
        private readonly EventQueue m_EventQueue;
        private readonly EventQueue.PropertyEventDispatcher m_CurrentEntryChangedDispatcher;
        private readonly EventQueue.PropertyEventDispatcher m_DeckBGColorChangedDispatcher;
        private readonly EventQueue.PropertyEventDispatcher m_DeckBGTemplateChangedDispatcher;

        private readonly DeckTraversalModel m_DeckTraversal;
        private readonly IAdaptee m_Viewer;
        private bool m_Disposed;

        public DeckTraversalModelAdapter(EventQueue dispatcher, IAdaptee viewer, DeckTraversalModel traversal) {
            this.m_EventQueue = dispatcher;
            this.m_DeckTraversal = traversal;
            this.m_Viewer = viewer;

            this.m_CurrentEntryChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandleCurrentEntryChanged));
            this.m_DeckBGColorChangedDispatcher = new EventQueue.PropertyEventDispatcher( this.m_EventQueue, new PropertyEventHandler( this.HandleDeckBGColorChanged ) );
            this.m_DeckBGTemplateChangedDispatcher = new EventQueue.PropertyEventDispatcher(this.m_EventQueue, new PropertyEventHandler(this.HandleDeckBGTemplateChanged));
            this.m_DeckTraversal.Changed["Current"].Add( this.m_CurrentEntryChangedDispatcher.Dispatcher );
            using (Synchronizer.Lock(this.m_DeckTraversal.Deck.SyncRoot)) {
                this.m_DeckTraversal.Deck.Changed["DeckBackgroundColor"].Add( this.m_DeckBGColorChangedDispatcher.Dispatcher );
                this.m_DeckTraversal.Deck.Changed["DeckBackgroundTemplate"].Add(this.m_DeckBGTemplateChangedDispatcher.Dispatcher);
            }

            this.m_CurrentEntryChangedDispatcher.Dispatcher(this.m_DeckTraversal, null);
            this.m_CurrentEntryChangedDispatcher.Dispatcher(this.m_DeckTraversal, null);
        }

        ~DeckTraversalModelAdapter() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);

        }

        protected virtual void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            if(disposing) {
                this.m_DeckTraversal.Changed["Current"].Remove(this.m_CurrentEntryChangedDispatcher.Dispatcher);
                this.m_DeckTraversal.Deck.Changed["DeckBackgroundColor"].Remove(this.m_DeckBGColorChangedDispatcher.Dispatcher);
                this.m_DeckTraversal.Deck.Changed["DeckBackgroundTemplate"].Remove(this.m_DeckBGTemplateChangedDispatcher.Dispatcher);
            }
            this.m_Disposed = true;
        }

        private void HandleCurrentEntryChanged(object sender, PropertyEventArgs args) {
            using(Synchronizer.Lock(this.m_DeckTraversal.SyncRoot)) {
                if(this.m_DeckTraversal.Current == null) {
                    this.m_Viewer.Slide = null;
                }

                else {
                    this.m_Viewer.Slide = this.m_DeckTraversal.Current.Slide;
                }
            }
        }

        private void HandleDeckBGColorChanged(object sender, PropertyEventArgs args) {
            using (Synchronizer.Lock(this.m_DeckTraversal.SyncRoot)) {
                using (Synchronizer.Lock(this.m_DeckTraversal.Deck.SyncRoot)) {
                    this.m_Viewer.DefaultDeckBGColor = this.m_DeckTraversal.Deck.DeckBackgroundColor;
                }
            }
        }

        private void HandleDeckBGTemplateChanged(object sender, PropertyEventArgs args) {
            using (Synchronizer.Lock(this.m_DeckTraversal.SyncRoot)) {
                using (Synchronizer.Lock(this.m_DeckTraversal.Deck.SyncRoot)) {
                    this.m_Viewer.DefaultDeckBGTemplate = this.m_DeckTraversal.Deck.DeckBackgroundTemplate;                }
            }
        }

        public interface IAdaptee {
            SlideModel Slide { set; }
            Color DefaultDeckBGColor { set; }
            BackgroundTemplate DefaultDeckBGTemplate { set;}
        }
    }

    public class WorkspaceModelAdapter : IDisposable {
        private readonly EventQueue m_EventQueue;
        private readonly IDisposable m_CurrentDeckTraversalChangedDispatcher;
        private readonly EventQueue.PropertyEventDispatcher m_DeckBGColorChangedDispatcher;
        private readonly EventQueue.PropertyEventDispatcher m_DeckBGTemplateChangedDispatcher;

        private readonly PresenterModel m_Model;
        private readonly DeckTraversalModelAdapter.IAdaptee m_Viewer;

        private DeckTraversalModel m_CurrentDeckTraversal;
        private DeckTraversalModelAdapter m_Adapter;
        private bool m_Disposed;

        public WorkspaceModelAdapter(EventQueue dispatcher, DeckTraversalModelAdapter.IAdaptee viewer, PresenterModel model) {
            this.m_EventQueue = dispatcher;
            this.m_Model = model;
            this.m_Viewer = viewer;
            this.m_DeckBGColorChangedDispatcher = new EventQueue.PropertyEventDispatcher( dispatcher, new PropertyEventHandler( this.HandleBackgroundColorChanged ) );
            this.m_DeckBGTemplateChangedDispatcher = new EventQueue.PropertyEventDispatcher(dispatcher, new PropertyEventHandler(this.HandleBackgroundTemplateChanged));
            
            this.m_CurrentDeckTraversalChangedDispatcher =
                this.m_Model.Workspace.CurrentDeckTraversal.ListenAndInitialize(dispatcher,
                delegate(Property<DeckTraversalModel>.EventArgs args) {
                    if (this.CurrentDeckTraversal != null)
                        using (Synchronizer.Lock(this.CurrentDeckTraversal.SyncRoot))
                        using (Synchronizer.Lock(this.CurrentDeckTraversal.Deck.SyncRoot)){
                            this.CurrentDeckTraversal.Deck.Changed["DeckBackgroundColor"].Remove( this.m_DeckBGColorChangedDispatcher.Dispatcher );
                            this.CurrentDeckTraversal.Deck.Changed["DeckBackgroundTemplate"].Remove(this.m_DeckBGTemplateChangedDispatcher.Dispatcher);
                        }

                    this.CurrentDeckTraversal = args.New;

                    if (this.CurrentDeckTraversal != null) {
                        using (Synchronizer.Lock(this.CurrentDeckTraversal.SyncRoot))
                        using (Synchronizer.Lock(this.CurrentDeckTraversal.Deck.SyncRoot)) {
                            this.CurrentDeckTraversal.Deck.Changed["DeckBackgroundColor"].Add(this.m_DeckBGColorChangedDispatcher.Dispatcher);
                            this.CurrentDeckTraversal.Deck.Changed["DeckBackgroundTemplate"].Add(this.m_DeckBGTemplateChangedDispatcher.Dispatcher);
                        }
                        this.HandleBackgroundColorChanged(this, null);
                        this.HandleBackgroundTemplateChanged(this, null);
                    }
                });
        }

        ~WorkspaceModelAdapter() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);

        }

        protected virtual void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            if(disposing) {
                this.m_CurrentDeckTraversalChangedDispatcher.Dispose();

                // Note: this duplicates code in the CurrentDeckTraversal setter.
                if(this.m_CurrentDeckTraversal != null) {
                    Debug.Assert(this.m_Adapter != null);
                    this.m_Adapter.Dispose();
                    this.m_Adapter = null;
                    using (Synchronizer.Lock(this.CurrentDeckTraversal.SyncRoot)) {
                        using (Synchronizer.Lock(this.CurrentDeckTraversal.Deck.SyncRoot)) {
                            this.CurrentDeckTraversal.Deck.Changed["DeckBackgroundColor"].Remove(new PropertyEventHandler(this.HandleBackgroundColorChanged));
                            this.CurrentDeckTraversal.Deck.Changed["DeckBackgroundTemplate"].Remove(new PropertyEventHandler(this.HandleBackgroundTemplateChanged));
                        }
                    }
                }
            }
            this.m_Disposed = true;
        }

        private DeckTraversalModel CurrentDeckTraversal {
            get { return this.m_CurrentDeckTraversal; }
            set {
                // Note: this duplicates code in Dispose.
                if(this.m_CurrentDeckTraversal != null) {
                    Debug.Assert(this.m_Adapter != null);
                    this.m_Adapter.Dispose();
                    this.m_Adapter = null;
                }

                this.m_CurrentDeckTraversal = value;

                if(this.m_CurrentDeckTraversal != null) {
                    Debug.Assert(this.m_Adapter == null);
                    this.m_Adapter = new DeckTraversalModelAdapter(this.m_EventQueue, this.m_Viewer, this.m_CurrentDeckTraversal);
                } else {
                    this.m_Viewer.Slide = null;
                }
            }
        }

        private void HandleBackgroundColorChanged(object sender, PropertyEventArgs args) {
            DeckTraversalModel dtm = null;
            using (this.m_Model.Workspace.Lock()) {
                dtm = ~this.m_Model.Workspace.CurrentDeckTraversal;
            }
            DeckModel dm = null;
            using (Synchronizer.Lock(dtm.SyncRoot)) {
                dm = dtm.Deck;
            }
            using (Synchronizer.Lock(dm.SyncRoot)) {
                this.m_Viewer.DefaultDeckBGColor = dm.DeckBackgroundColor;
            }
        }

        private void HandleBackgroundTemplateChanged(object sender, PropertyEventArgs args)
        {
            DeckTraversalModel dtm = null;
            using (this.m_Model.Workspace.Lock())
            {
                dtm = ~this.m_Model.Workspace.CurrentDeckTraversal;
            }
            DeckModel dm = null;
            using (Synchronizer.Lock(dtm.SyncRoot))
            {
                dm = dtm.Deck;
            }
            using (Synchronizer.Lock(dm.SyncRoot))
            {
                this.m_Viewer.DefaultDeckBGTemplate = dm.DeckBackgroundTemplate;
            }
        }
    }
}
