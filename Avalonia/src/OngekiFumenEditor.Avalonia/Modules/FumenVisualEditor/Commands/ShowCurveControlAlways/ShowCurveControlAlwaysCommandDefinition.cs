using Avalonia.Input;
using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.ShowCurveControlAlways;

[RegisterSingleton<CommandDefinitionBase>]
public class ShowCurveControlAlwaysCommandDefinition : CommandDefinition
{
    public const string CommandName = "OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.ShowCurveControlAlways.ShowCurveControlAlwaysCommandDefinition";

    [RegisterStaticObject<CommandKeyboardShortcut>]
    public static CommandKeyboardShortcut KeyGesture = new CommandKeyboardShortcut<ShowCurveControlAlwaysCommandDefinition>(
        new KeyGesture(Key.S, KeyModifiers.Alt));

    public override string Name => CommandName;

    public override LocalizedString Text { get; } = Lang.B.CommandShowCurveControlAlways.ToLocalizedString();

    public override LocalizedString ToolTip { get; } = Lang.B.CommandShowCurveControlAlwaysTipText.ToLocalizedString();

    public override Uri IconSource => ResourceUtils.GetResourceUri("Icons/ease.png");
}
