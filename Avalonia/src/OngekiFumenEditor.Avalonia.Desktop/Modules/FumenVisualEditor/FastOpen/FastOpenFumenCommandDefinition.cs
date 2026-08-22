using Avalonia.Input;
using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Desktop.Modules.FumenVisualEditor.FastOpen.Assets.Languages;

namespace OngekiFumenEditor.Avalonia.Desktop.Modules.FumenVisualEditor.FastOpen;

[RegisterSingleton<CommandDefinitionBase>]
public class FastOpenFumenCommandDefinition : CommandDefinition
{
    public const string CommandName = "OngekiFumenEditor.Avalonia.Desktop.Modules.FumenVisualEditor.FastOpen.FastOpenFumenCommandDefinition";

    [RegisterStaticObject<CommandKeyboardShortcut>]
    public static CommandKeyboardShortcut KeyGesture =
        new CommandKeyboardShortcut<FastOpenFumenCommandDefinition>(new KeyGesture(Key.F, KeyModifiers.Control));

    public override string Name => CommandName;

    public override LocalizedString Text { get; } = DesktopLang.B.FastOpenFumen.ToLocalizedString();

    public override LocalizedString ToolTip => Text;
}
