using System.Diagnostics;

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
	}
}
