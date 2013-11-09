#region Issues
// Issues:
// - NOT using button "Tag" field b/c it also controls dispatch for event handlers! (although only when non-empty!!)
// - currently disables visibility toggle when shapes disagree on visibility; should
//   really show some "third" state and allow switching to on/off (like grayed checkbox)
//   suggested resolution: could change the caption or something else about appearance
//   when this happens.
// - does not address master views at all (which means has unpredictable behavior on them)
//   actually, caching won't work with master views as it does with slides b/c of lack of
//   id though it would, presumably, work if appropriate work were done.

#region Resolved

// - crashes from INSIDE PPT at the moment when I try *iterating* through 
//   a group which itself contains groups (i.e., after already grabbing the group
//   and playing with its count previously); possible resolution: Office XP SPs.
//   RESOLUTION: passed on to PPT group; INTERNAL PPT bug
// - visual distinction code is not working b/c of PPT bug. Old issue:
//   there's no visual distinction between instructor-only shapes and public
//   shapes; suggested resolution: well, the super-duper way to do this would
//   be to have some configurable default thingummy that the user could set
//   that decided which visual (if they want) properties tag the I-Mode shapes.
//   RESOLUTION: (cheesy alternative) use the shadow property; note: there is
//   a supported interface to define alternate markings;; NEW NOTE: if I stop running
//   the IVM app and then start it again, any shapes that were previously not swapping
//   shadows *start* doing so again! So, it's *something* tied to what I'm doing.. but
//   WHAT?
//   RESOLUTION: passed on to PPT group; INTERNAL PPT bug

// - STRONGLY suspected to be a copying problem: having trouble with visibility of widgets; restricted widgets seem to be toggling off when they should be on and vice-versa; started after trying to copy a widget and change its restriction level
//   RESOLUTION: seems to have been a problem with getting unique keys for the restriction hashtable from slide id/shape name. Switched over to relying on PPTLibrary for unique keys and using the shapeId to get the unique key. Here's hoping shapeId is unique!!!
// - it takes a LONG time to switch from I-Mode to normal on large presentations
//   suggested resolution: one possibility is to switch slide-by-slide, but that
//   means you can't just save aside each version of the presentation and Build
//   them. RESOLUTION: track (cache) on a per-presentation basis the
//   set of shapes that are IMode restricted and only change them when needed
//   (going on the assumption that relatively few of the shapes will be marked
//   as restricted); the cache can be a dictionary mapping from a presentation 
//   (may need a Guid to id the presentation??) to a hashset of shapes associated
//   w/the presentation that are restricted.
// - didn't think about how groups would work. Experiments show: if a group is
//   marked restricted, that GROUP is restricted, but sub-shapes are not. if one
//   shape in a group is restricted and another is not, they are shown based on
//   the GROUP's visibility ONLY. when a group is created, its restriction is based on
//   the mode.  Resulotion: groups are no longer consider shapes at
//   all; they are not tagged, their visibility is not set, etc.; instead, their
//   tag is the sum of their children's tags, and their children's visibility is
//   set. this should mean: if a group is marked restricted, all sub-shapes of 
//   that group become restricted. if one shape in a group is restricted and another
//   is not, the group is mixed mode and they are shown based on individual
//   visibility. when a group is created, nothing changes in restrictions.
// - doesn't handle pasted objects as expected (they continue to have their
//   pre-existing value); suggested resolution: instead of making the value
//   strictly "true" or "false", make the value <pres id>:<shape name>:<value>
//   and consider the value unset of the pres id or shape name do not match the
//   current shape's. RESOLUTION (probably better): call it expected behavior
//   for pasted objects to retain their mode value.
// - not really an issue: an alternative to the selection-based introduction of
//   new shapes with the appropriate tag is to give the default shape a tag that
//   reflects the current mode. This might be a better way to do it although
//   it suffers from a *major* disadvantage: if the user changes the default shape,
//   then all new shapes match *that* shape's tag w/out changing the mode button's
//   state.

#endregion

#endregion

using System;
using System.Collections;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;
using Core = Microsoft.Office.Core;
using Office2007InstructorModeAddin;

namespace PPTPaneManagement
{
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
	public class PPTPaneManager
	{
		#region Definitions 

		/// <summary>
		/// Interface to allow customized marking of shapes.
		/// </summary>
		public interface IShapeMarker
		{
			void MarkShape(PowerPoint.Shape shape, string mode, string[] taggedModes);
		}

