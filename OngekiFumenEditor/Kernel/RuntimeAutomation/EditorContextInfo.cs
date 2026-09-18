namespace OngekiFumenEditor.Kernel.RuntimeAutomation
{
    public sealed class EditorContextInfo
    {
        public string EditorId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string ProjectPath { get; set; }
        public string FumenPath { get; set; }
        public bool IsDirty { get; set; }
        public bool IsActive { get; set; }
        public int LaneCount { get; set; }
        public int TapCount { get; set; }
        public int HoldCount { get; set; }
        public int BellCount { get; set; }
        public int BulletCount { get; set; }
        public int BpmChangeCount { get; set; }
        public int SoflanCount { get; set; }
    }
}
