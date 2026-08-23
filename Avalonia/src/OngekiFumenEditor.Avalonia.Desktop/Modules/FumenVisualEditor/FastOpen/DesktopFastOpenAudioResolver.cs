using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.XPath;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Desktop.Modules.FumenVisualEditor.FastOpen;

/// <summary>
///     FastOpen 的音频自动发现规则，保持与原 WPF 项目一致：
///     Music.xml 的 MusicSourceName/id 优先，谱面文件名 (\d+)_\d+ 回退，
///     目标目录为谱面目录上两级的 musicsource/musicsourceNNNN，
///     谱面位于 package 树内时允许在 package 根内递归查找。
///     返回 null 表示自动发现失败，需要用户手动选择音频。
/// </summary>
public static class DesktopFastOpenAudioResolver
{
    internal static IReadOnlyList<string> GetExternalAwbFileNameCandidates(
        string acbFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(acbFileName);
        var stem = Path.GetFileNameWithoutExtension(acbFileName);
        return
        [
            $"{stem}_streamfiles.awb",
            $"{stem}.awb",
            $"{stem}_STR.awb"
        ];
    }

    internal static string TryResolveExternalAwbFilePath(string acbFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(acbFilePath);
        if (!Path.GetExtension(acbFilePath).Equals(".acb", StringComparison.OrdinalIgnoreCase))
            return null;

        var directory = Path.GetDirectoryName(acbFilePath);
        if (string.IsNullOrWhiteSpace(directory))
            return null;

        foreach (var candidateName in GetExternalAwbFileNameCandidates(acbFilePath))
        {
            var candidatePath = Path.Combine(directory, candidateName);
            if (File.Exists(candidatePath))
                return candidatePath;
        }

        return null;
    }

    public static async Task<string?> TryResolveAudioFilePathAsync(
        string ogkrFilePath,
        IReadOnlyList<string> supportedAudioExtensions)
    {
        ArgumentNullException.ThrowIfNull(ogkrFilePath);
        ArgumentNullException.ThrowIfNull(supportedAudioExtensions);

        var ogkrFileDir = Path.GetDirectoryName(ogkrFilePath) ?? string.Empty;
        var musicId = await ReadMusicIdFromMusicXmlAsync(ogkrFileDir);

        if (musicId is null)
        {
            musicId = ReadMusicIdFromFileName(ogkrFilePath);
            if (musicId is null)
                return null;
        }

        var musicIdStr = FormatMusicId(musicId.Value);
        var musicSourceFolder = Path.GetFullPath(Path.Combine(
            ogkrFileDir, "..", "..", "musicsource", $"musicsource{musicIdStr}"));

        if (!Directory.Exists(musicSourceFolder))
        {
            // 只在明确的 package 根内递归，不能从任意用户路径向整个磁盘扩散。
            var packageFolder = FindConstrainedPackageRoot(ogkrFilePath, ogkrFileDir);
            if (packageFolder is null)
            {
                Log.LogWarn($"FastOpen: musicsource folder not found for musicId {musicIdStr}.");
                return null;
            }

            musicSourceFolder = Directory.GetDirectories(
                packageFolder, $"musicsource{musicIdStr}", SearchOption.AllDirectories).FirstOrDefault() ?? string.Empty;
        }

        if (!Directory.Exists(musicSourceFolder))
        {
            Log.LogWarn($"FastOpen: musicsource folder for musicId {musicIdStr} missing after package scan.");
            return null;
        }

        var candidates = Directory.GetFiles(musicSourceFolder, $"music{musicIdStr}.*")
            .Where(x => supportedAudioExtensions.Any(t =>
                x.EndsWith(t, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        if (candidates.Length == 0)
        {
            Log.LogWarn($"FastOpen: no supported audio file (music{musicIdStr}.*) under {musicSourceFolder}.");
            return null;
        }

        if (candidates.Length > 1)
            Log.LogWarn($"FastOpen: multiple audio candidates found, using the first one: {candidates[0]}");

        return candidates[0];
    }

    private static async Task<int?> ReadMusicIdFromMusicXmlAsync(string ogkrFileDir)
    {
        var musicXmlFilePath = Path.Combine(ogkrFileDir, "Music.xml");
        if (!File.Exists(musicXmlFilePath))
            return null;

        try
        {
            await using var xmlStream = File.OpenRead(musicXmlFilePath);
            var musicXml = await XDocument.LoadAsync(xmlStream, LoadOptions.None, default);
            var element = musicXml.XPathSelectElement(@"//MusicSourceName[1]/id[1]");
            if (element is not null && int.TryParse(element.Value, out var parsed) && parsed >= 0)
                return parsed;

            Log.LogWarn("FastOpen: Music.xml exists but has no valid MusicSourceName/id.");
            return null;
        }
        catch (Exception exception)
        {
            Log.LogWarn($"FastOpen: failed to read Music.xml ({exception.Message}), fallback to file name matching.");
            return null;
        }
    }

    private static int? ReadMusicIdFromFileName(string ogkrFilePath)
    {
        var match = new Regex(@"(\d+)_\d+").Match(Path.GetFileNameWithoutExtension(ogkrFilePath));
        if (match.Success && int.TryParse(match.Groups[1].Value, out var parsed) && parsed >= 0)
            return parsed;

        Log.LogWarn("FastOpen: can't resolve music id from Music.xml or the fumen file name.");
        return null;
    }

    internal static string FormatMusicId(int musicId)
    {
        var raw = musicId.ToString();
        return musicId < 1000 ? string.Concat("0".Repeat(4 - raw.Length)) + musicId : raw;
    }

    private static string? FindConstrainedPackageRoot(string ogkrFilePath, string ogkrFileDir)
    {
        var idx = ogkrFileDir.LastIndexOf("/package", StringComparison.OrdinalIgnoreCase);
        idx = idx < 0 ? ogkrFileDir.LastIndexOf("\\package", StringComparison.OrdinalIgnoreCase) : idx;
        if (idx < 0)
            return null;

        return ogkrFilePath.Substring(0, "/package".Length + idx);
    }
}
