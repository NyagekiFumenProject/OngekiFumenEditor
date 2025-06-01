using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using System.Collections.Generic;
using System.Linq;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl
{
    public abstract class LaneDrawingTargetBase<T> : CommonLinesDrawTargetBase<T> where T : ConnectableStartObject
    {
        public override int DefaultRenderOrder => 100;
    }

    public abstract class LaneDrawingTargetBase : LaneDrawingTargetBase<LaneStartBase>
    {
        public override void DrawBatch(IFumenEditorDrawingContext target, IEnumerable<LaneStartBase> starts)
        {
            base.DrawBatch(target, starts.Where(x => !x.IsTransparent));
        }
    }
}
