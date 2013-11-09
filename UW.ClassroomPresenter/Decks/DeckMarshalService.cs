// $Id: DeckMarshalService.cs 2233 2013-09-27 22:17:28Z fred $

using System;
using System.Diagnostics;
using System.IO;
using System.ComponentModel;
using System.Windows.Forms;

using UW.ClassroomPresenter.Model.Presentation;
using Decks = UW.ClassroomPresenter.Decks;

namespace UW.ClassroomPresenter.Decks {
    /// <summary>
    /// An implementation of this abstract class is responsible for writing and reading
    /// <see cref="DeckModel"><c>DeckModel</c>s</see> to and from files.
    /// </summary>
    public abstract class DeckMarshalService : IDisposable {
        public DeckMarshalService() {
        }

        ~DeckMarshalService() {
            this.Dispose(false);
        }

        public void Dispose() {
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) { }

        public abstract DeckModel ReadDeck(FileInfo file);
        public abstract DeckModel ReadDeckAsync(FileInfo file, BackgroundWorker worker, DoWorkEventArgs progress);
        public abstract void SaveDeck(FileInfo file, DeckModel deck);
    }

    public class DefaultDeckMarshalService : DeckMarshalService {
        public DefaultDeckMarshalService() {
        }

        public override DeckModel ReadDeck(FileInfo file) {
            return this.ReadDeckAsync(file, null, null);
        }

        //TODO: Is returning null a good thing here?
        public override DeckModel ReadDeckAsync(FileInfo file, BackgroundWorker worker, DoWorkEventArgs progress) {
            try {
                if (file.Extension.ToLower() == ".ppt" || file.Extension.ToLower() == ".pptx") {
                    return Decks.PPTDeckIO.OpenPPT(file, worker, progress);
                }
                else if (file.Extension == ".cp3") {
                    return Decks.PPTDeckIO.OpenCP3(file);
                }
                else if (file.Extension == ".xps") {
                    return Decks.XPSDeckIO.OpenXPS(file);
                }
                else
                {
                    return null;
                }
            }
            catch (Decks.PPTDeckIO.PPTNotInstalledException ex) {
                Trace.WriteLine(ex.Message + "; InnerException: " + ex.InnerException.ToString());
                MessageBox.Show(null, "An error occurred while opening \""
                    + file.Name + "\".  It appears that PowerPoint is not installed on the local system, " + 
                    "or is not a compatible version.  See Help for more information.",
                    "Classroom Presenter 3", MessageBoxButtons.OK, MessageBoxIcon.Exclamation,
                    MessageBoxDefaultButton.Button1);
                return null;
            }
            catch (Decks.PPTDeckIO.PPTFileOpenException ex) {
                Trace.WriteLine(ex.Message + "; InnerException: " + ex.InnerException.ToString());
                MessageBox.Show(null, "An error occurred while opening \""
                    + file.Name + "\".  The file could not be opened by the version of PowerPoint " +
                    "installed on the local system.  Possibly the file was created by a newer version " + 
                    "of PowerPoint.  See Help for more information.",
                    "Classroom Presenter 3", MessageBoxButtons.OK, MessageBoxIcon.Exclamation,
                    MessageBoxDefaultButton.Button1);
                return null;
            }
            catch (Exception ex) {
                Trace.WriteLine(this.GetType().ToString()
                    + ": Failed to open \""
                    + file.Name + "\": "
                    + ex.ToString());
                Trace.Indent();
                Trace.WriteLine(ex.StackTrace);
                Trace.Unindent();
                MessageBox.Show(null, "An error occurred while opening \""
                    + file.Name + "\".  File is corrupted or is incompatible with the current version.",
                    "Classroom Presenter 3", MessageBoxButtons.OK, MessageBoxIcon.Exclamation,
                    MessageBoxDefaultButton.Button1);
                return null;
            }
        }

        public override void SaveDeck(FileInfo file, DeckModel deck) {
            Decks.PPTDeckIO.SaveAsCP3(file, deck);
        }
    }
}
