#if WEBSERVER
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace UW.ClassroomPresenter.Web.Model {
    /// <summary>
    /// Class that keeps a history of SimpleWebModel object and can
    /// calculate the diff between any two of those models when requested.
    /// </summary>
    public class SimpleModelHistory {
        #region Properties

        /// <summary>
        /// History of SimpleWebModels index by sequence number.
        /// </summary>
        protected Dictionary<int, SimpleWebModel> History = new Dictionary<int,SimpleWebModel>();
        
        /// <summary>
        /// The maximum history size we want to make sure we have.
        /// </summary>
        protected int MaximumEntries = 100;

        /// <summary>
        /// The maximum history that our data structure actually stores.
        /// </summary>
        protected int MaximumBufferEntries = 200;

        /// <summary>
        /// The current sequence number.
        /// </summary>
        protected int CurrentSequenceNumber = 0;

        /// <summary>
        /// Getter for the current sequence number.
        /// </summary>
        public int SequenceNumber {
            get {
                lock (this) {
                    return CurrentSequenceNumber;
                }
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Construct the data structure with the default history size.
        /// </summary>
        public SimpleModelHistory() {
            this.MaximumEntries = 100;
            this.MaximumBufferEntries = 200;
        }

        /// <summary>
        /// Construct the data structure by specifying the history size.
        /// </summary>
        /// <param name="historySize">The size of the history.</param>
        public SimpleModelHistory(int historySize) {
            if(historySize < 5)
                historySize = 5;
            this.MaximumEntries = historySize;
            this.MaximumBufferEntries = this.MaximumEntries * 2;
        }

        #endregion

        #region Adding Models

        /// <summary>
        /// Append a new model to the history.
        /// </summary>
        /// <param name="model">The model to append.</param>
        /// <returns>The sequence number of this event.</returns>
        public int AddModel(SimpleWebModel model) {
            int sequenceNumber = 0;
            lock (this) {
                // Add the next sequence number
                this.CurrentSequenceNumber++;
                sequenceNumber = this.CurrentSequenceNumber;
                this.History.Add(this.CurrentSequenceNumber, model);

                // Occassionally remove a bunch of the web models
                if (this.History.Count > this.MaximumBufferEntries) {
                    // Get the keys and sort them
                    int[] keys = new int[this.History.Count];
                    this.History.Keys.CopyTo(keys, 0);
                    Array.Sort<int>(keys);

                    // Calculate how many we need to remove (N)
                    int stopIndex = keys.Length - this.MaximumEntries;

                    // Remove the smallest N keys
                    for (int i = 0; i < stopIndex; i++) {
                        if (this.History.ContainsKey(keys[i])) {
                            this.History.Remove(keys[i]);
                        }
                    }
                }
            }
            return sequenceNumber;
        }

        #endregion

        #region JSON

        /// <summary>
        /// Get the difference between the models with the given sequence numbers. If the
        /// sequence numbers are the same, this should still return a valid empty JSON object.
        /// </summary>
        /// <param name="first">The sequence number of the first model.</param>
        /// <param name="second">The sequence number of the second model.</param>
        /// <returns>The JSON string of the difference between the models (second - first).</returns>
        public string GetModelDiff(int first, int second) {
            // Check the arguments
            if (first < 0 || second < 0 || first > second) {
                throw new ArgumentException("The sequence numbers must be positive and the first number must be smaller than the second.");
            }

            SimpleWebModel firstModel, secondModel;

            lock (this) {
                // Get the first model
                if (this.History.ContainsKey(first)) {
                    firstModel = this.History[first];
                }
                else {
                    // If the first sequence number isn't in the history, we can just return the whole model.
                    firstModel = null;
                    first = 0;
                }

                // Get the second model
                if (this.History.ContainsKey(second)) {
                    secondModel = this.History[second];
                }
                else {
                    throw new ArgumentException("The sequence numbers must be positive and the first number must be smaller than the second.");
                }
            }

            return PrintModelString(BuildModel(first, firstModel, second, secondModel));
        }

        /// <summary>
        /// Get the difference between the current model and the model with the given sequence number.
        /// </summary>
        /// <param name="first">The first sequence number.</param>
        /// <returns>The difference string.</returns>
        public string GetModelDiff(int first) {
            return this.GetModelDiff(first, this.SequenceNumber);
        }

        /// <summary>
        /// Get the current model string.
        /// </summary>
        /// <returns>The current model string.</returns>
        public string GetCurrentModel() {
            return this.GetModelDiff(0, this.SequenceNumber);
        }

        #endregion

        #region String Constants

        // NOTE: These constants should match those in the web client.

        // Global Encoding Strings
        protected const string StartSNString = "\"s\"";
        protected const string EndSNString = "\"e\"";
        protected const string ModelDataString = "\"d\"";

        // Presentation Encoding Strings
        protected const string ModelDataName = "\"n\"";
        protected const string ModelDataCurrentDeck = "\"d\"";
        protected const string ModelDataCurrentSlide = "\"s\"";
        protected const string ModelDataForceLink = "\"f\"";
        protected const string ModelDataAcceptingSS = "\"a\"";
        protected const string ModelDataAcceptingQP = "\"q\"";
        protected const string ModelDataPollStyle = "\"p\"";
        protected const string ModelDataRequestSubmission = "\"grs\"";
        protected const string ModelDataRequestLog = "\"grl\"";
        protected const string ModelDataDecks = "\"k\"";

        // Deck Encoding Strings
        protected const string DeckName = "\"n\"";
        protected const string DeckSlides = "\"s\"";

        // Slide Encoding Strings
        protected const string SlideIndex = "\"i\"";
        protected const string SlideName = "\"n\"";
        protected const string SlideImagePath = "\"u\"";
        protected const string SlideStrokes = "\"k\"";

        // Stroke Encoding Strings
        protected const string StrokePts = "\"k\"";
        protected const string StrokeColor = "\"c\"";
        protected const string StrokeOpacity = "\"o\"";
        protected const string StrokeWidth = "\"w\"";

        // Color Encoding Strings
        protected const string ColorR = "\"r\"";
        protected const string ColorG = "\"g\"";
        protected const string ColorB = "\"b\"";

        #endregion

        #region Model

        /// <summary>
        /// Print out the model string recursively using reflection on the model object.
        /// NOTE: The model object may only consist of some very basic types.
        /// </summary>
        /// <param name="model">The object to print.</param>
        /// <returns>The resulting string.</returns>
        protected string PrintModelString(object model) {
            StringBuilder result = new StringBuilder();

            if(model is List<KeyValuePair<string, object>>) {   // JSON OBJECT
                List<KeyValuePair<string, object>> obj = (List<KeyValuePair<string,object>>)model;
                result.Append("{");
                for(int i=0; i<obj.Count; i++) {
                    result.Append(PrintModelString(obj[i]));
                    if (i + 1 < obj.Count) result.Append(",");
                }
                result.Append("}");
            } else if(model is List<System.Drawing.Point>) {    // POINT ARRAY
                List<System.Drawing.Point> obj = (List<System.Drawing.Point>)model;
                result.Append("[");
                for(int i=0; i<obj.Count; i++) {
                    result.Append(obj[i].X + "," + obj[i].Y);
                    if (i + 1 < obj.Count) result.Append(",");
                }
                result.Append("]");
            } else if(model is List<object>) {                  // JSON ARRAY
                result.Append("[");
                List<object> obj = (List<object>)model;
                for (int i = 0; i < obj.Count; i++) {
                    if (obj[i] == null) {
                        result.Append("{}");
                    }
                    else {
                        result.Append(PrintModelString(obj[i]));
                    }
                    if (i + 1 < obj.Count) result.Append(",");
                }
                result.Append("]");
            } else if(model is KeyValuePair<string, object>) {  // JSON KEYVALUE PAIR
                KeyValuePair<string, object> obj = (KeyValuePair<string,object>)model;
                result.Append(obj.Key + ":" + PrintModelString(obj.Value));
            } else if(model is string){                                            // JSON VALUE
                result.Append("\"" + model.ToString() + "\"");
            }
            else if (model is bool) {
                result.Append((((bool)model) == true) ? "true" : "false");
            }
            else {
                result.Append(model.ToString());
            }

            return result.ToString();
        }

        /// <summary>
        /// Helper to create a simple data structure representing the model. This data structure will have a
        /// one-to-one conversion to JSON.
        /// </summary>
        /// <param name="firstNum">The first sequence number</param>
        /// <param name="first">The first model</param>
        /// <param name="secondNum">The second sequence number</param>
        /// <param name="second">The second model</param>
        /// <returns>The data structure.</returns>
        protected List<KeyValuePair<string,object>> BuildModel(int firstNum, SimpleWebModel first, int secondNum, SimpleWebModel second) {
            List<KeyValuePair<string,object>> model = new List<KeyValuePair<string,object>>();

            // Add the global data.
            model.Add(new KeyValuePair<string,object>(StartSNString, firstNum));
            model.Add(new KeyValuePair<string,object>(EndSNString, secondNum));

            // Optionally, add the presentation data.
            List<KeyValuePair<string,object>> modelData = BuildModelData(first, second);
            if(modelData != null) {
                model.Add(new KeyValuePair<string,object>(ModelDataString, modelData));
            }

            return model;
        }

        /// <summary>
        /// Build the values for the model presentation.
        /// </summary>
        /// <param name="first">The first model.</param>
        /// <param name="second">The second model.</param>
        /// <returns>The data structure.</returns>
        protected List<KeyValuePair<string,object>> BuildModelData(SimpleWebModel first, SimpleWebModel second) {
            List<KeyValuePair<string,object>> model = new List<KeyValuePair<string,object>>();

            // Optionally, add the presentation name.
            if(first == null || first.Name != second.Name) {
                model.Add(new KeyValuePair<string,object>(ModelDataName, second.Name));
            }
            // Optionally, add the presentation current deck index.
            if (first == null || first.CurrentDeck != second.CurrentDeck) {
                model.Add(new KeyValuePair<string,object>(ModelDataCurrentDeck, second.CurrentDeck));
            }
            // Optionally, add the presentation current slide index.
            if (first == null || first.CurrentSlide != second.CurrentSlide) {
                model.Add(new KeyValuePair<string,object>(ModelDataCurrentSlide, second.CurrentSlide));
            }
            // Optionally, add the presentation force navigation value.
            if (first == null || first.ForceLink != second.ForceLink) {
                model.Add(new KeyValuePair<string,object>(ModelDataForceLink, second.ForceLink));
            }
            // Optionally, add the presentation student submission enabled value.
            if (first == null || first.AcceptingSS != second.AcceptingSS) {
                model.Add(new KeyValuePair<string,object>(ModelDataAcceptingSS, second.AcceptingSS));
            }
            // Optionally, add the presentation quick poll enabled value.
            if (first == null || first.AcceptingQP != second.AcceptingQP) {
                model.Add(new KeyValuePair<string,object>(ModelDataAcceptingQP, second.AcceptingQP));
            }
            // Optionally, add the presentation quick poll type value.
            if (first == null || first.PollStyle != second.PollStyle) {
                model.Add(new KeyValuePair<string,object>(ModelDataPollStyle, second.PollStyle));
            }
            // Optionally, add the request submission signal value.
            if (first == null || first.RequestSubmissionSignal != second.RequestSubmissionSignal) {
                model.Add(new KeyValuePair<string, object>(ModelDataRequestSubmission, second.RequestSubmissionSignal));
            }
            // Optionally, add the request log signal value.
            if (first == null || first.RequestLogSignal != second.RequestLogSignal) {
                model.Add(new KeyValuePair<string, object>(ModelDataRequestLog, second.RequestLogSignal));
            }
            // Optionall, add the decks data.
            List<object> decksData = BuildDecksData(first, second);
            if(decksData != null) {
                model.Add(new KeyValuePair<string,object>(ModelDataDecks, decksData));
            }

            // Return the result only if we added/updated data.
            if(model.Count > 0)
                return model;
            else
                return null;
        }

        #endregion

        #region Decks

        /// <summary>
        /// Build the values for the model decks.
        /// </summary>
        /// <param name="first">The first model.</param>
        /// <param name="second">The second model.</param>
        /// <returns>The data structure for the decks array.</returns>
        protected List<object> BuildDecksData(SimpleWebModel first, SimpleWebModel second) {
            bool nonNull = false;
            List<object> decks = new List<object>();

            // We need an entry for every deck in the second model.
            for (int i = 0; i < second.Decks.Count; i++) {
                // Optionally, get the first deck if this deck existed in the first model.
                SimpleWebDeck firstDeck = null;
                if (first != null && i < first.Decks.Count) {
                    firstDeck = (SimpleWebDeck)first.Decks[i];
                }

                // Get the difference between the two decks, if the decks are the same add null.
                List<KeyValuePair<string, object>> deckData = BuildDeckData(firstDeck, (SimpleWebDeck)second.Decks[i]);
                if (deckData != null) {
                    decks.Add(deckData);
                    nonNull = true;
                } else {
                    decks.Add(null);
                }
            }

            // Return the result only if some deck was updated.
            if (nonNull == true && decks.Count > 0)
                return decks;
            else
                return null;
        }

        /// <summary>
        /// Build the values for a single model deck.
        /// </summary>
        /// <param name="first">The first deck.</param>
        /// <param name="second">The second deck.</param>
        /// <returns>The data structure for the deck difference.</returns>
        protected List<KeyValuePair<string, object>> BuildDeckData(SimpleWebDeck first, SimpleWebDeck second) {
            List<KeyValuePair<string, object>> deck = new List<KeyValuePair<string, object>>();

            // Optionally, add the deck name.
            if (first == null || first.Name != second.Name) {
                deck.Add(new KeyValuePair<string, object>(DeckName, second.Name));
            }
            // Optionally, add the deck slides.
            List<object> slides = BuildSlidesData(first, second);
            if (slides != null) {
                deck.Add(new KeyValuePair<string, object>(DeckSlides, slides));
            }

            // Return the result only if we added/updated data.
            if (deck.Count > 0)
                return deck;
            else
                return null;
        }

        #endregion

        #region Slides

        /// <summary>
        /// Build the values for the model slides.
        /// </summary>
        /// <param name="first">The first deck.</param>
        /// <param name="second">The second deck.</param>
        /// <returns>The data structure for the slides array.</returns>
        protected List<object> BuildSlidesData(SimpleWebDeck first, SimpleWebDeck second) {
            bool nonNull = false;
            List<object> slides = new List<object>();

            // We need an entry for every slide in the second model.
            for (int i = 0; i < second.Slides.Count; i++) {
                // Optionally, get the first slide if this slide existed in the first model.
                SimpleWebSlide firstSlide = null;
                if (first != null && i < first.Slides.Count) {
                    firstSlide = (SimpleWebSlide)first.Slides[i];
                }

                // Get the difference between the two slides, if the slides are the same add null.
                List<KeyValuePair<string, object>> slideData = BuildSlideData(firstSlide, (SimpleWebSlide)second.Slides[i], i, second.Name);
                if (slideData != null) {
                    slides.Add(slideData);
                    nonNull = true;
                } else {
                    slides.Add(null);
                }
            }

            // Return the result only if some slide was updated.
            if (nonNull == true && slides.Count > 0)
                return slides;
            else
                return null;
        }

        /// <summary>
        /// Build the values for a single deck slide.
        /// </summary>
        /// <param name="first">The first slide.</param>
        /// <param name="second">The second slide.</param>
        /// <param name="index">The slide index.</param>
        /// <param name="deckName">The deck name, needed to construct the background image path.</param>
        /// <returns>The data structure for the slide difference.</returns>
        protected List<KeyValuePair<string, object>> BuildSlideData(SimpleWebSlide first, SimpleWebSlide second, int index, string deckName) {
            List<KeyValuePair<string, object>> slide = new List<KeyValuePair<string, object>>();

            // Optionally, add the slide index.
            if (first == null || first.Index != second.Index) {
                slide.Add(new KeyValuePair<string, object>(SlideIndex, second.Index));
            }
            // Optionally, add the slide name.
            if (first == null || first.Name != second.Name) {
                slide.Add(new KeyValuePair<string, object>(SlideName, second.Name));
            }
            // Optionally, add the slide background image path.
            if (first == null || first.Id != second.Id) {
                slide.Add(new KeyValuePair<string, object>(SlideImagePath, "./images/decks/" + deckName + "/" + deckName + "/" + deckName + "_" + String.Format("{0:000}", index + 1) + ".png"));
            }
            // Optionally, add the slide strokes.
            List<object> strokes = BuildStrokesData(first, second);
            if (strokes != null) {
                slide.Add(new KeyValuePair<string, object>(SlideStrokes, strokes));
            }

            // Return the result only if we added/updated data.
            if (slide.Count > 0)
                return slide;
            else
                return null;
        }

        #endregion

        #region Inks

        /// <summary>
        /// Build the values for the slide strokes.
        /// </summary>
        /// <param name="first">The first slide.</param>
        /// <param name="second">The second slide.</param>
        /// <returns>The data structure for the strokes array.</returns>
        protected List<object> BuildStrokesData(SimpleWebSlide first, SimpleWebSlide second) {
            bool nonNull = false;
            List<object> strokes = new List<object>();

            // We need an entry for every stroke in the second model.
            for (int i = 0; i < second.Inks.Count; i++) {
                // Optionally, get the first stroke if this stroke existed in the first model.
                SimpleWebInk firstInk = null;
                if (first != null && i < first.Inks.Count) {
                    firstInk = (SimpleWebInk)first.Inks[i];
                }

                // Get the difference between the two strokes, if the strokes are the same add null.
                List<KeyValuePair<string, object>> strokeData = BuildStrokeData(firstInk, (SimpleWebInk)second.Inks[i]);
                if (strokeData != null) {
                    strokes.Add(strokeData);
                    nonNull = true;
                } else {
                    strokes.Add(null);
                }
            }

            // Return the result only if some stroke was updated.
            if (nonNull == true && strokes.Count > 0)
                return strokes;
            else
                return null;
        }

        /// <summary>
        /// Build the values for a single slide stroke.
        /// </summary>
        /// <param name="first">The first ink.</param>
        /// <param name="second">The second ink.</param>
        /// <returns>The data structure for the stroke difference.</returns>
        protected List<KeyValuePair<string,object>> BuildStrokeData(SimpleWebInk first, SimpleWebInk second) {
            List<KeyValuePair<string, object>> stroke = new List<KeyValuePair<string, object>>();

            // Optionally, add the opacity.
            if (first == null || first.Opacity != second.Opacity) {
                stroke.Add(new KeyValuePair<string, object>(StrokeOpacity, second.Opacity));
            }

            // Optionally, add the width.
            if (first == null || first.Width != second.Width) {
                stroke.Add(new KeyValuePair<string, object>(StrokeWidth, second.Width));
            }

            // Optionally, add the color.
            if (first == null || first.R != second.R || first.G != second.G || first.B != second.B) {
                List<KeyValuePair<string, object>> colorData = new List<KeyValuePair<string, object>>();
                colorData.Add(new KeyValuePair<string, object>(ColorR, second.R));
                colorData.Add(new KeyValuePair<string, object>(ColorG, second.G));
                colorData.Add(new KeyValuePair<string, object>(ColorB, second.B));
                stroke.Add(new KeyValuePair<string, object>(StrokeColor, colorData));
            }

            // Optionally, add the stroke points.
            List<System.Drawing.Point> pts = BuildInkData((first != null) ? first.Pts : null, second.Pts);
            if (pts != null) {
                stroke.Add(new KeyValuePair<string, object>(StrokePts, pts));
            }

            // Return the result only if we added/updated data.
            if (stroke.Count > 0)
                return stroke;
            else
                return null;
        }

        /// <summary>
        /// Return the points in a stroke if the two strokes are different.
        /// Unlike previous lists, the stroke points are all or nothing.
        /// That is if anything has changed we resend the entire set of stroke points.
        /// </summary>
        /// <param name="first">The first set of points.</param>
        /// <param name="second">The second set of points.</param>
        /// <returns>The stroke points or null if they are the same.</returns>
        protected List<System.Drawing.Point> BuildInkData(System.Drawing.Point[] first, System.Drawing.Point[] second) {
            List<System.Drawing.Point> pts = new List<System.Drawing.Point>(second);
            bool isDifferent = false;

            // Short-circuit if the lengths are different since we know we need to send the array again.
            if (first == null || first.Length != second.Length) {
                isDifferent = true;
            } else {
                // Check if they have the same data
                for (int i = 0; i < second.Length; i++) {
                    if (!first[i].Equals(second[i])) {
                        isDifferent = true;
                        break;
                    }
                }
            }

            // Return the result only if we added data.
            if (isDifferent)
                return pts;
            else
                return null;
        }

        #endregion
    }
}
#endif