		/// <summary>
		/// A default implementation of the IShapeMarker interface which uses
		/// the shape's shadow to indicate whether it is restricted.
		/// </summary>
		public class DefaultShapeMarker : IShapeMarker
		{
			public DefaultShapeMarker() { }
			public void MarkShape(PowerPoint.Shape shape, string mode, string[] taggedModes)
			{
				if (shape == null)
					throw new ArgumentNullException("shape");
				try
				{
					bool isRestricted = taggedModes[0] != DEFAULT_MODE;
					if (isRestricted && shape.Shadow.Visible != Core.MsoTriState.msoTrue)
						shape.Shadow.Visible = Core.MsoTriState.msoTrue;
					else if (!isRestricted && shape.Shadow.Visible != Core.MsoTriState.msoFalse)
						shape.Shadow.Visible = Core.MsoTriState.msoFalse;
				}
				catch (Exception)
				{
					// Ignore.
				}
			}
		}

		#endregion

		#region Constants

		public const string NAME = "Instructor View";

/*		private const string PRESENTATION_MODEBAR_NAME = "Presenter View Mode";
		private const string SHAPE_MODEBAR_NAME = "Presenter Object Mode";
*/
		public static readonly string[] DEFAULT_MODES = new string[] { "Instructor" };

		/// <summary>
		/// The Guid which identifies tags related to this class.
		/// </summary>
		private static readonly Guid GUID = new Guid("{19C14C36-AC8E-43bc-9DB6-C2AAF774C7DC}");

		private const string PANE_TAG = "PANE_TAG";

		public const string DEFAULT_MODE = "";

		#endregion 

		#region Properties

		public IShapeMarker ShapeMarker 
		{ 
			get { return this.myShapeMarker; } 
			set {this.myShapeMarker = value; } 
		}


		public string Mode 
		{
			get 
			{ 
				PowerPoint.Presentation presentation = this.myPPT.GetActivePresentation();
				if (presentation == null)
					return DEFAULT_MODE;
				return this.GetPresentationMode(presentation.Tags);
			}
		
			set 
			{ 
				if (this.Mode == value)
					return;

				// Get the current presentation.
				PowerPoint.Presentation presentation = this.myPPT.GetActivePresentation();
				if (presentation != null)
				{
					// Set up the button and presentation to reflect the new mode.
					this.SetPresentationMode(presentation.Tags, value);
				}

				// Reset the mode bar.
				this.ConfigurePresentationModeBar();

				// Change the visibility of all shapes to reflect the new mode.
				this.ConfigureVisibility(this.GetRestrictedShapes(presentation));
			}
		}

		#endregion

		#region Methods
/*
		public void Hide()
		{
			if (this.myPresentationModeBar != null)
				this.myPresentationModeBar.Visible = false;
			if (this.myShapeModeBar != null)
				this.myShapeModeBar.Visible = false;
		}

		public void Show()
		{
			if (this.myPresentationModeBar != null)
				this.myPresentationModeBar.Visible = true;
			if (this.myShapeModeBar != null)
				this.myShapeModeBar.Visible = true;
		}
*/
		public bool IsRestricted(PowerPoint.Shape shape)
		{
			foreach (string mode in this.GetModes(shape))
				if (mode != DEFAULT_MODE)
					return true;
			return false;
		}

		/// <summary>
		/// Get the modes of the supplied shape.
		/// </summary>
		/// <param name="shape">must be non-null shape actually in the current presentation</param>
		/// <returns>Guaranteed to return a list with at least one element.</returns>
		public string[] GetModes(PowerPoint.Shape shape)
		{
			return this.GetShapeModes(shape.Tags);
		}

		public bool IsMode(PowerPoint.Shape shape, string mode)
		{
			if (shape == null)
				throw new ArgumentNullException("shape");

			string[] modes = this.GetModes(shape);

			// Unrestricted shapes are in all modes.
			if (modes[0] == DEFAULT_MODE)
				return true;
			else
				return System.Array.IndexOf(modes, mode) != -1;
		}

		public void UnsetMode(PowerPoint.Shape shape, string mode)
		{
			if (shape == null) 
				throw new ArgumentNullException("shape");

			if (this.myPPT.IsGroup(shape))
				foreach (PowerPoint.Shape subshape in this.myPPT.GetGroupItems(shape))
					this.UnsetMode(subshape, mode);
			else
			{
				string[] oldModes = this.GetModes(shape);
				this.ClearModes(shape);
				foreach (string oldMode in oldModes)
					if (mode != oldMode)
						this.SetMode(shape, oldMode);
				this.RecacheShapeRestricted(shape);
			}
		}

		public void ClearModes(PowerPoint.Shape shape)
		{
			this.SetMode(shape, DEFAULT_MODE);
		}

