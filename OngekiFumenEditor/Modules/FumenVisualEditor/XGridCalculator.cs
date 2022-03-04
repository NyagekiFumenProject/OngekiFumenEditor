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
        public static double CalculateXUnitSize(FumenVisualEditorViewModel editor)
            => CalculateXUnitSize(editor.Setting.XGridDisplayMaxUnit, editor.CanvasWidth, editor.Setting.XGridUnitSpace);

        public static double CalculateXUnitSize(double xGridDisplayMaxUnit, double viewWidth, double xUnitSpace)
        {
            return viewWidth / (xGridDisplayMaxUnit * 2) * xUnitSpace;
        }

        public static XGrid ConvertXToXGrid(double x, FumenVisualEditorViewModel editor)
        {
            var xUnit = (x - editor.CanvasWidth / 2) / (CalculateXUnitSize(editor) / editor.Setting.XGridUnitSpace);
            var nearXUnit = xUnit > 0 ? Math.Floor(xUnit + 0.5) : Math.Ceiling(xUnit - 0.5);
            return new XGrid() { Unit = Math.Abs(xUnit - nearXUnit) < 0.00001 ? (int)nearXUnit : (float)xUnit };
        }

        public static double ConvertXGridToX(XGrid xGrid, FumenVisualEditorViewModel editor)
            => ConvertXGridToX(xGrid, editor.Setting.XGridDisplayMaxUnit, editor.CanvasWidth, editor.Setting.XGridUnitSpace);

        public static double ConvertXGridToX(XGrid xGrid, double xGridDisplayMaxUnit, double viewWidth, double xUnitSpace)
        {
            var xUnitSize = CalculateXUnitSize(xGridDisplayMaxUnit, viewWidth, xUnitSpace);
            var xUnit = xGrid.Unit + xGrid.Grid * 1.0 / xGrid.ResX;
            var x = (xUnit * (xUnitSize / xUnitSpace)) + viewWidth / 2;
            return x;
        }
    }
}
