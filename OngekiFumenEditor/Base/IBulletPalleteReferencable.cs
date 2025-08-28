using OngekiFumenEditor.Base.OngekiObjects;
using System.ComponentModel;

namespace OngekiFumenEditor.Base
{
	public interface IBulletPalleteReferencable : IDisplayableObject, IHorizonPositionObject, ITimelineObject, INotifyPropertyChanged
	{
		BulletPallete ReferenceBulletPallete { get; set; }
	}
}
