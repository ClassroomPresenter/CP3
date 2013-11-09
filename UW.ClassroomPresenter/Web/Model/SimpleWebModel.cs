#if WEBSERVER
using System;
using System.Collections;
using System.Text;

namespace UW.ClassroomPresenter.Web.Model {
    // NOTE: This file contains a set of classes that are simplified data-only
    // equivalents to the model representations in the main application. These
    // models are designed to be cloned and compact.

    /// <summary>
    /// A simple model of a presentation for use with the web client.
    /// </summary>
    public class SimpleWebModel : ICloneable {
        #region Members

        public ArrayList Decks = new ArrayList();
        public int CurrentDeck = -1;
        public int CurrentSlide = -1;
        public string Name = "Untitled Presentation";
        public bool AcceptingSS = true;
        public bool AcceptingQP = false;
        public bool ForceLink = true;
        public int PollStyle = 5;
        public int RequestSubmissionSignal = 0;
        public int RequestLogSignal = 0;
        #endregion

        #region ICloneable Members

        /// <summary>
        /// Clone this model.
        /// </summary>
        /// <returns>The cloned model.</returns>
        public object Clone() {
            SimpleWebModel model = new SimpleWebModel();
            foreach (SimpleWebDeck deck in this.Decks) {
                model.Decks.Add(deck.Clone());
            }
            model.CurrentDeck = this.CurrentDeck;
            model.CurrentSlide = this.CurrentSlide;
            model.Name = (string)this.Name.Clone();
            model.AcceptingSS = this.AcceptingSS;
            model.AcceptingQP = this.AcceptingQP;
            model.ForceLink = this.ForceLink;
            model.PollStyle = this.PollStyle;
            model.RequestSubmissionSignal = this.RequestSubmissionSignal;
            model.RequestLogSignal = this.RequestLogSignal;
            return model;
        }

        #endregion
    }

    /// <summary>
    /// A simple model of a deck for use with the web client.
    /// </summary>
    public class SimpleWebDeck : ICloneable {
        public ArrayList Slides = new ArrayList();
        public string Name = "Untitled Deck";

        #region ICloneable Members

        /// <summary>
        /// Clone this model.
        /// </summary>
        /// <returns>The cloned model.</returns>
        public object Clone() {
            SimpleWebDeck deck = new SimpleWebDeck();
            foreach (SimpleWebSlide slide in this.Slides) {
                deck.Slides.Add(slide.Clone());
            }
            deck.Name = (string)this.Name.Clone();
            return deck;
        }

        #endregion
    }

    /// <summary>
    /// A simple model of a slide for use with the web client.
    /// </summary>
    public class SimpleWebSlide : ICloneable {
        public ArrayList Inks = new ArrayList();
        public Guid Id = Guid.Empty;
        public int Index = -1;
        public string Name = "Untitled Slide";

        #region ICloneable Members

        /// <summary>
        /// Clone this model.
        /// </summary>
        /// <returns>The cloned model.</returns>
        public object Clone() {
            SimpleWebSlide slide = new SimpleWebSlide();
            foreach (SimpleWebInk ink in this.Inks) {
                slide.Inks.Add(ink.Clone());
            }
            slide.Id = this.Id;
            slide.Index = this.Index;
            slide.Name = (string)this.Name.Clone();
            return slide;
        }

        #endregion
    }

    /// <summary>
    /// A simple model of a stroke for use with the web client.
    /// </summary>
    public class SimpleWebInk : ICloneable {
        public System.Drawing.Point[] Pts = null;
        public byte R = 0;
        public byte G = 0;
        public byte B = 0;
        public byte Opacity = 255;
        public float Width = 5.0f;

        #region ICloneable Members

        /// <summary>
        /// Clone this model.
        /// </summary>
        /// <returns>The cloned model.</returns>
        public object Clone() {
            SimpleWebInk ink = new SimpleWebInk();
            if (this.Pts != null) {
                ink.Pts = (System.Drawing.Point[])this.Pts.Clone();
            }
            ink.R = this.R;
            ink.G = this.G;
            ink.B = this.B;
            ink.Opacity = this.Opacity;
            ink.Width = this.Width;
            return ink;
        }

        #endregion
    }
}
#endif