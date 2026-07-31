using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;

namespace OngekiFumenEditor.Avalonia.Modules.FumenConverter.Commands;

[RegisterSingleton<CommandDefinitionBase>]
public class ViewFumenConverterCommandDefinition : CommandDefinition
{
    public const string CommandName = "View.FumenConverter";
    public override string Name => CommandName;
    public override LocalizedString Text { get; } = Lang.B.FumenConverter.ToLocalizedString();
    public override LocalizedString ToolTip => Text;
}
