using System.Collections.Generic;

namespace OngekiFumenEditor.Parser
{
	public interface IFumenParserManager
	{
		IFumenSerializable GetSerializer(string saveFilePath);
		IFumenDeserializable GetDeserializer(string loadFilePath);

		IEnumerable<(string desc, string[] fileFormat)> GetSerializerDescriptions();
		IEnumerable<(string desc, string[] fileFormat)> GetDeserializerDescriptions();
	}
}
