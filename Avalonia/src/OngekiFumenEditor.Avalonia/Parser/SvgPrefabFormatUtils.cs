using System.Globalization;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Kernel.CurveInterpolater;
using OngekiFumenEditor.Avalonia.Kernel.CurveInterpolater.DefaultImpl.Factory;
using OngekiFumenEditor.Avalonia.Kernel.CurveInterpolater.OgkrImpl.Factory;

namespace OngekiFumenEditor.Avalonia.Parser;

internal static class SvgPrefabFormatUtils
{
    public static string Format(float value) => value.ToString("R", CultureInfo.InvariantCulture);
    public static string Format(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    public static string Format(int value) => value.ToString(CultureInfo.InvariantCulture);
    public static string Format(bool value) => value ? bool.TrueString : bool.FalseString;

    public static float ParseSingle(string value, string fieldName)
    {
        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) && float.IsFinite(result))
            return result;
        throw new FormatException($"SVG prefab field '{fieldName}' is not a finite invariant-culture Single: '{value}'.");
    }

    public static double ParseDouble(string value, string fieldName)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) && double.IsFinite(result))
            return result;
        throw new FormatException($"SVG prefab field '{fieldName}' is not a finite invariant-culture Double: '{value}'.");
    }

    public static int ParseInt32(string value, string fieldName)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            return result;
        throw new FormatException($"SVG prefab field '{fieldName}' is not an invariant-culture Int32: '{value}'.");
    }

    public static bool ParseBoolean(string value, string fieldName)
    {
        if (bool.TryParse(value, out var result))
            return result;
        if (value == "0")
            return false;
        if (value == "1")
            return true;
        throw new FormatException($"SVG prefab field '{fieldName}' is not a Boolean: '{value}'.");
    }

    public static TEnum ParseEnum<TEnum>(string value, string fieldName) where TEnum : struct, Enum
    {
        if (Enum.TryParse<TEnum>(value, ignoreCase: false, out var result) && Enum.IsDefined(result))
            return result;
        throw new FormatException($"SVG prefab field '{fieldName}' has an unsupported {typeof(TEnum).Name} value: '{value}'.");
    }

    public static ColorId ResolveColorId(int id)
    {
        foreach (var color in ColorIdConst.SvgPrefabColors)
        {
            if (color.Id == id)
                return color;
        }

        throw new FormatException($"SVG prefab references unknown ColorId '{id}'.");
    }

    public static ICurveInterpolaterFactory ResolveCurveInterpolaterFactory(string name)
    {
        return name switch
        {
            "XGrid.Unit limited" => XGridLimitedCurveInterpolaterFactory.Default,
            "Default" => DefaultCurveInterpolaterFactory.Default,
            _ => throw new FormatException($"SVG prefab references unknown curve interpolater factory '{name}'.")
        };
    }

    public static TGrid ParseTGrid(string value, string fieldName)
    {
        var fields = UnwrapGrid(value, 'T', fieldName);
        return new TGrid(ParseSingle(fields[0], fieldName + ".Unit"), ParseInt32(fields[1], fieldName + ".Grid"));
    }

    public static XGrid ParseXGrid(string value, string fieldName)
    {
        var fields = UnwrapGrid(value, 'X', fieldName);
        return new XGrid(ParseSingle(fields[0], fieldName + ".Unit"), ParseInt32(fields[1], fieldName + ".Grid"));
    }

    private static string[] UnwrapGrid(string value, char prefix, string fieldName)
    {
        var trimmed = value.Trim();
        if (trimmed.Length >= 4 && trimmed[0] == prefix && trimmed[1] == '[' && trimmed[^1] == ']')
            trimmed = trimmed[2..^1];

        var fields = trimmed.Split(',');
        if (fields.Length != 2)
            throw new FormatException($"SVG prefab field '{fieldName}' must contain Unit and Grid: '{value}'.");
        return fields;
    }
}
