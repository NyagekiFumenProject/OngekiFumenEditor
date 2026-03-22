using System;
using System.Collections.Generic;

namespace OngekiFumenEditor.Kernel.RuntimeAutomation
{
    public sealed class ScriptRunResult
    {
        public bool Success { get; set; }
        public string EditorId { get; set; }
        public string TransactionName { get; set; }
        public string ReturnValueJson { get; set; }
        public IReadOnlyList<string> Logs { get; set; } = Array.Empty<string>();
        public IReadOnlyList<ScriptDiagnostic> Diagnostics { get; set; } = Array.Empty<ScriptDiagnostic>();
        public string ErrorCode { get; set; }
        public string ErrorMessage { get; set; }
    }
}
