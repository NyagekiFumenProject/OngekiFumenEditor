using System.Globalization;
using System.Reflection;
using System.Resources;

namespace OngekiFumenEditor.Core.Properties
{
    internal static class Resources
    {
        private static readonly ResourceManager resourceManager = new("OngekiFumenEditor.Core.Properties.Resources", typeof(Resources).GetTypeInfo().Assembly);

        public static string GetString(string name)
        {
            return resourceManager.GetString(name, CultureInfo.CurrentUICulture) ?? name;
        }
    }
}
