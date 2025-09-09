using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Linq;
using System.Numerics;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects.Beam
{
    [Export(typeof(IFumenEditorDrawingTarget))]
    internal class BeamLazerDrawingTarget : CommonDrawTargetBase<BeamStart>, IDisposable
    {
        private IBeamDrawing lazerDrawing;
        private IImage textureBody;
        private IImage pixelImg;
        private IImage textureWarn;

        public override IEnumerable<string> DrawTargetID { get; } = new[] { "BMS", "OBS" };
        public override DrawingVisible DefaultVisible => DrawingVisible.Preview;

        public override int DefaultRenderOrder => 300;

        public override void Draw(IFumenEditorDrawingContext target, BeamStart obj)
        {
            var xGridWidth = XGridCalculator.CalculateXUnitSize(target.Editor.Setting.XGridDisplayMaxUnit, target.CurrentDrawingTargetContext.Rect.Width, target.Editor.Setting.XGridUnitSpace) / target.Editor.Setting.XGridUnitSpace;
            //var width = xGridWidth * 3f * obj.WidthId.Id;
            var width = xGridWidth * obj.WidthId.WidthDraw;

            var beginTGrid = obj.MinTGrid;
            var endTGrid = obj.MaxTGrid;

            var duration = endTGrid.TotalGrid - beginTGrid.TotalGrid;
            if (duration == 0)
                return;

            var curTGrid = target.Editor.GetCurrentTGrid();

            /* ^  -- leadOutTGrid
             * |  |
             * |  |   progress = [1,2]
             * |  |
             *    -- endTGrid
             *    |
             *    |
             *    |   progress = [0,1]
             *    |
             *    |
             *    -- beginTGrid
             *    |
             *    |   progress = [-1,0]
             *    |
             *    --leadInTGrid
             */

            double progress;
            XGrid xGrid;
            bool prepareWarn = false;

            if (curTGrid < beginTGrid)
            {
                //progress = [-1,0]
                var leadBodyInTGrid = TGridCalculator.ConvertAudioTimeToTGrid(TGridCalculator.ConvertTGridToAudioTime(beginTGrid, target.Editor) - TimeSpan.FromMilliseconds(BeamStart.LEAD_IN_BODY_DURATION), target.Editor);
                progress = MathUtils.Normalize(leadBodyInTGrid.TotalGrid, beginTGrid.TotalGrid, curTGrid.TotalGrid) - 1;
                xGrid = obj.XGrid;

                prepareWarn = true;
            }
            else if (curTGrid > endTGrid)
            {
                //progress = [1,2]
                var leadOutTGrid = TGridCalculator.ConvertAudioTimeToTGrid(TGridCalculator.ConvertTGridToAudioTime(endTGrid, target.Editor) + TimeSpan.FromMilliseconds(BeamStart.LEAD_OUT_DURATION), target.Editor);
                progress = MathUtils.Normalize(endTGrid.TotalGrid, leadOutTGrid.TotalGrid, curTGrid.TotalGrid) + 1;
                xGrid = obj.Children.LastOrDefault()?.XGrid;
            }
            else
            {
                //progress = [0,1]
                progress = MathUtils.Normalize(beginTGrid.TotalGrid, endTGrid.TotalGrid, curTGrid.TotalGrid);
                xGrid = obj.CalulateXGrid(curTGrid);
            }

            if (xGrid is null)
                return;
            var currentX = (float)XGridCalculator.ConvertXGridToX(xGrid, target.Editor);

            var rotate = 0f;
            var x = currentX;
            var judgeOffset = (float)target.Editor.Setting.JudgeLineOffsetY;

            if (obj.ObliqueSourceXGridOffset is not null)
            {
                //It's oblique beam.
                IBeamObject curBeamObj = obj.Children.LastOrDefault(x => curTGrid > x.TGrid) as IBeamObject ?? obj;
                var curObliqueTopXGrid = obj.XGrid.TotalUnit + curBeamObj.ObliqueSourceXGridOffset.TotalUnit;

                //beam not support SoflanGroup
                var currentY = target.ConvertToY_DefaultSoflanGroup(target.Editor.GetCurrentTGrid());
                var obliqueTopX = (float)XGridCalculator.ConvertXGridToX(curObliqueTopXGrid, target.Editor);
                var obliqueTopY = currentY - judgeOffset + target.CurrentDrawingTargetContext.Rect.Height;

                x = (obliqueTopX + currentX) / 2;

                rotate = (float)Math.Atan((currentX - obliqueTopX) / (obliqueTopY - currentY));
            }

            var warnProgress = 0f;

            if (prepareWarn)
            {
                var audioTime = TGridCalculator.ConvertTGridToAudioTime(beginTGrid, target.Editor);
                var leadAudioTime = audioTime - TGridCalculator.ConvertFrameToAudioTime(BeamStart.LEAD_IN_DURATION_FRAME);
                var leadInTGrid = TGridCalculator.ConvertAudioTimeToTGrid(leadAudioTime, target.Editor);
                if (leadInTGrid is null)
                    leadInTGrid = TGrid.Zero;

                warnProgress = (float)MathUtils.Normalize(leadInTGrid.TotalGrid, beginTGrid.TotalGrid, curTGrid.TotalGrid);
                var a = MathUtils.SmoothStep(0.0f, 0.25f, warnProgress);
                var warnColor = new OpenTK.Mathematics.Vector4(1, 215 / 255.0f, 0, 0.5f * a);

                lazerDrawing.Draw(target, pixelImg, (int)width, x, (float)warnProgress, warnColor, rotate, judgeOffset);
            }

            lazerDrawing.Draw(target, textureBody, (int)width, x, (float)progress, OpenTK.Mathematics.Vector4.One, rotate, judgeOffset);
            //Log.LogDebug($"a\nx:{x:F2}, progress:{progress:F2}, warnProgress:{warnProgress:F2}, rotate:{rotate:F2}");
        }

        public void Dispose()
        {
            textureBody?.Dispose();
            textureBody = default;

            textureWarn?.Dispose();
            textureWarn = default;

            pixelImg?.Dispose();
            pixelImg = default;
        }

        public override void Initialize(IRenderManagerImpl impl)
        {
            lazerDrawing = impl.BeamDrawing;

            IImage load(string name) => ResourceUtils.OpenReadTextureFromFile(impl, @".\Resources\editor\" + name);

            textureBody = load("beamBody.png");
            textureBody.TextureWrapT = TextureWrapMode.Repeat;

            pixelImg = load("pixel.png");

            textureWarn = load("beamWarn.png");
            textureWarn.TextureWrapS = TextureWrapMode.Repeat;
            textureWarn.TextureWrapT = TextureWrapMode.Repeat;
        }
    }
}
