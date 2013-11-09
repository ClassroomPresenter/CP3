using System;
using System.Runtime.InteropServices;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;
using Office = Microsoft.Office.Core;
using System.Diagnostics;
using System.Reflection;
using System.Collections;

namespace PPTLibrary
{	
	/// <summary>
	/// Wrapper for PowerPoint. Strictly used for getting PowerPoint started
	/// and killing it later.
	/// </summary>
	/// <remarks>The Main method of an application using this class should
	/// be marked with the MTAThread attribute.</remarks>
	public class PPT
	{
		#region Methods
/*
		/// <summary>
		/// Finds the named bar in the top-level command bars. (Only looks at the top level, not in sub-menus.)
		/// </summary>
		/// <param name="name">non-null name of the bar to search for</param>
		/// <returns>the bar or null if no such bar exists</returns>
		public Core.CommandBar FindCommandBar(string name) 
		{
			return this.FindCommandBar(this.App.CommandBars, name);
		}

		/// <summary>
		/// Finds the named bar in the set of bars given. (Only looks at the top level, not in sub-menus.)
		/// </summary>
		/// <param name="bars">non-null bars object</param>
		/// <param name="name">non-null name of the bar to search for</param>
		/// <returns>the bar or null if no such bar exists</returns>
		public Core.CommandBar FindCommandBar(Core.CommandBars bars, string name) 
		{
			if (bars == null) throw new ArgumentNullException("bars");
			if (name == null) throw new ArgumentNullException("name");

			foreach (Core.CommandBar bar in bars)
				if (bar.Name == name)
					return bar;
			return null;
		}

		/// <summary>
		/// Add a new command bar to PPT.
		/// </summary>
		/// <param name="bars">the bars object to which to add the new bar</param>
		/// <param name="name">the name of the bar.</param>
		/// <param name="position">the position of the bar.</param>
		/// <param name="visible">should the bar be visible immediately</param>
		/// <param name="replace">should existing bars of the same name be deleted (an error is thrown if replace is false and a bar of the same name exists)</param>
		/// <returns>the newly created bar</returns>
		public Core.CommandBar AddCommandBar(Core.CommandBars bars, string name, 	
			Core.MsoBarPosition position, bool visible, bool replace) 
		{
			Debug.Assert(bars != null, "bars must be non-null");
			Debug.Assert(name != null && name != "", "name must be non-null and non-empty");
			Debug.Assert(Enum.IsDefined(position.GetType(), position), "position has an invalid value");

			Core.CommandBar newBar = null;

			// Delete all bars with the same name.
			foreach (Core.CommandBar bar in bars)
				if (bar.Name == name)
				{
					if (replace)
					{
						newBar = bar;
						break;
					}
					else
						throw new Exception("A bar by the name \"" + name + "\" already exists.");
				}
			if (newBar == null)
				newBar = bars.Add(name, position, false, false);
			if (newBar.Visible != visible)
				newBar.Visible = visible;
			this.myObjectCache.Add(newBar);
			return newBar;
		}

		/// <summary>
		/// Add a new command bar to PPT. (Adds to the primary command bars list.)
		/// </summary>
		/// <param name="name">the name of the bar.</param>
		/// <param name="position">the position of the bar.</param>
		/// <param name="visible">should the bar be visible immediately</param>
		/// <param name="replace">should existing bars of the same name be deleted (an error is thrown if replace is false and a bar of the same name exists)</param>
		/// <returns>the newly created bar</returns>
		public Core.CommandBar AddCommandBar(string name, 	
			Core.MsoBarPosition position, bool visible, bool replace) 
		{
			return this.AddCommandBar(this.App.CommandBars, name,
				position, visible, replace);
		}

		public Core.CommandBarControl AddControl(Core.CommandBar bar,
			Core.MsoControlType type, string name) 
		{
			Core.CommandBarControl newControl = null;
			foreach (Core.CommandBarControl control in bar.Controls)
			{
				// Find an existing control by the same name and reuse it.. unless it's the wrong type.
				if (control.Caption == name)
				{
					if (control.Type != type)
						control.Delete(false);
					else
					{
						newControl = control;
						break;
					}
				}
			}
			if (newControl == null)
			{
				newControl = bar.Controls.Add(type, Missing.Value, Missing.Value, Missing.Value, false);
				newControl.Caption = name;
			}
			if (newControl is Core.CommandBarButton)
				((Core.CommandBarButton)newControl).Style = Core.MsoButtonStyle.msoButtonCaption;
			newControl.Visible = true;
			this.myObjectCache.Add(newControl);
			return newControl;
		}
*/
		/// <summary>
		/// Get the active window, returning null if there is none.
		/// </summary>
		public PowerPoint.DocumentWindow GetActiveWindow()
		{
			PowerPoint.DocumentWindow window = null;
			foreach (PowerPoint.DocumentWindow w in this.App.Windows)
				if (w.Active == Office.MsoTriState.msoTrue)
				{
					window = w;
					break;
				}
			return window;
		}

