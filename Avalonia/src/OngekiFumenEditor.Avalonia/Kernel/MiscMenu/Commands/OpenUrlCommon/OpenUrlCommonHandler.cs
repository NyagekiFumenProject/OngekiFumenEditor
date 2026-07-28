using Gekimini.Avalonia.Framework.Commands;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Kernel.MiscMenu.Commands.OpenUrlCommon;

public class OpenUrlCommonHandler<T> : CommandHandlerBase<T> where T : OpenUrlCommonCommandDefinition
{
    public override Task Run(Command command)
    {
        if (command.CommandDefinition is T def)
            ProcessUtils.OpenUrl(def.Url);
        return Task.CompletedTask;
    }
}

[RegisterSingleton<ICommandHandler>]
public class UsageWikiUrlCommonHandler : OpenUrlCommonHandler<UsageWikiCommandDefinition>
{
}

[RegisterSingleton<ICommandHandler>]
public class OpenProjectUrlCommonHandler : OpenUrlCommonHandler<OpenProjectUrlCommandDefinition>
{
}

[RegisterSingleton<ICommandHandler>]
public class RequestIssueHelpCommonHandler : OpenUrlCommonHandler<RequestIssueHelpCommandDefinition>
{
}

[RegisterSingleton<ICommandHandler>]
public class PostSuggestCommonHandler : OpenUrlCommonHandler<PostSuggestCommandDefinition>
{
}
