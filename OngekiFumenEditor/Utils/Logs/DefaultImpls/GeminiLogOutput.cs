using Gemini.Modules.Output;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Utils.Logs.DefaultImpls
{
    [Export(typeof(ILogOutput))]
    public class GeminiLogOutput : ILogOutput
    {
        [Import(typeof(IOutput))]
        private IOutput output = default;

        public void WriteLog(string content)
        {
            output.Append(content);
        }
    }
}
