using Avalonia;
using Gekimini.Avalonia.Views;

namespace OngekiFumenEditor.Avalonia.Modules.SplashScreen.Views;

public partial class SplashScreenCommonView : ViewBase
{
    /// <summary>
    ///     平台专属的启动操作卡片区域；Desktop 在此处放 FastOpen 等平台功能，
    ///     Core 基类与公共视图不感知任何平台业务。
    /// </summary>
    public static readonly StyledProperty<object?> AdditionalActionsProperty =
        AvaloniaProperty.Register<SplashScreenCommonView, object>(nameof(AdditionalActions));

    public object AdditionalActions
    {
        get => GetValue(AdditionalActionsProperty);
        set => SetValue(AdditionalActionsProperty, value);
    }

    public SplashScreenCommonView()
    {
        InitializeComponent();
    }
}