using Avalonia.Input;
using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;

namespace OngekiFumenEditor.Avalonia.Modules.FumenSoflanGroupListViewer.Commands;

[RegisterSingleton<CommandDefinitionBase>]
public class FumenSoflanGroupListViewerCommandDefinition : CommandDefinition
{
    public const string CommandName = "View.FumenSoflanGroupListViewer";

    [RegisterStaticObject<CommandKeyboardShortcut>]
    public static CommandKeyboardShortcut KeyGesture = new CommandKeyboardShortcut<FumenSoflanGroupListViewerCommandDefinition>(
        new KeyGesture(Key.F, KeyModifiers.Alt | KeyModifiers.Shift));

    public override string Name => CommandName;
    public override LocalizedString Text { get; } = Lang.B.SoflanGroupListViewer.ToLocalizedString();
    public override LocalizedString ToolTip => Text;
}
