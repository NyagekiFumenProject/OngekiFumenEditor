using Gemini.Framework;
using OngekiFumenEditor.Modules.OptionGeneratorTools.Kernel;
using OngekiFumenEditor.Modules.OptionGeneratorTools.Models;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.Modules.OptionGeneratorTools.ViewModels
{
	[Export(typeof(IAcbGenerator))]
	public class AcbGeneratorWindowViewModel : WindowBase, IAcbGenerator
	{
		private bool isBusy = false;
		public bool IsBusy
		{
			get => isBusy;
			set
			{
				Set(ref isBusy, value);
			}
		}

		public bool IsGeneratable =>
			((!string.IsNullOrWhiteSpace(GenerateOption.InputAudioFilePath)) && File.Exists(GenerateOption.InputAudioFilePath)) &&
			((!string.IsNullOrWhiteSpace(GenerateOption.OutputFolderPath)) && Directory.Exists(GenerateOption.OutputFolderPath));

		private AcbGenerateOption generateOption = new();
		public AcbGenerateOption GenerateOption
		{
			get => generateOption;
			set
			{
				Set(ref generateOption, value);
			}
		}

		public AcbGeneratorWindowViewModel()
		{

		}

		public void SelectAcbFilePath()
		{
			var imageFilePath = FileDialogHelper.OpenFile(Resources.SelectAudioFile, new[]
			{
				(".wav","音频文件"),
				(".mp3","音频文件"),
				(".ogg","音频文件"),
			});

			GenerateOption.InputAudioFilePath = imageFilePath;
			NotifyOfPropertyChange(() => IsGeneratable);
		}

		public void SelectOutputFolder()
		{
			if (!FileDialogHelper.OpenDirectory(Resources.SelectOutputFolder, out var outputFolderPath))
				return;

			GenerateOption.OutputFolderPath = outputFolderPath;
			NotifyOfPropertyChange(() => IsGeneratable);
		}

		public async Task<bool> Generate(AcbGenerateOption option)
		{
			var result = await AcbGeneratorFuckWrapper.Generate(option);
			return result.IsSuccess;
		}

		public async void Generate()
		{
			IsBusy = true;
			var result = await AcbGeneratorFuckWrapper.Generate(GenerateOption);
			if (!result.IsSuccess)
			{
				var msg = result.Message;
				MessageBox.Show($"{Resources.GenerateAudioFileFail}{msg}");
			}
			else
			{
				if (MessageBox.Show(Resources.GenerateAudioSuccess, string.Empty, MessageBoxButton.YesNo) == MessageBoxResult.Yes)
					ProcessUtils.OpenPath(GenerateOption.OutputFolderPath);
			}
			IsBusy = false;
		}
	}
}
