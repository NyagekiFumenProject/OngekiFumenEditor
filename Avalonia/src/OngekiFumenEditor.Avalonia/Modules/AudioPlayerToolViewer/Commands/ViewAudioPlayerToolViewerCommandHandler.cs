using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Modules.Shell;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Modules.AudioPlayerToolViewer.Commands;

[RegisterSingleton<ICommandHandler>]
public partial class ViewAudioPlayerToolViewerCommandHandler : CommandHandlerBase<ViewAudioPlayerToolViewerCommandDefinition>
{
    private IShell Shell => OngekiFumenEditor.Avalonia.Avalonia.IoC.Get<IShell>();

    public override Task Run(Command command)
    {
        var tool = IoC.Get<IAudioPlayerToolViewer>();
        if (tool is Gekimini.Avalonia.Framework.IToolViewModel tvm)
            Shell.ShowTool(tvm);
        return Task.CompletedTask;
    }
}