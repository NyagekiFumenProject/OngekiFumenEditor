using System;
using OngekiFumenEditor.Base.OngekiObjects;

namespace OngekiFumenEditor.Modules.SelectionFilter;

public enum LinePointTypeFilteringMode
{
    Head,
    Tail,
    Any
}

public enum YesNoFilteringMode
{
    Yes,
    No,
    Any
}

public enum LeftRightFilteringMode
{
    Left,
    Right,
    Any
}

public enum FilterMode
{
    Remove,
    Replace
}

public static class FilterOptionEnumsExtensions
{
    public static Flick.FlickDirection ToFlickDirection(this LeftRightFilteringMode @this)
        => @this switch
        {
            LeftRightFilteringMode.Left => Flick.FlickDirection.Left,
            LeftRightFilteringMode.Right => Flick.FlickDirection.Right,
            _ => throw new ArgumentOutOfRangeException(nameof(@this), @this, null)
        };

    public static LaneBlockArea.BlockDirection ToLaneBlockDirection(this LeftRightFilteringMode @this)
        => @this switch
        {
            LeftRightFilteringMode.Left => LaneBlockArea.BlockDirection.Left,
            LeftRightFilteringMode.Right => LaneBlockArea.BlockDirection.Right,
            _ => throw new ArgumentOutOfRangeException(nameof(@this), @this, null)
        };
}
