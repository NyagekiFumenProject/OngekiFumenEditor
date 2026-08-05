using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;

namespace OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.Commands;

[RegisterSingleton<CommandDefinitionBase>]
public class ViewFumenObjectPropertyBrowserCommandDefinition : CommandDefinition
{
    public const string CommandName = "OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.Commands.ViewFumenObjectPropertyBrowserCommandDefinition";

    public override string Name => CommandName;

    public override LocalizedString Text { get; } = Lang.B.FumenObjectPropertyBrowser.ToLocalizedString();

    public override LocalizedString ToolTip => Text;
}
