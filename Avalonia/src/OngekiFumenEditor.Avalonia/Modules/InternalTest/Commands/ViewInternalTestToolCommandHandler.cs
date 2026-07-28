using Gekimini.Avalonia;
using Gekimini.Avalonia.Attributes;
using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Modules.Shell;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Modules.InternalTest.ViewModels.Tools;

namespace OngekiFumenEditor.Avalonia.Modules.InternalTest.Commands;

[RegisterSingleton<ICommandHandler>]
public partial class ViewInternalTestToolCommandHandler : CommandHandlerBase<ViewInternalTestToolCommandDefinition>
{
    private IShell Shell => OngekiFumenEditor.Avalonia.Avalonia.IoC.Get<IShell>();
    private IServiceProvider ServiceProvider => OngekiFumenEditor.Avalonia.Avalonia.IoC.Get<IServiceProvider>();

    public override Task Run(Command command)
    {
        Shell.ShowTool(ServiceProvider.Resolve<InternalTestToolViewModel>());
        return Task.CompletedTask;
    }
}
