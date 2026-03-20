using System.Threading;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.RuntimeAutomation
{
    public interface IMcpToolAuthorizationService
    {
        Task<bool> EnsureAuthorizedAsync(string toolName, string requestedBy, string clientId, string requestPreview, CancellationToken cancellationToken = default);
    }
}
