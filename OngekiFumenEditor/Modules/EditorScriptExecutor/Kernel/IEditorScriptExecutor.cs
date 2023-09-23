using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.EditorScriptExecutor.Kernel
{
    public interface IEditorScriptExecutor
    {
        Task<BuildResult> Build(BuildParam param);
        Task<ExecuteResult> Execute(BuildResult buildResult, FumenVisualEditorViewModel targetEditor);

        Task<IDocumentContext> InitDocumentContext();
    }
}
