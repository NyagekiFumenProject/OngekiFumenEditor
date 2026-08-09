using Avalonia.Headless.XUnit;
using Gekimini.Avalonia.Framework;
using Gekimini.Avalonia.Modules.Shell;
using OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.UI;

public sealed class ToolLayoutRestorationTests
{
    [AvaloniaFact]
    public async Task LoadLayout_RestoresToolBoundToSameDiSingleton()
    {
        var shell = IoC.Get<IShell>();
        var browser = IoC.Get<IFumenObjectPropertyBrowser>();

        await shell.ResetLayout();
        shell.ShowTool((IToolViewModel)browser);
        Assert.Single(shell.Tools);

        await shell.SaveLayout();

        // 模拟重启：从持久化的布局 JSON 恢复。布局里只有具体类型名，
        // 恢复时必须复用 DI 单例，否则面板会绑定到一个无人更新的新实例。
        await shell.LoadLayout();

        var restored = Assert.Single(shell.Tools);
        Assert.Same(browser, restored);

        // 恢复后再次从菜单打开同一工具，不应出现重复面板。
        shell.ShowTool((IToolViewModel)browser);
        Assert.Single(shell.Tools);
    }
}
