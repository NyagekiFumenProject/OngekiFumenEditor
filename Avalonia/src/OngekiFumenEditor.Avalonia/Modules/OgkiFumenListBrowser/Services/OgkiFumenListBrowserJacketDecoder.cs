#nullable enable

using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Cryptography;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Platforms.Services.FileSystem.Providers;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ImageSharpImage = SixLabors.ImageSharp.Image;

namespace OngekiFumenEditor.Avalonia.Modules.OgkiFumenListBrowser.Services;

[RegisterSingleton<IOgkiFumenListBrowserJacketDecoder>]
public sealed class OgkiFumenListBrowserJacketDecoder : IOgkiFumenListBrowserJacketDecoder
{
    private const int MaxParallelDecodes = 2;
    private static readonly byte[] UnityAssetBundleMagic = "UnityFS"u8.ToArray();
    private static readonly SemaphoreSlim AssetBundleDecodeGate = new(MaxParallelDecodes, MaxParallelDecodes);
    private static readonly Lazy<MethodInfo?> TextureDecodeMethod = new(FindTextureDecodeMethod);
    private readonly ITemporaryFolderProvider temporaryFolderProvider;
    private readonly ConcurrentDictionary<string, WeakReference<byte[]>> memoryCache = new(StringComparer.Ordinal);

    public OgkiFumenListBrowserJacketDecoder(ITemporaryFolderProvider temporaryFolderProvider)
    {
        this.temporaryFolderProvider = temporaryFolderProvider ?? throw new ArgumentNullException(nameof(temporaryFolderProvider));
    }

    public async Task<byte[]?> LoadPngBytesAsync(
        ISimpleFile sourceFile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceFile);
        var sourceBytes = await sourceFile.ReadAllBytesAsync(cancellationToken).ConfigureAwait(false);
        var cacheKey = Convert.ToHexString(SHA256.HashData(sourceBytes));

        if (memoryCache.TryGetValue(cacheKey, out var weak) && weak.TryGetTarget(out var memoryBytes))
            return memoryBytes;

        var cached = await TryReadCacheAsync(cacheKey, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            Remember(cacheKey, cached);
            return cached;
        }

        byte[]? pngBytes;
        if (!IsUnityAssetBundle(sourceBytes))
        {
            pngBytes = sourceBytes;
        }
        else
        {
            await AssetBundleDecodeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                pngBytes = await DecodeAssetBundleAsync(
                    sourceBytes,
                    sourceFile.FileName,
                    cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                AssetBundleDecodeGate.Release();
            }
        }
        if (pngBytes is null)
            return null;

