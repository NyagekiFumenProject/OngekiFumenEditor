using Gemini.Framework.Commands;
using Gemini.Framework.Services;
using Gemini.Framework.Threading;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.AudioPlayerToolViewer.Commands
{
	[CommandHandler]
	public class ViewAudioPlayerToolViewerCommandHandler : CommandHandlerBase<ViewAudioPlayerToolViewerCommandDefinition>
	{
		private readonly IShell _shell;

		[ImportingConstructor]
		public ViewAudioPlayerToolViewerCommandHandler(IShell shell)
		{
			_shell = shell;
		}

		public override Task Run(Command command)
		{
			_shell.ShowTool<IAudioPlayerToolViewer>();
			return TaskUtility.Completed;
		}
	}
}