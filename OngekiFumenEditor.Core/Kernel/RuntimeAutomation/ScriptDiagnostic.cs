namespace OngekiFumenEditor.Kernel.RuntimeAutomation
{
    public sealed class ScriptDiagnostic
    {
        public string Severity { get; set; } = "Info";
        public string Message { get; set; } = string.Empty;
        public int? Line { get; set; }
        public int? Column { get; set; }
    }
}
