using Gemini.Modules.Output;
using System.ComponentModel.Composition;

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