		/// <summary>
		/// Sets the shape's restricted property to include the given mode.
		/// </summary>
		public void SetMode(PowerPoint.Shape shape, string mode)
		{
			if (shape == null) 
				throw new ArgumentNullException("shape");

			if (this.myPPT.IsGroup(shape))
				foreach (PowerPoint.Shape subshape in this.myPPT.GetGroupItems(shape))
					this.SetMode(subshape, mode);
			else
			{
				this.SetShapeMode(shape.Tags, mode);
				this.RecacheShapeRestricted(shape);
			}
		}

		#endregion

		#region Members

        private PPTLibrary.PPT myPPT;
        private InstructorModeRibbon ribbon;
//		private Core.CommandBar myPresentationModeBar;
//		private Core.CommandBar myShapeModeBar;
		private IShapeMarker myShapeMarker;
		private string[] myModes;

		private IDictionary /* of string/presentation name -> (ShapeKey -> PowerPoint.Shape) */
			myRestrictedShapesTable = new Hashtable();

		#endregion

		#region Constructors/Destructor

		//public PPTPaneManager() : this(DEFAULT_MODES) { }
		public PPTPaneManager(PPTLibrary.PPT ppt, InstructorModeRibbon r) : this(DEFAULT_MODES, ppt, r) { }
		//public PPTPaneManager(string[] modes) : this(modes, new PPTLibrary.PPT()) { }

		public PPTPaneManager(string[] modes, PPTLibrary.PPT ppt, InstructorModeRibbon r)
		{

			if (modes == null)
				modes = DEFAULT_MODES;

			if (ppt == null)
				throw new ArgumentNullException("ppt");

			this.myPPT = ppt;

            this.ribbon = r;

			// Set up the list of modes; should match the ordering of the buttons in the mode button bars.
			this.myModes = new string[modes.Length + 1];
			System.Array.Copy(modes, 0, this.myModes, 1, modes.Length);
			this.myModes[0] = DEFAULT_MODE;

            this.ribbon.PresentationModeChanged += new ChangedEventHandler( this.HandlePresentationModeClick );
            this.ribbon.ShapeModeChanged += new ChangedEventHandler( this.HandleShapeModeClick );

/*			DONE CMPRINCE: Add the appropriate code here to hook up to the button events
            // Set up the bars.
			this.myPresentationModeBar = this.myPPT.AddCommandBar(PRESENTATION_MODEBAR_NAME,
				Core.MsoBarPosition.msoBarTop, true, true);
			this.myShapeModeBar = this.myPPT.AddCommandBar(SHAPE_MODEBAR_NAME,
				Core.MsoBarPosition.msoBarTop, true, true);

			// Add the "base" buttons
			Core.CommandBarButton button = (Core.CommandBarButton)
				this.myPPT.AddControl(this.myPresentationModeBar, Core.MsoControlType.msoControlButton, "Projected View");
			button.Click += new Core._CommandBarButtonEvents_ClickEventHandler(this.HandlePresentationModeClick);
			button.TooltipText = "Switch to base mode in which only unrestricted objects are visible.";
			button.Style = Core.MsoButtonStyle.msoButtonWrapCaption;

			button = (Core.CommandBarButton)
				this.myPPT.AddControl(this.myShapeModeBar, Core.MsoControlType.msoControlButton, "Unrestricted");
			button.Click += new Core._CommandBarButtonEvents_ClickEventHandler(this.HandleShapeModeClick);
			button.TooltipText = "Set shape(s) to be unrestricted, making them visible in all modes.";
			button.Style = Core.MsoButtonStyle.msoButtonWrapCaption;

			foreach (string mode in modes) {
				button = (Core.CommandBarButton)
					this.myPPT.AddControl(this.myPresentationModeBar, Core.MsoControlType.msoControlButton, mode + " View");
				button.Click += new Core._CommandBarButtonEvents_ClickEventHandler(this.HandlePresentationModeClick);
				button.TooltipText = "Toggle '" + mode + " View'.\nOnly unrestricted objects and objects restricted to '" + mode + " View' are visible in '" + mode + " View'.";
				button.Style = Core.MsoButtonStyle.msoButtonWrapCaption;
				
				button = (Core.CommandBarButton)
					this.myPPT.AddControl(this.myShapeModeBar, Core.MsoControlType.msoControlButton, mode + " Note");
				button.Click += new Core._CommandBarButtonEvents_ClickEventHandler(this.HandleShapeModeClick);
				button.TooltipText = "Toggle restriction of shape(s) to '" + mode + "' mode.\nUnrestricted objects are visible in all modes.\nRestricted objects are visible only in the modes to which they are restricted.";
				button.Style = Core.MsoButtonStyle.msoButtonWrapCaption;
			}
*/
			// Hook to relevant PPT events.
			this.myPPT.App.WindowActivate += new PowerPoint.EApplication_WindowActivateEventHandler(this.HandleWindowActivate);
			this.myPPT.App.WindowDeactivate += new PowerPoint.EApplication_WindowDeactivateEventHandler(this.HandleWindowDeactivate);
			this.myPPT.App.WindowSelectionChange += new PowerPoint.EApplication_WindowSelectionChangeEventHandler(this.HandleWindowSelectionChange);
			// New presentation event doesn't seem to be working in pre-SP2 (or possibly SP1).
			this.myPPT.App.AfterNewPresentation += new PowerPoint.EApplication_AfterNewPresentationEventHandler(this.HandleAfterNewPresentation);
			this.myPPT.App.PresentationSave += new PowerPoint.EApplication_PresentationSaveEventHandler(this.HandlePresentationSave);
			this.myPPT.App.AfterPresentationOpen += new PowerPoint.EApplication_AfterPresentationOpenEventHandler(this.HandleAfterPresentationOpen);
			this.myPPT.App.PresentationNewSlide += new PowerPoint.EApplication_PresentationNewSlideEventHandler(this.HandlePresentationNewSlide);

			this.ConfigurePresentationModeBar();
			this.ConfigureShapeModeBar();

			foreach (PowerPoint.Presentation presentation in this.myPPT.App.Presentations)
				this.RebuildCache(presentation);
		}

