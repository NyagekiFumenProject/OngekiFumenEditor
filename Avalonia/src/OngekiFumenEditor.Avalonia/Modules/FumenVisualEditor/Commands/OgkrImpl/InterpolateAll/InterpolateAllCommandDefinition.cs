using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.OgkrImpl.InterpolateAll;

[RegisterSingleton<CommandDefinitionBase>]
public class InterpolateAllCommandDefinition : CommandDefinition
{
    public const string CommandName = "OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.OgkrImpl.InterpolateAll.InterpolateAllCommandDefinition";

    public override string Name => CommandName;

    public override LocalizedString Text { get; } = "InterpolateAllCommandDefinition".ToLocalizedStringByRawText();

    public override LocalizedString ToolTip => Text;
}