        Remember(cacheKey, pngBytes);
        await TryWriteCacheAsync(cacheKey, pngBytes, cancellationToken).ConfigureAwait(false);
        return pngBytes;
    }

    private async Task<byte[]?> TryReadCacheAsync(
        string cacheKey,
        CancellationToken cancellationToken)
    {
        if (!temporaryFolderProvider.IsAvailable)
            return null;

        try
        {
            var cacheDirectory = await temporaryFolderProvider.Root
                .GetOrCreateDirectoryAsync("OgkiFumenListBrowserJackets", cancellationToken)
                .ConfigureAwait(false);
            var cacheFile = await cacheDirectory.TryGetFileAsync(
                cacheKey + ".png",
                cancellationToken).ConfigureAwait(false);
            return cacheFile is null
                ? null
                : await cacheFile.ReadAllBytesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            Log.LogDebug($"Unable to read cached Ogki jacket '{cacheKey}': {exception.Message}");
            return null;
        }
    }

    private async Task TryWriteCacheAsync(
        string cacheKey,
        byte[] pngBytes,
        CancellationToken cancellationToken)
    {
        if (!temporaryFolderProvider.IsAvailable)
            return;

        try
        {
            var cacheDirectory = await temporaryFolderProvider.Root
                .GetOrCreateDirectoryAsync("OgkiFumenListBrowserJackets", cancellationToken)
                .ConfigureAwait(false);
            var cacheFile = await cacheDirectory.GetOrCreateFileAsync(
                cacheKey + ".png",
                cancellationToken).ConfigureAwait(false);
            await cacheFile.WriteAllBytesAsync(pngBytes, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            // A cache is an optimization. A read-only or unavailable temporary provider
            // must not make an otherwise valid jacket disappear.
            Log.LogDebug($"Unable to cache Ogki jacket '{cacheKey}': {exception.Message}");
        }
    }

    private static Task<byte[]?> DecodeAssetBundleAsync(
        byte[] bundleBytes,
        string fileName,
        CancellationToken cancellationToken)
    {
        var decodeMethod = TextureDecodeMethod.Value;
        if (decodeMethod is null)
        {
            Log.LogWarn("Unity jacket decoding is unavailable because TexturePlugin.dll could not be loaded.");
            return Task.FromResult<byte[]?>(null);
        }

        cancellationToken.ThrowIfCancellationRequested();
        // AssetBundle parsing and texture decompression are CPU-bound; keep them off
        // Avalonia's UI thread while retaining cancellation checks inside the loop.
        return Task.Run(
            () => DecodeAssetBundleCore(bundleBytes, fileName, decodeMethod, cancellationToken),
            cancellationToken);
    }

    private static byte[]? DecodeAssetBundleCore(
        byte[] bundleBytes,
        string fileName,
        MethodInfo decodeMethod,
        CancellationToken cancellationToken)
    {
        var assetManager = new AssetsManager();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var bundleStream = new MemoryStream(bundleBytes, writable: false);
            var bundle = assetManager.LoadBundleFile(bundleStream, fileName);
            var assetsFile = assetManager.LoadAssetsFileFromBundle(bundle, 0);
            foreach (var assetInfo in assetsFile.table.GetAssetsOfType(0x1C))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var baseField = assetManager.GetTypeInstance(assetsFile.file, assetInfo).GetBaseField();
                    var width = baseField["m_Width"].GetValue().AsInt();
                    var height = baseField["m_Height"].GetValue().AsInt();
                    if (width <= 0 || height <= 0)
                        continue;

                    var format = (TextureFormat)baseField["m_TextureFormat"].GetValue().AsInt();
                    var encodedPixels = ReadTextureBytes(bundle, baseField);
                    if (encodedPixels is null or { Length: 0 })
                        continue;

                    var decodedPixels = DecodeTexture(
                        decodeMethod,
                        encodedPixels,
                        width,
                        height,
                        format);
                    if (decodedPixels is null or { Length: 0 })
                        continue;

                    return EncodePng(decodedPixels, width, height);
                }
                catch (Exception exception)
                {
                    Log.LogDebug($"Unable to decode one Unity jacket texture in '{fileName}': {exception.Message}");
                }
            }

            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            Log.LogDebug($"Unable to open Unity jacket bundle '{fileName}': {exception.Message}");
            return null;
        }
        finally
        {
            assetManager.UnloadAll();
        }
    }

    private static byte[]? ReadTextureBytes(
        BundleFileInstance bundle,
        AssetTypeValueField baseField)
    {
        var streamData = baseField["m_StreamData"];
        var streamPath = streamData["path"].GetValue().AsString();
        if (!string.IsNullOrWhiteSpace(streamPath))
        {
            var offset = streamData["offset"].GetValue().AsUInt();
            var size = streamData["size"].GetValue().AsUInt();
            var leafName = GetLeafName(streamPath);
            var directoryEntries = bundle.file.bundleInf6?.dirInf ?? [];
            foreach (var directoryEntry in directoryEntries)
            {
                if (!directoryEntry.name.Equals(leafName, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (size > int.MaxValue)
                    return null;

                var reader = bundle.file.reader;
                reader.Position = bundle.file.bundleHeader6.GetFileDataOffset() + directoryEntry.offset + offset;
                return reader.ReadBytes((int)size);
            }
        }

        var imageData = baseField["image data"].GetValue().value.asByteArray;
        if (imageData.size == 0)
            return null;
        var result = new byte[imageData.size];
        Array.Copy(imageData.data, result, imageData.size);
        return result;
    }

    private static byte[]? DecodeTexture(
        MethodInfo decodeMethod,
        byte[] encodedPixels,
        int width,
        int height,
        TextureFormat format)
    {
        try
        {
            return decodeMethod.Invoke(null, [encodedPixels, width, height, format]) as byte[];
        }
        catch (TargetInvocationException exception)
        {
            Log.LogDebug($"TexturePlugin failed to decode format '{format}': {exception.InnerException?.Message ?? exception.Message}");
            return null;
        }
        catch (Exception exception)
        {
            Log.LogDebug($"TexturePlugin invocation failed for format '{format}': {exception.Message}");
            return null;
        }
    }

    private static byte[] EncodePng(byte[] rgbaBytes, int width, int height)
    {
        using var image = ImageSharpImage.LoadPixelData<Rgba32>(rgbaBytes, width, height);
        image.Mutate(static context => context.Flip(FlipMode.Vertical));
        using var output = new MemoryStream();
        image.SaveAsPng(output);
        return output.ToArray();
    }

    private void Remember(string cacheKey, byte[] bytes)
    {
        memoryCache[cacheKey] = new WeakReference<byte[]>(bytes);
    }

    private static bool IsUnityAssetBundle(byte[] bytes) =>
        bytes.Length >= UnityAssetBundleMagic.Length &&
        bytes.AsSpan(0, UnityAssetBundleMagic.Length).SequenceEqual(UnityAssetBundleMagic);

    private static string GetLeafName(string value)
    {
        var trimmed = value.StartsWith("archive:/", StringComparison.OrdinalIgnoreCase)
            ? value["archive:/".Length..]
            : value;
        var slash = Math.Max(trimmed.LastIndexOf('/'), trimmed.LastIndexOf('\\'));
        return slash < 0 ? trimmed : trimmed[(slash + 1)..];
    }

    private static MethodInfo? FindTextureDecodeMethod()
    {
        var type = Type.GetType(
            "TexturePlugin.TextureEncoderDecoder, TexturePlugin",
            throwOnError: false,
            ignoreCase: false);
        return type?.GetMethod(
            "Decode",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [typeof(byte[]), typeof(int), typeof(int), typeof(TextureFormat)],
            modifiers: null);
    }
}
