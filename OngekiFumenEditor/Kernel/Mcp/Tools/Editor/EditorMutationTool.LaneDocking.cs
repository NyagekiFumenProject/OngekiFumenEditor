using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using System;
using System.Globalization;
using System.Linq;

namespace OngekiFumenEditor.Kernel.Mcp.Tools.Editor
{
    internal sealed partial class EditorMutationTool
    {
        /// <summary>
        /// Tap/Hold 的 lane 引用在 MCP 里统一用 pseudo 属性 <c>referenceLaneRecordId</c> 表达（设计文档 §58）：
        /// 数字绑定、空串/null/负数（含 UI 侧的 -1 哨兵）滞空。CLR 侧的 <c>ReferenceLaneStart</c> 与
        /// 触发式的 <c>ReferenceLaneStrIdManualSet</c> 都不作为稳定 API 字段暴露。
        /// </summary>
        private const string ReferenceLaneRecordIdProperty = "referenceLaneRecordId";

        private static ILaneDockable RequireLaneDockable(OngekiObjectBase obj)
            => obj as ILaneDockable ?? throw new ArgumentException($"Object #{obj.Id} ({obj.GetType().Name}) is not lane-dockable; only tap and hold support {ReferenceLaneRecordIdProperty}.");

        private static LaneStartBase ResolveLaneByRecordId(OngekiFumen fumen, int recordId)
            => fumen.Lanes.FirstOrDefault(x => x.RecordId == recordId)
                ?? throw new ArgumentException($"No lane with RecordId {recordId} in the editor.");

        private const string DockModeExplicit = "explicit";
        private const string DockModeNearest = "nearest";

