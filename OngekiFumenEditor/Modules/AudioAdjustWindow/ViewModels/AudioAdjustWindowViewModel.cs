using Caliburn.Micro;
using Gemini.Framework;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Windows;

namespace OngekiFumenEditor.Modules.AudioAdjustWindow.ViewModels
{
	[Export(typeof(IAudioAdjustWindow))]
	public class AudioAdjustWindowViewModel : WindowBase, IAudioAdjustWindow
	{
		private string inputFumenFilePath = "";
		public string InputFumenFilePath { get => inputFumenFilePath; set => Set(ref inputFumenFilePath, value); }

		private string outputFumenFilePath = "";
		public string OutputFumenFilePath { get => outputFumenFilePath; set => Set(ref outputFumenFilePath, value); }

		private bool isUseInputFile = true;
		public bool IsUseInputFile
		{
			get => isUseInputFile;
			set
			{
				if (CurrentEditorName is null)
					Set(ref isUseInputFile, true);
				else
					Set(ref isUseInputFile, value);
				NotifyOfPropertyChange(() => IsCurrentEditorAsInputFumen);
				NotifyOfPropertyChange(() => CurrentEditorName);
				NotifyOfPropertyChange(() => Bpm);
			}
		}

		public bool IsCurrentEditorAsInputFumen
		{
			get => !IsUseInputFile;
			set => IsUseInputFile = !value;
		}

		public string CurrentEditorName => IoC.Get<IEditorDocumentManager>()?.CurrentActivatedEditor?.DisplayName;

		public float Unit { get; set; } = 0;
		public int Grid { get; set; } = 0;
		public float Seconds { get; set; } = 0;

		private double? bpm = null;
		public double Bpm
		{
			get
			{
				if (IsCurrentEditorAsInputFumen)
					return IoC.Get<IEditorDocumentManager>()?.CurrentActivatedEditor?.Fumen.BpmList.FirstBpm.BPM ?? 0;
				if (bpm is double b)
					return b;
				return default;
			}
			set => Set(ref bpm, value);
		}

		private bool isUseGridOffset = false;
		public bool IsUseGridOffset
		{
			get => isUseGridOffset;
			set
			{
				Set(ref isUseGridOffset, value);
			}
		}

		private bool isRecalculateObjects = false;
		public bool IsRecalculateObjects
		{
			get => isRecalculateObjects;
			set
			{
				Set(ref isRecalculateObjects, value);
			}
		}

		public AudioAdjustWindowViewModel()
		{
			NotifyOfPropertyChange(() => CurrentEditorName);
			IoC.Get<IEditorDocumentManager>().OnActivateEditorChanged += (n, _) => NotifyOfPropertyChange(() => CurrentEditorName);
		}

		public void OnOpenSelectInputFileDialog()
		{
			var result = FileDialogHelper.OpenFile(Resources.SelectAudioFile, FileDialogHelper.GetSupportAudioFileExtensionFilterList());
			if (!string.IsNullOrWhiteSpace(result))
			{
				InputFumenFilePath = result;
				IsUseInputFile = true;
			}
		}

		public void OnOpenSelectOutputFileDialog()
		{
			var result = FileDialogHelper.SaveFile(Resources.SaveNewAudioFile, new[] { (".wav", ".wav Audio File") });
			if (!string.IsNullOrWhiteSpace(result))
				OutputFumenFilePath = result;
		}

		public async void OnExecuteConverter()
		{
			var audioFilePath = "";
			var currentEditor = IoC.Get<IEditorDocumentManager>()?.CurrentActivatedEditor;

			if (IsUseInputFile)
			{
				if (!File.Exists(InputFumenFilePath))
				{
					MessageBox.Show(Resources.ErrorProcessFumenFileNotSelect);
					return;
				}

				audioFilePath = InputFumenFilePath;
			}
			else
			{
				audioFilePath = currentEditor.EditorProjectData.AudioFilePath;
			}

			if (!File.Exists(audioFilePath))
			{
				MessageBox.Show(Resources.ErrorProcessAudioNotFound);
				return;
			}

			if (string.IsNullOrWhiteSpace(OutputFumenFilePath))
			{
				MessageBox.Show(Resources.ErrorSaveAudioFileNotSelect);
				return;
			}

			var timeOffset = TimeSpan.Zero;
			if (IsUseGridOffset)
			{
				var bpmChange = new BPMChange()
				{
					BPM = Bpm,
					TGrid = TGrid.Zero
				};
				var offset = new GridOffset(Unit, Grid);
				var msec = MathUtils.CalculateBPMLength(bpmChange, TGrid.Zero + offset);
				timeOffset = TimeSpan.FromMilliseconds(msec);
			}
			else
			{
				timeOffset = TimeSpan.FromSeconds(Seconds);
			}

			try
			{
				var audio = AcbGeneratorFuck.Generator.LoadAsWavFile(audioFilePath);
				var offseted = AcbGeneratorFuck.Generator.AdjustDuration(audio, timeOffset.TotalSeconds);

				using var fs = File.OpenWrite(OutputFumenFilePath);

				if (IsCurrentEditorAsInputFumen)
				{
					if (IsRecalculateObjects)
					{
						var offset = currentEditor.Fumen.BpmList.FirstBpm.LengthConvertToOffset(timeOffset.TotalMilliseconds);
						var map = new Dictionary<ITimelineObject, (TGrid before, TGrid after)>();

						foreach (var timelineObject in currentEditor.Fumen.GetAllDisplayableObjects().OfType<ITimelineObject>())
						{
							var newTGrid = timelineObject.TGrid + offset;
							if (newTGrid is null)
							{
								MessageBox.Show($"{Resources.ErrorCantApplyNewAdjust}{timelineObject}");
								return;
							}

							map[timelineObject] = (timelineObject.TGrid, newTGrid);
						}

						currentEditor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Resources.ApplyAudioAdjust, () =>
						{
							foreach (var item in map)
								item.Key.TGrid = item.Value.after.CopyNew();
						}, () =>
						{
							foreach (var item in map)
								item.Key.TGrid = item.Value.before.CopyNew();
						}));
					}
				}

				offseted.SaveTo(fs);
				if (IsCurrentEditorAsInputFumen)
					MessageBox.Show(Resources.ApplyAudioAdjustSuccessButSuggest);
				else
					MessageBox.Show(Resources.ApplyAudioAdjustSuccess);
			}
			catch (Exception e)
			{
				MessageBox.Show($"{Resources.ApplyAudioAdjustFail}{e.Message}");
			}
		}
	}
}
