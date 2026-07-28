using Avalonia.Threading;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Utils;
using System.Reflection;

namespace OngekiFumenEditor.Avalonia.Kernel.ArgProcesser;

[RegisterSingleton<IProgramArgProcessManager>]
internal class DefaultArgProcessManager : IProgramArgProcessManager
{
    public async Task ProcessArgs(string[] args)
    {
        if (args is null || args.Length == 0)
            return;

        if (args.Length == 1 && File.Exists(args[0]))
        {
            var filePath = args[0];
            Log.LogInfo($"arg.filePath: {filePath}");
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                _ = await DocumentOpenHelper.TryOpenAsDocument(filePath);
            });
        }

        if (args.Contains("--notifySucess", StringComparer.InvariantCultureIgnoreCase))
        {
            Version sourceVersion = default;
            for (int i = 0; i < args.Length; i++)
            {
                if ("--sourceVersion".Equals(args[i], StringComparison.InvariantCultureIgnoreCase) &&
                    Version.TryParse(args.ElementAtOrDefault(i + 1), out var sv))
                {
                    sourceVersion = sv;
                }
            }

            var currentVersion = (Assembly.GetEntryAssembly() ?? typeof(DefaultArgProcessManager).Assembly).GetName().Version;
            Log.LogInfo($"Update finished. sourceVersion={sourceVersion}, currentVersion={currentVersion}");
        }
    }
}
