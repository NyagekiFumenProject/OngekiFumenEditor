using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Gekimini.Avalonia;
using OngekiFumenEditor.Avalonia.Browser.Utils.Interops;
using Gekimini.Avalonia.Platforms.Services.Settings;
using Injectio.Attributes;
using Microsoft.Extensions.Logging;

namespace OngekiFumenEditor.Avalonia.Browser.Platforms.Services.Settings;

[RegisterSingleton<ISettingManager>]
public class BrowserSettingManager : ISettingManager
{
    private const string persistenceStoreKey = "__browserPersistence_";
    private readonly Dictionary<string, object> cacheObj = new();
    private readonly ILogger logger;
    private readonly IServiceProvider provider;

    public BrowserSettingManager(IServiceProvider provider, ILogger<__BrowserSettingManager> logger)
    {
        this.provider = provider;
        this.logger = logger;
    }


    public T GetSetting<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(
        JsonTypeInfo<T> jsonTypeInfo) where T : new()
    {
        return LoadInternal(jsonTypeInfo);
    }

    public void SaveSetting<T>(T obj, JsonTypeInfo<T> jsonTypeInfo)
    {
        if (obj is null)
            return;

        var key = GetKey<T>();
        var jsonContent = JsonSerializer.Serialize(obj, jsonTypeInfo);
        SetLocalStorage(key, jsonContent);
        cacheObj[key] = obj;
        logger.LogDebugEx($"save/update cached {typeof(T).Name} object, hash = {obj.GetHashCode()}");
    }

    private T LoadInternal<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        T>(JsonTypeInfo<T> jsonTypeInfo) where T : new()
    {
        var key = GetKey<T>();
        var typeName = typeof(T).Name;

        if (cacheObj.TryGetValue(key, out var obj))
        {
            logger.LogDebugEx($"return cached {typeName} object, hash = {obj.GetHashCode()}");
            return (T) obj;
        }

        var jsonContent = GetLocalStorage(key);
        T cw;
        if (!string.IsNullOrWhiteSpace(jsonContent))
        {
            cw = JsonSerializer.Deserialize(jsonContent, jsonTypeInfo);
            logger.LogDebugEx($"create new {typeName} object from browser storage, hash = {cw.GetHashCode()}");
        }
        else
        {
            cw = provider.Resolve<T>();
            logger.LogDebugEx(
                $"create new {typeName} object from ActivatorUtilities.CreateInstance(), hash = {cw.GetHashCode()}");
        }

        cacheObj[key] = cw;
        return cw;
    }

    private string GetKey<T>()
    {
        return persistenceStoreKey + typeof(T).FullName.Replace(".", "_");
    }

    private void SetLocalStorage(string key, string value)
    {
        LocalStorageInterop.Save(key, value);
        logger.LogDebugEx($"setting from storage {key} = {value}");
    }

    private string GetLocalStorage(string key)
    {
        var value = LocalStorageInterop.Load(key);

        logger.LogDebugEx($"getting from storage {key} = {value}");
        return value;
    }
}