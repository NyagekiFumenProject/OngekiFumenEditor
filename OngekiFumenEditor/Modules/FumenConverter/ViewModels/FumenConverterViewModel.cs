using Caliburn.Micro;
using Gemini.Framework;
using Microsoft.Win32;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Parser;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Windows;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenConverter.Kernel;
using MessageBox = System.Windows.MessageBox;

namespace OngekiFumenEditor.Modules.FumenConverter.ViewModels
{
    [Export(typeof(IFumenConverterWindow))]
    public class FumenConverterViewModel : WindowBase, IFumenConverterWindow
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
            var option = new FumenConvertOption()
            {
                InputFumenFilePath = IsUseInputFile ? InputFumenFilePath : string.Empty,
                OutputFumenFilePath = OutputFumenFilePath
            };

            OngekiFumen input = null;	
            
            if (!IsUseInputFile) {
                var editor = IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor;
                if (editor is not null) {
                    input = editor.Fumen;
                }
                else {
                    MessageBox.Show(Resources.NoEditorTarget);
                    return;
                }
            }
			
            var result = await FumenConverterWrapper.Generate(option, input);
            MessageBox.Show(result.IsSuccess ? Resources.ConvertSuccess : $"{Resources.ConvertFail} {result.Message}");
        }
    }
}