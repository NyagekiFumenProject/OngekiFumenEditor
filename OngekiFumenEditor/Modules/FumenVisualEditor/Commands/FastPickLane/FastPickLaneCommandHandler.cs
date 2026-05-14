using Caliburn.Micro;
using Gemini.Framework.Commands;
using Gemini.Framework.Threading;
using OngekiFumenEditor.Core.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Core.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Core.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System.Linq;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Commands.FastPickLane
{
	public abstract class FastPickLaneCommandHandler<T, DEF> : CommandHandlerBase<DEF> where DEF : FastPickLaneCommandDefinition<T> where T : LaneStartBase
	{
		public override Task Run(Command command)
		{
			if (IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor is not FumenVisualEditorViewModel editor)
				return TaskUtility.Completed;
			if (!editor.IsDesignMode)
			{
				editor.Toast.ShowMessage(Resources.EditorMustBeDesignMode);
				return TaskUtility.Completed;
			}

			var filterTGrid = editor.ConvertYToTGrid_DesignMode(editor.RectInDesignMode.MaxY);
			var selectLane = editor.Fumen.Lanes.OfType<T>().Where(x => x.MaxTGrid <= filterTGrid).OrderBy(x => x.MaxTGrid).LastOrDefault();

			var obj = selectLane?.Children.LastOrDefault() as ConnectableObjectBase;
			obj = obj ?? selectLane;

			if (obj is not null)
				editor.NotifyObjectClicked(obj);

			return TaskUtility.Completed;
		}
	}

	[CommandHandler]
	public class FastPickWallLeftLaneCommandHandler : FastPickLaneCommandHandler<WallLeftStart, FastPickWallLeftLaneCommandDefinition>
	{ }

	[CommandHandler]
	public class FastPickWallRightLaneCommandHandler : FastPickLaneCommandHandler<WallRightStart, FastPickWallRightLaneCommandDefinition>
	{ }

	[CommandHandler]
	public class FastPickRightLaneCommandHandler : FastPickLaneCommandHandler<LaneRightStart, FastPickRightLaneCommandDefinition>
	{ }


	[CommandHandler]
	public class FastPickCenterLaneCommandHandler : FastPickLaneCommandHandler<LaneCenterStart, FastPickCenterLaneCommandDefinition>
	{ }


	[CommandHandler]
	public class FastPickLeftLaneCommandHandler : FastPickLaneCommandHandler<LaneLeftStart, FastPickLeftLaneCommandDefinition>
	{ }
}
