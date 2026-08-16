#nullable enable

using CommunityToolkit.Mvvm.ComponentModel;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;
using System;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;

// Runtime companion of EditorProjectDataModel: it owns the parsed fumen, the file-access
// capabilities and other session-only state, so the data model stays a pure persistable
// data class. Disposing the context releases the fumen's disposable prefabs and all
// file handles held by the file-access context.
public sealed partial class EditorContext : ObservableObject, IDisposable
{
    private EditorFileAccessContext? fileAccessContext;
    private bool isDisposed;

    public required EditorProjectDataModel ProjectData { get; init; }

    public string ProjectFileLocator { get; set; } = string.Empty;

    public Guid RecentRecordId { get; set; }

    // 与旧模型一致：默认持有一个空谱面，未加载谱面时也能安全访问。
    [ObservableProperty]
    public partial OngekiFumen Fumen { get; set; } = new();

    public EditorFileAccessContext? FileAccessContext
    {
        get => fileAccessContext;
        set
        {
            if (ReferenceEquals(fileAccessContext, value))
                return;

            fileAccessContext?.Dispose();
            fileAccessContext = value;
        }
    }

    public ISimpleFile? ProjectFile => FileAccessContext?.ProjectFile;

    public ISimpleFile? FumenFile => FileAccessContext?.FumenFile;

    public ISimpleFile? AudioFile => FileAccessContext?.AudioFile;

    public ISimpleFile? AudioAwbFile => FileAccessContext?.AudioAwbFile;

    public ISimpleDirectory? ProjectRoot => FileAccessContext?.ProjectDirectory;

    public double BaseBPM
    {
        get => Fumen.MetaInfo.BpmDefinition.First;
        set
        {
            if (Fumen is not null)
            {
                Fumen.MetaInfo.BpmDefinition.First = value;
                Fumen.BpmList.FirstBpm = value;
            }
            OnPropertyChanged(nameof(BaseBPM));
        }
    }

    partial void OnFumenChanged(OngekiFumen oldValue, OngekiFumen newValue) =>
        OnPropertyChanged(nameof(BaseBPM));

    public void Dispose()
    {
        if (isDisposed)
            return;

        isDisposed = true;
        if (Fumen is { } fumen)
        {
            foreach (var svg in fumen.SvgPrefabs.ToArray())
                svg.Dispose();
        }

        FileAccessContext = null;
    }
}
