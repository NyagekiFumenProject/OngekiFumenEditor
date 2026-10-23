using Gemini.Framework;
using OngekiFumenEditor.Modules.FumenSoflanGroupListViewer.Models;

namespace OngekiFumenEditor.Modules.FumenSoflanGroupListViewer
{
    public interface IFumenSoflanGroupListViewer : ITool
    {
        /// <summary>当前选中的组 ID；选中 Category/无选中时为 null。</summary>
        int? CurrentSelectedSoflanGroupId { get; }

        /// <summary>当前在设计模式绘制变速线的组 ID（单选）。</summary>
        int? DrawnSoflanGroupId { get; }

        /// <summary>当前是否处于 ISA 两点放置模式。</summary>
        bool IsIsaPlacementEnabled { get; }

        bool IsGroupVisible(int soflanGroup, bool designMode);

        /// <summary>分配下一个组 ID，会避开已删除但仍在撤销栈中的组编号。</summary>
        int GetNextSoflanGroupId();

        /// <summary>用默认组名快速新建一个组（可撤销），并返回新组节点。</summary>
        SoflanGroupNode CreateGroupWithDefaultName(int id, SoflanGroupCategoryNode parent);
    }
}
