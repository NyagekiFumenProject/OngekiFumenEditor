using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Modules.SplashScreen.Commands.ShowSplashScreen;

[RegisterSingleton<CommandDefinitionBase>]
public class ShowSplashScreenCommandDefinition : CommandDefinition
{
    public const string CommandName = "OngekiFumenEditor.Avalonia.Modules.SplashScreen.Commands.ShowSplashScreen.ShowSplashScreenCommandDefinition";

    public override string Name => CommandName;

    public override LocalizedString Text { get; } = "ShowSplashScreenCommandDefinition".ToLocalizedStringByRawText();

    public override LocalizedString ToolTip => Text;

    public override Uri IconSource => ResourceUtils.GetResourceUri("Icons/home.png");
}
