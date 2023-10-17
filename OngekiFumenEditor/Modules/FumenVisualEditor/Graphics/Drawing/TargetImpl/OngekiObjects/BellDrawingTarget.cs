using Caliburn.Micro;
using FontStashSharp;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Kernel.Graphics.Base;
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
    [Export(typeof(IFumenEditorDrawingTarget))]
    public class BellDrawingTarget : CommonBatchDrawTargetBase<Bell>, IDisposable
    {
        public override int DefaultRenderOrder => 1200;
        private SoflanList nonSoflanList = new(new[] { new Soflan() { TGrid = TGrid.Zero, Speed = 1 } });

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

        public void PostDrawEditor(IFumenEditorDrawingContext target, Bell obj)
        {
            var toTime = target.ConvertToY(obj.TGrid);
            var toX = XGridCalculator.ConvertXGridToX(obj.XGrid.TotalUnit, target.Editor);

            var pos = new Vector2((float)toX, (float)toTime);
            normalFlichList.Add((size, pos, 0f));
            if (obj.IsSelected)
                selectedFlickList.Add((selectSize, pos, 0f));
            DrawPallateStr(target, obj, pos);
            target.RegisterSelectableObject(obj, pos, size);
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

            double convertToYNonSoflan(TGrid tgrid)
            {
                return TGridCalculator.ConvertTGridToY_DesignMode(
                    tgrid,
                    nonSoflanList,
                    target.Editor.Fumen.BpmList,
                    target.Editor.Setting.VerticalDisplayScale,
                    target.Editor.Setting.TGridUnitLength); ;
            }

            var fromX = XGridCalculator.ConvertXGridToX(obj.ReferenceBulletPallete?.CalculateFromXGridTotalUnit(obj, target.Editor.Fumen) ?? obj.XGrid.TotalUnit, target.Editor);
            var toX = XGridCalculator.ConvertXGridToX(obj.ReferenceBulletPallete?.CalculateToXGridTotalUnit(obj, target.Editor.Fumen) ?? obj.XGrid.TotalUnit, target.Editor);

            //计算向量化的物件运动时间
            var timeX = 0d;
            var timeY = 0d;

            if (!(obj.ReferenceBulletPallete?.IsEnableSoflan ?? true))
            {
                var toTime = convertToYNonSoflan(obj.TGrid);
                var fromTime = toTime - appearOffsetTime;
                var currentTime = convertToYNonSoflan(TGridCalculator.ConvertAudioTimeToTGrid(target.CurrentPlayTime, target.Editor));

                var precent = (currentTime - fromTime) / appearOffsetTime;

                timeX = MathUtils.CalculateXFromTwoPointFormFormula(currentTime, fromX, fromTime, toX, toTime);
                timeY = Math.Min(target.Rect.MinY, target.Rect.MaxY) + target.Rect.Height * (1 - precent) + target.Editor.Setting.JudgeLineOffsetY;
            }
            else
            {
                var toTime = target.ConvertToY(obj.TGrid);
                var fromTime = toTime - appearOffsetTime;
                var currentTime = target.ConvertToY(TGridCalculator.ConvertAudioTimeToTGrid(target.CurrentPlayTime, target.Editor));
                var precent = (currentTime - fromTime) / appearOffsetTime;

                //Log.LogDebug($"precent : {precent * 100:F2}");

                timeX = MathUtils.CalculateXFromTwoPointFormFormula(currentTime, fromX, fromTime, toX, toTime);
                timeY = target.Rect.MinY + target.Rect.Height * (1 - precent) + target.Editor.Setting.JudgeLineOffsetY;
            }

            var pos = new Vector2((float)timeX, (float)timeY);
            normalFlichList.Add((size, pos, 0f));
            if (obj.IsSelected)
                selectedFlickList.Add((selectSize, pos, 0f));
            target.RegisterSelectableObject(obj, pos, size);
        }

        public override void DrawBatch(IFumenEditorDrawingContext target, IEnumerable<Bell> objs)
        {
            if (target.Editor.IsDesignMode)
            {
                foreach (var obj in objs)
                    PostDrawEditor(target, obj);
            }
            else
            {
                foreach (var obj in objs)
                    PostDraw(target, obj);
            }

            highlightDrawing.Draw(target, texture, selectedFlickList);
            textureDrawing.Draw(target, texture, normalFlichList);

            normalFlichList.Clear();
            selectedFlickList.Clear();
        }

        private void DrawPallateStr(IDrawingContext target, IBulletPalleteReferencable obj, Vector2 pos)
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
