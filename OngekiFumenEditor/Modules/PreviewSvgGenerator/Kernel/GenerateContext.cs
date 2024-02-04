using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using Svg;
using System;

namespace OngekiFumenEditor.Modules.PreviewSvgGenerator.Kernel
{
    public record GenerateContext
    {
        public GenerateOption Option { get; init; }
        public double TotalWidth => Option.ViewWidth;
        public double TotalHeight { get; init; }
        public TGrid MaxTGrid { get; init; }

        public OngekiFumen Fumen { get; init; }
        public SvgDocument Document { get; init; }

        public float CalculateToY(TGrid grid)
            => CalculateToY(grid.TotalUnit);

        public float CalculateToY(double totalUnit)
        {
            if (Option.SoflanMode == SoflanMode.Soflan)
                return (float)(TotalHeight - TGridCalculator.ConvertTGridUnitToY_PreviewMode(totalUnit, Fumen.Soflans, Fumen.BpmList, Option.VerticalScale));
            return (float)(TotalHeight - TGridCalculator.ConvertTGridUnitToY_DesignMode(totalUnit, Fumen.Soflans, Fumen.BpmList, Option.VerticalScale));
        }

        public float CalculateToX(XGrid grid)
            => CalculateToX(grid.TotalUnit);

        public float CalculateToX(double totalUnit)
        {
            return (float)XGridCalculator.ConvertXGridToX(totalUnit, Option.XGridDisplayMaxUnit, Option.ViewWidth, 1, 0);
        }
    }
}