		/// <summary>
		/// Get the active presentation, returning null if there is none.
		/// </summary>
		public PowerPoint.Presentation GetActivePresentation()
		{
			// Assume that the activepresentation == the activewindow's presentation
			PowerPoint.DocumentWindow window = this.GetActiveWindow();
			if (window == null)
				return null;

			return window.Presentation;
		}

		/// <summary>
		/// Determines whether the given shape is a group or not.
		/// ("is a group" == "has a sub-shape list")
		/// </summary>
		public bool IsGroup(PowerPoint.Shape shape)
		{
			return this.GetGroupItems(shape) != null;
		}

		/// <summary>
		/// Get the groups items (set of shapes inside the group, essentially)
		/// associated with the given shape. Returns null if the shape is not
		/// a group.
		/// </summary>
		public PowerPoint.GroupShapes GetGroupItems(PowerPoint.Shape shape)
		{
			PowerPoint.GroupShapes shapes = null;
			int count = -1;
			try { 
                shapes = shape.GroupItems;
                count = shapes.Count; 
            }
			catch (System.UnauthorizedAccessException) 
            {  
            }
			if (count >= 0)
				return shapes;
			else
				return null;
		}

		/// <summary>
		/// Get the slide or master which is the (eventual) parent of the given shape.
		/// </summary>
		public object GetSlideOrMasterFromShape(PowerPoint.Shape shape)
		{
			if (shape == null) throw new ArgumentNullException("slide");
			object parent = shape.Parent;
			while (!(parent is PowerPoint.Slide || parent is PowerPoint.Master))
			{
				if (!(parent is PowerPoint.Shape))
					throw new Exception("Cannot find slide for given shape.");
				parent = ((PowerPoint.Shape)parent).Parent;
			}
			return parent;
		}

		/// <summary>
		/// Get the slide which is the (eventual) parent of the given shape.
		/// </summary>
		/// <returns>The slide or null if the shape is not on a slide</returns>
		public PowerPoint.Slide GetSlideFromShape(PowerPoint.Shape shape)
		{
			object slide = this.GetSlideOrMasterFromShape(shape);
			if (slide is PowerPoint.Slide)
				return (PowerPoint.Slide)slide;
			else
				return null;
		}

		/// <summary>
		/// Get the slide master which is the (eventual) parent of the given shape.
		/// </summary>
		/// <returns>The slide master or null if the shape is not on a slide master</returns>
		public PowerPoint.Master GetMasterFromShape(PowerPoint.Shape shape)
		{
			object slide = this.GetSlideOrMasterFromShape(shape);
			if (slide is PowerPoint.Master)
				return (PowerPoint.Master)slide;
			else
				return null;
		}

		/// <summary>
		/// Get the presentation which is the parent of the given slide.
		/// </summary>
		public PowerPoint.Presentation GetPresentationFromSlide(PowerPoint.Slide slide)
		{
			if (slide == null) throw new ArgumentNullException("slide");
			object parent = slide.Parent;
			if (!(parent is PowerPoint.Presentation))
				throw new Exception("Cannot find presentation for given slide.");
			return (PowerPoint.Presentation)parent;
		}

