using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Serialization.Metadata;
using CommunityToolkit.Mvvm.ComponentModel;
using Gekimini.Avalonia.Platforms.Services.Settings;
using OngekiFumenEditor.Avalonia.Avalonia;

namespace OngekiFumenEditor.Avalonia.Models.Settings;

public abstract class SettingModelBase<TSelf> : ObservableObject, ISettingModel
    where TSelf : SettingModelBase<TSelf>, new()
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> cacheProps = new();

    protected abstract JsonTypeInfo<TSelf> JsonTypeInfoCore { get; }

    public void Save()
    {
        IoC.Get<ISettingManager>().SaveSetting((TSelf)this, JsonTypeInfoCore);
    }

    public void Reload()
    {
        var loaded = IoC.Get<ISettingManager>().GetSetting(JsonTypeInfoCore);
        AssignFrom(loaded);
    }

    public virtual void Reset()
    {
        AssignFrom(new TSelf());
    }

    protected static TSelf LoadDefault(JsonTypeInfo<TSelf> jsonTypeInfo)
    {
        return IoC.Get<ISettingManager>().GetSetting(jsonTypeInfo);
    }

    private void AssignFrom(TSelf source)
    {
        var props = cacheProps.GetOrAdd(typeof(TSelf), type =>
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => x.CanRead && x.CanWrite && x.GetIndexParameters().Length is 0)
                .ToArray());

        foreach (var prop in props)
            prop.SetValue(this, prop.GetValue(source));
    }
}
