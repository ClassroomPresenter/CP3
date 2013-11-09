// $Id: PPTDeckIO.cs 2230 2013-09-27 17:32:07Z fred $

using System;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;
using Core = Microsoft.Office.Core;
using PPTLibrary;
using PPTPaneManagement;
using UW.ClassroomPresenter.Model.Presentation;
using System.Security.Cryptography;
using System.Threading;
using System.IO;
using System.CodeDom.Compiler;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using System.Runtime.Serialization.Formatters.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Text;
using UW.ClassroomPresenter.Model.Background;
using UW.ClassroomPresenter.Viewer.Background;

namespace UW.ClassroomPresenter.Decks {


    /// <summary>
    /// Summary description for PPTDeckIO.
    /// </summary>
    public class PPTDeckIO {

        #region OpenPPT + Helper Method

        public class PPTNotInstalledException : ApplicationException {
            public PPTNotInstalledException(string message, Exception innerException) : base(message, innerException) { }
        }
        public class PPTFileOpenException : ApplicationException {
            public PPTFileOpenException(string message, Exception innerException) : base(message, innerException) { }
        }

        public static DeckModel OpenPPT(FileInfo file) {
            return PPTDeckIO.OpenPPT(file, null, null);
        }

        //TODO: Image type needs to be more dynamically chosen...
        public static DeckModel OpenPPT(FileInfo file, BackgroundWorker worker, DoWorkEventArgs progress) {
            //Start the progress bar
            if (worker != null) {
                worker.ReportProgress(0, "  Initializing...");
            }

            //Make the default flat tree representation of the PPT
            //Try to detect if powerpoint is already running (powerpnt.exe)
            bool pptAlreadyRunning = false;
            Process[] processes = Process.GetProcesses();
            for (int i = 0; i < processes.Length; i++) {
                string currentProcess = processes[i].ProcessName.ToLower();
                if (currentProcess == "powerpnt") {
                    pptAlreadyRunning = true;
                    break;
                }
            }
            //Open PowerPoint + open file
            PowerPoint.Application pptapp = null;
            try {
                pptapp = new PowerPoint.Application();
            }
            catch (Exception e) {
                throw new PPTNotInstalledException("Failed to create PowerPoint Application.  See InnerException for details.", e);
            }

            PowerPoint._Presentation presentation;
            try {
                presentation = pptapp.Presentations.Open(file.FullName, Core.MsoTriState.msoTrue, Core.MsoTriState.msoFalse, Core.MsoTriState.msoFalse);
            }
            catch (Exception e) {
                throw new PPTFileOpenException("Failed to open PowerPoint file.  See InnerException for details.", e);
            }

            //Initialize the PPT Shape tag reader
            PPTPaneManagement.PPTPaneManager pptpm = new PPTPaneManagement.PPTPaneManager();
            //Create a new DeckModel

            Guid deckGuid = Guid.Empty;
            try {
                string g = presentation.Tags["WEBEXPORTGUID"];
                if (g == "") {
                    deckGuid = Guid.NewGuid();
                }
                else {
                    deckGuid = new Guid(g);
                }
            }
            catch {
                deckGuid = Guid.NewGuid();
            }

            DeckModel deck = new DeckModel(deckGuid, DeckDisposition.Empty, file.Name);

            //Initialize a temporary file collection that will be where slide images are exported to
            TempFileCollection tempFileCollection = new TempFileCollection();
            string dirpath = tempFileCollection.BasePath;
            if (!Directory.Exists(dirpath)) {
                Directory.CreateDirectory(dirpath);
            } else {
                Directory.Delete(dirpath, true);
                Directory.CreateDirectory(dirpath);
            }

            //Lock it
            using(Synchronizer.Lock(deck.SyncRoot)) {
                //Iterate over all slides
                for (int i = 1;  i <= presentation.Slides.Count; i++) {
                    if (progress != null && progress.Cancel)
                        break;

                    //Get the slide
                    PowerPoint._Slide currentSlide= presentation.Slides[i];

                    if (currentSlide.SlideShowTransition.Hidden == Core.MsoTriState.msoTrue)
                        continue;

                    SlideModel newSlideModel = CreateSlide(presentation.PageSetup, pptpm, deck, tempFileCollection, dirpath, currentSlide);

                    //Create a new Entry + reference SlideModel
                    TableOfContentsModel.Entry newEntry = new TableOfContentsModel.Entry(Guid.NewGuid(), deck.TableOfContents, newSlideModel);
                    //Lock the TOC
                    using(Synchronizer.Lock(deck.TableOfContents.SyncRoot)) {
                        //Add Entry to TOC
                        deck.TableOfContents.Entries.Add(newEntry);
                    }
                    //Increment the ProgressBarForm
                    if (worker != null) {
                        worker.ReportProgress((i * 100) / presentation.Slides.Count, "  Reading slide " + i + " of " + presentation.Slides.Count);
                    }
                }
            }
            //Close the presentation
            presentation.Close();
            presentation = null;
            //If PowerPoint was not open before, close PowerPoint
            if (!pptAlreadyRunning) {
                pptapp.Quit();
                pptapp = null;
            }
            GC.Collect();
            //Delete temp directory
            tempFileCollection.Delete();
            Directory.Delete(dirpath);

            if (worker != null)
                worker.ReportProgress(100, " Done!");

            //Return the deck
            if (progress != null)
                progress.Result = deck;
            return deck;
        }


        /// <summary>
        /// PowerPoint shape with additional information
        /// </summary>
        struct TaggedShape {
            public PowerPoint.Shape shape;
            public Model.Presentation.SheetDisposition disp;
            public int index;
            public bool isImage;
        }


