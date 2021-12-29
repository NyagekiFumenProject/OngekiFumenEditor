using OngekiFumenEditor.Base.OngekiObjects.Beam;
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
    public class BeamList : IEnumerable<BeamStart>
    {
        private List<BeamStart> beams = new();

        public IEnumerator<BeamStart> GetEnumerator() => beams.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Add(BeamBase beam)
        {
            if (beam is BeamStart beamStart)
            {
                beamStart.PropertyChanged += OnBeamStartPropertyChanged;
                if (beamStart.RecordId < 0)
                    beamStart.RecordId = beams.Count + 1;
                beams.Add(beamStart);
            }
            else if (beam is BeamChildBase beamChild)
            {
                beamChild.PropertyChanged += OnBeamChildPropertyChanged;
                if (beams.FirstOrDefault(x => x.RecordId == beamChild.RecordId) is BeamStart start)
                    start.AddChildBeamObject(beamChild);
            }
        }

        public void Remove(BeamBase beam)
        {
            if (beam is BeamStart beamStart)
            {
                beamStart.PropertyChanged -= OnBeamStartPropertyChanged;
                beams.Remove(beamStart);
            }
            else if (beam is BeamChildBase beamChild)
            {
                beamChild.PropertyChanged -= OnBeamChildPropertyChanged;
                if (beams.FirstOrDefault(x => x.RecordId == beamChild.RecordId) is BeamStart start)
                    start.RemoveChildBeamObject(beamChild);
            }
        }

        private void OnBeamStartPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(BeamBase.RecordId) || sender is not BeamStart beamStart)
                return;

            if (beams.FirstOrDefault(x => x == beamStart) is BeamStart s)
            {
                beams.Remove(s);
                Log.LogDebug($"migrate recId {s.RecordId} -> {beamStart.RecordId}");
            }

            beams.Add(beamStart);
        }

        private void OnBeamChildPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(BeamBase.RecordId) || sender is not BeamChildBase beamChild)
                return;

            if (beams.FirstOrDefault(x => x.RecordId == beamChild.RecordId) is BeamStart newRefBeam)
            {
                beamChild.ReferenceBeam?.RemoveChildBeamObject(beamChild);
                newRefBeam.AddChildBeamObject(beamChild);
                Log.LogDebug($"Changed child recId {beamChild.ReferenceBeam?.RecordId} -> {beamChild.RecordId}");
            }
            else
            {
                if (beamChild.ReferenceBeam is BeamStart prevRefBeam)
                    beamChild.RecordId = prevRefBeam.RecordId;//set failed and roll back
                Log.LogDebug($"Can't change child recId {beamChild.ReferenceBeam?.RecordId} -> {beamChild.RecordId}");
            }
        }
    }
}
