using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.BatchModeToggle;

[RegisterSingleton<CommandDefinitionBase>]
public class BatchModeToggleCommandDefinition : CommandDefinition
{
    public const string CommandName = "OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.BatchModeToggle.BatchModeToggleCommandDefinition";

    public override string Name => CommandName;

    public override LocalizedString Text { get; } = "BatchModeToggleCommandDefinition".ToLocalizedStringByRawText();

    public override LocalizedString ToolTip => Text;
}