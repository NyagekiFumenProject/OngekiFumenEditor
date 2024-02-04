using Svg;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Utils
{
    public static class SvgExtensionMethod
    {
        public static void AddCustomClass(this SvgElement element, string className)
        {
            var list = (element.CustomAttributes.FirstOrDefault(x => x.Key == "class").Value ?? string.Empty).Split(" ").Append("ofe_svg_" + className);
            element.CustomAttributes["class"] = string.Join(" ", list).Trim();
        }
    }
}
