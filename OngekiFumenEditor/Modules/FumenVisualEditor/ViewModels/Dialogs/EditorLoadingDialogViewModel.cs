using Caliburn.Micro;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Dialogs
{
    public class EditorLoadingDialogViewModel : Screen
    {
        private readonly CancellationTokenSource cancellation = new();
        private readonly string targetName;

        private string stepText = Resources.EditorLoadingStepPreparing;
        private int stepNumber = (int)EditorLoadingStep.Preparing;
        private bool isCancelling;
        private bool closeApproved;
        private bool finished;
        private bool viewLoaded;

        public EditorLoadingDialogViewModel(string targetName)
        {
            this.targetName = targetName;
            DisplayName = Resources.EditorLoadingDialogTitle;
        }

        internal CancellationToken CancellationToken => cancellation.Token;
        internal bool IsFinished => finished;
        /// <summary>用户点「取消」或直接关窗时触发一次（UI 线程）。</summary>
        internal event EventHandler CancellationRequested;

        /// <summary>「正在加载：xxx」，用户据此确认自己打开的是哪个谱面/工程。</summary>
        public string TargetText => string.Format(Resources.EditorLoadingTargetFormat, targetName);
        public string StepText => isCancelling ? Resources.EditorLoadingCancelling : stepText;
        public int StepCount => EditorLoadingSession.StepCount;
        public int StepNumber
        {
            get => stepNumber;
            private set
            {
                if (Set(ref stepNumber, value))
                    NotifyOfPropertyChange(nameof(StepProgressText));
            }
        }
        public string StepProgressText => $"{StepNumber} / {StepCount}";
        public bool CanCancel => !isCancelling;

        public void Cancel() => RequestCancellation();

        protected override void OnViewLoaded(object view)
        {
            base.OnViewLoaded(view);
            viewLoaded = true;
        }

        public override Task<bool> CanCloseAsync(CancellationToken cancellationToken = default)
        {
            if (closeApproved)
                return Task.FromResult(true);
            // 标题栏 X = 请求取消，并否决这次关闭；等加载流程退栈后由 Finish() 真正关闭。
            RequestCancellation();
            return Task.FromResult(false);
        }

        internal void ReportStep(EditorLoadingStep step)
        {
            stepText = GetStepText(step);
            StepNumber = (int)step;
            NotifyOfPropertyChange(nameof(StepText));
        }

        /// <summary>加载流程结束（成功/失败/取消），关闭对话框。可重复调用。</summary>
        internal void Finish()
        {
            if (finished)
                return;
            finished = true;
            closeApproved = true;

            if (!viewLoaded)
            {
                // 对话框窗口从未真正显示（视图加载失败，或加载在窗口显示前就结束）。
                // 此时没有可关闭的窗口，且 ShowDialog 的投递回调会因 IsFinished 提前返回；
                // 绝不能去调 TryCloseAsync —— 对未以模态显示的窗口设置 DialogResult 会抛异常，
                // 而该异常无人观察，会顺着 TaskScheduler.UnobservedTaskException 把整个进程带走。
                Log.LogWarn("Editor loading dialog finished before its window was ever shown; nothing to close.");
                return;
            }

            _ = CloseAsync();
        }

        private async Task CloseAsync()
        {
            try
            {
                await TryCloseAsync(true);
            }
            catch (Exception e)
            {
                Log.LogError($"Failed to close the editor loading dialog: {e.Message}", e);
            }
        }

        // 注意：不要 Dispose 内部 CTS。取消后加载流程仍可能在跑并读取 Token
        //（CancellationTokenSource.Dispose 之后读取 token 会抛 ObjectDisposedException），
        // 这里只负责 Cancel。
        private void RequestCancellation()
        {
            if (isCancelling)
                return;
            isCancelling = true;
            NotifyOfPropertyChange(nameof(StepText));
            NotifyOfPropertyChange(nameof(CanCancel));
            cancellation.Cancel();
            CancellationRequested?.Invoke(this, EventArgs.Empty);
        }

        private static string GetStepText(EditorLoadingStep step) => step switch
        {
            EditorLoadingStep.SelectingAudio => Resources.EditorLoadingStepSelectingAudio,
            EditorLoadingStep.Parsing => Resources.EditorLoadingStepParsing,
            EditorLoadingStep.LoadingAudio => Resources.EditorLoadingStepLoadingAudio,
            EditorLoadingStep.InitializingRender => Resources.EditorLoadingStepInitializingRender,
            EditorLoadingStep.CollectingMemory => Resources.EditorLoadingStepCollectingMemory,
            _ => Resources.EditorLoadingStepPreparing,
        };
    }
}
