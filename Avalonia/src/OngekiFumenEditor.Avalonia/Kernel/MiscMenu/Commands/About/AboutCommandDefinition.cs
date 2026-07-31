using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;

namespace OngekiFumenEditor.Avalonia.Kernel.MiscMenu.Commands.About;

[RegisterSingleton<CommandDefinitionBase>]
public class AboutCommandDefinition : CommandDefinition
{
    public const string CommandName = "Help.About";

    public override string Name => CommandName;

    public override LocalizedString Text { get; } = Lang.B.CommandAbout.ToLocalizedString();

    public override LocalizedString ToolTip => Text;
}

