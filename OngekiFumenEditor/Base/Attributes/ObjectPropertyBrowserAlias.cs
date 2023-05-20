using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
}
