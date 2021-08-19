using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base
{
    public abstract class GridBase : IComparable<GridBase> , ISerializable
    {
        public int Grid { get; set; } //grid
        public int Unit { get; set; } //unit

        public int Compare(GridBase x, GridBase y)
        {
            return x.CompareTo(y);
        }

        public int CompareTo(GridBase other)
        {
            if(other.Unit != Unit)
            {
                return Unit - other.Unit;
            }

            if (other.Grid != Grid)
            {
                return Grid - other.Grid;
            }

            return 0;
        }

        public abstract string Serialize(OngekiFumen fumenData);
    }
}
