using System.Collections.Generic;

namespace OngekiFumenEditor.Kernel.RuntimeAutomation
{
    public interface IMcpClientAuthorizationManager
    {
        McpClientRegistrationInfo RegisterClientUsage(string requestedBy, string clientId);
        bool IsExecutionApprovalRemembered(string requestedBy, string clientId);
        void RememberExecutionApproval(string requestedBy, string clientId);
        bool RevokeExecutionApproval(string identityKey);
        IReadOnlyList<McpClientRegistrationInfo> GetKnownClients();
    }
}
