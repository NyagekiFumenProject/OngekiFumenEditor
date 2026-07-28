using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditorSettings.Commands;

[RegisterSingleton<CommandDefinitionBase>]
public class ViewFumenVisualEditorSettingsCommandDefinition : CommandDefinition
{
    public const string CommandName = "OngekiFumenEditor.Avalonia.Modules.FumenVisualEditorSettings.Commands.ViewFumenVisualEditorSettingsCommandDefinition";

    public override string Name => CommandName;

    public override LocalizedString Text { get; } = "ViewFumenVisualEditorSettingsCommandDefinition".ToLocalizedStringByRawText();

    public override LocalizedString ToolTip => Text;
}