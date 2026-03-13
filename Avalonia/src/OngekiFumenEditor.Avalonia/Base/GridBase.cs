using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OngekiFumenEditor.Avalonia.Base;

public abstract class GridBase : ObservableObject, IComparable<GridBase>, ISerializable, IComparable
{
    private readonly uint gridRadix;
    private int grid; //grid
    private float unit; //unit

    public GridBase(float unit, int grid)
    {
        Grid = grid;
        Unit = unit;
    }

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

    public int Grid
    {
        get => grid;
        set
        {
            grid = value;
            RecalculateTotalValues();
            OnPropertyChanged();
        }
    }

    public float Unit
    {
        get => unit;
        set
        {
            unit = value;
            RecalculateTotalValues();
            OnPropertyChanged(nameof(Unit));
        }
    }

    public int CompareTo(GridBase other)
    {
        return TotalGrid.CompareTo(other.TotalGrid);
    }

    public abstract string Serialize();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecalculateTotalValues()
    {
        TotalGrid = (int) (Unit * GridRadix + Grid);
        TotalUnit = Unit + Grid * 1.0 / GridRadix;
    }

    public void NormalizeSelf()
    {
        var addUnit = Grid / GridRadix;
        Unit += addUnit;
        Grid = (int) (Grid % GridRadix);

        var diff = Unit - (int) Unit;
        Unit = (int) Unit;
        Grid += (int) (diff * GridRadix);

        if (Grid < 0)
        {
            Grid += (int) GridRadix;
            Unit--;
        }
    }

    public int Compare(GridBase x, GridBase y)
    {
        return x.CompareTo(y);
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

        return new GridOffset(unitDiff, (int) gridDiff);
    }

    #region Implement Equals and Compares

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(this, obj))
            return true;

        if (ReferenceEquals(obj, null))
            return false;

        return obj is not GridBase g ? false : g == this;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Unit, Grid, GridRadix);
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

    #endregion
}