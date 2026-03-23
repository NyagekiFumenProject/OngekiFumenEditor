namespace OngekiFumenEditor.Kernel.RuntimeAutomation
{
    public sealed class ScriptRunRequest
    {
        public string ScriptText { get; set; } = string.Empty;
        public string ExpectedEditorId { get; set; }
        public bool RequireConfirmation { get; set; } = true;
        public bool WrapUndoTransaction { get; set; } = true;
        public string TransactionName { get; set; }
        public string RequestedBy { get; set; }
        public string ClientId { get; set; }
    }
}
