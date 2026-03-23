using System;

namespace OngekiFumenEditor.Core.Base.Attributes
{
    [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public class ObjectPropertyBrowserTipText : Attribute
    {
        public ObjectPropertyBrowserTipText(string tipTextResourceKey = default)
        {
            TipTextResourceKey = tipTextResourceKey ?? string.Empty;
        }

        public string TipTextResourceKey { get; }
    }
}
