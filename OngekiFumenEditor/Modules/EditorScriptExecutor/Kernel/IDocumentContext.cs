using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.EditorScriptExecutor.Kernel
{
    public interface IDocumentContext : IDisposable
    {
        BuildParam CreateBuildParam();
        IAsyncEnumerable<ICompletionItem> CompleteCode(string str, int cursorPos, bool insertOrDelete);
        bool GenerateProjectFile(string genProjOutputDirPath, string scriptFilePath, out string projFilePath);
    }
}
