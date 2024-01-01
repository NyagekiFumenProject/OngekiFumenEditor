using Caliburn.Micro;
using Gemini.Framework.Commands;
using Microsoft.Win32;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Parser;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Commands.OgkrImpl.StandardizeFormat
{
	[CommandHandler]
	public class StandardizeFormatCommandHandler : CommandHandlerBase<StandardizeFormatCommandDefinition>
	{
		public override void Update(Command command)
		{
			base.Update(command);
			command.Enabled = IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor is not null;
		}

		public override async Task Run(Command command)
		{
			if (IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor is not FumenVisualEditorViewModel editor)
				return;
			var saveFileDialog = new SaveFileDialog();
			saveFileDialog.Filter = FileDialogHelper.BuildExtensionFilter((".ogkr", Resources.OngekiFumenStandardized));
			saveFileDialog.Title = Resources.NewFumenFileSavePath;

			if (saveFileDialog.ShowDialog() != true)
				return;

			editor.LockAllUserInteraction();

			var newFilePath = saveFileDialog.FileName;

			var taskResult = await Utils.Ogkr.StandardizeFormat.Process(editor.Fumen);
			editor.UnlockAllUserInteraction();

			if (!taskResult.IsSuccess)
			{
				if (!string.IsNullOrWhiteSpace(taskResult.Message))
					MessageBox.Show(taskResult.Message, Resources.StandardizeFormat, MessageBoxButton.OK);
				return;
			}

			var serializer = IoC.Get<IFumenParserManager>().GetSerializer(newFilePath);
			await File.WriteAllBytesAsync(newFilePath, await serializer.SerializeAsync(taskResult.SerializedFumen));

			if (MessageBox.Show(Resources.NewFumenFileSaveDone, Resources.StandardizeFormat, MessageBoxButton.YesNo) == MessageBoxResult.Yes)
			{
				Process.Start(new ProcessStartInfo(Path.GetDirectoryName(newFilePath))
				{
					UseShellExecute = true
				});
			}

			return;
		}
	}
}