using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing
{
    public interface IPerfomenceMonitor
    {
        public interface IPerformenceData
        {
            public float AveSpendTicks { get; }
            public float MostSpendTicks { get; }
        }

        public interface IDrawingPerformenceData : IPerformenceData
        {
            public record PerformenceItem(string Name, float AveSpendTicks);
            public IEnumerable<PerformenceItem> PerformenceRanks { get; }
        }

        void OnBeforeRender();
        void OnBeginDrawing(IDrawing drawing);
        void OnBeginTargetDrawing(IDrawingTarget drawing);

        void CountDrawCall(IDrawing drawing);

        void OnAfterTargetDrawing(IDrawingTarget drawing);
        void OnAfterDrawing(IDrawing drawing);
        void OnAfterRender();

        IDrawingPerformenceData GetDrawingPerformenceData();
        IDrawingPerformenceData GetDrawingTargetPerformenceData();
        IPerformenceData GetRenderPerformenceData();

        void Clear();
    }
}
