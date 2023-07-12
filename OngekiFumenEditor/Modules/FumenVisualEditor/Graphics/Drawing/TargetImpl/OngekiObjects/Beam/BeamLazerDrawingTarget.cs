using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects.Beam
{
    [Export(typeof(IFumenEditorDrawingTarget))]
    internal class BeamLazerDrawingTarget : CommonDrawTargetBase<BeamStart>, IDisposable
    {
        private DefaultBeamLazerTextureDrawing lazerDrawing;
        public override IEnumerable<string> DrawTargetID { get; } = new[] { "BMS" };
        public override DrawingVisible DefaultVisible => DrawingVisible.Preview;

        public override int DefaultRenderOrder => 300;

        public BeamLazerDrawingTarget()
        {
            lazerDrawing = new();
        }

        public override void Draw(IFumenEditorDrawingContext target, BeamStart obj)
        {
            var width = 50 * obj.WidthId;

            var beginTGrid = obj.MinTGrid;
            var endTGrid = obj.MaxTGrid;

            var duration = endTGrid.TotalGrid - beginTGrid.TotalGrid;
            if (duration == 0)
                return;

            var curTGrid = target.Editor.GetCurrentTGrid();
            var leadInTGrid = TGridCalculator.ConvertAudioTimeToTGrid(TGridCalculator.ConvertTGridToAudioTime(beginTGrid, target.Editor) - TimeSpan.FromMilliseconds(BeamStart.LEAD_DURATION), target.Editor);
            var leadOutTGrid = TGridCalculator.ConvertAudioTimeToTGrid(TGridCalculator.ConvertTGridToAudioTime(endTGrid, target.Editor) + TimeSpan.FromMilliseconds(BeamStart.LEAD_DURATION), target.Editor);

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
            if (curTGrid < beginTGrid)
            {
                //progress = [-1,0]
                progress = MathUtils.Normalize(leadInTGrid.TotalGrid, beginTGrid.TotalGrid, curTGrid.TotalGrid) - 1;
                xGrid = obj.XGrid;
            }
            else if (curTGrid > endTGrid)
            {
                //progress = [1,2]
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
            var x = (float)XGridCalculator.ConvertXGridToX(xGrid, target.Editor);
            lazerDrawing.Draw(target, width, x, (float)progress, 0);
        }

        public void Dispose()
        {
            lazerDrawing?.Dispose();
            lazerDrawing = null;
        }
    }
}
