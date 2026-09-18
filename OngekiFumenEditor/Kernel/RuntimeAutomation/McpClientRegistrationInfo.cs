using System;

namespace OngekiFumenEditor.Kernel.RuntimeAutomation
{
    public sealed class McpClientRegistrationInfo
    {
        public string IdentityKey { get; set; }
        public string RequestedBy { get; set; }
        public string ClientId { get; set; }
        public bool IsExecutionApproved { get; set; }
        public DateTimeOffset LastSeenUtc { get; set; }
    }
}
