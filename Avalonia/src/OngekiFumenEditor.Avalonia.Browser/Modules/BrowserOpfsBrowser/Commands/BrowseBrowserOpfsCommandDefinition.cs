#nullable enable

using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Browser.Modules.BrowserOpfsBrowser.Assets.Languages;

namespace OngekiFumenEditor.Avalonia.Browser.Modules.BrowserOpfsBrowser.Commands;

[RegisterSingleton<CommandDefinitionBase>]
public sealed class BrowseBrowserOpfsCommandDefinition : CommandDefinition
{
    public const string CommandName = "File.BrowseBrowserOpfs";

    public override string Name => CommandName;
    public override LocalizedString Text { get; } = BrowserOpfsLang.B.BrowserOpfsMenu.ToLocalizedString();
    public override LocalizedString ToolTip => Text;
}
