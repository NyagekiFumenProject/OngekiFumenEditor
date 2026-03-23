using OngekiFumenEditor.Core.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System.Runtime.CompilerServices;
using CoreXGridCalculator = OngekiFumenEditor.Core.Modules.FumenVisualEditor.XGridCalculator;

namespace OngekiFumenEditor.Modules.FumenVisualEditor
{
    public static class XGridCalculator
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double CalculateXUnitSize(FumenVisualEditorViewModel editor)
            => CalculateXUnitSize(editor.Setting.XGridDisplayMaxUnit, editor.ViewWidth, editor.Setting.XGridUnitSpace);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double CalculateXUnitSize(double xGridDisplayMaxUnit, double viewWidth, double xUnitSpace)
            => CoreXGridCalculator.CalculateXUnitSize(xGridDisplayMaxUnit, viewWidth, xUnitSpace);

        public static XGrid ConvertXToXGrid(double x, FumenVisualEditorViewModel editor)
            => ConvertXToXGrid(x, editor.Setting.XGridDisplayMaxUnit, editor.ViewWidth, editor.Setting.XGridUnitSpace, editor.Setting.XOffset);

        public static XGrid ConvertXToXGrid(double x, double xGridDisplayMaxUnit, double viewWidth, double xUnitSpace, double xOffset)
            => CoreXGridCalculator.ConvertXToXGrid(x, xGridDisplayMaxUnit, viewWidth, xUnitSpace, xOffset);

        public static double ConvertXToXGridTotalUnit(double x, FumenVisualEditorViewModel editor)
            => ConvertXToXGridTotalUnit(x, editor.Setting.XGridDisplayMaxUnit, editor.ViewWidth, editor.Setting.XGridUnitSpace, editor.Setting.XOffset);

        public static double ConvertXToXGridTotalUnit(double x, double xGridDisplayMaxUnit, double viewWidth, double xUnitSpace, double xOffset)
            => CoreXGridCalculator.ConvertXToXGridTotalUnit(x, xGridDisplayMaxUnit, viewWidth, xUnitSpace, xOffset);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ConvertXGridToX(XGrid xGrid, FumenVisualEditorViewModel editor)
            => ConvertXGridToX(xGrid, editor.Setting.XGridDisplayMaxUnit, editor.ViewWidth, editor.Setting.XGridUnitSpace, editor.Setting.XOffset);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ConvertXGridToX(double xGridUnit, FumenVisualEditorViewModel editor)
            => ConvertXGridToX(xGridUnit, editor.Setting.XGridDisplayMaxUnit, editor.ViewWidth, editor.Setting.XGridUnitSpace, editor.Setting.XOffset);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ConvertXGridToX(XGrid xGrid, double xGridDisplayMaxUnit, double viewWidth, double xUnitSpace, double xOffset)
            => CoreXGridCalculator.ConvertXGridToX(xGrid, xGridDisplayMaxUnit, viewWidth, xUnitSpace, xOffset);

        public static double ConvertXGridToX(double xGridUnit, double xGridDisplayMaxUnit, double viewWidth, double xUnitSpace, double xOffset)
            => CoreXGridCalculator.ConvertXGridToX(xGridUnit, xGridDisplayMaxUnit, viewWidth, xUnitSpace, xOffset);
    }
}
