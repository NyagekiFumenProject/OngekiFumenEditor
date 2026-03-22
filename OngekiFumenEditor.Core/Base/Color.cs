namespace OngekiFumenEditor.Base.ValueTypes;

public readonly record struct Color(byte A, byte R, byte G, byte B)
{
    public static Color FromArgb(byte a, byte r, byte g, byte b) => new(a, r, g, b);

    public static Color FromRgb(byte r, byte g, byte b) => new(255, r, g, b);

    public override string ToString() => $"#{A:X2}{R:X2}{G:X2}{B:X2}";
}

public static class Colors
{
    public static Color Transparent { get; } = Color.FromArgb(0, 0, 0, 0);
    public static Color White { get; } = Color.FromRgb(255, 255, 255);
    public static Color Green { get; } = Color.FromRgb(0, 128, 0);
    public static Color DarkKhaki { get; } = Color.FromRgb(189, 183, 107);
}
