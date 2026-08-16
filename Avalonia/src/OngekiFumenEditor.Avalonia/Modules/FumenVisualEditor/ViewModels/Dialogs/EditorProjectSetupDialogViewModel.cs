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
		private EditorContext editorContext = new() { ProjectData = new EditorProjectDataModel() };
		private bool keepRuntimeFilesAfterClose;

		public EditorContext EditorContext
		{
			get => editorContext;
			set
			{
				if (ReferenceEquals(editorContext, value))
					return;

				editorContext?.Dispose();
				SetProperty(ref editorContext, value);
				OnPropertyChanged(nameof(EditorProjectData));
			}
		}

		// 纯数据模型，仅用于绑定持久化设置项（路径、时长等）。
		public EditorProjectDataModel EditorProjectData => EditorContext?.ProjectData;

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
				EditorContext.FileAccessContext ??= new EditorFileAccessContext();
				EditorContext.FileAccessContext.AudioFile = file;
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
				EditorContext.BaseBPM = fumen.MetaInfo.BpmDefinition.First;
				EditorContext.Fumen = fumen;
				EditorContext.FileAccessContext ??= new EditorFileAccessContext();
				EditorContext.FileAccessContext.FumenFile = file;
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
			if (EditorContext.AudioFile is null &&
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
				EditorContext?.Dispose();

			base.OnViewBeforeUnload(view);
		}
	}
}
