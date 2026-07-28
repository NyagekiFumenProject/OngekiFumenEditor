using Avalonia.Input;
using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;

namespace OngekiFumenEditor.Avalonia.Modules.FumenBulletPalleteListViewer.Commands;

[RegisterSingleton<CommandDefinitionBase>]
public class ViewFumenBulletPalleteListViewerCommandDefinition : CommandDefinition
{
    public const string CommandName = "View.FumenBulletPalleteListViewer";

    [RegisterStaticObject<CommandKeyboardShortcut>]
    public static CommandKeyboardShortcut KeyGesture = new CommandKeyboardShortcut<ViewFumenBulletPalleteListViewerCommandDefinition>(
        new KeyGesture(Key.B, KeyModifiers.Alt | KeyModifiers.Shift));

    public override string Name => CommandName;
    public override LocalizedString Text { get; } = Lang.FumenBulletPalleteListViewer.ToLocalizedStringByRawText();
    public override LocalizedString ToolTip => Text;
}

