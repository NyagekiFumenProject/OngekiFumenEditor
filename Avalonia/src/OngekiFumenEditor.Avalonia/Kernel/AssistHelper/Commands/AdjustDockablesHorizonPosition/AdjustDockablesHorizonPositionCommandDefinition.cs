using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;

namespace OngekiFumenEditor.Avalonia.Kernel.AssistHelper.Commands.AdjustDockablesHorizonPosition;

[RegisterSingleton<CommandDefinitionBase>]
public class AdjustDockablesHorizonPositionCommandDefinition : CommandDefinition
{
    public const string CommandName = "Assist.AdjustDockablesHorizonPosition";

    public override string Name => CommandName;

    public override LocalizedString Text { get; } = Lang.CommandAdjustDockablesHorizonPosition.ToLocalizedStringByRawText();

    public override LocalizedString ToolTip => Text;
}

