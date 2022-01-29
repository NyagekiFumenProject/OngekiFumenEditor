using Caliburn.Micro;
using OngekiFumenEditor.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Parser.DefaultImpl
{
    [Export(typeof(IFumenSerializable))]
    class DefaultNyagekiFumenFormatter : IFumenSerializable
    {
        public string FileFormatName => DefaultNyagekiFumenParser.FormatName;
        public string[] SupportFumenFileExtensions => DefaultNyagekiFumenParser.FumenFileExtensions;

        public static JsonSerializerOptions DefaultJsonOption { get; } = new JsonSerializerOptions()
        {
            WriteIndented = true,
            IgnoreReadOnlyFields = true,
            IncludeFields = false,
            IgnoreReadOnlyProperties = true
        };

        static DefaultNyagekiFumenFormatter()
        {
            foreach (var converter in IoC.GetAll<JsonConverter>())
                DefaultJsonOption.Converters.Add(converter);
        }

        public Task<string> SerializeAsync(OngekiFumen fumen)
        {
            var str = JsonSerializer.Serialize(fumen, DefaultJsonOption);
            return Task.FromResult(str);
        }
    }
}
