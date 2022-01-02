using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Wall;
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
    public class ConnectableObjectList<START_TYPE, BASE_TYPE> : IEnumerable<START_TYPE> where START_TYPE : ConnectableStartObject where BASE_TYPE : ConnectableObjectBase
    {
        private List<START_TYPE> walls = new();

        public IEnumerator<START_TYPE> GetEnumerator() => walls.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Add(BASE_TYPE wall)
        {
            if (wall is START_TYPE wallStart)
            {
                //wallStart.PropertyChanged += OnBeamStartPropertyChanged;
                if (wallStart.RecordId < 0)
                    wallStart.RecordId = walls.Count + 1;
                walls.Add(wallStart);
            }
            else if (wall is ConnectableChildObjectBase wallChild)
            {
                wallChild.PropertyChanged += OnBeamChildPropertyChanged;
                if (walls.FirstOrDefault(x => x.RecordId == wallChild.RecordId) is START_TYPE start)
                    start.AddChildWallObject(wallChild);
            }
        }

        public void Remove(BASE_TYPE beam)
        {
            if (beam is START_TYPE wallStart)
            {
                //wallStart.PropertyChanged -= OnBeamStartPropertyChanged;
                walls.Remove(wallStart);
            }
            else if (beam is ConnectableChildObjectBase wallChild)
            {
                wallChild.PropertyChanged -= OnBeamChildPropertyChanged;
                if (walls.FirstOrDefault(x => x.RecordId == wallChild.RecordId) is START_TYPE start)
                    start.RemoveChildWallObject(wallChild);
            }
        }

        private void OnBeamStartPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ConnectableObjectBase.RecordId) || sender is not START_TYPE wallStart)
                return;

            if (walls.FirstOrDefault(x => x.RecordId == wallStart.RecordId) is START_TYPE s)
            {
                walls.Remove(s);
                Log.LogDebug($"migrate recId {s.RecordId} -> {wallStart.RecordId}");
            }

            walls.Add(wallStart);
        }

        private void OnBeamChildPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ConnectableObjectBase.RecordId) || sender is not ConnectableChildObjectBase wallChild)
                return;

            if (walls.FirstOrDefault(x => x.RecordId == wallChild.RecordId) is START_TYPE newRefBeam)
            {
                wallChild.ReferenceStartObject?.RemoveChildWallObject(wallChild);
                newRefBeam.AddChildWallObject(wallChild);
                Log.LogDebug($"Changed child recId {wallChild.ReferenceStartObject?.RecordId} -> {wallChild.RecordId}");
            }
            else
            {
                if (wallChild.ReferenceStartObject is START_TYPE prevRefBeam)
                    wallChild.RecordId = prevRefBeam.RecordId;//set failed and roll back
                Log.LogDebug($"Can't change child recId {wallChild.ReferenceStartObject?.RecordId} -> {wallChild.RecordId}");
            }
        }
    }
}
