using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Utils;
using System.Reflection;

namespace OngekiFumenEditor.Avalonia.Kernel.CommandExecutor.Attributes;

public abstract class OptionBindingAttrbuteBase : Attribute
{
    protected OptionBindingAttrbuteBase(string name, string description, object defaultValue, Type type)
    {
        Name = name;
        Description = description;
        DefaultValue = defaultValue;
        Type = type;
    }

    public string Name { get; set; }
    public string Description { get; set; }
    public object DefaultValue { get; set; }
    public Type Type { get; }
    public bool Require { get; set; }
}

[AttributeUsage(AttributeTargets.Property)]
public class OptionBindingAttrbute<T> : OptionBindingAttrbuteBase
{
    public OptionBindingAttrbute(string name, string description, T defaultValue) : base(name, description, defaultValue, typeof(T))
    {
    }
}

[AttributeUsage(AttributeTargets.Property)]
public class LocalizableOptionBindingAttribute<T> : OptionBindingAttrbute<T>
{
    public LocalizableOptionBindingAttribute(string name, string resourceKey, T defaultValue, bool require = false)
        : base(name, GetResourceText(resourceKey), defaultValue)
    {
        Require = require;
#if DEBUG
        if (string.IsNullOrWhiteSpace(Description))
            Log.LogDebug($"Invalid resource key '{resourceKey}' for option '{name}'");
#endif
    }

    private static string GetResourceText(string resourceKey)
    {
        var field = typeof(Resources).GetField(resourceKey, BindingFlags.Public | BindingFlags.Static);
        if (field is not null)
            return field.GetValue(null)?.ToString() ?? resourceKey;

        var prop = typeof(Resources).GetProperty(resourceKey, BindingFlags.Public | BindingFlags.Static);
        return prop?.GetValue(null)?.ToString() ?? resourceKey;
    }
}


