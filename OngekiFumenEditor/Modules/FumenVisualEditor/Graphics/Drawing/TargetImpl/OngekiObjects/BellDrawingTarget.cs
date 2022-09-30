using Caliburn.Micro;
using FontStashSharp;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Vector2 = System.Numerics.Vector2;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects
{
    [Export(typeof(IDrawingTarget))]
    public class BellDrawingTarget : CommonBatchDrawTargetBase<Bell>, IDisposable
    {
        public override int DefaultRenderOrder => 1200;

        private Texture texture;
        private Vector2 size;
        private Vector2 selectSize;

        private IStringDrawing stringDrawing;
        private IBatchTextureDrawing textureDrawing;
        private IHighlightBatchTextureDrawing highlightDrawing;

        private List<(Vector2, Vector2, float)> selectedFlickList = new();
        private List<(Vector2, Vector2, float)> normalFlichList = new();

        public override IEnumerable<string> DrawTargetID { get; } = new[] { Bell.CommandName };

        public BellDrawingTarget() : base()
        {
            texture = ResourceUtils.OpenReadTextureFromResource(@"Modules\FumenVisualEditor\Views\OngekiObjects\bell.png");
            size = new(40, 40);
            selectSize = new(50, 50);
            stringDrawing = IoC.Get<IStringDrawing>();
            textureDrawing = IoC.Get<IBatchTextureDrawing>();
            highlightDrawing = IoC.Get<IHighlightBatchTextureDrawing>();
        }

        public float CalculateBulletMsecTime(IFumenEditorDrawingContext target, Bell obj, float userSpeed = 2.35f)
        {
            //const float fat = 3.95f;
            //var time =  32.5f * fat / (Math.Max(4.7f, 0.2f * userSpeed) * (/*obj.ReferenceBulletPallete?.Speed ??*/ 1f)) * 16.666666f;
            var time = (float)target.ViewHeight / (obj.ReferenceBulletPallete?.Speed ?? 1f);
            return time;
        }

        public void PostDraw(IFumenEditorDrawingContext target, Bell obj)
        {
            /*
            --------------------------- toTime 
                    \
                     \
                      \
                       \
                        \
                         O      <- currentTime
                          bell
                           \
                            \
                             \
                              \
                               \
            ---------------------------- fromTime = toTime - appearOffsetTime
            */
            var appearOffsetTime = CalculateBulletMsecTime(target, obj);

            var toTime = TGridCalculator.ConvertTGridToY(obj.TGrid, target.Editor.Fumen.BpmList, 1, 240);
            var fromTime = toTime - appearOffsetTime;

            if (target.Rect.MinY < fromTime)
                return;

            var fromX = XGridCalculator.ConvertXGridToX(obj.ReferenceBulletPallete?.CalculateFromXGrid(obj.XGrid.TotalUnit, target.Editor.Fumen) ?? obj.XGrid.TotalUnit, 30, target.ViewWidth, 1);
            var toX = XGridCalculator.ConvertXGridToX(obj.ReferenceBulletPallete?.CalculateToXGrid(obj.XGrid.TotalUnit, target.Editor.Fumen) ?? obj.XGrid.TotalUnit, 30, target.ViewWidth, 1);

            var currentTime = MathUtils.Limit(target.CurrentPlayTime, toTime, fromTime);

            var precent = (currentTime - fromTime) / appearOffsetTime;

            var timeX = MathUtils.CalculateXFromTwoPointFormFormula(currentTime, fromX, fromTime, toX, toTime);

            timeX = MathUtils.Limit(timeX, fromX, toX);
            var timeY = target.Rect.MinY + target.Rect.Height * (1 - precent);

            var pos = new Vector2((float)timeX, (float)timeY);
            normalFlichList.Add((size, pos, 0f));
            if (obj.IsSelected)
                selectedFlickList.Add((selectSize, pos, 0f));
            DrawPallateStr(target, obj, pos);
            target.RegisterSelectableObject(obj, pos, size);
        }

        public override void DrawBatch(IFumenEditorDrawingContext target, IEnumerable<Bell> objs)
        {
            foreach (var obj in objs)
                PostDraw(target, obj);
            highlightDrawing.Draw(target, texture, selectedFlickList);
            textureDrawing.Draw(target, texture, normalFlichList);

            normalFlichList.Clear();
            selectedFlickList.Clear();
        }

        private void DrawPallateStr(IFumenEditorDrawingContext target, IBulletPalleteReferencable obj, Vector2 pos)
        {
            if (obj.ReferenceBulletPallete is null)
                return;
            stringDrawing.Draw($"{obj.ReferenceBulletPallete.StrID}", new(pos.X, pos.Y + 5), Vector2.One, 16, 0, new(1, 0, 0, 1), new(0.5f, 0.5f), IStringDrawing.StringStyle.Normal, target, default, out _);
        }

        public void Dispose()
        {
            texture?.Dispose();
            texture = null;
        }
    }
}
