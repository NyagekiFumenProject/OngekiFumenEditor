using OngekiFumenEditor.Base.OngekiObjects;
using System.ComponentModel;

namespace OngekiFumenEditor.Base
{
	public interface IBulletPalleteReferencable : IDisplayableObject, IHorizonPositionObject, ITimelineObject, INotifyPropertyChanged, IBulletPalleteChangable
	{
		BulletPallete ReferenceBulletPallete { get; set; }
	}
}
