using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base
{
    public abstract class GridBase : PropertyChangedBase, IComparable<GridBase>, ISerializable
    {
        private int grid; //grid
        private float unit; //unit

        public int Grid
        {
            get
            {
                return grid;
            }
            set
            {
                grid = value;
                NotifyOfPropertyChange(() => Grid);
            }
        }
        public float Unit
        {
            get
            {
                return unit;
            }
            set
            {
                unit = value;
                NotifyOfPropertyChange(() => Unit);
            }
        }

        public int Compare(GridBase x, GridBase y)
        {
            return x.CompareTo(y);
        }

        public int CompareTo(GridBase other)
        {
            if (other.Unit != Unit)
            {
                return Math.Sign(Unit - other.Unit);
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
