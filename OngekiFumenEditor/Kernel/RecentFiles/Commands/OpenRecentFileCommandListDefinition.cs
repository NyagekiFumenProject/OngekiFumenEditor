using Gemini.Framework.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.RecentFiles.Commands
{
    [CommandDefinition]
    public class OpenRecentFileCommandListDefinition : CommandListDefinition
    {
        public const string CommandName = "File.OpenRecentFileList";

        public override string Name
        {
            get { return CommandName; }
        }
    }
}