		/// <summary>
		/// Get the "slide" from a document window in PPT. The result (placed in
		/// slide) may be either a PowerPoint.Slide object or a PowerPoint.Master
		/// object. Both have similar fields but share no base type.
		/// </summary>
		/// <param name="window">A non-null document window in the currently active PowerPoint application.</param>
		/// <param name="slide">the slide found (if any, check return value)</param>
		/// <param name="isMaster">true if the returned slide is a Master, false otherwise (so if there is a return, it's a Slide)</param>
		/// <returns>true iff a slide was found</returns>
		/// <remarks>This method may activate the pane of the slide that is returned.</remarks>
		public bool GetSlideFromWindow(PowerPoint.DocumentWindow window,
			out object slide, out bool isMaster)
		{
			/*
				 * Notes on views:
				 * 
				 * Normal view is, well, the normal view and the only one with "panes":
				 * multiple selectable windows embedded in a single window. The current
				 * pane can be detected by checking the ViewType of the Active Pane: PpViewSlide,
				 * PpViewThumbnails, PpViewNotesPage, PpViewOutline, PpViewSlideMaster, 
				 * PpViewMasterThumbnails, or PpViewTitleMaster. Note that the *type* of
				 * the slide field can vary depending on whether Pane #2 (guaranteed to be
				 * the "slide" pane) is a master or a slide. (It should be either PowerPoint.Master
				 * or PowerPoint.Slide, I believe. Both have a Shapes field.)
				 * 
				 * As far as I can tell, only PpViewNotesPage can also be a Window ViewType.
				 * Note that if a pane other than pane #2 is selected, the Slide property
				 * of the view is NOT VALID and accessing it will cause an error.
				 * 
				 * Of the window (non-pane, usually) views, I've been able to find:
				 *
				 ** HandoutMaster (View > Masters > Handout master)
				 ** NotesMaster (as w/HandoutMaster)
				 ** NotesPage (View > Notes Page)
				 ** PrintPreview (File > Print Preview)
				 ** SlideSorter (View > Slide Sorter)
				 *
				 * It seems to me that the right thing to do to get the Shapes field of any
				 * Normal view is to activate the "Slide" pane (#2) and grab the Shapes field
				 * of the resulting Master or Slide. For all others, should probably just
				 * report inability to get the Shapes field. Note that for SlideSorter or
				 * NotesPage, it *might* also make sense to activate whatever the current slide
				 * is in a normal view and then return the shapes field.
				 * 
				 */
			Debug.Assert(window != null, "window parameter must be non-null");

			slide = null;
			isMaster = false;
						
			// Make sure there's at least one slide!
			if (window.Presentation.Slides.Count == 0)
			{
				//DebugForm.WriteLine("No slides in presentation while trying to find slide.");
				return false;
			}
			else if (window.ViewType == PowerPoint.PpViewType.ppViewNormal)
			{
				// Pane 2 is guaranteed to be the slide/master pane. Make sure it's active so that
				// the slide member will be accessible.
				if (GetSlidePane(window) != window.ActivePane)
					GetSlidePane(window).Activate();
				slide = window.View.Slide;
				// Should be one of PpViewSlide, PpViewSlideMaster, or PpViewTitleMaster.
				PowerPoint.PpViewType paneType = GetSlidePane(window).ViewType;
				Debug.Assert(paneType == PowerPoint.PpViewType.ppViewSlide ||
					paneType == PowerPoint.PpViewType.ppViewSlideMaster ||
					paneType == PowerPoint.PpViewType.ppViewTitleMaster,
					"Unexpected pane type encountered: " + paneType);
				isMaster = paneType != PowerPoint.PpViewType.ppViewSlide;
				return true;
			}
			else
			{
				// Not currently trying to track down slide for any other type.
				return false;
			}
		}

		public PowerPoint.Pane GetSlidePane(PowerPoint.DocumentWindow window)
		{
			if (window.ViewType == PowerPoint.PpViewType.ppViewNormal)
				return window.Panes[2];
			else
				return null;
		}

		/// <summary>
		/// Find the currently active slide or slide master.
		/// </summary>
		/// <param name="slide">The active slide or master (if it exists); must be null, a PowerPoint.Slide, or a PowerPoint.Master</param>
		/// <param name="isMaster">whether the slide output parameter is a PowerPoint.Master or not</param>
		/// <returns>true iff an active slide or master was found</returns>
		public bool GetCurrentSlideOrMaster(out object slide, out bool isMaster)
		{
			slide = null;
			isMaster = false;

			PowerPoint.DocumentWindow window = this.GetActiveWindow();
			if (window == null)
				return false;

			return this.GetSlideFromWindow(window, out slide, out isMaster);
		}

		/// <summary>
		/// Get the currently active slide.
		/// </summary>
		/// <returns>A slide or null if there is no currently active slide.</returns>
		public PowerPoint.Slide GetCurrentSlide()
		{
			object slide;
			bool isMaster;
			if (!this.GetCurrentSlideOrMaster(out slide, out isMaster) ||
				isMaster)
				return null;
			return (PowerPoint.Slide)slide;
		}

		/// <summary>
		/// Get the currently active master.
		/// </summary>
		/// <returns>A slide master or null if there is none currently active.</returns>
		public PowerPoint.Master GetCurrentMaster()
		{
			object slide;
			bool isMaster;
			if (!this.GetCurrentSlideOrMaster(out slide, out isMaster) ||
				!isMaster)
				return null;
			return (PowerPoint.Master)slide;
		}

