using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.FastPlayPause;

[RegisterSingleton<CommandDefinitionBase>]
public class FastPlayPauseCommandDefinition : CommandDefinition
{
    public const string CommandName = "OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.FastPlayPause.FastPlayPauseCommandDefinition";

    public override string Name => CommandName;

    public override LocalizedString Text { get; } = Lang.B.FastPlayPause.ToLocalizedString();

    public override LocalizedString ToolTip => Text;
}
