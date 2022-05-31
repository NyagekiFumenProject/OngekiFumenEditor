using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using Caliburn.Micro;
using Gemini.Framework.Commands;
using Gemini.Framework.Services;
using Gemini.Framework.Threading;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Utils;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Commands.FastPickLane
{
    public abstract class FastPickLaneCommandHandler<T, DEF> : CommandHandlerBase<DEF> where DEF : FastPickLaneCommandDefinition<T> where T : LaneStartBase
    {
        public override Task Run(Command command)
        {
            if (IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor is not FumenVisualEditorViewModel editor)
                return TaskUtility.Completed;

            var filterTGrid = TGridCalculator.ConvertYToTGrid(editor.MaxVisibleCanvasY, editor);
            var selectLane = editor.Fumen.Lanes.OfType<T>().Where(x => x.MaxTGrid <= filterTGrid).OrderBy(x => x.MaxTGrid).LastOrDefault();

            var obj = selectLane?.Children.LastOrDefault() as ConnectableObjectBase;
            obj = obj ?? selectLane;

            if (obj is not null)
                editor.NotifyObjectClicked(obj);

            return TaskUtility.Completed;
        }
    }

    [CommandHandler]
    public class FastPickRightLaneCommandHandler : FastPickLaneCommandHandler<LaneRightStart, FastPickRightLaneCommandDefinition>
    {}


    [CommandHandler]
    public class FastPickCenterLaneCommandHandler : FastPickLaneCommandHandler<LaneCenterStart, FastPickCenterLaneCommandDefinition>
    { }


    [CommandHandler]
    public class FastPickLeftLaneCommandHandler : FastPickLaneCommandHandler<LaneLeftStart, FastPickLeftLaneCommandDefinition>
    { }
}