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

    public override string Name => CommandName;

    public override LocalizedString Text { get; } = Lang.B.TGridCalculatorToolViewer.ToLocalizedString();

    public override LocalizedString ToolTip => Text;
}
