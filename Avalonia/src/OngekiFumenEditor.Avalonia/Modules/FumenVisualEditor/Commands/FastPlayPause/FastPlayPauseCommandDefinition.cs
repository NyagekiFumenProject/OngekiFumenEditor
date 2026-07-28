using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.FastPlayPause;

[RegisterSingleton<CommandDefinitionBase>]
public class FastPlayPauseCommandDefinition : CommandDefinition
{
    public const string CommandName = "OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.FastPlayPause.FastPlayPauseCommandDefinition";

    public override string Name => CommandName;

    public override LocalizedString Text { get; } = "FastPlayPauseCommandDefinition".ToLocalizedStringByRawText();

    public override LocalizedString ToolTip => Text;
}