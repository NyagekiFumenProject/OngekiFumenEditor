namespace OngekiFumenEditor.Kernel.RuntimeAutomation
{
    /// <summary>
    /// 跟踪 <c>editor.begin_action</c> / <c>editor.end_action</c> 打开的撤销合并作用域。
    /// 每个编辑器同时只允许一个作用域，归属身份由 <c>clientId</c> / <c>requestedBy</c> / anonymous 决定。
    /// </summary>
    public interface IEditorActionScopeManager
    {
        bool TryBegin(string editorId, string identityKey, out string errorCode, out string errorMessage);

        bool IsOpenFor(string editorId);

        /// <summary>作用域打开时登记一条待执行结果；返回 false 表示当前没有可用的作用域。</summary>
        bool TryTrack(string editorId, string identityKey, EditorActionOutcome outcome);

        /// <summary>关闭并取走作用域状态（不触碰撤销管理器，由调用方在 UI 线程执行 EndCombineAction）。</summary>
        bool TryTake(string editorId, string identityKey, out EditorActionScopeState state, out string errorCode, out string errorMessage);
    }
}
