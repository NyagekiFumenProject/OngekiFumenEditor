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
					MessageBox.Show(Resources.FumenFileNotSelect);
					return;
				}

				if (parserManager.GetDeserializer(InputFumenFilePath) is not IFumenDeserializable deserializable)
				{
					MessageBox.Show(Resources.FumenFileDeserializeNotSupport);
					return;
				}

				try
				{
					using var stream = File.OpenRead(InputFumenFilePath);
					fumen = await deserializable.DeserializeAsync(stream);
				}
				catch (Exception e)
				{
					MessageBox.Show($"{Resources.FumenLoadFailed}{e.Message}");
				}
			}

			if (fumen is null)
			{
				MessageBox.Show(Resources.NoFumenInput);
				return;
			}

			if (string.IsNullOrWhiteSpace(OutputFumenFilePath))
			{
				MessageBox.Show(Resources.OutputFumenFileNotSelect);
				return;
			}

			if (parserManager.GetSerializer(OutputFumenFilePath) is not IFumenSerializable serializable)
			{
				MessageBox.Show(Resources.OutputFumenNotSupport);
				return;
			}

			try
			{
				var data = await serializable.SerializeAsync(fumen);
				await File.WriteAllBytesAsync(OutputFumenFilePath, data);
				MessageBox.Show(Resources.ConvertSuccess);
			}
			catch (Exception e)
			{
				MessageBox.Show($"{Resources.ConvertFail}{e.Message}");
			}
		}
	}
}
