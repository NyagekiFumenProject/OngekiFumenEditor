using System;

namespace OngekiFumenEditor.Core.Base.Attributes
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
            if (string.IsNullOrWhiteSpace(resourceKey))
                throw new ArgumentException("cannot use empty string as resource key");
#endif
            ResourceKey = resourceKey ?? string.Empty;
        }

        public string ResourceKey { get; }
    }
}
