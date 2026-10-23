using OngekiFumenEditor.Base;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Models
{
    public enum IsaPlacementPhase
    {
        Inactive,
        WaitingFirstPoint,
        WaitingSecondPoint,
    }

    /// <summary>规范化后的 ISA 对角点：Start 为时间/水平坐标较小的一侧。</summary>
    public sealed record IsaPlacementResult(TGrid StartTGrid, XGrid StartXGrid, TGrid EndTGrid, XGrid EndXGrid);

    /// <summary>
    /// ISA 两点放置的无 UI 状态机：只负责阶段、第一点缓存与边界规范化。
    /// 吸附、越界检查与撤销由编辑器 ViewModel 负责。
    /// </summary>
    public sealed class IsaPlacementSession
    {
        public IsaPlacementPhase Phase { get; private set; } = IsaPlacementPhase.Inactive;
        public int TargetSoflanGroupId { get; private set; }
        public TGrid FirstTGrid { get; private set; }
        public XGrid FirstXGrid { get; private set; }

        /// <summary>完成一个 ISA 后是否保持放置模式并继续等待下一个第一点。</summary>
        public bool Continuous { get; set; }

        public bool IsActive => Phase != IsaPlacementPhase.Inactive;

        public void Begin(int soflanGroupId)
        {
            TargetSoflanGroupId = soflanGroupId;
            FirstTGrid = null;
            FirstXGrid = null;
            Phase = IsaPlacementPhase.WaitingFirstPoint;
        }

        public void Cancel()
        {
            TargetSoflanGroupId = 0;
            FirstTGrid = null;
            FirstXGrid = null;
            Phase = IsaPlacementPhase.Inactive;
        }

        /// <summary>
        /// 收取一个吸附后的角点。第二点返回 true，并输出规范化后的起止坐标；
        /// 此时会话已自动回到 Inactive。
        /// </summary>
        public bool TryTakePoint(TGrid tGrid, XGrid xGrid, out IsaPlacementResult result)
        {
            result = null;
            if (!IsActive || tGrid is null || xGrid is null)
                return false;

            if (Phase == IsaPlacementPhase.WaitingFirstPoint)
            {
                FirstTGrid = tGrid.CopyNew();
                FirstXGrid = xGrid.CopyNew();
                Phase = IsaPlacementPhase.WaitingSecondPoint;
                return false;
            }

            var startT = FirstTGrid.TotalUnit <= tGrid.TotalUnit ? FirstTGrid : tGrid;
            var endT = ReferenceEquals(startT, FirstTGrid) ? tGrid : FirstTGrid;
            var startX = FirstXGrid.TotalUnit <= xGrid.TotalUnit ? FirstXGrid : xGrid;
            var endX = ReferenceEquals(startX, FirstXGrid) ? xGrid : FirstXGrid;

            result = new IsaPlacementResult(startT.CopyNew(), startX.CopyNew(), endT.CopyNew(), endX.CopyNew());
            if (Continuous)
            {
                FirstTGrid = null;
                FirstXGrid = null;
                Phase = IsaPlacementPhase.WaitingFirstPoint;
            }
            else
            {
                Cancel();
            }
            return true;
        }
    }
}
