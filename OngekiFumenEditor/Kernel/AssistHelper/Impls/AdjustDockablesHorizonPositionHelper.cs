using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Utils;
using System.Collections.Generic;
using System.Linq;

namespace OngekiFumenEditor.Kernel.AssistHelper.Impls
{
    /// <summary>
    /// 单个 Tap/Hold 的水平位置对齐结果：Before 为调整前的位置，After 为依附轨道算出的目标位置。
    /// </summary>
    public readonly record struct DockableHorizonAdjustment(IHorizonPositionObject Target, XGrid Before, XGrid After);

    public static class AdjustDockablesHorizonPositionHelper
    {
        private static XGrid CalculateConnectableObjectXGrid(ConnectableStartObject startObject, TGrid tGrid)
        {
            if (tGrid < startObject.TGrid)
                return default;

            return startObject.CalulateXGrid(tGrid);
        }

        /// <summary>
        /// 计算所有依附轨道的 Tap/Hold 需要对水平位置做的调整；计算失败的项保持原样并记录日志。
        /// 调用方负责把返回结果包成可撤销动作。
        /// </summary>
        public static IReadOnlyList<DockableHorizonAdjustment> CalculateAdjustments(OngekiFumen fumen)
        {
            var adjustments = new List<DockableHorizonAdjustment>();

            void collect<T>(IEnumerable<T> objs) where T : IHorizonPositionObject, ITimelineObject, ILaneDockable
            {
                foreach (var o in objs)
                {
                    if (CalculateConnectableObjectXGrid(o.ReferenceLaneStart, o.TGrid) is XGrid xGrid)
                    {
                        adjustments.Add(new(o, o.XGrid.CopyNew(), xGrid.CopyNew()));
                    }
                    else
                    {
                        Log.LogError($"Adjust dockable horizon position failed: object={o.GetType().Name}, tGrid={o.TGrid}, referenceLane={o.ReferenceLaneStart?.RecordId}");
                    }
                }
            }

            collect(fumen.Taps.Where(x => x.ReferenceLaneStart is not null));
            collect(fumen.Holds.Where(x => x.ReferenceLaneStart is not null));

            return adjustments;
        }
    }
}
