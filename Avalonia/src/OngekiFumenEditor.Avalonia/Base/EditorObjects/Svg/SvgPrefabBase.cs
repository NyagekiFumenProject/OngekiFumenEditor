using OngekiFumenEditor.Avalonia.Base.Attributes;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Kernel.CurveInterpolater;
using OngekiFumenEditor.Avalonia.Kernel.CurveInterpolater.OgkrImpl.Factory;
using OngekiFumenEditor.Avalonia.Utils;
using OngekiFumenEditor.Avalonia.Base;

namespace OngekiFumenEditor.Avalonia.Base.EditorObjects.Svg;

public abstract class SvgPrefabBase : OngekiMovableObjectBase
{
    private ICurveInterpolaterFactory curveInterpolaterFactory = XGridLimitedCurveInterpolaterFactory.Default;
    public ICurveInterpolaterFactory CurveInterpolaterFactory
    {
        get => curveInterpolaterFactory;
        set => SetProperty(ref curveInterpolaterFactory, value);
    }

    private bool isForceColorful;
    public bool IsForceColorful
    {
        get => isForceColorful;
        set
        {
            if (SetProperty(ref isForceColorful, value))
                RebuildGeometry();
        }
    }

    private ColorId colorfulLaneColor = ColorIdConst.Yuzu;
    public ColorId ColorfulLaneColor
    {
        get => colorfulLaneColor;
        set
        {
            if (SetProperty(ref colorfulLaneColor, value))
                RebuildGeometry();
        }
    }

    private RangeValue colorfulLaneBrightness = RangeValue.Create(-3, 3, 0);
    [ObjectPropertyBrowserSingleSelectedOnly]
    public RangeValue ColorfulLaneBrightness
    {
        get => colorfulLaneBrightness;
        set
        {
            this.RegisterOrUnregisterPropertyChangeEvent(colorfulLaneBrightness, value);
            if (SetProperty(ref colorfulLaneBrightness, value))
                RebuildGeometry();
        }
    }

    private RangeValue rotation = RangeValue.Create(-180, 180f, 0f);
    [ObjectPropertyBrowserSingleSelectedOnly]
    public RangeValue Rotation
    {
        get => rotation;
        set
        {
            this.RegisterOrUnregisterPropertyChangeEvent(rotation, value);
            if (SetProperty(ref rotation, value))
                RebuildGeometry();
        }
    }

    private RangeValue offsetX = RangeValue.CreateNormalized(0.5f);
    [ObjectPropertyBrowserSingleSelectedOnly]
    public RangeValue OffsetX
    {
        get => offsetX;
        set
        {
            this.RegisterOrUnregisterPropertyChangeEvent(offsetX, value);
            if (SetProperty(ref offsetX, value))
                RebuildGeometry();
        }
    }

    private RangeValue colorSimilar = RangeValue.Create(1, 1000, 600);
    [ObjectPropertyBrowserSingleSelectedOnly]
    public RangeValue ColorSimilar
    {
        get => colorSimilar;
        set
        {
            this.RegisterOrUnregisterPropertyChangeEvent(colorSimilar, value);
            if (SetProperty(ref colorSimilar, value))
                RebuildGeometry();
        }
    }

    private RangeValue offsetY = RangeValue.CreateNormalized(0.5f);
    [ObjectPropertyBrowserSingleSelectedOnly]
    public RangeValue OffsetY
    {
        get => offsetY;
        set
        {
            this.RegisterOrUnregisterPropertyChangeEvent(offsetY, value);
            if (SetProperty(ref offsetY, value))
                RebuildGeometry();
        }
    }

    private bool enableColorfulLaneSimilar = true;
    public bool EnableColorfulLaneSimilar
    {
        get => enableColorfulLaneSimilar;
        set
        {
            if (SetProperty(ref enableColorfulLaneSimilar, value))
                RebuildGeometry();
        }
    }

    private bool showOriginColor;
    public bool ShowOriginColor
    {
        get => showOriginColor;
        set
        {
            if (SetProperty(ref showOriginColor, value))
                RebuildGeometry();
        }
    }

    private float scale = 1;
    public float Scale
    {
        get => scale;
        set
        {
            if (SetProperty(ref scale, value))
                RebuildGeometry();
        }
    }

    private RangeValue opacity = RangeValue.CreateNormalized(1);
    [ObjectPropertyBrowserSingleSelectedOnly]
    public RangeValue Opacity
    {
        get => opacity;
        set
        {
            this.RegisterOrUnregisterPropertyChangeEvent(opacity, value);
            if (SetProperty(ref opacity, value))
                RebuildGeometry();
        }
    }

    private RangeValue tolerance = RangeValue.Create(0.001f, 20f, 20f);
    [ObjectPropertyBrowserSingleSelectedOnly]
    public RangeValue Tolerance
    {
        get => tolerance;
        set
        {
            this.RegisterOrUnregisterPropertyChangeEvent(tolerance, value);
            if (SetProperty(ref tolerance, value))
                RebuildGeometry();
        }
    }

    private object rawSvgContent;

    // Placeholder for migrated non-UI stage: keep field shape for downstream logic.
    public object ProcessingDrawingGroup { get; private set; }

    protected SvgPrefabBase()
    {
        Tolerance = Tolerance;
        Opacity = Opacity;
        Rotation = Rotation;
        OffsetX = OffsetX;
        OffsetY = OffsetY;
        ColorSimilar = ColorSimilar;
        ColorfulLaneBrightness = ColorfulLaneBrightness;
    }

    public override void Copy(OngekiObjectBase fromObj)
    {
        base.Copy(fromObj);
        if (fromObj is not SvgPrefabBase from)
            return;

        Tolerance = from.Tolerance;
        Opacity = from.Opacity;
        Rotation = from.Rotation;
        OffsetX = from.OffsetX;
        OffsetY = from.OffsetY;
        ColorSimilar = from.ColorSimilar;
        ShowOriginColor = from.ShowOriginColor;
        IsForceColorful = from.IsForceColorful;
        CurveInterpolaterFactory = from.CurveInterpolaterFactory;
        ColorfulLaneColor = from.ColorfulLaneColor;
        EnableColorfulLaneSimilar = from.EnableColorfulLaneSimilar;
    }

    protected void ApplySvgContent(object svgContent)
    {
        rawSvgContent = svgContent;
        RebuildGeometry();
    }

    public void CleanGeometry()
    {
        rawSvgContent = default;
        ProcessingDrawingGroup = default;
    }

    public void RebuildGeometry()
    {
        // SVG rendering pipeline is intentionally downgraded during migration.
        // Keep object state for parser/serializer compatibility.
        ProcessingDrawingGroup = rawSvgContent;
    }
}

