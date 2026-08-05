using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.OgkrImpl.InterpolateAll;

[RegisterSingleton<CommandDefinitionBase>]
public class InterpolateAllCommandDefinition : CommandDefinition
{
    public const string CommandName = "OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.OgkrImpl.InterpolateAll.InterpolateAllCommandDefinition";

    public override string Name => CommandName;

    public override LocalizedString Text { get; } = Lang.B.CommandInterpolateAll.ToLocalizedString();

    public override LocalizedString ToolTip => Text;
}

[RegisterSingleton<CommandDefinitionBase>]
public class InterpolateAllWithXGridLimitCommandDefinition : CommandDefinition
{
    public const string CommandName = "OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.OgkrImpl.InterpolateAll.InterpolateAllWithXGridLimitCommandDefinition";

    public override string Name => CommandName;

    public override LocalizedString Text { get; } = Lang.B.CommandInterpolateAllWithXGridLimit.ToLocalizedString();

    public override LocalizedString ToolTip => Text;
}
