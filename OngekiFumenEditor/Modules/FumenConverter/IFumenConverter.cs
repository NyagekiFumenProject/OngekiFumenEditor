using System.Threading.Tasks;
using Gemini.Framework;
using OngekiFumenEditor.Core.Base;
using OngekiFumenEditor.Core.Parser;

namespace OngekiFumenEditor.Modules.FumenConverter
{
	public interface IFumenConverter
	{
		Task<byte[]> ConvertFumenAsync(OngekiFumen fumen, string savePathOrFormat);
	}
}
