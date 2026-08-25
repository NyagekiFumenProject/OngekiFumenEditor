using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Languages;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;

namespace OngekiFumenEditor.Avalonia.Modules.OgkiFumenListBrowser.Commands;

[RegisterSingleton<CommandDefinitionBase>]
public sealed class ViewOgkiFumenListBrowserCommandDefinition : CommandDefinition
{
    public const string CommandName = "View.OgkiFumenListBrowser";

    public override string Name => CommandName;

    public override LocalizedString Text { get; } = Lang.B.OgkiFumenListBrowser.ToLocalizedString();

    public override LocalizedString ToolTip => Text;
}
