#nullable enable

using System.Text.Json.Serialization;

namespace OngekiFumenEditor.Avalonia.Browser.Platforms.Services.FileSystem.BrowserOpfs;

internal sealed class BrowserOpfsEntryDto
{
    public string Name { get; init; } = string.Empty;
    public string RelativePath { get; init; } = string.Empty;
    public int Kind { get; init; }
    public long? Size { get; init; }
    public long? LastModified { get; init; }
    public int StagingState { get; init; }
}

internal sealed class BrowserOpfsManifestRequestDto
{
    public BrowserOpfsSelectionDto[] SelectedEntries { get; init; } = [];
}

internal sealed class BrowserOpfsSelectionDto
{
    public string RelativePath { get; init; } = string.Empty;
    public int Kind { get; init; }
}

internal sealed class BrowserOpfsManifestDto
{
    public BrowserOpfsManifestEntryDto[] Entries { get; init; } = [];
    public long TotalBytes { get; init; }
    public int TotalFiles { get; init; }
}

internal sealed class BrowserOpfsManifestEntryDto
{
    public string Path { get; init; } = string.Empty;
    public int Kind { get; init; }
    public long? Size { get; init; }
    public long? LastModified { get; init; }
}

internal sealed class BrowserOpfsBeginDownloadDto
{
    public int Handle { get; init; }
    public bool Canceled { get; init; }
    public string Mode { get; init; } = string.Empty;
    public string? StagingPath { get; init; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(BrowserOpfsEntryDto[]))]
[JsonSerializable(typeof(BrowserOpfsManifestRequestDto))]
[JsonSerializable(typeof(BrowserOpfsManifestDto))]
[JsonSerializable(typeof(BrowserOpfsBeginDownloadDto))]
internal partial class BrowserOpfsJsonContext : JsonSerializerContext
{
}
