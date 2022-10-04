using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.Attributes
{
    [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public class ObjectPropertyBrowserTipText : Attribute
    {
        public ObjectPropertyBrowserTipText(string tipText = default)
        {
            TipText = tipText ?? string.Empty;
        }

        public string TipText { get; set; }
    }
}
