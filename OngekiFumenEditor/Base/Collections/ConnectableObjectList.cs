using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Utils;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace OngekiFumenEditor.Base.Collections
{
    public class ConnectableObjectList<START_TYPE, CHILD_TYPE> : IEnumerable<START_TYPE> where START_TYPE : ConnectableStartObject where CHILD_TYPE : ConnectableChildObjectBase
    {
        private List<START_TYPE> startObjects = new();

        public IEnumerator<START_TYPE> GetEnumerator() => startObjects.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Add(ConnectableObjectBase obj)
        {
            if (obj is START_TYPE startObject)
            {
                //wallStart.PropertyChanged += OnBeamStartPropertyChanged;
                if (startObject.RecordId < 0)
                    startObject.RecordId = startObjects.Count + 1;
                startObjects.Add(startObject);
            }
            else if (obj is CHILD_TYPE child)
            {
                child.PropertyChanged += OnBeamChildPropertyChanged;
                if (startObjects.FirstOrDefault(x => x.RecordId == child.RecordId) is START_TYPE start)
                    start.AddChildObject(child);
            }
        }

        public void Remove(ConnectableObjectBase obj)
        {
            if (obj is START_TYPE startObj)
            {
                //wallStart.PropertyChanged -= OnBeamStartPropertyChanged;
                startObjects.Remove(startObj);
            }
            else if (obj is CHILD_TYPE child)
            {
                child.PropertyChanged -= OnBeamChildPropertyChanged;
                if (startObjects.FirstOrDefault(x => x.RecordId == child.RecordId) is START_TYPE start)
                    start.RemoveChildObject(child);
            }
        }

        private void OnBeamStartPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ConnectableObjectBase.RecordId) || sender is not START_TYPE startObject)
                return;

            if (startObjects.FirstOrDefault(x => x.RecordId == startObject.RecordId) is START_TYPE s)
            {
                startObjects.Remove(s);
                Log.LogDebug($"migrate recId {s.RecordId} -> {startObject.RecordId}");
            }

            startObjects.Add(startObject);
        }

        private void OnBeamChildPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ConnectableObjectBase.RecordId) || sender is not CHILD_TYPE child)
                return;

            if (startObjects.FirstOrDefault(x => x.RecordId == child.RecordId) is START_TYPE start)
            {
                child.ReferenceStartObject?.RemoveChildObject(child);
                start.AddChildObject(child);
                Log.LogDebug($"Changed child recId {child.ReferenceStartObject?.RecordId} -> {child.RecordId}");
            }
            else
            {
                if (child.ReferenceStartObject is START_TYPE prevStart)
                    child.RecordId = prevStart.RecordId;//set failed and roll back
                Log.LogDebug($"Can't change child recId {child.ReferenceStartObject?.RecordId} -> {child.RecordId}");
            }
        }
    }
}
