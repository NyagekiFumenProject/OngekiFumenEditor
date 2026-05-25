using Caliburn.Micro;
using System;
using System.Runtime.CompilerServices;

namespace OngekiFumenEditor.Base
{
    public abstract class GridBase : PropertyChangedBase, IComparable<GridBase>, ISerializable, IComparable
    {
        private int grid = 0;
        private float unit = 0;

        private readonly uint gridRadix;
        public uint GridRadix
        {
            get => gridRadix;
            init
            {
                gridRadix = value;
                RecalculateTotalValues();
            }
        }

        public int TotalGrid { get; private set; }
        public double TotalUnit { get; private set; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RecalculateTotalValues()
        {
            TotalGrid = (int)(Unit * GridRadix + Grid);
            TotalUnit = Unit + Grid * 1.0 / GridRadix;
        }

        protected GridBase(float unit, int grid)
        {
            Grid = grid;
            Unit = unit;
        }

        public int Grid
        {
            get => grid;
            set
            {
                grid = value;
                RecalculateTotalValues();
                NotifyOfPropertyChange(nameof(Grid));
            }
        }

        public float Unit
        {
            get => unit;
            set
            {
                unit = value;
                RecalculateTotalValues();
                NotifyOfPropertyChange(nameof(Unit));
            }
        }

        public void NormalizeSelf()
        {
            var addUnit = Grid / GridRadix;
            Unit += addUnit;
            Grid = (int)(Grid % GridRadix);

            var diff = Unit - (int)Unit;
            Unit = (int)Unit;
            Grid += (int)(diff * GridRadix);

            if (Grid < 0)
            {
                Grid += (int)GridRadix;
                Unit--;
            }
        }

        public int Compare(GridBase x, GridBase y)
        {
            return x.CompareTo(y);
        }

        public int CompareTo(GridBase other)
        {
            return TotalGrid.CompareTo(other.TotalGrid);
        }

        public static bool operator ==(GridBase l, GridBase r)
        {
            if (l is null)
                return r is null;
            if (r is null)
                return false;
            return l.CompareTo(r) == 0;
        }

        public static bool operator !=(GridBase l, GridBase r)
        {
            return !(l == r);
        }

        public static GridOffset operator -(GridBase l, GridBase r)
        {
            var unitDiff = l.Unit - r.Unit;
            long gridDiff = l.Grid - r.Grid;

            while (gridDiff < 0)
            {
                unitDiff = unitDiff - 1;
                gridDiff += l.GridRadix;
            }

            return new GridOffset(unitDiff, (int)gridDiff);
        }

        public abstract string Serialize();

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;

            if (ReferenceEquals(obj, null))
                return false;

            return obj is GridBase g && g == this;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + Unit.GetHashCode();
                hash = hash * 31 + Grid.GetHashCode();
                hash = hash * 31 + GridRadix.GetHashCode();
                return hash;
            }
        }

        public int CompareTo(object obj)
        {
            return CompareTo(obj as GridBase);
        }

        public static bool operator <(GridBase left, GridBase right)
        {
            return ReferenceEquals(left, null) ? !ReferenceEquals(right, null) : left.CompareTo(right) < 0;
        }

        public static bool operator <=(GridBase left, GridBase right)
        {
            return ReferenceEquals(left, null) || left.CompareTo(right) <= 0;
        }

        public static bool operator >(GridBase left, GridBase right)
        {
            return !ReferenceEquals(left, null) && left.CompareTo(right) > 0;
        }

        public static bool operator >=(GridBase left, GridBase right)
        {
            return ReferenceEquals(left, null) ? ReferenceEquals(right, null) : left.CompareTo(right) >= 0;
        }
    }
}
