// $Id: NetworkEventInfoForm.cs 774 2005-09-21 20:22:33Z pediddle $

using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using UW.ClassroomPresenter.Network.Messages;
using UW.ClassroomPresenter.Network.Messages.Network;
using UW.ClassroomPresenter.Network.Messages.Presentation;
using UW.ClassroomPresenter.Network.Chunking;
using UW.ClassroomPresenter.Network.RTP;
using UW.ClassroomPresenter.Test.Network.Common;

namespace UW.ClassroomPresenter.Test.Network.Playback {
    /// <summary>
    /// Summary description for NetworkEventInfoForm.
    /// </summary>
    public class NetworkEventInfoForm : System.Windows.Forms.Form {
        private System.Windows.Forms.Label infoLabel;
        private UW.ClassroomPresenter.Test.Network.Player.DoubleBufferPanel doubleBufferPanel;
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.Container components = null;
        private Image m_Image;

        public NetworkEventInfoForm() {
            //
            // Required for Windows Form Designer support
            //
            InitializeComponent();

            //
            // TODO: Add any constructor code after InitializeComponent call
            //
            this.doubleBufferPanel.Paint += new PaintEventHandler(this.onDoubleBufferPanelPaint);
        }

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        protected override void Dispose( bool disposing ) {
            if( disposing ) {
                if(components != null) {
                    components.Dispose();
                }
            }
            base.Dispose( disposing );
        }

        public void DisplayText(NetworkEvent ne) {
            this.m_Image = null;
            DateTime time = new DateTime(ne.timeIndex);
            this.infoLabel.Text = "";
            this.appendText("Sender: " + ne.source);
            this.appendText("Time: " + time.ToLongDateString() + " " + time.ToLongTimeString());
            if (ne is NetworkChunkEvent) {
                NetworkChunkEvent nce = ne as NetworkChunkEvent;
                this.appendText("Category: Chunk");
                this.appendText("Message Sequence: " + nce.chunk.MessageSequence);
                this.appendText("Chunk Sequence: " + nce.chunk.ChunkSequence);
                this.appendText("First Chunk Sequence of Message: " + nce.chunk.FirstChunkSequenceOfMessage);
                this.appendText("Number of Chunks: " + nce.chunk.NumberOfChunksInMessage);
            } else if (ne is NetworkMessageEvent) {
                NetworkMessageEvent nme = ne as NetworkMessageEvent;
                this.appendText("Category: Message");
                this.displayMessageEventRecursive(nme.message);
            } else if (ne is NetworkNACKMessageEvent) {

            } else {
                //Unknown
                this.infoLabel.Text = "Unknown Type";
            }
            this.doubleBufferPanel.Invalidate();
        }

