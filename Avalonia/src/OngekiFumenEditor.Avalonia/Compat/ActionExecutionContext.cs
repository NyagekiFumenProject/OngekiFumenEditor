namespace OngekiFumenEditor.Avalonia.Compat
{
    /// <summary>
    /// 迁移兼容垫片：替代 Caliburn.Micro 的 ActionExecutionContext。
    ///
    /// 旧 WPF 项目通过 cal:Message.Attach="[Event Xxx] = [Action Method($executionContext)]"
    /// 把控件事件绑定到 ViewModel 方法，Caliburn 在触发时注入一个 ActionExecutionContext。
    /// Gekimini.Avalonia 框架未提供等价类型，这里补齐 ViewModel 实际用到的三个成员，
    /// 使这些方法能够通过编译。真正的事件接线由 Avalonia.Xaml.Interactivity(Behaviors)
    /// 在各视图 AXAML 中重建后填充这些字段。
    /// </summary>
    public class ActionExecutionContext
    {
        /// <summary>触发事件的控件（对应 Caliburn 的 Source）。</summary>
        public object Source { get; set; }

        /// <summary>原始事件参数（Avalonia 的 RoutedEventArgs / PointerEventArgs 等）。</summary>
        public object EventArgs { get; set; }

        /// <summary>关联的视图对象。</summary>
        public object View { get; set; }
    }
}
