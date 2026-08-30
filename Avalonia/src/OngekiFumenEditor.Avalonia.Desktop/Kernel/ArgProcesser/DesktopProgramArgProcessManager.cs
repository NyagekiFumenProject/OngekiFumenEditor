using Avalonia.Threading;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Desktop.Modules.FumenVisualEditor.FastOpen;
using OngekiFumenEditor.Avalonia.Desktop.Utils;
using OngekiFumenEditor.Avalonia.Kernel.ArgProcesser;
using OngekiFumenEditor.Avalonia.Parser;
using OngekiFumenEditor.Avalonia.Utils;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem.Impl.LocalFileSystem;
using System.Reflection;

namespace OngekiFumenEditor.Avalonia.Desktop.Kernel.ArgProcesser;

/// <summary>
///     启动参数只在 Desktop 生效：本地路径文档打开属于平台能力，
///     Browser 不解析也不注册该处理器。
/// </summary>
[RegisterSingleton<IProgramArgProcessManager>]
internal class DesktopProgramArgProcessManager : IProgramArgProcessManager
{
    private readonly DesktopFastOpenService fastOpenService;
    private readonly IFumenParserManager parserManager;

    public DesktopProgramArgProcessManager(
        DesktopFastOpenService fastOpenService,
        IFumenParserManager parserManager)
    {
        this.fastOpenService = fastOpenService;
        this.parserManager = parserManager;
    }

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
                // 所有已注册的谱面解析器都走同一套 Desktop FastOpen 逻辑。
                var isFumenFile = parserManager.GetDeserializer(filePath) is not null;
                _ = isFumenFile
                    ? await fastOpenService.TryOpenAsync(new LocalSimpleFile(filePath))
                    : await DesktopDocumentOpenService.TryOpenAsync(filePath);
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

            var currentVersion = (Assembly.GetEntryAssembly() ?? typeof(DesktopProgramArgProcessManager).Assembly).GetName().Version;
            Log.LogInfo($"Update finished. sourceVersion={sourceVersion}, currentVersion={currentVersion}");
        }
    }
}
