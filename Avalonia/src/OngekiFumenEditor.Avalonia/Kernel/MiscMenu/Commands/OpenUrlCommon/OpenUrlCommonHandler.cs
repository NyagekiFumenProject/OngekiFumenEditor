using Avalonia;
using Gekimini.Avalonia.Framework.Commands;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Kernel.MiscMenu.Commands.OpenUrlCommon;

public class OpenUrlCommonHandler<T> : CommandHandlerBase<T> where T : OpenUrlCommonCommandDefinition
{
    public override async Task Run(Command command)
    {
        if (command.CommandDefinition is not T definition ||
            Application.Current is not global::Gekimini.Avalonia.App app)
            return;

        var launched = await app.TopLevel.Launcher.LaunchUriAsync(new Uri(definition.Url));
        if (!launched)
            Log.LogWarning($"Unable to launch URL: {definition.Url}");
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
