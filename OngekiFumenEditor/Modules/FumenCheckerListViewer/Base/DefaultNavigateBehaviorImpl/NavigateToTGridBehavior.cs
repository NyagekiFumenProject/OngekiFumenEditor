using OngekiFumenEditor.Base;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl
{
    public class NavigateToTGridBehavior : INavigateBehavior
    {
        public NavigateToTGridBehavior(TGrid tGrid)
        {
            TargetTGrid = tGrid;
        }

        /// <summary>该检查结果指向的谱面位置；MCP 的 editor.check 用它回报 tGrid。</summary>
        public TGrid TargetTGrid { get; }

        public void Navigate(IFumenCheckContext editor)
        {
            editor?.ScrollTo(TargetTGrid);
        }
    }
}
