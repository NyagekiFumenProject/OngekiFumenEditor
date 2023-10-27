using Gemini.Framework;
using OngekiFumenEditor.Modules.OptionGeneratorTools.Models;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.OptionGeneratorTools
{
	public interface IMusicXmlGenerator : IWindow
	{
		Task<bool> Generate(string saveFilePath, MusicXmlGenerateOption option);
	}
}
