using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Injectio.Attributes;
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

    public StandardizeFormatOutputService()
    {
    }

    public async Task<ISimpleFile> PickOutputFileAsync()
    {
        Log.LogInfo("Requesting standardized fumen output file path.");
        var outputFile = await FileDialogHelper.SaveFileAsync(
            Lang.NewFumenFileSavePath,
            [(".ogkr", Lang.OngekiFumenStandardized)],
            suggestedFileName: "standardized.ogkr",
            defaultExtension: "ogkr");
        if (outputFile is null)
            Log.LogInfo("Standardized fumen output file picking canceled.");
        else
            Log.LogInfo($"Standardized fumen output file selected: '{outputFile.FullPath}'.");
        return outputFile;
    }

    public bool CanRevealOutputDirectory(ISimpleFile outputFile) =>
        TryGetOutputDirectory(outputFile, out _) && TryGetTopLevel() is not null;

    public async Task<bool> RevealOutputDirectoryAsync(ISimpleFile outputFile)
    {
        Log.LogInfo($"Revealing output directory for '{outputFile?.FullPath ?? "(unknown)"}'.");
        if (!TryGetOutputDirectory(outputFile, out var outputDirectory) ||
            TryGetTopLevel() is not { } topLevel)
        {
            Log.LogWarn($"Cannot reveal output directory for '{outputFile?.FullPath ?? "(unknown)"}'.");
            return false;
        }

        var launched = await topLevel.Launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(outputDirectory));
        Log.LogInfo($"Reveal output directory result for '{outputFile.FullPath}': {launched}.");
        return launched;
    }

    internal static bool TryGetOutputDirectory(ISimpleFile outputFile, out string outputDirectory)
    {
        outputDirectory = outputFile?.FullPath is { } localPath
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
