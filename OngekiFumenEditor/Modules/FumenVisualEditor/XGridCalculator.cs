using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using System.Runtime.CompilerServices;

namespace OngekiFumenEditor.Modules.FumenVisualEditor
{
    public static class XGridCalculator
    {
        #region XGrid -> X

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double CalculateXUnitSize(FumenVisualEditorViewModel editor)
            => CalculateXUnitSize(editor.Setting.XGridDisplayMaxUnit, editor.ViewWidth, editor.Setting.XGridUnitSpace);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double CalculateXUnitSize(double xGridDisplayMaxUnit, double viewWidth, double xUnitSpace)
            => viewWidth / (xGridDisplayMaxUnit * 2) * xUnitSpace;

        #endregion

        #region X -> XGrid

        public static XGrid ConvertXToXGrid(double x, FumenVisualEditorViewModel editor)
            => ConvertXToXGrid(x, editor.Setting.XGridDisplayMaxUnit, editor.ViewWidth, editor.Setting.XGridUnitSpace, editor.Setting.XOffset);

        public static XGrid ConvertXToXGrid(double x, double xGridDisplayMaxUnit, double viewWidth, double xUnitSpace, double xOffset)
        {
            var xUnit = (float)ConvertXToXGridTotalUnit(x, xGridDisplayMaxUnit, viewWidth, xUnitSpace, xOffset);
            return new XGrid() { Unit = xUnit };
        }

        public static double ConvertXToXGridTotalUnit(double x, FumenVisualEditorViewModel editor)
            => ConvertXToXGridTotalUnit(x, editor.Setting.XGridDisplayMaxUnit, editor.ViewWidth, editor.Setting.XGridUnitSpace, editor.Setting.XOffset);

        public static double ConvertXToXGridTotalUnit(double x, double xGridDisplayMaxUnit, double viewWidth, double xUnitSpace, double xOffset)
        {
            var xUnit = ((x - xOffset) - viewWidth / 2) / (CalculateXUnitSize(xGridDisplayMaxUnit, viewWidth, xUnitSpace) / xUnitSpace);
            var nearXUnit = xUnit > 0 ? Math.Floor(xUnit + 0.5) : Math.Ceiling(xUnit - 0.5);
            return Math.Abs(xUnit - nearXUnit) < 0.00001 ? (int)nearXUnit : (float)xUnit;
        }

        /// <summary>
        /// 将 XGrid 四舍五入到最近的整数 unit（半值远离零），Grid 置 0。
        /// 与 ConvertXToXGridTotalUnit 内 nearXUnit 的舍入方向一致。
        /// </summary>
        public static XGrid RoundToIntegerUnit(XGrid xGrid)
        {
            var totalUnit = xGrid.TotalUnit;
            var nearestUnit = totalUnit > 0 ? Math.Floor(totalUnit + 0.5) : Math.Ceiling(totalUnit - 0.5);
            return new XGrid((float)nearestUnit, 0);
        }

        #endregion

        #region XGrid -> X

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ConvertXGridToX(XGrid xGrid, FumenVisualEditorViewModel editor)
            => ConvertXGridToX(xGrid, editor.Setting.XGridDisplayMaxUnit, editor.ViewWidth, editor.Setting.XGridUnitSpace, editor.Setting.XOffset);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ConvertXGridToX(double xGridUnit, FumenVisualEditorViewModel editor)
            => ConvertXGridToX(xGridUnit, editor.Setting.XGridDisplayMaxUnit, editor.ViewWidth, editor.Setting.XGridUnitSpace, editor.Setting.XOffset);

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
