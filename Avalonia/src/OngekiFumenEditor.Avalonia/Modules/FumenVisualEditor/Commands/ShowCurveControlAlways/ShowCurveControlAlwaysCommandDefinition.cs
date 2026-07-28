using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.ShowCurveControlAlways;

[RegisterSingleton<CommandDefinitionBase>]
public class ShowCurveControlAlwaysCommandDefinition : CommandDefinition
{
    public const string CommandName = "OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.ShowCurveControlAlways.ShowCurveControlAlwaysCommandDefinition";

    public override string Name => CommandName;

    public override LocalizedString Text { get; } = "ShowCurveControlAlwaysCommandDefinition".ToLocalizedStringByRawText();

    public override LocalizedString ToolTip => Text;
}