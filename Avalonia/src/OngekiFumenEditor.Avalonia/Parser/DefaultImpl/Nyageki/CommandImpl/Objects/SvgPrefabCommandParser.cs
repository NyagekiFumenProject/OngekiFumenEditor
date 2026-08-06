using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.EditorObjects.Svg;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Parser.DefaultImpl.Nyageki.CommandImpl.Objects;

[RegisterSingleton<INyagekiCommandParser>]
public sealed class SvgPrefabCommandParser : INyagekiCommandParser
{
    public string CommandName => "SvgPrefab";

    public void ParseAndApply(OngekiFumen fumen, string[] seg)
    {
        ArgumentNullException.ThrowIfNull(fumen);
        if (seg.Length < 2)
            throw new FormatException("SvgPrefab command has no field payload.");

        using var scope = seg[1].GetValuesMapWithDisposable(out var fields);
        var type = Required(fields, "Type");
        SvgPrefabBase svg = type switch
        {
            SvgImageFilePrefab.CommandName => ParseImage(fields),
            SvgStringPrefab.CommandName => ParseString(fields),
            _ => throw new FormatException($"Unsupported SvgPrefab type '{type}'.")
        };

        ApplyCommonFields(svg, fields);
        fumen.AddObject(svg);
    }

    private static SvgImageFilePrefab ParseImage(IReadOnlyDictionary<string, string> fields)
    {
        var svg = new SvgImageFilePrefab();
        var encodedPath = Required(fields, "FilePathBase64");
        var path = Base64.Decode(encodedPath);
        if (!string.IsNullOrWhiteSpace(path))
            svg.SvgFilePath = path;
        return svg;
    }

    private static SvgStringPrefab ParseString(IReadOnlyDictionary<string, string> fields)
    {
        return new SvgStringPrefab
        {
            Content = Base64.Decode(Required(fields, "Content")),
            TypefaceName = Required(fields, "TypefaceName"),
            FontSize = SvgPrefabFormatUtils.ParseDouble(Required(fields, "FontSize"), "FontSize"),
            ContentFlowDirection = SvgPrefabFormatUtils.ParseEnum<SvgStringPrefab.FlowDirection>(
                Required(fields, "ContentFlowDirection"), "ContentFlowDirection"),
            ContentLineHeight = SvgPrefabFormatUtils.ParseDouble(
                Required(fields, "ContentLineHeight"), "ContentLineHeight")
        };
    }

    private static void ApplyCommonFields(SvgPrefabBase svg, IReadOnlyDictionary<string, string> fields)
    {
        svg.OffsetX.CurrentValue = SvgPrefabFormatUtils.ParseSingle(Required(fields, "OffsetX"), "OffsetX");
        svg.OffsetY.CurrentValue = SvgPrefabFormatUtils.ParseSingle(Required(fields, "OffsetY"), "OffsetY");
        svg.ColorfulLaneBrightness.CurrentValue = SvgPrefabFormatUtils.ParseSingle(Required(fields, "Brightness"), "Brightness");
        svg.ShowOriginColor = SvgPrefabFormatUtils.ParseBoolean(Required(fields, "ShowOriginColor"), "ShowOriginColor");
        svg.ColorSimilar.CurrentValue = SvgPrefabFormatUtils.ParseSingle(Required(fields, "ColorSimilar"), "ColorSimilar");
        svg.Rotation.CurrentValue = SvgPrefabFormatUtils.ParseSingle(Required(fields, "Rotation"), "Rotation");
        svg.EnableColorfulLaneSimilar = SvgPrefabFormatUtils.ParseBoolean(
            Required(fields, "EnableColorfulLaneSimilar"), "EnableColorfulLaneSimilar");
        svg.Opacity.CurrentValue = SvgPrefabFormatUtils.ParseSingle(Required(fields, "Opacity"), "Opacity");
        svg.Scale = SvgPrefabFormatUtils.ParseSingle(Required(fields, "Scale"), "Scale");
        svg.Tolerance.CurrentValue = SvgPrefabFormatUtils.ParseSingle(Required(fields, "Tolerance"), "Tolerance");
        svg.TGrid = SvgPrefabFormatUtils.ParseTGrid(Required(fields, "T"), "T");
        svg.XGrid = SvgPrefabFormatUtils.ParseXGrid(Required(fields, "X"), "X");

        if (fields.TryGetValue("IsForceColorful", out var forceColorful))
            svg.IsForceColorful = SvgPrefabFormatUtils.ParseBoolean(forceColorful, "IsForceColorful");

        var colorField = fields.TryGetValue("ColorfulLaneColorId", out var commonColor)
            ? commonColor
            : fields.TryGetValue("FontColorId", out var legacyFontColor) ? legacyFontColor : null;
        if (colorField is not null)
            svg.ColorfulLaneColor = SvgPrefabFormatUtils.ResolveColorId(
                SvgPrefabFormatUtils.ParseInt32(colorField, "ColorfulLaneColorId"));

        if (fields.TryGetValue("CurveInterpolaterFactory", out var curveFactory))
            svg.CurveInterpolaterFactory = SvgPrefabFormatUtils.ResolveCurveInterpolaterFactory(curveFactory);
    }

    private static string Required(IReadOnlyDictionary<string, string> fields, string name)
    {
        if (fields.TryGetValue(name, out var value))
            return value;
        throw new FormatException($"SvgPrefab command is missing required field '{name}'.");
    }
}
