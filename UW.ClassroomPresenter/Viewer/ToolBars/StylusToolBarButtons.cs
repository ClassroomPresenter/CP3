// $Id: StylusToolBarButtons.cs 1711 2008-08-14 01:18:25Z cmprince $

using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

using Microsoft.Ink;

using UW.ClassroomPresenter.Model;
using UW.ClassroomPresenter.Model.Stylus;

namespace UW.ClassroomPresenter.Viewer.ToolBars {
    public class StylusToolBarButtons {
        /// <summary>
        /// The presenter model to modify
        /// </summary>
        private readonly PresenterModel m_Model;

        /// <summary>
        /// The pen stylus model
        /// </summary>
        private readonly PenStylusModel m_PenModel;
        /// <summary>
        /// The highlighter stylus model
        /// </summary>
        private readonly PenStylusModel m_HighlighterModel;
        /// <summary>
        /// The eraser stylus model
        /// </summary>
        private readonly EraserStylusModel m_EraserModel;
        /// <summary>
        /// The lasso stylus model
        /// </summary>
        //private readonly LassoStylusModel m_LassoModel;
        /// <summary>
        /// The text stylus model
        /// </summary>
        private readonly TextStylusModel m_TextModel;
        /// <summary>
        /// The image model
        /// </summary>
        private readonly ImageStylusModel m_ImageModel;
        /// <summary>
        /// The DrawingAttributes for pen
        /// </summary>
        private readonly DrawingAttributes m_PenAtts;
        /// <summary>
        /// The DrawingAttributes for high lighter
        /// </summary>
        private readonly DrawingAttributes m_HighLighterAtts;
        /// <summary>
        /// The StylusEntries
        /// </summary>
        private readonly StylusEntry[][] m_StylusEntrys;


        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="model">The presenter model</param>
        public StylusToolBarButtons(PresenterModel model) {
            this.m_Model = model;

            // Initialize the stylus models.
            this.m_PenModel = new PenStylusModel(Guid.NewGuid());
            this.m_HighlighterModel = new PenStylusModel(Guid.NewGuid());
            this.m_EraserModel = new EraserStylusModel(Guid.NewGuid());
            // Comment out as it is not being used currently
            //this.m_LassoModel = new LassoStylusModel(Guid.NewGuid());
            this.m_ImageModel = new ImageStylusModel(Guid.NewGuid());
            this.m_TextModel = TextStylusModel.GetInstance();

            this.m_PenAtts = new DrawingAttributes(Color.Black);
            this.m_HighLighterAtts = new HighlighterDrawingAttributes(Color.Orange);
            this.m_StylusEntrys = new StylusEntry[][] {
                new StylusEntry[] { 
                    new StylusEntry(new DrawingAttributes(Color.Orange), this.m_PenModel, Strings.SelectCustomColorPen, PenType.Pen),
                    new StylusEntry(new HighlighterDrawingAttributes(Color.Pink), this.m_HighlighterModel, Strings.SelectCustomColorHighlighter, PenType.Hightlighter),
                    new StylusEntry(new DrawingAttributes(Color.Orange), this.m_TextModel, Strings.CustomColorText, PenType.Text),
                },
                new StylusEntry[] {
                    new StylusEntry(new DrawingAttributes(Color.Blue), this.m_PenModel, Strings.SelectBluePen, PenType.Pen),
                    new StylusEntry(new HighlighterDrawingAttributes(Color.Cyan), this.m_HighlighterModel, Strings.SelectCyanHighlighter, PenType.Hightlighter),
                    new StylusEntry(new DrawingAttributes(Color.Blue), this.m_TextModel, Strings.BlueText, PenType.Text),
                },
                new StylusEntry[] {
                    new StylusEntry(new DrawingAttributes(Color.Green), this.m_PenModel, Strings.SelectGreenPen, PenType.Pen),
                    new StylusEntry(new HighlighterDrawingAttributes(Color.LawnGreen), this.m_HighlighterModel, Strings.SelectGreenHighlighter, PenType.Hightlighter),
                    new StylusEntry(new DrawingAttributes(Color.Green), this.m_TextModel, Strings.GreenText, PenType.Text),
                },
                new StylusEntry[] {
                    new StylusEntry(new DrawingAttributes(Color.Red), this.m_PenModel, Strings.SelectRedPen, PenType.Pen),
                    new StylusEntry(new HighlighterDrawingAttributes(Color.Magenta), this.m_HighlighterModel, Strings.SelectMagentaHighlighter, PenType.Hightlighter),
                    new StylusEntry(new DrawingAttributes(Color.Red), this.m_TextModel, Strings.RedText, PenType.Text),
                },
                new StylusEntry[] {
                    new StylusEntry(new DrawingAttributes(Color.Yellow), this.m_PenModel, Strings.SelectYellowPen, PenType.Pen),
                    new StylusEntry(new HighlighterDrawingAttributes(Color.Yellow), this.m_HighlighterModel, Strings.SelectYellowHighlighter, PenType.Hightlighter),
                    new StylusEntry(new DrawingAttributes(Color.Yellow), this.m_TextModel, Strings.YellowText, PenType.Text),
                },
                new StylusEntry[] {
                    new StylusEntry(this.m_PenAtts, this.m_PenModel, Strings.SelectBlackPen, PenType.Pen),
                    new StylusEntry(this.m_HighLighterAtts, this.m_HighlighterModel, Strings.SelectOrangeHighlighter, PenType.Hightlighter),
                    new StylusEntry(new DrawingAttributes(Color.Black), this.m_TextModel, Strings.BlackText, PenType.Text),
                },
            };
        }

