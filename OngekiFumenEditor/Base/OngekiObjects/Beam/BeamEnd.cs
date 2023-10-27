using OngekiFumenEditor.Base.Attributes;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Utils;

namespace OngekiFumenEditor.Base.OngekiObjects.Beam
{
	public class BeamEnd : ConnectableEndObject, IBeamObject
	{
		public override string IDShortName => "BME";

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
			get { return obliqueSourceXGrid ?? (ReferenceStartObject as IBeamObject)?.ObliqueSourceXGridOffset; }
			set
			{
				this.RegisterOrUnregisterPropertyChangeEvent(obliqueSourceXGrid, value);
				obliqueSourceXGrid = value;
				NotifyOfPropertyChange(() => ObliqueSourceXGridOffset);
			}
		}
	}
}
