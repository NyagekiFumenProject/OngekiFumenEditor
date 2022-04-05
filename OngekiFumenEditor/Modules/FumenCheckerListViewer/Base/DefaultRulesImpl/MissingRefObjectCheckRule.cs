using OngekiFumenEditor.Base;
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
    internal class MissingRefObjectCheckRule : IFumenCheckRule
    {
        const string RuleName = "MissingRefObject";

        public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, object fumenHostViewModel)
        {
            foreach (var dockableObj in fumen.Holds.AsEnumerable<ILaneDockable>().Concat(fumen.Taps).Where(x => x.ReferenceLaneStart is null))
            {
                yield return new CommonCheckResult()
                {
                    Description = $"{dockableObj.GetType().Name}物件缺少引用Lane物件",
                    LocationDescription = dockableObj.ToString(),
                    NavigateTGridLocation = dockableObj.TGrid,
                    RuleName = RuleName,
                    Severity = RuleSeverity.Error
                };
            }

            foreach (var dockableObj in fumen.Holds.AsEnumerable<ILaneDockable>().Concat(fumen.Taps).Where(x => !fumen.Lanes.Contains(x.ReferenceLaneStart)))
            {
                yield return new CommonCheckResult()
                {
                    Description = $"{dockableObj.GetType().Name}物件引用的Lane物件(laneId:{dockableObj.ReferenceLaneStrId}),不存在于谱面文件内",
                    LocationDescription = dockableObj.ToString(),
                    NavigateTGridLocation = dockableObj.TGrid,
                    RuleName = RuleName,
                    Severity = RuleSeverity.Error
                };
            }
        }
    }
}
