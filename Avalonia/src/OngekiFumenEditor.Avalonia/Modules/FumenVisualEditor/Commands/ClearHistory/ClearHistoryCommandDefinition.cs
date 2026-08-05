using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.ClearHistory;

[RegisterSingleton<CommandDefinitionBase>]
public class ClearHistoryCommandDefinition : CommandDefinition
{
    public const string CommandName = "OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.ClearHistory.ClearHistoryCommandDefinition";

    public override string Name => CommandName;

    public override LocalizedString Text { get; } = Lang.B.ClearHistory.ToLocalizedString();

    public override LocalizedString ToolTip => Text;
}
