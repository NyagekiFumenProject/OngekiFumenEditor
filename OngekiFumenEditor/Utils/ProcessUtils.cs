using System.Diagnostics;
using System.IO;

namespace OngekiFumenEditor.Utils
{
	public static class ProcessUtils
	{
		public static void OpenUrl(string url)
		{
			Log.LogDebug($"user request open url by default : {url}");
			Process.Start(new ProcessStartInfo(url)
			{
				UseShellExecute = true
			});
		}

		public static void OpenPath(string path)
		{
			Log.LogDebug($"user request open url by default : {path}");
			Process.Start(new ProcessStartInfo(path)
			{
				UseShellExecute = true
			});
		}

		public static void OpenExplorerToBrowser(string path)
		{
			Log.LogDebug($"user request open explorer and select a file/folder: {path}");
			var startInfo = new ProcessStartInfo("explorer.exe")
			{
				UseShellExecute = true
			};

			if (File.Exists(path))
				startInfo.Arguments = $"/select,\"{path}\"";
			else if (Directory.Exists(path))
				startInfo.ArgumentList.Add(path);
			else
				return;

			Process.Start(startInfo);
		}
	}
}
