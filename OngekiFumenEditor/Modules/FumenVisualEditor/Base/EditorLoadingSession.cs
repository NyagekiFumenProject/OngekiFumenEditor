using Caliburn.Micro;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Dialogs;
using OngekiFumenEditor.Utils;
using System;
using System.Diagnostics;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Base
{
    /// <summary>加载步骤，数值 1..StepCount 即进度序号。</summary>
    public enum EditorLoadingStep
    {
        Preparing = 1,
        SelectingAudio = 2,
        Parsing = 3,
        LoadingAudio = 4,
        InitializingRender = 5,
        CollectingMemory = 6,
    }

    public enum EditorLoadingOutcome
    {
        Ready,
        Cancelled,
        Failed,
    }

    /// <summary>
    /// 一次「谱面加载到编辑器」的全过程：创建时弹出模态加载对话框，流程按步骤上报，
    /// 结束/失败/取消时关闭对话框；取消通过 <see cref="CancellationToken"/> 协作式中止。
    /// </summary>
    public sealed class EditorLoadingSession : IDisposable
    {
        public const int StepCount = 6;
        /// <summary>等待渲染初始化与首帧的上限；超时只记日志，不阻塞对话框关闭。</summary>
        public static readonly TimeSpan EditorReadyTimeout = TimeSpan.FromSeconds(30);

        private static readonly object sharedDialogLock = new();
        private static EditorLoadingDialogViewModel sharedDialog;
        private static int sharedDialogUsers;

        private readonly EditorLoadingDialogViewModel dialog;
        private readonly bool ownsDialog;
        private readonly TaskCompletionSource<EditorLoadingOutcome> completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool disposed;
        private bool dialogReleased;

        /// <param name="step">起始步骤。</param>
        /// <param name="targetName">正在加载的谱面/工程名，显示给用户确认。</param>
        public static EditorLoadingSession Begin(EditorLoadingStep step, string targetName)
        {
            var session = new EditorLoadingSession(targetName);
            Log.LogInfo($"Editor loading session started (step={step}, steps={StepCount}, target={targetName})");
            session.ReportStep(step);
            session.ShowDialogIfOwner();
            return session;
        }

        private EditorLoadingSession(string targetName)
        {
            lock (sharedDialogLock)
            {
                // 并发加载复用同一个加载对话框：第一个会话创建并显示它，后来者只挂上去；
                // 最后一个退栈的会话负责关闭（见 ReleaseDialog）。
                if (sharedDialog is { IsFinished: false })
                {
                    dialog = sharedDialog;
                    dialog.SetTarget(targetName);
                    sharedDialogUsers++;
                }
                else
                {
                    dialog = new EditorLoadingDialogViewModel(targetName);
                    sharedDialog = dialog;
                    sharedDialogUsers = 1;
                    ownsDialog = true;
                }
            }

            dialog.CancellationRequested += OnCancellationRequested;
        }

        public CancellationToken CancellationToken => dialog.CancellationToken;
        /// <summary>加载结束（Ready/Cancelled/Failed）时完成；entry point 用它等到编辑器就绪。</summary>
        public Task<EditorLoadingOutcome> Completion => completion.Task;

        public void ReportStep(EditorLoadingStep step)
        {
            Log.LogInfo($"Editor loading step: {step}");
            dialog.ReportStep(step);
        }

        /// <summary>
        /// 加载末尾回收加载期在 LOH 留下的大对象空洞（实测碎片 ~300 MB）。CompactOnce 只对「下一次
        /// full blocking GC」生效（后台 GC 不算），故必须紧跟一次 GC.Collect()；它是阻塞式收集，
        /// 只能留在加载路径。先让对话框把本步骤画出来（Render 优先级有界等待，避免被渲染循环饿死），
        /// 再开始收集。
        /// </summary>
        public async Task CollectLoadingGarbageAsync()
        {
            ReportStep(EditorLoadingStep.CollectingMemory);
            await Dispatcher.Yield(DispatcherPriority.Render);

            var sw = Stopwatch.StartNew();
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect();
            Log.LogInfo($"Editor loading step: LOH compaction finished in {sw.ElapsedMilliseconds} ms");
        }

        public void Complete(EditorLoadingOutcome outcome)
        {
            Log.LogInfo($"Editor loading session finished: {outcome}");
            if (!completion.TrySetResult(outcome))
                return;
            ReleaseDialog();
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            completion.TrySetResult(EditorLoadingOutcome.Failed);
            ReleaseDialog();
        }

        /// <summary>
        /// 从共享对话框上摘掉本会话；只有最后一个退栈的会话才真正关闭对话框，
        /// 这样并发加载不会留下别人已经不再负责的孤儿窗口。
        /// </summary>
        private void ReleaseDialog()
        {
            if (dialogReleased)
                return;
            dialogReleased = true;

            dialog.CancellationRequested -= OnCancellationRequested;

            var closeDialog = false;
            lock (sharedDialogLock)
            {
                sharedDialogUsers--;
                if (sharedDialogUsers <= 0)
                {
                    sharedDialogUsers = 0;
                    closeDialog = ReferenceEquals(sharedDialog, dialog);
                    if (closeDialog)
                        sharedDialog = null;
                }
            }

            if (closeDialog)
                dialog.Finish();
        }

        private void OnCancellationRequested(object sender, EventArgs e)
        {
            Log.LogInfo("Editor loading cancellation requested.");
            // 只解锁等待方；对话框本体等加载流程真正退栈后由 Complete/Dispose 关闭。
            completion.TrySetResult(EditorLoadingOutcome.Cancelled);
        }

        private void ShowDialogIfOwner()
        {
            if (!ownsDialog)
                return;

            // Caliburn 的 ShowDialogAsync 会同步进入 ShowDialog() 的嵌套消息循环，
            // 直接调用会卡住加载流程；投递到队列后加载继续跑在该嵌套循环里。
            Dispatcher.CurrentDispatcher.InvokeAsync(async () =>
            {
                if (dialog.IsFinished)
                    return;
                try
                {
                    await IoC.Get<IWindowManager>().ShowDialogAsync(dialog);
                }
                catch (Exception e)
                {
                    Log.LogError($"Failed to show the editor loading dialog: {e.Message}", e);
                }
            });
        }
    }
}
