using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor
{
    public static class XGridCalculator
    {
        public static XGrid ConvertXToXGrid(double x, FumenVisualEditorViewModel editor)
        {
            var xUnit = (x - editor.CanvasWidth / 2) / (editor.XUnitSize / editor.Setting.UnitCloseSize);
            var nearXUnit = xUnit > 0 ? Math.Floor(xUnit + 0.5) : Math.Ceiling(xUnit - 0.5);
            return new XGrid() { Unit = Math.Abs(xUnit - nearXUnit) < 0.00001 ? (int)nearXUnit : (float)xUnit };
        }

        public static double ConvertXGridToX(XGrid xGrid, FumenVisualEditorViewModel editor)
        {
            var xUnit = xGrid.Unit + xGrid.Grid * 1.0 / xGrid.ResX;
            var x = (xUnit * (editor.XUnitSize / editor.Setting.UnitCloseSize)) + editor.CanvasWidth / 2;
            return x;
        }
    }
}
