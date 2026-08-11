#nullable enable

using System;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using OngekiFumenEditor.Avalonia.Browser.Modules.BrowserOpfsBrowser.Assets.Languages;

namespace OngekiFumenEditor.Avalonia.Browser.Modules.BrowserOpfsBrowser.ViewModels;

public sealed class BrowserOpfsEntryViewModel : ObservableObject
{
    private readonly Action selectionChanged;
    private long? size;
    private long? lastModifiedUnixMilliseconds;
    private BrowserOpfsStagingState stagingState;
    private bool isSelected;

    public BrowserOpfsEntryViewModel(
        BrowserOpfsEntrySnapshot snapshot,
        Action selectionChanged)
    {
        this.selectionChanged = selectionChanged;
        Name = snapshot.Name;
        RelativePath = snapshot.RelativePath;
        Kind = snapshot.Kind;
        size = snapshot.Size;
        lastModifiedUnixMilliseconds = snapshot.LastModifiedUnixMilliseconds;
        stagingState = snapshot.StagingState;
    }

    public string Name { get; }
    public string RelativePath { get; }
    public BrowserOpfsEntryKind Kind { get; }
    public bool IsFolder => Kind == BrowserOpfsEntryKind.Folder;
    public bool IsFile => Kind == BrowserOpfsEntryKind.File;
    public long? Size => size;
    public long? LastModifiedUnixMilliseconds => lastModifiedUnixMilliseconds;
    public BrowserOpfsStagingState StagingState => stagingState;
    public bool IsSelectable => StagingState == BrowserOpfsStagingState.None;

    public bool IsSelected
    {
        get => isSelected;
        set
        {
            bool nextValue = value && IsSelectable;
            if (!SetProperty(ref isSelected, nextValue))
                return;
            selectionChanged();
        }
    }

    public string TypeDisplay
    {
        get
        {
            if (IsFolder)
                return BrowserOpfsLang.BrowserOpfsFolder;

            string extension = Path.GetExtension(Name);
            return string.IsNullOrWhiteSpace(extension)
                ? BrowserOpfsLang.BrowserOpfsFile
                : string.Format(
                    CultureInfo.CurrentCulture,
                    BrowserOpfsLang.BrowserOpfsTypedFile,
                    extension.TrimStart('.').ToUpperInvariant());
        }
    }

    public string SizeDisplay => IsFolder || Size is null ? "—" : FormatByteSize(Size.Value);

    public string ModifiedTimeDisplay => LastModifiedUnixMilliseconds is null
        ? "—"
        : DateTimeOffset.FromUnixTimeMilliseconds(LastModifiedUnixMilliseconds.Value)
            .ToLocalTime()
            .ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture);

    public string StagingStatusDisplay => StagingState switch
    {
        BrowserOpfsStagingState.GeneratingDownload => BrowserOpfsLang.BrowserOpfsGeneratingDownload,
        BrowserOpfsStagingState.WaitingAutomaticCleanup => BrowserOpfsLang.BrowserOpfsWaitingAutomaticCleanup,
        _ => string.Empty
    };

    public BrowserOpfsEntrySnapshot ToSnapshot() =>
        new(Name, RelativePath, Kind, Size, LastModifiedUnixMilliseconds, StagingState);

    public void ApplySnapshot(BrowserOpfsEntrySnapshot snapshot)
    {
        if (snapshot.RelativePath != RelativePath || snapshot.Kind != Kind)
            throw new InvalidOperationException("An OPFS row can only be updated from the same path and entry kind.");

        if (SetProperty(ref size, snapshot.Size, nameof(Size)))
            OnPropertyChanged(nameof(SizeDisplay));

        if (SetProperty(
                ref lastModifiedUnixMilliseconds,
                snapshot.LastModifiedUnixMilliseconds,
                nameof(LastModifiedUnixMilliseconds)))
            OnPropertyChanged(nameof(ModifiedTimeDisplay));

        if (!SetProperty(ref stagingState, snapshot.StagingState, nameof(StagingState)))
            return;

        OnPropertyChanged(nameof(IsSelectable));
        OnPropertyChanged(nameof(StagingStatusDisplay));
        if (!IsSelectable)
            IsSelected = false;
    }

    private static string FormatByteSize(long byteCount)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = byteCount;
        int unitIndex = 0;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        string format = unitIndex == 0 ? "0" : value >= 100 ? "0" : value >= 10 ? "0.0" : "0.00";
        return $"{value.ToString(format, CultureInfo.CurrentCulture)} {units[unitIndex]}";
    }
}
