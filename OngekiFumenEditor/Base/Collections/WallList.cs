using OngekiFumenEditor.Base.OngekiObjects.Beam;
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
    public class WallList : IEnumerable<WallStart>
    {
        private List<WallStart> walls = new();

        public IEnumerator<WallStart> GetEnumerator() => walls.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Add(WallBase wall)
        {
            if (wall is WallStart wallStart)
            {
                //wallStart.PropertyChanged += OnBeamStartPropertyChanged;
                if (wallStart.RecordId < 0)
                    wallStart.RecordId = walls.Count + 1;
                walls.Add(wallStart);
            }
            else if (wall is WallChildBase wallChild)
            {
                wallChild.PropertyChanged += OnBeamChildPropertyChanged;
                if (walls.FirstOrDefault(x => x.RecordId == wallChild.RecordId) is WallStart start)
                    start.AddChildWallObject(wallChild);
            }
        }

        public void Remove(WallBase beam)
        {
            if (beam is WallStart wallStart)
            {
                //wallStart.PropertyChanged -= OnBeamStartPropertyChanged;
                walls.Remove(wallStart);
            }
            else if (beam is WallChildBase wallChild)
            {
                wallChild.PropertyChanged -= OnBeamChildPropertyChanged;
                if (walls.FirstOrDefault(x => x.RecordId == wallChild.RecordId) is WallStart start)
                    start.RemoveChildWallObject(wallChild);
            }
        }

        private void OnBeamStartPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(WallBase.RecordId) || sender is not WallStart wallStart)
                return;

            if (walls.FirstOrDefault(x => x.RecordId == wallStart.RecordId) is WallStart s)
            {
                walls.Remove(s);
                Log.LogDebug($"migrate recId {s.RecordId} -> {wallStart.RecordId}");
            }

            walls.Add(wallStart);
        }

        private void OnBeamChildPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(WallBase.RecordId) || sender is not WallChildBase wallChild)
                return;

            if (walls.FirstOrDefault(x => x.RecordId == wallChild.RecordId) is WallStart newRefBeam)
            {
                wallChild.ReferenceWall?.RemoveChildWallObject(wallChild);
                newRefBeam.AddChildWallObject(wallChild);
                Log.LogDebug($"Changed child recId {wallChild.ReferenceWall?.RecordId} -> {wallChild.RecordId}");
            }
            else
            {
                if (wallChild.ReferenceWall is WallStart prevRefBeam)
                    wallChild.RecordId = prevRefBeam.RecordId;//set failed and roll back
                Log.LogDebug($"Can't change child recId {wallChild.ReferenceWall?.RecordId} -> {wallChild.RecordId}");
            }
        }
    }
}
