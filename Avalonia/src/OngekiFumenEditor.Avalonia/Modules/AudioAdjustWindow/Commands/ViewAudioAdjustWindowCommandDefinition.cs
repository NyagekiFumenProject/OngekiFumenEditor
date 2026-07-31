using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;

namespace OngekiFumenEditor.Avalonia.Modules.AudioAdjustWindow.Commands;

[RegisterSingleton<CommandDefinitionBase>]
public class ViewAudioAdjustWindowCommandDefinition : CommandDefinition
{
    public const string CommandName = "View.AudioAdjustWindow";
    public override string Name => CommandName;
    public override LocalizedString Text { get; } = Lang.B.CommandAudioAdjustWindow.ToLocalizedString();
    public override LocalizedString ToolTip => Text;
}
