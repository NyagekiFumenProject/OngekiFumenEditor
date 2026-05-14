using OngekiFumenEditor.Core.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Core.Base.OngekiObjects.ConnectableObject;

namespace OngekiFumenEditor.Core.Base.Collections
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
