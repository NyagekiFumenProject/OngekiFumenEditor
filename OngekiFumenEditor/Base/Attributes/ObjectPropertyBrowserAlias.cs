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
        public LocalizableObjectPropertyBrowserAlias(string resourceKey)
        {
#if DEBUG
            if (resourceKey == default)
            {
                throw new ArgumentException("cannot use empty string as resource key");
            }
            if (Resources.ResourceManager.GetString(resourceKey) is null)
            {
                throw new ArgumentException($"invalid resource key \"{resourceKey}\"");
            }
#endif

            Alias = Resources.ResourceManager.GetString(resourceKey!) ?? string.Empty;
        }
    }
}