		/// <summary>
		/// Finds the shapes selected in the active window.
		/// </summary>
		/// <returns>A shapes object representing the selected shape(s)
		/// or null if no selected shapes could be found. If non-null, 
		/// guaranteed to contain at least one shape.</returns>
		public PowerPoint.ShapeRange GetSelectedShapes()
		{
			PowerPoint.DocumentWindow window = this.GetActiveWindow();
			
			// Only gather the selection of a slide if it is in the active window.
			// TODO: may want to restrict this to regular slides rather than slide masters.
			// Would look something like: GetSlidePane(window).ViewType == PowerPoint.PpViewType.ppViewSlide
			if (window == null || window.ViewType != PowerPoint.PpViewType.ppViewNormal ||
				window.ActivePane != GetSlidePane(window))
				return null;

			PowerPoint.Selection selection = window.Selection;
			if (selection == null)
				return null;
				
			// Find the shapes associated with this selection.
			PowerPoint.ShapeRange shapes = null;
			// Only one of shapes and shape should be set in this switch; the other
			// should be left null.
			switch (selection.Type)
			{
				case PowerPoint.PpSelectionType.ppSelectionShapes:
					shapes = selection.ShapeRange;
					break;
				case PowerPoint.PpSelectionType.ppSelectionText:
					// Try to find a shape associated with the text.
					PowerPoint.Shape shape = null;
					object parent = selection.TextRange.Parent;
					if (parent is PowerPoint.Shape)
						shape = (PowerPoint.Shape)parent;
					else if (parent is PowerPoint.TextFrame)
					{
						parent = ((PowerPoint.TextFrame)parent).Parent;
						if (parent is PowerPoint.Shape)
							shape = (PowerPoint.Shape)parent;
					}

					// Transform the shape into a ShapeRange
					if (shape != null && window.View.Slide is PowerPoint.Slide)
					{
						PowerPoint.Shapes slideShapes = ((PowerPoint.Slide)window.View.Slide).Shapes;
						int i;
						for (i = 1; i <= slideShapes.Count; i++)
							if ( ShapeKey.ShapesMatch(slideShapes[i], this.GetSlideFromShape(slideShapes[i]).SlideID, shape, this.GetSlideFromShape(shape).SlideID ) )
								break;
						if (i <= slideShapes.Count)
							shapes = ((PowerPoint.Slide)window.View.Slide).Shapes.Range(i);
					}
					break;
				default:
					break;
			}

			if (shapes == null || shapes.Count == 0)
				return null;
			else
				return shapes;
		}

		#endregion

		public PPT(PowerPoint.Application app)
		{
			this.myApp = app;
//			this.myInitialNumberPresentations = this.myApp.Presentations.Count;
		}
/*
		// TODO: add the ability to close PPT when a static count of the number of instances reaches zero.
		[Obsolete("Clients should no longer use the default constructor as it implicitly news a PowerPoint.ApplicationClass (causing immortal PPT); call one-place constructor instead with the application object passed from the add-in.", false)]
		public PPT() : this(new PowerPoint.ApplicationClass())
		{
		}

		~PPT()
		{
			if (this.myApp != null)
			{
				// Grab value to avoid threading causing more than one value below.
				CloseConditions condition = this.CloseOnExit;

				// Quit if called for.
				if (condition == CloseConditions.Always)
					this.myApp.Quit();
				else if (condition == CloseConditions.IfNoPresentations &&
					this.myApp.Presentations.Count == 0)
					this.myApp.Quit();

				// Regardless, clear the pointer to allow deallocation of COM object.
				this.myApp = null;
			}
		}
*/
		#region Properties
/*
		public enum CloseConditions { Never, Always, IfNoPresentations }

		/// <summary>
		/// Should this object attempt to close PPT when it is destroyed? Defaults to Never.
		/// </summary>
		public CloseConditions CloseOnExit
		{
			get { return this.myCloseOnExit; }
			set { this.myCloseOnExit = value; }
		}
*/
		public PowerPoint.Application App { get { return this.myApp; } }
/*
		/// <summary>
		/// Number of presentations that were open when this object was created.
		/// </summary>
		public int InitialNumberPresentations { get { return this.myInitialNumberPresentations; } }
*/
		#endregion

		#region Members

		private PowerPoint.Application myApp = null;
/*		private CloseConditions myCloseOnExit = CloseConditions.Never;
		private readonly int myInitialNumberPresentations;
*/
		// Keeps a handle on every single button/bar created so that it won't disappear as long
		// as this PPT object exists.
//		private IList /* of PPT/Office stuff */ myObjectCache = new ArrayList();

