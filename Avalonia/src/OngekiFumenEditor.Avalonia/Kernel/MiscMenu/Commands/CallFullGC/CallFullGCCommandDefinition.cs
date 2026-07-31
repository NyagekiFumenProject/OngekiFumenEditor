using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;

namespace OngekiFumenEditor.Avalonia.Kernel.MiscMenu.Commands.CallFullGC;

[RegisterSingleton<CommandDefinitionBase>]
public class CallFullGCCommandDefinition : CommandDefinition
{
    public const string CommandName = "File.CallFullGC";

    public override string Name => CommandName;

    public override LocalizedString Text { get; } = Lang.B.CommandCallFullGC.ToLocalizedString();

    public override LocalizedString ToolTip => Text;
}

