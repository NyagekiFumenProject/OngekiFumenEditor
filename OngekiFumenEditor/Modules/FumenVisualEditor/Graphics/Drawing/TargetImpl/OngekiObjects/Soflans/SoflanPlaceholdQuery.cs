using NWaves.Transforms;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects.Soflans
{
    public class SoflanPlaceholdQuery
    {
        private Dictionary<int, int> cachedSoflanGroupToIndexMap = new Dictionary<int, int>();
        private Dictionary<int, int> cachedIndexToSoflanGroupMap = new Dictionary<int, int>();

        public int QueryPositionIndex(int soflanGroup)
        {
            if (!cachedSoflanGroupToIndexMap.TryGetValue(soflanGroup, out var index))
            {
                index = AssignAvailableIndex(soflanGroup);
                cachedSoflanGroupToIndexMap[soflanGroup] = index;
                cachedIndexToSoflanGroupMap[index] = soflanGroup;
            }

            return index;
        }

        public void UpdatePositionIndexesForNewFrame(IEnumerable<int> soflanGroups)
        {
            //assgin new index for new soflan group
            foreach (var soflanGroup in soflanGroups.Where(x => !cachedSoflanGroupToIndexMap.ContainsKey(x)))
            {
                var index = AssignAvailableIndex(soflanGroup);
                cachedSoflanGroupToIndexMap[soflanGroup] = index;
                cachedIndexToSoflanGroupMap[index] = soflanGroup;
            }

            //remove old soflan groups that are not in the soflan group list
            using var _d = cachedSoflanGroupToIndexMap.Keys.Except(soflanGroups).ToListWithObjectPool(out var keysToRemove);
            foreach (var soflanGroup in keysToRemove)
            {
                var index = cachedSoflanGroupToIndexMap[soflanGroup];
                cachedSoflanGroupToIndexMap.Remove(soflanGroup);
                cachedIndexToSoflanGroupMap.Remove(index);
            }
        }

        private int AssignAvailableIndex(int soflanGroup)
        {
            return soflanGroup % 2;
        }
    }
}
