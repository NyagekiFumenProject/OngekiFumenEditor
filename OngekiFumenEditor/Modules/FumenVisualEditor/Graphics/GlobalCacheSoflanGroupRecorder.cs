using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics
{
    public class GlobalCacheSoflanGroupRecorder
    {
        private ConcurrentDictionary<int, (SoflanList, int)> cacheSoflanGroupMap = new();
        private FrozenDictionary<int, (SoflanList, int)> freezedSoflanGroupMap;
        private SoflanList defaultSoflanList;

        public void SetDefault(SoflanList soflanList)
        {
            defaultSoflanList = soflanList;
        }

        public void SetCache(int objectId, SoflanList soflanList, int soflanGroup)
        {
            cacheSoflanGroupMap[objectId] = (soflanList, soflanGroup);
        }

        public void Freeze()
        {
            freezedSoflanGroupMap = cacheSoflanGroupMap.ToFrozenDictionary();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SoflanList GetCache(OngekiObjectBase ongekiObject) => GetCache(ongekiObject.Id, out _);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SoflanList GetCache(int objectId) => GetCache(objectId, out _);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SoflanList GetCache(OngekiObjectBase ongekiObject, out int soflanGroup) => GetCache(ongekiObject.Id, out soflanGroup);
        public SoflanList GetCache(int objectId, out int soflanGroup)
        {
            if (freezedSoflanGroupMap?.TryGetValue(objectId, out var pair) ?? false)
            {
                soflanGroup = pair.Item2;
                return pair.Item1;
            }

            soflanGroup = 0;
            return defaultSoflanList;
        }

        public void Clear()
        {
            cacheSoflanGroupMap.Clear();
            freezedSoflanGroupMap = default;
            defaultSoflanList = default;
        }
    }
}
