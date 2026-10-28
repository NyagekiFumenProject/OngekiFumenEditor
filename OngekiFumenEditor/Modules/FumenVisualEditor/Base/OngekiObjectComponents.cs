using System.Collections.Generic;
using OngekiFumenEditor.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;

namespace OngekiFumenEditor.Base
{
    public static class OngekiObjectComponents
    {
        // 枚举对象自身及其组成部件：持续物件的尾节点、轨道的子节点和曲线控制点。
        // 只展开一层；子节点/控制点单独作为根时保持“只返回自己”，避免反向带出整条轨道。
        public static IEnumerable<OngekiObjectBase> EnumerateSelfAndComponents(this OngekiObjectBase obj)
        {
            if (obj is not OngekiTimelineObjectBase timeline)
            {
                yield return obj;
                yield break;
            }

            if (obj is ConnectableChildObjectBase or LaneCurvePathControlObject)
            {
                yield return obj;
                yield break;
            }

            foreach (var displayable in timeline.GetDisplayableObjects())
                if (displayable is OngekiObjectBase component)
                    yield return component;
        }

        // 尾节点 → 所属头对象；头对象或无关对象返回 null。
        public static OngekiTimelineObjectBase GetHeadObject(this OngekiObjectBase obj) => obj switch
        {
            HoldEnd end => end.RefHold,
            Soflan.SoflanEndIndicator end => end.RefSoflan,
            LaneBlockArea.LaneBlockAreaEndIndicator end => end.RefLaneBlockArea,
            IndividualSoflanArea.IndividualSoflanAreaEndIndicator end => end.RefIndividualSoflanArea,
            _ => null
        };

        // 头对象 → 尾节点；无尾或非持续物件返回 null。
        public static OngekiTimelineObjectBase GetTailObject(this OngekiObjectBase obj) => obj switch
        {
            Hold hold => hold.HoldEnd,
            Soflan soflan => soflan.EndIndicator,
            LaneBlockArea block => block.EndIndicator,
            IndividualSoflanArea area => area.EndIndicator,
            _ => null
        };

        // 轨道子节点/曲线控制点 → 所属轨道起点；其他对象返回 null。
        public static ConnectableStartObject GetLaneStartObject(this OngekiObjectBase obj) => obj switch
        {
            ConnectableObjectBase connectable => connectable.ReferenceStartObject,
            LaneCurvePathControlObject control => control.RefCurveObject?.ReferenceStartObject,
            _ => null
        };
    }
}
