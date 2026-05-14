using OngekiFumenEditor.Core.Base;

namespace OngekiFumenEditor.Core.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl
{
    public class NavigateToObjectBehavior : INavigateBehavior
    {
        private readonly OngekiTimelineObjectBase ongekiObject;

        public NavigateToObjectBehavior(OngekiTimelineObjectBase ongekiObject)
        {
            this.ongekiObject = ongekiObject;
        }

        public void Navigate(IFumenCheckContext editor)
        {
            editor?.ScrollTo(ongekiObject);
            editor?.NotifyObjectClicked(ongekiObject);
        }
    }
}
