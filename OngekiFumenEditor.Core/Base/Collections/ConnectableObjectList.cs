using OngekiFumenEditor.Base.Collections.Base;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace OngekiFumenEditor.Base.Collections
{
    public class ConnectableObjectList<START_TYPE, CHILD_TYPE> : IReadOnlyCollection<START_TYPE>
        where START_TYPE : ConnectableStartObject
        where CHILD_TYPE : ConnectableChildObjectBase
    {
        private readonly IntervalTreeWrapper<TGrid, START_TYPE> startObjects = new(
            x => new() { Min = x.MinTGrid, Max = x.MaxTGrid },
            true,
            nameof(ConnectableStartObject.MinTGrid),
            nameof(ConnectableStartObject.MaxTGrid)
        );

        public int Count => startObjects.Count;

        public IEnumerator<START_TYPE> GetEnumerator() => startObjects.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public int GetNewRecordId()
        {
            return startObjects.Count > 0 ? startObjects.Select(x => x.RecordId).Max() + 1 : 0;
        }

        public void Add(ConnectableObjectBase obj)
        {
            if (obj is START_TYPE startObject)
            {
                if (startObject.RecordId < 0)
                    startObject.RecordId = GetNewRecordId();

                startObjects.Add(startObject);
                return;
            }

            if (obj is CHILD_TYPE child && startObjects.FirstOrDefault(x => x.RecordId == child.RecordId) is START_TYPE start)
            {
                start.AddChildObject(child);
                child.PropertyChanged += OnChildPropertyChanged;
            }
        }

        public void Remove(ConnectableObjectBase obj)
        {
            if (obj is START_TYPE startObj)
            {
                startObjects.Remove(startObj);
                return;
            }

            if (obj is CHILD_TYPE child)
            {
                child.PropertyChanged -= OnChildPropertyChanged;
                if (startObjects.FirstOrDefault(x => x.RecordId == child.RecordId) is START_TYPE start)
                    start.RemoveChildObject(child);
            }
        }

        private void OnChildPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ConnectableObjectBase.RecordId) || sender is not CHILD_TYPE child)
                return;

            if (startObjects.FirstOrDefault(x => x.RecordId == child.RecordId) is START_TYPE start)
            {
                child.ReferenceStartObject?.RemoveChildObject(child);
                start.AddChildObject(child);
            }
            else if (child.ReferenceStartObject is START_TYPE prevStart)
            {
                child.RecordId = prevStart.RecordId;
            }
        }

        public IEnumerable<START_TYPE> GetVisibleStartObjects(TGrid min, TGrid max)
        {
            return startObjects.QueryInRange(min, max);
        }

        public bool Contains(START_TYPE o)
        {
            return startObjects.FastContains(o);
        }
    }
}
