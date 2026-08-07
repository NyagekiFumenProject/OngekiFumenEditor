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
    public Task<ISimpleFile> PickOutputFileAsync() =>
        FileDialogHelper.SaveFileAsync(
            Lang.NewFumenFileSavePath,
            [(".ogkr", Lang.OngekiFumenStandardized)],
            suggestedFileName: "standardized.ogkr",
            defaultExtension: "ogkr");

    public bool CanRevealOutputDirectory(ISimpleFile outputFile) =>
        TryGetOutputDirectory(outputFile, out _) && TryGetTopLevel() is not null;

    public async Task<bool> RevealOutputDirectoryAsync(ISimpleFile outputFile)
    {
        if (!TryGetOutputDirectory(outputFile, out var outputDirectory) ||
            TryGetTopLevel() is not { } topLevel)
            return false;

        return await topLevel.Launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(outputDirectory));
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
