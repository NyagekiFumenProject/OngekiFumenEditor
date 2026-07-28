using Microsoft.Win32;
using OngekiFumenEditor.Avalonia.Kernel.Audio;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Avalonia.Parser;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Utils;
using System;
using System.IO;
using Avalonia;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels.Dialogs
{
	public class EditorProjectSetupDialogViewModel : Screen
	{
		private EditorProjectDataModel editorProjectData = new();
		public EditorProjectDataModel EditorProjectData
		{
			get => editorProjectData;
			set => SetProperty(ref editorProjectData, value);
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
					MessageBox.Show($"{Lang.CantLoadFumen}{e.Message}");
				}
			}
		}

		public async void OnCreateButtonClicked()
		{
			if (string.IsNullOrWhiteSpace(EditorProjectData.AudioFilePath) || !File.Exists(EditorProjectData.AudioFilePath))
			{
				MessageBox.Show(Lang.AudioFileNotFound);
				return;
			}

			await TryCloseAsync(true);
		}
	}
}




