using System;
using System.Collections.Generic;

namespace OngekiFumenEditor.Kernel.RuntimeAutomation
{
    public sealed class ScriptBuildResult
    {
        public bool Success { get; set; }
        public IReadOnlyList<ScriptDiagnostic> Diagnostics { get; set; } = Array.Empty<ScriptDiagnostic>();
        public IReadOnlyList<string> SecurityIssues { get; set; } = Array.Empty<string>();
    }
}
