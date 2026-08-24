using System;
using System.Collections.Generic;
using OngekiFumenEditor.Avalonia.Utils;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Gekimini.Avalonia;
using Gekimini.Avalonia.Platforms.Services.Settings;
using Gekimini.Avalonia.Utils;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Browser.Platforms.Services.Settings;

//[RegisterSingleton<ISettingManager>]
public class __BrowserSettingManager : ISettingManager
{
    private const string persistenceStoreKey = "__browserPersistence";
    private readonly Dictionary<string, object> cacheObj = new();
    private readonly IServiceProvider provider;

    private Dictionary<string, string> settingMap;

    public __BrowserSettingManager(IServiceProvider provider)
    {
        this.provider = provider;
    }


    public T GetSetting<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(
        JsonTypeInfo<T> jsonTypeInfo) where T : new()
    {
        return LoadInternal(jsonTypeInfo);
    }

    public void SaveSetting<T>(T obj, JsonTypeInfo<T> jsonTypeInfo)
    {
        var key = GetKey<T>();

        settingMap[key] = JsonSerializer.Serialize(obj, jsonTypeInfo);
        var content = JsonSerializer.Serialize(settingMap, JsonSourceGenerateContext.Default.DictionaryStringString);

        // Use localStorage for browser persistence
        SetLocalStorage(persistenceStoreKey, content);
    }

    private T LoadInternal<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        T>(JsonTypeInfo<T> jsonTypeInfo)
    {
        var key = GetKey<T>();

        if (cacheObj.TryGetValue(key, out var obj))
        {
            Log.LogDebug($"return cached {typeof(T).Name} object, hash = {obj.GetHashCode()}");
            return (T) obj;
        }

        if (settingMap is null)
        {
            var content = GetLocalStorage(persistenceStoreKey);
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
                    Log.LogError($"Can't load browser settings : {e.Message}");
                    settingMap = new Dictionary<string, string>();
                }
        }
        else
        {
            settingMap = new Dictionary<string, string>();
        }

        T cw;
        if (settingMap.TryGetValue(key, out var jsonContent))
        {
            cw = JsonSerializer.Deserialize(jsonContent, jsonTypeInfo);
            Log.LogDebug($"create new {typeof(T).Name} object from browser storage, hash = {cw.GetHashCode()}");
        }
        else
        {
            cw = provider.Resolve<T>();
            Log.LogDebug(
                $"create new {typeof(T).Name} object from ActivatorUtilities.CreateInstance(), hash = {cw.GetHashCode()}");
        }

        cacheObj[key] = cw;
        return cw;
    }

    private string GetKey<T>()
    {
        return typeof(T).FullName;
    }

    private void SetLocalStorage(string key, string value)
    {
        Utils.Interops.LocalStorageInterop.Save(key, value);
        Log.LogDebug($"saved setting '{key}', length = {value?.Length ?? 0}");
    }

    private string GetLocalStorage(string key)
    {
        var value = Utils.Interops.LocalStorageInterop.Load(key);

        Log.LogDebug($"loaded setting '{key}', length = {value?.Length ?? 0}");
        return value;
    }
}
