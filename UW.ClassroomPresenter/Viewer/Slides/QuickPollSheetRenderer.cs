using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;
using System.Windows.Forms;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using UW.ClassroomPresenter.Model.Viewer;

namespace UW.ClassroomPresenter.Viewer.Slides {
    class QuickPollSheetRenderer : SheetRenderer {
        #region Private Members

        /// <summary>
        /// The sheet that this renderer draws
        /// </summary>
        private readonly QuickPollSheetModel m_Sheet;
        /// <summary>
        /// Variable to keep track of when this object is disposed
        /// </summary>
        private bool m_Disposed;        
        /// <summary>
        /// Listener for the SlideDisplay.PixelTransform property.
        /// </summary>
        private readonly EventQueue.PropertyEventDispatcher repaint_dispatcher_;

        #endregion

        #region Constructors

        /// <summary>
        /// Construct the renderer
        /// </summary>
        /// <param name="display">The SlideDisplayModel</param>
        /// <param name="sheet">The QuickPollSheetModel</param>
        public QuickPollSheetRenderer( SlideDisplayModel display, QuickPollSheetModel sheet) : base(display, sheet) {
            this.m_Sheet = sheet;
            repaint_dispatcher_ = new EventQueue.PropertyEventDispatcher(SlideDisplay.EventQueue, this.Repaint);

            /// Add event listeners
            this.m_Sheet.QuickPoll.Changed["Updated"].Add(new PropertyEventHandler(this.repaint_dispatcher_.Dispatcher));
            this.SlideDisplay.Changed["Slide"].Add(new PropertyEventHandler(this.repaint_dispatcher_.Dispatcher));
        }

        #endregion

        #region Disposing

        /// <summary>
        /// 
        /// </summary>
        /// <param name="disposing"></param>
        protected override void Dispose(bool disposing) {
            if(this.m_Disposed) return;
            try {
                if(disposing) {
                    // TODO CMPRINCE: Change these also...
                    this.m_Sheet.QuickPoll.Changed["Updated"].Remove(new PropertyEventHandler(this.repaint_dispatcher_.Dispatcher));
                    this.SlideDisplay.Changed["Slide"].Remove(new PropertyEventHandler(this.repaint_dispatcher_.Dispatcher));
                    this.SlideDisplay.Invalidate();
                }
            } finally {
                base.Dispose(disposing);
            }
            this.m_Disposed = true;
        }
        #endregion

        #region Painting

        /// <summary>
        /// Specifies the different types of ways to display the quick poll results
        /// </summary>
        public enum ResultDisplayType {
            Text = 0,
            Histogram = 1
        };

        /// <summary>
        /// The currently selected way to display quickpoll results
        /// </summary>
        public ResultDisplayType DisplayType = ResultDisplayType.Histogram;

        /// <summary>
        /// A helper member that specifies the brushes to use for the various columns
        /// </summary>
        private System.Drawing.Brush[] columnBrushes = new System.Drawing.Brush[] { Brushes.Orange, 
                                                                            Brushes.Cyan, 
                                                                            Brushes.Magenta, 
                                                                            Brushes.Yellow, 
                                                                            Brushes.GreenYellow };

