using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Utils;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace OngekiFumenEditor.Avalonia.Kernel.RecentFiles.DefaultImp;

[RegisterSingleton<IEditorRecentFilesManager>]
internal class DefaultEditorRecentFilesManager : IEditorRecentFilesManager
{
    private readonly ObservableCollection<RecentRecordInfo> recentRecordInfos = [];
    public IEnumerable<RecentRecordInfo> RecentRecordInfos => recentRecordInfos;

    private const int MaxRecordCount = 10;
    private readonly object locker = new();

    // Temporary persistence until Settings module is migrated.
    private static readonly string RecentFileStorePath =
        Path.Combine(TempFileHelper.GetTempFolderPath("ongeki_editor", random: false), "recent_opened.json");

    public DefaultEditorRecentFilesManager()
    {
        LoadRecordOpenedList();
    }

    private void SaveRecordOpenedList()
    {
        lock (locker)
        {
            var list = recentRecordInfos.Take(MaxRecordCount).ToList();
            var json = JsonSerializer.Serialize(list);
            Directory.CreateDirectory(Path.GetDirectoryName(RecentFileStorePath));
            File.WriteAllText(RecentFileStorePath, json);
        }
    }

    private void LoadRecordOpenedList()
    {
        lock (locker)
        {
            recentRecordInfos.Clear();

            if (!File.Exists(RecentFileStorePath))
                return;

            var json = File.ReadAllText(RecentFileStorePath);
            if (string.IsNullOrWhiteSpace(json))
                return;

            var list = JsonSerializer.Deserialize<List<RecentRecordInfo>>(json);
            if (list is not null)
                recentRecordInfos.AddRange(list.Take(MaxRecordCount));
        }
    }

    public void PostRecord(RecentRecordInfo info)
    {
        var fileName = Path.GetFullPath(info.FileName);
        info = info with { FileName = fileName, LastAccessTime = DateTime.Now };

        if (info.FileName == recentRecordInfos.FirstOrDefault()?.FileName)
            return;

        recentRecordInfos.RemoveRange(recentRecordInfos.Where(x => x.FileName == info.FileName).ToArray());
        recentRecordInfos.Insert(0, info);
        SaveRecordOpenedList();
    }

    public void ClearAllRecords()
    {
        recentRecordInfos.Clear();
        SaveRecordOpenedList();
    }
}

