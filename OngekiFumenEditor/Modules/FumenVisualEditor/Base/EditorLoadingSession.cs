using Caliburn.Micro;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Dialogs;
using OngekiFumenEditor.Utils;
using System;
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
        public const int StepCount = 5;
        /// <summary>等待渲染初始化与首帧的上限；超时只记日志，不阻塞对话框关闭。</summary>
        public static readonly TimeSpan EditorReadyTimeout = TimeSpan.FromSeconds(30);

        private readonly EditorLoadingDialogViewModel dialog;
        private readonly TaskCompletionSource<EditorLoadingOutcome> completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool disposed;

        /// <param name="step">起始步骤。</param>
        /// <param name="targetName">正在加载的谱面/工程名，显示给用户确认。</param>
        public static EditorLoadingSession Begin(EditorLoadingStep step, string targetName)
        {
            var session = new EditorLoadingSession(targetName);
            Log.LogInfo($"Editor loading session started (step={step}, steps={StepCount}, target={targetName})");
            session.ReportStep(step);
            session.ShowDialog();
            return session;
        }

        private EditorLoadingSession(string targetName)
        {
            dialog = new EditorLoadingDialogViewModel(targetName);
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

        public void Complete(EditorLoadingOutcome outcome)
        {
            Log.LogInfo($"Editor loading session finished: {outcome}");
            if (!completion.TrySetResult(outcome))
                return;
            dialog.Finish();
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            dialog.CancellationRequested -= OnCancellationRequested;
            completion.TrySetResult(EditorLoadingOutcome.Failed);
            dialog.Finish();
        }

        private void OnCancellationRequested(object sender, EventArgs e)
        {
            Log.LogInfo("Editor loading cancellation requested.");
            // 只解锁等待方；对话框本体等加载流程真正退栈后由 Complete/Dispose 关闭。
            completion.TrySetResult(EditorLoadingOutcome.Cancelled);
        }

        private void ShowDialog()
        {
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
