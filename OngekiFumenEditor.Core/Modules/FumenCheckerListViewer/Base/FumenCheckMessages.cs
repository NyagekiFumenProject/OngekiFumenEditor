using OngekiFumenEditor.Core.Properties;
using OngekiFumenEditor.Utils;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer.Base
{
    public static class FumenCheckMessages
    {
        public static string Get(FumenCheckMessageKey key, params object[] args)
        {
            var format = Resources.GetString(key.ToString());
            return args is { Length: > 0 } ? format.Format(args) : format;
        }
    }
}
