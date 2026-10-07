using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl;
using OngekiFumenEditor.Utils;
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
                    Description = Resources.WrongLocation.Format(dockableObj.GetType().Name, dockableObj.ReferenceLaneStrId),
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

            var resX = calXGrid.ResX;
            if (!(Math.Abs((calXGrid - obj.XGrid).TotalGrid(resX)) > resX))
                return false;

            // 水平段（整段 T 相同、只有 X 在变，例如同 T 的 WLS/WLE 瞬时墙）在该时刻覆盖的是一段 X 区间，
            // 物件落在区间内（同样留 1 单位容差）即算贴着轨道线：
            // 零时长段的插值只能取到起点键的 X，不能拿它当“唯一正确位置”判定物件放错。
            return !IsOnHorizontalSegment(referenceLaneStart, obj);
        }

        /// <summary>
        /// 物件所在时刻是否存在一条水平段（整段采样点 T 都等于该时刻），且物件的 X 落在该段的 X 区间内（含 1 单位容差）。
        /// </summary>
        private static bool IsOnHorizontalSegment(LaneStartBase referenceLaneStart, ILaneDockable obj)
        {
            var totalTGrid = obj.TGrid.TotalGrid;
            var totalXGrid = obj.XGrid.TotalGrid;
            var resX = obj.XGrid.ResX;

            foreach (var child in referenceLaneStart.Children)
            {
                var path = child.GetConnectionPaths();
                if (path.Count == 0)
                    continue;

                var minXTotalGrid = double.MaxValue;
                var maxXTotalGrid = double.MinValue;
                var isHorizontal = true;
                foreach (var (pos, _) in path)
                {
                    if (pos.Y != totalTGrid)
                    {
                        isHorizontal = false;
                        break;
                    }

                    minXTotalGrid = Math.Min(minXTotalGrid, pos.X);
                    maxXTotalGrid = Math.Max(maxXTotalGrid, pos.X);
                }

                if (isHorizontal && minXTotalGrid - resX <= totalXGrid && totalXGrid <= maxXTotalGrid + resX)
                    return true;
            }

            return false;
        }
    }
}

