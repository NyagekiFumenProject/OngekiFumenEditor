using OngekiFumenEditor.Utils.Json.Converters;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace OngekiFumenEditor.Utils.Json
{
    internal static class CommonJsonSerializerOptions
    {
        static CommonJsonSerializerOptions()
        {
            Default = new JsonSerializerOptions()
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            Default.Converters.Add(new ColorJsonConverter());
        }

        public static JsonSerializerOptions Default { get; }
    }
}
