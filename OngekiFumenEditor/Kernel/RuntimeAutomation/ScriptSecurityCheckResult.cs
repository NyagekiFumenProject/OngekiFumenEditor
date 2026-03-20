using System;
using System.Collections.Generic;

namespace OngekiFumenEditor.Kernel.RuntimeAutomation
{
    public sealed class ScriptSecurityCheckResult
    {
        public bool Success { get; set; }
        public IReadOnlyList<string> Issues { get; set; } = Array.Empty<string>();
    }
}
