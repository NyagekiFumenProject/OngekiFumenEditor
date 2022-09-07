using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.Base;
using OngekiFumenEditor.Modules.FumenPreviewer.Graphics.PrimitiveValue;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.CommonLinesDrawTargetBase<OngekiFumenEditor.Base.OngekiObjects.Lane.Base.LaneStartBase>;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.TargetImpl
{
    [Export(typeof(IDrawingTarget))]
    public class HoldDrawingTarget : CommonDrawTargetBase<Hold>
    {
        public override IEnumerable<string> DrawTargetID { get; } = new string[] { "HLD", "CHD", "XHD" };

        private class HoldLinesDrawHelper : CommonLinesDrawTargetBase<LaneStartBase>
        {
            public override IEnumerable<string> DrawTargetID => default;
            public override void FillLine(List<LinePoint> list, LaneStartBase obj, OngekiFumen fumen)
            { }
        }

        private HoldLinesDrawHelper lineDraw = new();
        private TapDrawingTarget tapDraw;

        public HoldDrawingTarget() : base()
        {
            tapDraw = IoC.Get<TapDrawingTarget>();
        }

        public override void Draw(Hold hold, OngekiFumen fumen)
        {
            if (hold.Children.FirstOrDefault() is not HoldEnd holdEnd || hold.ReferenceLaneStart is not LaneStartBase start)
                return;

            var color = start.LaneType switch
            {
                LaneType.Left => new Vector4(1, 0, 0, 0.75f),
                LaneType.Center => new Vector4(0, 1, 0, 0.75f),
                LaneType.Right => new Vector4(0, 0, 1, 0.75f),
                LaneType.WallLeft => new Vector4(35 / 255.0f, 4 / 255.0f, 117 / 255.0f, 0.75f),
                LaneType.WallRight => new Vector4(136 / 255.0f, 3 / 255.0f, 152 / 255.0f, 0.75f),
                _ => default,
            };

            //draw line
            using var d = ObjectPool<List<LinePoint>>.GetWithUsingDisposable(out var list, out _);
            list.Clear();

            void Upsert<T>(T obj) where T : IHorizonPositionObject, ITimelineObject
            {
                var y = (float)TGridCalculator.ConvertTGridToY(obj.TGrid, fumen.BpmList, 1.0, 240);
                var x = (float)XGridCalculator.ConvertXGridToX(obj.XGrid, 30, Previewer.ViewWidth, 1);
                list.Add(new(new(x, y), color));
            }

            Upsert(hold);
            foreach (var node in start.Children.AsEnumerable<ConnectableObjectBase>().Prepend(start).Where(x => hold.TGrid <= x.TGrid && x.TGrid <= holdEnd.TGrid))
                Upsert(node);
            Upsert(holdEnd);

            lineDraw.BeginDraw(Previewer);
            lineDraw.Draw(list, 13);
            lineDraw.EndDraw();

            //draw taps
            tapDraw.BeginDraw(Previewer);
            tapDraw.Draw(start.LaneType, hold.TGrid, hold.XGrid, fumen);
            tapDraw.Draw(start.LaneType, holdEnd.TGrid, holdEnd.XGrid, fumen);
            tapDraw.EndDraw();
        }
    }
}
