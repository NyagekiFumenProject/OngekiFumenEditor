using Avalonia.Input;
using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;

namespace OngekiFumenEditor.Avalonia.Modules.TGridCalculatorToolViewer.Commands;

[RegisterSingleton<CommandDefinitionBase>]
public class ViewTGridCalculatorToolViewerCommandDefinition : CommandDefinition
{
    public const string CommandName = "OngekiFumenEditor.Avalonia.Modules.TGridCalculatorToolViewer.Commands.ViewTGridCalculatorToolViewerCommandDefinition";

    [RegisterStaticObject<CommandKeyboardShortcut>]
    public static CommandKeyboardShortcut KeyGesture = new CommandKeyboardShortcut<ViewTGridCalculatorToolViewerCommandDefinition>(
        new KeyGesture(Key.C, KeyModifiers.Alt | KeyModifiers.Shift));

    public override string Name => CommandName;

    public override LocalizedString Text { get; } = Lang.B.TGridCalculatorToolViewer.ToLocalizedString();

    public override LocalizedString ToolTip => Text;
}
