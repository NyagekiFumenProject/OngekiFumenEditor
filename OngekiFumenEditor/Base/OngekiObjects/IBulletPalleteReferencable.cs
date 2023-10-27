using System.ComponentModel;

namespace OngekiFumenEditor.Base.OngekiObjects
{
	public interface IBulletPalleteReferencable : IDisplayableObject, IHorizonPositionObject, ITimelineObject, INotifyPropertyChanged
	{
		BulletPallete ReferenceBulletPallete { get; set; }
	}
}
