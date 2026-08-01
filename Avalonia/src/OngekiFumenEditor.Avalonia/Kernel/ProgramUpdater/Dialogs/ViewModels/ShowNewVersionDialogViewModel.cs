using CommunityToolkit.Mvvm.ComponentModel;
using Gekimini.Avalonia.Modules.Window.ViewModels;
using System.Reflection;

namespace OngekiFumenEditor.Avalonia.Kernel.ProgramUpdater.Dialogs.ViewModels;

public partial class ShowNewVersionDialogViewModel : WindowViewModelBase
{
    private readonly IProgramUpdater programUpdater;

    public VersionInfo NewVersionInfo => programUpdater.RemoteVersionInfo;

    public string CurrentVersion =>
        ((Assembly.GetEntryAssembly() ?? typeof(ShowNewVersionDialogViewModel).Assembly).GetName().Version ?? new Version(0, 0, 0, 0))
        .ToString(4);

    [ObservableProperty]
    private bool isReady;

    public ShowNewVersionDialogViewModel()
    {
        programUpdater = IoC.Get<IProgramUpdater>();
    }

    public async Task StartUpdate()
    {
        await programUpdater.StartUpdate();
    }
}