        /// <summary>
        /// Handle the creation of all the buttons in this part of the ToolStrip
        /// </summary>
        /// <param name="parent">The parent ToolStrip</param>
        /// <param name="dispatcher">The event queue</param>
        public void MakeButtons(ToolStrip parent, ControlEventQueue dispatcher) {
            // Initialize the stylus selector buttons, which serve mainly to choose the stylus's color.
            // There are two sets of buttons: for the pen and for the highlighter.
            ToolStripItem[] penButtons = new ToolStripItem[] {
                new CustomDrawingAttributesDropDownButton(dispatcher, this.m_StylusEntrys[0], this.m_Model, parent),
                new DrawingAttributesToolBarButton(dispatcher, this.m_StylusEntrys[1], this.m_Model, parent),
                new DrawingAttributesToolBarButton(dispatcher, this.m_StylusEntrys[2], this.m_Model, parent),
                new DrawingAttributesToolBarButton(dispatcher, this.m_StylusEntrys[3], this.m_Model, parent),
                new DrawingAttributesToolBarButton(dispatcher, this.m_StylusEntrys[4], this.m_Model, parent),
                new DrawingAttributesToolBarButton(dispatcher, this.m_StylusEntrys[5], this.m_Model, parent),
            };

            using(Synchronizer.Lock(this.m_Model.SyncRoot)) {
                using(Synchronizer.Lock(this.m_PenModel.SyncRoot)) {
                    this.m_PenModel.DrawingAttributes = this.m_PenAtts;
                }

                using(Synchronizer.Lock(this.m_HighlighterModel.SyncRoot)) {
                    this.m_HighlighterModel.DrawingAttributes = this.m_HighLighterAtts;
                }

                // Default to the pen stylus, if none is in use already.
                if(this.m_Model.Stylus == null)
                    this.m_Model.Stylus = this.m_PenModel;
            }

            // Add the correct image index and add to the ToolStrip
            foreach(ToolStripItem button in penButtons) {
                Bitmap bmp = new Bitmap( parent.ImageScalingSize.Width, parent.ImageScalingSize.Height );
                Misc.ImageListHelper.Add( bmp, parent.ImageList );

                if( button is DrawingAttributesToolBarButton ) {
                    parent.Items.Add( (DrawingAttributesToolBarButton)button );
                    ((DrawingAttributesToolBarButton)button).ImageIndex = parent.ImageList.Images.Count - 1;
                    ((DrawingAttributesToolBarButton)button).UpdateBitmapAtImageIndex();
                } else if( button is CustomDrawingAttributesDropDownButton ) {
                    parent.Items.Add( (CustomDrawingAttributesDropDownButton)button );
                    ((CustomDrawingAttributesDropDownButton)button).ImageIndex = parent.ImageList.Images.Count - 1;
                    ((CustomDrawingAttributesDropDownButton)button).UpdateBitmapAtImageIndex();
                    ((CustomDrawingAttributesDropDownButton)button).DropDownOpening += new EventHandler( ((CustomDrawingAttributesDropDownButton)button).HandleParentButtonDropDown );
                }

            }

            // Add a separator
            parent.Items.Add(new ToolStripSeparator());

            // Create the Pen Button
            StylusToolBarButton penButton = new StylusToolBarButton( dispatcher, this.m_PenModel, this.m_Model );
            penButton.ToolTipText = Strings.SelectPenStylus;
            penButton.Image = UW.ClassroomPresenter.Properties.Resources.pencil;

            // Create the Highlighter Button
            StylusToolBarButton highlighterButton = new StylusToolBarButton(dispatcher, this.m_HighlighterModel, this.m_Model);
            highlighterButton.ToolTipText = Strings.SelectHighlighterStylus;
            highlighterButton.Image = UW.ClassroomPresenter.Properties.Resources.highlight;

            // Create the Lasso Button
            //StylusToolBarButton lassoButton = new StylusToolBarButton(dispatcher, this.m_LassoModel, this.m_Model);
            //lassoButton.ToolTipText = "Select the Ink Lasso.";
            //lassoButton.Image = UW.ClassroomPresenter.Properties.Resources.lasso;

            // Create the Text Button
            StylusToolBarButton textButton = new StylusToolBarButton(dispatcher, this.m_TextModel, this.m_Model);
            textButton.ToolTipText = Strings.AddEditText;
            textButton.Image = UW.ClassroomPresenter.Properties.Resources.text;

            CustomFontButton customfontbutton = new CustomFontButton(textButton, m_Model);

            // Create the Image Button

            StylusToolBarButton imageButton = new StylusToolBarButton(dispatcher, m_ImageModel, m_Model);
            imageButton.ToolTipText = Strings.AddEditImages;
            imageButton.Image = UW.ClassroomPresenter.Properties.Resources.image;

            // Create the Eraser Button
            StylusToolBarButton eraserButton = new StylusToolBarButton(dispatcher, this.m_EraserModel, this.m_Model);
            eraserButton.ToolTipText = Strings.SelectEraser;
            eraserButton.Image = UW.ClassroomPresenter.Properties.Resources.eraser;


            // Add the buttons to the ToolStrip
            parent.Items.Add(penButton);
            parent.Items.Add(highlighterButton);
            parent.Items.Add(eraserButton);
            //parent.Items.Add(lassoButton);                            // Removed by RJA for faculty summit version
            parent.Items.Add(textButton);
            parent.Items.Add(customfontbutton);
            // TODO: Reinsert image button once objectdisposed exception is fixed
            parent.Items.Add(imageButton);
        }

