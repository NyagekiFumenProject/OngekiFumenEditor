using Avalonia.Input;
using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditorSettings.Commands;

[RegisterSingleton<CommandDefinitionBase>]
public class ViewFumenVisualEditorSettingsCommandDefinition : CommandDefinition
{
    public const string CommandName = "OngekiFumenEditor.Avalonia.Modules.FumenVisualEditorSettings.Commands.ViewFumenVisualEditorSettingsCommandDefinition";

    [RegisterStaticObject<CommandKeyboardShortcut>]
    public static CommandKeyboardShortcut KeyGesture = new CommandKeyboardShortcut<ViewFumenVisualEditorSettingsCommandDefinition>(
        new KeyGesture(Key.E, KeyModifiers.Alt | KeyModifiers.Shift));

    public override string Name => CommandName;

    public override LocalizedString Text { get; } = Lang.B.FumenVisualEditorSettings.ToLocalizedString();

    public override LocalizedString ToolTip => Text;
}
