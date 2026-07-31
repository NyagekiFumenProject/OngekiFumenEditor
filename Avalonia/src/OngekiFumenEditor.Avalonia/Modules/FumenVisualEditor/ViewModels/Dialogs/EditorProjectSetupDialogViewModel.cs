using Gekimini.Avalonia.Framework.Dialogs;
using Gekimini.Avalonia.Modules.Window.ViewModels;
using Gekimini.Avalonia.Utils.MethodExtensions;
using OngekiFumenEditor.Avalonia.Kernel.Audio;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Avalonia.Parser;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Utils;
using System;
using System.IO;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels.Dialogs
{
	public class EditorProjectSetupDialogViewModel : WindowViewModelBase
	{
		private EditorProjectDataModel editorProjectData = new();
		public EditorProjectDataModel EditorProjectData
		{
			get => editorProjectData;
			set => SetProperty(ref editorProjectData, value);
		}

		private void ShowMessage(string content)
		{
			_ = IoC.Get<IDialogManager>().ShowMessageDialog(content);
		}

		public async void OnSelectAudioFilePathButtonClicked()
		{
			var filePath = await FileDialogHelper.OpenFileAsync(null, FileDialogHelper.GetSupportAudioFileExtensionFilterList());
			if (string.IsNullOrWhiteSpace(filePath))
				return;

			EditorProjectData.AudioFilePath = filePath;
			using var audio = await IoC.Get<IAudioManager>().LoadAudioAsync(EditorProjectData.AudioFilePath);
			var durationMs = audio.Duration;
			EditorProjectData.AudioDuration = durationMs;
		}

		public async void OnSelectFumenFilePathButtonClicked()
		{
			var filePath = await FileDialogHelper.OpenFileAsync(null, FileDialogHelper.GetSupportFumenFileExtensionFilterList());
			if (string.IsNullOrWhiteSpace(filePath))
				return;

			try
			{
				using var fs = File.OpenRead(filePath);
				var fumen = await IoC.Get<IFumenParserManager>().GetDeserializer(filePath).DeserializeAsync(fs);

				EditorProjectData.FumenFilePath = filePath;
				EditorProjectData.BaseBPM = fumen.MetaInfo.BpmDefinition.First;
				EditorProjectData.Fumen = fumen;
			}
			catch (Exception e)
			{
				ShowMessage($"{Lang.CantLoadFumen}{e.Message}");
			}
		}

		public async void OnCreateButtonClicked()
		{
			if (string.IsNullOrWhiteSpace(EditorProjectData.AudioFilePath) || !File.Exists(EditorProjectData.AudioFilePath))
			{
				ShowMessage(Lang.AudioFileNotFound);
				return;
			}

			await TryCloseAsync(true);
		}
	}
}
