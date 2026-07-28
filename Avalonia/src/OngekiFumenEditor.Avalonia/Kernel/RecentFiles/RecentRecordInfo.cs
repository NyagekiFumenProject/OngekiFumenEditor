namespace OngekiFumenEditor.Avalonia.Kernel.RecentFiles;

public record RecentRecordInfo(string FileName, string DisplayName, RecentOpenType Type, DateTime? LastAccessTime = default);

