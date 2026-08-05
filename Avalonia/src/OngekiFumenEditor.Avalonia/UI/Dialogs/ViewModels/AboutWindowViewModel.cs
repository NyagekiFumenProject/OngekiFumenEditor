using Gekimini.Avalonia.Modules.Window.ViewModels;
using Gekimini.Avalonia.Platforms.Services.Miscellaneous;
using OngekiFumenEditor.Avalonia.Utils;
using System.Reflection;

namespace OngekiFumenEditor.Avalonia.UI.Dialogs.ViewModels;

public partial class AboutWindowViewModel : WindowViewModelBase
{
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
    }

    public void OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        IoC.Get<IMiscellaneousFeature>().OpenUrl(url);
    }
}
