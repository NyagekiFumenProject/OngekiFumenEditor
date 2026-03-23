using OngekiFumenEditor.Core.Base;
using System;
using System.Runtime.CompilerServices;

namespace OngekiFumenEditor.Core.Modules.FumenVisualEditor
{
    public static class XGridCalculator
    {
        #region XGrid -> X

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double CalculateXUnitSize(double xGridDisplayMaxUnit, double viewWidth, double xUnitSpace)
            => viewWidth / (xGridDisplayMaxUnit * 2) * xUnitSpace;

        #endregion

        #region X -> XGrid

        public static XGrid ConvertXToXGrid(double x, double xGridDisplayMaxUnit, double viewWidth, double xUnitSpace, double xOffset)
        {
            var xUnit = (float)ConvertXToXGridTotalUnit(x, xGridDisplayMaxUnit, viewWidth, xUnitSpace, xOffset);
            return new XGrid() { Unit = xUnit };
        }

        public static double ConvertXToXGridTotalUnit(double x, double xGridDisplayMaxUnit, double viewWidth, double xUnitSpace, double xOffset)
        {
            var xUnit = ((x - xOffset) - viewWidth / 2) / (CalculateXUnitSize(xGridDisplayMaxUnit, viewWidth, xUnitSpace) / xUnitSpace);
            var nearXUnit = xUnit > 0 ? Math.Floor(xUnit + 0.5) : Math.Ceiling(xUnit - 0.5);
            return Math.Abs(xUnit - nearXUnit) < 0.00001 ? (int)nearXUnit : (float)xUnit;
        }

        #endregion

        #region XGrid -> X

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ConvertXGridToX(XGrid xGrid, double xGridDisplayMaxUnit, double viewWidth, double xUnitSpace, double xOffset)
            => ConvertXGridToX(xGrid.TotalUnit, xGridDisplayMaxUnit, viewWidth, xUnitSpace, xOffset);

        public static double ConvertXGridToX(double xGridUnit, double xGridDisplayMaxUnit, double viewWidth, double xUnitSpace, double xOffset)
        {
            var xUnitSize = CalculateXUnitSize(xGridDisplayMaxUnit, viewWidth, xUnitSpace);
            var x = (xGridUnit * (xUnitSize / xUnitSpace)) + viewWidth / 2 + xOffset;
            return x;
        }

        #endregion
    }
}
