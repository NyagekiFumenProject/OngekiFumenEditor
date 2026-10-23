using System;

namespace OngekiFumenEditor.Modules.FumenSoflanGroupListViewer.Models
{
    /// <summary>下拉选项：组属性选择与删除组的替代目标共用。</summary>
    public sealed class SoflanGroupOption : IEquatable<SoflanGroupOption>
    {
        public SoflanGroupOption(int id, string displayName)
        {
            Id = id;
            DisplayName = displayName ?? string.Empty;
        }

        public int Id { get; }
        public string DisplayName { get; }

        public static SoflanGroupOption FromGroup(SoflanGroupNode group)
            => group is null ? null : new SoflanGroupOption(group.Id, SoflanGroupDisplayName.FormatGroup(group));

        public bool Equals(SoflanGroupOption other) => other is not null && Id == other.Id;
        public override bool Equals(object obj) => Equals(obj as SoflanGroupOption);
        public override int GetHashCode() => Id;
        public override string ToString() => DisplayName;
    }
}
