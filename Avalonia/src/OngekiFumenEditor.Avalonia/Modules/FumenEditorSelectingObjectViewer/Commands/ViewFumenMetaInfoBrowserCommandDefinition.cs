using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;

namespace OngekiFumenEditor.Avalonia.Modules.FumenEditorSelectingObjectViewer.Commands;

[RegisterSingleton<CommandDefinitionBase>]
public class ViewFumenEditorSelectingObjectViewerCommandDefinition : CommandDefinition
{
    public const string CommandName = "OngekiFumenEditor.Avalonia.Modules.FumenEditorSelectingObjectViewer.Commands.ViewFumenEditorSelectingObjectViewerCommandDefinition";

    public override string Name => CommandName;

    public override LocalizedString Text { get; } = Lang.B.FumenEditorSelectingObjectViewer.ToLocalizedString();

    public override LocalizedString ToolTip => Text;
}
