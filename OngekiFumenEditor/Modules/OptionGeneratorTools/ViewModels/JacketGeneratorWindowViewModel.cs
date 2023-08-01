using Caliburn.Micro;
using Gemini.Framework;
using OngekiFumenEditor.Modules.OgkiFumenListBrowser;
using OngekiFumenEditor.Modules.OptionGeneratorTools.Kernel;
using OngekiFumenEditor.Modules.OptionGeneratorTools.Models;
using OngekiFumenEditor.Modules.OptionGeneratorTools.Models.EnumStructs;
using OngekiFumenEditor.Modules.OptionGeneratorTools.ViewModels.Dialogs;
using OngekiFumenEditor.Modules.OptionGeneratorTools.Views.Dialogs;
using OngekiFumenEditor.Utils;
using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

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
            var imageFilePath = FileDialogHelper.OpenFile("选择图片", new[]
            {
                (".png","图片文件")
            });

            GenerateOption.InputImageFilePath = imageFilePath;
            NotifyOfPropertyChange(() => IsGeneratable);
        }

        public void SelectOutputFolder()
        {
            if (!FileDialogHelper.OpenDirectory("选择输出文件夹", out var outputFolderPath))
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
                MessageBox.Show($"生成封面文件失败:{msg}");
            }
            else
            {
                if (MessageBox.Show($"生成封面文件成功,是否打开输出文件夹?", string.Empty, MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    ProcessUtils.OpenPath(GenerateOption.OutputAssetbundleFolderPath);
            }
            IsBusy = false;
        }
    }
}
