using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.RecalculateTotalHeight;

[RegisterSingleton<CommandDefinitionBase>]
public class RecalculateTotalHeightCommandDefinition : CommandDefinition
{
    public const string CommandName = "OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.RecalculateTotalHeight.RecalculateTotalHeightCommandDefinition";

    public override string Name => CommandName;

    public override LocalizedString Text { get; } = "RecalculateTotalHeightCommandDefinition".ToLocalizedStringByRawText();

    public override LocalizedString ToolTip => Text;
}