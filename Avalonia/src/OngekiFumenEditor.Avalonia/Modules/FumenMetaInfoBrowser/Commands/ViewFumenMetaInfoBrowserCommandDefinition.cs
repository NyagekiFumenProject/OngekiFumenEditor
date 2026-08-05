using Avalonia.Input;
using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;

namespace OngekiFumenEditor.Avalonia.Modules.FumenMetaInfoBrowser.Commands;

[RegisterSingleton<CommandDefinitionBase>]
public class ViewFumenMetaInfoBrowserCommandDefinition : CommandDefinition
{
    public const string CommandName = "View.FumenMetaInfoBrowser";

    [RegisterStaticObject<CommandKeyboardShortcut>]
    public static CommandKeyboardShortcut KeyGesture =
        new CommandKeyboardShortcut<ViewFumenMetaInfoBrowserCommandDefinition>(
            new KeyGesture(Key.M, KeyModifiers.Alt | KeyModifiers.Shift));

    public override string Name => CommandName;

    public override LocalizedString Text { get; } = Lang.B.FumenMetaInfoBrowser.ToLocalizedString();

    public override LocalizedString ToolTip => Text;
}

