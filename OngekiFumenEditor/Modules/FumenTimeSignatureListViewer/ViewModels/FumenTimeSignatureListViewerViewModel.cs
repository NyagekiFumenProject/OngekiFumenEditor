using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Services;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;

namespace OngekiFumenEditor.Modules.FumenTimeSignatureListViewer.ViewModels
{
	[Export(typeof(IFumenTimeSignatureListViewer))]
	public class FumenTimeSignatureListViewerViewModel : Tool, IFumenTimeSignatureListViewer
	{
		public class DisplayTimeSignatureItem : PropertyChangedBase
		{
			private TimeSpan startAudioTime;
			public TimeSpan StartAudioTime
			{
				get => startAudioTime;
				set => Set(ref startAudioTime, value);
			}

			private TGrid startTGrid;
			public TGrid StartTGrid
			{
				get => startTGrid;
				set => Set(ref startTGrid, value);
			}

			private MeterChange meter;
			public MeterChange Meter
			{
				get => meter;
				set => Set(ref meter, value);
			}

			private BPMChange bPMChange;
			public BPMChange BPMChange
			{
				get => bPMChange;
				set => Set(ref bPMChange, value);
			}
		}

		public override PaneLocation PreferredLocation => PaneLocation.Bottom;

		public ObservableCollection<DisplayTimeSignatureItem> DisplayTimeSignatures { get; } = new();

		private FumenVisualEditorViewModel editor;
		public FumenVisualEditorViewModel Editor
		{
			get => editor;
			set
			{
				this.RegisterOrUnregisterPropertyChangeEvent(editor, value, OnEditorPropertyChanged);
				Set(ref editor, value);
				Fumen = Editor?.Fumen;
			}
		}

		private OngekiFumen fumen;
		public OngekiFumen Fumen
		{
			get => fumen;
			set
			{
				if (fumen is not null)
				{
					fumen.BpmList.OnChangedEvent -= OnTimeSignatureListChanged;
					fumen.MeterChanges.OnChangedEvent -= OnTimeSignatureListChanged;
				}
				if (value is not null)
				{
					value.BpmList.OnChangedEvent += OnTimeSignatureListChanged;
					value.MeterChanges.OnChangedEvent += OnTimeSignatureListChanged;
				}
				Set(ref fumen, value);
				Log.LogDebug("Refresh time signatures list viewer by fumen object changed.");
				RefreshFumen();
			}
		}

		private void OnTimeSignatureListChanged()
		{
			Log.LogDebug("Refresh time signatures list viewer.");
			RefreshFumen();
		}

		private DisplayTimeSignatureItem currentSelectTimeSignature;
		public DisplayTimeSignatureItem CurrentSelectTimeSignature
		{
			get => currentSelectTimeSignature;
			set
			{
				Set(ref currentSelectTimeSignature, value);
			}
		}

		private void OnEditorPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(FumenVisualEditorViewModel.Fumen))
				Fumen = Editor.Fumen;
		}

		public FumenTimeSignatureListViewerViewModel()
		{
			DisplayName = Resources.FumenTimeSignatureListViewer;
			IoC.Get<IEditorDocumentManager>().OnActivateEditorChanged += (n, o) => Editor = n;
			Editor = IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor;
		}

		private void RefreshFumen()
		{
			if (Editor is null || Fumen is null)
			{
				DisplayTimeSignatures.Clear();
				return;
			}

			using var disp = DisplayTimeSignatures.ToListWithObjectPool(out var removeList);
			CurrentSelectTimeSignature = default;

			var list = Fumen.MeterChanges.GetCachedAllTimeSignatureUniformPositionList(Fumen.BpmList);
			foreach (var ts in list)
			{
				var cacheObj = removeList.FirstOrDefault();
				if (cacheObj is null)
				{
					cacheObj = ObjectPool<DisplayTimeSignatureItem>.Get();
					DisplayTimeSignatures.Add(cacheObj);
				}
				else
				{
					removeList.RemoveAt(0);
				}

				cacheObj.StartAudioTime = ts.audioTime;
				cacheObj.BPMChange = ts.bpm;
				cacheObj.Meter = ts.meter;
				cacheObj.StartTGrid = ts.startTGrid;
			}

			foreach (var item in removeList)
			{
				DisplayTimeSignatures.Remove(item);
				ObjectPool<DisplayTimeSignatureItem>.Return(item);
			}

			NotifyOfPropertyChange(() => CurrentSelectTimeSignature);
		}

		public void OnItemSingleClick(DisplayTimeSignatureItem item)
		{
			OngekiObjectBase obj = item.StartTGrid == item.BPMChange.TGrid ? item.BPMChange : item.Meter;

			/*
            Editor.SelectObjects.Where(x => x != obj).ForEach(x => x.IsSelected = false);
            if (obj is ISelectableObject selectable)
                selectable.IsSelected = true;
            */
			IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected(Editor, obj);
		}

		public void OnItemDoubleClick(DisplayTimeSignatureItem item)
		{
			Editor.ScrollTo(item.StartTGrid);
			IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected(Editor);
		}
	}
}
