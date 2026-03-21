using Gemini.Framework.Commands;
using OngekiFumenEditor.Properties;

namespace OngekiFumenEditor.Kernel.Mcp.Commands
{
    internal static class McpMenuCommandResources
    {
        public static string GetString(string key, string fallback)
        {
            return Resources.ResourceManager.GetString(key) ?? fallback;
        }
    }

    [CommandDefinition]
    public sealed class McpServerUrlCommandDefinition : CommandDefinition
    {
        public override string Name => "Mcp.ServerUrl";
        public override string Text => McpMenuCommandResources.GetString("McpServerUrlMenuText", "Server URL");
        public override string ToolTip => McpMenuCommandResources.GetString("McpServerUrlMenuToolTip", "Show the current MCP server URL.");
    }

    [CommandDefinition]
    public sealed class StartMcpServerCommandDefinition : CommandDefinition
    {
        public override string Name => "Mcp.StartServer";
        public override string Text => McpMenuCommandResources.GetString("McpStartServerMenuText", "Start MCP Server");
        public override string ToolTip => McpMenuCommandResources.GetString("McpStartServerMenuToolTip", "Start the MCP server.");
    }

    [CommandDefinition]
    public sealed class StopMcpServerCommandDefinition : CommandDefinition
    {
        public override string Name => "Mcp.StopServer";
        public override string Text => McpMenuCommandResources.GetString("McpStopServerMenuText", "Stop MCP Server");
        public override string ToolTip => McpMenuCommandResources.GetString("McpStopServerMenuToolTip", "Stop the MCP server.");
    }

    [CommandDefinition]
    public sealed class ConnectedMcpClientsCommandDefinition : CommandDefinition
    {
        public override string Name => "Mcp.ConnectedClients";
        public override string Text => McpMenuCommandResources.GetString("McpConnectedClientsMenuText", "Connected Clients");
        public override string ToolTip => McpMenuCommandResources.GetString("McpConnectedClientsMenuToolTip", "List MCP clients that have used MCP tools and revoke their authorization.");
    }

    [CommandDefinition]
    public sealed class RevokeMcpClientAuthorizationCommandListDefinition : CommandListDefinition
    {
        public override string Name => "Mcp.RevokeClientAuthorizationList";
    }
}
