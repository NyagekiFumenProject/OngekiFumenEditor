using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl
{
    [Export(typeof(IFumenCheckRule))]
    internal class CommonObjectOverlapCheckRule : IFumenCheckRule
    {
        public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, object fumenHostViewModel)
        {
            IEnumerable<ICheckResult> CheckList(IEnumerable<OngekiMovableObjectBase> objs)
            {
                const string RuleName = "ObjectOverlap";

                foreach (var conflict in objs.GroupBy(x => (x.TGrid, x.XGrid)).Where(x => x.Count() > 1))
                {
                    yield return new CommonCheckResult()
                    {
                        Severity = conflict.FirstOrDefault() switch
                        {
                            Bell => RuleSeverity.Problem,
                            _ => RuleSeverity.Error
                        },
                        Description = $"多个同类{conflict.FirstOrDefault().IDShortName}物件重叠",
                        LocationDescription = $"{conflict.Key.XGrid} {conflict.Key.TGrid}",
                        NavigateTGridLocation = conflict.Key.TGrid,
                        RuleName = RuleName,
                    };
                }
            }

            IEnumerable<ICheckResult> CheckOnlyTimelineList(IEnumerable<OngekiTimelineObjectBase> objs)
            {
                const string RuleName = "ObjectOverlap";

                foreach (var conflict in objs.GroupBy(x => (x.TGrid)).Where(x => x.Count() > 1))
                {
                    yield return new CommonCheckResult()
                    {
                        Severity = RuleSeverity.Error,
                        Description = $"多个ITimelineObject同类({conflict.FirstOrDefault().IDShortName})物件重叠",
                        LocationDescription = $"{conflict.Key}",
                        NavigateTGridLocation = conflict.Key,
                        RuleName = RuleName,
                    };
                }
            }


            foreach (var result in CheckList(fumen.Bells))
                yield return result;
            foreach (var result in CheckList(fumen.Flicks))
                yield return result;
            foreach (var result in CheckList(fumen.Taps
                .AsEnumerable<OngekiMovableObjectBase>()
                //HoldEnd暂时不用考虑重叠问题
                .Concat(fumen.Holds)))
                yield return result;
            foreach (var result in CheckList(fumen.Bullets))
                yield return result;
            foreach (var result in CheckOnlyTimelineList(fumen.ClickSEs))
                yield return result;
            foreach (var result in CheckOnlyTimelineList(fumen.BpmList))
                yield return result;
            foreach (var result in CheckOnlyTimelineList(fumen.EnemySets))
                yield return result;
            foreach (var result in CheckOnlyTimelineList(fumen.MeterChanges))
                yield return result;
        }
    }
}