		#endregion

		#region Event Handlers

		private void HandleWindowActivate(PowerPoint.Presentation presentation, PowerPoint.DocumentWindow window)
		{
			// TODO: need to make a method for building the presentation at this point
			// in case when it was opened/newed it didn't have an active window but
			// now it does. Suggestion: add a tag to the presentation that contains a
			// GUID unique to this instance which will tell us whether THIS INSTANCE
			// has ever built the presentation before? That's actually no good b/c
			// it could have been closed, changed elsewhere, and reopened all while
			// this instance is running. :(

			// Update the mode bars.
			this.ConfigurePresentationModeBar();
			this.ConfigureShapeModeBar();
		}

		private void HandleWindowDeactivate(PowerPoint.Presentation presentation, PowerPoint.DocumentWindow window)
		{
			// Update the mode bars.
			this.ConfigurePresentationModeBar();
			this.ConfigureShapeModeBar();
		}

		private void HandleWindowSelectionChange(PowerPoint.Selection selection)
		{
			// Ensure that any newly added shapes are set to the appropriate
			// value per the mode:
			PowerPoint.ShapeRange shapes = this.myPPT.GetSelectedShapes();
			if (shapes != null)
				this.NormalizeShapes(shapes);

			// Reconfigure the shape mode bar per the new selection.
			this.ConfigureShapeModeBar();

			// Reconfigure the visibility of shapes in the selection (in case
			// they are newly added).
			if (shapes != null)
				this.ConfigureVisibility(shapes);
		}

		private void HandlePresentationModeClick( bool newValue )
		{
            if (newValue == true)
            {
                this.Mode = this.myModes[0];
            }
            else
            {
                this.Mode = this.myModes[1];
            }
		}

		private void HandleShapeModeClick( bool newValue )
		{            
			PowerPoint.ShapeRange shapes = this.myPPT.GetSelectedShapes();
			if (shapes != null)
			{
				foreach (PowerPoint.Shape shape in shapes)
                    if (newValue)
                    {
                        this.SetMode(shape, this.myModes[0]);
                        this.UnsetMode(shape, this.myModes[1]);
                    }
                    else
                    {
                        this.SetMode(shape, this.myModes[1]);
                        this.UnsetMode(shape, this.myModes[0]);
                    }

				// Re-set the visibility of the selection.
				this.ConfigureVisibility(shapes);
			}

			// Update the shape mode bar's state.
			this.ConfigureShapeModeBar();
		}

		private void HandlePresentationSave(PowerPoint.Presentation presentation)
		{
			bool active = false;
			foreach (PowerPoint.DocumentWindow window in presentation.Windows)
				if (window.Active == Core.MsoTriState.msoTrue)
				{
					active = true;
					break;
				}
			if (active)
				this.RebuildCache(presentation);
		}

		private void HandleAfterNewPresentation(PowerPoint.Presentation presentation)
		{
			bool active = false;
			foreach (PowerPoint.DocumentWindow window in presentation.Windows)
				if (window.Active == Core.MsoTriState.msoTrue)
				{
					active = true;
					break;
				}

			// Only touch the presentation if PPT is actually active.
			// Avoids changing presentations when they are, for example, opened embedded in a web browser.
			if (active)
				// Rebuild/normalize/configure every shape.
				this.FullPassAllShapes(presentation);
		}

