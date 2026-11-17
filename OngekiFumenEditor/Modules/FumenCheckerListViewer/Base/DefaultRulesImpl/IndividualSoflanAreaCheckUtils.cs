using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using System.Linq;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl
{
    internal static class IndividualSoflanAreaCheckUtils
    {
        /// <summary>
        /// 区域里是否存在会被游戏写入 soflan pattern 的长条。
        /// 游戏侧只有 Hold 会执行 laneObj.pattern = area.pattern，Tap/Bullet 等不会。
        /// </summary>
        public static bool ContainsHold(IndividualSoflanArea area, OngekiFumen fumen)
            => fumen.Holds.Cast<ILaneDockable>().Any(x => IsInsideArea(area, x));

        /// <summary>
        /// 与游戏侧 Notes.CheckSoflanPatternLange 对 Hold 的判定保持一致：
        /// T 区间为 [区域起点, 区域终点)，X 用整数 place（编辑器里就是 XGrid.TotalUnit 的整数部分）比较。
        /// </summary>
        public static bool IsInsideArea(IndividualSoflanArea area, ILaneDockable dockable)
        {
            if (dockable.TGrid < area.TGrid || dockable.TGrid >= area.EndIndicator.TGrid)
                return false;

            var x = (int)dockable.XGrid.TotalUnit;
            return x >= (int)area.XGrid.TotalUnit && x <= (int)area.EndIndicator.XGrid.TotalUnit;
        }
    }
}
