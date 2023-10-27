using System.Collections.Generic;

namespace OngekiFumenEditor.Kernel.RecentFiles
{
	public partial interface IEditorRecentFilesManager
	{
		IEnumerable<RecentRecordInfo> RecentRecordInfos { get; }
		void PostRecord(RecentRecordInfo info);
		void ClearAllRecords();
	}
}