		private void HandleAfterPresentationOpen(PowerPoint.Presentation presentation)
		{
			bool active = false;
			foreach (PowerPoint.DocumentWindow window in presentation.Windows)
				if (window.Active == Core.MsoTriState.msoTrue)
				{
					active = true;
					break;
				}

			// Only touch the presentation if PPT is actually active.
			// Avoids changing presentations when they are, for example, opened embedded in a web browser.
			if (active)
				// Rebuild/normalize/configure every shape.
				this.FullPassAllShapes(presentation);
		}

		private void HandlePresentationNewSlide(PowerPoint.Slide slide)
		{
			this.NormalizeShapes(slide.Shapes);

			// Should NOT be necessary to configure visibility.
		}

		#endregion

		#region Internals

        private void DisablePresentationBar()
        {
            this.ribbon.ProjectedViewEnabled = false;
            this.ribbon.InstructorViewEnabled = false;
        }
        private void DisableShapesBar()
        {
            this.ribbon.ProjectedSetEnabled = false;
            this.ribbon.InstructorSetEnabled = false;
        }

		private string UniqueName { get { return NAME + GUID.ToString(); } }

		/// <summary>
		/// Tag all untagged shapes in the presentation.
		/// </summary>
		/// <remarks>Tags the presentation itself as public mode.</remarks>
		private void NormalizePresentation(PowerPoint.Presentation presentation)
		{
			this.SetPresentationMode(presentation.Tags, DEFAULT_MODE);
			foreach (PowerPoint.Slide slide in presentation.Slides)
				this.NormalizeShapes(slide.Shapes, DEFAULT_MODE);
		}

		/// <summary>
		/// Tag all untagged shapes in the range.
		/// </summary>
		private void NormalizeShapes(IEnumerable shapes)
		{
			this.NormalizeShapes(shapes, this.Mode);
		}

		/// <summary>
		/// Tag all untagged shapes in the range.
		/// </summary>
		private void NormalizeShapes(IEnumerable shapes, string mode)
		{
			foreach (PowerPoint.Shape shape in shapes)
				this.NormalizeShape(shape, mode);
		}

		/// <summary>
		/// Gives the shape a tagged value if it does not already have one
		/// (or its subshapes if it's a group). 
		/// </summary>
		private void NormalizeShape(PowerPoint.Shape shape)
		{
			this.NormalizeShape(shape, this.Mode);
		}

		/// <summary>
		/// Gives the shape a tagged value if it does not already have one
		/// (or its subshapes if it's a group), defaulting to the given value.
		/// </summary>
		private void NormalizeShape(PowerPoint.Shape shape, string mode)
		{
			if (this.myPPT.IsGroup(shape))
				foreach (PowerPoint.Shape subshape in this.myPPT.GetGroupItems(shape))
					this.NormalizeShape(subshape, mode);
			else
				this.NormalizeShapeTags(shape.Tags, mode);
		}

		/// <summary>
		/// Sets the visibility of the given shape to reflect the combination 
		/// of the given mode and the shape's restricted status.
		/// </summary>
		private void ConfigureVisibility(PowerPoint.Shape shape, string mode)
		{
			if (this.myPPT.IsGroup(shape))
				foreach (PowerPoint.Shape subshape in this.myPPT.GetGroupItems(shape))
					this.ConfigureVisibility(subshape, mode);
			else
			{
				this.RecacheShapeRestricted(shape);

				// If we're in a restricted mode matching the tag or the tag
				// isn't restricted, make it visible. Otherwise, not.
				bool visible = this.IsMode(shape, mode);
				if (visible && shape.Visible != Core.MsoTriState.msoTrue)
					shape.Visible = Core.MsoTriState.msoTrue;
				else if (!visible && shape.Visible != Core.MsoTriState.msoFalse)
					shape.Visible = Core.MsoTriState.msoFalse;

				if (this.ShapeMarker != null)
					this.ShapeMarker.MarkShape(shape, mode, this.GetModes(shape));
			}
		}

		/// <summary>
		/// Sets the visibility of the given shapes to reflect the combination of the 
		/// current presentation's mode and the shape's visibility. The shapes may be
		/// on different slides in different presentations.
		/// </summary>
		private void ConfigureVisibility(IEnumerable /* of PowerPoint.Shape */ shapes)
		{
			// Get the current mode.
			string mode = this.Mode;

			foreach (PowerPoint.Shape shape in shapes)
				this.ConfigureVisibility(shape, mode);
		}

