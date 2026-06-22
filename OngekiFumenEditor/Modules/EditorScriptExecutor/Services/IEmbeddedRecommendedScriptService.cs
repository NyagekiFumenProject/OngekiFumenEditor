using System.Collections.Generic;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.EditorScriptExecutor.Services
{
    public interface IEmbeddedRecommendedScriptService
    {
        IEnumerable<EmbeddedRecommendedScriptInfo> GetScripts();

        EmbeddedRecommendedScriptInfo GetScript(string resourceName);

        bool Contains(string resourceName);

        Task<string> ReadScriptAsync(string resourceName);

        Task OpenScriptAsync(string resourceName);
    }
}
