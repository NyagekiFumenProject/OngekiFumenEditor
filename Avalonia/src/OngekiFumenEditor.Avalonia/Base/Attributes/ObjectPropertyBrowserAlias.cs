using SimpleTypedLocalizer;

namespace OngekiFumenEditor.Avalonia.Base.Attributes
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
            if (LocalizerManager.GetLocalizedStringGlobally(resourceKey) is null)
                throw new ArgumentException($"invalid resource key \"{resourceKey}\"");
#endif

            Alias = LocalizerManager.GetLocalizedStringGlobally(resourceKey) ?? string.Empty;
        }
    }
}