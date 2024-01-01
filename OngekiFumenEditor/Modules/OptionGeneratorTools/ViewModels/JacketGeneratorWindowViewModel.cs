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
	[Export(typeof(IJacketGenerator))]
	public class JacketGeneratorWindowViewModel : WindowBase, IJacketGenerator
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
			((!string.IsNullOrWhiteSpace(GenerateOption.InputImageFilePath)) && File.Exists(GenerateOption.InputImageFilePath)) &&
			((!string.IsNullOrWhiteSpace(GenerateOption.OutputAssetbundleFolderPath)) && Directory.Exists(GenerateOption.OutputAssetbundleFolderPath));

		private JacketGenerateOption generateOption = new();
		public JacketGenerateOption GenerateOption
		{
			get => generateOption;
			set
			{
				Set(ref generateOption, value);
			}
		}

		public JacketGeneratorWindowViewModel()
		{

		}

		public void SelectImageFilePath()
		{
			var imageFilePath = FileDialogHelper.OpenFile(Resources.SelectImage, new[]
			{
				(".png","图片文件")
			});

			GenerateOption.InputImageFilePath = imageFilePath;
			NotifyOfPropertyChange(() => IsGeneratable);
		}

		public void SelectOutputFolder()
		{
			if (!FileDialogHelper.OpenDirectory(Resources.SelectOutputFolder, out var outputFolderPath))
				return;

			GenerateOption.OutputAssetbundleFolderPath = outputFolderPath;
			NotifyOfPropertyChange(() => IsGeneratable);
		}

		public async Task<bool> Generate(JacketGenerateOption option)
		{
			var result = await JacketGenerateWrapper.Generate(option);
			return result.IsSuccess;
		}

		public async void Generate()
		{
			IsBusy = true;
			var result = await JacketGenerateWrapper.Generate(GenerateOption);
			if (!result.IsSuccess)
			{
				var msg = result.Message;
				MessageBox.Show($"{Resources.GenerateJacketFileFail}{msg}");
			}
			else
			{
				if (MessageBox.Show(Resources.GenerateJacketFileSuccess, string.Empty, MessageBoxButton.YesNo) == MessageBoxResult.Yes)
					ProcessUtils.OpenPath(GenerateOption.OutputAssetbundleFolderPath);
			}
			IsBusy = false;
		}
	}
}