        /// <summary>
        /// Create a slide model from a powerpoint slide
        /// </summary>
        /// <param name="pageSetup"></param>
        /// <param name="pptpm"></param>
        /// <param name="deck"></param>
        /// <param name="tempFileCollection"></param>
        /// <param name="dirpath"></param>
        /// <param name="currentSlide"></param>
        /// <returns></returns>
        private static SlideModel CreateSlide(PowerPoint.PageSetup pageSetup, PPTPaneManagement.PPTPaneManager pptpm, DeckModel deck, TempFileCollection tempFileCollection, string dirpath, PowerPoint._Slide currentSlide) {
            int slideWidth = (int)pageSetup.SlideWidth;     //Standard = 720  => 6000
            int slideHeight = (int)pageSetup.SlideHeight;   //Standard = 540  => 4500
            float emfWidth = slideWidth * 25 / 3;
            float emfHeight = slideHeight * 25 / 3;

            // Adding an empty textbox in the upper left corner of the the 
            // slide before layers are exported works around a common 
            // issue with shape positioning.  Without this, text shapes often get pushed too far
            // to the right and down due to some extra padding that PPT inserts on
            // the top and left of the exported images.  It doesn't pad the top left of an
            // empty text box, so the positioning is corrected.
            // This isn't perfect since the workaround should probably be applied to 
            // every layer with text boxes.
            currentSlide.Shapes.AddTextbox(Core.MsoTextOrientation.msoTextOrientationHorizontal, 0, 0, 1, 1);

            PowerPoint.Shapes currentShapes = currentSlide.Shapes;

            List<TaggedShape> taggedShapeList = PPTDeckIO.BuildTaggedShapeList(currentShapes, pptpm);

            //Create a new SlideModel
            SlideModel newSlideModel = new SlideModel(Guid.NewGuid(), new LocalId(), SlideDisposition.Empty, new Rectangle(0, 0, slideWidth, slideHeight));
            //Lock it
            using (Synchronizer.Lock(newSlideModel.SyncRoot)) {
                //Set the slide's title
                newSlideModel.Title = PPTDeckIO.FindSlideTitle(taggedShapeList, currentSlide);

                PPTDeckIO.MakeShapesInvisible(currentShapes);

                //Create the Background image
                //Generate a new filename
                string filename = PPTDeckIO.GenerateFilename();
                bool bitmapMode = true;
                if (bitmapMode) {
                    filename = dirpath + "\\" + filename + ".png";
                    currentSlide.Export(filename, "PNG", 0, 0);
                    
                    // Need to also export as EMF to get the size of the slide in inches
                    currentSlide.Export(filename + "_TEMP", "EMF", 0, 0);
                    tempFileCollection.AddFile( filename + "_TEMP", false );
                }
                else {
                    filename = dirpath + "\\" + filename + ".emf";
                    currentSlide.Export(filename, "EMF", 0, 0);
                }
                tempFileCollection.AddFile(filename, false);

                //Compute the MD5 of the BG
                FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
                MD5 md5Provider = new MD5CryptoServiceProvider();
                byte[] md5 = md5Provider.ComputeHash(fs);
                fs.Seek(0, SeekOrigin.Begin);
                Image image = Image.FromStream(fs);
                if (bitmapMode) {
                    image = DisassociateBitmap(image);
                }
                fs.Close();

                // Open the EMF version if we used a bitmap to get the conversion
                if( bitmapMode ) {
                    FileStream fsEMF = new FileStream( filename + "_TEMP", FileMode.Open, FileAccess.Read );
                    Image image_emf = Image.FromStream( fsEMF );
                    emfWidth = image_emf.Width;
                    emfHeight = image_emf.Height;
                    fsEMF.Close();
                    image_emf.Dispose();
                } else {
                    emfWidth = image.Width;
                    emfHeight = image.Height;
                }

                //Create the ImageSheet
                ImageSheetModel sheet = new ImageSheetModel(deck, Guid.NewGuid(), Model.Presentation.SheetDisposition.Background,
                    new Rectangle(0, 0, slideWidth, slideHeight), (ByteArray)md5, 1);
                //Add the ImageSheet to the Slide
                newSlideModel.ContentSheets.Add(sheet);
                //Add the Image+MD5 to the deck
                deck.AddSlideContent((ByteArray)md5, image);
                                
                                                // Restore visibility - this makes everything visible - a bug?
                PPTDeckIO.MakeShapesVisible(currentShapes);

                List<List<TaggedShape>> layerList = PPTDeckIO.SeparateIntoLayers(taggedShapeList);

                int startHeight = 2;
                foreach (List<TaggedShape> layer in layerList)
                    PPTDeckIO.ProcessLayer( layer, tempFileCollection, currentShapes, deck, newSlideModel, 
                                            slideWidth/emfWidth, slideHeight/emfHeight, startHeight++ );

                //Add SlideModel to the deck
                deck.InsertSlide(newSlideModel);
            }
            return newSlideModel;
        }

        /// <summary>
        /// Split a list into sublist of common disposition and picture status.  Images are assigned to separate layers
        /// </summary>
        /// <param name="taggedShapeList"></param>
        /// <returns></returns>
        private static List<List<TaggedShape>> SeparateIntoLayers(List<TaggedShape> taggedShapeList) {
            List<List<TaggedShape>> listList = new List<List<TaggedShape>>();
            if (taggedShapeList.Count == 0)
                return listList;

            List<TaggedShape> list = new List<TaggedShape>();

            for(int i = 0; i < taggedShapeList.Count; i++){
                if (i > 0 && (taggedShapeList[i - 1].isImage || taggedShapeList[i].isImage || taggedShapeList[i - 1].disp != taggedShapeList[i].disp)) {
                    listList.Add(list);
                    list = new List<TaggedShape>();
                }
                list.Add(taggedShapeList[i]);
            }
            listList.Add(list);
            return listList;
        }

