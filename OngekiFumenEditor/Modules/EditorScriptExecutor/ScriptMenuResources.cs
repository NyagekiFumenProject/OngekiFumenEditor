using OngekiFumenEditor.Properties;

namespace OngekiFumenEditor.Modules.EditorScriptExecutor
{
    internal static class ScriptMenuResources
    {
        public static string Scripts => GetString(nameof(Scripts), "Scripts (_S)");
        public static string NewScript => GetString(nameof(NewScript), "New Script");
        public static string NewScriptToolTip => GetString(nameof(NewScriptToolTip), "Create a new editor script.");
        public static string RecommendedScripts => GetString(nameof(RecommendedScripts), "Recommended Scripts");
        public static string RecommendedScriptsToolTip => GetString(nameof(RecommendedScriptsToolTip), "Open a built-in recommended script for review and editing.");
        public static string RecentScripts => GetString(nameof(RecentScripts), "Recent Scripts");
        public static string RecentScriptsToolTip => GetString(nameof(RecentScriptsToolTip), "Open a recently used script.");
        public static string NoScriptsFound => GetString(nameof(NoScriptsFound), "No scripts found");
        public static string EmbeddedRecommendedScriptDocumentTitleFormat => GetString(nameof(EmbeddedRecommendedScriptDocumentTitleFormat), "[Built-in/Read-only]{0}");
        public static string EmbeddedRecommendedScriptRecentDisplayNameFormat => GetString(nameof(EmbeddedRecommendedScriptRecentDisplayNameFormat), "[Built-in]{0}");
        public static string EmbeddedRecommendedScriptNotFound => GetString(nameof(EmbeddedRecommendedScriptNotFound), "Embedded recommended script not found.");
        public static string EmbeddedRecommendedScriptOpenFailed => GetString(nameof(EmbeddedRecommendedScriptOpenFailed), "Failed to open embedded recommended script.");

        private static string GetString(string name, string fallback)
        {
            return Resources.ResourceManager.GetString(name, Resources.Culture) ?? fallback;
        }
    }
}
