using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Utils;

namespace OngekiFumenEditor.Kernel.Graphics.Skia
{
    /// <summary>
    /// Skia 离屏渲染通道：manager 内唯一的后台线程 + <b>无界</b> FIFO 队列。
    /// 所有离屏上下文共用该线程顺序渲染；线程随 manager 首次创建离屏上下文启动，活到 <c>Term()</c>/进程退出。
    /// </summary>
    internal sealed class SkiaOffscreenRenderLane
    {
        private readonly BlockingCollection<SkiaOffscreenRenderRequest> queue = new(new ConcurrentQueue<SkiaOffscreenRenderRequest>());
        private readonly Thread thread;
        private int terminating;

        public SkiaOffscreenRenderLane(string threadName)
        {
            thread = new Thread(Loop)
            {
                IsBackground = true,
                Name = threadName,
            };
            thread.Start();
        }

        /// <summary>是否已进入终止流程（终止后不再接受提交）。</summary>
        public bool IsTerminated => Volatile.Read(ref terminating) != 0;

        /// <summary>提交一个渲染请求。通道已终止时抛 <see cref="ObjectDisposedException"/>。</summary>
        public void Submit(SkiaOffscreenRenderRequest request)
        {
            if (request is null)
                throw new ArgumentNullException(nameof(request));

            try
            {
                queue.Add(request);
            }
            catch (InvalidOperationException)
            {
                throw new ObjectDisposedException(nameof(SkiaOffscreenRenderLane), "离屏渲染通道已关闭，无法提交新的渲染请求。");
            }
        }

        /// <summary>
        /// 关闭通道并等待返回：尚未开始的排队请求立即结束（取消），在途请求完成后本方法才返回。
        /// 该方法是同步的（不依赖任何消息泵），可在退出路径上直接调用。
        /// </summary>
        public void Terminate()
        {
            if (Interlocked.Exchange(ref terminating, 1) != 0)
            {
                thread.Join();
                return;
            }

            queue.CompleteAdding();
            thread.Join();
        }

        private void Loop()
        {
            foreach (var request in queue.GetConsumingEnumerable())
            {
                if (Volatile.Read(ref terminating) != 0)
                    request.End(new OperationCanceledException("离屏渲染通道已关闭，排队中的渲染请求被取消。"));
                else if (request.CancellationToken.IsCancellationRequested)
                    request.EndCanceled();
                else
                    request.Execute();
            }
        }
    }

    /// <summary>
    /// 一次离屏渲染请求。生命周期与 <see cref="DrawCommandList"/> 的 present 状态严格配对：
    /// 提交线程调用 <c>TryBeginPresent()</c>，渲染通道线程在结束时调用一次 <c>EndPresent()</c>，
    /// 随后按 <c>autoDispose</c> 释放命令列表。
    /// </summary>
    internal sealed class SkiaOffscreenRenderRequest
    {
        private readonly SkiaOffscreenRenderContext context;
        private readonly DrawCommandList drawCommandList;
        private readonly bool autoDispose;
        private readonly TaskCompletionSource<IImage> taskSource;
        private int completed;

        public SkiaOffscreenRenderRequest(SkiaOffscreenRenderContext context, DrawCommandList drawCommandList, bool autoDispose, CancellationToken cancellationToken)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            this.drawCommandList = drawCommandList ?? throw new ArgumentNullException(nameof(drawCommandList));
            this.autoDispose = autoDispose;
            CancellationToken = cancellationToken;

            taskSource = new TaskCompletionSource<IImage>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public CancellationToken CancellationToken { get; }

        public Task<IImage> Task => taskSource.Task;

        /// <summary>在渲染通道线程上执行本次渲染。</summary>
        public void Execute()
        {
            if (Interlocked.Exchange(ref completed, 1) != 0)
                return;

            IImage image = null;
            Exception error = null;

            try
            {
                image = context.RenderCore(drawCommandList);
            }
            catch (Exception ex)
            {
                error = ex;
            }
            finally
            {
                Finish();
            }

            // 先完成清理再唤醒调用方：调用方观察到任务完成时，autoDispose 的列表必然已经完全释放，
            // 否则调用方线程（例如 using 块）可能在渲染线程仍处于 DisposeCore 时再次进入 DisposeCore，并发清理同一个池化列表。
            if (error is null)
                taskSource.TrySetResult(image);
            else
                taskSource.TrySetException(error);
        }

        /// <summary>跳过渲染并以指定异常结束（通道关闭等）。</summary>
        public void End(Exception exception)
        {
            if (Interlocked.Exchange(ref completed, 1) != 0)
                return;

            Finish();
            taskSource.TrySetException(exception);
        }

        /// <summary>跳过渲染并以取消结束（提交时携带的取消令牌已被触发）。</summary>
        public void EndCanceled()
        {
            if (Interlocked.Exchange(ref completed, 1) != 0)
                return;

            Finish();
            taskSource.TrySetCanceled(CancellationToken);
        }

        private void Finish()
        {
            try
            {
                // 必须在 EndPresent() 之前 Dispose()：此时列表处于 present 中，Dispose 只会置 DisposeRequested，
                // 真正的释放由随后唯一一次 EndPresent() 完成。反序会让状态短暂回到 Normal，
                // 使调用方线程的并发 Dispose 也进入 DisposeCore，两个线程同时清理池化列表。
                if (autoDispose)
                    drawCommandList.Dispose();

                drawCommandList.EndPresent();
            }
            catch (Exception ex)
            {
                // 清理失败不能杀死渲染通道线程，否则后续所有请求都会永久挂起。
                Log.LogError($"离屏渲染请求清理命令列表失败: {ex.Message}");
            }

            context.OnRequestFinished();
        }
    }
}
