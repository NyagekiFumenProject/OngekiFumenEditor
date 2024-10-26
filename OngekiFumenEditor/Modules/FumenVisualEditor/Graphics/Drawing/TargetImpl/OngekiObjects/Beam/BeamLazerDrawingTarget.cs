using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Kernel.Graphics.Base;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Linq;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects.Beam
{
    [Export(typeof(IFumenEditorDrawingTarget))]
    internal class BeamLazerDrawingTarget : CommonDrawTargetBase<BeamStart>, IDisposable
    {
        private DefaultBeamLazerTextureDrawing lazerDrawing;
        private Texture textureBody;
        private Texture textureWarn;

        public override IEnumerable<string> DrawTargetID { get; } = new[] { "BMS", "OBS" };
        public override DrawingVisible DefaultVisible => DrawingVisible.Preview;

        public override int DefaultRenderOrder => 300;

        public BeamLazerDrawingTarget()
        {
            lazerDrawing = new();

            void load(ref Texture t, string name)
            {
                t = ResourceUtils.OpenReadTextureFromFile(@".\Resources\editor\" + name);
            }

            load(ref textureBody, "beamBody.png");
            textureBody.TextureWrapT = OpenTK.Graphics.OpenGL.TextureWrapMode.Repeat;

            load(ref textureWarn, "beamWarn.png");
            textureWarn.TextureWrapS = OpenTK.Graphics.OpenGL.TextureWrapMode.Repeat;
            textureWarn.TextureWrapT = OpenTK.Graphics.OpenGL.TextureWrapMode.Repeat;
        }

        public override void Draw(IFumenEditorDrawingContext target, BeamStart obj)
        {
            //todo 宽度目测的，需要精确计算
            var xGridWidth = XGridCalculator.CalculateXUnitSize(target.Editor.Setting.XGridDisplayMaxUnit, target.ViewWidth, target.Editor.Setting.XGridUnitSpace) / target.Editor.Setting.XGridUnitSpace;
            var width = xGridWidth * 3f * obj.WidthId;

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

                var currentY = target.ConvertToY(target.Editor.GetCurrentTGrid());
                var obliqueTopX = (float)XGridCalculator.ConvertXGridToX(curObliqueTopXGrid, target.Editor);
                var obliqueTopY = currentY - judgeOffset + target.ViewHeight;

                x = (obliqueTopX + currentX) / 2;

                rotate = (float)Math.Atan((currentX - obliqueTopX) / (obliqueTopY - currentY));
            }

            if (prepareWarn)
            {
                var audioTime = TGridCalculator.ConvertTGridToAudioTime(beginTGrid, target.Editor);
                var leadAudioTime = audioTime - TGridCalculator.ConvertFrameToAudioTime(BeamStart.LEAD_IN_DURATION_FRAME);
                var leadInTGrid = TGridCalculator.ConvertAudioTimeToTGrid(leadAudioTime, target.Editor);
                if (leadInTGrid is null)
                    leadInTGrid = TGrid.Zero;
                var warnProgress = MathUtils.Normalize(leadInTGrid.TotalGrid, beginTGrid.TotalGrid, curTGrid.TotalGrid);
                if (warnProgress < 0) warnProgress = -1;
                lazerDrawing.Draw(target, textureWarn, (int)width, x, (float)warnProgress, new(1, 215 / 255.0f, 0, 0.5f), rotate, judgeOffset);
            }

            lazerDrawing.Draw(target, textureBody, (int)width, x, (float)progress, OpenTK.Mathematics.Vector4.One, rotate, judgeOffset);
        }

        public void Dispose()
        {
            lazerDrawing?.Dispose();
            lazerDrawing = default;

            textureBody?.Dispose();
            textureBody = default;

            textureWarn?.Dispose();
            textureWarn = default;
        }
    }
}
