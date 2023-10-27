using Gemini.Framework.Commands;
using Microsoft.Win32;
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
			openFileDialog.Filter = FileDialogHelper.BuildExtensionFilter((".ogkr", "音击谱面"), (".nyageki", "音击谱面"));
			openFileDialog.Title = "快速打开音击谱面";
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
				var msg = $"无法快速打开谱面: {e.Message}";
				Log.LogError(e.Message);
				MessageBox.Show(msg);
			}
		}
	}
}