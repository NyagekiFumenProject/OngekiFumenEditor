#nullable enable

using System.ComponentModel;
using System.Globalization;
using System.Xml.Linq;

namespace OngekiFumenEditor.Avalonia.Base.EditorObjects.Svg;

public sealed class SvgStringPrefab : SvgPrefabBase
{
    public enum FlowDirection
    {
        LeftToRight,
        RightToLeft,
        TopToBottom,
        BottomToTop
    }

    public const string CommandName = "[SVG_STR]";
    private string content = string.Empty;
    private FlowDirection contentFlowDirection = FlowDirection.LeftToRight;
    private double fontSize = 16;
    private double contentLineHeight = 16;
    private string typefaceName = "Tahoma";

    public override string IDShortName => CommandName;

    public string Content
    {
        get => content;
        set => SetProperty(ref content, value ?? string.Empty);
    }

    public FlowDirection ContentFlowDirection
    {
        get => contentFlowDirection;
        set => SetProperty(ref contentFlowDirection, value);
    }

    public double FontSize
    {
        get => fontSize;
        set => SetProperty(ref fontSize, value);
    }

    public double ContentLineHeight
    {
        get => contentLineHeight;
        set => SetProperty(ref contentLineHeight, value);
    }

    public string TypefaceName
    {
        get => typefaceName;
        set => SetProperty(ref typefaceName, value ?? string.Empty);
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
#if ENABLE_SVG_PREFAB_OBJECTS
        switch (e.PropertyName)
        {
            case nameof(Content):
            case nameof(FontSize):
            case nameof(ColorfulLaneColor):
            case nameof(ContentLineHeight):
            case nameof(ContentFlowDirection):
            case nameof(TypefaceName):
                RebuildSvgContent();
                break;
        }
#endif
    }

    public void RebuildSvgContent()
    {
        CleanGeometry();
#if ENABLE_SVG_PREFAB_OBJECTS
        if (string.IsNullOrWhiteSpace(Content) || !double.IsFinite(FontSize) || FontSize <= 0)
            return;

        var text = ContentFlowDirection is FlowDirection.RightToLeft or FlowDirection.BottomToTop
            ? new string(Content.Reverse().ToArray())
            : Content;
        var isVertical = ContentFlowDirection is FlowDirection.TopToBottom or FlowDirection.BottomToTop;
        var lineHeight = double.IsFinite(ContentLineHeight) && ContentLineHeight > 0 ? ContentLineHeight : FontSize;
        var width = isVertical ? Math.Max(FontSize, lineHeight) : Math.Max(FontSize, text.Length * FontSize);
        var height = isVertical ? Math.Max(FontSize, text.Length * lineHeight + FontSize) : Math.Max(FontSize, lineHeight + FontSize);
        XNamespace ns = "http://www.w3.org/2000/svg";
        var textElement = new XElement(ns + "text",
            new XAttribute("x", "0"),
            new XAttribute("y", FontSize.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("font-family", TypefaceName),
            new XAttribute("font-size", FontSize.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("fill", $"#{ColorfulLaneColor.Color.R:X2}{ColorfulLaneColor.Color.G:X2}{ColorfulLaneColor.Color.B:X2}"));

        if (isVertical)
        {
            var first = true;
            foreach (var character in text)
            {
                textElement.Add(new XElement(ns + "tspan",
                    new XAttribute("x", "0"),
                    new XAttribute("dy", first ? "0" : lineHeight.ToString(CultureInfo.InvariantCulture)),
                    character));
                first = false;
            }
        }
        else
        {
            if (ContentFlowDirection == FlowDirection.RightToLeft)
                textElement.SetAttributeValue("direction", "rtl");
            textElement.Value = text;
        }

        var document = new XDocument(new XElement(ns + "svg",
            new XAttribute("width", width.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("height", height.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("viewBox", FormattableString.Invariant($"0 0 {width} {height}")),
            textElement));
        ApplySvgContent(document.ToString(SaveOptions.DisableFormatting));
#endif
    }

    public override void Copy(OngekiObjectBase fromObj)
    {
        base.Copy(fromObj);
        if (fromObj is not SvgStringPrefab from)
            return;

        Content = from.Content;
        TypefaceName = from.TypefaceName;
        ContentLineHeight = from.ContentLineHeight;
        FontSize = from.FontSize;
        ContentFlowDirection = from.ContentFlowDirection;
    }
}
