using Gemini.Framework.Commands;
using Gemini.Framework.Threading;
using OngekiFumenEditor.Kernel.RuntimeAutomation;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.Kernel.Mcp.Commands
{
    [CommandHandler]
    public sealed class McpServerUrlCommandHandler : CommandHandlerBase<McpServerUrlCommandDefinition>
    {
        private readonly IMcpServerHost mcpServerHost;

        [ImportingConstructor]
        public McpServerUrlCommandHandler(IMcpServerHost mcpServerHost)
        {
            this.mcpServerHost = mcpServerHost;
        }

        public override void Update(Command command)
        {
            command.Enabled = true;
            command.Text = $"Server URL: {mcpServerHost.ServerUrl}{(mcpServerHost.IsRunning ? string.Empty : " (Stopped)")}";
            command.ToolTip = "Click to copy the current MCP server URL.";
        }

        public override Task Run(Command command)
        {
            try
            {
                Clipboard.SetText(mcpServerHost.ServerUrl ?? string.Empty);
                Log.LogInfo($"Copied MCP server URL: {mcpServerHost.ServerUrl}");
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to copy MCP server URL: {ex}");
                MessageBox.Show($"Failed to copy MCP server URL.{Environment.NewLine}{Environment.NewLine}{ex.Message}", "MCP", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return TaskUtility.Completed;
        }
    }

    [CommandHandler]
    public sealed class StartMcpServerCommandHandler : CommandHandlerBase<StartMcpServerCommandDefinition>
    {
        private readonly IMcpServerHost mcpServerHost;

        [ImportingConstructor]
        public StartMcpServerCommandHandler(IMcpServerHost mcpServerHost)
        {
            this.mcpServerHost = mcpServerHost;
        }

        public override void Update(Command command)
        {
            command.Enabled = !mcpServerHost.IsRunning;
        }

        public override async Task Run(Command command)
        {
            try
            {
                await mcpServerHost.StartAsync();
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to start MCP server from menu: {ex}");
                MessageBox.Show($"Failed to start MCP server.{Environment.NewLine}{Environment.NewLine}{ex.Message}", "MCP", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [CommandHandler]
    public sealed class StopMcpServerCommandHandler : CommandHandlerBase<StopMcpServerCommandDefinition>
    {
        private readonly IMcpServerHost mcpServerHost;

        [ImportingConstructor]
        public StopMcpServerCommandHandler(IMcpServerHost mcpServerHost)
        {
            this.mcpServerHost = mcpServerHost;
        }

        public override void Update(Command command)
        {
            command.Enabled = mcpServerHost.IsRunning;
        }

        public override async Task Run(Command command)
        {
            try
            {
                await mcpServerHost.StopAsync();
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to stop MCP server from menu: {ex}");
                MessageBox.Show($"Failed to stop MCP server.{Environment.NewLine}{Environment.NewLine}{ex.Message}", "MCP", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [CommandHandler]
    public sealed class ConnectedMcpClientsCommandHandler : CommandHandlerBase<ConnectedMcpClientsCommandDefinition>
    {
        private readonly IMcpClientAuthorizationManager mcpClientAuthorizationManager;

        [ImportingConstructor]
        public ConnectedMcpClientsCommandHandler(IMcpClientAuthorizationManager mcpClientAuthorizationManager)
        {
            this.mcpClientAuthorizationManager = mcpClientAuthorizationManager;
        }

        public override void Update(Command command)
        {
            var clients = mcpClientAuthorizationManager.GetKnownClients();
            command.Enabled = clients.Any();
            command.Text = $"\u5df2\u8fde\u63a5\u4f7f\u7528\u7684\u5ba2\u6237\u7aef ({clients.Count})";
        }

        public override Task Run(Command command)
        {
            return TaskUtility.Completed;
        }
    }

    [CommandHandler]
    public sealed class RevokeMcpClientAuthorizationCommandHandler : ICommandListHandler<RevokeMcpClientAuthorizationCommandListDefinition>
    {
        private readonly IMcpClientAuthorizationManager mcpClientAuthorizationManager;

        [ImportingConstructor]
        public RevokeMcpClientAuthorizationCommandHandler(IMcpClientAuthorizationManager mcpClientAuthorizationManager)
        {
            this.mcpClientAuthorizationManager = mcpClientAuthorizationManager;
        }

        public void Populate(Command command, List<Command> commands)
        {
            var clients = mcpClientAuthorizationManager.GetKnownClients();
            if (!clients.Any())
            {
                commands.Add(new Command(command.CommandDefinition)
                {
                    Text = "(none)",
                    Enabled = false,
                });
                return;
            }

            foreach (var client in clients)
            {
                commands.Add(new Command(command.CommandDefinition)
                {
                    Text = BuildClientMenuText(client),
                    ToolTip = BuildClientToolTip(client),
                    Tag = client,
                    Enabled = client.IsExecutionApproved,
                });
            }
        }

        public Task Run(Command command)
        {
            if (command?.Tag is not McpClientRegistrationInfo client)
                return TaskUtility.Completed;

            var revoked = mcpClientAuthorizationManager.RevokeExecutionApproval(client.IdentityKey);
            if (revoked)
                Log.LogInfo($"Revoked MCP tool authorization for client '{BuildClientDisplayName(client)}' ({client.IdentityKey}).");
            else
                Log.LogInfo($"MCP client '{BuildClientDisplayName(client)}' ({client.IdentityKey}) was already not authorized.");

            return TaskUtility.Completed;
        }

        private static string BuildClientMenuText(McpClientRegistrationInfo client)
        {
            var status = client.IsExecutionApproved ? "[Authorized]" : "[Not Authorized]";
            var displayName = BuildClientDisplayName(client);
            return $"{status} {displayName}";
        }

        private static string BuildClientToolTip(McpClientRegistrationInfo client)
        {
            if (!client.IsExecutionApproved)
                return "This client is currently not authorized to call MCP tools.";

            return $"Click to revoke MCP tool authorization for {BuildClientDisplayName(client)}.";
        }

        private static string BuildClientDisplayName(McpClientRegistrationInfo client)
        {
            var requestedBy = string.IsNullOrWhiteSpace(client?.RequestedBy) ? "Unknown Client" : client.RequestedBy;
            if (string.IsNullOrWhiteSpace(client?.ClientId))
                return requestedBy;

            return $"{requestedBy} ({client.ClientId})";
        }
    }
}
