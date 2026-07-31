using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Modules.TGridCalculatorToolViewer.Commands;

[RegisterSingleton<CommandDefinitionBase>]
public class ViewTGridCalculatorToolViewerCommandDefinition : CommandDefinition
{
    public const string CommandName = "OngekiFumenEditor.Avalonia.Modules.TGridCalculatorToolViewer.Commands.ViewTGridCalculatorToolViewerCommandDefinition";

    public override string Name => CommandName;

    public override LocalizedString Text { get; } = "ViewTGridCalculatorToolViewerCommandDefinition".ToLocalizedStringByRawText();

    public override LocalizedString ToolTip => Text;
}
