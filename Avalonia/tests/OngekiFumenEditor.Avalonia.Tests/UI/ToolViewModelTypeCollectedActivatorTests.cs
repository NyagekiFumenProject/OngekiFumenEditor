using System.Collections;
using System.Reflection;
using OngekiFumenEditor.Avalonia;
using OngekiFumenEditor.Avalonia.Modules.AudioPlayerToolViewer.ViewModels;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.UI;

public sealed class ToolViewModelTypeCollectedActivatorTests
{
    [Fact]
    public void Default_InitializesFactoryTableWithPartialToolViewModel()
    {
        // AudioPlayerToolViewerViewModel 跨两个文件的 partial 声明曾使源生成器
        // 产出重复字典键，导致桌面/Browser 入口在类型初始化时抛出
        // TypeInitializationException（ArgumentException: 相同键）。访问 Default
        // 即触发该静态构造路径，测试宿主自身的 RegisterServices 不会经过这里。
        var activator = ToolViewModelTypeCollectedActivator.Default;

        Assert.NotNull(activator);

        var field = typeof(ToolViewModelTypeCollectedActivator).GetField("_typeFactories", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(field);

        var factories = Assert.IsAssignableFrom<IDictionary>(field.GetValue(null));
        Assert.Contains(typeof(AudioPlayerToolViewerViewModel).FullName, factories.Keys.Cast<string>());
    }
}
