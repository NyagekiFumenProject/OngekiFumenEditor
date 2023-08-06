using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.Attributes
{
    /// <summary>
    /// 如果此属性只读，可以钦定此特性强制显示
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public class ObjectPropertyBrowserShow : Attribute
    {
    }
}
