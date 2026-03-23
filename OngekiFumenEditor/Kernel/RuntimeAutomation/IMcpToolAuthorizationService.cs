using System.Threading;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.RuntimeAutomation
{
    public interface IMcpToolAuthorizationService
    {
        Task<McpToolAuthorizationResult> EnsureAuthorizedAsync(string toolName, string requestedBy, string clientId, string requestPreview, bool allowInteractivePrompt = true, CancellationToken cancellationToken = default);
    }
}
