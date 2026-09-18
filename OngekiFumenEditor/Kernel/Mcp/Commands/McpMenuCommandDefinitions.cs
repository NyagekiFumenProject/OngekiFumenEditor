using Gemini.Framework.Commands;
using OngekiFumenEditor.Properties;

namespace OngekiFumenEditor.Kernel.Mcp.Commands
{
    [CommandDefinition]
    public sealed class McpServerUrlCommandDefinition : CommandDefinition
    {
        public override string Name => "Mcp.ServerUrl";
        public override string Text => Resources.McpServerUrlMenuText;
        public override string ToolTip => Resources.McpServerUrlMenuToolTip;
    }

    [CommandDefinition]
    public sealed class StartMcpServerCommandDefinition : CommandDefinition
    {
        public override string Name => "Mcp.StartServer";
        public override string Text => Resources.McpStartServerMenuText;
        public override string ToolTip => Resources.McpStartServerMenuToolTip;
    }

    [CommandDefinition]
    public sealed class StopMcpServerCommandDefinition : CommandDefinition
    {
        public override string Name => "Mcp.StopServer";
        public override string Text => Resources.McpStopServerMenuText;
        public override string ToolTip => Resources.McpStopServerMenuToolTip;
    }

    [CommandDefinition]
    public sealed class ConnectedMcpClientsCommandDefinition : CommandDefinition
    {
        public override string Name => "Mcp.ConnectedClients";
        public override string Text => Resources.McpConnectedClientsMenuText;
        public override string ToolTip => Resources.McpConnectedClientsMenuToolTip;
    }

    [CommandDefinition]
    public sealed class RevokeMcpClientAuthorizationCommandListDefinition : CommandListDefinition
    {
        public override string Name => "Mcp.RevokeClientAuthorizationList";
    }
}
