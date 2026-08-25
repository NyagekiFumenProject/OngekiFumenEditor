#nullable enable

using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using OngekiFumenEditor.Avalonia.Kernel.Audio;
using OngekiFumenEditor.Avalonia.Modules.OgkiFumenListBrowser.Models;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;

namespace OngekiFumenEditor.Avalonia.Modules.OgkiFumenListBrowser.Services;

/// <summary>
/// Scans one selected simple-file-system root. The scanner never falls back to a local path.
/// </summary>
public sealed class OgkiFumenListBrowserScanner
{
    private const int MaxParallelOperations = 4;
    private static readonly Regex BpmRegex = new(@"BPM_DEF\s*([\d.]+)", RegexOptions.Compiled);
    private static readonly Regex CreatorRegex = new(@"CREATOR\s*(.+)", RegexOptions.Compiled);
    private readonly HashSet<string> supportedAudioExtensions;

    public OgkiFumenListBrowserScanner(IEnumerable<string> supportedAudioExtensions)
    {
        ArgumentNullException.ThrowIfNull(supportedAudioExtensions);
        this.supportedAudioExtensions = supportedAudioExtensions
            .Select(NormalizeExtension)
            .Where(x => x.Length > 1)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public OgkiFumenListBrowserScanner(IAudioManager audioManager)
        : this(audioManager?.SupportAudioFileExtensionList.Select(x => x.fileExt)
            ?? throw new ArgumentNullException(nameof(audioManager)))
    {
    }

    public async Task<IReadOnlyList<OngekiFumenSet>> ScanAsync(
        ISimpleDirectory root,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(root);
        var entries = EnumerateFiles(root, cancellationToken).ToArray();

        var audioBySourceId = new Dictionary<int, AudioResource>();
        var jacketsByMusicId = new Dictionary<int, JacketResource>();
        var audioEntries = entries
            .Where(static entry => entry.Capability.FileName.Equals("MusicSource.xml", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var audioResources = await RunBoundedAsync(
            audioEntries,
            (entry, token) => TryReadAudioResourceAsync(entry.Capability, entry.Locator, token),
            cancellationToken).ConfigureAwait(false);
        foreach (var resource in audioResources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (resource is not null && !audioBySourceId.ContainsKey(resource.SourceId))
                audioBySourceId.Add(resource.SourceId, resource);
        }

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (TryParseJacket(entry.Capability.FileName, entry.Locator, out var musicId))
                jacketsByMusicId.TryAdd(musicId, new JacketResource(entry.Capability, entry.Locator));
        }

        var musicEntries = entries
            .Where(static entry => entry.Capability.FileName.Equals("Music.xml", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var fumenSets = await RunBoundedAsync(
            musicEntries,
            (entry, token) => TryReadFumenSetAsync(entry.Capability, entry.Locator, token),
            cancellationToken).ConfigureAwait(false);

        var result = new List<OngekiFumenSet>(fumenSets.Length);
        foreach (var set in fumenSets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (set is null || !audioBySourceId.TryGetValue(set.MusicSourceId, out var audio))
                continue;

            set.AudioFile = audio.File;
            set.AudioLocator = audio.Locator;
            set.AudioAwbFile = audio.ExternalAwbFile;
            set.AudioAwbLocator = audio.ExternalAwbLocator;
            if (jacketsByMusicId.TryGetValue(set.MusicId, out var jacket))
            {
                set.JacketFile = jacket.File;
                set.JacketLocator = jacket.Locator;
            }

            result.Add(set);
        }

        return result
            .OrderBy(x => x.MusicId)
            .ThenBy(x => x.MusicXmlLocator, StringComparer.OrdinalIgnoreCase)
            .DistinctBy(x => x.MusicId)
            .ToArray();
    }

    public static Task<IReadOnlyList<OngekiFumenSet>> ScanAsync(
        ISimpleDirectory root,
        IEnumerable<string> supportedAudioExtensions,
        CancellationToken cancellationToken = default) =>
        new OgkiFumenListBrowserScanner(supportedAudioExtensions).ScanAsync(root, cancellationToken);

    private async Task<OngekiFumenSet?> TryReadFumenSetAsync(
        ISimpleFile musicFile,
        string locator,
        CancellationToken cancellationToken)
    {
        XDocument musicXml;
        try
        {
            musicXml = Parse(await musicFile.ReadAllBytesAsync(cancellationToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }

        var musicId = ParseInt(FindValue(musicXml, "Name", "id"));
        var sourceId = ParseInt(FindValue(musicXml, "MusicSourceName", "id"));
        if (musicId is null || sourceId is null)
            return null;

        var set = new OngekiFumenSet(
            musicFile,
            locator,
            musicId.Value,
            sourceId.Value,
            FindValue(musicXml, "Name", "str") ?? string.Empty,
            FindValue(musicXml, "ArtistName", "str") ?? string.Empty,
            FindValue(musicXml, "Genre", "str") ?? string.Empty);

        var fumenEntries = musicXml
            .Descendants("FumenData")
            .Where(x => x.Element("FumenFile") is not null)
            .ToArray();
        var diffTasks = new Task<OngekiFumenDiff?>[fumenEntries.Length];
        for (var index = 0; index < fumenEntries.Length; index++)
        {
            var entry = fumenEntries[index];
            diffTasks[index] = TryReadFumenDiffAsync(
                set,
                musicFile,
                locator,
                entry,
                index,
                cancellationToken);
        }

        var diffs = await Task.WhenAll(diffTasks).ConfigureAwait(false);
        foreach (var diff in diffs)
        {
            if (diff is not null)
                set.Difficults.Add(diff);
        }

        return set.Difficults.Count == 0 ? null : set;
    }

    private static async Task<OngekiFumenDiff?> TryReadFumenDiffAsync(
        OngekiFumenSet set,
        ISimpleFile musicFile,
        string musicLocator,
        XElement entry,
        int diffIndex,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = entry.Element("FumenFile")?.Element("path")?.Value;
        var parentLocator = GetParentLocator(musicLocator);
        if (!OgkiFumenListBrowserPath.TryCombineRelative(parentLocator, path, out var fumenLocator))
            return null;

        // A Music.xml capability only knows its own parent. Resolve through its root by
        // walking parents instead of accepting a provider-specific path string.
        var fumenFile = ResolveFromFileParent(musicFile, path ?? string.Empty);
        if (fumenFile is null)
            return null;

        var integerPart = ParseInt(entry.Element("FumenConstIntegerPart")?.Value) ?? 0;
        var fractionalPart = ParseInt(entry.Element("FumenConstFractionalPart")?.Value) ?? 0;
        var diff = new OngekiFumenDiff(set)
        {
            DiffIdx = diffIndex,
            Level = integerPart + fractionalPart / 100f,
            FumenFile = fumenFile,
            FumenLocator = fumenLocator
        };
        await ReadFumenInfoAsync(diff, cancellationToken).ConfigureAwait(false);
        return diff;
    }

    private async Task<AudioResource?> TryReadAudioResourceAsync(
        ISimpleFile musicSourceFile,
        string locator,
        CancellationToken cancellationToken)
    {
        XDocument sourceXml;
        try
        {
            sourceXml = Parse(await musicSourceFile.ReadAllBytesAsync(cancellationToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }

        var sourceId = ParseInt(FindValue(sourceXml, "Name", "id"));
        var audioLocatorValue = FindValue(sourceXml, "acbFile", "path") ??
            FindValue(sourceXml, "audioFile", "path");
        if (sourceId is null || string.IsNullOrWhiteSpace(audioLocatorValue))
            return null;

        var audioFile = ResolveFromFileParent(musicSourceFile, audioLocatorValue);
        if (audioFile is null || !supportedAudioExtensions.Contains(GetExtension(audioFile.FileName)))
            return null;

        var awbFile = default(ISimpleFile);
        string? awbLocator = null;
        if (GetExtension(audioFile.FileName).Equals(".acb", StringComparison.OrdinalIgnoreCase))
        {
            var audioExtension = GetExtension(audioFile.FileName);
            var expectedAwbName = audioFile.FileName[..^audioExtension.Length] + ".awb";
            awbFile = audioFile.ParentDictionary?.ChildFiles.FirstOrDefault(file =>
                file.FileName.Equals(expectedAwbName, StringComparison.OrdinalIgnoreCase));
            if (awbFile is null)
                return null;

            awbLocator = BuildLocator(awbFile);
            if (string.IsNullOrEmpty(awbLocator))
                return null;
        }

        var parentLocator = GetParentLocator(locator);
        if (!OgkiFumenListBrowserPath.TryCombineRelative(parentLocator, audioLocatorValue, out var audioLocator))
            return null;

        return new AudioResource(sourceId.Value, audioFile, audioLocator, awbFile, awbLocator);
    }

    private static async Task ReadFumenInfoAsync(
        OngekiFumenDiff diff,
        CancellationToken cancellationToken)
    {
        try
        {
            var lines = await diff.FumenFile.ReadAllLines().ConfigureAwait(false);
            foreach (var line in lines)
            {
                if (diff.Bpm <= 0 && BpmRegex.Match(line) is { Success: true } bpmMatch &&
                    float.TryParse(
                        bpmMatch.Groups[1].Value,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var bpm))
                {
                    diff.Bpm = bpm;
                }
                if (string.IsNullOrWhiteSpace(diff.Creator) && CreatorRegex.Match(line) is { Success: true } creatorMatch)
                    diff.Creator = creatorMatch.Groups[1].Value.Trim();
                if (diff.Bpm > 0 && !string.IsNullOrWhiteSpace(diff.Creator))
                    break;
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Chart metadata is optional; a readable chart remains visible when its header
            // is incomplete or encoded in a legacy format.
        }
    }

    private static IEnumerable<FileEntry> EnumerateFiles(
        ISimpleDirectory directory,
        CancellationToken cancellationToken,
        string locator = "")
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var file in directory.ChildFiles.OrderBy(x => x.FileName, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new(file, string.IsNullOrEmpty(locator) ? file.FileName : locator + "/" + file.FileName);
        }

        foreach (var child in directory.ChildDictionaries.OrderBy(x => x.DirectoryName, StringComparer.OrdinalIgnoreCase))
        {
            var childLocator = string.IsNullOrEmpty(locator) ? child.DirectoryName : locator + "/" + child.DirectoryName;
            foreach (var entry in EnumerateFiles(child, cancellationToken, childLocator))
                yield return entry;
        }
    }

    private static ISimpleFile? ResolveFromFileParent(ISimpleFile sourceFile, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return null;

        var trimmed = relativePath.Trim();
        if (trimmed[0] is '/' or '\\' ||
            trimmed.IndexOf(':') >= 0 ||
            (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Scheme)))
            return null;

        var parts = trimmed.Split(['/','\\'], StringSplitOptions.RemoveEmptyEntries);
        var directory = sourceFile.ParentDictionary;
        if (directory is null)
            return parts.Length == 1 && parts[0] is not "." and not ".." &&
                   sourceFile.FileName.Equals(parts[0], StringComparison.OrdinalIgnoreCase)
                ? sourceFile
                : null;

        for (var index = 0; index < parts.Length; index++)
        {
            if (parts[index] is ".")
                continue;
            if (parts[index] is "..")
            {
                directory = directory.ParentDictionary;
                if (directory is null)
                    return null;
                continue;
            }

            if (index == parts.Length - 1)
                return directory.ChildFiles.FirstOrDefault(x =>
                    x.FileName.Equals(parts[index], StringComparison.OrdinalIgnoreCase));

            directory = directory.ChildDictionaries.FirstOrDefault(x =>
                x.DirectoryName.Equals(parts[index], StringComparison.OrdinalIgnoreCase));
            if (directory is null)
                return null;
        }

        return null;
    }

    private static string BuildLocator(ISimpleFile file)
    {
        var parts = new Stack<string>();
        parts.Push(file.FileName);
        for (var parent = file.ParentDictionary; parent is not null && !string.IsNullOrEmpty(parent.DirectoryName); parent = parent.ParentDictionary)
            parts.Push(parent.DirectoryName);
        return string.Join('/', parts);
    }

    private static string GetParentLocator(string locator)
    {
        var slash = locator.LastIndexOf('/');
        return slash < 0 ? string.Empty : locator[..slash];
    }

    private static bool TryParseJacket(string fileName, string locator, out int musicId)
    {
        musicId = 0;
        const string prefix = "ui_jacket_";
        if (!fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var parentLocator = locator[..Math.Max(0, locator.LastIndexOf('/'))];
        var isAssetsEntry = parentLocator
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(x => x.Equals("assets", StringComparison.OrdinalIgnoreCase));
        if (!isAssetsEntry)
            return false;

        var tail = fileName[prefix.Length..];
        var suffixIndex = tail.IndexOf("_s", StringComparison.OrdinalIgnoreCase);
        if (suffixIndex < 0)
            return false;
        var idText = tail[..suffixIndex];
        return int.TryParse(idText, NumberStyles.Integer, CultureInfo.InvariantCulture, out musicId);
    }

    private static XDocument Parse(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        using var stream = new MemoryStream(bytes, writable: false);
        return XDocument.Load(stream, LoadOptions.None);
    }

    private static string? FindValue(XDocument document, string elementName, string childName)
    {
        return document.Descendants(elementName)
            .Select(x => x.Element(childName)?.Value)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
    }

    private static int? ParseInt(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;

    private static string NormalizeExtension(string extension)
    {
        var value = extension?.Trim() ?? string.Empty;
        if (value.Length == 0)
            return string.Empty;
        return value.StartsWith('.') ? value : "." + value;
    }

    private static string GetExtension(string fileName)
    {
        var index = fileName.LastIndexOf('.');
        return index < 0 ? string.Empty : fileName[index..];
    }

    private static async Task<TOutput[]> RunBoundedAsync<TInput, TOutput>(
        IReadOnlyList<TInput> items,
        Func<TInput, CancellationToken, Task<TOutput>> operation,
        CancellationToken cancellationToken)
    {
        var results = new TOutput[items.Count];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, items.Count),
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = MaxParallelOperations
            },
            async (index, token) =>
            {
                results[index] = await operation(items[index], token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        return results;
    }

    private sealed record FileEntry(ISimpleFile Capability, string Locator);

    private sealed record JacketResource(ISimpleFile File, string Locator);

    private sealed record AudioResource(
        int SourceId,
        ISimpleFile File,
        string Locator,
        ISimpleFile? ExternalAwbFile,
        string? ExternalAwbLocator);
}
