using System.Windows.Input;
using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.RecentFiles;
using Gekimini.Avalonia.Framework.RecentFiles.Commands;

namespace OngekiFumenEditor.Avalonia.Modules.SplashScreen.ViewModels;

public sealed record GroupedItem(string Name, IReadOnlyList<RecentFileItemViewModel> Recents);

public sealed class RecentFileItemViewModel
{
    public RecentFileItemViewModel(RecentRecordInfo record, ICommandService commandService)
    {
        Record = record;
        OpenCommand = commandService.GetTargetableCommand(new Command(new OpenRecentFileCommandListDefinition())
        {
            Tag = record
        });
    }

    public RecentRecordInfo Record { get; }
    public string Name => Record.Name;
    public string LocationDescription => Record.LocationDescription;
    public string LastAccessTimeText => Record.LastAccessTime?.ToString("g") ?? string.Empty;
    public ICommand OpenCommand { get; }
}
