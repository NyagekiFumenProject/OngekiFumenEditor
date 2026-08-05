using Gekimini.Avalonia.Modules.Window.ViewModels;
using Gekimini.Avalonia.Platforms.Services.Miscellaneous;
using OngekiFumenEditor.Avalonia.Utils;
using System.Globalization;
using System.Reflection;

namespace OngekiFumenEditor.Avalonia.UI.Dialogs.ViewModels;

public partial class AboutWindowViewModel : WindowViewModelBase
{
    private const int ShortCommitHashLength = 7;

    public string CommitHash { get; }
    public string Version { get; }
    public string ProductVersion { get; }
    public string BuildTime { get; }
    public string BuildConfiguration { get; }
    public string CommitDate { get; }

    public bool IsNotifyUpdateSuccess { get; }
    public string SourceVersion { get; }

    public AboutWindowViewModel()
        : this(false, null)
    {
    }

    public AboutWindowViewModel(bool isNotifyUpdateSuccess, global::System.Version sourceVersion)
    {
        IsNotifyUpdateSuccess = isNotifyUpdateSuccess;
        SourceVersion = sourceVersion?.ToString();

        var assembly = typeof(AboutWindowViewModel).Assembly;
        Version = assembly.GetName().Version?.ToString() ?? string.Empty;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? Version;
        ProductVersion = ShortenProductVersion(informationalVersion, out var commitHash);
        CommitHash = string.IsNullOrEmpty(commitHash) ? "N/A" : commitHash;
        BuildConfiguration = assembly
            .GetCustomAttribute<AssemblyConfigurationAttribute>()
            ?.Configuration ?? string.Empty;
        BuildTime = FormatTimestamp(GetAssemblyMetadata(assembly, "BuildDateTime"));
        CommitDate = FormatTimestamp(GetAssemblyMetadata(assembly, "GitCommitDate"));
    }

    internal static string ShortenProductVersion(string informationalVersion, out string commitHash)
    {
        commitHash = string.Empty;
        if (string.IsNullOrWhiteSpace(informationalVersion))
            return string.Empty;

        var metadataSeparator = informationalVersion.IndexOf('+');
        if (metadataSeparator < 0 || metadataSeparator == informationalVersion.Length - 1)
            return informationalVersion;

        var metadataParts = informationalVersion[(metadataSeparator + 1)..].Split('.');
        var commitPartIndex = -1;
        for (var i = 0; i < metadataParts.Length; i++)
        {
            var part = metadataParts[i];
            if (part.Length < ShortCommitHashLength || !part.All(Uri.IsHexDigit))
                continue;

            if (commitPartIndex < 0 || part.Length > metadataParts[commitPartIndex].Length)
                commitPartIndex = i;
        }

        if (commitPartIndex < 0)
            return informationalVersion;

        commitHash = metadataParts[commitPartIndex][..ShortCommitHashLength];
        metadataParts[commitPartIndex] = commitHash;
        return $"{informationalVersion[..(metadataSeparator + 1)]}{string.Join('.', metadataParts)}";
    }

    private static string GetAssemblyMetadata(Assembly assembly, string key) =>
        assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(x => x.Key == key)
            ?.Value;

    private static string FormatTimestamp(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "N/A";

        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces,
            out var timestamp)
            ? timestamp.ToLocalTime().ToString("yyyy/M/dd H:mm:ss.fff", CultureInfo.InvariantCulture)
            : "N/A";
    }

    public void OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        IoC.Get<IMiscellaneousFeature>().OpenUrl(url);
    }
}
