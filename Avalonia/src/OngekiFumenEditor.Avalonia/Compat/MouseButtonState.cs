namespace OngekiFumenEditor.Avalonia.Compat
{
    /// <summary>
    /// 迁移兼容垫片：替代 WPF 的 System.Windows.Input.MouseButtonState。
    ///
    /// Avalonia 没有等价枚举，鼠标按钮状态改由 PointerPointProperties 的
    /// IsLeftButtonPressed / IsRightButtonPressed 等 bool 属性表达。这里补齐
    /// 与 WPF 语义一致的枚举，使沿用旧输入模型的代码能够通过编译；相关方法体
    /// 真正接入 Avalonia 输入事件时应改用 PointerPointProperties。
    /// </summary>
    public enum MouseButtonState
    {
        /// <summary>按钮已释放。</summary>
        Released = 0,

        /// <summary>按钮已按下。</summary>
        Pressed = 1,
    }
}
