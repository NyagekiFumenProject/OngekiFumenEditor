using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.EditorModeSwitch;

[RegisterSingleton<CommandDefinitionBase>]
public class EditorModeSwitchCommandDefinition : CommandDefinition
{
    public const string CommandName = "OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.EditorModeSwitch.EditorModeSwitchCommandDefinition";

    public override string Name => CommandName;

    public override LocalizedString Text { get; } = Lang.B.EditorModeSwitch.ToLocalizedString();

    public override LocalizedString ToolTip => Text;
}
