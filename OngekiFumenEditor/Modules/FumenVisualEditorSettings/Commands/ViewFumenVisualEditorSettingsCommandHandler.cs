using Gemini.Framework.Commands;
using Gemini.Framework.Services;
using Gemini.Framework.Threading;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditorSettings.Commands
{
	[CommandHandler]
	public class ViewFumenVisualEditorSettingsCommandHandler : CommandHandlerBase<ViewFumenVisualEditorSettingsCommandDefinition>
	{
		private readonly IShell _shell;

		[ImportingConstructor]
		public ViewFumenVisualEditorSettingsCommandHandler(IShell shell)
		{
			_shell = shell;
		}

		public override Task Run(Command command)
		{
			_shell.ShowTool<IFumenVisualEditorSettings>();
			return TaskUtility.Completed;
		}
	}
}