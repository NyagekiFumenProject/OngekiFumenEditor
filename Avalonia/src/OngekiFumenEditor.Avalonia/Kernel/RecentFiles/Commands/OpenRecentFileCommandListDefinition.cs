using Gekimini.Avalonia.Framework.Commands;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Kernel.RecentFiles.Commands;

[RegisterSingleton<CommandDefinitionBase>]
public class OpenRecentFileCommandListDefinition : CommandListDefinition
{
    public const string CommandName = "File.OpenRecentFileList";

    public override string Name => CommandName;
}
