using System.Text.Json.Serialization;
using OngekiFumenEditor.Avalonia.Modules.InternalTest.Models;

namespace OngekiFumenEditor.Avalonia.Modules.InternalTest.Models;

[JsonSerializable(typeof(InternalTestValueStoreData))]
[JsonSerializable(typeof(InternalTestRecentInfoData))]
public partial class InternalTestJsonSourceGenerateContext : JsonSerializerContext
{
}
