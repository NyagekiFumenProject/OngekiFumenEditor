using Avalonia.Controls;
using OngekiFumenEditor.Avalonia.Utils;
using System.Reflection;

namespace OngekiFumenEditor.Avalonia.UI.Dialogs;

public partial class AboutWindow : Window
{
    public string CommitHash { get; }
    public string Version { get; }
    public string ProductVersion { get; }
    public string BuildTime { get; }
    public string BuildConfiguration { get; }
    public string CommitDate { get; }

    public bool IsNotifyUpdateSuccess { get; }
    public string SourceVersion { get; }

    public AboutWindow(bool isNotifyUpdateSuccess = false, global::System.Version sourceVersion = null)
    {
        IsNotifyUpdateSuccess = isNotifyUpdateSuccess;
        SourceVersion = sourceVersion?.ToString();

        var assembly = typeof(AboutWindow).Assembly;
        Version = assembly.GetName().Version?.ToString() ?? string.Empty;
        ProductVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? Version;
        BuildConfiguration = assembly
            .GetCustomAttribute<AssemblyConfigurationAttribute>()
            ?.Configuration ?? string.Empty;
        BuildTime = assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(x => x.Key == "BuildDateTime")
            ?.Value ?? string.Empty;

        CommitHash = string.Empty;
        CommitDate = string.Empty;

        DataContext = this;
    }

    public void OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;
        ProcessUtils.OpenUrl(url);
    }
}
