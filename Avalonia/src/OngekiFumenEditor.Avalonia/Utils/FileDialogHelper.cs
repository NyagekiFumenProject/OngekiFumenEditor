using Avalonia;
using Avalonia.Platform.Storage;
using OngekiFumenEditor.Avalonia.Kernel.Audio;
using OngekiFumenEditor.Avalonia.Parser;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem.Impl.AvaloniaStorageProvider;

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

    public static async Task<ISimpleFile> OpenFileAsync(string title, IEnumerable<(string ext, string desc)> extParams)
    {
        var topLevel = GetTopLevel();
        if (topLevel is null)
        {
            Log.LogWarn($"OpenFileAsync('{title}') failed because no active TopLevel.");
            return default;
        }

        var storageProvider = topLevel.StorageProvider;
        if (!storageProvider.CanOpen)
        {
            Log.LogWarn($"OpenFileAsync('{title}') is not supported by the current storage provider.");
            return default;
        }

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = BuildPickerFilters(extParams)
        });

        if (files.Count == 0)
            return default;

        for (var i = 1; i < files.Count; i++)
            files[i].Dispose();

        return await AvaloniaStorageProviderFileSystemBuilder.LoadFromAvaloniaStorageFile(files[0]);
    }

    public static async Task<ISimpleFile> SaveFileAsync(
        string title,
        IEnumerable<(string ext, string desc)> extParams,
        string suggestedFileName = null,
        string defaultExtension = null)
    {
        var topLevel = GetTopLevel();
        if (topLevel is null)
        {
            Log.LogWarn($"SaveFileAsync('{title}') failed because no active TopLevel.");
            return default;
        }

        var storageProvider = topLevel.StorageProvider;
        if (!storageProvider.CanSave)
        {
            Log.LogWarn($"SaveFileAsync('{title}') is not supported by the current storage provider.");
            return default;
        }

        var pickerFilters = BuildPickerFilters(extParams);
        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedFileName,
            DefaultExtension = defaultExtension?.TrimStart('.'),
            FileTypeChoices = pickerFilters,
            SuggestedFileType = pickerFilters.LastOrDefault(),
            ShowOverwritePrompt = true
        });

        return file is null
            ? default
            : await AvaloniaStorageProviderFileSystemBuilder.LoadFromAvaloniaStorageFile(file);
    }

    public static async Task<ISimpleDirectory> OpenDirectoryAsync(string title)
    {
        var folder = await OpenStorageFolderAsync(title);
        return folder is null
            ? default
            : AvaloniaStorageProviderFileSystemBuilder.LoadRootFromAvaloniaStorageFolder(folder);
    }

    public static async Task<IStorageFolder> OpenStorageFolderAsync(string title)
    {
        var topLevel = GetTopLevel();
        if (topLevel is null)
        {
            Log.LogWarn($"OpenDirectoryAsync('{title}') failed because no active TopLevel.");
            return default;
        }

        var storageProvider = topLevel.StorageProvider;
        if (!storageProvider.CanPickFolder)
        {
            Log.LogWarn($"OpenDirectoryAsync('{title}') is not supported by the current storage provider.");
            return default;
        }

        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        if (folders.Count == 0)
            return default;

        for (var i = 1; i < folders.Count; i++)
            folders[i].Dispose();

        return folders[0];
    }
}

