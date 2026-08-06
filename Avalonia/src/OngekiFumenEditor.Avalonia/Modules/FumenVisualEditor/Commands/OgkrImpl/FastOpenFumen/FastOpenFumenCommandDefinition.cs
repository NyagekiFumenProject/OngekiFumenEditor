using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.OgkrImpl.FastOpenFumen;

// Fast-open is intentionally not registered by the cross-platform Core project.
// [RegisterSingleton<CommandDefinitionBase>]
public class FastOpenFumenCommandDefinition : CommandDefinition
{
    public const string CommandName = "OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.OgkrImpl.FastOpenFumen.FastOpenFumenCommandDefinition";

    public override string Name => CommandName;

    public override LocalizedString Text { get; } = Lang.B.FastOpenFumen.ToLocalizedString();

    public override LocalizedString ToolTip => Text;
}
