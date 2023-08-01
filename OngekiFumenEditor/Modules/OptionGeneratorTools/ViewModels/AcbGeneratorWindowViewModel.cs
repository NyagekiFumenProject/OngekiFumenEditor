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
            var imageFilePath = FileDialogHelper.OpenFile("选择音频文件", new[]
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
            if (!FileDialogHelper.OpenDirectory("选择输出文件夹", out var outputFolderPath))
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
                MessageBox.Show($"生成音频文件失败:{msg}");
            }
            else
            {
                if (MessageBox.Show($"生成音频文件成功,是否打开输出文件夹?", string.Empty, MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    ProcessUtils.OpenPath(GenerateOption.OutputFolderPath);
            }
            IsBusy = false;
        }
    }
}
