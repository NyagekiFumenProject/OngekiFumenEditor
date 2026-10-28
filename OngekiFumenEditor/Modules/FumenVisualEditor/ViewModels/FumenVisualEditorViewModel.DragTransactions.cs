using System;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels
{
    public partial class FumenVisualEditorViewModel
    {
        private int gridDragUpdateDepth;
        private bool gridDragMetricsPending;

        internal bool IsGridDragTransactionActive => gridDragUpdateDepth > 0;

        internal void ExecuteGridDragUpdate(Action update)
        {
            gridDragUpdateDepth++;
            try
            {
                update();
            }
            finally
            {
                if (--gridDragUpdateDepth == 0 && gridDragMetricsPending)
                {
                    gridDragMetricsPending = false;
                    RecalculateScrollMetrics();
                }
            }
        }
    }
}
