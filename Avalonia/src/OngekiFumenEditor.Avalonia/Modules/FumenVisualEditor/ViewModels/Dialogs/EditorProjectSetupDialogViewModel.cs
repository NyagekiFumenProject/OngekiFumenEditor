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
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;
using System;

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
				OnPropertyChanged(nameof(AudioFileDisplayName));
				OnPropertyChanged(nameof(FumenFileDisplayName));
				OnPropertyChanged(nameof(CanEditBaseBpm));
			}
		}

		// 纯数据模型仅绑定持久化设置；运行时文件能力由 EditorContext 持有。
		public EditorProjectDataModel EditorProjectData => EditorContext?.ProjectData;

		public string AudioFileDisplayName => GetDisplayPath(EditorContext?.AudioFile);

		public string FumenFileDisplayName => GetDisplayPath(EditorContext?.FumenFile);

		public bool CanEditBaseBpm => EditorContext?.FumenFile is null;

		private Task ShowMessageAsync(string content)
		{
			return IoC.Get<IDialogManager>().ShowMessageDialog(content);
		}

		[RelayCommand]
		private async Task SelectAudioFileAsync()
		{
			var file = await FileDialogHelper.OpenFileAsync(null, FileDialogHelper.GetSupportAudioFileExtensionFilterList());
			if (file is null)
				return;

			try
			{
				using var audio = await IoC.Get<IAudioManager>().LoadAudioAsync(file);
				EditorProjectData.AudioDuration = audio.Duration;
				EditorContext.FileAccessContext ??= new EditorFileAccessContext();
				EditorContext.FileAccessContext.AudioFile = file;
				file = null;
				OnPropertyChanged(nameof(AudioFileDisplayName));
			}
			finally
			{
				file?.Dispose();
			}
		}

		[RelayCommand]
		private async Task SelectFumenFileAsync()
		{
			var file = await FileDialogHelper.OpenFileAsync(null, FileDialogHelper.GetSupportFumenOpenFileExtensionFilterList());
			if (file is null)
				return;

			try
			{
				await using var fs = await file.OpenRead();
				var deserializer = IoC.Get<IFumenParserManager>().GetDeserializer(file.FileName);
				if (deserializer is null)
					throw new NotSupportedException($"{Lang.DeserializeFumenFileNotSupport}{file.FileName}");
				var fumen = await deserializer.DeserializeAsync(fs);

				EditorContext.BaseBPM = fumen.MetaInfo.BpmDefinition.First;
				EditorContext.Fumen = fumen;
				EditorContext.FileAccessContext ??= new EditorFileAccessContext();
				EditorContext.FileAccessContext.FumenFile = file;
				file = null;
				OnPropertyChanged(nameof(FumenFileDisplayName));
				OnPropertyChanged(nameof(CanEditBaseBpm));
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
			if (EditorContext.AudioFile is null)
			{
				await ShowMessageAsync(Lang.AudioFileNotFound);
				return;
			}

			keepRuntimeFilesAfterClose = true;
			await TryCloseAsync(true);
		}

		private static string GetDisplayPath(ISimpleFile file)
		{
			if (file is null)
				return string.Empty;
			if (!string.IsNullOrWhiteSpace(file.LocalPath))
				return file.LocalPath;
			if (!string.IsNullOrWhiteSpace(file.FullPath))
				return file.FullPath;
			return file.FileName;
		}

		public override void OnViewBeforeUnload(IView view)
		{
			if (!keepRuntimeFilesAfterClose)
				EditorContext?.Dispose();

			base.OnViewBeforeUnload(view);
		}
	}
}
