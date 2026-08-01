using Avalonia;
using Avalonia.Markup.Xaml;

namespace OngekiFumenEditor.Avalonia.Avalonia;

public abstract class App : Gekimini.Avalonia.App
{
    public bool IsGUIMode { get; }

    protected App(bool isGUIMode = true)
    {
        IsGUIMode = isGUIMode;
    }

    public override void Initialize()
    {
        // 基类 Initialize() 内部同样是 AvaloniaXamlLoader.Load(this)，会沿类型链串联加载本类 App.axaml；
        // 但 XAML 编译器要求本类型（有自定义构造函数）内显式出现 Load 调用，否则报 AVLN3000。
        // 因此这里重写并与基类保持一致，避免重复加载。
        AvaloniaXamlLoader.Load(this);

#if DEBUG
        if (IsGUIMode)
            this.AttachDeveloperTools();
#endif
    }
}
