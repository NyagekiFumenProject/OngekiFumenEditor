using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Parser.DefaultImpl
{
    public abstract class SimpleJsonConverter<T, IN_TYPE> : JsonConverter<T>
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return typeof(T) == typeToConvert;
        }

        public abstract IN_TYPE Write(T jsonObj);
        public abstract T Read(IN_TYPE jsonObj);

        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var json = JsonSerializer.Deserialize<IN_TYPE>(ref reader, options);
            var o = Read(json);
            return o;
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, Write(value), options);
        }
    }
}
