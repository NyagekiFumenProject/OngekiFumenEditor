using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using System.Collections.Generic;
using System.Linq;

namespace OngekiFumenEditor.Modules.FumenSoflanGroupListViewer.Models
{
    /// <summary>删除有引用组时用于转移/删除/撤销恢复的引用快照。</summary>
    public sealed class SoflanGroupReferenceSnapshot
    {
        public IReadOnlyList<ISoflan> Soflans { get; init; } = [];
        public IReadOnlyList<IndividualSoflanArea> Areas { get; init; } = [];
        public IReadOnlyList<int> SoflanGroups { get; init; } = [];
        public IReadOnlyList<int> AreaGroups { get; init; } = [];
        public IReadOnlyList<bool> SoflanSelected { get; init; } = [];
        public IReadOnlyList<bool> AreaSelected { get; init; } = [];

        public int Count => Soflans.Count + Areas.Count;

        public static SoflanGroupReferenceSnapshot Capture(OngekiFumen fumen, int soflanGroupId)
        {
            var soflans = fumen.SoflansMap.Values
                .SelectMany(x => x)
                .Where(x => x.SoflanGroup == soflanGroupId)
                .ToList();
            var areas = fumen.IndividualSoflanAreaMap.Values
                .SelectMany(x => x)
                .Where(x => x.SoflanGroup == soflanGroupId)
                .ToList();

            return new SoflanGroupReferenceSnapshot
            {
                Soflans = soflans,
                Areas = areas,
                SoflanGroups = soflans.Select(x => x.SoflanGroup).ToList(),
                AreaGroups = areas.Select(x => x.SoflanGroup).ToList(),
                SoflanSelected = soflans.Select(x => (x as ISelectableObject)?.IsSelected == true).ToList(),
                AreaSelected = areas.Select(x => x.IsSelected).ToList(),
            };
        }

        /// <summary>统一改归属。SoflansMap/IndividualSoflanAreaMap 会随属性变更自行迁移。</summary>
        public void ApplyGroup(int soflanGroupId)
        {
            foreach (var soflan in Soflans)
                soflan.SoflanGroup = soflanGroupId;
            foreach (var area in Areas)
                area.SoflanGroup = soflanGroupId;
        }

        public void RestoreGroup()
        {
            for (var i = 0; i < Soflans.Count; i++)
                Soflans[i].SoflanGroup = SoflanGroups[i];
            for (var i = 0; i < Areas.Count; i++)
                Areas[i].SoflanGroup = AreaGroups[i];
        }

        public void RemoveFrom(OngekiFumen fumen)
        {
            foreach (var soflan in Soflans)
                fumen.RemoveObject((OngekiObjectBase)soflan);
            foreach (var area in Areas)
                fumen.RemoveObject(area);
        }

        public void AddTo(OngekiFumen fumen)
        {
            foreach (var soflan in Soflans)
                fumen.AddObject((OngekiObjectBase)soflan);
            foreach (var area in Areas)
                fumen.AddObject(area);
        }

        public void RestoreSelection()
        {
            for (var i = 0; i < Soflans.Count; i++)
                if (Soflans[i] is ISelectableObject selectable)
                    selectable.IsSelected = SoflanSelected[i];
            for (var i = 0; i < Areas.Count; i++)
                Areas[i].IsSelected = AreaSelected[i];
        }
    }
}
