using Caliburn.Micro;
using Gemini.Framework.Commands;
using Gemini.Framework.Threading;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
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
			if (MessageBox.Show("是否插值所有包含曲线的轨道物件?\n可能将会删除并重新生成已经插值好的,不含曲线的轨道物件\n部分高度重叠的Tap/Hold物件可能会因此改变它依赖的轨道物件", "提醒", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
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
			if (MessageBox.Show("是否插值所有包含曲线的轨道物件?\n可能将会删除并重新生成已经插值好的,不含曲线的轨道物件\n部分高度重叠的Tap/Hold物件可能会因此改变它依赖的轨道物件", "提醒", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
				return TaskUtility.Completed;
			editor.LockAllUserInteraction();

			Process(editor, true);

			editor.UnlockAllUserInteraction();
			return TaskUtility.Completed;
		}
	}
}