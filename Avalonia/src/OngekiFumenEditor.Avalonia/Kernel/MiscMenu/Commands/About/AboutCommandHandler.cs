using Gekimini.Avalonia.Framework.Commands;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Utils;
using System.Reflection;

namespace OngekiFumenEditor.Avalonia.Kernel.MiscMenu.Commands.About;

[RegisterSingleton<ICommandHandler>]
public class AboutCommandHandler : CommandHandlerBase<AboutCommandDefinition>
{
    public override Task Run(Command command)
    {
        var asm = Assembly.GetEntryAssembly() ?? typeof(AboutCommandHandler).Assembly;
        var ver = asm.GetName().Version?.ToString() ?? "unknown";
        Log.LogInfo($"OngekiFumenEditor.Avalonia version: {ver}");
        return Task.CompletedTask;
    }
}
