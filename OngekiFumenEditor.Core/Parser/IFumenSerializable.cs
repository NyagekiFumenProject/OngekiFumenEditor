using OngekiFumenEditor.Core.Base;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Core.Parser
{
	public interface IFumenSerializable
	{
		string FileFormatName { get; }
		string[] SupportFumenFileExtensions { get; }
		Task<byte[]> SerializeAsync(OngekiFumen fumen);
	}
}
