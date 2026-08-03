#nullable enable

using System.Globalization;
using System.Numerics;
using System.Text;
using Avalonia.Platform;
using OngekiFumenEditor.Avalonia.Kernel.Graphics;

namespace OngekiFumenEditor.Avalonia.Utils;

public static class ResourceUtils
{
    private const string ResourceRootName = "Resources";
    private static readonly string ResourceAssemblyName = typeof(ResourceUtils).Assembly.GetName().Name!;
    private static readonly IAssetLoader embeddedAssetLoader = new StandardAssetLoader(typeof(ResourceUtils).Assembly);
    private static readonly IReadOnlyDictionary<string, string> textureSizeOriginMap = LoadTextureSizeOriginMap();

    public static Stream OpenReadResourceStream(string resourcePath) =>
        OpenReadResourceStream(
            resourcePath,
            Path.Combine(AppContext.BaseDirectory, ResourceRootName),
            allowLocalOverride: !OperatingSystem.IsBrowser());

    internal static Stream OpenReadResourceStream(
        string resourcePath,
        string overrideRootPath,
        bool allowLocalOverride)
    {
        var normalizedPath = NormalizeResourcePath(resourcePath);
        if (allowLocalOverride && TryGetOverrideFilePath(normalizedPath, overrideRootPath) is { } overrideFilePath)
            return File.OpenRead(overrideFilePath);

        return embeddedAssetLoader.Open(CreateEmbeddedResourceUri(normalizedPath));
    }

    public static Uri GetResourceUri(string resourcePath) =>
        CreateEmbeddedResourceUri(NormalizeResourcePath(resourcePath));

    public static IImage OpenReadTextureFromResource(IRenderManagerImpl impl, string resourcePath)
    {
        using var stream = OpenReadResourceStream(resourcePath);
        return impl.LoadImageFromStream(stream);
    }

    public static string ReadTextureSizeAnchor(string key) =>
        textureSizeOriginMap.TryGetValue(key, out var value) ? value : string.Empty;

    public static bool OpenReadTextureSizeAnchorByConfigFile(
        string textureName,
        out Vector2 size,
        out Vector2 anchor)
    {
        size = default;
        anchor = default;
        var good = false;

        try
        {
            var key = textureName + "Size";
            var str = ReadTextureSizeAnchor(key);
            if (!string.IsNullOrWhiteSpace(str))
            {
                var split = str.Split(',');
                size = new(
                    float.Parse(split[0].Trim(), CultureInfo.InvariantCulture),
                    float.Parse(split[1].Trim(), CultureInfo.InvariantCulture));
                good = true;
            }
            else
            {
                Log.LogWarn($"size key {key} is not found.");
            }

            key = textureName + "Anchor";
            str = ReadTextureSizeAnchor(key);
            if (!string.IsNullOrWhiteSpace(str))
            {
                var split = str.Split(',');
                anchor = new(
                    float.Parse(split[0].Trim(), CultureInfo.InvariantCulture),
                    float.Parse(split[1].Trim(), CultureInfo.InvariantCulture));
            }
            else
            {
                //Log.LogWarn($"anchor key {key} is not found.");
            }

            return good;
        }
        catch
        {
            //todo log
            return false;
        }
    }

    private static Dictionary<string, string> LoadTextureSizeOriginMap()
    {
        using var stream = OpenReadResourceStream("editor/textureSizeAnchor.ini");
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var result = new Dictionary<string, string>();

        while (reader.ReadLine() is { } line)
        {
            var split = line.Split('=', 2);
            if (split.Length == 2)
                result[split[0]] = split[1];
        }

        return result;
    }

    internal static string NormalizeResourcePath(string resourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourcePath);
        if (Path.IsPathRooted(resourcePath) || Uri.TryCreate(resourcePath, UriKind.Absolute, out _))
            throw new ArgumentException("Resource path must be relative to the Resources directory.", nameof(resourcePath));

        var segments = resourcePath.Replace('\\', '/').Split('/', StringSplitOptions.None);
        if (segments.Any(static segment =>
                string.IsNullOrWhiteSpace(segment) || segment is "." or ".."))
        {
            throw new ArgumentException("Resource path contains an invalid segment.", nameof(resourcePath));
        }

        return string.Join('/', segments);
    }

    internal static string? TryGetOverrideFilePath(string normalizedPath, string overrideRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(overrideRootPath);
        var fullRootPath = Path.GetFullPath(overrideRootPath);
        var fullFilePath = Path.GetFullPath(Path.Combine(
            fullRootPath,
            normalizedPath.Replace('/', Path.DirectorySeparatorChar)));
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var rootPrefix = Path.TrimEndingDirectorySeparator(fullRootPath) + Path.DirectorySeparatorChar;

        if (!fullFilePath.StartsWith(rootPrefix, comparison))
            throw new ArgumentException("Resource path escapes the override directory.", nameof(normalizedPath));

        return File.Exists(fullFilePath) ? fullFilePath : null;
    }

    private static Uri CreateEmbeddedResourceUri(string normalizedPath)
    {
        var escapedPath = string.Join('/', normalizedPath.Split('/').Select(Uri.EscapeDataString));
        return new Uri($"avares://{ResourceAssemblyName}/{ResourceRootName}/{escapedPath}", UriKind.Absolute);
    }
}
