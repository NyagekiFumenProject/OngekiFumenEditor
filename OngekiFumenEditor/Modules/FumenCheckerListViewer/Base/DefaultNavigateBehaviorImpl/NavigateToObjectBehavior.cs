using OngekiFumenEditor.Base;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl
{
    public class NavigateToObjectBehavior : INavigateBehavior
    {
        public NavigateToObjectBehavior(OngekiTimelineObjectBase ongekiObject)
        {
            OngekiObject = ongekiObject;
        }

        /// <summary>该检查结果指向的谱面对象；MCP 的 editor.check 用它回报 objectId / tGrid。</summary>
        public OngekiTimelineObjectBase OngekiObject { get; }

        public void Navigate(IFumenCheckContext editor)
        {
            editor?.ScrollTo(OngekiObject);
            editor?.NotifyObjectClicked(OngekiObject);
        }
    }
}