		/// <summary>
		/// Sets the visibility of all shapes in the current 
		/// presentation to reflect the combination of the current presentation's mode
		/// and the shape's visibility.
		/// </summary>
		private void ConfigureVisibility()
		{
			// Get the current presentation.
			PowerPoint.Presentation presentation = this.myPPT.GetActivePresentation();
			if (presentation == null)
				return;

			foreach (PowerPoint.Slide slide in presentation.Slides)
				this.ConfigureVisibility(slide.Shapes);
		}

		/// <summary>
		/// Counts the number of shapes in this shape/group that are restricted
		/// by restriction category. Groups are assumed to be neither; only leaf shapes
		/// are counted. Note that unrestricted is counted under DEFAULT_MODE.
		/// </summary>
		/// <param name="count">increased by the total count of shapes in this shape group</param>
		private void CountRestrictions(PowerPoint.Shape shape, 
			IDictionary /* from string/mode -> count */ restrictionTable, ref int count)
		{
			if (this.myPPT.IsGroup(shape))
			{
				foreach (PowerPoint.Shape subshape in this.myPPT.GetGroupItems(shape))
					this.CountRestrictions(subshape, restrictionTable, ref count);
			}
			else
			{
				foreach (string mode in this.GetModes(shape))
				{
					if (!restrictionTable.Contains(mode))
						restrictionTable[mode] = 0;
					restrictionTable[mode] = (int)restrictionTable[mode] + 1;
				}
				count++;
			}
		}

		/// <summary>
		/// Configrue the shape mode bar to an appropriate state based on the current
		/// selection.
		/// </summary>
		private void ConfigureShapeModeBar()
		{
            // DONE CMPRINCE: Make this work correctly
			PowerPoint.ShapeRange shapes = this.myPPT.GetSelectedShapes();
			if (shapes == null || shapes.Count == 0)
			{
                // DONE CMPRINCE: Make this work correctly
				this.DisableShapesBar();
				return;
			}

			// Figure out on which restrictions the selection agrees.
			IDictionary restrictionTable = new Hashtable();
			int count = 0;
			foreach (PowerPoint.Shape shape in shapes)
				this.CountRestrictions(shape, restrictionTable, ref count);

			// Set all buttons to enabled but only those on which the selection agrees to down.
            // ProjectedSetButton
            this.ribbon.ProjectedSetEnabled = true;
            if (restrictionTable.Contains(this.myModes[0]) &&
                (int)restrictionTable[this.myModes[0]] == count)
            {
                this.ribbon.UnrestrictedState = true;
            }
            else
            {
                this.ribbon.UnrestrictedState = false;
            }

            // InstructorSetButton
            this.ribbon.InstructorSetEnabled = true;
            if (restrictionTable.Contains(this.myModes[1]) &&
                (int)restrictionTable[this.myModes[1]] == count)
            {
                this.ribbon.UnrestrictedState = false;
            }
            else
            {
                this.ribbon.UnrestrictedState = true;
            }            
		}

		/// <summary>
		/// Set up the mode bar appropriately based on the active
		/// presentation's recorded mode.
		/// </summary>
		private void ConfigurePresentationModeBar()
		{
            // DONE CMPRINCE: Make this work correctly
			PowerPoint.Presentation presentation = this.myPPT.GetActivePresentation();
			if (presentation == null)
			{
				// Disable the mode bar's buttons.
                // DONE CMPRINCE: Make this work correctly
				this.DisablePresentationBar();
			}
			else
			{
				// Enable the mode bar's buttons and set their states appropriately.
				string mode = this.Mode;
                this.ribbon.ProjectedViewEnabled = true;
                this.ribbon.InstructorViewEnabled = true;                
                if (mode == this.myModes[0])
                {
                    this.ribbon.ProjectedViewState = true;
				}
                else
                {
                    this.ribbon.ProjectedViewState = false;
                }
			}
		}

		/// <summary>
		/// Get the mode of the given presentation tag.
		/// </summary>
		/// <remarks>if it is so far
		/// entirely untagged, Defaults to DEFAULT_MODE</remarks>
		private string GetPresentationMode(PowerPoint.Tags tags)
		{
			// Tag if necessary.
			this.NormalizePresentationTags(tags);

			IDictionary tagDict = new PPTLibrary.TagDictionary(tags, true, this.UniqueName);

			// Discover what mode this is currently tagged as being in.
			return (string)tagDict[PANE_TAG];
		}

