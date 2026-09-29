using System;
using System.Collections.Generic;

namespace OngekiFumenEditor.Kernel.RuntimeAutomation
{
    /// <summary>
    /// 单个编辑操作的结果。作用域打开时由 redo lambda 在稍后回填，作用域关闭时同步回填。
    /// </summary>
    public sealed class EditorActionOutcome
    {
        public string Operation { get; set; } = string.Empty;
        public string ObjectType { get; set; }
        public int ObjectId { get; set; }
        public bool Executed { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// <c>editor.end_action</c> 的汇总结果。
    /// </summary>
    public sealed class EditorActionScopeSummary
    {
        public string EditorId { get; set; } = string.Empty;
        public string TransactionName { get; set; } = string.Empty;
        public bool Applied { get; set; }
        public bool RolledBack { get; set; }
        public int OutcomeCount { get; set; }
        public int FailedCount { get; set; }
        public string ErrorCode { get; set; }
        public string ErrorMessage { get; set; }
        public IReadOnlyList<EditorActionOutcome> Outcomes { get; set; } = Array.Empty<EditorActionOutcome>();
    }

    public sealed class EditorActionScopeState
    {
        public string EditorId { get; set; } = string.Empty;
        public string IdentityKey { get; set; } = string.Empty;
        public IReadOnlyList<EditorActionOutcome> Outcomes { get; set; } = Array.Empty<EditorActionOutcome>();
    }
}