        /// <summary>
        /// Remove the PPT 2007 formatting from a shape to control the image blow up on conversion
        /// </summary>
        private static void Scrub2007EffectsFromShape(PowerPoint.Shape shape) {
            //These shape types are thought to always except when some of the following properties are set
            // Note this information is based on observing a small number of cases. 
            // There are other types that sometimes except, but in some cases are important to scrub.
            if (shape.Type == Core.MsoShapeType.msoPicture ||
                shape.Type == Core.MsoShapeType.msoEmbeddedOLEObject ||
                shape.Type == Core.MsoShapeType.msoTable) return;

            try {
                if (shape.Shadow != null) {
                    shape.Shadow.Visible = Microsoft.Office.Core.MsoTriState.msoFalse;
                }
                if (shape.TextFrame2 != null) {
                    shape.TextFrame2.ThreeD.BevelTopType = Microsoft.Office.Core.MsoBevelType.msoBevelNone;
                    shape.TextFrame2.TextRange.Font.Reflection.Type = Microsoft.Office.Core.MsoReflectionType.msoReflectionTypeNone;
                }
                if (shape.TextFrame != null) {
                    shape.TextFrame.TextRange.Font.Shadow = Microsoft.Office.Core.MsoTriState.msoFalse;
                }
                //Trace.WriteLine("Scrubbed effects for shape type: " + shape.Type.ToString());
            }
            catch (Exception e) {
                Trace.WriteLine("Failed to set PPT 2007 property for shape type: " + shape.Type.ToString() + 
                    "\r\n" + e.ToString());
            }

        }
        
      



        /// <summary>
        /// Tag all the shapes with their visibility mode, index, and whether or not they are a picture
        /// </summary>
        /// <param name="shapes"></param>
        /// <param name="pptpm"></param>
        /// <returns></returns>
        private static List<TaggedShape> BuildTaggedShapeList(PowerPoint.Shapes shapes, PPTPaneManagement.PPTPaneManager pptpm) {
            List<TaggedShape> tsList = new List<TaggedShape>();
            int index = 1;

            double version;
            if (!double.TryParse(((PowerPoint.Application)shapes.Application).Version,out version)) {
                version = 0.0;
            }

            for (int i = 1; i <= shapes.Count; i++) {
                PowerPoint.Shape shape = shapes[i];

                if(version >= 12.0)
                    Scrub2007EffectsFromShape(shape);

                if( shape.Type == Microsoft.Office.Core.MsoShapeType.msoGroup && (version < 12.0)) {
                    shape.Ungroup();
                    i--;
                    continue;
                } else
                    AddTaggedShapeToList( tsList, ref index, shape, pptpm );            
            }

            return tsList;
        }

        private static void AddTaggedShapeToList( List<TaggedShape> tsList, ref int index, PowerPoint.Shape shape, PPTPaneManagement.PPTPaneManager pptpm ) {
            TaggedShape ts;
            ts.shape = shape;
            ts.index = index;
            index++;
            ts.isImage = (shape.Name.StartsWith( "Picture" ));      // A shape is an image if its called a picture!
            string[] modes = pptpm.GetModes( shape );

            if( modes.Length == 0 ) {
                ts.disp = SheetDisposition.All;
                tsList.Add( ts );
            } else {
                foreach( string mode in modes ) {
                    ts.disp = PPTDeckIO.Disposition( mode );          // Make a copy for each mode, and add to the list
                    tsList.Add( ts );
                }
            }
        }

        /// <summary>
        /// Find the title of the slide.  
        /// </summary>
        /// <param name="tsList"></param>
        /// <returns></returns>
        private static string FindSlideTitle(List<TaggedShape> tsList, PowerPoint._Slide currentSlide){
            try {
                //First look for a proper title on the slide.  Assume a slide title always has SheetDisposition.All.
                if (currentSlide.Shapes.HasTitle == Microsoft.Office.Core.MsoTriState.msoTrue &&
                    currentSlide.Shapes.Title.HasTextFrame == Microsoft.Office.Core.MsoTriState.msoTrue &&
                    currentSlide.Shapes.Title.TextFrame.HasText == Microsoft.Office.Core.MsoTriState.msoTrue) {
                    return PPTDeckIO.CleanString(currentSlide.Shapes.Title.TextFrame.TextRange.Text);
                }

                //If there is no title, take the first object with text that has SheetDispositon.All
                foreach (TaggedShape ts in tsList) {
                    if (ts.disp == SheetDisposition.All &&
                        ts.shape.HasTextFrame == Microsoft.Office.Core.MsoTriState.msoTrue &&
                        ts.shape.TextFrame.HasText == Microsoft.Office.Core.MsoTriState.msoTrue) {
                        return PPTDeckIO.CleanString(ts.shape.TextFrame.TextRange.Text);
                    }
                }
            }
            catch (Exception e) {
                Debug.WriteLine(e.ToString());
            }

            //Last resort: Set the title to "Slide N"
            return "Slide " + currentSlide.SlideIndex.ToString();
        }

        private static void MakeShapesVisible(PowerPoint.Shapes shapes) {
            foreach (PowerPoint.Shape shape in shapes)
                shape.Visible = Microsoft.Office.Core.MsoTriState.msoTrue;
        }

        private static void MakeShapesInvisible(PowerPoint.Shapes shapes) {
            foreach (PowerPoint.Shape shape in shapes)
                shape.Visible = Microsoft.Office.Core.MsoTriState.msoFalse;
        }

