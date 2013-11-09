// $Id: CSDLibraryTest.cs 774 2005-09-21 20:22:33Z pediddle $

using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using System.Data;
using UW.ClassroomPresenter.CSDLibrary;

namespace UW.ClassroomPresenter.CSDLibraryTest
{
    /// <summary>
    /// Summary description for Form1.
    /// </summary>
    public class CSDLibraryTest : System.Windows.Forms.Form
    {
        private System.Windows.Forms.RichTextBox outputTextBox;
        private System.Windows.Forms.Button TOCTestButton;
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.Container components = null;

        private int currentTest = 0;
        private bool result = true;

        public CSDLibraryTest()
        {
            //
            // Required for Windows Form Designer support
            //
            InitializeComponent();

            //
            // TODO: Add any constructor code after InitializeComponent call
            //
        }

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        protected override void Dispose( bool disposing )
        {
            if( disposing )
            {
                if (components != null) 
                {
                    components.Dispose();
                }
            }
            base.Dispose( disposing );
        }

        #region Windows Form Designer generated code
        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.TOCTestButton = new System.Windows.Forms.Button();
            this.outputTextBox = new System.Windows.Forms.RichTextBox();
            this.SuspendLayout();
            // 
            // TOCTestButton
            // 
            this.TOCTestButton.Location = new System.Drawing.Point(8, 16);
            this.TOCTestButton.Name = "TOCTestButton";
            this.TOCTestButton.TabIndex = 0;
            this.TOCTestButton.Text = "TOC Test";
            this.TOCTestButton.Click += new System.EventHandler(this.TOCButton_Click);
            // 
            // outputTextBox
            // 
            this.outputTextBox.Font = new System.Drawing.Font("Lucida Console", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((System.Byte)(0)));
            this.outputTextBox.Location = new System.Drawing.Point(8, 344);
            this.outputTextBox.Name = "outputTextBox";
            this.outputTextBox.ReadOnly = true;
            this.outputTextBox.Size = new System.Drawing.Size(696, 280);
            this.outputTextBox.TabIndex = 1;
            this.outputTextBox.Text = "";
            this.outputTextBox.WordWrap = false;
            // 
            // CSDLibraryTest
            // 
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.ClientSize = new System.Drawing.Size(712, 630);
            this.Controls.Add(this.outputTextBox);
            this.Controls.Add(this.TOCTestButton);
            this.Name = "CSDLibraryTest";
            this.Text = "CSDLibrary Test";
            this.ResumeLayout(false);

        }
        #endregion

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main() 
        {
            Application.Run(new CSDLibraryTest());
        }

