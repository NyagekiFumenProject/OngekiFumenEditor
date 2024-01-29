using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.Kernel.ArgProcesser.DefaultImp
{
	[Export(typeof(IProgramArgProcessManager))]
	internal class DefaultArgProcessManager : IProgramArgProcessManager
	{
		public async Task ProcessArgs(string[] args)
		{
			void ErrorExit(string message)
			{
				MessageBox.Show(message, Resources.Error, MessageBoxButton.OK, MessageBoxImage.Stop);
				Application.Current.Shutdown(-1);
			}

			if (args.LastOrDefault() is string filePath)
			{
				if (File.Exists(filePath))
				{
					Log.LogInfo($"arg.filePath: {filePath}");

					if (await DocumentOpenHelper.TryOpenAsDocument(filePath))
						Application.Current?.MainWindow?.Focus();
				}
				else
					ErrorExit(Resources.ErrorFileByParamNotFound);
			}
		}
	}
}
