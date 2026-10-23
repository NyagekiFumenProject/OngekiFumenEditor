using OngekiFumenEditor.Properties;

namespace OngekiFumenEditor.Modules.FumenSoflanGroupListViewer.Models
{
    /// <summary>查看器树、组属性下拉与删除对话框共用的组显示名规则。</summary>
    public static class SoflanGroupDisplayName
    {
        public static string FormatGroup(SoflanGroupNode group)
        {
            if (group is null)
                return string.Empty;

            if (group.IsDefault && string.IsNullOrWhiteSpace(group.Name))
                return Resources.SoflanGroupDefaultGroupName;
            if (string.IsNullOrWhiteSpace(group.Name))
                return $"#{group.Id}";
            return group.Name;
        }

        public static string FormatGroupWithId(SoflanGroupNode group)
        {
            var name = FormatGroup(group);
            var suffix = $"#{group.Id}";
            return name.EndsWith(suffix, System.StringComparison.Ordinal) ? name : $"{name} {suffix}";
        }
    }
}
