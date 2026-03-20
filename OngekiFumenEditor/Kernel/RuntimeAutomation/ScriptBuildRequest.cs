namespace OngekiFumenEditor.Kernel.RuntimeAutomation
{
    public sealed class ScriptBuildRequest
    {
        public string ScriptText { get; set; } = string.Empty;
        public bool EnableSecurityCheck { get; set; } = true;
    }
}
