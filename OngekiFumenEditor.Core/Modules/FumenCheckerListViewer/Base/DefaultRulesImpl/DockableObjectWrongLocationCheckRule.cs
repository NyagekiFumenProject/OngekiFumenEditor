using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl
{
    [Export(typeof(IFumenCheckRule))]
    internal class DockableObjectWrongLocationCheckRule : IFumenCheckRule
    {
        private const string RuleName = "WrongLocation";

        public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, IFumenCheckContext fumenHostViewModel)
        {
            foreach (var dockableObj in fumen.Holds
                         .AsEnumerable<ILaneDockable>()
                         .Concat(fumen.Taps)
                         .Where(x => x.ReferenceLaneStart is not null)
                         .Where(x => CheckIsWrongLocation(x, x.ReferenceLaneStart)))
            {
                yield return new CommonCheckResult
                {
                    Description = FumenCheckMessages.Get(FumenCheckMessageKey.WrongLocation, dockableObj.GetType().Name, dockableObj.ReferenceLaneStrId),
                    LocationDescription = dockableObj.ToString(),
                    NavigateBehavior = new NavigateToObjectBehavior(dockableObj as OngekiTimelineObjectBase),
                    RuleName = RuleName,
                    Severity = RuleSeverity.Problem
                };
            }
        }

        private static bool CheckIsWrongLocation(ILaneDockable obj, LaneStartBase referenceLaneStart)
        {
            if (obj.TGrid > referenceLaneStart.MaxTGrid || obj.TGrid < referenceLaneStart.MinTGrid)
                return true;

            var calXGrid = referenceLaneStart.CalulateXGrid(obj.TGrid);
            if (calXGrid is null)
                return false;

            return Math.Abs((calXGrid - obj.XGrid).TotalGrid(calXGrid.ResX)) > calXGrid.ResX;
        }
    }
}
