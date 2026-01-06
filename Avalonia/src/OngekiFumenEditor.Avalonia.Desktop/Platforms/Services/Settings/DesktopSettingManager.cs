using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using Gekimini.Avalonia;
using Gekimini.Avalonia.Framework.Dialogs;
using Gekimini.Avalonia.Platforms.Services.Settings;
using Gekimini.Avalonia.Utils;
using Injectio.Attributes;
using Microsoft.Extensions.Logging;

namespace OngekiFumenEditor.Avalonia.Desktop.Platforms.Services.Settings;

[RegisterSingleton<ISettingManager>]
public class DesktopSettingManager : ISettingManager
{
    private readonly Dictionary<string, object> cacheObj = new();
    private readonly IDialogManager dialogManager;
    private readonly object locker = new();
    private readonly ILogger logger;
    private readonly IServiceProvider provider;
    private readonly string savePath;

    private Dictionary<string, string> settingMap;

    public DesktopSettingManager(IServiceProvider provider, ILogger<DesktopSettingManager> logger,
        IDialogManager dialogManager)
    {
        this.provider = provider;
        this.logger = logger;
        this.dialogManager = dialogManager;
        savePath = Path.Combine(AppContext.BaseDirectory,
            "setting.json");
    }

    public void SaveSetting<T>(T obj, JsonTypeInfo<T> typeInfo)
    {
#if DEBUG
        if (DesignModeHelper.IsDesignMode)
            return;
#endif

        var key = GetKey<T>();

        settingMap[key] = JsonSerializer.Serialize(obj, typeInfo);
        var content = JsonSerializer.Serialize(settingMap, JsonSourceGenerateContext.Default.DictionaryStringString);

        File.WriteAllText(savePath, content);
    }

    public T GetSetting<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]T>(JsonTypeInfo<T> typeInfo) where T : new()
    {
        return LoadInternal(typeInfo);
    }

    private T LoadInternal<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(
        JsonTypeInfo<T> typeInfo)
    {
        var key = GetKey<T>();

        if (cacheObj.TryGetValue(key, out var obj))
        {
            logger.LogDebugEx($"return cached {typeof(T).Name} object, hash = {obj.GetHashCode()}");
            return (T) obj;
        }

        if (settingMap is null)
        {
            if (File.Exists(savePath))
            {
                var content = File.ReadAllText(savePath);
                if (string.IsNullOrWhiteSpace(content))
                    settingMap = new Dictionary<string, string>();
                else
                    try
                    {
                        settingMap = JsonSerializer.Deserialize(content,
                            JsonSourceGenerateContext.Default.DictionaryStringString);
                    }
                    catch (Exception e)
                    {
                        logger.LogErrorEx(e, $"Can't load setting.json : {e.Message}");
                        Task.Run(async () =>
                        {
                            await dialogManager.ShowMessageDialog($"无法加载应用配置文件setting.json:{e.Message}",
                                DialogMessageType.Error);
                            Environment.Exit(-1);
                        }).Wait();
                    }
            }
            else
            {
                settingMap = new Dictionary<string, string>();
            }
        }

        T cw = default;
        if (settingMap.TryGetValue(key, out var jsonContent))
        {
            cw = JsonSerializer.Deserialize(jsonContent, typeInfo);
            logger.LogDebugEx($"create new {typeof(T).Name} object from setting.json, hash = {cw.GetHashCode()}");
        }
        else
        {
            cw = provider.Resolve<T>();
            logger.LogDebugEx(
                $"create new {typeof(T).Name} object from ActivatorUtilities.CreateInstance(), hash = {cw.GetHashCode()}");
        }

        cacheObj[key] = cw;
        return cw;
    }

    private string GetKey<T>()
    {
        return typeof(T).FullName;
    }
}