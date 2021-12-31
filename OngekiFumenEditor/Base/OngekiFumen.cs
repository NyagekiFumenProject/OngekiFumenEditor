using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Base.OngekiObjects.Collections;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base
{
    public class OngekiFumen
    {
        public FumenMetaInfo MetaInfo { get; } = new();
        public List<BulletPalleteList> BulletPalleteList { get; } = new();
        public List<Bell> Bells { get; } = new();
        public List<Flick> Flicks { get; } = new();
        public BpmList BpmList { get; } = new();
        public List<MeterChange> MeterChanges { get; } = new();
        public List<EnemySet> EnemySets { get; } = new();
        public BeamList Beams { get; } = new();

        #region Overload Methods
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddObjects(params OngekiObjectBase[] objs) => AddObjects(objs);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddObjects(IEnumerable<OngekiObjectBase> objs)
        {
            foreach (var item in objs)
            {
                AddObject(item);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveObjects(params OngekiObjectBase[] objs) => RemoveObjects(objs);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveObjects(IEnumerable<OngekiObjectBase> objs)
        {
            foreach (var item in objs)
            {
                RemoveObject(item);
            }
        }
        #endregion

        public void Setup()
        {
            Bells.Sort();
            BpmList.Sort();
            MeterChanges.Sort();
            EnemySets.Sort();
        }

        public void AddObject(OngekiObjectBase obj)
        {
            if (obj is Bell bel)
            {
                Bells.Add(bel);
            }
            else if (obj is BPMChange bpm)
            {
                BpmList.Add(bpm);
            }
            else if (obj is MeterChange met)
            {
                MeterChanges.Add(met);
            }
            else if (obj is EnemySet est)
            {
                EnemySets.Add(est);
            }
            else if (obj is BulletPalleteList bpl)
            {
                BulletPalleteList.Add(bpl);
            }
            else if (obj is Flick flick)
            {
                Flicks.Add(flick);
            }
            else if (obj is BeamBase beam)
            {
                Beams.Add(beam);
            }
            else
            {
                Log.LogWarn($"add-in list target not found, object type : {obj?.GetType()?.Name}");
                return;
            }
        }

        public void RemoveObject(OngekiObjectBase obj)
        {
            if (obj is Bell bel)
            {
                Bells.Remove(bel);
            }
            else if (obj is BPMChange bpm)
            {
                BpmList.Remove(bpm);
            }
            else if (obj is MeterChange met)
            {
                MeterChanges.Remove(met);
            }
            else if (obj is EnemySet est)
            {
                EnemySets.Remove(est);
            }
            else if (obj is BulletPalleteList bpl)
            {
                BulletPalleteList.Remove(bpl);
            }
            else if (obj is Flick flick)
            {
                Flicks.Remove(flick);
            }
            else if (obj is BeamBase beam)
            {
                Beams.Remove(beam);
            }
            else
            {
                Log.LogWarn($"delete list target not found, object type : {obj?.GetType()?.Name}");
                return;
            }
        }


        public IEnumerable<IDisplayableObject> GetAllDisplayableObjects()
        {
            var first = Enumerable.Empty<IDisplayableObject>()
                .Concat(Bells)
                .Concat(Flicks)
                .Concat(Beams);

            return first.SelectMany(x => x.GetDisplayableObjects());
        }

        public string Serialize()
        {
            var sb = new StringBuilder();

            #region HEADER
            sb.AppendLine("[HEADER]");
            sb.AppendLine(MetaInfo.Serialize(this));
            #endregion

            #region B_PALETTE
            sb.AppendLine();
            sb.AppendLine("[B_PALETTE]");

            foreach (var bpl in BulletPalleteList.OrderBy(x => x.StrID))
                sb.AppendLine(bpl.Serialize(this));
            #endregion

            #region COMPOSITION
            sb.AppendLine();
            sb.AppendLine("[COMPOSITION]");

            foreach (var o in BpmList.OrderBy(x => x.TGrid).Where(x=>x != BpmList.FirstBpm))
                sb.AppendLine(o.Serialize(this));
            sb.AppendLine();
            foreach (var o in MeterChanges.OrderBy(x => x.TGrid))
                sb.AppendLine(o.Serialize(this));
            foreach (var o in EnemySets.OrderBy(x => x.TGrid))
                sb.AppendLine(o.Serialize(this));
            #endregion

            #region BEAM
            sb.AppendLine();
            sb.AppendLine("[BEAM]");
            foreach (var u in Beams.OrderBy(x => x.RecordId))
                sb.AppendLine(u.Serialize(this));
            #endregion

            #region BELL
            sb.AppendLine();
            sb.AppendLine("[BELL]");

            foreach (var u in Bells.OrderBy(x => x.TGrid))
                sb.AppendLine(u.Serialize(this));
            #endregion

            #region FLICK
            sb.AppendLine();
            sb.AppendLine("[FLICK]");

            foreach (var u in Flicks.OrderBy(x => x.TGrid))
                sb.AppendLine(u.Serialize(this));
            #endregion

            return sb.ToString();
        }
    }
}
