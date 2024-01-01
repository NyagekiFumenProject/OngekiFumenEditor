using Caliburn.Micro;
using Gemini.Framework.Commands;
using Gemini.Framework.Threading;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Properties;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Commands.OgkrImpl.InterpolateAll
{
	[CommandHandler]
	public class InterpolateAllCommandHandler : InterpolateAllCommandHandlerBase<InterpolateAllCommandDefinition>
	{
		public override void Update(Command command)
		{
			base.Update(command);
			command.Enabled = IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor is not null;
		}

		public override Task Run(Command command)
		{
			if (IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor is not FumenVisualEditorViewModel editor)
				return TaskUtility.Completed;
			if (MessageBox.Show(Resources.ComfirmInterpolateMessage, Resources.Suggest, MessageBoxButton.YesNo) != MessageBoxResult.Yes)
				return TaskUtility.Completed;
			editor.LockAllUserInteraction();

			Process(editor, false);

			editor.UnlockAllUserInteraction();
			return TaskUtility.Completed;
		}
	}
	[CommandHandler]
	public class InterpolateAllWithXGridLimitCommandHandler : InterpolateAllCommandHandlerBase<InterpolateAllWithXGridLimitCommandDefinition>
	{
		public override void Update(Command command)
		{
			base.Update(command);
			command.Enabled = IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor is not null;
		}

		public override Task Run(Command command)
		{
			if (IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor is not FumenVisualEditorViewModel editor)
				return TaskUtility.Completed;
			if (MessageBox.Show(Resources.ComfirmInterpolateMessage, Resources.Suggest, MessageBoxButton.YesNo) != MessageBoxResult.Yes)
				return TaskUtility.Completed;
			editor.LockAllUserInteraction();

			Process(editor, true);

			editor.UnlockAllUserInteraction();
			return TaskUtility.Completed;
		}
	}
}