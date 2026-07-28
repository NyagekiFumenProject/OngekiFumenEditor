using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.Commands;

[RegisterSingleton<CommandDefinitionBase>]
public class ViewFumenObjectPropertyBrowserCommandDefinition : CommandDefinition
{
    public const string CommandName = "OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.Commands.ViewFumenObjectPropertyBrowserCommandDefinition";

    public override string Name => CommandName;

    public override LocalizedString Text { get; } = "ViewFumenObjectPropertyBrowserCommandDefinition".ToLocalizedStringByRawText();

    public override LocalizedString ToolTip => Text;
}