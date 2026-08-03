#nullable enable

using System.Reflection;
using Avalonia.Platform;

namespace OngekiFumenEditor.Avalonia.Utils;

public sealed class ResourceOverrideAssetLoader : IAssetLoader
{
    private const string ResourcePathPrefix = "/Resources/";
    private static readonly Assembly resourceAssembly = typeof(ResourceOverrideAssetLoader).Assembly;
    private static readonly string resourceAssemblyName = resourceAssembly.GetName().Name!;
    private readonly IAssetLoader inner;
    private readonly string overrideRootPath;

    public ResourceOverrideAssetLoader(IAssetLoader inner, string overrideRootPath)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        ArgumentException.ThrowIfNullOrWhiteSpace(overrideRootPath);
        this.overrideRootPath = Path.GetFullPath(overrideRootPath);
    }

    public void SetDefaultAssembly(Assembly assembly) => inner.SetDefaultAssembly(assembly);

    public bool Exists(Uri uri, Uri? baseUri = null) =>
        TryGetOverrideFilePath(uri, baseUri, out _) || inner.Exists(uri, baseUri);

    public Stream Open(Uri uri, Uri? baseUri = null)
    {
        if (TryGetOverrideFilePath(uri, baseUri, out var overrideFilePath))
            return File.OpenRead(overrideFilePath);

        return inner.Open(uri, baseUri);
    }

    public (Stream stream, Assembly assembly) OpenAndGetAssembly(Uri uri, Uri? baseUri = null)
    {
        if (TryGetOverrideFilePath(uri, baseUri, out var overrideFilePath))
            return (File.OpenRead(overrideFilePath), resourceAssembly);

        return inner.OpenAndGetAssembly(uri, baseUri);
    }

    public Assembly? GetAssembly(Uri uri, Uri? baseUri = null) =>
        TryGetOverrideFilePath(uri, baseUri, out _) ? resourceAssembly : inner.GetAssembly(uri, baseUri);

    public IEnumerable<Uri> GetAssets(Uri uri, Uri? baseUri) => inner.GetAssets(uri, baseUri);

    public void InvalidateAssemblyCache(string name) => inner.InvalidateAssemblyCache(name);

    public void InvalidateAssemblyCache() => inner.InvalidateAssemblyCache();

    private bool TryGetOverrideFilePath(Uri uri, Uri? baseUri, out string overrideFilePath)
    {
        overrideFilePath = string.Empty;
        var absoluteUri = uri.IsAbsoluteUri
            ? uri
            : baseUri is null
                ? null
                : new Uri(baseUri, uri);

        if (absoluteUri is null ||
            !absoluteUri.Scheme.Equals("avares", StringComparison.OrdinalIgnoreCase) ||
            !absoluteUri.Authority.Equals(resourceAssemblyName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var absolutePath = Uri.UnescapeDataString(absoluteUri.AbsolutePath);
        if (!absolutePath.StartsWith(ResourcePathPrefix, StringComparison.Ordinal))
            return false;

        var relativePath = absolutePath[ResourcePathPrefix.Length..];
        if (relativePath.Length == 0)
            return false;

        var resourcePath = ResourceUtils.NormalizeResourcePath(relativePath);
        overrideFilePath = ResourceUtils.TryGetOverrideFilePath(resourcePath, overrideRootPath) ?? string.Empty;
        return overrideFilePath.Length > 0;
    }
}
