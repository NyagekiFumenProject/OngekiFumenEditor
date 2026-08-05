using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.EditorObjects.Svg;
using OngekiFumenEditor.Avalonia.Utils;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem.Impl.LocalFileSystem;

namespace OngekiFumenEditor.Avalonia.Parser.Ogkr.CommandParserImpl.Editor;

public abstract class SvgPrefabCommandBase : ICommandParser
{
    public abstract string CommandLineHeader { get; }
    protected abstract int CurveFactoryIndex { get; }

    public void AfterParse(OngekiObjectBase obj, OngekiFumen fumen)
    {
    }

    public OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen)
    {
        var svg = CreateAndParseSvgObject(args);
        svg.ColorSimilar.CurrentValue = Single(args, 1, "ColorSimilar");
        svg.Rotation.CurrentValue = Single(args, 2, "Rotation");
        svg.EnableColorfulLaneSimilar = Boolean(args, 3, "EnableColorfulLaneSimilar");
        svg.OffsetX.CurrentValue = Single(args, 4, "OffsetX");
        svg.OffsetY.CurrentValue = Single(args, 5, "OffsetY");
        svg.ShowOriginColor = Boolean(args, 6, "ShowOriginColor");
        svg.Opacity.CurrentValue = Single(args, 7, "Opacity");
        svg.Scale = Single(args, 8, "Scale");
        svg.Tolerance.CurrentValue = Single(args, 9, "Tolerance");
        svg.TGrid = new TGrid(Single(args, 10, "TGrid.Unit"), Int32(args, 11, "TGrid.Grid"));
        svg.XGrid = new XGrid(Single(args, 12, "XGrid.Unit"), Int32(args, 13, "XGrid.Grid"));
        svg.ColorfulLaneBrightness.CurrentValue = Single(args, 14, "Brightness");
        svg.IsForceColorful = Boolean(args, 15, "IsForceColorful");
        svg.ColorfulLaneColor = SvgPrefabFormatUtils.ResolveColorId(Int32(args, 16, "ColorfulLaneColorId"));

        if (args.GetRawData(CurveFactoryIndex) is { Length: > 0 } encodedFactory)
            svg.CurveInterpolaterFactory = SvgPrefabFormatUtils.ResolveCurveInterpolaterFactory(Base64.Decode(encodedFactory));
        return svg;
    }

    protected abstract SvgPrefabBase CreateAndParseSvgObject(CommandArgs args);

    protected static string Required(CommandArgs args, int index, string fieldName)
    {
        var value = args.GetRawData(index);
        if (!string.IsNullOrWhiteSpace(value))
            return value;
        throw new FormatException($"SVG prefab command is missing required field '{fieldName}' at index {index}.");
    }

    protected static float Single(CommandArgs args, int index, string fieldName) =>
        SvgPrefabFormatUtils.ParseSingle(Required(args, index, fieldName), fieldName);

    protected static double Double(CommandArgs args, int index, string fieldName) =>
        SvgPrefabFormatUtils.ParseDouble(Required(args, index, fieldName), fieldName);

    protected static int Int32(CommandArgs args, int index, string fieldName) =>
        SvgPrefabFormatUtils.ParseInt32(Required(args, index, fieldName), fieldName);

    protected static bool Boolean(CommandArgs args, int index, string fieldName) =>
        SvgPrefabFormatUtils.ParseBoolean(Required(args, index, fieldName), fieldName);
}

[RegisterSingleton<ICommandParser>]
public sealed class SvgImageFilePrefabCommand : SvgPrefabCommandBase
{
    public override string CommandLineHeader => SvgImageFilePrefab.CommandName;
    protected override int CurveFactoryIndex => 18;

    protected override SvgPrefabBase CreateAndParseSvgObject(CommandArgs args)
    {
        var svg = new SvgImageFilePrefab();
        var path = Base64.Decode(Required(args, 17, "FilePathBase64"));
        if (!string.IsNullOrWhiteSpace(path))
            svg.SvgFile = new LocalSimpleFile(path);
        return svg;
    }
}

[RegisterSingleton<ICommandParser>]
public sealed class SvgStringPrefabCommand : SvgPrefabCommandBase
{
    public override string CommandLineHeader => SvgStringPrefab.CommandName;
    protected override int CurveFactoryIndex => 22;

    protected override SvgPrefabBase CreateAndParseSvgObject(CommandArgs args)
    {
        return new SvgStringPrefab
        {
            Content = Base64.Decode(Required(args, 17, "Content")),
            FontSize = Double(args, 18, "FontSize"),
            TypefaceName = Base64.Decode(Required(args, 19, "TypefaceName")),
            ContentFlowDirection = SvgPrefabFormatUtils.ParseEnum<SvgStringPrefab.FlowDirection>(
                Required(args, 20, "ContentFlowDirection"), "ContentFlowDirection"),
            ContentLineHeight = Double(args, 21, "ContentLineHeight")
        };
    }
}
