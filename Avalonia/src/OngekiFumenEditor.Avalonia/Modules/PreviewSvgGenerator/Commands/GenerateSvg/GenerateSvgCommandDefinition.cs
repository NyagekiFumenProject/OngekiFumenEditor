using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;

namespace OngekiFumenEditor.Avalonia.Modules.PreviewSvgGenerator.Commands.GenerateSvg;

[RegisterSingleton<CommandDefinitionBase>]
public class GenerateSvgCommandDefinition : CommandDefinition
{
    public override string Name => "Toolbar.GenerateSvg";

    public override LocalizedString Text { get; } = Lang.B.GenerateSvg.ToLocalizedString();

    public override LocalizedString ToolTip => Text;
}

