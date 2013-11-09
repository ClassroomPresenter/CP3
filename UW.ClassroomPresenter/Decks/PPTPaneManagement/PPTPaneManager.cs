// $Id: PPTPaneManager.cs 775 2005-09-21 20:42:02Z pediddle $

using System;
using System.Collections;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;
using Core = Microsoft.Office.Core;

namespace PPTPaneManagement {
    /// <summary>
    /// Summary description for PPTPaneManager.
    /// </summary>
    /// <remarks>
    /// Tracks the visibility of individual shapes through a tag
    /// related to its GUID/name. Tracks the mode of a presentation through
    /// its tags. A new presentation defaults to public visibility.</remarks>
    ///
    /// <remarks>
    /// All shapes in a presentation should be tagged as soon as that
    /// presentation is loaded with a default tag (non-restricted seems best)
    /// and the same for new slides as soon as they are created. Then, when
    /// the selection changes, if new shapes in the selection have no tag, they
    /// can be tagged based on the current mode.
    /// </remarks>
    ///
    /// <remarks>"mode" strings represent the mode. An empty "" mode means just the
    /// base is visible. Any other mode indicates that the base plus that string
    /// mode is visible.</remarks>
    public class PPTPaneManager {

        #region Constants

        public const string NAME = "Instructor View";

        public static readonly string[] DEFAULT_MODES = new string[] { "Instructor", "Student", "Shared" };

        /// <summary>
        /// The Guid which identifies tags related to this class.
        /// </summary>
        private static readonly Guid GUID = new Guid("{19C14C36-AC8E-43bc-9DB6-C2AAF774C7DC}");

        private const string PANE_TAG = "PANE_TAG";

        public const string DEFAULT_MODE = "";

        #endregion

        #region Methods

        /// <summary>
        /// Get the modes of the supplied shape.
        /// </summary>
        /// <param name="shape">must be non-null shape actually in the current presentation</param>
        /// <returns>Guaranteed to return a list with at least one element.</returns>
        public string[] GetModes(PowerPoint.Shape shape) {
            return this.GetShapeModes(shape.Tags);
        }

        #endregion

        #region Constructors/Destructor

        public PPTPaneManager() : this(DEFAULT_MODES) { }

        public PPTPaneManager(string[] modes) {
            if (modes == null)
                modes = DEFAULT_MODES;
        }


        #endregion

        #region Internals

        private string UniqueName { get { return NAME + GUID.ToString(); } }

        /// <summary>
        /// Get the modes of the given presentation tag.
        /// </summary>
        /// <remarks>if it is so far
        /// entirely untagged, defaults to DEFAULT_MODE</remarks>
        private string[] GetShapeModes(PowerPoint.Tags tags) {
            // Tag if necessary.
            this.NormalizeShapeTags(tags);

            IDictionary tagDict = new PPTLibrary.TagDictionary(tags, true, this.UniqueName);

            // Discover what mode this is currently tagged as being in.
            return ((string)tagDict[PANE_TAG]).Split(',');
        }

        /// <summary>
        /// Tags with the default value (the presentation's current value or
        /// false if the presentation has no current value or there is no
        /// current presentation) iff there is currently NO value tagged.
        /// </summary>
        private void NormalizeShapeTags(PowerPoint.Tags tags) {
            IDictionary tagDict = new PPTLibrary.TagDictionary(tags, true, this.UniqueName);
            if (!tagDict.Contains(PANE_TAG))
                tagDict[PANE_TAG] = DEFAULT_MODE;
        }

        #endregion
    }
}
