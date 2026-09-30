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
        /// 吸附改动的 XGrid 并不属于被修改的那个属性本身，所以 undo / 回滚时必须单独把这几处位置也还原，
        /// 否则撤销 lane 绑定之后对象会停在吸附后的位置上。
        /// </summary>
        private sealed class DockableXGridSnapshot
        {
            private XGrid startXGrid;
            private XGrid holdEndXGrid;

            public static DockableXGridSnapshot Capture(OngekiObjectBase obj) => obj switch
            {
                Tap tap => new DockableXGridSnapshot { startXGrid = tap.XGrid },
                Hold hold => new DockableXGridSnapshot { startXGrid = hold.XGrid, holdEndXGrid = hold.HoldEnd?.XGrid },
                _ => default,
            };

            public void Restore(OngekiObjectBase obj)
            {
                switch (obj)
                {
                    case Tap tap when startXGrid is not null:
                        tap.XGrid = startXGrid;
                        return;

                    case Hold hold:
                        if (startXGrid is not null)
                            hold.XGrid = startXGrid;
                        if (hold.HoldEnd is { } holdEnd && holdEndXGrid is not null)
                            holdEnd.XGrid = holdEndXGrid;
                        return;
                }
            }
        }
    }
}
