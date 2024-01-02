using OngekiFumenEditor.Kernel.RecentFiles;
using System.Collections.Generic;

namespace OngekiFumenEditor.Modules.SplashScreen.ViewModels
{
	public record GroupedItem(string Name, IEnumerable<RecentRecordInfo> Recents);
}