        /// <summary>
        /// Paints the text of the textsheetmodel onto the slide using the DrawString method.
        /// </summary>
        /// <param name="args"></param>
        public override void Paint(PaintEventArgs args) {            
            Graphics g = args.Graphics;
            int startX, startY, endX, endY, width, height;
            RectangleF finalLocation;
            Font writingFont = new Font( FontFamily.GenericSansSerif, 12.0f );
            StringFormat format = new StringFormat( StringFormat.GenericDefault );
            format.Alignment = StringAlignment.Center;
            format.LineAlignment = StringAlignment.Center;

            // Sanity Check
            using( Synchronizer.Lock( this.m_Sheet.SyncRoot ) ) {
                if( this.m_Sheet.QuickPoll == null ) {
                    return;
                }
            }

            // Get everything we need from the SlideDisplay
            using( Synchronizer.Lock( this.SlideDisplay.SyncRoot ) ) {
                ///transform what we will paint so that it will fit nicely into our slideview
                g.Transform = this.SlideDisplay.PixelTransform;
                if (this.SlideDisplay.Slide != null) {
                    using (Synchronizer.Lock(this.SlideDisplay.Slide.SyncRoot)) {
                        startX = (int)(this.SlideDisplay.Slide.Bounds.Width * 0.5f);
                        endX = (int)(this.SlideDisplay.Slide.Bounds.Width * 0.95f);
                        startY = (int)(this.SlideDisplay.Slide.Bounds.Height * 0.3f);
                        endY = (int)(this.SlideDisplay.Slide.Bounds.Height * 0.85f);
                        width = endX - startX;
                        height = endY - startY;
                        finalLocation = new RectangleF(startX, startY, width, height);
                    }
                }
                else  {
                    startX = (int)(this.SlideDisplay.Bounds.Width * 0.5f);
                    endX = (int)(this.SlideDisplay.Bounds.Width * 0.95f);
                    startY = (int)(this.SlideDisplay.Bounds.Height * 0.3f);
                    endY = (int)(this.SlideDisplay.Bounds.Height * 0.85f);
                    width = endX - startX;
                    height = endY - startY;
                    finalLocation = new RectangleF(startX, startY, width, height);
                }
            }

            // Get the vote data
            System.Collections.ArrayList names;
            System.Collections.Hashtable table;
            using( Synchronizer.Lock( this.m_Sheet.SyncRoot ) ) {
                using( Synchronizer.Lock( this.m_Sheet.QuickPoll.SyncRoot ) ) {
                    names = QuickPollModel.GetVoteStringsFromStyle( this.m_Sheet.QuickPoll.PollStyle );
                    table = this.m_Sheet.QuickPoll.GetVoteCount();
                }
            }

            // Draw the outline
            g.FillRectangle( Brushes.White, startX - 1, startY - 1, width, height );
            g.DrawRectangle( Pens.Black, startX - 1, startY - 1, width, height );

            switch( this.DisplayType ) {
                case ResultDisplayType.Text: // Text
                    // Get the results string
                    string result = "";
                    foreach( string s in table.Keys ) {
                        result += QuickPollModel.GetLocalizedQuickPollString(s) + " - " + table[s].ToString() + System.Environment.NewLine;
                    }
                    g.DrawString( result, writingFont, Brushes.Black, finalLocation, format );
                    break;
                case ResultDisplayType.Histogram:                    
                    // Count the total number of results
                    int totalVotes = 0;
                    foreach( string s in table.Keys ) {
                        totalVotes += (int)table[s];
                    }

                    // Draw the choices
                    float columnWidth = width / names.Count;
                    int columnStartY = (int)((height * 0.9f) + startY);
                    int columnTotalHeight = columnStartY - startY;
                    for( int i = 0; i < names.Count; i++ ) {
                        // Draw the column
                        int columnHeight = 0;
                        if( totalVotes != 0 ) {
                            columnHeight = (int)Math.Round( (float)columnTotalHeight * ((int)table[names[i]] / (float)totalVotes) );
                        }
                        if( columnHeight == 0 ) {
                            columnHeight = 1;
                        }
                        g.FillRectangle( this.columnBrushes[i], (int)(i * columnWidth) + startX, columnStartY - columnHeight, (int)columnWidth, columnHeight );

                        // Draw the label
                        g.DrawString( QuickPollModel.GetLocalizedQuickPollString( names[i].ToString() ),
                                      writingFont,
                                      Brushes.Black,
                                      new RectangleF( (i * columnWidth) + startX, columnStartY, columnWidth, endY - columnStartY ),
                                      format );

                        // Draw the number
                        string percentage = String.Format( "{0:0%}", (totalVotes == 0) ? 0 : (float)(((int)table[names[i]] / (float)totalVotes)) );
                        int numberHeight = (endY - columnStartY) * 2;
                        RectangleF numberRectangle = new RectangleF( (i * columnWidth) + startX,
                                                                     (numberHeight > columnHeight) ? (columnStartY - columnHeight - numberHeight) : (columnStartY - columnHeight),
                                                                     columnWidth,
                                                                     numberHeight );
                        string numberString = percentage + System.Environment.NewLine + "(" + table[names[i]].ToString() + ")";
                        g.DrawString( numberString, writingFont, Brushes.Black, numberRectangle, format );
                    }
                    break;
            }
        }
        #endregion

        #region Event Listening

        // TODO CMPRINCE: Not sure if we need this
        private void Repaint(object sender, PropertyEventArgs args)
        {
            this.SlideDisplay.Invalidate();
        }

        #endregion
    }
}
