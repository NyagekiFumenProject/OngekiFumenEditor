using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Mcp
{
    [Export(typeof(IMcpServerHost))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal sealed class McpServerHost : IMcpServerHost
    {
        private const string DefaultPath = "/mcp";
        private const int MinPort = 1;
        private const int MaxPort = 65535;

        private readonly EditorTools editorTools;
        private readonly ScriptTools scriptTools;
        private readonly SemaphoreSlim lifecycleLock = new SemaphoreSlim(1, 1);
        private WebApplication webApplication;
        private string runningEndpoint;

        [ImportingConstructor]
        public McpServerHost(EditorTools editorTools, ScriptTools scriptTools)
        {
            this.editorTools = editorTools;
            this.scriptTools = scriptTools;
        }

        public bool IsRunning { get; private set; }
        public string ServerUrl => IsRunning && !string.IsNullOrWhiteSpace(runningEndpoint) ? runningEndpoint : BuildServerUrl(NormalizePort(ProgramSetting.Default.McpServerListenPort));

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await lifecycleLock.WaitAsync(cancellationToken);
            try
            {
                if (IsRunning)
                    return;

                var requestedPort = NormalizePort(ProgramSetting.Default.McpServerListenPort);
                var port = requestedPort;
                Exception lastAddressInUseException = null;

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var app = BuildWebApplication(port);
                    try
                    {
                        await app.StartAsync(cancellationToken);

                        webApplication = app;
                        runningEndpoint = BuildServerUrl(port);
                        IsRunning = true;

                        if (port == requestedPort)
                            Log.LogInfo($"McpServerHost started at {runningEndpoint}");
                        else
                            Log.LogWarn($"Requested MCP port {requestedPort} was unavailable. McpServerHost started at {runningEndpoint} instead.");

                        return;
                    }
                    catch (Exception ex) when (IsAddressInUseException(ex))
                    {
                        lastAddressInUseException = ex;
                        await app.DisposeAsync();

                        var nextPort = GetNextPort(port);
                        if (nextPort == requestedPort)
                            throw new InvalidOperationException($"Unable to start MCP server. No available TCP port was found after probing from {requestedPort}.", lastAddressInUseException);

                        Log.LogWarn($"MCP port {port} is already in use. Retrying with port {nextPort}.");
                        port = nextPort;
                    }
                    catch
                    {
                        await app.DisposeAsync();
                        throw;
                    }
                }
            }
            finally
            {
                lifecycleLock.Release();
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await lifecycleLock.WaitAsync(cancellationToken);
            try
            {
                if (!IsRunning && webApplication is null)
                    return;

                var app = webApplication;
                var endpoint = runningEndpoint;
                webApplication = null;
                runningEndpoint = null;
                IsRunning = false;

                if (app is not null)
                {
                    await app.StopAsync(cancellationToken);
                    await app.DisposeAsync();
                }

                Log.LogInfo(string.IsNullOrWhiteSpace(endpoint) ? "McpServerHost stopped." : $"McpServerHost stopped: {endpoint}");
            }
            finally
            {
                lifecycleLock.Release();
            }
        }

        private static int NormalizePort(int port)
        {
            return port is > 0 and <= 65535 ? port : 39281;
        }

        private WebApplication BuildWebApplication(int port)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = [],
                ApplicationName = typeof(McpServerHost).Assembly.FullName,
            });

            builder.WebHost.UseUrls($"http://127.0.0.1:{port}");

            builder.Services
                .AddMcpServer()
                .WithHttpTransport()
                .WithTools<EditorTools>(editorTools)
                .WithTools<ScriptTools>(scriptTools);

            var app = builder.Build();
            app.MapMcp(DefaultPath);
            return app;
        }

        private static int GetNextPort(int port)
        {
            return port >= MaxPort ? MinPort : port + 1;
        }

        private static bool IsAddressInUseException(Exception exception)
        {
            for (var current = exception; current is not null; current = current.InnerException)
            {
                if (current is SocketException socketException && socketException.SocketErrorCode == SocketError.AddressAlreadyInUse)
                    return true;

                if (current is IOException ioException &&
                    ioException.Message?.IndexOf("address already in use", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static string BuildServerUrl(int port)
        {
            return $"http://127.0.0.1:{port}{DefaultPath}";
        }
    }
}
