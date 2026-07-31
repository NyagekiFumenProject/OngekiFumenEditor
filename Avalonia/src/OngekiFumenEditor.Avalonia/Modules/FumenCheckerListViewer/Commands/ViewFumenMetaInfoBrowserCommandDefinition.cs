using Avalonia.Input;
using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;

namespace OngekiFumenEditor.Avalonia.Modules.FumenCheckerListViewer.Commands;

[RegisterSingleton<CommandDefinitionBase>]
public class ViewFumenCheckerListViewerCommandDefinition : CommandDefinition
{
    public const string CommandName = "View.FumenCheckerListViewer";

    [RegisterStaticObject<CommandKeyboardShortcut>]
    public static CommandKeyboardShortcut KeyGesture = new CommandKeyboardShortcut<ViewFumenCheckerListViewerCommandDefinition>(
        new KeyGesture(Key.C, KeyModifiers.Alt | KeyModifiers.Shift));

    public override string Name => CommandName;
    public override LocalizedString Text { get; } = Lang.B.FumenCheckerListViewer.ToLocalizedString();
    public override LocalizedString ToolTip => Text;
}
