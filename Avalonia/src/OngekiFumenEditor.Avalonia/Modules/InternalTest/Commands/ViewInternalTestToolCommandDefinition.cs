using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Modules.InternalTest.Commands;

[RegisterSingleton<CommandDefinitionBase>]
public class ViewInternalTestToolCommandDefinition : CommandDefinition
{
    public const string CommandName = "View.InternalTestTool";

    public override string Name => CommandName;

    public override LocalizedString Text { get; } = "内部测试Toolbox".ToLocalizedStringByRawText();

    public override LocalizedString ToolTip { get; } = "内部测试Toolbox的ToolTip".ToLocalizedStringByRawText();
}