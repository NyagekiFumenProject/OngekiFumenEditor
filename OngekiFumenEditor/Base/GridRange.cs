using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base
{
    public struct GridRange
    {
        public GridBase Min { get; set; }
        public GridBase Max { get; set; }

        public bool IsInRange(GridBase chk, bool includeEdge = true)
        {
            return includeEdge ? (Min <= chk && chk <= Max) : (Min < chk && chk < Max);
        }

        public bool IsInRange(GridRange range, bool includeEdge = true)
        {
            return IsInRange(range.Min, includeEdge) || IsInRange(range.Max, includeEdge);
        }

        public override string ToString() => $"{{{Min} ~ {Max}}}";
    }
}