        private void TOCButton_Click(object sender, System.EventArgs e) {
            //TOC Simple Button
            //Test 1 = Run a simple test case of adding slides and using NextSlide to retrieve them
            this.beginTest("Add + NextSlide");
            this.display("Actual LID :: Expected LID");
            TableOfContents toc = new TableOfContents();
            toc.Root.AddEntry(new TOCEntry(TOCEntry.TOCEntryContents.SlideCollection));
            
            //Create a bunch of slides
            ArrayList slides = new ArrayList();
            for (int i = 0; i < 10; i++) {
                slides.Add(new Slide(new byte[1]));
            }

            //Add slides to TOC
            for (int i = 0; i < slides.Count; i++) {
                toc.Root[0].AddSlide((Slide)slides[i]);
            }

            //Retrieve the slides and see if they are the same
            for (int i = 0; i < slides.Count; i++) {
                String currentSlide = toc.NextSlide();
                this.display(currentSlide + " :: " + ((Slide)slides[i]).LocalID);
                if (currentSlide != ((Slide)slides[i]).LocalID) {
                    this.testFailed();
                }
            }
            
            //Display results
            this.endTest();
            
            //Test 2 = Bounds checking on NextSlide()
            this.beginTest("NextSlide Bounds Check");
            try {
                toc.NextSlide();
                toc.NextSlide();
            } catch (TOCNodeException f) {
                this.display(f.ToString());
            } catch (Exception f) {
                this.display(f.ToString());
                this.testFailed();
            }

            //Display results
            this.endTest();
            
            //Test 3 = Test PrevSlide()
            this.beginTest("PrevSlide");
            this.display("Actual LID :: Expected LID");
            
            for (int i = slides.Count - 2; i >= 0; i--) {
                String currentSlide = toc.PrevSlide();
                this.display(currentSlide + " :: " + ((Slide)slides[i]).LocalID);
                if (currentSlide != ((Slide)slides[i]).LocalID) {
                    this.testFailed();
                }
            }

            //Display results
            this.endTest();

            //Test 4 = Bounds checking on PrevSlide()
            this.beginTest("PrevSlide Bounds Check");
            try {
                toc.PrevSlide();
                toc.PrevSlide();
            } catch (TOCNodeException f) {
                this.display(f.ToString());
            } catch (Exception f) {
                this.display(f.ToString());
                this.testFailed();
            }

            //Display results
            this.endTest();

            //Test 5 = Insert Slides
            this.beginTest("Insert");

            //Insert one at the begining
            slides.Insert(0, new Slide(new byte[1]));
            toc.Root[0].InsertSlide(0, (Slide)slides[0]);

            //Insert one at the end
            slides.Add(new Slide(new byte[1]));
            toc.Root[0].InsertSlide(toc.Root[0].Count, (Slide)slides[slides.Count - 1]);

            //Insert some in the middle
            Random r = new Random();
            for (int i = 0; i < 5; i++) {
                int index = r.Next(1, 10);
                slides.Insert(index, new Slide(new byte[1]));
                toc.Root[0].InsertSlide(index, (Slide)slides[index]);
            }

            //Retrieve the slides and see if they are the same
            toc.Root[0].ResetCurrentIndex();
            for (int i = 0; i < slides.Count; i++) { 
                String currentSlide = toc.NextSlide();
                this.display(currentSlide + " :: " + ((Slide)slides[i]).LocalID);
                if (currentSlide != ((Slide)slides[i]).LocalID) {
                    this.testFailed();
                }
            }

            //Display results
            this.endTest();

            //Test 6 = Boundary Testing for Insert slides
            this.beginTest("Insert Bounds Check");

            //Add to negative
            try {
                toc.Root[0].InsertSlide(-5, new Slide(new byte[1]));
                this.testFailed();
            } catch (TOCEntryException f) {
                //Good
                this.display(f.ToString());
            } catch (Exception f) {
                //Bad...
                this.display(f.ToString());
                this.testFailed();
            }

            //Add past end
            try {
                toc.Root[0].InsertSlide(1000, new Slide(new byte[1]));
                this.testFailed();
            } catch (TOCEntryException f) {
                //Good
                this.display(f.ToString());
            } catch (Exception f) {
                //Bad...
                this.display(f.ToString());
                this.testFailed();
            }

            //Display results
            this.endTest();

            //Test 7 = GetSlide
            this.beginTest("GetSlide");

            for (int i = 0; i < slides.Count; i++) {
                String currentSlide = toc.Root[0].GetSlide(i);
                this.display(currentSlide + " :: " + ((Slide)slides[i]).LocalID);
                if (currentSlide != ((Slide)slides[i]).LocalID) {
                    this.testFailed();
                }
            }

            this.endTest();

            //Test 8 = Bounds check on GetSlide
            this.beginTest("GetSlide Bounds Check");

            try {
                toc.Root[0].GetSlide(-10);
                this.testFailed();
            } catch (TOCEntryException f) {
                //Good
                this.display(f.ToString());
            } catch (Exception f) {
                //Bad
                this.display(f.ToString());
                this.testFailed();
            }

            try {
                toc.Root[0].GetSlide(1000);
                this.testFailed();
            } catch (TOCEntryException f) {
                //Good
                this.display(f.ToString());
            } catch (Exception f) {
                //Bad
                this.display(f.ToString());
                this.testFailed();
            }

            this.endTest();

            //Test 9 = Remove slide test
            this.beginTest("Remove");

            //Remove at begining
            slides.RemoveAt(0);
            toc.Root[0].RemoveSlide(0);

            //Remove at end
            int end = slides.Count - 1;
            slides.RemoveAt(end);
            toc.Root[0].RemoveSlide(end);

            //Remove some in the middle
            for (int i = 0; i < 5; i++) {
                int index = r.Next(1, slides.Count);
                slides.RemoveAt(index);
                toc.Root[0].RemoveSlide(index);
            }

            //Compare
            for (int i = 0; i < slides.Count; i++) {
                String currentSlide = toc.Root[0].GetSlide(i);
                this.display(currentSlide + " :: " + ((Slide)slides[i]).LocalID);
                if (currentSlide != ((Slide)slides[i]).LocalID) {
                    this.testFailed();
                }
            }
            
            this.endTest();

            //Test 10 = Remove boundary testing
            this.beginTest("RemoveSlide Boundary Check");

            try {
                toc.Root[0].RemoveSlide(-10);
                this.testFailed();
            } catch (TOCEntryException f) {
                //Good
                this.display(f.ToString());
            } catch (Exception f) {
                //Bad
                this.display(f.ToString());
                this.testFailed();
            }

            try {
                toc.Root[0].RemoveSlide(1000);
                this.testFailed();
            } catch (TOCEntryException f) {
                //Good
                this.display(f.ToString());
            } catch (Exception f) {
                //Bad
                this.display(f.ToString());
                this.testFailed();
            }

            this.endTest();
        }

        private string getResult() {
            if (this.result) {
                return "PASS";
            } else {
                return "FAIL";
            }
        }

        private void display(string text) {
            this.outputTextBox.Text += text + "\n";
        }

        private void beginTest(string desc) {
            this.result = true;
            this.currentTest++;
            this.display("***TEST " + this.currentTest + ": " + desc + "***");
        }

        private void testFailed() {
            this.result = false;
        }

        private void endTest() {
            this.display("TEST " + this.currentTest + " RESULTS: " + this.getResult() + "\n");
        }
    }
}
