using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.EditorObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Utils;
using System;
using System.Linq;

namespace OngekiFumenEditor.Avalonia.Parser.DefaultImpl.Nyageki.CommandImpl.Objects
{
    [RegisterSingleton<INyagekiCommandParser>]
    public class IndividualSoflanAreaCommandParser : INyagekiCommandParser
    {
        public virtual string CommandName => "IndividualSoflanArea";

        public virtual void ParseAndApply(OngekiFumen fumen, string[] seg)
        {
            //$"Soflan:{soflan.Speed}:(T[{soflan.TGrid.Unit},{soflan.TGrid.Grid}]) -> (T[{soflan.EndTGrid.Unit},{soflan.EndTGrid.Grid}])"
            var soflan = new IndividualSoflanArea();
            Apply(soflan, seg);
            fumen.AddObject(soflan);
        }

        public void Apply(IndividualSoflanArea isf, string[] seg)
        {
            var data = seg[1].Split(":");

            var tgridRange = data[0]
                .Split("->")
                .Select(x => x.Trim().TrimStart('(').TrimEnd(')'))
                .Select(x => x.ParseToTGrid())
                .ToArray();

            isf.TGrid = tgridRange[0];
            isf.EndIndicator.TGrid = tgridRange[1];

            var xgridRange = data[1]
                .Split("->")
                .Select(x => x.Trim().TrimStart('(').TrimEnd(')'))
                .Select(x => x.ParseToXGrid())
                .ToArray();

            isf.XGrid = xgridRange[0];
            isf.EndIndicator.XGrid = xgridRange[1];

            using var d = data.LastOrDefault().GetValuesMapWithDisposable(out var map);
            if (map.TryGetValue("SoflanGroup", out var soflanGroupStr))
            {
                if (int.TryParse(soflanGroupStr, out var soflanGroup))
                {
                    isf.SoflanGroup = soflanGroup;
                }
            }
        }
    }
}


