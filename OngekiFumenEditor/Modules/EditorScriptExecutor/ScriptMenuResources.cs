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
        public static string OpenScript => GetString(nameof(OpenScript), "Open Script");
        public static string OpenScriptToolTip => GetString(nameof(OpenScriptToolTip), "Open this script in the script editor.");
        public static string RunTo => GetString(nameof(RunTo), "Run To...");
        public static string RunToToolTip => GetString(nameof(RunToToolTip), "Compile and run this script for the selected target.");
        public static string CurrentActivingEditor => GetString(nameof(CurrentActivingEditor), "Current Activing Editor");
        public static string NoEditor => GetString(nameof(NoEditor), "No Editor");
        public static string NoOpenedEditors => GetString(nameof(NoOpenedEditors), "No opened editors");
        public static string UnnamedEditor => GetString(nameof(UnnamedEditor), "Unnamed Editor");
        public static string ConfirmRunScriptFormat => GetString(nameof(ConfirmRunScriptFormat), "Run script '{0}' to '{1}'?");
        public static string ScriptExecutionResultFormat => GetString(nameof(ScriptExecutionResultFormat), "Script: {0}{1}Target: {2}{1}Result: {3}");
        public static string ScriptCompileFailedFormat => GetString(nameof(ScriptCompileFailedFormat), "Script compile failed: {0}{1}{2}");
        public static string ScriptExecutionTargetClosed => GetString(nameof(ScriptExecutionTargetClosed), "The selected target editor is no longer open.");
        public static string ScriptExecutionSourceReadFailed => GetString(nameof(ScriptExecutionSourceReadFailed), "Failed to read script source.");
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
