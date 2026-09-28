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
            var setting = Properties.OptionGeneratorToolsSetting.Default;
            var needSave = false;

            //路径类参数若已失效则回退默认值并清空已存字段，避免下次仍套用无效路径
            if (!string.IsNullOrWhiteSpace(setting.Acb_InputAudioFilePath))
            {
                if (File.Exists(setting.Acb_InputAudioFilePath))
                    GenerateOption.InputAudioFilePath = setting.Acb_InputAudioFilePath;
                else
                {
                    setting.Acb_InputAudioFilePath = string.Empty;
                    needSave = true;
                }
            }

            if (!string.IsNullOrWhiteSpace(setting.Acb_OutputFolderPath))
            {
                if (Directory.Exists(setting.Acb_OutputFolderPath))
                    GenerateOption.OutputFolderPath = setting.Acb_OutputFolderPath;
                else
                {
                    setting.Acb_OutputFolderPath = string.Empty;
                    needSave = true;
                }
            }

            GenerateOption.PreviewBeginTime = setting.Acb_PreviewBeginTime;
            GenerateOption.PreviewEndTime = setting.Acb_PreviewEndTime;

            if (needSave)
                setting.Save();
        }

        private void SaveGenerateOptionToSetting()
        {
            var setting = Properties.OptionGeneratorToolsSetting.Default;

            setting.Acb_InputAudioFilePath = GenerateOption.InputAudioFilePath ?? string.Empty;
            setting.Acb_OutputFolderPath = GenerateOption.OutputFolderPath ?? string.Empty;
            setting.Acb_PreviewBeginTime = GenerateOption.PreviewBeginTime;
            setting.Acb_PreviewEndTime = GenerateOption.PreviewEndTime;

            setting.Save();
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
            //用户在界面上确认参数并点击生成后，把环境类参数写回设置
            SaveGenerateOptionToSetting();

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
