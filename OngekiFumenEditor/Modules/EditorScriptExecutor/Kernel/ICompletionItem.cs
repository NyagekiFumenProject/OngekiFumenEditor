using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.EditorScriptExecutor.Kernel
{
    public interface ICompletionItem
    {
        string Name { get; }
        string Description { get; }
        int Priority { get; }
    }
}
