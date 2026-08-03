using CommunityToolkit.Mvvm.Input;
using Gekimini.Avalonia.Framework.Dialogs;
using Gekimini.Avalonia.Modules.Window.ViewModels;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Gekimini.Avalonia.Views;
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
		private bool keepRuntimeFilesAfterClose;

		public EditorProjectDataModel EditorProjectData
		{
			get => editorProjectData;
			set
			{
				if (ReferenceEquals(editorProjectData, value))
					return;

				editorProjectData?.DisposeRuntimeFiles();
				SetProperty(ref editorProjectData, value);
			}
		}

		private Task ShowMessageAsync(string content)
		{
			return IoC.Get<IDialogManager>().ShowMessageDialog(content);
		}

		[RelayCommand]
		private async Task SelectAudioFilePathAsync()
		{
			var file = await FileDialogHelper.OpenFileAsync(null, FileDialogHelper.GetSupportAudioFileExtensionFilterList());
			if (file is null)
				return;

			try
			{
				using var audio = await IoC.Get<IAudioManager>().LoadAudioAsync(file);
				EditorProjectData.AudioFilePath = file.LocalPath ?? file.FullPath;
				EditorProjectData.AudioDuration = audio.Duration;
				EditorProjectData.AudioFile = file;
				file = null;
			}
			finally
			{
				file?.Dispose();
			}
		}

		[RelayCommand]
		private async Task SelectFumenFilePathAsync()
		{
			var file = await FileDialogHelper.OpenFileAsync(null, FileDialogHelper.GetSupportFumenFileExtensionFilterList());
			if (file is null)
				return;

			try
			{
				await using var fs = await file.OpenRead();
				var deserializer = IoC.Get<IFumenParserManager>().GetDeserializer(file.FileName);
				if (deserializer is null)
					throw new NotSupportedException($"{Lang.DeserializeFumenFileNotSupport}{file.FileName}");
				var fumen = await deserializer.DeserializeAsync(fs);

				EditorProjectData.FumenFilePath = file.LocalPath ?? file.FullPath;
				EditorProjectData.BaseBPM = fumen.MetaInfo.BpmDefinition.First;
				EditorProjectData.Fumen = fumen;
				EditorProjectData.FumenFile = file;
				file = null;
			}
			catch (Exception e)
			{
				await ShowMessageAsync($"{Lang.CantLoadFumen}{e.Message}");
			}
			finally
			{
				file?.Dispose();
			}
		}

		[RelayCommand]
		private async Task CreateAsync()
		{
			if (EditorProjectData.AudioFile is null &&
				(string.IsNullOrWhiteSpace(EditorProjectData.AudioFilePath) || !File.Exists(EditorProjectData.AudioFilePath)))
			{
				await ShowMessageAsync(Lang.AudioFileNotFound);
				return;
			}

			keepRuntimeFilesAfterClose = true;
			await TryCloseAsync(true);
		}

		public override void OnViewBeforeUnload(IView view)
		{
			if (!keepRuntimeFilesAfterClose)
				EditorProjectData?.DisposeRuntimeFiles();

			base.OnViewBeforeUnload(view);
		}
	}
}
