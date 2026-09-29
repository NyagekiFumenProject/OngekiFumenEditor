using Caliburn.Micro;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.ProgramUpdater.Dialogs.ViewModels
{
    public class UpdateProgressDialogViewModel : Screen
    {
        private readonly IProgramUpdater programUpdater;
        private readonly IProgress<UpdatePrepareProgress> progress;
        // 重试时重建：一次准备流程（含取消）对应一个 CTS。
        private CancellationTokenSource cancellation = new();

        private string stateText = Resources.UpdaterStepDownloading;
        private double progressValue = -1;
        private bool isPreparing = true;
        private bool isReady;
        private bool isFailed;
        private bool isCancelling;
        private bool closeApproved;
        private bool viewLoaded;
        private bool started;

        public UpdateProgressDialogViewModel()
        {
            programUpdater = IoC.Get<IProgramUpdater>();
            // Progress<T> 在构造时捕获 UI 线程的同步上下文，报告会自动回到 UI 线程。
            progress = new Progress<UpdatePrepareProgress>(OnProgressReported);
            DisplayName = Resources.UpdaterProgressTitle;
        }

        /// <summary>「正在更新：旧版本 → 新版本」。</summary>
        public string TargetText => string.Format(Resources.UpdaterTargetFormat,
            Version.Parse(ThisAssembly.AssemblyFileVersion).ToString(4), programUpdater.RemoteVersionInfo?.Version);

        public string StateText => isCancelling ? Resources.UpdaterStepCancelling : stateText;
        public double ProgressValue => progressValue;
        public bool IsProgressIndeterminate => progressValue < 0;
        public string ProgressText => progressValue < 0 ? string.Empty : progressValue.ToString("P0");
        public bool IsPreparing => isPreparing;
        public bool IsReady => isReady;
        public bool IsFailed => isFailed;
        public bool CanCancel => isPreparing && !isCancelling;

        public void Cancel() => RequestCancellation();

        public void ConfirmStartUpdate()
        {
            try
            {
                closeApproved = true;
                programUpdater.LaunchPreparedUpdate();
            }
            catch (Exception e)
            {
                ShowFailure(e);
            }
        }

        public void CloseDialog()
        {
            closeApproved = true;
            _ = CloseAsync();
        }

        /// <summary>下载/解压失败后重新开始一次准备流程。</summary>
        public void Retry()
        {
            if (isPreparing)
                return;

            cancellation = new CancellationTokenSource();
            isCancelling = false;
            isFailed = false;
            isReady = false;
            isPreparing = true;
            stateText = Resources.UpdaterStepDownloading;
            progressValue = -1;
            NotifyProgress();
            NotifyState();
            Log.LogInfo("Retrying update preparation.");
            _ = PrepareAsync();
        }

        protected override void OnViewLoaded(object view)
        {
            base.OnViewLoaded(view);
            viewLoaded = true;
            if (!started)
            {
                started = true;
                _ = PrepareAsync();
            }
        }

        public override Task<bool> CanCloseAsync(CancellationToken cancellationToken = default)
        {
            if (closeApproved || isReady || isFailed)
                return Task.FromResult(true);

            // 准备阶段里标题栏 X 等同于点「取消」，等准备流程退栈后再真正关闭。
            RequestCancellation();
            return Task.FromResult(false);
        }

        private async Task PrepareAsync()
        {
            try
            {
                Log.LogInfo("Update preparation started.");
                await programUpdater.PrepareUpdateAsync(progress, cancellation.Token);

                isPreparing = false;
                isReady = true;
                stateText = Resources.UpdaterStepReady;
                progressValue = 1;
                NotifyProgress();
                NotifyState();
                Log.LogInfo("Update package is ready, waiting for user confirmation.");
            }
            catch (OperationCanceledException)
            {
                Log.LogInfo("Update preparation was cancelled by user.");
                closeApproved = true;
                await CloseAsync();
            }
            catch (Exception e)
            {
                ShowFailure(e);
            }
        }

        private void OnProgressReported(UpdatePrepareProgress report)
        {
            // 准备流程结束后仍可能收到最后一条上报（Progress<T> 异步投递），不能让它盖掉就绪/失败提示。
            if (!isPreparing || cancellation.IsCancellationRequested)
                return;

            progressValue = report.Progress;
            if (report.Step == UpdatePrepareStep.Extracting)
                stateText = Resources.UpdaterStepExtracting;
            else
            {
                var speed = FormatSpeed(report.BytesPerSecond);
                stateText = string.IsNullOrEmpty(speed)
                    ? Resources.UpdaterStepDownloading
                    : string.Format(Resources.UpdaterStepDownloadingFormat, speed);
            }

            NotifyProgress();
            NotifyOfPropertyChange(nameof(StateText));
        }

        private static string FormatSpeed(double bytesPerSecond) => bytesPerSecond switch
        {
            <= 0 => string.Empty,
            < 1024 => $"{bytesPerSecond:0} B/s",
            < 1024 * 1024 => $"{bytesPerSecond / 1024:0.0} KB/s",
            _ => $"{bytesPerSecond / 1024 / 1024:0.00} MB/s",
        };

        private void ShowFailure(Exception e)
        {
            Log.LogError($"Failed to prepare update: {e.Message}", e);
            isPreparing = false;
            isReady = false;
            isFailed = true;
            stateText = string.Format(Resources.UpdaterStepFailedFormat, e.Message);
            NotifyState();
        }

        private void NotifyProgress()
        {
            NotifyOfPropertyChange(nameof(ProgressValue));
            NotifyOfPropertyChange(nameof(IsProgressIndeterminate));
            NotifyOfPropertyChange(nameof(ProgressText));
        }

        private void NotifyState()
        {
            NotifyOfPropertyChange(nameof(StateText));
            NotifyOfPropertyChange(nameof(IsPreparing));
            NotifyOfPropertyChange(nameof(IsReady));
            NotifyOfPropertyChange(nameof(IsFailed));
            NotifyOfPropertyChange(nameof(CanCancel));
        }

        // 不要 Dispose 内部 CTS：取消后准备流程仍可能在跑并读取 Token（Dispose 后读取会抛 ObjectDisposedException）。
        private void RequestCancellation()
        {
            if (isCancelling)
                return;
            isCancelling = true;
            NotifyOfPropertyChange(nameof(StateText));
            NotifyOfPropertyChange(nameof(CanCancel));
            cancellation.Cancel();
        }

        private async Task CloseAsync()
        {
            if (!viewLoaded)
            {
                Log.LogWarn("Update progress dialog finished before its window was ever shown; nothing to close.");
                return;
            }

            try
            {
                await TryCloseAsync(true);
            }
            catch (Exception e)
            {
                Log.LogError($"Failed to close the update progress dialog: {e.Message}", e);
            }
        }
    }
}
