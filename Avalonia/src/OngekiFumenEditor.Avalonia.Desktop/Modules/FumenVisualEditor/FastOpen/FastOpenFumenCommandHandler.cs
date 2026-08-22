using Gekimini.Avalonia.Framework.Commands;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Desktop.Modules.FumenVisualEditor.FastOpen;

[RegisterSingleton<ICommandHandler>]
public class FastOpenFumenCommandHandler : CommandHandlerBase<FastOpenFumenCommandDefinition>
{
    private readonly DesktopFastOpenService fastOpenService;

    public FastOpenFumenCommandHandler(DesktopFastOpenService fastOpenService)
    {
        this.fastOpenService = fastOpenService;
    }

    public override async Task Run(Command command)
    {
        if (command?.Tag is string filePath && !string.IsNullOrWhiteSpace(filePath))
            await fastOpenService.TryOpenAsync(filePath);
        else
            await fastOpenService.OpenAsync();
    }
}
