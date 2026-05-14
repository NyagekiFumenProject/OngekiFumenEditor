using OngekiFumenEditor.Core.Base;
using System.IO;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Core.Parser
{
	public interface IFumenDeserializable
	{
		string FileFormatName { get; }
		string[] SupportFumenFileExtensions { get; }
		Task<OngekiFumen> DeserializeAsync(Stream stream);
	}
}
