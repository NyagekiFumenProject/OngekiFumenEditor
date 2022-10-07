using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Services;
using Microsoft.Win32;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using OngekiFumenEditor.Parser;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace OngekiFumenEditor.Modules.FumenConverter.ViewModels
{
    [Export(typeof(IFumenConverter))]
    public class FumenConverterViewModel : WindowBase, IFumenConverter
    {
        private string inputFumenFilePath = "";
        public string InputFumenFilePath { get => inputFumenFilePath; set => Set(ref inputFumenFilePath, value); }

        private string outputFumenFilePath = "";
        public string OutputFumenFilePath { get => outputFumenFilePath; set => Set(ref outputFumenFilePath, value); }

        private bool isUseInputFile = true;
        public bool IsUseInputFile
        {
            get => isUseInputFile; 
            set
            {
                if (CurrentEditorName is null)
                    Set(ref isUseInputFile, true);
                else
                    Set(ref isUseInputFile, value);
                NotifyOfPropertyChange(() => IsCurrentEditorAsInputFumen);
            }
        }

        public bool IsCurrentEditorAsInputFumen
        {
            get => !IsUseInputFile;
            set => IsUseInputFile = !value;
        }

        public string CurrentEditorName => IoC.Get<IEditorDocumentManager>()?.CurrentActivatedEditor?.DisplayName;

        public FumenConverterViewModel()
        {
            NotifyOfPropertyChange(() => CurrentEditorName);
            IoC.Get<IEditorDocumentManager>().OnActivateEditorChanged += (n, _) => NotifyOfPropertyChange(() => CurrentEditorName);
        }

        public Task ConvertFumenFile(string fromFilePath, string toFilePath)
        {
            throw new NotImplementedException();
        }

        public Task ConvertFumenFile(OngekiFumen fumen, string toFilePath)
        {
            throw new NotImplementedException();
        }

        public void OnOpenSelectInputFileDialog()
        {
            var dialog = new OpenFileDialog();
            dialog.Multiselect = false;
            dialog.Filter = FileDialogHelper.GetSupportFumenFileExtensionFilter();
            if (dialog.ShowDialog() == true)
            {
                InputFumenFilePath = dialog.FileName;
                IsUseInputFile = true;
            }
        }

        public void OnOpenSelectOutputFileDialog()
        {
            var dialog = new SaveFileDialog();
            dialog.Filter = FileDialogHelper.GetSupportFumenFileExtensionFilter();
            if (dialog.ShowDialog() == true)
                OutputFumenFilePath = dialog.FileName;
        }

        public async void OnExecuteConverter()
        {
            var fumen = IoC.Get<IEditorDocumentManager>()?.CurrentActivatedEditor?.Fumen;
            var parserManager = IoC.Get<IFumenParserManager>();

            if (IsUseInputFile)
            {
                if (!File.Exists(InputFumenFilePath))
                {
                    MessageBox.Show("请先选择输入的谱面文件路径");
                    return;
                }

                if (parserManager.GetDeserializer(InputFumenFilePath) is not IFumenDeserializable deserializable)
                {
                    MessageBox.Show("不支持解析此谱面文件");
                    return;
                }

                try
                {
                    using var stream = File.OpenRead(InputFumenFilePath);
                    fumen = await deserializable.DeserializeAsync(stream);
                }
                catch (Exception e)
                {
                    MessageBox.Show($"加载谱面文件失败 : {e.Message}");
                }
            }

            if (fumen is null)
            {
                MessageBox.Show("无谱面输入");
                return;
            }

            if (string.IsNullOrWhiteSpace(OutputFumenFilePath))
            {
                MessageBox.Show("没钦定谱面输出路径");
                return;
            }

            if (parserManager.GetSerializer(OutputFumenFilePath) is not IFumenSerializable serializable)
            {
                MessageBox.Show("不支持谱面输出路径");
                return;
            }

            try
            {
                var data = await serializable.SerializeAsync(fumen);
                await File.WriteAllBytesAsync(OutputFumenFilePath, data);
                MessageBox.Show("转换成功");
            }
            catch (Exception e)
            {
                MessageBox.Show($"转换失败 : {e.Message}");
            }
        }
    }
}
