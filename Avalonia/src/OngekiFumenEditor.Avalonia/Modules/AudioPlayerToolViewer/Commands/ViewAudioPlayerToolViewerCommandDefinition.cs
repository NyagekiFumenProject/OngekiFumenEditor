using Avalonia.Input;
using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;

namespace OngekiFumenEditor.Avalonia.Modules.AudioPlayerToolViewer.Commands;

[RegisterSingleton<CommandDefinitionBase>]
public class ViewAudioPlayerToolViewerCommandDefinition : CommandDefinition
{
    public const string CommandName = "View.AudioPlayerToolViewer";

    [RegisterStaticObject<CommandKeyboardShortcut>]
    public static CommandKeyboardShortcut KeyGesture = new CommandKeyboardShortcut<ViewAudioPlayerToolViewerCommandDefinition>(
        new KeyGesture(Key.A, KeyModifiers.Alt | KeyModifiers.Shift));

    public override string Name => CommandName;
    public override LocalizedString Text { get; } = Lang.B.AudioPlayerToolViewer.ToLocalizedString();
    public override LocalizedString ToolTip => Text;
}
