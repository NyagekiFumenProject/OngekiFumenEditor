using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.RecentFiles
{
    public partial interface IEditorRecentFilesManager
    {
        IEnumerable<RecentRecordInfo> RecentRecordInfos { get; }
        void PostRecord(RecentRecordInfo info);
        void ClearAllRecords();
    }
}
