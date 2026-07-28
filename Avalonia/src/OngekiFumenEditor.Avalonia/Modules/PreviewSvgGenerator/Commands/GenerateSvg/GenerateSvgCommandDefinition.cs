using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Modules.PreviewSvgGenerator.Commands.GenerateSvg;

[RegisterSingleton<CommandDefinitionBase>]
public class GenerateSvgCommandDefinition : CommandDefinition
{
    public override string Name => "Toolbar.GenerateSvg";

    public override LocalizedString Text { get; } = "Generate SVG".ToLocalizedStringByRawText();

    public override LocalizedString ToolTip { get; } = "Generate SVG".ToLocalizedStringByRawText();
}

