using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl
{
    [Export(typeof(IFumenCheckRule))]
    internal class DockableObjectWrongLocationCheckRule : IFumenCheckRule
    {
        const string RuleName = "WrongLocation";

        public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, FumenVisualEditorViewModel fumenHostViewModel)
        {
            foreach (var dockableObj in fumen.Holds
                .AsEnumerable<ILaneDockable>()
                .Concat(fumen.Taps)
                .Where(x => x.ReferenceLaneStart is not null)
                .Where(x => CheckIsWrongLocation(x, x.ReferenceLaneStart))
                )
            {
                yield return new CommonCheckResult()
                {
                    Description = $"{dockableObj.GetType().Name}物件貌似没有放置在正确的轨道(id:{dockableObj.ReferenceLaneStrId})线上",
                    LocationDescription = dockableObj.ToString(),
                    NavigateBehavior = new NavigateToObjectBehavior(dockableObj as OngekiTimelineObjectBase),
                    RuleName = RuleName,
                    Severity = RuleSeverity.Problem
                };
            }
        }

        private bool CheckIsWrongLocation(ILaneDockable x, LaneStartBase referenceLaneStart)
        {
            if (x.TGrid > referenceLaneStart.MaxTGrid || x.TGrid < referenceLaneStart.MinTGrid)
                return true;

            var calXGrid = referenceLaneStart.CalulateXGrid(x.TGrid);
            if (calXGrid is null)
                return false;

            return Math.Abs((calXGrid - x.XGrid).TotalGrid(calXGrid.ResX)) > calXGrid.ResX * 1;
        }
    }
}
