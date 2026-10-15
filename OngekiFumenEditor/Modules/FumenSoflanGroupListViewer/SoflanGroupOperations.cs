using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using System;
using System.Linq;
namespace OngekiFumenEditor.Modules.FumenSoflanGroupListViewer
{
    public static class SoflanGroupOperations
    {
        public static LambdaUndoAction CreateDeleteAction(OngekiFumen fumen, SoflanGroupWrapItem item,
            SoflanGroupWrapItem replacement, bool deleteReferences, Action changed = null)
        {
            var map = fumen.IndividualSoflanAreaMap;
            if (item is null || item.SoflanGroupId == 0 || !map.GetSoflanGroups().Contains(item))
                throw new InvalidOperationException(Resources.SoflanGroupInvalidDelete);
            var references = fumen.SoflansMap.Values.SelectMany(x => x).OfType<OngekiObjectBase>()
                .Concat(map.Values.SelectMany(x => x))
                .Where(x => (int)x.GetType().GetProperty("SoflanGroup").GetValue(x) == item.SoflanGroupId)
                .Distinct().ToArray();
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
                    if (deleteReferences) fumen.RemoveObject(obj);
                    else obj.GetType().GetProperty("SoflanGroup").SetValue(obj, replacement.SoflanGroupId);
                item.IsSelected = false; item.IsDisplaySoflanDesignMode = false;
                parent.Remove(item); changed?.Invoke();
            }, () =>
            {
                if (next?.Parent == parent) parent.InsertBefore(item, next); else parent.Add(item);
                foreach (var obj in references)
                    if (deleteReferences) fumen.AddObject(obj);
                    else obj.GetType().GetProperty("SoflanGroup").SetValue(obj, item.SoflanGroupId);
                item.IsSelected = selected; item.IsDisplaySoflanDesignMode = displayed;
                changed?.Invoke();
            });
        }
    }
}
