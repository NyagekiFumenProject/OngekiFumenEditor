using Gemini.Framework.Commands;

namespace OngekiFumenEditor.Modules.EditorScriptExecutor.Commands
{
    [CommandDefinition]
    public sealed class NewScriptCommandDefinition : CommandDefinition
    {
        public override string Name => "Scripts.NewScript";
        public override string Text => ScriptMenuResources.NewScript;
        public override string ToolTip => ScriptMenuResources.NewScriptToolTip;
    }

    [CommandDefinition]
    public sealed class RecommendedScriptsCommandDefinition : CommandDefinition
    {
        public override string Name => "Scripts.RecommendedScripts";
        public override string Text => ScriptMenuResources.RecommendedScripts;
        public override string ToolTip => ScriptMenuResources.RecommendedScriptsToolTip;
    }

    [CommandDefinition]
    public sealed class RecentScriptsCommandDefinition : CommandDefinition
    {
        public override string Name => "Scripts.RecentScripts";
        public override string Text => ScriptMenuResources.RecentScripts;
        public override string ToolTip => ScriptMenuResources.RecentScriptsToolTip;
    }

    [CommandDefinition]
    public sealed class OpenRecommendedScriptCommandListDefinition : CommandListDefinition
    {
        public override string Name => "Scripts.OpenRecommendedScriptList";
    }

    [CommandDefinition]
    public sealed class OpenRecentScriptCommandListDefinition : CommandListDefinition
    {
        public override string Name => "Scripts.OpenRecentScriptList";
    }
}
