using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.OgkrImpl.FastOpenFumen;

[RegisterSingleton<CommandDefinitionBase>]
public class FastOpenFumenCommandDefinition : CommandDefinition
{
    public const string CommandName = "OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.OgkrImpl.FastOpenFumen.FastOpenFumenCommandDefinition";

    public override string Name => CommandName;

    public override LocalizedString Text { get; } = "FastOpenFumenCommandDefinition".ToLocalizedStringByRawText();

    public override LocalizedString ToolTip => Text;
}