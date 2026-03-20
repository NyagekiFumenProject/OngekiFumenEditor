using Gemini.Framework.Commands;

namespace OngekiFumenEditor.Kernel.Mcp.Commands
{
    [CommandDefinition]
    public sealed class McpServerUrlCommandDefinition : CommandDefinition
    {
        public override string Name => "Mcp.ServerUrl";
        public override string Text => "Server URL";
        public override string ToolTip => "Show the current MCP server URL.";
    }

    [CommandDefinition]
    public sealed class StartMcpServerCommandDefinition : CommandDefinition
    {
        public override string Name => "Mcp.StartServer";
        public override string Text => "Start MCP Server";
        public override string ToolTip => "Start the MCP server.";
    }

    [CommandDefinition]
    public sealed class StopMcpServerCommandDefinition : CommandDefinition
    {
        public override string Name => "Mcp.StopServer";
        public override string Text => "Stop MCP Server";
        public override string ToolTip => "Stop the MCP server.";
    }

    [CommandDefinition]
    public sealed class ConnectedMcpClientsCommandDefinition : CommandDefinition
    {
        public override string Name => "Mcp.ConnectedClients";
        public override string Text => "\u5df2\u8fde\u63a5\u4f7f\u7528\u7684\u5ba2\u6237\u7aef";
        public override string ToolTip => "List MCP clients that have used MCP tools and revoke their authorization.";
    }

    [CommandDefinition]
    public sealed class RevokeMcpClientAuthorizationCommandListDefinition : CommandListDefinition
    {
        public override string Name => "Mcp.RevokeClientAuthorizationList";
    }
}
