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

            var hitObjects = Enumerable.Empty<OngekiMovableObjectBase>()
            .Concat(target.Editor.Fumen.Flicks.BinaryFindRange(minTGrid, maxTGrid))
            .Concat(target.Editor.Fumen.Taps.BinaryFindRange(minTGrid, maxTGrid))
            .Concat(target.Editor.Fumen.Holds.GetVisibleStartObjects(minTGrid, maxTGrid));

            var y = (float)target.ConvertToY_DefaultSoflanGroup(maxTGrid);

            circleDrawing.Begin(target);

            foreach (var obj in hitObjects)
            {
                var x = (float)XGridCalculator.ConvertXGridToX(obj.XGrid, target.Editor);
                var p = new Vector2(x, y);

                var tGrid = obj.TGrid;
                var progress = (maxTGrid.TotalGrid * 1.0f - tGrid.TotalGrid) / durationTotalGrid;
                progress = Math.Clamp(progress, 0, 1);

                var circleScale = (float)Interpolation.EasingValue(progress, 0, 1, EasingTypes.OutExpo) * 20f;

                var shortProgress = Interpolation.EasingValue(progress, 0, 0.65, 0, 1);
                var soliderCircleAlpha = (float)Interpolation.EasingValue(shortProgress, 1, 0, EasingTypes.In);

                var hollowCircleAlpha = (float)Interpolation.EasingValue(progress, 1, 0, EasingTypes.InQuart);

                circleDrawing.Post(p, new(1, 1, 1, soliderCircleAlpha), true, circleScale);
                circleDrawing.Post(p, new(1, 1, 1, hollowCircleAlpha), false, circleScale, 2);
            }
            circleDrawing.End();
        }
    }
}
