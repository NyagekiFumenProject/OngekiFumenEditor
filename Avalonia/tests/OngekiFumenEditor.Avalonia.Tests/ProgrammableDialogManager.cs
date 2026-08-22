using Gekimini.Avalonia.Framework.Dialogs;
using Gekimini.Avalonia.Modules.Dialogs.ViewModels;
using Gekimini.Avalonia.Modules.Documents.Models;
using Gekimini.Avalonia.Modules.Documents.ViewModels;

namespace OngekiFumenEditor.Avalonia.Tests;

/// <summary>
///     替身对话框管理器：真实 DefaultDialogManager 在 headless 环境会等待
///     永不出现的视图交互，这里按可编程应答直接完成对话流程。
/// </summary>
public sealed class ProgrammableDialogManager : IDialogManager
{
    private readonly Queue<DialogResult> scriptedDirtyDialogAnswers = new();

    public static ProgrammableDialogManager Instance { get; } = new();

    public DialogResult DirtyDialogAnswer { get; set; } = DialogResult.No;

    public int DirtyDialogCount { get; private set; }

    public int MessageDialogCount { get; private set; }

    /// <summary>按弹出顺序脚本化应答；队列为空时退回 DirtyDialogAnswer。</summary>
    public void ScriptDirtyDialogAnswers(params DialogResult[] answers)
    {
        foreach (var answer in answers)
            scriptedDirtyDialogAnswers.Enqueue(answer);
    }

    /// <summary>非空时，脏文档对话框会等待其完成再应答，用于把确认流程卡在打开状态。</summary>
    public TaskCompletionSource DirtyDialogGate { get; set; }

    public Task<T> ShowDialog<T>() where T : DialogViewModelBase => throw new NotSupportedException();

    public async Task ShowDialog(DialogViewModelBase dialogViewModel)
    {
        if (dialogViewModel is SaveDirtyDocumentDialogViewModel dirtyDocumentDialog)
        {
            DirtyDialogCount++;
            if (DirtyDialogGate is not null)
                await DirtyDialogGate.Task;
            dirtyDocumentDialog.Result = scriptedDirtyDialogAnswers.Count > 0
                ? scriptedDirtyDialogAnswers.Dequeue()
                : DirtyDialogAnswer;
        }
    }

    public Task ShowMessageDialog(string content, DialogMessageType messageType = DialogMessageType.Info)
    {
        MessageDialogCount++;
        return Task.CompletedTask;
    }

    public Task<bool> ShowComfirmDialog(string content, string title = null!, string yesButtonContent = null!,
        string noButtonContent = null!)
    {
        return Task.FromResult(false);
    }

    public void Reset()
    {
        DirtyDialogAnswer = DialogResult.No;
        scriptedDirtyDialogAnswers.Clear();
        DirtyDialogGate = null;
        DirtyDialogCount = 0;
        MessageDialogCount = 0;
    }
}
