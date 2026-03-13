using OngekiFumenEditor.Avalonia.Base.Attributes;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.ConnectableObject;

namespace OngekiFumenEditor.Avalonia.Base.OngekiObjects.Beam;

public class BeamStart : ConnectableStartObject, IBeamObject
{
    public const float LEAD_IN_DURATION_FRAME = 50f;
    public const int LEAD_IN_BODY_DURATION = 250;
    public const int LEAD_OUT_DURATION = 250;

    private XGrid obliqueSourceXGrid;

    private WidthId widthId = WidthIdConst.Id_1;

    public bool IsObliqueBeam => ObliqueSourceXGridOffset is not null;

    public override string IDShortName => IsObliqueBeam ? "OBS" : "BMS";

    public override LaneType LaneType => LaneType.Beam;

    public WidthId WidthId
    {
        get => widthId;
        set => SetProperty(ref widthId, value);
    }

    [ObjectPropertyBrowserAllowSetNull]
    public XGrid ObliqueSourceXGridOffset
    {
        get => obliqueSourceXGrid;
        set
        {
            this.RegisterOrUnregisterPropertyChangeEvent(obliqueSourceXGrid, value);
            obliqueSourceXGrid = value;
            OnPropertyChanged();
        }
    }

    public override ConnectableChildObjectBase CreateChildObject()
    {
        return new BeamNext();
    }

    public override void Dispose()
    {
        base.Dispose();
        ObliqueSourceXGridOffset = default;
    }
}