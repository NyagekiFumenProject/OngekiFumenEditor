using Caliburn.Micro;
using OngekiFumenEditor.Base;
using System;
using System.Collections.Generic;
using System.Drawing.Text;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ViewModels.Dialog
{
    public class BrushTGridRangeDialogViewModel : Screen
    {
        private TGrid beginTGrid = new TGrid();
        private TGrid endTGrid = new TGrid();

        public TGrid BeginTGrid
        {
            get => beginTGrid;
            set => Set(ref beginTGrid, value);
        }

        public TGrid EndTGrid
        {
            get => endTGrid;
            set => Set(ref endTGrid, value);
        }

        public void OnCancelButtonClicked()
        {
            this.TryCloseAsync(false);
        }

        public void OnComfirmButtonClicked()
        {
            this.TryCloseAsync(true);
        }
    }
}