		#endregion
/*
		#region Test Drivers

		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[MTAThread]
		static void Main(string[] args)
		{
			Test();
			Console.ReadLine();
		}

		static void Test()
		{
			PPT obj = new PPT();
			Console.WriteLine("Num presentations: " + obj.InitialNumberPresentations);
			obj.CloseOnExit = CloseConditions.IfNoPresentations;
			Console.ReadLine();
		}

		#endregion
*/	}

	/// <summary>
	/// Designed to facilitate the creation of a "unique" key based on a shape that shouldn't change for that
	/// shape even as it is manipulated but should be different for a copy of that shape. Note that Equals is
	/// equivalent to ShapesMatch.
	/// </summary>
	public class ShapeKey : IComparable
	{
		#region Constants

		public const char SEPARATOR_CHAR = '-';
		public static readonly string SEPARATOR_STRING = new string(SEPARATOR_CHAR, 1);

		#endregion

		#region Constructors

		/// <summary>
		/// Create a shape key from the given string. Should be used only with 
		/// stringified shape keys!
		/// </summary>
		public ShapeKey(string key)
		{
			string[] parts = key.Split(SEPARATOR_CHAR);
			if (parts.Length < 2)
				throw new FormatException("key parameter must be of the form: <string>" + SEPARATOR_STRING + "<number>; given key \"" + key + "\" has no '.'");
			try
			{
				this.myNumber = int.Parse(parts[parts.Length - 1]);
			}
			catch (FormatException e)
			{
				throw new FormatException("key parameter must be of the form: <string>" + SEPARATOR_STRING + "<number>; given key \"" + key + "\" does not end with a valid number", e);
			}
			catch (OverflowException e)
			{
				throw new FormatException("key parameter must be of the form: <string>" + SEPARATOR_STRING + "<number>; given key \"" + key + "\" ends in a number that is too large/small", e);
			}

			string[] firstParts = new string[parts.Length - 1];
			System.Array.Copy(parts, firstParts, firstParts.Length);
			this.myFluff = string.Join(SEPARATOR_STRING, firstParts);
		}

		/// <summary>
		/// Create a shape key from the given shape.
		/// </summary>
		/// <param name="shape">non-null, etc.</param>
		// TODO: decide whether or not to catch the shape.Name/shape.Id COMExceptions if the shape does not exist.
		public ShapeKey(PowerPoint.Shape shape, int slideNumber)
		{
			if (shape == null) throw new ArgumentNullException("shape");

			// Assuming that shape ID is unique across slides, using it to "uniquify" names.
			// Simultaneously stripping off the extraneous ".xxx" that was previously used (badly) by PPT to uniquify.
			this.myFluff = shape.Name.Split('.')[0];
			this.myNumber = shape.Id;
            this.mySlideNumber = slideNumber;
		}

		#endregion

		#region Methods

		public override string ToString()
		{
			return this.myFluff + SEPARATOR_STRING + this.myNumber;
		}

		public bool ShapesMatch(ShapeKey other)
		{
			if (other == null) throw new ArgumentNullException("other");
			return (this.myNumber == other.myNumber && this.mySlideNumber == other.mySlideNumber);
		}

		public static bool ShapesMatch(PowerPoint.Shape lShape, int slide1, PowerPoint.Shape rShape, int slide2)
		{
			if (lShape == null) throw new ArgumentNullException("lShape");
			if (rShape == null) throw new ArgumentNullException("rShape");
			return new ShapeKey(lShape,slide1).ShapesMatch(new ShapeKey(rShape,slide2));
		}

		public static bool ShapesMatch(ShapeKey lSK, ShapeKey rSK)
		{
			if (lSK == null) throw new ArgumentNullException("lSK");
			if (rSK == null) throw new ArgumentNullException("rSK");
			return lSK.ShapesMatch(rSK);
		}

		#region Comparison/Hashing

		public override bool Equals(object obj)
		{
			ShapeKey otherSK = obj as ShapeKey;
			if (otherSK == null) return false;
			return this.ShapesMatch(otherSK);
		}

		public override int GetHashCode()
		{
			return this.myNumber.GetHashCode();
		}

		public int CompareTo(object obj)
		{
            if (obj == this) return 0;
            else if (obj == null) return 1; // by def'n, greater than null
            else if (!(obj is ShapeKey)) throw new ArgumentException("not a ShapeKey", "obj");
            else if ((obj as ShapeKey).mySlideNumber != this.mySlideNumber) return (this.mySlideNumber - (obj as ShapeKey).mySlideNumber);
            else return (this.myNumber - (obj as ShapeKey).myNumber);
		}

		#endregion

		#endregion

		#region Members

		private string myFluff;
		private int myNumber;
        private int mySlideNumber;

		#endregion
	}
}
