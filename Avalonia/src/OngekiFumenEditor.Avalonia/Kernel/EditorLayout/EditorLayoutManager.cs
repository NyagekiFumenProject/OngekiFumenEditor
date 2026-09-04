using System.IO;
using Gekimini.Avalonia.Models.Settings;
using Gekimini.Avalonia.Modules.Shell;
using Gekimini.Avalonia.Platforms.Services.Settings;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Kernel.EditorLayout;

[RegisterSingleton<IEditorLayoutManager>]
public class EditorLayoutManager : IEditorLayoutManager
{
    private readonly ISettingManager settingManager;

    public EditorLayoutManager(ISettingManager settingManager)
    {
        this.settingManager = settingManager;
    }

    public async Task<bool> LoadLayout(Stream intputLayoutDataStream)
    {
        var shell = IoC.Get<IShell>();

        if (intputLayoutDataStream is null)
            return await shell.LoadLayout();

        string json;
        using (var reader = new StreamReader(intputLayoutDataStream, leaveOpen: true))
            json = await reader.ReadToEndAsync();

        if (string.IsNullOrWhiteSpace(json))
        {
            Log.LogWarning("LoadLayout(Stream): stream payload is empty, skip layout restoring.");
            return false;
        }

        settingManager.LoadAndSave(GekiminiSetting.JsonTypeInfo, setting => setting.ShellLayout = json);
        return await shell.LoadLayout();
    }

    public async Task<bool> SaveLayout(Stream outputLayoutDataStream)
    {
        var shell = IoC.Get<IShell>();
        var result = await shell.SaveLayout();
        if (result && outputLayoutDataStream is not null)
        {
            var json = settingManager.GetSetting(GekiminiSetting.JsonTypeInfo).ShellLayout;
            using (var writer = new StreamWriter(outputLayoutDataStream, leaveOpen: true))
                await writer.WriteAsync(json);
            await outputLayoutDataStream.FlushAsync();
        }

        return result;
    }

    public Task<bool> ApplyDefaultSuggestEditorLayout()
    {
        return IoC.Get<IShell>().LoadLayout();
    }
}
