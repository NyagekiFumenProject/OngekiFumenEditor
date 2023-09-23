using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.EditorScriptExecutor.Kernel
{
    public class BuildParam
    {
        public string Script { get; set; }
        public List<string> AssemblyLocations { get; set; } = new List<string>();

        public bool Optimze { get; set; } = false;
        public string DisplayFileName { get; set; }
    }
}
