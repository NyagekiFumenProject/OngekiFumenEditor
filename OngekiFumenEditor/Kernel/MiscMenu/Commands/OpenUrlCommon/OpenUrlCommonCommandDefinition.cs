using Gemini.Framework.Commands;
using OngekiFumenEditor.Properties;

namespace OngekiFumenEditor.Kernel.MiscMenu.Commands.OpenUrlCommon
{
	public abstract class OpenUrlCommonCommandDefinition : CommandDefinition
	{
		public abstract string NameOverride { get; }
		public abstract string Url { get; }
		public override string Name => $"OpenUrl.{NameOverride}";
		public override string ToolTip => Text;
	}

	[CommandDefinition]
	public class UsageWikiCommandDefinition : OpenUrlCommonCommandDefinition
	{
		public override string NameOverride => "UsageWiki";
		public override string Text => Resources.TutorialAndDocument;
		public override string Url => "https://github.com/NyagekiFumenProject/OngekiFumenEditor/wiki";
	}

	[CommandDefinition]
	public class OpenProjectUrlCommandDefinition : OpenUrlCommonCommandDefinition
	{
		public override string NameOverride => "OpenProjectUrl";
		public override string Text => Resources.CommandOpenProjectUrl;
		public override string Url => "https://github.com/NyagekiFumenProject/OngekiFumenEditor";
	}


	[CommandDefinition]
	public class RequestIssueHelpCommandDefinition : OpenUrlCommonCommandDefinition
	{
		public override string NameOverride => "RequestIssueHelp";
		public override string Text => Resources.CommandRequestIssueHelp;
		public override string Url => "https://docs.microsoft.com/en-us/visualstudio/get-started/csharp/tutorial-debugger?view=vs-2022";
	}

	[CommandDefinition]
	public class PostSuggestCommandDefinition : OpenUrlCommonCommandDefinition
	{
		public override string NameOverride => "PostSuggest";
		public override string Text => Resources.CommandPostSuggest;
		public override string Url => "https://github.com/NyagekiFumenProject/OngekiFumenEditor/pulls";
	}
}
