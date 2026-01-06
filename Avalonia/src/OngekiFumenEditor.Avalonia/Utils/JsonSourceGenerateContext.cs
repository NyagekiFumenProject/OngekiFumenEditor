using System.Text.Json.Serialization;
using OngekiFumenEditor.Avalonia.Modules.InternalTest.Models;

[JsonSerializable(typeof(InternalTestValueStoreData))]
[JsonSerializable(typeof(InternalTestRecentInfoData))]
[JsonSerializable(typeof(Dictionary<string, string>))]
public partial class JsonSourceGenerateContext : JsonSerializerContext
{
}