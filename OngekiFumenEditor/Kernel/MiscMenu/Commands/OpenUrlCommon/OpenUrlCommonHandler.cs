using Gemini.Framework.Commands;
using Gemini.Framework.Threading;
using OngekiFumenEditor.Utils;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.MiscMenu.Commands.OpenUrlCommon
{
	public class OpenUrlCommonHandler<T> : CommandHandlerBase<T> where T : OpenUrlCommonCommandDefinition
	{
		public override Task Run(Command command)
		{
			var def = (T)command.CommandDefinition;
			ProcessUtils.OpenUrl(def.Url);
			return TaskUtility.Completed;
		}
	}

	[CommandHandler]
	public class UsageWikiUrlCommonHandler : OpenUrlCommonHandler<UsageWikiCommandDefinition> { }

	[CommandHandler]
	public class OpenProjectUrlCommonHandler : OpenUrlCommonHandler<OpenProjectUrlCommandDefinition> { }

	[CommandHandler]
	public class RequestIssueHelpCommonHandler : OpenUrlCommonHandler<RequestIssueHelpCommandDefinition> { }

	[CommandHandler]
	public class PostSuggestCommonHandler : OpenUrlCommonHandler<PostSuggestCommandDefinition> { }
}
