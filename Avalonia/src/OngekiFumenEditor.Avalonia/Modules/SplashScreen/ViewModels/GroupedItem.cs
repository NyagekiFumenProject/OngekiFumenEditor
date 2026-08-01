using CommunityToolkit.Mvvm.Input;
using Gekimini.Avalonia.Framework.RecentFiles;

namespace OngekiFumenEditor.Avalonia.Modules.SplashScreen.ViewModels;

public sealed record GroupedItem(string Name, IReadOnlyList<RecentFileItemViewModel> Recents);

public sealed class RecentFileItemViewModel
{
    public RecentFileItemViewModel(RecentRecordInfo record, Func<RecentRecordInfo, Task> openRecentAsync)
    {
        Record = record;
        OpenCommand = new AsyncRelayCommand(() => openRecentAsync(record));
    }

    public RecentRecordInfo Record { get; }
    public string Name => Record.Name;
    public string LocationDescription => Record.LocationDescription;
    public string LastAccessTimeText => Record.LastAccessTime?.ToString("g") ?? string.Empty;
    public IAsyncRelayCommand OpenCommand { get; }
}
