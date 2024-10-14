using System;
using OngekiFumenEditor.Properties;

namespace OngekiFumenEditor.Base.Attributes
{
    [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public class ObjectPropertyBrowserAlias : Attribute
    {
        public ObjectPropertyBrowserAlias(string alias = default)
        {
            Alias = alias ?? string.Empty;
        }

        public string Alias { get; set; }
    }

    [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public class LocalizableObjectPropertyBrowserAlias : ObjectPropertyBrowserAlias
    {
        public LocalizableObjectPropertyBrowserAlias(string key)
        {
#if DEBUG
            if (asResourceKey) {
                if (key == default) {
                    throw new ArgumentException("cannot use empty string as resource key");
                }
                if (Resources.ResourceManager.GetString(key) is null) {
                    throw new ArgumentException($"invalid resource key \"{key}\"");
                }
            }
#endif
            
            Alias = Resources.ResourceManager.GetString(key!) ?? string.Empty;
        }
    }
}