using Avalonia.Input;
using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;

namespace OngekiFumenEditor.Avalonia.Modules.FumenEditorRenderControlViewer.Commands;

[RegisterSingleton<CommandDefinitionBase>]
public class FumenEditorRenderControlViewerCommandDefinition : CommandDefinition
{
    public const string CommandName = "View.FumenEditorRenderControlViewer";

    [RegisterStaticObject<CommandKeyboardShortcut>]
    public static CommandKeyboardShortcut KeyGesture = new CommandKeyboardShortcut<FumenEditorRenderControlViewerCommandDefinition>(
        new KeyGesture(Key.R, KeyModifiers.Alt | KeyModifiers.Shift));

    public override string Name => CommandName;
    public override LocalizedString Text { get; } = Lang.FumenEditorRenderControlViewer.ToLocalizedStringByRawText();
    public override LocalizedString ToolTip => Text;
}

