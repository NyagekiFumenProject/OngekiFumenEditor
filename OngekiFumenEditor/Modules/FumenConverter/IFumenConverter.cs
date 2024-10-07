using System.Threading.Tasks;
using Gemini.Framework;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Parser;

namespace OngekiFumenEditor.Modules.FumenConverter
{
	public interface IFumenConverter
	{
		Task<byte[]> ConvertFumenAsync(OngekiFumen fumen, string savePathOrFormat);
	}
}
