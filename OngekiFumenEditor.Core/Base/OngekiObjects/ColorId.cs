using System;
using OngekiFumenEditor.Core.Base.ValueTypes;

namespace OngekiFumenEditor.Core.Base.OngekiObjects;

public struct ColorId : IEquatable<ColorId>
{
    public int Id { get; set; }
    public string Name { get; set; }
    public Color Color { get; set; }

    public override string ToString()
    {
        return $"{Id} {Name}";
    }

    public static bool operator ==(ColorId left, ColorId right)
    {
        return left.Id == right.Id;
    }

    public static bool operator !=(ColorId left, ColorId right)
    {
        return !(left == right);
    }

    public bool Equals(ColorId other)
    {
        return Id == other.Id;
    }

    public override bool Equals(object obj)
    {
        return obj is ColorId other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Id;
    }
}
