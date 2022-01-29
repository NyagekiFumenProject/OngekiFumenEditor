using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
