using OngekiFumenEditor.Base;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Parser
{
	public interface IFumenSerializable
	{
		string FileFormatName { get; }
		string[] SupportFumenFileExtensions { get; }
		Task<byte[]> SerializeAsync(OngekiFumen fumen);
	}
}
