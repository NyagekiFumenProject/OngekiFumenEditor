using System.Threading;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.RuntimeAutomation
{
    public interface IRuntimeAutomationScriptHost
    {
        Task<ScriptBuildResult> BuildAsync(ScriptBuildRequest request, CancellationToken cancellationToken = default);

        Task<ScriptRunResult> RunOnCurrentEditorAsync(ScriptRunRequest request, CancellationToken cancellationToken = default);

        Task<ScriptRunResult> RunOnEditorAsync(string editorId, ScriptRunRequest request, CancellationToken cancellationToken = default);

        ScriptRunResult GetLastResult();
    }
}
