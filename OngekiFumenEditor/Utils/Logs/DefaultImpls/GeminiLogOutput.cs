using Gemini.Modules.Output;
using System.ComponentModel.Composition;
using static OngekiFumenEditor.Utils.Logs.ILogOutput;

namespace OngekiFumenEditor.Utils.Logs.DefaultImpls
{
    [Export(typeof(ILogOutput))]
    public class GeminiLogOutput : ILogOutput
    {
        [Import(typeof(IOutput))]
        private IOutput output = default;

        public void WriteLog(Severity severity , string content)
        {
            output.Append(content);
        }
    }
}
