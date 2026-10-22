using Gemini.Framework;

namespace OngekiFumenEditor.Modules.FumenSoflanGroupListViewer
{
    public interface IFumenSoflanGroupListViewer : ITool
    {
        /// <summary>当前选中的组 ID；选中 Category/无选中时为 null。</summary>
        int? CurrentSelectedSoflanGroupId { get; }

        /// <summary>当前在设计模式绘制变速线的组 ID（单选）。</summary>
        int? DrawnSoflanGroupId { get; }

        bool IsGroupVisible(int soflanGroup, bool designMode);
    }
}
