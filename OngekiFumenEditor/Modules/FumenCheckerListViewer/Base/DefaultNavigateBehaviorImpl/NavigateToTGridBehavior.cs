using OngekiFumenEditor.Base;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl
{
    public class NavigateToTGridBehavior : INavigateBehavior
    {
        private readonly TGrid tGrid;

        public NavigateToTGridBehavior(TGrid tGrid)
        {
            this.tGrid = tGrid;
        }

        public void Navigate(IFumenCheckContext editor)
        {
            editor?.ScrollTo(tGrid);
        }
    }
}
