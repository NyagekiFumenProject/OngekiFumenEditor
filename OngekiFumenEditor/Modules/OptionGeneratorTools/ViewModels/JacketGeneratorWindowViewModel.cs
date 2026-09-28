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
            var setting = Properties.OptionGeneratorToolsSetting.Default;
            var needSave = false;

            //路径类参数若已失效则回退默认值并清空已存字段，避免下次仍套用无效路径
            if (!string.IsNullOrWhiteSpace(setting.Jacket_InputImageFilePath))
            {
                if (File.Exists(setting.Jacket_InputImageFilePath))
                    GenerateOption.InputImageFilePath = setting.Jacket_InputImageFilePath;
                else
                {
                    setting.Jacket_InputImageFilePath = string.Empty;
                    needSave = true;
                }
            }

            if (!string.IsNullOrWhiteSpace(setting.Jacket_OutputAssetbundleFolderPath))
            {
                if (Directory.Exists(setting.Jacket_OutputAssetbundleFolderPath))
                    GenerateOption.OutputAssetbundleFolderPath = setting.Jacket_OutputAssetbundleFolderPath;
                else
                {
                    setting.Jacket_OutputAssetbundleFolderPath = string.Empty;
                    needSave = true;
                }
            }

            GenerateOption.Width = setting.Jacket_Width;
            GenerateOption.Height = setting.Jacket_Height;
            GenerateOption.WidthSmall = setting.Jacket_WidthSmall;
            GenerateOption.HeightSmall = setting.Jacket_HeightSmall;
            GenerateOption.UpdateAssetBytesFile = setting.Jacket_UpdateAssetBytesFile;

            if (needSave)
                setting.Save();
        }

        private void SaveGenerateOptionToSetting()
        {
            var setting = Properties.OptionGeneratorToolsSetting.Default;

            setting.Jacket_InputImageFilePath = GenerateOption.InputImageFilePath ?? string.Empty;
            setting.Jacket_OutputAssetbundleFolderPath = GenerateOption.OutputAssetbundleFolderPath ?? string.Empty;
            setting.Jacket_Width = GenerateOption.Width;
            setting.Jacket_Height = GenerateOption.Height;
            setting.Jacket_WidthSmall = GenerateOption.WidthSmall;
            setting.Jacket_HeightSmall = GenerateOption.HeightSmall;
            setting.Jacket_UpdateAssetBytesFile = GenerateOption.UpdateAssetBytesFile;

            setting.Save();
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
            //用户在界面上确认参数并点击生成后，把环境类参数写回设置
            SaveGenerateOptionToSetting();

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
