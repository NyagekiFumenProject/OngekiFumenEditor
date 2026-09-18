namespace OngekiFumenEditor.Kernel.RuntimeAutomation
{
    public interface IScriptSecurityPolicy
    {
        ScriptSecurityCheckResult Check(string scriptText);
    }
}
