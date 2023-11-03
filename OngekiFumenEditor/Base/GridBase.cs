using Caliburn.Micro;
using System;
using System.Runtime.CompilerServices;

namespace OngekiFumenEditor.Base
{
	public abstract class GridBase : PropertyChangedBase, IComparable<GridBase>, ISerializable, IComparable
	{
		private int grid = 0; //grid
		private float unit = 0; //unit

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

		public GridBase(float unit, int grid)
		{
			Grid = grid;
			Unit = unit;
		}

		public int Grid
		{
			get
			{
				return grid;
			}
			set
			{
				grid = value;
				RecalculateTotalValues();
				NotifyOfPropertyChange(nameof(Grid));
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
				gridDiff = gridDiff + l.GridRadix;
			}

			return new GridOffset(unitDiff, (int)gridDiff);
		}

		public abstract string Serialize();

		#region Implement Equals and Compares

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(this, obj))
			{
				return true;
			}

			if (ReferenceEquals(obj, null))
			{
				return false;
			}

			return obj is not GridBase g ? false : (g == this);
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(Unit, Grid, GridRadix);
		}

		public int CompareTo(object obj)
		{
			return this.CompareTo(obj as GridBase);
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
		#endregion
	}
}
