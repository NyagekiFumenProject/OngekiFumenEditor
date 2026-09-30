using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using System;
using System.Linq;

namespace OngekiFumenEditor.Kernel.Mcp.Tools.Editor
{
    /// <summary>
    /// lane / beam 这类「可连接物件」是「起点 + 延伸段链」的结构：
    /// <c>fumen.Lanes</c> / <c>fumen.Beams</c> 只枚举起点，延伸段在起点的 <c>Children</c> 里，
    /// 曲线控制点又挂在每个延伸段上。MCP 用三个独立族把它们拆开暴露（lane / lanenext / curvecontrol，
    /// beam / beamnext），本文件放这层结构上共用的解析与增删逻辑。
    /// </summary>
    internal sealed partial class EditorMutationTool
    {
        /// <summary>
        /// 延伸段改过 TGrid 之后，起点内部的 children 必须重新按 TGrid 排序：
        /// children 的顺序同时决定了 PrevObject/NextObject 链，顺序错了路径就会算歪。
        /// </summary>
        private static void ReorderConnectableChild(OngekiObjectBase obj)
        {
            if (obj is not ConnectableChildObjectBase child)
                return;

            var parent = child.ReferenceStartObject;
            if (parent is null)
                return;

            // InsertChildObject 对已在 children 里的对象是空操作，必须先摘掉再按 TGrid 重新插入。
            parent.RemoveChildObject(child);
            parent.InsertChildObject(child.TGrid, child);
        }

        /// <summary>按 RecordId 找 lane 起点（lanenext 的 parentRecordId 就取自这里）。</summary>
        private static LaneStartBase ResolveLaneStartByRecordId(OngekiFumen fumen, int recordId)
            => fumen.Lanes.FirstOrDefault(x => x.RecordId == recordId)
                ?? throw new ArgumentException($"No lane start with RecordId {recordId} in the editor. List them with editor.query_object objectType='lane'.");

        /// <summary>按 RecordId 找 beam 起点（beamnext 的 parentRecordId 就取自这里）。</summary>
        private static BeamStart ResolveBeamStartByRecordId(OngekiFumen fumen, int recordId)
            => fumen.Beams.FirstOrDefault(x => x.RecordId == recordId)
                ?? throw new ArgumentException($"No beam start with RecordId {recordId} in the editor. List them with editor.query_object objectType='beam'.");

        /// <summary>按对象 id 找 lane 的一段延伸（curvecontrol 的 referenceObjectId 就取自这里）。</summary>
        private static ConnectableChildObjectBase ResolveLaneSegmentByObjectId(OngekiFumen fumen, int objectId)
            => fumen.Lanes.SelectMany(x => x.Children).FirstOrDefault(x => x.Id == objectId)
                ?? throw new ArgumentException($"No lane segment with object id {objectId} in the editor. List them with editor.query_object objectType='lanenext'.");

        /// <summary>
        /// <c>OngekiFumen.AddObject</c> 不认识曲线控制点（只有 <c>RemoveObject</c> 认识），
        /// 所以曲线控制点的增删必须走所在延伸段。
        /// 另外 <c>RemoveControlObject</c> 会清掉 <c>RefCurveObject</c>，因此 redo/undo
        /// 必须用捕获下来的 owner，不能回头再读 <c>RefCurveObject</c> —— 这也是
        /// <paramref name="curveOwnerHint"/> 存在的原因（刚 new 出来的控制点还没绑定过任何延伸段）。
        /// </summary>
        private static Action BuildAddObject(OngekiFumen fumen, OngekiObjectBase obj, ConnectableChildObjectBase curveOwnerHint = default)
        {
            if (obj is LaneCurvePathControlObject control)
            {
                var owner = curveOwnerHint ?? control.RefCurveObject
                    ?? throw new ArgumentException($"Curve path control #{control.Id} is not attached to a lane segment.");
                return () => owner.AddControlObject(control);
            }

            // fumen 的 AddObject 对延伸段是「追加到链尾」，撤销后再重做会把它挪到最后一段之后；
            // 补一次按 TGrid 重新插入，保证链序与原状一致。
            return () =>
            {
                fumen.AddObject(obj);
                ReorderConnectableChild(obj);
            };
        }

        private static Action BuildRemoveObject(OngekiFumen fumen, OngekiObjectBase obj)
        {
            if (obj is LaneCurvePathControlObject control)
                return () => control.RefCurveObject?.RemoveControlObject(control);

            return () => fumen.RemoveObject(obj);
        }
    }
}
