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

    [CommandDefinition]
    public sealed class OpenScriptMenuActionCommandDefinition : CommandDefinition
    {
        public override string Name => "Scripts.OpenScriptMenuAction";
        public override string Text => ScriptMenuResources.OpenScript;
        public override string ToolTip => ScriptMenuResources.OpenScriptToolTip;
    }

    [CommandDefinition]
    public sealed class RunScriptToCommandDefinition : CommandDefinition
    {
        public override string Name => "Scripts.RunScriptTo";
        public override string Text => ScriptMenuResources.RunTo;
        public override string ToolTip => ScriptMenuResources.RunToToolTip;
    }

    [CommandDefinition]
    public sealed class RunScriptToTargetCommandListDefinition : CommandListDefinition
    {
        public override string Name => "Scripts.RunScriptToTargetList";
    }
}
