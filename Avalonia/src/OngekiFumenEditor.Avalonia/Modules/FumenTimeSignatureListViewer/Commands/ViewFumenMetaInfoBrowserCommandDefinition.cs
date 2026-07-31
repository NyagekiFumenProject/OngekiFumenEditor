using Avalonia.Input;
using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;

namespace OngekiFumenEditor.Avalonia.Modules.FumenTimeSignatureListViewer.Commands;

[RegisterSingleton<CommandDefinitionBase>]
public class ViewFumenTimeSignatureListViewerCommandDefinition : CommandDefinition
{
    public const string CommandName = "View.FumenTimeSignatureListViewer";

    [RegisterStaticObject<CommandKeyboardShortcut>]
    public static CommandKeyboardShortcut KeyGesture = new CommandKeyboardShortcut<ViewFumenTimeSignatureListViewerCommandDefinition>(
        new KeyGesture(Key.T, KeyModifiers.Alt | KeyModifiers.Shift));

    public override string Name => CommandName;
    public override LocalizedString Text { get; } = Lang.B.FumenTimeSignatureListViewer.ToLocalizedString();
    public override LocalizedString ToolTip => Text;
}
