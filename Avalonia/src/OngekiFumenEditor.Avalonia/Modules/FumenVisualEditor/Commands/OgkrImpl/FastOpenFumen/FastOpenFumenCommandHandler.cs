using Gekimini.Avalonia.Framework.Commands;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.OgkrImpl.FastOpenFumen;

[RegisterSingleton<ICommandHandler>]
public class FastOpenFumenCommandHandler : CommandHandlerBase<FastOpenFumenCommandDefinition>
{
    public override async Task Run(Command command)
    {
        if (command.Tag is string filePath && File.Exists(filePath))
        {
            _ = await DocumentOpenHelper.TryOpenAsDocument(filePath);
            return;
        }

        Log.LogWarning("FastOpenFumen requires command.Tag as existing file path.");
    }
}
