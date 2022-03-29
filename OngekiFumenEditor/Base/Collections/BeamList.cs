using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.Collections
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
