using Gemini.Framework.Commands;
using Microsoft.Win32;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Commands.OgkrImpl.FastOpenFumen
{
	[CommandHandler]
	public class FastOpenFumenCommandHandler : CommandHandlerBase<FastOpenFumenCommandDefinition>
	{
		public override async Task Run(Command command)
		{
			var openFileDialog = new OpenFileDialog();
			openFileDialog.Filter = FileDialogHelper.BuildExtensionFilter((".ogkr", Resources.OngekiFumen), (".nyageki", Resources.OngekiFumen));
			openFileDialog.Title = Resources.FastOpenOgkrFumen;
			openFileDialog.CheckFileExists = true;

			if (openFileDialog.ShowDialog() != true)
				return;
			var ogkrFilePath = openFileDialog.FileName;

			try
			{
				await DocumentOpenHelper.TryOpenOgkrFileAsDocument(ogkrFilePath);
			}
			catch (Exception e)
			{
				var msg = $"{Resources.CantFastOpenFumen}{e.Message}";
				Log.LogError(e.Message);
				MessageBox.Show(msg);
			}
		}
	}
}