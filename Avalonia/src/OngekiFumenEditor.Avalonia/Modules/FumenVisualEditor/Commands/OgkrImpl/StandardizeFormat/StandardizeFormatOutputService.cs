using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Injectio.Attributes;
using Microsoft.Extensions.Logging;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Utils;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.OgkrImpl.StandardizeFormat;

public interface IStandardizeFormatOutputService
{
    Task<ISimpleFile> PickOutputFileAsync();

    bool CanRevealOutputDirectory(ISimpleFile outputFile);

    Task<bool> RevealOutputDirectoryAsync(ISimpleFile outputFile);
}

[RegisterSingleton<IStandardizeFormatOutputService>]
public sealed class StandardizeFormatOutputService : IStandardizeFormatOutputService
{
    private readonly ILogger<StandardizeFormatOutputService> logger;

    public StandardizeFormatOutputService(ILogger<StandardizeFormatOutputService> logger)
    {
        this.logger = logger;
    }

    public async Task<ISimpleFile> PickOutputFileAsync()
    {
        logger.LogInformation("Requesting standardized fumen output file path.");
        var outputFile = await FileDialogHelper.SaveFileAsync(
            Lang.NewFumenFileSavePath,
            [(".ogkr", Lang.OngekiFumenStandardized)],
            suggestedFileName: "standardized.ogkr",
            defaultExtension: "ogkr");
        if (outputFile is null)
            logger.LogInformation("Standardized fumen output file picking canceled.");
        else
            logger.LogInformation("Standardized fumen output file selected: '{Path}'.", outputFile.FullPath);
        return outputFile;
    }

    public bool CanRevealOutputDirectory(ISimpleFile outputFile) =>
        TryGetOutputDirectory(outputFile, out _) && TryGetTopLevel() is not null;

    public async Task<bool> RevealOutputDirectoryAsync(ISimpleFile outputFile)
    {
        logger.LogInformation("Revealing output directory for '{Path}'.", outputFile?.LocalPath ?? "(unknown)");
        if (!TryGetOutputDirectory(outputFile, out var outputDirectory) ||
            TryGetTopLevel() is not { } topLevel)
        {
            logger.LogWarning("Cannot reveal output directory for '{Path}'.", outputFile?.LocalPath ?? "(unknown)");
            return false;
        }

        var launched = await topLevel.Launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(outputDirectory));
        logger.LogInformation("Reveal output directory result for '{Path}': {Launched}.", outputFile.LocalPath, launched);
        return launched;
    }

    internal static bool TryGetOutputDirectory(ISimpleFile outputFile, out string outputDirectory)
    {
        outputDirectory = outputFile?.LocalPath is { } localPath
            ? Path.GetDirectoryName(localPath)
            : null;
        return !string.IsNullOrWhiteSpace(outputDirectory);
    }

    private static TopLevel TryGetTopLevel()
    {
        try
        {
            return (Application.Current as App)?.TopLevel;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
