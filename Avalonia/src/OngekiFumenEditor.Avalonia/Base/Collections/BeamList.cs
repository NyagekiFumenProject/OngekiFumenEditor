using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.ConnectableObject;

namespace OngekiFumenEditor.Avalonia.Base.Collections
{
	public class BeamList : ConnectableObjectList<BeamStart, ConnectableChildObjectBase>
	{
		public void Add(IBeamObject beam)
		{
			Add(beam as ConnectableObjectBase);
		}

		public void Remove(IBeamObject beam)
		{
			Remove(beam as ConnectableObjectBase);
		}
	}
}
