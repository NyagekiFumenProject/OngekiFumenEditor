namespace OngekiFumenEditor.Avalonia.Kernel.RecentFiles;

public partial interface IEditorRecentFilesManager
{
    IEnumerable<RecentRecordInfo> RecentRecordInfos { get; }
    void PostRecord(RecentRecordInfo info);
    void ClearAllRecords();
}

