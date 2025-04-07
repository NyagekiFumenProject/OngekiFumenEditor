using OngekiFumenEditor.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics
{
    public class GlobalCacheSoflanGroupRecorder
    {
        private Dictionary<int, int> cacheSoflanGroupMap = new Dictionary<int, int>();

        public void SetCache(int objectId, int soflanGroup)
        {
            cacheSoflanGroupMap[objectId] = soflanGroup;
        }

        public int GetCache(OngekiObjectBase ongekiObject) => GetCache(ongekiObject.Id);
        public int GetCache(int objectId)
        {
            if (cacheSoflanGroupMap.TryGetValue(objectId, out var soflanGroup))
                return soflanGroup;
            return 0;
        }

        public void Clear()
        {
            cacheSoflanGroupMap.Clear();
        }
    }
}
