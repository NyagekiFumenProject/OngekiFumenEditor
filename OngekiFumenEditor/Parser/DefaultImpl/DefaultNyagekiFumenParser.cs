using OngekiFumenEditor.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Parser.DefaultImpl
{
    [Export(typeof(IFumenDeserializable))]
    public class DefaultNyagekiFumenParser : IFumenDeserializable
    {
        public const string FormatName = "Nyageki Fumen File";
        public string FileFormatName => FormatName;

        public static readonly string[] FumenFileExtensions = new[] { ".nyageki" };
        public string[] SupportFumenFileExtensions => FumenFileExtensions;

        public async Task<OngekiFumen> DeserializeAsync(Stream stream)
        {
            var fumen = await JsonSerializer.DeserializeAsync<OngekiFumen>(stream, DefaultNyagekiFumenFormatter.DefaultJsonOption);

            fumen.Setup();

            return fumen;
        }
    }
}
