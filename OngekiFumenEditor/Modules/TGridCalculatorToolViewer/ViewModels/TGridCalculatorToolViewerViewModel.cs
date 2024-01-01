using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Services;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;

namespace OngekiFumenEditor.Modules.TGridCalculatorToolViewer.ViewModels
{
	[Export(typeof(ITGridCalculatorToolViewer))]
	public class TGridCalculatorToolViewerViewModel : Tool, ITGridCalculatorToolViewer
	{
		private FumenVisualEditorViewModel editor;
		public FumenVisualEditorViewModel Editor
		{
			get => editor;
			set
			{
				Set(ref editor, value);
				NotifyOfPropertyChange(() => IsEnabled);
			}
		}

		private ITimelineObject timelineObject;
		public ITimelineObject TimelineObject
		{
			get => timelineObject;
			set
			{
				Set(ref timelineObject, value);
				if (value is not null && IsAutoUpdateTimeIfSelectedObject)
				{
					Unit = TimelineObject.TGrid.Unit;
					Grid = TimelineObject.TGrid.Grid;
					UpdateToTGrid();
					UpdateToMsec();
				}
				NotifyOfPropertyChange(() => IsSelectedObject);
			}
		}

		private int grid = 0;
		public int Grid
		{
			get => grid;
			set
			{
				Set(ref grid, value);
				UpdateToMsec();
			}
		}

		private float unit = 0;
		public float Unit
		{
			get => unit;
			set
			{
				Set(ref unit, value);
				UpdateToMsec();
			}
		}

		private string msecStr = "00:00:00.000";
		public string MsecStr
		{
			get => msecStr;
			set
			{
				Set(ref msecStr, value);
				UpdateToTGrid();
			}
		}

		public bool IsEnabled => Editor is not null;

		public bool IsAutoUpdateTimeIfSelectedObject { get; set; } = false;

		public bool IsSelectedObject => TimelineObject is not null;

		public override PaneLocation PreferredLocation => PaneLocation.Right;

		public TGridCalculatorToolViewerViewModel()
		{
			DisplayName = Resources.TGridCalculatorToolViewer;
			IoC.Get<IEditorDocumentManager>().OnActivateEditorChanged += OnActivateEditorChanged;
			Editor = IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor;
			IoC.Get<IFumenObjectPropertyBrowser>().PropertyChanged += TGridCalculatorToolViewerViewModel_PropertyChanged;
		}

		public void UpdateToTGrid()
		{
			if (Editor is not null)
			{
				var audioTime = ParseMsecStr();
				Log.LogInfo($"{MsecStr}  ->  {audioTime}");
				var tGrid = TGridCalculator.ConvertAudioTimeToTGrid(audioTime, Editor);
				Unit = tGrid.Unit;
				Grid = tGrid.Grid;
			}
		}

		public void UpdateToMsec()
		{
			if (Editor is not null)
				msecStr = TGridCalculator.ConvertTGridToAudioTime(new(Unit, Grid), Editor).ToString("hh\\:mm\\:ss\\.fff");
			NotifyOfPropertyChange(() => MsecStr);
		}

		private void TGridCalculatorToolViewerViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof(IFumenObjectPropertyBrowser.SelectedObjects):
					var objs = ((IFumenObjectPropertyBrowser)sender).SelectedObjects;
					if (objs.Count == 1)
						TimelineObject = objs.OfType<ITimelineObject>().First();
					else
						TimelineObject = null;
					break;
				default:
					break;
			}
		}

		private void OnActivateEditorChanged(FumenVisualEditorViewModel @new, FumenVisualEditorViewModel old)
		{
			Editor = @new;
		}

		private TimeSpan ParseMsecStr()
		{
			if (MsecStr.Contains(":"))
			{
				var r = MsecStr.Split(":");
				var sms = r.LastOrDefault();
				var w = sms.Split(".");
				var msec = w.Length == 2 ? int.Parse(w[1]) : 0;
				var revArr = r.Reverse().Skip(1).Select(x => int.Parse(x)).ToArray();
				var sec = int.Parse(w[0]);

				//hh:mm:ss.msec
				//01:05:500.571

				return (TimeSpan.FromSeconds(sec) +
					TimeSpan.FromMinutes(revArr.ElementAtOrDefault(0)) +
					TimeSpan.FromHours(revArr.ElementAtOrDefault(1)) +
					TimeSpan.FromMilliseconds(msec)
					);
			}
			else
			{
				return TimeSpan.FromMilliseconds(float.Parse(MsecStr));
			}
		}

		public void OnRequestEditorScrollTo()
		{
			Editor.ScrollTo(new TGrid(Unit, Grid));
		}

		public void OnRequestApplyTGridToObject()
		{
			TimelineObject.TGrid = new TGrid(Unit, Grid);
		}
	}
}