        /// <summary>
        /// Disassociate bitmaps from the filestream so that we can close the stream
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        private static Image DisassociateBitmap(Image image) {
            //Need to disassociate bitmaps from the filestream so that we can close the stream
            //Workaround from http://support.microsoft.com/?id=814675
            if (((Bitmap)image).PixelFormat == System.Drawing.Imaging.PixelFormat.Format8bppIndexed ||
                ((Bitmap)image).PixelFormat == System.Drawing.Imaging.PixelFormat.Format4bppIndexed ||
                ((Bitmap)image).PixelFormat == System.Drawing.Imaging.PixelFormat.Format1bppIndexed) {
                //The workaround does not support indexed bitmap formats, we convert it to something that isn't
                Bitmap orig = ((Bitmap)image).Clone(new Rectangle(0, 0, image.Width, image.Height), System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                Bitmap b = new Bitmap(image.Width, image.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                Graphics g = Graphics.FromImage(b);
                // NOTE: Need to possibly stretch image since original and new bitmaps may have different pixels per inch.
                g.DrawImage(orig, 0, 0, image.Width, image.Height);
                g.Dispose();
                orig.Dispose();
                image.Dispose();
                image = b;
            }
            else {
                Bitmap b = new Bitmap(image.Width, image.Height, image.PixelFormat);
                Graphics g = Graphics.FromImage(b);
                g.DrawImage(image, new Rectangle(0, 0, image.Width, image.Height));
                g.Dispose();
                image.Dispose();
                image = b;
            }
            return image;
        }

        /// <summary>
        /// Extract the shapes range from a list of tagged shapes
        /// </summary>
        /// <param name="layer"></param>
        /// <returns></returns>
        private static int[] BuildIntRange(List<TaggedShape> layer) {
            int[] shapeIndices = new int[layer.Count];
            for (int i = 0; i < layer.Count; i++)
                shapeIndices[i] = layer[i].index;
            return shapeIndices;
        }
 
        /// <summary>
        /// 
        /// </summary>
        /// <param name="tfc"></param>
        /// <param name="shapes"></param>
        /// <param name="range"></param>
        /// <param name="deck"></param>
        /// <param name="slide"></param>
        /// <param name="disposition"></param>
        /// <param name="emfHeight"></param>
        /// <param name="startHeight">The starting height to place this sheet at</param>
        private static void ProcessLayer( List<TaggedShape> layer, TempFileCollection tfc, PowerPoint.Shapes shapes, Model.Presentation.DeckModel deck, 
            Model.Presentation.SlideModel slide, float emfWidthRatio, float emfHeightRatio, int startHeight ) {
            if (layer.Count < 1)
                return;

            //Create the image
            int[] range = PPTDeckIO.BuildIntRange(layer);
            PowerPoint.ShapeRange sr = shapes.Range(range);

            PowerPoint.PpShapeFormat format;
            string fileExt = "";
            bool bitmapMode = layer[0].isImage;

            if (bitmapMode) {
                format = PowerPoint.PpShapeFormat.ppShapeFormatPNG;
                fileExt = "png";
            }
            else {
                format = PowerPoint.PpShapeFormat.ppShapeFormatEMF;
                fileExt = "emf";
            }

            //Generate a new filename
            string dirpath = tfc.BasePath;
            string filename = PPTDeckIO.GenerateFilename();
            filename = dirpath + "\\" + filename + "." + fileExt;
            while (File.Exists(filename)) {
                filename = PPTDeckIO.GenerateFilename();
                filename = dirpath + "\\" + filename + "." + fileExt;
            }

            sr.Export(filename, format, 0, 0,
                PowerPoint.PpExportMode.ppRelativeToSlide);

            if (bitmapMode) {
                // Starting with Office 2013, bitmaps may not be exported in the correct size.
                double version;
                if (!double.TryParse(((PowerPoint.Application)shapes.Application).Version, out version)) {
                    version = 0.0;
                }
                if (version >= 15.0) {
                    ScaleShapeImage(sr, filename);
                }
            }

            tfc.AddFile(filename, false);
            //Compute the MD5 of the BG
            FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
            MD5 md5Provider = new MD5CryptoServiceProvider();
            byte[] md5 = md5Provider.ComputeHash(fs);
            fs.Seek(0, SeekOrigin.Begin);
            Image image = Image.FromStream(fs);

            if (bitmapMode)
                image = DisassociateBitmap(image);

            fs.Close();
            //Calculate the geometry
            int xCoord = 0;
            int yCoord = 0;
            int width = 0;
            int height = 0;
            PPTDeckIO.CalculateGeometry( image, shapes, range, emfWidthRatio, emfHeightRatio, ref xCoord, ref yCoord, ref width, ref height );
            //Create the ImageSheet
            ImageSheetModel sheet = new ImageSheetModel(deck, Guid.NewGuid(), layer[0].disp,
                new Rectangle(xCoord, yCoord, width, height), (ByteArray)md5, startHeight);
            //Add the ImageSheet to the Slide
            slide.ContentSheets.Add(sheet);
            //Add the Image+MD5 to the deck
            deck.AddSlideContent((ByteArray)md5, image);
        }

        /// <summary>
        /// If necessary, resize the image to match the size of the ShapeRange.
        /// </summary>
        /// <param name="sr"></param>
        /// <param name="filename"></param>
        private static void ScaleShapeImage(PowerPoint.ShapeRange sr, string filename) {
            bool scaled = false;
            string tmpname = filename + "_RESIZED";
            using (Image img = Image.FromFile(filename)) {
                if (((int)Math.Round(sr.Width) != img.Width) ||
                    ((int)Math.Round(sr.Height) != img.Height)) {
                    using (Bitmap resized = ResizeImage(img, (int)Math.Round(sr.Width), (int)Math.Round(sr.Height))) {
                        resized.Save(tmpname);
                        scaled = true;
                    }
                }
            }

            if (scaled) {
                File.Delete(filename);
                File.Move(tmpname, filename);
            }
        }

        /// <summary>
        /// Resize the image to the specified width and height.
        /// </summary>
        /// <param name="image">The image to resize.</param>
        /// <param name="width">The width to resize to.</param>
        /// <param name="height">The height to resize to.</param>
        /// <returns>The resized image.</returns>
        public static Bitmap ResizeImage(System.Drawing.Image image, int width, int height) {
            //a holder for the result
            Bitmap result = new Bitmap(width, height);
            //set the resolutions the same to avoid cropping due to resolution differences
            result.SetResolution(image.HorizontalResolution, image.VerticalResolution);
            //use a graphics object to draw the resized image into the bitmap
            using (Graphics graphics = Graphics.FromImage(result)) {
                //set the resize quality modes to high quality
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                //draw the image into the target bitmap
                graphics.DrawImage(image, 0, 0, result.Width, result.Height);
            }

            //return the resulting bitmap
            return result;
        }

        private static PointF GetUpperCorner( float rotation, float left, float top, float width, float height ) {
            // BUG 1006: Need to account for rotation when determining bounds
            if( rotation != 0 ) {
                PointF[] pts = new PointF[4];
                pts[0] = new PointF( left, top );
                pts[1] = new PointF( left, top + height );
                pts[2] = new PointF( left + width, top );
                pts[3] = new PointF( left + width, top + height );

                // Rotate the points
                System.Drawing.Drawing2D.Matrix m = new System.Drawing.Drawing2D.Matrix();
                m.RotateAt( rotation, new PointF( left + width/2.0f, top + height/2.0f ) );
                m.TransformPoints( pts );

                return new PointF( Math.Min( Math.Min( Math.Min( pts[0].X, pts[1].X ), pts[2].X ), pts[3].X ),
                                   Math.Min( Math.Min( Math.Min( pts[0].Y, pts[1].Y ), pts[2].Y ), pts[3].Y ) );
            } else {
                return new PointF( left, top );
            }
        }

        private static void CalculateGeometry( Image image, PowerPoint.Shapes shapes, int[] range, float emfWidthRatio, float emfHeightRatio, ref int xCoord, ref int yCoord, ref int width, ref int height ) {
            float top = -1;
            float left = -1;

            // Get the size in pixels of the image
            if( image is System.Drawing.Imaging.Metafile ) {
                width = (int)(image.Width * emfWidthRatio);
                height = (int)(image.Height * emfHeightRatio);
            } else {
                width = image.Width;
                height = image.Height;
            }

            // Get the start coordinate of the image
            foreach( int i in range ) {
                PowerPoint.Shape currentShape = shapes[i];

                if( currentShape.Type == Microsoft.Office.Core.MsoShapeType.msoAutoShape &&
                    currentShape.AutoShapeType == Microsoft.Office.Core.MsoAutoShapeType.msoShapeArc )
                    continue;

                PointF corner = GetUpperCorner( currentShape.Rotation, 
                                                currentShape.Left, currentShape.Top, 
                                                currentShape.Width, currentShape.Height );
                float shapeLeft = corner.X;
                float shapeTop = corner.Y;

                if( top == -1 || shapeTop < top ) {
                    top = shapeTop;
                }
                if( left == -1 || shapeLeft < left ) {
                    left = shapeLeft;
                }

                if( currentShape.HasTextFrame == Microsoft.Office.Core.MsoTriState.msoTrue ) {
                    float x1, x2, x3, x4;
                    float y1, y2, y3, y4;
                    currentShape.TextFrame.TextRange.RotatedBounds( out x1, out y1, 
                                                                    out x2, out y2, 
                                                                    out x3, out y3, 
                                                                    out x4, out y4 );
                    float left_new = Math.Min( Math.Min( Math.Min( x1, x2 ), x3 ), x4 );
                    float top_new = Math.Min( Math.Min( Math.Min( y1, y2 ), y3 ), y4 );

                    if( top == -1 || ( top_new < top && currentShape.Height>currentShape.TextFrame.TextRange.BoundHeight)) {
                        top = top_new;
                    }
                    if( left == -1 || ( left_new < left && currentShape.Width>currentShape.TextFrame.TextRange.BoundWidth) ) {
                        left = left_new;
                    }
                }
            }

            yCoord = (int)Math.Round( top );
            xCoord = (int)Math.Round( left );

            // Unsure non-zero width and height (NOTE: Must be at least 2 to display some lines)
            if( width <= 1 )
                width = 2;
            if( height <= 1 )
                height = 2;
        }
/*
        OBSOLETE
        private static void ProcessLayer(TempFileCollection tfc, PowerPoint.Shapes shapes, int[] range, Model.Presentation.DeckModel deck, Model.Presentation.SlideModel slide, Model.Presentation.SheetDisposition disposition, int emfHeight, int startHeight) {
            //Create the image
            if (range.Length > 0) {
                PowerPoint.ShapeRange sr = shapes.Range(range);
                //Generate a new filename
                string dirpath = tfc.BasePath;
                string filename = PPTDeckIO.GenerateFilename();
                filename = dirpath + "\\" + filename + ".emf";
                while (File.Exists(filename)) {
                    filename = PPTDeckIO.GenerateFilename();
                    filename = dirpath + "\\" + filename + ".emf";
                }
                sr.Export(filename, PowerPoint.PpShapeFormat.ppShapeFormatEMF, 0, 0,
                    PowerPoint.PpExportMode.ppRelativeToSlide);
                tfc.AddFile(filename, false);
                //Compute the MD5 of the BG
                FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
                MD5 md5Provider = new MD5CryptoServiceProvider();
                byte[] md5 = md5Provider.ComputeHash(fs);
                fs.Seek(0, SeekOrigin.Begin);
                Image image = Image.FromStream(fs);
                fs.Close();
                //Calculate the geometry
                int xCoord = 0;
                int yCoord = 0;
                int width = 0;
                int height = 0;
                PPTDeckIO.CalculateGeometry(image, slide, emfHeight, shapes, range, ref xCoord, ref yCoord, ref width, ref height);
                //Create the ImageSheet
                ImageSheetModel sheet = new ImageSheetModel(deck, Guid.NewGuid(), disposition,
                    new Rectangle(xCoord, yCoord, width, height), (ByteArray)md5, startHeight);
                //Add the ImageSheet to the Slide
                slide.ContentSheets.Add(sheet);
                //Add the Image+MD5 to the deck
                deck.AddSlideContent((ByteArray)md5, image);
            }
        }
*/
/*
        UNUSED
        /// <summary>
        /// Construct a sheet from a single shape
        /// </summary>
        /// <param name="tfc"></param>
        /// <param name="shape"></param>
        /// <param name="deck"></param>
        /// <param name="slide"></param>
        /// <param name="disposition"></param>
        /// <param name="emfHeight"></param>
        /// <param name="startHeight">The starting height to place this sheet at</param>
        private static void ProcessSheet(TempFileCollection tfc, PowerPoint.Shape shape, Model.Presentation.DeckModel deck, Model.Presentation.SlideModel slide, Model.Presentation.SheetDisposition disposition, int emfHeight, int startHeight) {
            
            PowerPoint.PpShapeFormat format;
            string fileExt = "";
            bool bitmapMode = false;

            if (shape.Name.StartsWith("Picture"))
                bitmapMode = true;


            if (bitmapMode) {
                format = PowerPoint.PpShapeFormat.ppShapeFormatJPG;
                fileExt = "jpg";
            }
            else {
               format = PowerPoint.PpShapeFormat.ppShapeFormatEMF;
               fileExt = "emf";
            }

            string dirpath = tfc.BasePath;
            string filename = PPTDeckIO.GenerateFilename();
            filename = dirpath + "\\" + filename + "." + fileExt;
            while (File.Exists(filename)) {
                filename = PPTDeckIO.GenerateFilename();
                filename = dirpath + "\\" + filename + "." + fileExt;
            }



            shape.Export(filename, format, 0, 0, PowerPoint.PpExportMode.ppRelativeToSlide);
            tfc.AddFile(filename, false);
            //Compute the MD5 of the BG
            FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
            MD5 md5Provider = new MD5CryptoServiceProvider();
            byte[] md5 = md5Provider.ComputeHash(fs);
            fs.Seek(0, SeekOrigin.Begin);
            Image image = Image.FromStream(fs);

            if (bitmapMode)
                image = DisassociateBitmap(image);

            fs.Close();
            //Calculate the geometry
            int xCoord = (int) shape.Left - 2;
            int yCoord = (int) shape.Top - 2;
            int width = (int) shape.Width + 4;    // Hackery to make lines visibile
            int height = (int) shape.Height + 4;  // Ignoring EMF Normalization
 
            //Create the ImageSheet
            ImageSheetModel sheet = new ImageSheetModel(deck, Guid.NewGuid(), disposition,
                new Rectangle(xCoord, yCoord, width, height), (ByteArray)md5, startHeight);
            //Add the ImageSheet to the Slide
            slide.ContentSheets.Add(sheet);
            //Add the Image+MD5 to the deck
            deck.AddSlideContent((ByteArray)md5, image);
        }
*/
/*
        OBSOLETE
        private static void CalculateGeometry(Image image, SlideModel slide, int emfHeight, PowerPoint.Shapes shapes, int[] range, ref int xCoord, ref int yCoord, ref int width, ref int height) {
            float topmost = -1;
            float leftmost = -1;
            float bottommost = -1;
            float rightmost = -1;
            foreach (int i in range) {
                PowerPoint.Shape currentShape = shapes[i];
                if (topmost == -1 || currentShape.Top < topmost) {
                    topmost = currentShape.Top;
                }
                if (leftmost == -1 || currentShape.Left < leftmost) {
                    leftmost = currentShape.Left;
                }

                // Fix BUG 926: Single lines invisible
                // Horizontal and vertical lines have a width/height of 0, need to account for width and height of line.
                float currentShapeWidth = currentShape.Width;
                float currentShapeHeight = currentShape.Height;
                if( currentShape.Type == Microsoft.Office.Core.MsoShapeType.msoLine ) {
                    currentShapeWidth = Math.Max( currentShape.Width, currentShape.Line.Weight );
                    currentShapeHeight = Math.Max( currentShape.Height, currentShape.Line.Weight );
                }

                float currentRight = currentShape.Left + currentShapeWidth;
                if (rightmost == -1 || currentRight > rightmost) {
                    rightmost = currentRight;
                }
                float currentBottom = currentShape.Top + currentShapeHeight;
                if (bottommost == -1 || currentBottom > bottommost) {
                    bottommost = currentBottom;
                }

                // Fix BUG 618: Slide content scrunched vertically
                // Some text boxes have a text frame that is larger than the shape itself so we need to calculate bounds based on this as well
                if( currentShape.HasTextFrame == Microsoft.Office.Core.MsoTriState.msoTrue ) {
                    if( topmost == -1 || currentShape.TextFrame.TextRange.BoundTop < topmost ) {
                        topmost = currentShape.TextFrame.TextRange.BoundTop;
                    }
                    if( leftmost == -1 || currentShape.TextFrame.TextRange.BoundLeft < leftmost ) {
                        leftmost = currentShape.TextFrame.TextRange.BoundLeft;
                    }
                    float currentRight2 = currentShape.TextFrame.TextRange.BoundLeft + currentShape.TextFrame.TextRange.BoundWidth;
                    if( rightmost == -1 || currentRight2 > rightmost ) {
                        rightmost = currentRight2;
                    }
                    float currentBottom2 = currentShape.TextFrame.TextRange.BoundTop + currentShape.TextFrame.TextRange.BoundHeight;
                    if( bottommost == -1 || currentBottom2 > bottommost ) {
                        bottommost = currentBottom2;
                    }
                }
            }

            yCoord = (int)Math.Round(topmost);
            xCoord = (int)Math.Round(leftmost);
            width = (int)Math.Max(Math.Ceiling(rightmost - leftmost), 2);
            height = (int)Math.Max(Math.Ceiling(bottommost - topmost), 2);
        }
*/
        #endregion

        #region OpenCP3 Method

        public static DeckModel OpenCP3(FileInfo file) {
            //Deserialize it
            FileStream fs = file.Open(FileMode.Open, FileAccess.Read);

            try {
                BinaryFormatter bf = new BinaryFormatter();
                DeckModel toReturn = (DeckModel)bf.Deserialize(fs);
                toReturn.Filename = file.FullName;
                return toReturn;
            } finally {
                fs.Close();
            }
        }

        #endregion

        #region SaveAsCP3 Method

        public static void SaveAsCP3(FileInfo file, DeckModel in_deck) {
            //Update the deck's filename
            DeckModel deck = in_deck.Copy();
            deck.current_subs = false;
            deck.current_poll = false;
            if ((deck.Disposition & DeckDisposition.Remote) != 0){
                deck.Disposition = DeckDisposition.Empty;
            }
            using (Synchronizer.Lock(deck)) {
                deck.Filename = file.FullName;
            }

            //Serialize it
            FileStream fs = file.Open(FileMode.Create, FileAccess.Write);
            try {
                BinaryFormatter bf = new BinaryFormatter();
                bf.Serialize(fs, deck);
                fs.Flush();
            } finally {
                fs.Close();
            }
        }

        #endregion

        #region Export Deck Method

        //WARNING - This method assumes that all necessary sanity checks for path have been done!
        /// <summary>
        /// 
        /// </summary>
        /// <param name="traversal"></param>
        /// <param name="path"></param>
        /// <param name="format"></param>
        /// <returns>a List of strings with all the names of the image files</returns>
        public static List<string> ExportDeck(DefaultDeckTraversalModel traversal, string path, System.Drawing.Imaging.ImageFormat format) {
            return PPTDeckIO.ExportDeck(traversal, path, format, 0, 0, 1.0f);
        }

        //WARNING - This method assumes that all necessary sanity checks for path have been done!
        /// <summary>
        /// 
        /// </summary>
        /// <param name="traversal"></param>
        /// <param name="path"></param>
        /// <param name="format"></param>
        /// <returns>a List of strings with all the names of the image files</returns>
        public static List<string> ExportDeck(DefaultDeckTraversalModel traversal, string path, System.Drawing.Imaging.ImageFormat format, int width, int height, float scale ) {
            List<string> file_names = new List<string>();
            //Info here:  http://gotdotnet.com/Community/MessageBoard/Thread.aspx?id=27804
            //Iterate over all the slides
            using (Synchronizer.Lock(traversal.SyncRoot)) {
                using (Synchronizer.Lock(traversal.Deck.SyncRoot)) {
                    using (Synchronizer.Lock(traversal.Deck.TableOfContents.SyncRoot)) {
                        //Create the directory, if it doesn't already exist
                        if (!Directory.Exists(path)) {
                            Directory.CreateDirectory(path);
                        }

                        int imageCount = 1;
                        TableOfContentsModel.Entry currentEntry = traversal.Deck.TableOfContents.Entries[0];
                        while (currentEntry != null) {
                            Color background = Color.Transparent;
                            BackgroundTemplate template = null;

                            // Add the background color
                            // First see if there is a Slide BG, if not, try the Deck.  Otherwise, use transparent.
                            using (Synchronizer.Lock(currentEntry.Slide.SyncRoot)) {
                                if (currentEntry.Slide.BackgroundColor != Color.Empty) {
                                    background = currentEntry.Slide.BackgroundColor;
                                }
                                else if (traversal.Deck.DeckBackgroundColor != Color.Empty) {
                                    background = traversal.Deck.DeckBackgroundColor;
                                }
                                if (currentEntry.Slide.BackgroundTemplate != null) {
                                    template = currentEntry.Slide.BackgroundTemplate;
                                }
                                else if (traversal.Deck.DeckBackgroundTemplate != null) {
                                    template = traversal.Deck.DeckBackgroundTemplate;
                                }
                            }

                            // Get the Image
                            Bitmap toExport = DrawSlide(currentEntry, template, background, SheetDisposition.Background | SheetDisposition.Public | SheetDisposition.Student | SheetDisposition.All, width, height, scale );

                            // Format the imageCount
                            string number = "";
                            if (imageCount >= 0 && imageCount < 10) {
                                number = "00" + imageCount;
                            }
                            else if (imageCount >= 10 && imageCount < 100) {
                                number = "0" + imageCount;
                            }
                            else {
                                number = "" + imageCount;
                            }

                            // Get the extension string based on the exported image type
                            string extension = "";
                            if (format == System.Drawing.Imaging.ImageFormat.Bmp) {
                                extension = "bmp";
                            }
                            else if (format == System.Drawing.Imaging.ImageFormat.Emf) {
                                extension = "emf";
                            }
                            else if (format == System.Drawing.Imaging.ImageFormat.Exif) {
                                extension = "exf";
                            }
                            else if (format == System.Drawing.Imaging.ImageFormat.Gif) {
                                extension = "gif";
                            }
                            else if (format == System.Drawing.Imaging.ImageFormat.Icon) {
                                extension = "ico";
                            }
                            else if (format == System.Drawing.Imaging.ImageFormat.Jpeg) {
                                extension = "jpg";
                            }
                            else if (format == System.Drawing.Imaging.ImageFormat.Png) {
                                extension = "png";
                            }
                            else if (format == System.Drawing.Imaging.ImageFormat.Tiff) {
                                extension = "tif";
                            }
                            else if (format == System.Drawing.Imaging.ImageFormat.Wmf) {
                                extension = "wmf";
                            }
                            else {
                                extension = "bmp";
                            }

                            // Construct the complete file name and save
                            string file_name = traversal.Deck.HumanName + "_" + number + "." + extension;
                            //string file_name = "slide" + imageCount.ToString() + "." + extension;
                            toExport.Save(path + "\\" + file_name, format);

                            /// Save the file name to our list
                            file_names.Add(file_name);

                            // Increase the counter
                            imageCount++;

                            // Done, get next entry
                            currentEntry = traversal.FindNext(currentEntry);

                            // Cleanup
                            toExport.Dispose();
                        }
                    }
                }
            }
            return file_names;
        }

        // NOTE: Eventually this code should be converted to use SlideRenderer instead of each SheetRenderer. There were some issues with doing 
        // this initially so for the time being we will keep it like this.
        public static Bitmap DrawSlide(TableOfContentsModel.Entry currentEntry, BackgroundTemplate template, System.Drawing.Color background, SheetDisposition dispositions) {
            return PPTDeckIO.DrawSlide(currentEntry, template, background, dispositions, 0, 0, 1.0f);
        }

        // NOTE: Eventually this code should be converted to use SlideRenderer instead of each SheetRenderer. There were some issues with doing 
        // this initially so for the time being we will keep it like this.
        public static Bitmap DrawSlide(TableOfContentsModel.Entry currentEntry, BackgroundTemplate template, System.Drawing.Color background, SheetDisposition dispositions, int width, int height, float scale ) {
            // Save the old state
            System.Drawing.Drawing2D.GraphicsState oldState;

            using (Synchronizer.Lock(currentEntry.Slide.SyncRoot)) {
                Rectangle rect;
                int boundsWidth = currentEntry.Slide.Bounds.Width;
                int boundsHeight = currentEntry.Slide.Bounds.Height;
                int exportWidth = width;
                int exportHeight = height;

                if (width > 0 && height > 0) {
                    // Do Nothing
                }
                else if (width > 0) {
                    exportHeight = (int)Math.Round( ( (float)width / (float)boundsWidth ) * (float)boundsHeight );
                }
                else if (height > 0) {
                    exportWidth = (int)Math.Round( ( (float)height / (float)boundsHeight ) * (float)boundsWidth );
                }
                else {
                    exportWidth = boundsWidth;
                    exportHeight = boundsHeight;
                }

                // Scale the size
                exportWidth = (int)Math.Round( exportWidth * scale );
                exportHeight = (int)Math.Round( exportHeight * scale );
                rect = new Rectangle(0, 0, exportWidth, exportHeight);

                //Note: Uses DibGraphicsBuffer from TPC SDK to do antialiasing
                //See: http://msdn.microsoft.com/library/default.asp?url=/library/en-us/dntablet/html/tbconprintingink.asp
                /// create the bitmap we're exporting to
                Bitmap toExport = new Bitmap(rect.Width, rect.Height);
                /// create what we will be drawing on to the export
                Graphics toSave = Graphics.FromImage(toExport);

                /// draw the slide data on a temporary graphics object in a temporary form
                System.Windows.Forms.Form tempForm = new System.Windows.Forms.Form();
                Graphics screenGraphics = tempForm.CreateGraphics();
                DibGraphicsBuffer dib = new DibGraphicsBuffer();
                Graphics tempGraphics = dib.RequestBuffer(screenGraphics, rect.Width, rect.Height);

                //Add the background color
                //First see if there is a Slide BG, if not, try the Deck.  Otherwise, use transparent.
                tempGraphics.Clear(background);

                //Add the background template
                if (template != null)
                {
                    using (BackgroundTemplateRenderer bkgRender = new BackgroundTemplateRenderer(template))
                    {
                        bkgRender.DrawAll(tempGraphics, rect);
                    }
                }

                //Get the Slide content and draw it
                oldState = tempGraphics.Save();

                Model.Presentation.SlideModel.SheetCollection sheets = currentEntry.Slide.ContentSheets;
                for (int i = 0; i < sheets.Count; i++)
                {
                    Model.Viewer.SlideDisplayModel display = new Model.Viewer.SlideDisplayModel(tempGraphics, null);

                    Rectangle slide = rect;
                    float zoom = 1f;
                    if (currentEntry.Slide != null)
                    {
                        slide = currentEntry.Slide.Bounds;
                        zoom = currentEntry.Slide.Zoom;
                    }

                    System.Drawing.Drawing2D.Matrix pixel, ink;
                    display.FitSlideToBounds(System.Windows.Forms.DockStyle.Fill, rect, zoom, ref slide, out pixel, out ink);
                    using (Synchronizer.Lock(display.SyncRoot))
                    {
                        display.SheetDisposition = dispositions;
                        display.Bounds = slide;
                        display.PixelTransform = pixel;
                        display.InkTransform = ink;
                    }

                    Viewer.Slides.SheetRenderer r = Viewer.Slides.SheetRenderer.ForStaticSheet(display, sheets[i]);
                    if ((r.Sheet.Disposition & dispositions) != 0)
                        r.Paint(new System.Windows.Forms.PaintEventArgs(tempGraphics, rect));
                    r.Dispose();
                }

                //Restore the Old State
                tempGraphics.Restore(oldState);
                oldState = tempGraphics.Save();

                //Get the Annotation content and draw it
                sheets = currentEntry.Slide.AnnotationSheets;
                for (int i = 0; i < sheets.Count; i++)
                {
                    Model.Viewer.SlideDisplayModel display = new Model.Viewer.SlideDisplayModel(tempGraphics, null);

                    Rectangle slide = rect;
                    float zoom = 1f;
                    if (currentEntry.Slide != null)
                    {
                        slide = currentEntry.Slide.Bounds;
                        zoom = currentEntry.Slide.Zoom;
                    }

                    System.Drawing.Drawing2D.Matrix pixel, ink;
                    display.FitSlideToBounds(System.Windows.Forms.DockStyle.Fill, rect, zoom, ref slide, out pixel, out ink);
                    using (Synchronizer.Lock(display.SyncRoot))
                    {
                        display.SheetDisposition = dispositions;
                        display.Bounds = slide;
                        display.PixelTransform = pixel;
                        display.InkTransform = ink;
                    }

                    Viewer.Slides.SheetRenderer r = Viewer.Slides.SheetRenderer.ForStaticSheet(display, sheets[i]);
                    if ((r.Sheet.Disposition & dispositions) != 0)
                        r.Paint(new System.Windows.Forms.PaintEventArgs(tempGraphics, rect));
                    r.Dispose();
                }

                //Restore the Old State
                tempGraphics.Restore(oldState);

                //Export the image
                //Merge the graphics
                dib.PaintBuffer(toSave, 0, 0);

                //Dispose all the graphics
                toSave.Dispose();
                screenGraphics.Dispose();
                tempGraphics.Dispose();

                return toExport;
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Make sure all characters in a string are printable - replace any out of range characters by a space
        /// </summary>
        /// <param name="str">Input string</param>
        /// <returns></returns>
        private static string CleanString(string str){
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < str.Length; i++){
                char c = str[i];
                if (! (Char.IsLetterOrDigit(c) || Char.IsPunctuation(c) || Char.IsSymbol(c))) {
                    sb.Append(' ');
                } else {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Construct a new file name, which is unique for the session
        /// </summary>
        /// <returns>File name of the from "cptmpfilexx"</returns>
        private static string GenerateFilename() {
            string result = "cptmpfile" + tmpFileCtr;
            tmpFileCtr++;

            return result;
        }

        private static int tmpFileCtr = 0;

        /// <summary>
        /// Convert from a string to the corresponding sheet disposition.  
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        private static Model.Presentation.SheetDisposition Disposition(string str) {
            if (str.Equals("Instructor"))
                return Model.Presentation.SheetDisposition.Instructor;
            if (str.Equals("Student"))
                return Model.Presentation.SheetDisposition.Student;
            if (str.Equals("Shared"))
                return Model.Presentation.SheetDisposition.Public;

            return Model.Presentation.SheetDisposition.All;             // All as the default
        }

        #endregion
    }
}
