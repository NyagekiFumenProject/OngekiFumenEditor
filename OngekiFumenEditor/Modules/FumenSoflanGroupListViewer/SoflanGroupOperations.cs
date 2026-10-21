using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using System;
using System.Linq;

namespace OngekiFumenEditor.Modules.FumenSoflanGroupListViewer
{
    public static class SoflanGroupOperations
    {
        public static LambdaUndoAction CreateAddAction(OngekiFumen fumen, string name,
            out SoflanGroupWrapItem item, Action changed = null)
        {
            var map = fumen.IndividualSoflanAreaMap;
            var id = map.GetNextSoflanGroupId(fumen.SoflansMap.Keys.Concat(map.Keys));
            var createdItem = item = new SoflanGroupWrapItem(id);
            var group = new SoflanGroupWrapItemGroup
            {
                DisplayName = string.IsNullOrWhiteSpace(name) ? id.ToString() : name.Trim()
            };
            return LambdaUndoAction.Create(Resources.SoflanGroupCreateUndo, () =>
            {
                map.RegisterSoflanGroupWrapItem(createdItem);
                group.Add(createdItem);
                map.SoflanGroupWrapItemGroupRoot.Add(group);
                fumen.NotifySoflanGroupsChanged();
                changed?.Invoke();
            }, () =>
            {
                group.Remove(createdItem);
                map.SoflanGroupWrapItemGroupRoot.Remove(group);
                fumen.NotifySoflanGroupsChanged();
                changed?.Invoke();
            });
        }

        public static OngekiObjectBase[] GetReferences(OngekiFumen fumen, int id) =>
            fumen.SoflansMap.Values.SelectMany(x => x).Where(x => x.SoflanGroup == id).Cast<OngekiObjectBase>()
            .Concat(fumen.IndividualSoflanAreaMap.Values.SelectMany(x => x).Where(x => x.SoflanGroup == id)).Distinct().ToArray();

        private static void SetGroup(OngekiObjectBase obj, int id)
        {
            if (obj is ISoflan soflan)
                soflan.SoflanGroup = id;
            else if (obj is IndividualSoflanArea area)
                area.SoflanGroup = id;
            else
                throw new InvalidOperationException();
        }

        public static LambdaUndoAction CreateDeleteAction(OngekiFumen fumen, SoflanGroupWrapItem item,
            SoflanGroupWrapItem replacement, bool deleteReferences, Action changed = null)
        {
            var map = fumen.IndividualSoflanAreaMap;
            if (item is null || item.SoflanGroupId == 0 || !map.GetSoflanGroups().Contains(item))
                throw new InvalidOperationException(Resources.SoflanGroupInvalidDelete);
            var references = GetReferences(fumen, item.SoflanGroupId);
            if (references.Length > 0 && !deleteReferences &&
                (replacement is null || replacement == item || !map.GetSoflanGroups().Contains(replacement)))
                throw new InvalidOperationException(Resources.SoflanGroupReplacementRequired);
            var parent = item.Parent;
            var next = parent.Children.SkipWhile(x => x != item).Skip(1).FirstOrDefault();
            var selected = item.IsSelected;
            var displayed = item.IsDisplaySoflanDesignMode;
            return LambdaUndoAction.Create(Resources.SoflanGroupDelete, () =>
            {
                foreach (var obj in references)
                    if (deleteReferences)
                        fumen.RemoveObject(obj);
                    else
                        SetGroup(obj, replacement.SoflanGroupId);
                item.IsSelected = false;
                item.IsDisplaySoflanDesignMode = false;
                parent.Remove(item);
                fumen.NotifySoflanGroupsChanged();
                changed?.Invoke();
            }, () =>
            {
                if (next?.Parent == parent)
                    parent.InsertBefore(item, next);
                else
                    parent.Add(item);
                foreach (var obj in references)
                    if (deleteReferences)
                        fumen.AddObject(obj);
                    else
                        SetGroup(obj, item.SoflanGroupId);
                item.IsSelected = selected;
                item.IsDisplaySoflanDesignMode = displayed;
                fumen.NotifySoflanGroupsChanged();
                changed?.Invoke();
            });
        }
    }
}
