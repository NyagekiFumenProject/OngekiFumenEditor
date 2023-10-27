using Gemini.Framework;
using OngekiFumenEditor.Modules.OptionGeneratorTools.Models;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.OptionGeneratorTools
{
	public interface IJacketGenerator : IWindow
	{
		Task<bool> Generate(JacketGenerateOption option);
	}
}
