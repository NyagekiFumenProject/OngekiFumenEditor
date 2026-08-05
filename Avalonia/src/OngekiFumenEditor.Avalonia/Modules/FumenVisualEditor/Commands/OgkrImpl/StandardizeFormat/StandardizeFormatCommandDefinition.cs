using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.OgkrImpl.StandardizeFormat;

[RegisterSingleton<CommandDefinitionBase>]
public class StandardizeFormatCommandDefinition : CommandDefinition
{
    public const string CommandName = "OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.OgkrImpl.StandardizeFormat.StandardizeFormatCommandDefinition";

    public override string Name => CommandName;

    public override LocalizedString Text { get; } = Lang.B.StandardizeFormat.ToLocalizedString();

    public override LocalizedString ToolTip => Text;
}
