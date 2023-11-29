using System.ComponentModel;

namespace OngekiFumenEditor.Base.OngekiObjects
{
	public interface IBulletPalleteReferencable : IDisplayableObject, IHorizonPositionObject, ITimelineObject, INotifyPropertyChanged, IBulletPalleteChangable
	{
		BulletPallete ReferenceBulletPallete { get; set; }
	}
}
