using OngekiFumenEditor.Avalonia.Base;

namespace OngekiFumenEditor.Avalonia.Base.EditorObjects.Svg;

public class SvgStringPrefab : SvgPrefabBase
{
    public enum FlowDirection
    {
        LeftToRight,
        RightToLeft,
        TopToBottom,
        BottomToTop
    }

    public const string CommandName = "[SVG_STR]";
    public override string IDShortName => CommandName;

    private string content;
    public string Content
    {
        get => content;
        set
        {
            if (SetProperty(ref content, value))
                RebuildSvgContent();
        }
    }

    private FlowDirection contentFlowDirection = FlowDirection.LeftToRight;
    public FlowDirection ContentFlowDirection
    {
        get => contentFlowDirection;
        set
        {
            if (SetProperty(ref contentFlowDirection, value))
                RebuildSvgContent();
        }
    }

    private double fontSize = 16;
    public double FontSize
    {
        get => fontSize;
        set
        {
            if (SetProperty(ref fontSize, value))
                RebuildSvgContent();
        }
    }

    private double contentLineHeight = 16;
    public double ContentLineHeight
    {
        get => contentLineHeight;
        set
        {
            if (SetProperty(ref contentLineHeight, value))
                RebuildSvgContent();
        }
    }

    private string typefaceName = "Tahoma";
    public string TypefaceName
    {
        get => typefaceName;
        set
        {
            if (SetProperty(ref typefaceName, value))
                RebuildSvgContent();
        }
    }

    public void RebuildSvgContent()
    {
        CleanGeometry();

        if (string.IsNullOrWhiteSpace(Content))
            return;

        // Text-to-geometry conversion is not migrated yet. Keep source text for compatibility.
        var normalizedContent = ContentFlowDirection switch
        {
            FlowDirection.RightToLeft => new string(Content.Reverse().ToArray()),
            FlowDirection.TopToBottom => string.Join(Environment.NewLine, Content.Select(x => x)),
            FlowDirection.BottomToTop => string.Join(Environment.NewLine, Content.Reverse()),
            _ => Content
        };

        ApplySvgContent(new
        {
            Text = normalizedContent,
            TypefaceName,
            FontSize,
            ContentLineHeight,
            ColorfulLaneColor,
        });
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