        /// <summary>
        /// §17：<c>dockMode</c> 取值归一化。省略 / 空串 = <c>explicit</c>（维持显式绑定语义），
        /// 大小写不敏感地接受 explicit / nearest，其它取值直接失败。
        /// </summary>
        private static bool TryNormalizeDockMode(string rawValue, out string mode, out string error)
        {
            mode = DockModeExplicit;
            error = default;

            var text = (rawValue ?? string.Empty).Trim();
            if (text.Length == 0)
                return true;

            if (string.Equals(text, DockModeExplicit, StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.Equals(text, DockModeNearest, StringComparison.OrdinalIgnoreCase))
            {
                mode = DockModeNearest;
                return true;
            }

            error = $"'dockMode' must be '{DockModeExplicit}' (honor referenceLaneRecordId / snapXToLane) or '{DockModeNearest}' (dock to the closest lane with a path here); '{rawValue}' is not supported.";
            return false;
        }

        /// <summary>
        /// §17：<c>dockMode=nearest</c> 的选道规则 —— 取该 TGrid 处算得出 XGrid 的最近可停靠 lane
        /// （|laneXGrid - xGrid| 升序，同距按 RecordId 升序）。没有任何候选就直接失败，绝不静默滞空。
        /// </summary>
        private static LaneStartBase PickNearestDockableLane(OngekiFumen fumen, TGrid tGrid, XGrid xGrid)
        {
            var pick = fumen.Lanes
                .GetVisibleStartObjects(tGrid, tGrid)
                .Where(x => x.IsDockableLane)
                .Select(x => (Lane: x, LaneXGrid: x.CalulateXGrid(tGrid)))
                .Where(x => x.LaneXGrid is not null)
                .OrderBy(x => Math.Abs(x.LaneXGrid.TotalGrid - xGrid.TotalGrid))
                .ThenBy(x => x.Lane.RecordId)
                .FirstOrDefault();

            return pick.Lane
                ?? throw new ArgumentException($"No dockable lane (center/left/right/wallLeft/wallRight) has a path at T[{tGrid.Unit},{tGrid.Grid}]; dockMode=nearest needs at least one candidate there.");
        }

        /// <summary>按 dockMode=nearest 重新绑定 lane 并吸附 XGrid；hold 连终点一起吸附。</summary>
        private static void DockDockableToNearestLane(OngekiFumen fumen, OngekiObjectBase obj)
        {
            var dockable = RequireLaneDockable(obj);
            var (tGrid, xGrid) = obj switch
            {
                Tap tap => (tap.TGrid, tap.XGrid),
                Hold hold => (hold.TGrid, hold.XGrid),
                _ => throw new ArgumentException($"Object #{obj.Id} ({obj.GetType().Name}) cannot dock to a lane; only tap and hold do."),
            };

            dockable.ReferenceLaneStart = PickNearestDockableLane(fumen, tGrid, xGrid);
            SnapDockableXGridToBoundLane(obj);
        }

        /// <summary>
        /// 解析 MCP 的 lane 引用取值。返回 false 表示“滞空”（空串 / "null" / 负数），此时 out 值无意义。
        /// 只有既不是数字也不是滞空写法时才抛错。
        /// </summary>
        private static bool TryParseLaneRecordId(string rawValue, out int recordId)
        {
            recordId = default;

            var text = (rawValue ?? string.Empty).Trim();
            if (text.Length == 0 || string.Equals(text, "null", StringComparison.OrdinalIgnoreCase))
                return false;

            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                throw new ArgumentException($"'{rawValue}' is not a valid lane RecordId; pass a number, or an empty string / null / -1 to clear the binding.");

            if (parsed < 0)
                return false;

            recordId = parsed;
            return true;
        }

        /// <summary>
        /// §62：lane 必须在目标 TGrid 处确实算得出 XGrid，算不出就直接校验失败，绝不静默沿用旧 XGrid。
        /// </summary>
        private static XGrid RequireLaneXGrid(LaneStartBase lane, TGrid tGrid, OngekiObjectBase obj)
            => lane.CalulateXGrid(tGrid)
                ?? throw new ArgumentException($"Lane #{lane.RecordId} cannot produce an XGrid at T[{tGrid.Unit},{tGrid.Grid}]; object #{obj.Id} cannot be snapped there because the lane has no path covering that position.");

        /// <summary>按对象当前绑定的 lane 重新吸附 XGrid；hold 连终点一起吸附。</summary>
        private static void SnapDockableXGridToBoundLane(OngekiObjectBase obj)
        {
            var dockable = RequireLaneDockable(obj);
            var lane = dockable.ReferenceLaneStart
                ?? throw new ArgumentException($"Object #{obj.Id} is not bound to a lane; set {ReferenceLaneRecordIdProperty} before snapping.");

            switch (obj)
            {
                case Tap tap:
                    tap.XGrid = RequireLaneXGrid(lane, tap.TGrid, tap);
                    return;

                case Hold hold:
                    hold.XGrid = RequireLaneXGrid(lane, hold.TGrid, hold);
                    if (hold.HoldEnd is { } holdEnd)
                        holdEnd.XGrid = RequireLaneXGrid(lane, holdEnd.TGrid, holdEnd);
                    return;

                default:
                    throw new ArgumentException($"Object #{obj.Id} ({obj.GetType().Name}) cannot be snapped to a lane; only tap and hold do.");
            }
        }

        /// <summary>
        /// 停靠 / 吸附改动的是 lane 绑定和 XGrid，并不属于被修改的那个属性本身，所以 undo / 回滚时必须
        /// 单独把这些也还原，否则撤销 lane 绑定或 dockMode=nearest 之后对象会停在新的绑定或位置上。
        /// </summary>
        private sealed class LaneDockingSnapshot
        {
            private XGrid startXGrid;
            private XGrid holdEndXGrid;
            private LaneStartBase laneStart;

            public static LaneDockingSnapshot Capture(OngekiObjectBase obj) => obj switch
            {
                Tap tap => new LaneDockingSnapshot { startXGrid = tap.XGrid, laneStart = tap.ReferenceLaneStart },
                Hold hold => new LaneDockingSnapshot { startXGrid = hold.XGrid, holdEndXGrid = hold.HoldEnd?.XGrid, laneStart = hold.ReferenceLaneStart },
                _ => default,
            };

            public void Restore(OngekiObjectBase obj)
            {
                switch (obj)
                {
                    case Tap tap when startXGrid is not null:
                        tap.ReferenceLaneStart = laneStart;
                        tap.XGrid = startXGrid;
                        return;

                    case Hold hold:
                        if (startXGrid is not null)
                        {
                            hold.ReferenceLaneStart = laneStart;
                            hold.XGrid = startXGrid;
                        }
                        if (hold.HoldEnd is { } holdEnd && holdEndXGrid is not null)
                            holdEnd.XGrid = holdEndXGrid;
                        return;
                }
            }
        }
    }
}
