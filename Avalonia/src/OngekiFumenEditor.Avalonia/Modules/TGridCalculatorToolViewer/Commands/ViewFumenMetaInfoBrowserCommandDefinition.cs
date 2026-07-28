using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Modules.TGridCalculatorToolViewer.Commands;

[RegisterSingleton<CommandDefinitionBase>]
public class ViewFumenMetaInfoBrowserCommandDefinition : CommandDefinition
{
    public const string CommandName = "OngekiFumenEditor.Avalonia.Modules.TGridCalculatorToolViewer.Commands.ViewFumenMetaInfoBrowserCommandDefinition";

    public override string Name => CommandName;

    public override LocalizedString Text { get; } = "ViewFumenMetaInfoBrowserCommandDefinition".ToLocalizedStringByRawText();

    public override LocalizedString ToolTip => Text;
}