// $Id: PropertiesForm.cs 1698 2008-08-12 00:00:06Z lamphare $

using System;
using System.Drawing;
using System.Windows.Forms;
using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Viewer;
using UW.ClassroomPresenter.Model.Presentation;

namespace UW.ClassroomPresenter.Viewer.SSOrganizer {
    /// <summary>
    /// A dialog box that allow the instructor to organize a set of student
    /// submissions so that it is easier to keep track of good submissions.
    /// </summary>
    public class SSOrganizerForm : Form {
        #region Member Properties

        /// <summary>
        /// Keep track of the entire presentation
        /// </summary>
        private PresenterModel model = null;

        /// <summary>
        /// Keep track of the current student submissions deck
        /// </summary>
        private DeckModel deckModel = null;

        #endregion

        #region Construction

        /// <summary>
        /// Constructs the dialog form
        /// </summary>
        public SSOrganizerForm(PresenterModel m) {
            // Save the model
            this.model = m;

            // Get the student submission deck
            if(this.model != null) {
                using (Synchronizer.Lock(this.model.SyncRoot)) {
                    if (this.model.Workspace != null) {
                        using (this.model.Workspace.Lock()) {
                            if (~this.model.Workspace.CurrentPresentation != null) {
                                using (Synchronizer.Lock((~this.model.Workspace.CurrentPresentation).SyncRoot)) {
                                    deckModel = (~this.model.Workspace.CurrentPresentation).GetStudentSubmissionDeck();
                                }
                            }
                        }
                    }
                }
            }

            // Setup the display of the form
            // Suspend the layout
            this.SuspendLayout();

            this.AutoScaleBaseSize = new Size(5, 13);
            this.ClientSize = new System.Drawing.Size(564, 342);
            this.Font = ViewerStateModel.FormFont;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimumSize = new Size(220, 220);
            this.Name = "SSOrganizerForm";
            // TODO CMPRINCE: Need to localize this string
            this.Text = "Student Submission Organizer";

            // Add the child controls
            SSOrganizerMainPanel mainPanel = new SSOrganizerMainPanel(this.deckModel, new Point(0, 0), new Size(564, 304), 0);
            this.Controls.Add(mainPanel);
            this.Controls.Add(new SSOrganizerOKButton(this.deckModel, mainPanel, new Point(182, 310), 1));
            this.Controls.Add(new SSOrganizerCancelButton(new Point(300, 310), 2));

            this.ResumeLayout();
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Quit early if there is no student submission deck
        /// </summary>
        /// <param name="e">The event arguments</param>
        protected override void OnLoad(EventArgs e) {
            base.OnLoad(e);

            // Close the dialog of there is no submission deck
            if (this.deckModel == null) {
                MessageBox.Show("No Student Submission Deck");
                this.DialogResult = DialogResult.Cancel;
            }
        }

        #endregion

        #region Buttons

        /// <summary>
        /// The OK Button for the Organizer, clicking this will actually 
        /// change the ordering of the slides.
        /// </summary>
        public class SSOrganizerOKButton : Button {
            // Need a reference to where the result will be held
            private SSOrganizerMainPanel panel;

            private DeckModel deck;

            /// <summary>
            /// Construct the button
            /// </summary>
            /// <param name="location">The location for the button</param>
            /// <param name="tabIndex">The tab index for the button</param>
            public SSOrganizerOKButton(DeckModel deck, SSOrganizerMainPanel panel, Point location, int tabIndex) {
                // Need to assign the result reference
                this.panel = panel;
                this.deck = deck;

                this.DialogResult = System.Windows.Forms.DialogResult.OK;
                this.FlatStyle = FlatStyle.System;
                this.Font = ViewerStateModel.StringFont;
                this.Location = location;
                this.Name = "organizerOKButton";
                this.TabIndex = tabIndex;
                this.Anchor = AnchorStyles.Bottom;
                this.Text = Strings.OK;
            }

            /// <summary>
            /// When clicked we remove all the controls and readd them in the
            /// correct order
            /// </summary>
            /// <param name="e">The event arguments</param>
            protected override void OnClick(EventArgs e) {
                // Iterate through each control in reverse order and reorder
                // appropriately
                for(int i=panel.Controls.Count-1; i>=0; i--) {
                    SSOrganizerEntry entry = (SSOrganizerEntry)this.panel.Controls[i];
                    // Remove the entry from the table of contents
                    using (Synchronizer.Lock(deck.SyncRoot)) {
                        if (deck.TableOfContents != null) {
                            using (Synchronizer.Lock(deck.TableOfContents.SyncRoot)) {
                                this.deck.TableOfContents.Entries.Remove(entry.GetEntry());
                            }
                        }
                    }

                    // Add the entry to the table of contents
                    using (Synchronizer.Lock(deck.SyncRoot)) {
                        if (deck.TableOfContents != null) {
                            using (Synchronizer.Lock(deck.TableOfContents.SyncRoot)) {
                                this.deck.TableOfContents.Entries.Insert(0, entry.GetEntry());
                            }
                        }
                    }
                }

                base.OnClick(e);
            }
        }

        /// <summary>
        /// The Cancel Button for the Properties Form
        /// </summary>
        public class SSOrganizerCancelButton : Button {
            /// <summary>
            /// Constructor for the cancel button
            /// </summary>
            /// <param name="location">The location of the button</param>
            /// <param name="tabIndex">The tab index of the button</param>
            public SSOrganizerCancelButton(Point location, int tabIndex) {
                // Basically, just cancel if pressed
                this.DialogResult = System.Windows.Forms.DialogResult.Cancel;
                this.FlatStyle = FlatStyle.System;
                this.Font = ViewerStateModel.StringFont;
                this.Location = location;
                this.Name = "organizerCancelButton";
                this.TabIndex = tabIndex;
                this.Anchor = AnchorStyles.Bottom;
                this.Text = Strings.Cancel;
            }
        }

        #endregion

        #region MainPanel

        /// <summary>
        /// The panel that is where the user will interact to organize the 
        /// student submissions
        /// </summary>
        public class SSOrganizerMainPanel : Panel {
            /// <summary>
            /// Constructor for the panel that will be used to organize the
            /// various student submissions
            /// </summary>
            /// <param name="deck">The DeckModel whose slides we will be 
            /// organizing</param>
            /// <param name="location">The location of this control in the 
            /// parent</param>
            /// <param name="size">The size of this control</param>
            /// <param name="tabIndex">The tab index of this control</param>
            public SSOrganizerMainPanel(DeckModel deck, Point location,
                Size size, int tabIndex) {
                // Stop layout
                this.SuspendLayout();

                // Set all the parameters of the object
                this.Font = ViewerStateModel.StringFont;
                this.BorderStyle = BorderStyle.Fixed3D;
                this.Location = location;
                this.Name = "tabControl";
                this.Size = size;
                this.TabIndex = tabIndex;
                this.Anchor = AnchorStyles.Left | 
                    AnchorStyles.Top | 
                    AnchorStyles.Right | 
                    AnchorStyles.Bottom;

                // Add each slide as a control
                Color deckColor = Color.Transparent;
                int i = 0;
                if (deck != null) {
                    using( Synchronizer.Lock(deck.SyncRoot)) {
                        // Get the deck color
                        deckColor = deck.DeckBackgroundColor;
                        if(deck.TableOfContents != null) {
                            using( Synchronizer.Lock(deck.TableOfContents.SyncRoot) ) {
                                // Create a child control for each submission
                                foreach (TableOfContentsModel.Entry e in 
                                    deck.TableOfContents.Entries) {
                                    this.Controls.Add(new SSOrganizerEntry(i++, 
                                        e, deckColor, new PointF(0.5f, 0.5f), 
                                        new SizeF(1.0f, 1.0f)));
                                }
                            }
                        }
                    }
                }

                // TODO CMPRINCE: Remove this in the final version
                // Bring the last entry to the front
                if (this.Controls.Count > 0) {
                    this.Controls[this.Controls.Count - 1].BringToFront();
                }

                // Start the layout again
                this.ResumeLayout();
            }
        }

        #endregion

        #region Entries

        /// <summary>
        /// Helper class that is used to keep track of all the entries
        /// and has an associated UI for each.
        /// </summary>
        public class SSOrganizerEntry : Panel {
            #region Member Properties

            /// <summary>
            /// The relative location of this control
            /// </summary>
            private PointF location;

            /// <summary>
            /// The relative size of this control
            /// </summary>
            private SizeF size;

            /// <summary>
            /// The entry that this control represents
            /// </summary>
            public TableOfContentsModel.Entry entry;

            /// <summary>
            /// A cached image of the slide that this entry points to
            /// </summary>
            public Bitmap cachedImage;

            #endregion

            #region Construction

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="i">The index of this entry</param>
            /// <param name="e">The entry this control represents</param>
            /// <param name="deckColor">The background color for the deck
            /// containing this entry</param>
            /// <param name="location">The location for this control</param>
            /// <param name="size">The size of this control</param>
            public SSOrganizerEntry(int i, TableOfContentsModel.Entry e,
                Color deckColor, PointF location, SizeF size) {
                this.entry = e;
                this.location = location;
                this.size = size;

                this.SuspendLayout();

                // Create the background image
                this.cachedImage = this.CacheSlideImage(deckColor);
                this.BackgroundImage = this.cachedImage;
                this.BackgroundImageLayout = ImageLayout.Stretch;
                this.BackColor = Color.White;
                this.BorderStyle = BorderStyle.FixedSingle;
                this.ClientSize = new Size(200, 150);
                this.Location = new Point(i * this.Size.Width, 0);
                this.Anchor = AnchorStyles.Left | AnchorStyles.Top;

                this.ResumeLayout();
            }

            /// <summary>
            /// Create a cache of the image to use for rendering in the tool
            /// </summary>
            /// <returns>Returns a bitmap of the slide image</returns>
            private Bitmap CacheSlideImage(Color deckColor) {
                // Get the correct background color either from the 
                // slide background color or from the deck background color
                Color background = Color.Transparent;
                using (Synchronizer.Lock(this.entry.Slide.SyncRoot)) {
                    if (this.entry.Slide.BackgroundColor != Color.Empty) {
                        background = this.entry.Slide.BackgroundColor;
                    }
                    else if (deckColor != Color.Empty) {
                        background = deckColor;
                    }
                }

                // Create an image that will represent the slide
                return UW.ClassroomPresenter.Decks.PPTDeckIO.DrawSlide(this.entry,
                    null, background,
                    SheetDisposition.Background |
                    SheetDisposition.Public |
                    SheetDisposition.Student |
                    SheetDisposition.All |
                    SheetDisposition.Instructor);
            }

            #endregion

            #region Accessors

            /// <summary>
            /// Get the table of contents entry
            /// </summary>
            /// <returns>Returns the entry</returns>
            public TableOfContentsModel.Entry GetEntry() {
                return this.entry;
            }

            #endregion

            #region Cleanup

            /// <summary>
            /// Need to handle cleaning up the slide image
            /// </summary>
            /// <param name="disposing">True if called dispose 
            /// explicitly</param>
            protected override void Dispose(bool disposing) {
                // Need to dispose of the image we created
                if (this.cachedImage != null) {
                    this.cachedImage.Dispose();
                    this.cachedImage = null;
                }
                base.Dispose(disposing);
            }

            #endregion
        }

        #endregion
    }
}
