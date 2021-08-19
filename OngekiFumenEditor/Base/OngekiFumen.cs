using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base
{
    public class OngekiFumen
    {
        public FumenMetaInfo MetaInfo { get; set; }
        public List<BulletPalleteList> BulletPalleteList { get; set; } = new List<BulletPalleteList>();
        public List<Bell> Bells { get; set; } = new List<Bell>();
        public List<BPM> BPMs { get; set; } = new List<BPM>();
        public List<MeterChange> MeterChanges { get; set; } = new List<MeterChange>();
        public List<EnemySet> EnemySets { get; set; } = new List<EnemySet>();

        public void AddObject(IOngekiObject obj)
        {
            if (obj is Bell bell)
            {
                Bells.Add(bell);
            }
            else if(obj is BPM bpm){
                BPMs.Add(bpm);
            }
            else
            {
                Log.LogWarn($"add-in list target not found, object type : {obj?.GetType()?.Name}");
                return;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddObjects(params IOngekiObject[] objs) => AddObjects(objs);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddObjects(IEnumerable<IOngekiObject> objs)
        {
            foreach (var item in objs)
            {
                AddObject(item);
            }
        }


        public void RemoveObject(IOngekiObject obj)
        {
            if (obj is Bell bell)
            {
                Bells.Remove(bell);
            }
            else
            {
                Log.LogWarn($"remove list target not found, object type : {obj?.GetType()?.Name}");
                return;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveObjects(params IOngekiObject[] objs) => RemoveObjects(objs);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveObjects(IEnumerable<IOngekiObject> objs)
        {
            foreach (var item in objs)
            {
                RemoveObject(item);
            }
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

            foreach (var bpl in BulletPalleteList.OrderBy(x=>x.StrID))
                sb.AppendLine(bpl.Serialize(this));
            #endregion

            #region COMPOSITION
            sb.AppendLine();
            sb.AppendLine("[COMPOSITION]");

            foreach (var o in BPMs.OrderBy(x => x.TGrid))
                sb.AppendLine(o.Serialize(this));
            sb.AppendLine();
            foreach (var o in MeterChanges.OrderBy(x => x.TGrid))
                sb.AppendLine(o.Serialize(this));
            foreach (var o in EnemySets.OrderBy(x => x.TGrid))
                sb.AppendLine(o.Serialize(this));
            #endregion

            #region BELL
            sb.AppendLine();
            sb.AppendLine("[BELL]");

            foreach (var u in Bells.OrderBy(x => x.TGrid))
                sb.AppendLine(u.Serialize(this));
            #endregion

            return sb.ToString();
        }
    }
}