		/// <summary>
		/// Get the modes of the given presentation tag.
		/// </summary>
		/// <remarks>if it is so far
		/// entirely untagged, defaults to DEFAULT_MODE</remarks>
		private string[] GetShapeModes(PowerPoint.Tags tags)
		{
			// Tag if necessary.
			this.NormalizeShapeTags(tags);

			IDictionary tagDict = new PPTLibrary.TagDictionary(tags, true, this.UniqueName);

			// Discover what mode this is currently tagged as being in.
			return ((string)tagDict[PANE_TAG]).Split(',');
		}

		/// <summary>
		/// Tags with the given value, adding the mode if it is anything but default and resetting to that
		/// mode if it is default.
		/// </summary>
		private void SetShapeMode(PowerPoint.Tags tags, string mode)
		{
			this.NormalizeShapeTags(tags);

			IDictionary tagDict = new PPTLibrary.TagDictionary(tags, true, this.UniqueName);

			string oldModes = (string)tagDict[PANE_TAG];
			// HACK: theoretically, this should disallow the default mode ever ending up in a list with other modes. That's assumed, but it could be more explicitly monitored.
			if (mode == DEFAULT_MODE || oldModes == DEFAULT_MODE || oldModes == "")
				tagDict[PANE_TAG] = mode;
			else if (System.Array.IndexOf(oldModes.Split(','), mode) == -1)
				tagDict[PANE_TAG] = oldModes + "," + mode;
			// Otherwise, do nothing b/c the mode is already there.
		}

		/// <summary>
		/// Tags with the given value.
		/// </summary>
		private void SetPresentationMode(PowerPoint.Tags tags, string mode)
		{
			new PPTLibrary.TagDictionary(tags, 
				true, this.UniqueName)[PANE_TAG] = mode;
		}

		/// <summary>
		/// Tags with the default value (the presentation's current value or
		/// false if the presentation has no current value or there is no
		/// current presentation) iff there is currently NO value tagged.
		/// </summary>
		private void NormalizePresentationTags(PowerPoint.Tags tags)
		{
			IDictionary tagDict = new PPTLibrary.TagDictionary(tags, true, this.UniqueName);
			if (!tagDict.Contains(PANE_TAG))
				tagDict[PANE_TAG] = DEFAULT_MODE;
		}

		/// <summary>
		/// Tags with the default value (the presentation's current value or
		/// false if the presentation has no current value or there is no
		/// current presentation) iff there is currently NO value tagged.
		/// </summary>
		private void NormalizeShapeTags(PowerPoint.Tags tags)
		{
			IDictionary tagDict = new PPTLibrary.TagDictionary(tags, true, this.UniqueName);
			if (!tagDict.Contains(PANE_TAG))
				tagDict[PANE_TAG] = this.Mode;
		}

		/// <summary>
		/// Tags with the provided default value iff there is currently NO value tagged.
		/// </summary>
		private void NormalizeShapeTags(PowerPoint.Tags tags, string mode)
		{
			IDictionary tagDict = new PPTLibrary.TagDictionary(tags, true, this.UniqueName);
			if (!tagDict.Contains(PANE_TAG))
				tagDict[PANE_TAG] = mode;
		}

		#region Restricted Shapes Cache

		private void RecacheShapeRestricted(PowerPoint.Shape shape)
		{
			PowerPoint.Slide slide = this.myPPT.GetSlideFromShape(shape);
			if (slide == null)
				throw new Exception("Not handling shapes on Slide Masters.");
			PowerPoint.Presentation presentation = this.myPPT.GetPresentationFromSlide(slide);
			

			PPTLibrary.ShapeKey key = new PPTLibrary.ShapeKey(shape, slide.SlideID);
			IDictionary rsTable = this.GetRestrictedTable(presentation);

			PowerPoint.Shape oldShape = null;
			PPTLibrary.ShapeKey oldKey = key;
			if (rsTable.Contains(key))
			{
				oldShape = (PowerPoint.Shape)rsTable[key];
				try	{ oldKey = new PPTLibrary.ShapeKey(oldShape, this.myPPT.GetSlideFromShape(oldShape).SlideID); }
				catch (System.Runtime.InteropServices.COMException)
				{
					// Old shape does not exist. Ignore it.
				}
			}

			if (oldShape != null && !oldKey.Equals(key))
				this.RebuildCache(presentation);
			else if (this.IsRestricted(shape))
				rsTable[key] = shape;
			else
				rsTable.Remove(key);
		}

		private IDictionary GetRestrictedTable(PowerPoint.Presentation presentation)
		{
			if (!this.myRestrictedShapesTable.Contains(presentation.FullName))
				this.myRestrictedShapesTable[presentation.FullName] = new Hashtable();
			return (IDictionary)this.myRestrictedShapesTable[presentation.FullName];
		}

