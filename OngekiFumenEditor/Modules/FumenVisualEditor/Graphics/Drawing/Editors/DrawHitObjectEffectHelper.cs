using OngekiFumenEditor.Base;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.Editors
{
    public class DrawHitObjectEffectHelper
    {
        private ICircleDrawing circleDrawing;
        private bool showHitEffect;

        public void Initalize(IRenderManagerImpl impl)
        {
            circleDrawing = impl.CircleDrawing;
            showHitEffect = Properties.EditorGlobalSetting.Default.ShowHitObjectEffectInPreviewMode;

            Properties.EditorGlobalSetting.Default.PropertyChanged += Default_PropertyChanged;
        }

        private void Default_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Properties.EditorGlobalSetting.ShowHitObjectEffectInPreviewMode))
                showHitEffect = Properties.EditorGlobalSetting.Default.ShowHitObjectEffectInPreviewMode;
        }

        public void Draw(IFumenEditorDrawingContext target)
        {
            if (!(target.Editor.IsPreviewMode && showHitEffect))
                return;

            var durationMs = 300;

            var maxTGrid = target.Editor.GetCurrentTGrid();

            var minAudioTime = target.Editor.CurrentPlayTime - TimeSpan.FromMilliseconds(durationMs);
            var minTGrid = TGridCalculator.ConvertAudioTimeToTGrid(minAudioTime, target.Editor);
            if (minTGrid is null)
                minTGrid = TGrid.Zero;
            var durationTotalGrid = maxTGrid.TotalGrid - minTGrid.TotalGrid;

            var y = (float)target.ConvertToY_DefaultSoflanGroup(maxTGrid);

            void drawColorCircle(float progress, Vector2 pos, Vector4 solidColor, float radius)
            {
                progress = Math.Clamp(progress, 0, 1);

                var circleScale = (float)Interpolation.EasingValue(progress, 0, 1, EasingTypes.OutExpo) * radius;

                var shortProgress = Interpolation.EasingValue(progress, 0, 0.65, 0, 1);
                var soliderCircleAlpha = (float)Interpolation.EasingValue(shortProgress, 1, 0, EasingTypes.In);

                var hollowCircleAlpha = (float)Interpolation.EasingValue(progress, 1, 0, EasingTypes.InQuart);

                var solderColor = new Vector4(solidColor.X, solidColor.Y, solidColor.Z, soliderCircleAlpha);
                var hollowColor = new Vector4(solidColor.X, solidColor.Y, solidColor.Z, hollowCircleAlpha);

                circleDrawing.Post(pos, solderColor, true, circleScale);
                circleDrawing.Post(pos, hollowColor, false, circleScale, 2);
            }

            var hitObjects = Enumerable.Empty<OngekiMovableObjectBase>()
            .Concat(target.Editor.Fumen.Flicks.BinaryFindRange(minTGrid, maxTGrid))
            .Concat(target.Editor.Fumen.Taps.BinaryFindRange(minTGrid, maxTGrid))
            .Concat(target.Editor.Fumen.Holds.GetVisibleStartObjects(minTGrid, maxTGrid));

            circleDrawing.Begin(target);
            foreach (var hit in hitObjects)
            {
                var x = (float)XGridCalculator.ConvertXGridToX(hit.XGrid, target.Editor);
                var p = new Vector2(x, y);

                var tGrid = hit.TGrid;
                var progress = (maxTGrid.TotalGrid * 1.0f - tGrid.TotalGrid) / durationTotalGrid;

                drawColorCircle(progress, p, Vector4.One, 20);
            }
            circleDrawing.End();


            var bellObjects = target.Editor.Fumen.Bells.BinaryFindRange(minTGrid, maxTGrid);

            circleDrawing.Begin(target);
            foreach (var bell in bellObjects)
            {
                var x = (float)XGridCalculator.ConvertXGridToX(bell.XGrid, target.Editor);
                var p = new Vector2(x, y);

                var tGrid = bell.TGrid;
                var progress = (maxTGrid.TotalGrid * 1.0f - tGrid.TotalGrid) / durationTotalGrid;

                drawColorCircle(progress, p, new(1, 1, 0, 0), 15);
            }
            circleDrawing.End();


            var bulletObjects = target.Editor.Fumen.Bullets.BinaryFindRange(minTGrid, maxTGrid);

            circleDrawing.Begin(target);
            foreach (var bullet in bulletObjects)
            {
                var x = (float)XGridCalculator.ConvertXGridToX(bullet.XGrid, target.Editor);
                var p = new Vector2(x, y);

                var tGrid = bullet.TGrid;
                var progress = (maxTGrid.TotalGrid * 1.0f - tGrid.TotalGrid) / durationTotalGrid;

                drawColorCircle(progress, p, new(0.5f, 0, 1, 0), 10);
            }
            circleDrawing.End();

        }
    }
}
