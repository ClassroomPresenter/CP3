using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.IO.Packaging;
using System.Windows.Documents;
using System.Windows.Documents.Serialization;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Xps;
using System.Windows.Xps.Packaging;
using System.Windows.Xps.Serialization;
using System.Threading;
using System.Security.Cryptography;
using System.Runtime.Serialization.Formatters.Binary;

using UW.ClassroomPresenter.Model.Presentation;

namespace UW.ClassroomPresenter.Decks
{
    public class XPSDeckIO
    {
        #region Open XPS Method

        public static DeckModel OpenXPS(FileInfo file)
        {
            return XPSDeckIO.OpenXPS(file.FullName);
        }

        public static DeckModel OpenXPS(string file)
        {
            try
            {
                Misc.ProgressBarForm progressForm = new UW.ClassroomPresenter.Misc.ProgressBarForm("Opening \"" + file + "\"...");
                progressForm.Show();

                //Open Xps Document
                XpsDocument xpsDocument = null;

                xpsDocument = new XpsDocument(file, FileAccess.Read);

                //Create two DocumentPaginators for the XPS document. One is for local display, the other one is for the delivery to students.
                DocumentPaginator paginator = xpsDocument.GetFixedDocumentSequence().DocumentPaginator;

                //Create Deck Model
                Guid deckGuid = Guid.NewGuid();
                DeckModel deck = new DeckModel(deckGuid, DeckDisposition.Empty, file);

                using (Synchronizer.Lock(deck.SyncRoot))
                {
                    deck.IsXpsDeck = true;

                    //Iterate over all pages
                    for (int i = 0; i < paginator.PageCount; i++)
                    {
                        DocumentPage page = paginator.GetPage(i);

                        SlideModel newSlideModel = CreateSlide(page, deck);

                        //Create a new Entry + reference SlideModel
                        TableOfContentsModel.Entry newEntry = new TableOfContentsModel.Entry(Guid.NewGuid(), deck.TableOfContents, newSlideModel);
                        //Lock the TOC
                        using (Synchronizer.Lock(deck.TableOfContents.SyncRoot))
                        {
                            //Add Entry to TOC
                            deck.TableOfContents.Entries.Add(newEntry);
                        }

                        //Update the Progress Bar
                        progressForm.UpdateProgress(0, paginator.PageCount , i+1);
                    }
                }
                
                //Close Progress Bar
                progressForm.Close();

                return deck;
            }
            catch (Exception e)
            {
                System.Diagnostics.Trace.WriteLine(e.ToString());
            }
            GC.Collect();

            return null;
        }

        private static SlideModel CreateSlide(DocumentPage page, DeckModel deck)
        {
            int slideWidth = (int)page.Size.Width;
            int slideHeight = (int)page.Size.Height;

            //Create a new SlideModel
            SlideModel newSlideModel = new SlideModel(Guid.NewGuid(), new LocalId(), SlideDisposition.Empty, new Rectangle(0, 0, slideWidth, slideHeight));

            //Create a XPSPageSheetModel for each XPS Page
            XPSPageSheetModel sheet = new XPSPageSheetModel(page, new XPSDocumentPageWrapper(page), Guid.NewGuid(), SheetDisposition.All, new Rectangle(0, 0, slideWidth, slideHeight), slideHeight);

            //Add XPSPageSheetModel into SlideModel
            using (Synchronizer.Lock(newSlideModel.SyncRoot))
            {
                newSlideModel.ContentSheets.Add(sheet);
            }

            deck.InsertSlide(newSlideModel);

            return newSlideModel;
        }

        public class XPSFileOpenException : ApplicationException
        {
            public XPSFileOpenException(string message, Exception innerException) : base(message, innerException) { }
        }

        #endregion Open XPS Method       
    }

    /// <summary>
    /// Binary Wrapper for XPS Documen Page 
    /// </summary>
    [Serializable]
    public class XPSDocumentPageWrapper
    {
        private byte[] m_XPSDocumentPageObject;
        private static int tempFileCtr=0;

        public XPSDocumentPageWrapper(DocumentPage documentPage)
        {
            if (documentPage != null)
            {
                //Serialize the DocumentPage
                XpsSerializerFactory factory = new XpsSerializerFactory();
                MemoryStream ms = new MemoryStream();
                SerializerWriter writer=  factory.CreateSerializerWriter(ms);
                writer.Write(documentPage.Visual);
                m_XPSDocumentPageObject = ms.ToArray();
            }
        }
       
        /// <summary>
        /// Revert the DocumentPage form bytes
        /// </summary>
        public DocumentPage XPSDocumentPage
        {
            get
            {
                DocumentPage page = null;
                if (m_XPSDocumentPageObject == null)
                {
                    return null;
                }
                try
                {
                    //Get TempFile Collection
                    TempFileCollection tempFileCollection = new TempFileCollection();
                    string dirpath = tempFileCollection.BasePath;
                    if (!Directory.Exists(dirpath))
                    {
                        Directory.CreateDirectory(dirpath);
                    }
                    else
                    {
                        Directory.Delete(dirpath, true);
                        Directory.CreateDirectory(dirpath);
                    }

                    //Save the XPSDocumentPage to temp xps file.
                    string fileName = GenerateFileName();
                    fileName = dirpath + "\\" + fileName + ".xps";
                    FileStream stream = new FileStream(fileName, FileMode.Create);
                    stream.Write(m_XPSDocumentPageObject, 0, m_XPSDocumentPageObject.Length);
                    stream.Close();
                    tempFileCollection.AddFile(fileName, false);

                    //Open the xps file for the page, and get the page content
                    XpsDocument xpsDoc = new XpsDocument(fileName, FileAccess.Read);
                    FixedDocumentSequence df = xpsDoc.GetFixedDocumentSequence();
                    page = df.DocumentPaginator.GetPage(0);
                    xpsDoc.Close();

                    tempFileCollection.Delete();
                    Directory.Delete(dirpath);
                }
                catch (Exception e)
                {
                    System.Diagnostics.Trace.WriteLine("XPSDocumentPageWrapper deserialization failed.  " + e.ToString());
                }
                return page;
            }
        }

        private static string GenerateFileName()
        {
            string result = "xpstmpfile" + tempFileCtr;
            tempFileCtr++;

            return result;
        }
    }
}
