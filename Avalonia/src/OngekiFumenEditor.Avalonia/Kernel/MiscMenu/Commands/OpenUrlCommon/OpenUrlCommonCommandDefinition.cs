using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;

namespace OngekiFumenEditor.Avalonia.Kernel.MiscMenu.Commands.OpenUrlCommon;

public abstract class OpenUrlCommonCommandDefinition : CommandDefinition
{
    public abstract string NameOverride { get; }
    public abstract string Url { get; }

    public override string Name => $"OpenUrl.{NameOverride}";
    public override LocalizedString ToolTip => Text;
}

[RegisterSingleton<CommandDefinitionBase>]
public class UsageWikiCommandDefinition : OpenUrlCommonCommandDefinition
{
    public override string NameOverride => "UsageWiki";
    public override LocalizedString Text { get; } = Lang.B.TutorialAndDocument.ToLocalizedString();
    public override string Url => "https://github.com/NyagekiFumenProject/OngekiFumenEditor/wiki";
}

[RegisterSingleton<CommandDefinitionBase>]
public class OpenProjectUrlCommandDefinition : OpenUrlCommonCommandDefinition
{
    public override string NameOverride => "OpenProjectUrl";
    public override LocalizedString Text { get; } = Lang.B.CommandOpenProjectUrl.ToLocalizedString();
    public override string Url => "https://github.com/NyagekiFumenProject/OngekiFumenEditor";
}

[RegisterSingleton<CommandDefinitionBase>]
public class RequestIssueHelpCommandDefinition : OpenUrlCommonCommandDefinition
{
    public override string NameOverride => "RequestIssueHelp";
    public override LocalizedString Text { get; } = Lang.B.CommandRequestIssueHelp.ToLocalizedString();
    public override string Url => "https://docs.microsoft.com/en-us/visualstudio/get-started/csharp/tutorial-debugger?view=vs-2022";
}

[RegisterSingleton<CommandDefinitionBase>]
public class PostSuggestCommandDefinition : OpenUrlCommonCommandDefinition
{
    public override string NameOverride => "PostSuggest";
    public override LocalizedString Text { get; } = Lang.B.CommandPostSuggest.ToLocalizedString();
    public override string Url => "https://github.com/NyagekiFumenProject/OngekiFumenEditor/pulls";
}

