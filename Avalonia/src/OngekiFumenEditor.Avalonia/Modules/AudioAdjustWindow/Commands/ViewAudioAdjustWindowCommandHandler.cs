using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Modules.Shell;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Avalonia;

namespace OngekiFumenEditor.Avalonia.Modules.AudioAdjustWindow.Commands;

[RegisterSingleton<ICommandHandler>]
public partial class ViewAudioAdjustWindowCommandHandler : CommandHandlerBase<ViewAudioAdjustWindowCommandDefinition>
{
    public override Task Run(Command command)
    {
        var shell = IoC.Get<IShell>();
        var tool = IoC.Get<IAudioAdjustWindow>();
        if (tool is Gekimini.Avalonia.Framework.IToolViewModel tvm)
            shell.ShowTool(tvm);
        return Task.CompletedTask;
    }
}
