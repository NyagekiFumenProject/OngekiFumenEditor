using CommunityToolkit.Mvvm.Input;
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
	public partial class EditorProjectSetupDialogViewModel : WindowViewModelBase
	{
		private EditorProjectDataModel editorProjectData = new();
		public EditorProjectDataModel EditorProjectData
		{
			get => editorProjectData;
			set => SetProperty(ref editorProjectData, value);
		}

		private Task ShowMessageAsync(string content)
		{
			return IoC.Get<IDialogManager>().ShowMessageDialog(content);
		}

		[RelayCommand]
		private async Task SelectAudioFilePathAsync()
		{
			var filePath = await FileDialogHelper.OpenFileAsync(null, FileDialogHelper.GetSupportAudioFileExtensionFilterList());
			if (string.IsNullOrWhiteSpace(filePath))
				return;

			EditorProjectData.AudioFilePath = filePath;
			using var audio = await IoC.Get<IAudioManager>().LoadAudioAsync(EditorProjectData.AudioFilePath);
			var durationMs = audio.Duration;
			EditorProjectData.AudioDuration = durationMs;
		}

		[RelayCommand]
		private async Task SelectFumenFilePathAsync()
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
				await ShowMessageAsync($"{Lang.CantLoadFumen}{e.Message}");
			}
		}

		[RelayCommand]
		private async Task CreateAsync()
		{
			if (string.IsNullOrWhiteSpace(EditorProjectData.AudioFilePath) || !File.Exists(EditorProjectData.AudioFilePath))
			{
				await ShowMessageAsync(Lang.AudioFileNotFound);
				return;
			}

			await TryCloseAsync(true);
		}
	}
}
