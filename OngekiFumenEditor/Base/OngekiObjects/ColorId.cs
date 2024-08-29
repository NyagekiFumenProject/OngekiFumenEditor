using System.Numerics;
using System.Windows.Media;

namespace OngekiFumenEditor.Base.OngekiObjects;

public struct ColorId : IEqualityOperators<ColorId, ColorId, bool>
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
}