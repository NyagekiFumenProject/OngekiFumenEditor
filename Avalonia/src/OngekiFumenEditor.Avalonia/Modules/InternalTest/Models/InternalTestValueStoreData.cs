using System.Text.Json.Serialization.Metadata;
using Gekimini.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Modules.InternalTest.Models;

public class InternalTestValueStoreData
{
    public static JsonTypeInfo<InternalTestValueStoreData> JsonTypeInfo =>
        JsonSourceGenerateContext.Default.InternalTestValueStoreData;

    public int StoredValue { get; set; }
    public string DocumentName { get; set; }
}