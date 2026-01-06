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
    [GetServiceLazy]
    private partial IShell Shell { get; }

    [GetServiceLazy]
    private partial IServiceProvider ServiceProvider { get; }

    public override Task Run(Command command)
    {
        Shell.ShowTool(ServiceProvider.Resolve<InternalTestToolViewModel>());
        return Task.CompletedTask;
    }
}