		private ICollection GetRestrictedShapes(PowerPoint.Presentation presentation)
		{
			IDictionary rsTable = this.GetRestrictedTable(presentation);

			// Check correctness of the cache.
			bool correct = true;
			ArrayList /* of ShapeKey */ keysToDelete = new ArrayList();
			foreach (PPTLibrary.ShapeKey key in rsTable.Keys)
			{
				PowerPoint.Shape shape = (PowerPoint.Shape)rsTable[key];
				PPTLibrary.ShapeKey oldKey = key;
				try	{ oldKey = new PPTLibrary.ShapeKey(shape, this.myPPT.GetSlideFromShape(shape).SlideID ); }
				catch (System.Runtime.InteropServices.COMException)
				{
					// Shape does not exist, delete it from the cache.
					keysToDelete.Add(key);
					continue;
				}
				if (!key.Equals(oldKey) || !this.IsRestricted(shape))
				{
					correct = false;
					break;
				}
			}

			if (!correct)
			{
				this.RebuildCache(presentation);
				rsTable = this.GetRestrictedTable(presentation);
			}
			else
				foreach (PPTLibrary.ShapeKey key in keysToDelete)
					rsTable.Remove(key);

			ArrayList shapes = new ArrayList();
			// No need to test if objects exist since non-existant objects have
			// just been weeded out.
			foreach (PowerPoint.Shape shape in rsTable.Values)
				shapes.Add(shape);
			return shapes;
		}

		private void RebuildCache(PowerPoint.Presentation presentation)
		{
			this.myRestrictedShapesTable[presentation.FullName] = new Hashtable();
			foreach (PowerPoint.Slide slide in presentation.Slides)
				foreach (PowerPoint.Shape shape in slide.Shapes)
					if (this.IsRestricted(shape))
						this.RecacheShapeRestricted(shape);
		}

		/// <summary>Rebuild/normalize/configure every shape.</summary>
		private void FullPassAllShapes()
		{
			PowerPoint.Presentation presentation = this.myPPT.GetActivePresentation();
			if (presentation == null)
				return;
			this.FullPassAllShapes(presentation);
		}

		/// <summary>Normalize/rebuild/configure every shape.</summary>
		private void FullPassAllShapes(PowerPoint.Presentation presentation)
		{
			// TODO: could also unfold the group traversing here.
			string mode = this.Mode;
			this.myRestrictedShapesTable[presentation.FullName] = new Hashtable();
			foreach (PowerPoint.Slide slide in presentation.Slides)
				foreach (PowerPoint.Shape shape in slide.Shapes)
				{
					this.NormalizeShape(shape, mode);
					if (this.IsRestricted(shape))
						this.RecacheShapeRestricted(shape);
					this.ConfigureVisibility(shape, mode);
				}
		}

		#endregion

		#endregion

/*
		#region Bug Testing

		static void TestShadowBugHandleWSC(PowerPoint.Selection s)
		{
			try
			{
				if (s.Type == PowerPoint.PpSelectionType.ppSelectionShapes)
					foreach (PowerPoint.Shape shape in s.ShapeRange)
						shape.Shadow.Visible = Core.MsoTriState.msoTrue;
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
			}
		}

		static void TraverseReverseShapeVisible(PowerPoint.Shape shape)
		{
			try
			{
				foreach (PowerPoint.Shape subshape in shape.GroupItems)
				{
					TraverseReverseShapeVisible(subshape);
				}
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
				shape.Visible = (shape.Visible == Core.MsoTriState.msoTrue) ?
					Core.MsoTriState.msoFalse : Core.MsoTriState.msoTrue;
			}
		}

		static void TestGroupBugHandleWSC(PowerPoint.Selection s)
		{
			try
			{
				if (s.Type == PowerPoint.PpSelectionType.ppSelectionShapes)
					foreach (PowerPoint.Shape shape in s.ShapeRange)
						TraverseReverseShapeVisible(shape);
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
			}
		}

		#endregion

		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[MTAThread]
		static void Main(string[] args)
		{
			PPTLibrary.PPT ppt = new PPTLibrary.PPT();
		
			PPTPaneManager manager = new PPTPaneManager(ppt);
			manager.ShapeMarker = new DefaultShapeMarker(); // Not currently working b/c of PPT Shadow.Visible bug (demonstrated by HandleTestWSC above and: create two objects, select them, group them, ungroup them, unselect them, select one of them. 
			
			ppt.App.Activate();
			Console.ReadLine();
		}
*/
    }
}
