using Avalonia;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using OngekiFumenEditor.Avalonia.Kernel.Audio;
using OngekiFumenEditor.Avalonia.Parser;
using OngekiFumenEditor.Avalonia.Assets.Languages;

namespace OngekiFumenEditor.Avalonia.Utils;

public static class FileDialogHelper
{
    public static string BuildExtensionFilter(IEnumerable<(string ext, string desc)> extParams)
        => string.Join("|", extParams.Select(x => $"{x.desc} ({x.ext})|*{x.ext}"));

    public static string BuildExtensionFilter(params (string ext, string desc)[] extParams)
        => BuildExtensionFilter(extParams.AsEnumerable());

    private static string BuildExtensionFilterAndAll(IEnumerable<(string ext, string desc)> extParams)
        => $"{Lang.AllSupportFileFormat} *.*|{string.Join(";", extParams.Select(x => $"*{x.ext}"))}|{BuildExtensionFilter(extParams)}";

    private static TopLevel GetTopLevel()
    {
        return (Application.Current as App)?.TopLevel;
    }

    private static List<FilePickerFileType> BuildPickerFilters(IEnumerable<(string ext, string desc)> extParams)
    {
        var exts = extParams?.ToArray() ?? [];
        var list = new List<FilePickerFileType>();

        if (exts.Length > 0)
        {
            list.Add(new FilePickerFileType(Lang.AllSupportFileFormat)
            {
                Patterns = exts.Select(x => $"*{x.ext}").Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            });

            foreach (var group in exts.GroupBy(x => x.desc))
            {
                list.Add(new FilePickerFileType(group.Key)
                {
                    Patterns = group.Select(x => $"*{x.ext}").Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
                });
            }
        }

        return list;
    }

    public static string GetSupportFumenFileExtensionFilter()
        => BuildExtensionFilter(IoC.Get<IFumenParserManager>().GetSerializerDescriptions().SelectMany(x => x.fileFormat.Select(y => (y, x.desc))));

    public static string GetSupportAudioFileExtensionFilter()
        => BuildExtensionFilter(IoC.Get<IAudioManager>().SupportAudioFileExtensionList);

    public static IEnumerable<(string ext, string desc)> GetSupportFumenFileExtensionFilterList()
        => IoC.Get<IFumenParserManager>().GetSerializerDescriptions().SelectMany(x => x.fileFormat.Select(y => (y, x.desc)));

    public static IEnumerable<(string ext, string desc)> GetSupportAudioFileExtensionFilterList()
        => IoC.Get<IAudioManager>().SupportAudioFileExtensionList;

    public static async Task<string> OpenFileAsync(string title, IEnumerable<(string ext, string desc)> extParams)
    {
        var topLevel = GetTopLevel();
        if (topLevel is null)
        {
            Log.LogWarn($"OpenFileAsync('{title}') failed because no active TopLevel.");
            return default;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = BuildPickerFilters(extParams)
        });

        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    public static async Task<string> SaveFileAsync(string title, IEnumerable<(string ext, string desc)> extParams)
    {
        var topLevel = GetTopLevel();
        if (topLevel is null)
        {
            Log.LogWarn($"SaveFileAsync('{title}') failed because no active TopLevel.");
            return default;
        }

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            FileTypeChoices = BuildPickerFilters(extParams)
        });

        return file?.TryGetLocalPath();
    }

    public static async Task<string> OpenDirectoryAsync(string title)
    {
        var topLevel = GetTopLevel();
        if (topLevel is null)
        {
            Log.LogWarn($"OpenDirectoryAsync('{title}') failed because no active TopLevel.");
            return default;
        }

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        return folders.FirstOrDefault()?.TryGetLocalPath();
    }
}

