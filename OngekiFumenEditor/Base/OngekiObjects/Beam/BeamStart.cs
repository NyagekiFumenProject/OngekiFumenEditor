using OngekiFumenEditor.Base.Attributes;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Utils;

namespace OngekiFumenEditor.Base.OngekiObjects.Beam
{
	public class BeamStart : ConnectableStartObject, IBeamObject
	{
		public const int LEAD_IN_DURATION = 1000;
		public const int LEAD_IN_BODY_DURATION = 250;
		public const int LEAD_OUT_DURATION = 250;

		public bool IsObliqueBeam => ObliqueSourceXGridOffset is not null;

		public override string IDShortName => IsObliqueBeam ? "OBS" : "BMS";

		private int widthId = 2;
		public int WidthId
		{
			get => widthId;
			set => Set(ref widthId, value);
		}

		private XGrid obliqueSourceXGrid = null;
		[ObjectPropertyBrowserAllowSetNull]
		public XGrid ObliqueSourceXGridOffset
		{
			get { return obliqueSourceXGrid; }
			set
			{
				this.RegisterOrUnregisterPropertyChangeEvent(obliqueSourceXGrid, value);
				obliqueSourceXGrid = value;
				NotifyOfPropertyChange(() => ObliqueSourceXGridOffset);
			}
		}

		public override LaneType LaneType => LaneType.Beam;

		public override ConnectableChildObjectBase CreateChildObject() => new BeamNext();
	}
}
