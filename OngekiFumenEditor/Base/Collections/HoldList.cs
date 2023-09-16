using OngekiFumenEditor.Base.Collections.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Base.OngekiObjects.Wall;
using OngekiFumenEditor.Base.OngekiObjects.Wall.Base;
using OngekiFumenEditor.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.Collections
{
    public class HoldList : IEnumerable<Hold>
    {
        private IntervalTreeWrapper<TGrid, Hold> startObjects = new(
            x => new() { Min = x.TGrid, Max = x.EndTGrid },
            true,
            nameof(Hold.TGrid),
            nameof(Hold.EndTGrid)
            );

        public IEnumerator<Hold> GetEnumerator() => startObjects.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Add(Hold obj)
        {
            startObjects.Add(obj);
        }

        public void Remove(Hold obj)
        {
            startObjects.Remove(obj);
        }

        public IEnumerable<Hold> GetVisibleStartObjects(TGrid min, TGrid max)
        {
            return startObjects.QueryInRange(min, max);
        }

        public bool Contains(Hold o)
        {
            return startObjects.FastContains(o);
        }
    }
}
