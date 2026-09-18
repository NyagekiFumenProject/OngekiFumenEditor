using OngekiFumenEditor.Base;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer.Base
{
    public interface IFumenCheckContext
    {
        void ScrollTo(TGrid tGrid);
        void ScrollTo(OngekiTimelineObjectBase ongekiObject);
        void NotifyObjectClicked(OngekiTimelineObjectBase ongekiObject);
        void ShowFumenMetaInfo();
    }
}