        private void displayMessageEventRecursive(UW.ClassroomPresenter.Network.Messages.Message msg) {
            this.appendText("-----");
            string longtype = msg.GetType().ToString();
            string shorttype = longtype.Substring(longtype.LastIndexOf(".") + 1);
            this.appendText("Type: " + shorttype);
            if (msg is DeckSlideContentMessage) {
                DeckSlideContentMessage dscm = msg as DeckSlideContentMessage;
                this.appendText("Content Hash: " + dscm.Content.key.ToString());
                this.m_Image = dscm.Content.image;
            } else if (msg is PresentationEndedMessage) {
                //Nothing really interesting here...
            } else if (msg is TableOfContentsEntryMessage) {
                TableOfContentsEntryMessage tocem = msg as TableOfContentsEntryMessage;
                string toDisplay = "Path From Root: ";
                int[] path = tocem.PathFromRoot;
                for (int i = 0; i < path.Length; i++) {
                    toDisplay += " " + path[i].ToString();
                }
                this.appendText(toDisplay);
            } else if (msg is TableOfContentsEntryRemovedMessage) {
                TableOfContentsEntryRemovedMessage tocerm = msg as TableOfContentsEntryRemovedMessage;
                this.appendText("Entry ID: " + tocerm.TargetId.ToString());
            } else if (msg is InstructorMessage) {
                InstructorMessage im = msg as InstructorMessage;
                this.appendText("Linked Navigation: " + im.ForcingStudentNavigationLock.ToString());
                this.appendText("Student Submissions: " + im.AcceptingStudentSubmissions.ToString());
            } else if (msg is StudentMessage) {
                //Nothing really interesting here...
            } else if (msg is PublicMessage) {
                //Nothing really interesting here...
            } else if (msg is InstructorCurrentPresentationChangedMessage) {
                InstructorCurrentPresentationChangedMessage icpcm = msg as InstructorCurrentPresentationChangedMessage;
                this.appendText("Human Name: " + icpcm.HumanName);
            } else if (msg is InstructorCurrentDeckTraversalChangedMessage) {
                InstructorCurrentDeckTraversalChangedMessage icdtcm = msg as InstructorCurrentDeckTraversalChangedMessage;
                this.appendText("Traversal ID: " + icdtcm.TargetId.ToString());
            } else if (msg is DeckInformationMessage) {
                DeckInformationMessage dim = msg as DeckInformationMessage;
                this.appendText("Disposition: " + dim.Disposition);
                this.appendText("Human Name: " + dim.HumanName);
                this.appendText("Deck BG Color: " + dim.DeckBackgroundColor);
            } else if (msg is SlideDeckTraversalMessage) {
                SlideDeckTraversalMessage sdtm = msg as SlideDeckTraversalMessage;
                this.appendText("Traversal ID: " + sdtm.TargetId.ToString());
            } else if (msg is DeckTraversalRemovedFromPresentationMessage) {
                DeckTraversalRemovedFromPresentationMessage dtrfpm = msg as DeckTraversalRemovedFromPresentationMessage;
                this.appendText("Traversal ID: " + dtrfpm.TargetId.ToString());
            } else if (msg is ImageSheetMessage) {
                ImageSheetMessage ism = msg as ImageSheetMessage;
                this.appendText("MD5: " + ism.MD5.ToString());
            } else if (msg is InkSheetInformationMessage) {
                InkSheetInformationMessage isim = msg as InkSheetInformationMessage;
                this.appendText("Disposition: " + isim.Disposition.ToString());
            } else if (msg is InkSheetStrokesAddedMessage) {
                InkSheetStrokesAddedMessage issam = msg as InkSheetStrokesAddedMessage;
                this.appendText("Disposition: " + issam.Disposition.ToString());
            } else if (msg is InkSheetStrokesDeletingMessage) {
                InkSheetStrokesDeletingMessage issdm = msg as InkSheetStrokesDeletingMessage;
                this.appendText("Disposition: " + issdm.Disposition.ToString());
            } else if (msg is PresentationInformationMessage) {
                PresentationInformationMessage pim = msg as PresentationInformationMessage;
                this.appendText("Human Name: " + pim.HumanName);
            } else if (msg is RealTimeInkSheetMessage) {
                RealTimeInkSheetMessage rtism = msg as RealTimeInkSheetMessage;
                this.appendText("Disposition: " + rtism.Disposition.ToString());
            } else if (msg is RealTimeInkSheetInformationMessage) {
                RealTimeInkSheetInformationMessage rtisim = msg as RealTimeInkSheetInformationMessage;
                this.appendText("Disposition: " + rtisim.Disposition.ToString());
            } else if (msg is RealTimeInkSheetDataMessage) {
                RealTimeInkSheetDataMessage rtisdm = msg as RealTimeInkSheetDataMessage;
                this.appendText("Stylus ID: " + rtisdm.StylusId.ToString());
                this.appendText("# of Packets: " + rtisdm.Packets.Length);
            } else if (msg is RealTimeInkSheetPacketsMessage) {
                RealTimeInkSheetPacketsMessage rtispm = msg as RealTimeInkSheetPacketsMessage;
                this.appendText("Stylus ID: " + rtispm.StylusId.ToString());
                this.appendText("# of Packets: " + rtispm.Packets.Length);
            } else if (msg is RealTimeInkSheetStylusUpMessage) {
                RealTimeInkSheetStylusUpMessage rtissup = msg as RealTimeInkSheetStylusUpMessage;
                this.appendText("Stylus ID: " + rtissup.StylusId.ToString());
                this.appendText("# of Packets: " + rtissup.Packets.Length);
            } else if (msg is RealTimeInkSheetStylusDownMessage) {
                RealTimeInkSheetStylusDownMessage rtissdm = msg as RealTimeInkSheetStylusDownMessage;
                this.appendText("Stylus ID: " + rtissdm.StylusId.ToString());
                this.appendText("# of Packets: " + rtissdm.Packets.Length);
            } else if (msg is SheetRemovedMessage) {
                SheetRemovedMessage srm = msg as SheetRemovedMessage;
                this.appendText("Disposition: " + srm.Disposition.ToString());
            } else if (msg is SlideInformationMessage) {
                SlideInformationMessage sim = msg as SlideInformationMessage;
                this.appendText("LocalID: " + sim.LocalId.ToString());
                this.appendText("Title: " + sim.Title);
                this.appendText("Zoom: " + sim.Zoom);
                this.appendText("Slide BG Color: " + sim.SlideBackgroundColor);
            } else if (msg is SlideDeletedMessage) {
                SlideDeletedMessage sdm = msg as SlideDeletedMessage;
                this.appendText("ID: " + sdm.TargetId);
            } else if (msg is TextSheetMessage) {
                //Nothing interesting here...
            } else {
                //Unknown!
            }
            //Do the recursive thing
            if (msg.Child != null) {
                this.displayMessageEventRecursive(msg.Child);
            }
        }

        private void appendText(string text) {
            this.infoLabel.Text += text + "\r\n";
        }

        #region Windows Form Designer generated code
        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            this.infoLabel = new System.Windows.Forms.Label();
            this.doubleBufferPanel = new UW.ClassroomPresenter.Test.Network.Player.DoubleBufferPanel();
            this.SuspendLayout();
            // 
            // infoLabel
            // 
            this.infoLabel.Location = new System.Drawing.Point(8, 8);
            this.infoLabel.Name = "infoLabel";
            this.infoLabel.Size = new System.Drawing.Size(384, 376);
            this.infoLabel.TabIndex = 0;
            // 
            // doubleBufferPanel
            // 
            this.doubleBufferPanel.Location = new System.Drawing.Point(80, 400);
            this.doubleBufferPanel.Name = "doubleBufferPanel";
            this.doubleBufferPanel.Size = new System.Drawing.Size(224, 176);
            this.doubleBufferPanel.TabIndex = 1;
            // 
            // NetworkEventInfoForm
            // 
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.ClientSize = new System.Drawing.Size(402, 600);
            this.ControlBox = false;
            this.Controls.Add(this.doubleBufferPanel);
            this.Controls.Add(this.infoLabel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "NetworkEventInfoForm";
            this.Text = "Information";
            this.ResumeLayout(false);

        }
        #endregion

        private void onDoubleBufferPanelPaint(object sender, PaintEventArgs e) {
            if (this.m_Image != null) {
                e.Graphics.DrawImage(this.m_Image, 0, 0, this.doubleBufferPanel.Width, this.doubleBufferPanel.Height);
            }
        }
    }
}
