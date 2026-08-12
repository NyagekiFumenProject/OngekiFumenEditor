#nullable enable

using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Dialogs;
using Gekimini.Avalonia.Platforms.Services.Window;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Browser.Modules.BrowserOpfsBrowser.ViewModels;

namespace OngekiFumenEditor.Avalonia.Browser.Modules.BrowserOpfsBrowser.Commands;

[RegisterSingleton<ICommandHandler>]
public sealed class BrowseBrowserOpfsCommandHandler
    : CommandHandlerBase<BrowseBrowserOpfsCommandDefinition>
{
    private readonly IBrowserOpfsService service;
    private readonly IWindowManager windowManager;
    private readonly IDialogManager dialogManager;
    private BrowserOpfsBrowserViewModel? viewModel;

    public BrowseBrowserOpfsCommandHandler(
        IBrowserOpfsService service,
        IWindowManager windowManager,
        IDialogManager dialogManager)
    {
        this.service = service;
        this.windowManager = windowManager;
        this.dialogManager = dialogManager;
    }

    public override Task Update(Command command)
    {
        command.Enabled = service.IsAvailable;
        return Task.CompletedTask;
    }

    public override Task Run(Command command)
    {
        if (!service.IsAvailable)
            return Task.CompletedTask;

        return Dispatcher.UIThread.InvokeAsync(ShowOrActivateAsync);
    }

    private async Task ShowOrActivateAsync()
    {
        Dispatcher.UIThread.VerifyAccess();
        viewModel ??= new BrowserOpfsBrowserViewModel(service, dialogManager);

        if (windowManager.FindExistingWindow(viewModel) is { } existingWindow)
        {
            if (existingWindow.WindowState == WindowState.Minimized)
                existingWindow.WindowState = WindowState.Normal;
            existingWindow.Activate();
            await viewModel.RefreshNowAsync();
            return;
        }

        await windowManager.ShowWindowAsync(viewModel);
    }
}
