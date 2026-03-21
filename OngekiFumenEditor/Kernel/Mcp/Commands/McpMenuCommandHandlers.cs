using Gemini.Framework.Commands;
using Gemini.Framework.Threading;
using OngekiFumenEditor.Kernel.RuntimeAutomation;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.Kernel.Mcp.Commands
{
    internal static class McpMenuCommandHandlerResources
    {
        public static string GetString(string key, string fallback)
        {
            return Resources.ResourceManager.GetString(key) ?? fallback;
        }
    }

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
            command.Text = string.Format(
                McpMenuCommandHandlerResources.GetString("McpServerUrlStatusFormat", "Server URL: {0}{1}"),
                mcpServerHost.ServerUrl,
                mcpServerHost.IsRunning ? string.Empty : McpMenuCommandHandlerResources.GetString("McpServerStoppedSuffix", " (Stopped)"));
            command.ToolTip = McpMenuCommandHandlerResources.GetString("McpCopyServerUrlToolTip", "Click to copy the current MCP server URL.");
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
                MessageBox.Show(string.Format(McpMenuCommandHandlerResources.GetString("McpCopyServerUrlFailed", "Failed to copy MCP server URL.{0}{0}{1}"), Environment.NewLine, ex.Message), McpMenuCommandHandlerResources.GetString("McpMenuTitle", "MCP"), MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show(string.Format(McpMenuCommandHandlerResources.GetString("McpStartServerFailed", "Failed to start MCP server.{0}{0}{1}"), Environment.NewLine, ex.Message), McpMenuCommandHandlerResources.GetString("McpMenuTitle", "MCP"), MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show(string.Format(McpMenuCommandHandlerResources.GetString("McpStopServerFailed", "Failed to stop MCP server.{0}{0}{1}"), Environment.NewLine, ex.Message), McpMenuCommandHandlerResources.GetString("McpMenuTitle", "MCP"), MessageBoxButton.OK, MessageBoxImage.Error);
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
            command.Text = string.Format(McpMenuCommandHandlerResources.GetString("McpConnectedClientsMenuTextFormat", "Connected Clients ({0})"), clients.Count);
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
                    Text = McpMenuCommandHandlerResources.GetString("McpNone", "(none)"),
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

            McpOperationLogHelper.LogRequest("authorization.revoke", new
            {
                identityKey = client.IdentityKey,
                requestedBy = client.RequestedBy,
                clientId = client.ClientId,
                isExecutionApproved = client.IsExecutionApproved,
            });

            if (!client.IsExecutionApproved)
            {
                McpOperationLogHelper.LogResult("authorization.revoke", new
                {
                    identityKey = client.IdentityKey,
                    success = false,
                    reason = "CLIENT_NOT_AUTHORIZED",
                });
                return TaskUtility.Completed;
            }

            if (!ConfirmRevokeAuthorization(client))
            {
                McpOperationLogHelper.LogResult("authorization.revoke", new
                {
                    identityKey = client.IdentityKey,
                    success = false,
                    cancelled = true,
                });
                return TaskUtility.Completed;
            }

            var revoked = mcpClientAuthorizationManager.RevokeExecutionApproval(client.IdentityKey);
            if (revoked)
                Log.LogInfo($"Revoked MCP tool authorization for client '{BuildClientDisplayName(client)}' ({client.IdentityKey}).");
            else
                Log.LogInfo($"MCP client '{BuildClientDisplayName(client)}' ({client.IdentityKey}) was already not authorized.");

            McpOperationLogHelper.LogResult("authorization.revoke", new
            {
                identityKey = client.IdentityKey,
                requestedBy = client.RequestedBy,
                clientId = client.ClientId,
                success = revoked,
            });

            return TaskUtility.Completed;
        }

        private static string BuildClientMenuText(McpClientRegistrationInfo client)
        {
            var status = client.IsExecutionApproved ? McpMenuCommandHandlerResources.GetString("McpClientAuthorizedStatus", "[Authorized]") : McpMenuCommandHandlerResources.GetString("McpClientNotAuthorizedStatus", "[Not Authorized]");
            var displayName = BuildClientDisplayName(client);
            return $"{status} {displayName}";
        }

        private static string BuildClientToolTip(McpClientRegistrationInfo client)
        {
            if (!client.IsExecutionApproved)
                return McpMenuCommandHandlerResources.GetString("McpClientNotAuthorizedToolTip", "This client is currently not authorized to call MCP tools.");

            return string.Format(McpMenuCommandHandlerResources.GetString("McpClientRevokeAuthorizationToolTipFormat", "Click to revoke MCP tool authorization for {0}."), BuildClientDisplayName(client));
        }

        private static string BuildClientDisplayName(McpClientRegistrationInfo client)
        {
            var requestedBy = string.IsNullOrWhiteSpace(client?.RequestedBy) ? McpMenuCommandHandlerResources.GetString("McpAnonymousClient", "Anonymous Client") : client.RequestedBy;
            if (string.IsNullOrWhiteSpace(client?.ClientId))
                return requestedBy;

            return $"{requestedBy} ({client.ClientId})";
        }

        private static bool ConfirmRevokeAuthorization(McpClientRegistrationInfo client)
        {
            var displayName = BuildClientDisplayName(client);
            var identityKey = string.IsNullOrWhiteSpace(client?.IdentityKey) ? McpMenuCommandHandlerResources.GetString("McpUnknownIdentityKey", "Unknown") : client.IdentityKey;
            var message = string.Format(McpMenuCommandHandlerResources.GetString("McpRevokeAuthorizationConfirmFormat", "Revoke MCP tool authorization for this client?{0}{0}Client: {1}{0}Identity Key: {2}"), Environment.NewLine, displayName, identityKey);
            var result = MessageBox.Show(message, McpMenuCommandHandlerResources.GetString("McpMenuTitle", "MCP"), MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            return result == MessageBoxResult.Yes;
        }
    }
}
