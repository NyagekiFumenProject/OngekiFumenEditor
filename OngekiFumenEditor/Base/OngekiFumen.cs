using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Base.OngekiObjects.Collections;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Base.OngekiObjects.Wall;
using OngekiFumenEditor.Base.OngekiObjects.Wall.Base;
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
        public BulletPalleteList BulletPalleteList { get; } = new();
        public List<Bell> Bells { get; } = new();
        public List<Flick> Flicks { get; } = new();
        public List<Bullet> Bullets { get; } = new();
        public List<ClickSE> ClickSEs { get; } = new();
        public BpmList BpmList { get; } = new();
        public LaneList Lanes { get; } = new();
        public List<MeterChange> MeterChanges { get; } = new();
        public List<EnemySet> EnemySets { get; } = new();
        public BeamList Beams { get; } = new();
        public List<Tap> Taps { get; } = new();
        public HoldList Holds { get; } = new();

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
            else if (obj is BulletPallete bpl)
            {
                BulletPalleteList.AddPallete(bpl);
            }
            else if (obj is Flick flick)
            {
                Flicks.Add(flick);
            }
            else if (obj is BeamBase beam)
            {
                Beams.Add(beam);
            }
            else if (obj is Bullet bullet)
            {
                Bullets.Add(bullet);
            }
            else if (obj is ClickSE clickSE)
            {
                ClickSEs.Add(clickSE);
            }
            else if (obj is Tap tap)
            {
                Taps.Add(tap);
            }
            else if (obj is Hold hold)
            {
                Holds.Add(hold);
            }
            else if (obj is HoldEnd holdEnd)
            {
                Holds.Add(holdEnd);
            }
            else if (obj switch
            {
                LaneStartBase or LaneEndBase or LaneNextBase => obj,
                _ => null
            } is ConnectableObjectBase lane)
            {
                Lanes.Add(lane);
            }
            else
            {
                Log.LogWarn($"add-in list target not found, object type : {obj?.GetType()?.GetTypeName()}");
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
            else if (obj is BulletPallete bpl)
            {
                BulletPalleteList.RemovePallete(bpl);
            }
            else if (obj is Flick flick)
            {
                Flicks.Remove(flick);
            }
            else if (obj is BeamBase beam)
            {
                Beams.Remove(beam);
            }
            else if (obj is Bullet bullet)
            {
                Bullets.Remove(bullet);
            }
            else if (obj is ClickSE clickSE)
            {
                ClickSEs.Remove(clickSE);
            }
            else if (obj is Tap tap)
            {
                Taps.Remove(tap);
            }
            else if (obj is Hold hold)
            {
                Holds.Remove(hold);
            }
            else if (obj is HoldEnd holdEnd)
            {
                Holds.Remove(holdEnd);
            }
            else if (obj switch
            {
                LaneStartBase or LaneEndBase or LaneNextBase => obj,
                _ => null
            } is ConnectableObjectBase lane)
            {
                Lanes.Remove(lane);
            }
            else
            {
                Log.LogWarn($"delete list target not found, object type : {obj?.GetType()?.GetTypeName()}");
                return;
            }
        }

        public IEnumerable<IDisplayableObject> GetAllDisplayableObjects()
        {
            var first = Enumerable.Empty<IDisplayableObject>()
                .Concat(Bells)
                .Concat(Flicks)
                .Concat(MeterChanges)
                .Concat(BpmList.Skip(1)) //not show first bpm
                .Concat(ClickSEs)
                .Concat(EnemySets)
                .Concat(Bullets)
                .Concat(Lanes)
                .Concat(Taps)
                .Concat(Holds)
                .Concat(Beams);

            return first.SelectMany(x => x.GetDisplayableObjects());
        }
    }
}
