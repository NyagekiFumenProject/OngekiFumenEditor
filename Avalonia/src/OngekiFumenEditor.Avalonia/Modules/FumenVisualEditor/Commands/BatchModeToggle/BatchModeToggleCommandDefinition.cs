using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.BatchModeToggle;

[RegisterSingleton<CommandDefinitionBase>]
public class BatchModeToggleCommandDefinition : CommandDefinition
{
    public const string CommandName = "OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.BatchModeToggle.BatchModeToggleCommandDefinition";

    public override string Name => CommandName;

    public override LocalizedString Text { get; } = Lang.B.BatchModeToggle.ToLocalizedString();

    public override LocalizedString ToolTip { get; } = Lang.B.BatchModeToggleTipText.ToLocalizedString();

    public override Uri IconSource => ResourceUtils.GetResourceUri("Icons/icons8-paint-brush-16.png");
}
