using OngekiFumenEditor.Kernel.CommandExecutor.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.ProgramUpdater
{
    public class UpdaterOption
    {
        [OptionBindingAttrbute<string>("sourceFolder", "<INTERNAL>", null, Require = true)]
        public string SourceFolder { get; set; }
        [OptionBindingAttrbute<string>("targetFolder", "<INTERNAL>", null, Require = true)]
        public string TargetFolder { get; set; }
        [OptionBindingAttrbute<string>("sourceVersion", "<INTERNAL>", null, Require = true)]
        public string SourceVersion { get; set; }
    }
}
