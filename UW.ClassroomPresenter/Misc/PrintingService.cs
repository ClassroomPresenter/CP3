using System;
using Microsoft.Win32;
using System.IO;
using UW.ClassroomPresenter.Model.Viewer;
using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Presentation;
using System.Drawing;
using System.Drawing.Printing;
using UW.ClassroomPresenter.Decks;
using UW.ClassroomPresenter.Model.Background;
using UW.ClassroomPresenter.Viewer.Background;

namespace UW.ClassroomPresenter.Misc {
    public class PrintingService : IDisposable {
        #region Private Members

        /// <summary>
        /// True once the object is disposed, false otherwise.
        /// </summary>
        private bool m_bDisposed = false;

        /// <summary>
        /// Private reference to the <see cref="ViewerStateModel"/> to modify
        /// </summary>
        private ViewerStateModel m_ViewerState = null;

        private Font sheet_number_font = new Font( FontFamily.GenericSansSerif, 12.0f );
        private int m_SheetNumber = 1;
        private int m_PageNumber = -1;
        private int m_PagesPerSheet = 6;

        #endregion

        #region Initialization

        /// <summary>
        /// Constructs a service to keep track of changes to the model objects and store them in the
        /// registry
        /// </summary>
        /// <param name="toSave">The model class with values we want to save in the registry</param>
        public PrintingService( ViewerStateModel viewer ) {
            this.m_ViewerState = viewer;

            using( Synchronizer.Lock( this.m_ViewerState.SyncRoot ) ) {
                viewer.Document.PrintPage += new PrintPageEventHandler( this.OnPrintPage );
            }

            // Add even listeners
            this.m_ViewerState.Changed["SlidesPerPage"].Add( new PropertyEventHandler( this.OnPageSetupChanged ) );

            // Get the initial Pages Per Sheet setting
            using (Synchronizer.Lock(this.m_ViewerState.SyncRoot)) {
                this.m_PagesPerSheet = this.m_ViewerState.SlidesPerPage;
            }
            
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Event handler that's invoked there is a change in logging enabled or path
        /// </summary>
        /// <param name="sender">The object that was changed</param>
        /// <param name="args">Information about the changed property</param>
        private void OnPageSetupChanged( object sender, PropertyEventArgs args ) {
            using( Synchronizer.Lock( this.m_ViewerState.SyncRoot ) ) {
                this.m_PagesPerSheet = this.m_ViewerState.SlidesPerPage;
            }
        }

        /// <summary>
        /// Print out a page to the printer
        /// </summary>
        /// <param name="sender">The object sending the message</param>
        /// <param name="e">The printing parameters</param>
        private void OnPrintPage( object sender, PrintPageEventArgs e ) {
            DeckTraversalModel traversal = null;
            using( Synchronizer.Lock( this.m_ViewerState.SyncRoot ) )
                traversal = this.m_ViewerState.PrintableDeck;

            // Determine the print range
            int first_page = 0;
            int last_page = 0;
            if( e.PageSettings.PrinterSettings.PrintRange == PrintRange.SomePages ) {
                first_page = e.PageSettings.PrinterSettings.FromPage;
                last_page = e.PageSettings.PrinterSettings.ToPage;
            } else {
                first_page = 1;
                using( Synchronizer.Lock( traversal.SyncRoot ) )
                    using( Synchronizer.Lock( traversal.Deck.SyncRoot ) )
                        last_page = traversal.Deck.Slides.Count;
            }

            // Determine if we need to print this page
            if( this.m_PageNumber == -1 ) {
                this.m_PageNumber = first_page;
            } else if( this.m_PageNumber > last_page ) {
                this.m_PageNumber = -1;
                this.m_SheetNumber = 1;
                e.HasMorePages = false;
                return;
            }

            // Iterate through the pages
            for( int i = 0; i<this.m_PagesPerSheet && this.m_PageNumber<=last_page; i++ ) {
                int PageNumber = this.m_PageNumber;

                // Print the current page
                Rectangle bounds = this.GetPageBounds( e, this.m_PagesPerSheet, i, 0.1f );
                this.DrawSlide3( traversal, PageNumber - 1, bounds, e.Graphics );

                // Increment the page count
                this.m_PageNumber++;
            }

            // Print the Sheet Number centered at the bottom of the Page
            SizeF sheet_number_size = e.Graphics.MeasureString( this.m_SheetNumber.ToString(), sheet_number_font );
            e.Graphics.DrawString( this.m_SheetNumber.ToString(), 
                                   sheet_number_font, Brushes.Black, 
                                   e.MarginBounds.X+(e.MarginBounds.Width/2)-(sheet_number_size.Width/2),
                                   e.MarginBounds.Y+e.MarginBounds.Height-sheet_number_size.Height );


            // Determine if we are done printing
            if( this.m_PageNumber > last_page ) {
                this.m_PageNumber = -1;
                this.m_SheetNumber = 1;
                e.HasMorePages = false;
            } else {
                this.m_SheetNumber++;
                e.HasMorePages = true;
            }
        }

        #endregion

        #region Partitioning

        /// <summary>
        /// Determines the rectangle to draw the final page in
        /// </summary>
        /// <param name="e">The printer arguments</param>
        /// <param name="PagesPerSheet">The number of pages per sheet</param>
        /// <param name="PageOffset">The offset of this page</param>
        /// <param name="Gutter">Percentage of gutter around all sides, e.g. 0.1 would be a 10% gutter</param>
        /// <returns>The final rectangle to draw the slide in</returns>
        private Rectangle GetPageBounds( PrintPageEventArgs e, int PagesPerSheet, int PageOffset, float Gutter ) {
            // Get the cell boundaries
            Rectangle bounds = GetCellBounds( e.PageSettings.Landscape, PagesPerSheet, PageOffset, e.MarginBounds );

            // Determine the final bounds with gutter
            int WidthBorder = (int)((float)bounds.Width * Gutter);
            int HeightBorder = (int)((float)bounds.Height * Gutter);
            Rectangle full_bounds =  new Rectangle( bounds.X + WidthBorder, 
                                                     bounds.Y + HeightBorder, 
                                                     bounds.Width - (2 * WidthBorder), 
                                                     bounds.Height - (2 * HeightBorder) );
            
            // Inscribe the best 4:3 rectangle centered in this region
            Rectangle final_bounds;
            if( (float)full_bounds.Width / (float)full_bounds.Height < 4.0f / 3.0f ) {
                float new_height = 3.0f * (float)full_bounds.Width / 4.0f;
                float new_Y_offset = (full_bounds.Height - new_height) / 2.0f;
                final_bounds = new Rectangle( full_bounds.X,
                                              (int)(full_bounds.Y + new_Y_offset),
                                              full_bounds.Width,
                                              (int)new_height );
            } else if( (float)full_bounds.Width / (float)full_bounds.Height < 4.0f / 3.0f ) {
                float new_width = 4.0f * (float)full_bounds.Height / 3.0f;
                float new_X_offset = (full_bounds.Width - new_width) / 2.0f;
                final_bounds = new Rectangle( (int)(full_bounds.X + new_X_offset),
                                              full_bounds.Y,
                                              (int)new_width,
                                              full_bounds.Height );

            } else
                final_bounds = full_bounds;

            return final_bounds;
        }

        private Rectangle GetCellBounds( bool Landscape, int PagesPerSheet, int PageOffset, Rectangle MarginBounds ) {
            // Determine the number of rows and columns
            int rows = 1;
            int cols = 1;
            switch( PagesPerSheet ) {
                case 1:
                    if(Landscape) { rows = 1; cols = 1; } else { rows = 1; cols = 1; };
                    break;
                case 2:
                    if(Landscape) { rows = 1; cols = 2; } else { rows = 2; cols = 1; };
                    break;
                case 4:
                    if(Landscape) { rows = 2; cols = 2; } else { rows = 2; cols = 2; };
                    break;
                default:
                    if(Landscape) { rows = 2; cols = 3; } else { rows = 3; cols = 2; };
                    break;
            }

            // Determine the size of each row and column
            int CellWidth = MarginBounds.Width / cols;
            int CellHeight = MarginBounds.Height / rows;

            // Determine the position of the given row and column
            int CellRowNumber = PageOffset / cols;
            int CellColumnNumber = PageOffset % cols;
            int CellXPos = CellColumnNumber * CellWidth;
            int CellYPos = CellRowNumber * CellHeight;

            return new Rectangle( CellXPos + MarginBounds.X, CellYPos + MarginBounds.Y, CellWidth, CellHeight );
        }

        #endregion

        #region Drawing

        // Draws the slide border
        private void DrawSlideBorder( int index, System.Drawing.Rectangle rect, System.Drawing.Graphics buffer ) {
            buffer.DrawRectangle( Pens.Black, rect );
        }

        /// <summary>
        /// Draw the slide for printing. 
        /// Note: This code first prints to an image and then to the page to allow for transparency and anti-aliasing
        /// </summary>
        /// <param name="traversal">The deck traversal to draw from</param>
        /// <param name="index">The slide index in the deck to draw</param>
        /// <param name="displayBounds">The bounds to draw the slide in</param>
        /// <param name="g">The graphics context to draw onto</param>
        private void DrawSlide3( DeckTraversalModel traversal, int index, System.Drawing.Rectangle displayBounds, System.Drawing.Graphics g ) {
            using( Synchronizer.Lock( traversal.SyncRoot ) ) {
                using( Synchronizer.Lock( traversal.Deck.SyncRoot ) ) {
                    using( Synchronizer.Lock( traversal.Deck.TableOfContents.SyncRoot ) ) {
                        TableOfContentsModel.Entry currentEntry = traversal.Deck.TableOfContents.Entries[index];

                        // Get the background color and background template
                        Color background = Color.Transparent;
                        BackgroundTemplate template = null;
                        using( Synchronizer.Lock( currentEntry.Slide.SyncRoot ) ) {
                            if (currentEntry.Slide.BackgroundTemplate != null) {
                                template = currentEntry.Slide.BackgroundTemplate;
                            }
                            else if (traversal.Deck.DeckBackgroundTemplate != null) {
                                template = traversal.Deck.DeckBackgroundTemplate;
                            }
                            if( currentEntry.Slide.BackgroundColor != Color.Empty ) {
                                background = currentEntry.Slide.BackgroundColor;
                            } else if( traversal.Deck.DeckBackgroundColor != Color.Empty ) {
                                background = traversal.Deck.DeckBackgroundColor;
                            }
                            
                        }

                        Bitmap toExport = PPTDeckIO.DrawSlide( currentEntry, template, background, SheetDisposition.Background | SheetDisposition.Public | SheetDisposition.Student | SheetDisposition.All );

                        Rectangle newRect = FillUpRectangle( displayBounds, new Rectangle( 0, 0, toExport.Width, toExport.Height ) );
                        this.DrawSlideBorder( index + 1, newRect, g );
                        g.DrawImage( toExport, newRect );
                        toExport.Dispose();
                    }
                }
            }
        }

        /// <summary>
        /// Inscribe the src rectangle into the dest rectangle and center.
        /// </summary>
        /// <param name="dest"></param>
        /// <param name="src"></param>
        /// <returns></returns>
        private Rectangle FillUpRectangle( Rectangle dest, Rectangle src ) {
            Rectangle res = new Rectangle( 0, 0, 0, 0 );
            float destRatio = (float)dest.Width / (float)dest.Height;
            float srcRatio = (float)src.Width / (float)src.Height;
            if( destRatio == srcRatio ) {
                return dest;
            } else if( destRatio > srcRatio ) {
                res.Height = dest.Height;
                res.Width = (int)(dest.Height * srcRatio);
                res.Y = dest.Y;
                res.X = dest.X + ((dest.Width - res.Width) / 2);
            } else if( destRatio < srcRatio ) {
                res.Width = dest.Width;
                res.Height = (int)(dest.Width / srcRatio);
                res.X = dest.X;
                res.Y = dest.Y + ((dest.Height - res.Height) / 2);
            }
            return res;
        }

        #region Unused Printing Code
        private void DrawSlide2( DeckTraversalModel traversal, int index, System.Drawing.Rectangle displayBounds, System.Drawing.Graphics g ) {
            float width_scale = g.DpiX / 100f;
            float height_scale = g.DpiY / 100f;

            Rectangle bounds = new Rectangle( 0, 0, (int)(displayBounds.Width * 3f), (int)(displayBounds.Height * 3f) );

            // Create an image using temporary graphics and graphics buffer object
            Bitmap imageForPrinting = new Bitmap( bounds.Width, bounds.Height );
            using( Graphics graphicsImage = Graphics.FromImage( imageForPrinting ) ) {
                using( DibGraphicsBuffer dib = new DibGraphicsBuffer() ) {
                    // Create temporary screen Graphics
                    System.Windows.Forms.Form tempForm = new System.Windows.Forms.Form();
                    Graphics screenGraphics = tempForm.CreateGraphics();

                    // Create temporary Graphics from the Device Independent Bitmap
                    using( Graphics graphicsTemp = dib.RequestBuffer( screenGraphics, imageForPrinting.Width, imageForPrinting.Height ) ) {

                        // Save the old state
                        System.Drawing.Drawing2D.GraphicsState oldState;

                        using( Synchronizer.Lock( traversal.SyncRoot ) ) {
                            using( Synchronizer.Lock( traversal.Deck.SyncRoot ) ) {
                                using( Synchronizer.Lock( traversal.Deck.TableOfContents.SyncRoot ) ) {
                                    TableOfContentsModel.Entry entry = traversal.Deck.TableOfContents.Entries[index];
                                    using( Synchronizer.Lock( entry.Slide.SyncRoot ) ) {

                                        //Draw the background color
                                        //First see if there is a Slide BG, if not, try the Deck. Otherwise, use transparent.

                                        if( entry.Slide.BackgroundColor != Color.Empty ) {
                                            graphicsTemp.Clear( entry.Slide.BackgroundColor );
                                        } else if( traversal.Deck.DeckBackgroundColor != Color.Empty ) {
                                            graphicsTemp.Clear( traversal.Deck.DeckBackgroundColor );
                                        } else {
                                            graphicsTemp.Clear( Color.Transparent );
                                        }

                                        //Get the Slide content and draw it
                                        oldState = graphicsTemp.Save();
                                        Model.Presentation.SlideModel.SheetCollection sheets = entry.Slide.ContentSheets;
                                        for( int i = 0; i < sheets.Count; i++ ) {
                                            SlideDisplayModel display = new SlideDisplayModel( graphicsTemp, null );

                                            Rectangle rect = new Rectangle( 0, 0, bounds.Width, bounds.Height );
                                            Rectangle slide = new Rectangle( rect.X, rect.Y, rect.Width, rect.Height );
                                            float zoom = 1f;
                                            if( entry.Slide != null ) {
                                                slide = entry.Slide.Bounds;
                                                zoom = entry.Slide.Zoom;
                                            }

                                            System.Drawing.Drawing2D.Matrix pixel, ink;
                                            display.FitSlideToBounds( System.Windows.Forms.DockStyle.Fill, rect, zoom, ref slide, out pixel, out ink );
                                            using( Synchronizer.Lock( display.SyncRoot ) ) {
                                                display.Bounds = slide;
                                                display.PixelTransform = pixel;
                                                display.InkTransform = ink;
                                            }

                                            Viewer.Slides.SheetRenderer r = Viewer.Slides.SheetRenderer.ForStaticSheet( display, sheets[i] );
                                            r.Paint( new System.Windows.Forms.PaintEventArgs( graphicsTemp, bounds ) );
                                            r.Dispose();
                                        }

                                    }
                                }
                            }
                        }

                        //Restore the Old State
                        graphicsTemp.Restore( oldState );

                        // Use the buffer to paint onto the final image
                        dib.PaintBuffer( graphicsImage, 0, 0 );

                        // Draw this image onto the printer graphics,
                        // adjusting for printer margins
                        g.DrawImage( imageForPrinting, displayBounds );

                        //Cleanup
                        graphicsTemp.Dispose();
                        screenGraphics.Dispose();
                        tempForm.Dispose();
                        dib.Dispose();
                        graphicsImage.Dispose();
                        imageForPrinting.Dispose();
                    }
                }
            }
        }

        // TODO: Correctly Draw the Slide Annotation
        private void DrawSlide( DeckTraversalModel traversal, int index, System.Drawing.Rectangle rect, System.Drawing.Graphics buffer ) {
            // Save the old state
            System.Drawing.Drawing2D.GraphicsState oldState;

            using( Synchronizer.Lock( traversal.SyncRoot ) ) {
                using( Synchronizer.Lock( traversal.Deck.SyncRoot ) ) {
                    using( Synchronizer.Lock( traversal.Deck.TableOfContents.SyncRoot ) ) {
                        TableOfContentsModel.Entry entry = traversal.Deck.TableOfContents.Entries[index];
                        using( Synchronizer.Lock( entry.Slide.SyncRoot ) ) {
                            //Draw the background color
                            //First see if there is a Slide BG, if not, try the Deck. Otherwise, use transparent.                            
                            if( entry.Slide.BackgroundColor != Color.Empty ) {
                                buffer.FillRectangle( new System.Drawing.SolidBrush( entry.Slide.BackgroundColor ), rect );
                            } else if( traversal.Deck.DeckBackgroundColor != Color.Empty ) {
                                buffer.FillRectangle( new System.Drawing.SolidBrush( traversal.Deck.DeckBackgroundColor ), rect );
                            } else {
                                buffer.FillRectangle( new System.Drawing.SolidBrush( Color.Transparent ), rect );
                            }
                            //Draw the background Template
                            if (entry.Slide.BackgroundTemplate != null) {
                                using (BackgroundTemplateRenderer render = new BackgroundTemplateRenderer(entry.Slide.BackgroundTemplate)) {
                                    render.Zoom = entry.Slide.Zoom; 
                                    render.DrawAll(buffer, rect);
                                }
                            }

                            //Get the Slide content and draw it
                            oldState = buffer.Save();
                            Model.Presentation.SlideModel.SheetCollection sheets = entry.Slide.ContentSheets;
                            for( int i = 0; i < sheets.Count; i++ ) {
                                SlideDisplayModel display = new SlideDisplayModel( buffer, null );
                                
                                Rectangle slide = rect;
                                float zoom = 1f;
                                if( entry.Slide != null ) {
                                    slide = entry.Slide.Bounds;
                                    zoom = entry.Slide.Zoom;
                                }

                                System.Drawing.Drawing2D.Matrix pixel, ink;
                                display.FitSlideToBounds(System.Windows.Forms.DockStyle.Fill, rect, zoom, ref slide, out pixel, out ink);
                                using(Synchronizer.Lock(display.SyncRoot)) {
                                    display.Bounds = slide;
                                    display.PixelTransform = pixel;
                                    display.InkTransform = ink;
                                }

                                Viewer.Slides.SheetRenderer r = Viewer.Slides.SheetRenderer.ForStaticSheet( display, sheets[i] );                                        
                                r.Paint( new System.Windows.Forms.PaintEventArgs(buffer, rect) );
                                r.Dispose();
                            }

                            //Restore the Old State
                            buffer.Restore( oldState );
                            oldState = buffer.Save();

                            //Get the Annotation content and draw it
                            sheets = entry.Slide.AnnotationSheets;
                            for( int i = 0; i < sheets.Count; i++ ) {
                                SlideDisplayModel display = new SlideDisplayModel( buffer, null );
                                
                                Rectangle slide = rect;
                                float zoom = 1f;
                                if( entry.Slide != null ) {
                                    slide = entry.Slide.Bounds;
                                    zoom = entry.Slide.Zoom;
                                }

                                System.Drawing.Drawing2D.Matrix pixel, ink;
                                display.FitSlideToBounds(System.Windows.Forms.DockStyle.Fill, rect, zoom, ref slide, out pixel, out ink);
                                using(Synchronizer.Lock(display.SyncRoot)) {
                                    display.Bounds = slide;
                                    display.PixelTransform = pixel;
                                    display.InkTransform = ink;
                                }

                                Viewer.Slides.SheetRenderer r = Viewer.Slides.SheetRenderer.ForStaticSheet( display, sheets[i] );
                                if( r is Viewer.Slides.InkSheetRenderer ) {
                                    ((Viewer.Slides.InkSheetRenderer)r).Paint( buffer, rect );
                                }
                                else
                                    r.Paint( new System.Windows.Forms.PaintEventArgs(buffer, rect) );
                                r.Dispose();
                            }

                            //Restore the Old State
                            buffer.Restore( oldState );
/*
                            Microsoft.Ink.Renderer renderer = new Microsoft.Ink.Renderer();
                            for( int i = 0; i < sheets.Count; i++ ) {
                                SheetModel sm = sheets[i];
                                if( sm is InkSheetModel ) {
                                    InkSheetModel ism = sm as InkSheetModel;
                                    using( Synchronizer.Lock( ism.SyncRoot ) ) {
                                        foreach( Microsoft.Ink.Stroke stroke in ism.Ink.Strokes ) {
                                            renderer.Draw( buffer, stroke );
                                        }
                                    }
                                } else if( sm is TextSheetModel ) {
                                    TextSheetModel tsm = sm as TextSheetModel;
                                    using( Synchronizer.Lock( tsm.SyncRoot ) ) {
                                        buffer.DrawString( tsm.Text, tsm.Font, new SolidBrush( tsm.Color ), tsm.Bounds );
                                    }
                                } else if( sm is ImageSheetModel ) {
                                    //add any images in the AnnotationSheets
                                    ImageSheetModel image_sheet_model = (ImageSheetModel)sm;
                                    using( Synchronizer.Lock( image_sheet_model.SyncRoot ) ) {
                                         buffer.DrawImage( image_sheet_model.Image, image_sheet_model.Bounds );
                                    }
                                } else {
                                    //Unknown, skip it
                                    continue;
                                }
                            }
*/
                        }
                    }
                }
            }
        }
        #endregion

        #endregion

        #region Cleanup

        /// <summary>
        /// Dispose of this object
        /// </summary>
        public void Dispose() {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose of all event handlers that we created in the constructor
        /// </summary>
        /// <param name="bDisposing">True if we are in process of disposing using Dispose</param>
        private void Dispose( bool bDisposing ) {
            // Check to see if Dispose has already been called.
            if( !this.m_bDisposed && bDisposing ) {
                using( Synchronizer.Lock( this.m_ViewerState.SyncRoot ) ) {
                    this.m_ViewerState.Document.PrintPage -= new PrintPageEventHandler( this.OnPrintPage );
                }

                // TODO: Detach any listeners
                this.m_ViewerState.Changed["SlidesPerPage"].Remove( new PropertyEventHandler( this.OnPageSetupChanged ) );
            }
            this.m_bDisposed = true;
        }

        /// <summary>
        /// Destructs the object to ensure we do the cleanup, in case we don't call Dispose.
        /// </summary>
        ~PrintingService() {
            this.Dispose(false);
        }

        #endregion
    }
}
