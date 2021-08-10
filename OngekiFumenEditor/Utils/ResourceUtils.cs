using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Utils
{
    public static class ResourceUtils
    {
        public static Stream OpenReadFromLocalAssemblyResource(string resourceName) => typeof(ResourceUtils).Assembly.GetManifestResourceStream("OngekiFumenEditor.Resources." + resourceName);
    }
}
