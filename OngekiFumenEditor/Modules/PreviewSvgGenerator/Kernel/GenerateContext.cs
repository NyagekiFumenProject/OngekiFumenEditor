using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using Svg;
using System;
using System.Linq;

namespace OngekiFumenEditor.Modules.PreviewSvgGenerator.Kernel
{
    public class GenerateContext
    {
        public SvgGenerateOption Option { get; init; }
        public double TotalWidth => Option.ViewWidth;
        public double TotalHeight { get; init; }
        public TGrid MaxTGrid { get; init; }
        public SoflanList SpecifySoflans { get; init; }
        public OngekiFumen Fumen { get; init; }
        public SvgDocument Document { get; init; }

        public float CalculateToY(TGrid grid)
            => CalculateToY(grid.TotalUnit);

        public float CalculateToY(double totalUnit)
        {
            double y;
            switch (Option.SoflanMode)
            {
                case SoflanMode.Soflan:
                    y = TGridCalculator.ConvertTGridUnitToY_PreviewMode(totalUnit, Fumen.Soflans, Fumen.BpmList, Option.VerticalScale);
                    break;
                case SoflanMode.WeightedSoflan:
                    y = TGridCalculator.ConvertTGridUnitToY_PreviewMode(totalUnit, SpecifySoflans, Fumen.BpmList, Option.VerticalScale);
                    break;
                case SoflanMode.NoSoflan:
                case SoflanMode.AbsSoflan:
                default:
                    y = TGridCalculator.ConvertTGridUnitToY_DesignMode(totalUnit, Fumen.Soflans, Fumen.BpmList, Option.VerticalScale);
                    break;
            }

            return (float)(TotalHeight - y);
        }

        public float CalculateToX(XGrid grid)
            => CalculateToX(grid.TotalUnit);

        public float CalculateToX(double totalUnit)
        {
            return (float)XGridCalculator.ConvertXGridToX(totalUnit, Option.XGridDisplayMaxUnit, Option.ViewWidth, 1, 0);
        }
    }
}
