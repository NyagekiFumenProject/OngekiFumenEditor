using Caliburn.Micro;
using Microsoft.Win32;
using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Parser;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System;
using System.IO;
using System.Windows;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Dialogs
{
	public class EditorProjectSetupDialogViewModel : Screen
	{
		private EditorProjectDataModel editorProjectData = new();
		public EditorProjectDataModel EditorProjectData
		{
			get => editorProjectData;
			set => Set(ref editorProjectData, value);
		}

		public async void OnSelectAudioFilePathButtonClicked()
		{
			var dialog = new OpenFileDialog();
			dialog.Multiselect = false;
			dialog.Filter = FileDialogHelper.GetSupportAudioFileExtensionFilter();
			if (dialog.ShowDialog() == true)
			{
				EditorProjectData.AudioFilePath = dialog.FileName;
				using var audio = await IoC.Get<IAudioManager>().LoadAudioAsync(EditorProjectData.AudioFilePath);
				var durationMs = audio.Duration;
				EditorProjectData.AudioDuration = durationMs;
			}
		}

		public async void OnSelectFumenFilePathButtonClicked()
		{
			var dialog = new OpenFileDialog();
			dialog.Multiselect = false;
			dialog.Filter = FileDialogHelper.GetSupportFumenFileExtensionFilter();
			if (dialog.ShowDialog() == true)
			{
				try
				{
					using var fs = File.OpenRead(dialog.FileName);
					var fumen = await IoC.Get<IFumenParserManager>().GetDeserializer(dialog.FileName).DeserializeAsync(fs);

					EditorProjectData.FumenFilePath = dialog.FileName;
					EditorProjectData.BaseBPM = fumen.MetaInfo.BpmDefinition.First;
					EditorProjectData.Fumen = fumen;
				}
				catch (Exception e)
				{
					MessageBox.Show($"{Resources.CantLoadFumen}{e.Message}");
				}
			}
		}

		public async void OnCreateButtonClicked()
		{
			if (string.IsNullOrWhiteSpace(EditorProjectData.AudioFilePath) || !File.Exists(EditorProjectData.AudioFilePath))
			{
				MessageBox.Show(Resources.AudioFileNotFound);
				return;
			}

			await TryCloseAsync(true);
		}
	}
}
