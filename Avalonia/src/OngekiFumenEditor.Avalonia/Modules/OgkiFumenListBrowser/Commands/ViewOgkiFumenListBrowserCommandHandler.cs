using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Modules.Window.ViewModels;
using Gekimini.Avalonia.Platforms.Services.Window;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia;

namespace OngekiFumenEditor.Avalonia.Modules.OgkiFumenListBrowser.Commands;

[RegisterSingleton<ICommandHandler>]
public sealed class ViewOgkiFumenListBrowserCommandHandler : CommandHandlerBase<ViewOgkiFumenListBrowserCommandDefinition>
{
    public override Task Run(Command command)
    {
        if (IoC.Get<IOgkiFumenListBrowser>().WindowViewModel is WindowViewModelBase window)
            return IoC.Get<IWindowManager>().ShowWindowAsync(window);
        return Task.CompletedTask;
    }
}