        /// <summary>
        /// Handle the creation of all the buttons in this part of the ToolStrip
        /// </summary>
        /// <param name="main">The main ToolStrip</param>
        /// <param name="extra">The extra ToolStrip</param>
        /// <param name="dispatcher">The event queue</param>
        public void MakeButtons(ToolStrip main, ToolStrip extra, ControlEventQueue dispatcher)
        {

            // Initialize the stylus selector buttons, which serve mainly to choose the stylus's color.
            // There are two sets of buttons: for the pen and for the highlighter.
            ToolStripItem[] mainPenButtons = new ToolStripItem[] {
                new CustomDrawingAttributesDropDownButton(dispatcher, this.m_StylusEntrys[0], this.m_Model, main),
                new DrawingAttributesToolBarButton(dispatcher, this.m_StylusEntrys[2], this.m_Model, main),
                new DrawingAttributesToolBarButton(dispatcher, this.m_StylusEntrys[4], this.m_Model, main),
                
            };
            ToolStripItem[] extraPenButtons = new ToolStripItem[] {
                new DrawingAttributesToolBarButton(dispatcher, this.m_StylusEntrys[1], this.m_Model, extra),
                new DrawingAttributesToolBarButton(dispatcher, this.m_StylusEntrys[3], this.m_Model, extra),
                new DrawingAttributesToolBarButton(dispatcher, this.m_StylusEntrys[5], this.m_Model, extra),
            };
            
            using(Synchronizer.Lock(this.m_Model.SyncRoot)) {
                using(Synchronizer.Lock(this.m_PenModel.SyncRoot)) {
                    this.m_PenModel.DrawingAttributes = this.m_PenAtts;
                }

                using(Synchronizer.Lock(this.m_HighlighterModel.SyncRoot)) {
                    this.m_HighlighterModel.DrawingAttributes = this.m_HighLighterAtts;
                }

                // Default to the pen stylus, if none is in use already.
                if(this.m_Model.Stylus == null)
                    this.m_Model.Stylus = this.m_PenModel;
            }

            // Add the correct image index and add to the ToolStrip
            foreach (ToolStripItem button in mainPenButtons) {
                Bitmap bmp = new Bitmap(main.ImageScalingSize.Width, main.ImageScalingSize.Height);
                Misc.ImageListHelper.Add(bmp, main.ImageList);

                if (button is DrawingAttributesToolBarButton) {
                    main.Items.Add((DrawingAttributesToolBarButton)button);
                    ((DrawingAttributesToolBarButton)button).ImageIndex = main.ImageList.Images.Count - 1;
                    ((DrawingAttributesToolBarButton)button).UpdateBitmapAtImageIndex();
                }
                else if (button is CustomDrawingAttributesDropDownButton) {
                    main.Items.Add((CustomDrawingAttributesDropDownButton)button);
                    ((CustomDrawingAttributesDropDownButton)button).ImageIndex = main.ImageList.Images.Count - 1;
                    ((CustomDrawingAttributesDropDownButton)button).UpdateBitmapAtImageIndex();
                    ((CustomDrawingAttributesDropDownButton)button).DropDownOpening += new EventHandler(((CustomDrawingAttributesDropDownButton)button).HandleParentButtonDropDown);
                }
            }

            foreach (ToolStripItem button in extraPenButtons) {
                Bitmap bmp = new Bitmap(extra.ImageScalingSize.Width, extra.ImageScalingSize.Height);
                Misc.ImageListHelper.Add(bmp, extra.ImageList);

                if (button is DrawingAttributesToolBarButton) {
                    extra.Items.Add((DrawingAttributesToolBarButton)button);
                    ((DrawingAttributesToolBarButton)button).ImageIndex = extra.ImageList.Images.Count - 1;
                    ((DrawingAttributesToolBarButton)button).UpdateBitmapAtImageIndex();
                }
                else if (button is CustomDrawingAttributesDropDownButton) {
                    extra.Items.Add((CustomDrawingAttributesDropDownButton)button);
                    ((CustomDrawingAttributesDropDownButton)button).ImageIndex = extra.ImageList.Images.Count - 1;
                    ((CustomDrawingAttributesDropDownButton)button).UpdateBitmapAtImageIndex();
                    ((CustomDrawingAttributesDropDownButton)button).DropDownOpening += new EventHandler(((CustomDrawingAttributesDropDownButton)button).HandleParentButtonDropDown);
                }
            }

            // Add a separator
            main.Items.Add(new ToolStripSeparator());
            extra.Items.Add(new ToolStripSeparator());

            // Create the Pen Button
            StylusToolBarButton penButton = new StylusToolBarButton(dispatcher, this.m_PenModel, this.m_Model);
            penButton.ToolTipText = Strings.SelectPenStylus;
            penButton.Image = UW.ClassroomPresenter.Properties.Resources.pencil;

            // Create the Highlighter Button
            StylusToolBarButton highlighterButton = new StylusToolBarButton(dispatcher, this.m_HighlighterModel, this.m_Model);
            highlighterButton.ToolTipText = Strings.SelectHighlighterStylus;
            highlighterButton.Image = UW.ClassroomPresenter.Properties.Resources.highlight;

            // Create the Text Button
            StylusToolBarButton textButton = new StylusToolBarButton(dispatcher, this.m_TextModel, this.m_Model);
            textButton.ToolTipText = Strings.AddEditText;
            textButton.Image = UW.ClassroomPresenter.Properties.Resources.text;

            CustomFontButton customfontbutton = new CustomFontButton(textButton, m_Model);

            // Create the Eraser Button
            StylusToolBarButton eraserButton = new StylusToolBarButton(dispatcher, this.m_EraserModel, this.m_Model);
            eraserButton.ToolTipText = Strings.SelectEraser;
            eraserButton.Image = UW.ClassroomPresenter.Properties.Resources.eraser;

            // Add the buttons to the ToolStrip
            main.Items.Add(penButton);
            extra.Items.Add(highlighterButton);
            main.Items.Add(textButton);
            main.Items.Add(customfontbutton);
            extra.Items.Add(eraserButton);
            extra.Items.Add(new ToolStripSeparator());
        }

        /// <summary>
        /// Class that represents the default drawing attributes of a highlighter
        /// </summary>
        private class HighlighterDrawingAttributes : DrawingAttributes {
            public HighlighterDrawingAttributes(Color color) : base(color) {
                this.PenTip = PenTip.Ball;
                this.Width = this.Width * 6;
                this.Height = this.Width * 2;

                // Here is the essence of the highlighter:
                this.RasterOperation = RasterOperation.MaskPen;
            }
        }
    }
}
