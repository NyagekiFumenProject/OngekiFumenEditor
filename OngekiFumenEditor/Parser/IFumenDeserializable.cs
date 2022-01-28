using OngekiFumenEditor.Base;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Parser
{
    public interface IFumenDeserializable
    {
        string FileFormatName { get; }
        string[] SupportFumenFileExtensions { get; }
        Task<OngekiFumen> DeserializeAsync(Stream stream);
    }
}
