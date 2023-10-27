using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using System.Runtime.CompilerServices;

namespace OngekiFumenEditor.Modules.FumenVisualEditor
{
	public static class XGridCalculator
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static double CalculateXUnitSize(FumenVisualEditorViewModel editor)
			=> CalculateXUnitSize(editor.Setting.XGridDisplayMaxUnit, editor.ViewWidth, editor.Setting.XGridUnitSpace);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static double CalculateXUnitSize(double xGridDisplayMaxUnit, double viewWidth, double xUnitSpace)
		{
			return viewWidth / (xGridDisplayMaxUnit * 2) * xUnitSpace;
		}

		public static XGrid ConvertXToXGrid(double x, FumenVisualEditorViewModel editor)
		{
			var xUnit = ((x - editor.Setting.XOffset) - editor.ViewWidth / 2) / (CalculateXUnitSize(editor) / editor.Setting.XGridUnitSpace);
			var nearXUnit = xUnit > 0 ? Math.Floor(xUnit + 0.5) : Math.Ceiling(xUnit - 0.5);
			return new XGrid() { Unit = Math.Abs(xUnit - nearXUnit) < 0.00001 ? (int)nearXUnit : (float)xUnit };
		}

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
	}
}
