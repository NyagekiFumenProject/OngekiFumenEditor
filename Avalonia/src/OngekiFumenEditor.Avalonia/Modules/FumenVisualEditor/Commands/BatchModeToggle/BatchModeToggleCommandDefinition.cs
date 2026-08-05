using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.BatchModeToggle;

[RegisterSingleton<CommandDefinitionBase>]
public class BatchModeToggleCommandDefinition : CommandDefinition
{
    public const string CommandName = "OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.BatchModeToggle.BatchModeToggleCommandDefinition";

    public override string Name => CommandName;

    public override LocalizedString Text { get; } = Lang.B.BatchModeToggle.ToLocalizedString();

    public override LocalizedString ToolTip { get; } = Lang.B.BatchModeToggleTipText.ToLocalizedString();
}
