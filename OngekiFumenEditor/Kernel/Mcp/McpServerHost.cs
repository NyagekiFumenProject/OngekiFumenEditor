using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Mcp
{
    [Export(typeof(IMcpServerHost))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal sealed class McpServerHost : IMcpServerHost
    {
        private const string DefaultPath = "/mcp";

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

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await lifecycleLock.WaitAsync(cancellationToken);
            try
            {
                if (IsRunning)
                    return;

                var port = NormalizePort(ProgramSetting.Default.McpServerListenPort);
                var url = $"http://127.0.0.1:{port}";

                var builder = WebApplication.CreateBuilder(new WebApplicationOptions
                {
                    Args = [],
                    ApplicationName = typeof(McpServerHost).Assembly.FullName,
                });

                builder.WebHost.UseUrls(url);

                builder.Services
                    .AddMcpServer()
                    .WithHttpTransport()
                    .WithTools<EditorTools>(editorTools)
                    .WithTools<ScriptTools>(scriptTools);

                var app = builder.Build();
                app.MapMcp(DefaultPath);

                try
                {
                    await app.StartAsync(cancellationToken);
                }
                catch
                {
                    await app.DisposeAsync();
                    throw;
                }

                webApplication = app;
                runningEndpoint = $"{url}{DefaultPath}";
                IsRunning = true;
                Log.LogInfo($"McpServerHost started at {runningEndpoint}");
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
    }
}
