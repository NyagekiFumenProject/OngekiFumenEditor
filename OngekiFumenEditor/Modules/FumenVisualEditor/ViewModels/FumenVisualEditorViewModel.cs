using Caliburn.Micro;
using Gemini.Framework;
using Microsoft.Win32;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Kernel.RecentFiles;
using OngekiFumenEditor.Kernel.Scheduler;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel.DefaultImpl;
using OngekiFumenEditor.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Dialogs;
using OngekiFumenEditor.Parser;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels
{
	[Export(typeof(FumenVisualEditorViewModel))]
	public partial class FumenVisualEditorViewModel : PersistedDocument
	{
		private IEditorDocumentManager EditorManager => IoC.Get<IEditorDocumentManager>();

		private EditorProjectDataModel editorProjectData = new EditorProjectDataModel();
		public EditorProjectDataModel EditorProjectData
		{
			get
			{
				return editorProjectData;
			}
			set
			{
				var prevFumen = editorProjectData?.Fumen;
				Set(ref editorProjectData, value);
				RecalculateTotalDurationHeight();

				void setupFumen(OngekiFumen cur, OngekiFumen prev)
				{
					if (prev is not null)
					{
						prev.BpmList.OnChangedEvent -= OnTimeSignatureListChanged;
						prev.MeterChanges.OnChangedEvent -= OnTimeSignatureListChanged;
						prev.ObjectModifiedChanged -= OnFumenObjectModifiedChanged;
					}
					if (cur is not null)
					{
						cur.BpmList.OnChangedEvent += OnTimeSignatureListChanged;
						cur.MeterChanges.OnChangedEvent += OnTimeSignatureListChanged;
						cur.ObjectModifiedChanged += OnFumenObjectModifiedChanged;
					}
					NotifyOfPropertyChange(() => Fumen);
				}

				setupFumen(editorProjectData?.Fumen, prevFumen);
			}
		}

		private IAudioPlayer audioPlayer;
		public IAudioPlayer AudioPlayer
		{
			get
			{
				return audioPlayer;
			}
			set
			{
				if (audioPlayer != value)
					audioPlayer?.Dispose();

				Set(ref audioPlayer, value);
			}
		}

		private void OnSettingPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof(EditorSetting.VerticalDisplayScale):
					RecalculateTotalDurationHeight();
					var tGrid = GetCurrentTGrid();
					ScrollTo(tGrid);
					break;
				case nameof(EditorSetting.JudgeLineOffsetY):
					RecalcViewProjectionMatrix();
					break;
				case nameof(EditorSetting.XOffset):
					RecalcViewProjectionMatrix();
					break;
				case nameof(EditorSetting.XGridUnitSpace):
				case nameof(EditorSetting.DisplayTimeFormat):
				case nameof(EditorSetting.BeatSplit):
				case nameof(EditorSetting.XGridDisplayMaxUnit):
				default:
					break;
			}
		}

		public OngekiFumen Fumen => EditorProjectData.Fumen;

		private void OnFumenObjectModifiedChanged(OngekiObjectBase sender, PropertyChangedEventArgs e)
		{
			var objBrowser = IoC.Get<IFumenObjectPropertyBrowser>();
			var selectedObjects = objBrowser.SelectedObjects;

			switch (e.PropertyName)
			{
				case nameof(ISelectableObject.IsSelected):
					/*
                    if (sender is ISelectableObject selectable)
                    {
                        if (selectable.IsSelected)
                        {
                            //点击
                            CurrentSelectedObjects.Add(selectable);
                        }
                        else
                        {
                            //取消点击
                            if (curBrowserObj == sender)
                                objBrowser.SetCurrentOngekiObject(null, this);
                            CurrentSelectedObjects.Remove(selectable);
                        }
                        NotifyOfPropertyChange(() => SelectObjects);
                    }
                    */
					//IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected(this);
					break;
				case nameof(ConnectableChildObjectBase.IsAnyControlSelecting):
					/*
                    foreach (var controlPoint in ((ConnectableChildObjectBase)sender).PathControls)
                    {
                        var contains = CurrentSelectedObjects.Contains(controlPoint);
                        if (controlPoint.IsSelected && !contains)
                            CurrentSelectedObjects.Add(controlPoint);
                        else if ((!controlPoint.IsSelected) && contains)
                            CurrentSelectedObjects.Remove(controlPoint);
                    }
                    */
					break;
				default:
					IsDirty = true;
					break;
			}

		}

		public void RecalculateTotalDurationHeight()
		{
			if (EditorProjectData?.AudioDuration is TimeSpan timeSpan)
			{
				TotalDurationHeight = ConvertToY(TGridCalculator.ConvertAudioTimeToTGrid(timeSpan, this).TotalUnit);
			}
			else
			{
				//todo warning
				TotalDurationHeight = 0;
			}
		}

		private bool isSelectRangeDragging;
		private bool isLeftMouseDown;

		private bool brushMode = false;
		public bool BrushMode
		{
			get => brushMode;
			set
			{
				Set(ref brushMode, value);
				ToastNotify($"{Resource.BrushMode}{(BrushMode ? Resource.Enable : Resource.Disable)}");
			}
		}

		private bool isShowCurveControlAlways = false;
		public bool IsShowCurveControlAlways
		{
			get => isShowCurveControlAlways;
			set
			{
				Set(ref isShowCurveControlAlways, value);
				ToastNotify($"{Resource.ShowCurveControlAlways}{(IsShowCurveControlAlways ? Resource.Enable : Resource.Disable)}");
			}
		}

		public EditorSetting Setting { get; } = new EditorSetting();

		public FumenVisualEditorViewModel() : base()
		{
			//replace owned impl
			UndoRedoManager = new DefaultEditorUndoManager(this);

			Properties.EditorGlobalSetting.Default.PropertyChanged += OnSettingPropertyChanged;
			DisplayName = default;
		}

		#region Document New/Save/Load

		protected override async Task DoNew()
		{
			try
			{
				var dialogViewModel = new EditorProjectSetupDialogViewModel();
				var result = await IoC.Get<IWindowManager>().ShowDialogAsync(dialogViewModel);
				if (result != true)
				{
					Log.LogInfo(Resource.CloseEditorByProjectSetupFail);
					await TryCloseAsync(false);
					return;
				}
				var projectData = dialogViewModel.EditorProjectData;
				if (File.Exists(projectData.FumenFilePath))
				{
					using var fumenFileStream = File.OpenRead(projectData.FumenFilePath);
					var fumenDeserializer = IoC.Get<IFumenParserManager>().GetDeserializer(projectData.FumenFilePath);
					if (fumenDeserializer is null)
						throw new NotSupportedException($"{Resource.DeserializeFumenFileFail}{projectData.FumenFilePath}");
					var fumen = await fumenDeserializer.DeserializeAsync(fumenFileStream);
					projectData.Fumen = fumen;
				}
				EditorProjectData = dialogViewModel.EditorProjectData;
				AudioPlayer = await IoC.Get<IAudioManager>().LoadAudioAsync(editorProjectData.AudioFilePath);
				Log.LogInfo($"FumenVisualEditorViewModel DoNew()");
				await Dispatcher.Yield();
			}
			catch (Exception e)
			{
				var errMsg = $"{Resource.CantCreateProject}{e.Message}";
				Log.LogError(errMsg);
				MessageBox.Show(errMsg);
				await TryCloseAsync(false);
			}
		}

		protected override async Task DoLoad(string filePath)
		{
			try
			{
				using var _ = StatusBarHelper.BeginStatus("Editor project file loading : " + filePath);
				Log.LogInfo($"FumenVisualEditorViewModel DoLoad() : {filePath}");
				var projectData = await EditorProjectDataUtils.TryLoadFromFileAsync(filePath);
				await Load(projectData);
				ToastNotify(Resource.SaveProjectFileAndFumenFile);

				IoC.Get<IEditorRecentFilesManager>().PostRecord(new(filePath, DisplayName, RecentOpenType.NormalDocumentOpen));
			}
			catch (Exception e)
			{
				var errMsg = $"{Resource.CantLoadProject}{e.Message}";
				Log.LogError(errMsg);
				MessageBox.Show(errMsg);
				await TryCloseAsync(false);
			}
		}

		public async Task Load(EditorProjectDataModel projModel)
		{
			try
			{
				EditorProjectData = projModel;
				AudioPlayer = await IoC.Get<IAudioManager>().LoadAudioAsync(editorProjectData.AudioFilePath);

				var dispTGrid = TGridCalculator.ConvertAudioTimeToTGrid(projModel.RememberLastDisplayTime, this);
				ScrollTo(dispTGrid);
			}
			catch (Exception e)
			{
				var errMsg = $"{Resource.CantLoadProject}{e.Message}";
				Log.LogError(errMsg);
				MessageBox.Show(errMsg);
				await TryCloseAsync(false);
			}
		}

		protected override async Task DoSave(string filePath)
		{
			using var _ = StatusBarHelper.BeginStatus("Fumen saving : " + filePath);
			if (string.IsNullOrWhiteSpace(filePath))
			{
				var newProjFilePath = FileDialogHelper.SaveFile(Resource.SaveNewProjectFile, new[] { (FumenVisualEditorProvider.FILE_EXTENSION_NAME, Resource.FumenProjectFile) });
				if (!string.IsNullOrWhiteSpace(newProjFilePath))
					await Save(newProjFilePath);
				return;
			}
			Log.LogInfo($"FumenVisualEditorViewModel DoSave() : {filePath}");
			EditorProjectData.RememberLastDisplayTime = TGridCalculator.ConvertTGridToAudioTime(GetCurrentTGrid(), this);
			if (string.IsNullOrWhiteSpace(EditorProjectData.FumenFilePath))
			{
				//ask fumen file save path before save project.
				var dialog = new SaveFileDialog();

				dialog.Filter = FileDialogHelper.GetSupportFumenFileExtensionFilter();

				if (dialog.ShowDialog() != true)
				{
					MessageBox.Show(Resource.CancelProjectSaveByFumenSaveFail);
					return;
				}

				EditorProjectData.FumenFilePath = dialog.FileName;
			}

			var saveTaskResult = await EditorProjectDataUtils.TrySaveEditorAsync(filePath, EditorProjectData);
			if (!saveTaskResult.IsSuccess)
			{
				Log.LogError(saveTaskResult.ErrorMessage);
				MessageBox.Show(saveTaskResult.ErrorMessage);
			}
			else
			{
				DisplayName = default;
				ToastNotify(Resource.SaveProjectFileAndFumenFile);
				IoC.Get<IEditorRecentFilesManager>().PostRecord(new(filePath, DisplayName, RecentOpenType.NormalDocumentOpen));
			}
		}

		#endregion

		#region Activation

		protected override async Task OnActivateAsync(CancellationToken cancellationToken)
		{
			await base.OnActivateAsync(cancellationToken);
			await IoC.Get<ISchedulerManager>().AddScheduler(this);
			EditorManager.NotifyActivate(this);
		}

		protected override async Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
		{
			await base.OnDeactivateAsync(close, cancellationToken);
			await IoC.Get<ISchedulerManager>().RemoveScheduler(this);
			EditorManager.NotifyDeactivate(this);
			AudioPlayer?.Pause();
		}

		protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
		{
			await base.OnInitializeAsync(cancellationToken);
			EditorManager.NotifyCreate(this);
		}

		public override async Task TryCloseAsync(bool? dialogResult = null)
		{
			await base.TryCloseAsync(dialogResult);

			AudioPlayer?.Pause();
			AudioPlayer?.Dispose();
			AudioPlayer = null;

			if (dialogResult != false)
				EditorManager.NotifyDestory(this);
		}

		#endregion
	}
}
