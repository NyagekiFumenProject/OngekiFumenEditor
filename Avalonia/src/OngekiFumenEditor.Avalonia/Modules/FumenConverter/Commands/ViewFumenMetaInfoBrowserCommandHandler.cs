using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Modules.Shell;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Avalonia;

namespace OngekiFumenEditor.Avalonia.Modules.FumenConverter.Commands;

[RegisterSingleton<ICommandHandler>]
public partial class ViewFumenMetaInfoBrowserCommandHandler : CommandHandlerBase<ViewFumenConverterCommandDefinition>
{
    private IShell Shell => OngekiFumenEditor.Avalonia.Avalonia.IoC.Get<IShell>();

    public override Task Run(Command command)
    {
        var tool = IoC.Get<IFumenConverterWindow>();
        if (tool is Gekimini.Avalonia.Framework.IToolViewModel tvm)
            Shell.ShowTool(tvm);
        return Task.CompletedTask;
    }
}