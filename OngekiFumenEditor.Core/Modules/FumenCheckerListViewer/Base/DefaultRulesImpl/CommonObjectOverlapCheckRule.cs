using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Core.Properties;
using OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl;
using OngekiFumenEditor.Utils;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl
{
    [Export(typeof(IFumenCheckRule))]
    internal class CommonObjectOverlapCheckRule : IFumenCheckRule
    {
        public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, IFumenCheckContext fumenHostViewModel)
        {
            IEnumerable<ICheckResult> CheckList<T>(IEnumerable<T> objs) where T : OngekiObjectBase, ITimelineObject, IHorizonPositionObject
            {
                const string ruleName = "ObjectOverlap";

                foreach (var conflict in objs.GroupBy(x => (x.TGrid, x.XGrid)).Where(x => x.Count() > 1))
                {
                    if (conflict.FirstOrDefault() is IBulletPalleteReferencable)
                    {
                        if (conflict.OfType<IBulletPalleteReferencable>().GroupBy(x => x.ReferenceBulletPallete switch
                            {
                                null => "<2857null2857>",
                                BulletPallete pallete => pallete.StrID,
                            })
                            .All(x => x.Count() == 1))
                        {
                            continue;
                        }
                    }

                    yield return new CommonCheckResult
                    {
                        Severity = conflict.FirstOrDefault() switch
                        {
                            Bell => RuleSeverity.Problem,
                            _ => RuleSeverity.Error
                        },
                        Description = Resources.ObjectOverlap2.Format(conflict.First().IDShortName),
                        LocationDescription = $"{conflict.Key.XGrid} {conflict.Key.TGrid}",
                        NavigateBehavior = new NavigateToTGridBehavior(conflict.Key.TGrid),
                        RuleName = ruleName,
                    };
                }
            }

            IEnumerable<ICheckResult> CheckOnlyTimelineList(IEnumerable<OngekiTimelineObjectBase> objs)
            {
                const string ruleName = "ObjectOverlap";

                foreach (var conflict in objs.GroupBy(x => x.TGrid).Where(x => x.Count() > 1))
                {
                    yield return new CommonCheckResult
                    {
                        Severity = RuleSeverity.Error,
                        Description = Resources.ObjectOverlap.Format(conflict.First().IDShortName),
                        LocationDescription = $"{conflict.Key}",
                        NavigateBehavior = new NavigateToTGridBehavior(conflict.Key),
                        RuleName = ruleName,
                    };
                }
            }

            foreach (var result in CheckList(fumen.Bells))
                yield return result;
            foreach (var result in CheckList(fumen.Flicks))
                yield return result;
            foreach (var result in CheckList(fumen.Taps.AsEnumerable<OngekiMovableObjectBase>().Concat(fumen.Holds)))
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
