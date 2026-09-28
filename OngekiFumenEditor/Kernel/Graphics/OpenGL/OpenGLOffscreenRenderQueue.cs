using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Kernel.Graphics.OpenGL.Base;
using OngekiFumenEditor.Utils;
using OpenTK.Graphics.OpenGL;

namespace OngekiFumenEditor.Kernel.Graphics.OpenGL
{
    /// <summary>一次待执行的 GL 对象删除；两个字段中为 0 的表示没有该类对象。</summary>
    internal readonly record struct OpenGLPendingDeletion(int TextureId, int FramebufferId);

    /// <summary>
    /// OpenGL 离屏渲染共享队列：所有离屏上下文共用同一个队列，
    /// 渲染在任意活动 GL 控件的渲染回调（OpenGL 上下文 current）内排空执行（drain）。
    /// 同时承担 GL 对象的<b>延迟删除队列</b>——所有 GL 删除操作都必须回到上下文 current 的时机执行。
    /// </summary>
    internal sealed class OpenGLOffscreenRenderQueue
    {
        /// <summary>单次排空的渲染条数上限，避免无界积压把 UI 帧拖长。</summary>
        private const int MaxRendersPerDrain = 4;

        private readonly ConcurrentQueue<OpenGLOffscreenRenderRequest> requests = new();
        private readonly ConcurrentQueue<OpenGLPendingDeletion> pendingDeletions = new();
        private readonly object gate = new();
        private bool terminated;

        public bool IsTerminated
        {
            get
            {
                lock (gate)
                    return terminated;
            }
        }

        /// <summary>当前排队等待删除的 GL 对象数量。</summary>
        public int PendingDeletionCount => pendingDeletions.Count;

        /// <summary>提交一个渲染请求；队列已关闭时抛 <see cref="ObjectDisposedException"/>。</summary>
        public void Submit(OpenGLOffscreenRenderRequest request)
        {
            if (request is null)
                throw new ArgumentNullException(nameof(request));

            if (IsTerminated)
                throw new ObjectDisposedException(nameof(OpenGLOffscreenRenderQueue), "离屏渲染队列已关闭，无法提交新的渲染请求。");

            requests.Enqueue(request);
        }

        /// <summary>登记一个待删除的 GL 对象（下次 drain 或退出时执行）。</summary>
        public void EnqueueDeletion(in OpenGLPendingDeletion deletion)
        {
            pendingDeletions.Enqueue(deletion);
        }

        /// <summary>
        /// 在 GL 控件渲染回调（上下文 current）内排空：先执行延迟删除，再执行限量的渲染请求。
        /// 本方法必须整体处于 try/catch 边界内（渲染异常不得穿透到 WPF 渲染循环）。
        /// </summary>
        public void Pump(DefaultOpenGLRenderManagerImpl manager)
        {
            ExecutePendingDeletions();

            for (var i = 0; i < MaxRendersPerDrain && requests.TryDequeue(out var request); i++)
            {
                if (IsTerminated)
                    request.End(new OperationCanceledException("离屏渲染队列已关闭。"));
                else if (request.CancellationToken.IsCancellationRequested)
                    request.EndCanceled();
                else
                    request.Execute(manager);
            }
        }

        /// <summary>
        /// 把尚未开始的排队请求以指定异常结束；<paramref name="context"/> 为 null 时处理所有上下文。
        /// 重新入队的其它请求保持相对顺序（跨上下文的绝对顺序在取消后不保证，各上下文内部仍保序）。
        /// </summary>
        public void CancelQueued(OpenGLOffscreenRenderContext context, Exception exception)
        {
            List<OpenGLOffscreenRenderRequest> requeue = null;

            while (requests.TryDequeue(out var request))
            {
                if (context is null || ReferenceEquals(request.Context, context))
                {
                    request.End(exception);
                    continue;
                }

                (requeue ??= new List<OpenGLOffscreenRenderRequest>()).Add(request);
            }

            if (requeue is not null)
            {
                foreach (var request in requeue)
                    requests.Enqueue(request);
            }
        }

        /// <summary>
        /// 关闭队列：取消排队请求；若当前线程有 current 的 GL 上下文则执行延迟删除，否则放弃并记日志。
        /// 同步完成，不等待任何渲染 tick（退出路径依赖这一点）。
        /// </summary>
        public void Terminate()
        {
            lock (gate)
            {
                if (terminated)
                    return;
                terminated = true;
            }

            CancelQueued(null, new OperationCanceledException("离屏渲染队列已关闭，排队中的渲染请求被取消。"));

            if (DefaultOpenGLRenderManagerImpl.HasCurrentGlContext())
                ExecutePendingDeletions();
            else
                Log.LogWarn($"离屏渲染队列关闭时没有 current 的 GL 上下文，{pendingDeletions.Count} 个待删除 GL 对象被放弃（随进程退出释放）。");
        }

        private void ExecutePendingDeletions()
        {
            while (pendingDeletions.TryDequeue(out var deletion))
            {
                if (deletion.TextureId != 0)
                {
                    GL.DeleteTexture(deletion.TextureId);
                    OpenGLTextureBindingCache.InvalidateTexture(deletion.TextureId);
                }

                if (deletion.FramebufferId != 0)
                    GL.DeleteFramebuffer(deletion.FramebufferId);
            }
        }
    }

    /// <summary>
    /// 一次 OpenGL 离屏渲染请求。生命周期与 <see cref="DrawCommandList"/> 的 present 状态严格配对：
    /// 提交线程调用 <c>TryBeginPresent()</c>，drain 线程在结束时调用一次 <c>EndPresent()</c>，随后按 <c>autoDispose</c> 释放命令列表。
    /// </summary>
    internal sealed class OpenGLOffscreenRenderRequest
    {
        private readonly DrawCommandList drawCommandList;
        private readonly bool autoDispose;
        private readonly TaskCompletionSource<IImage> taskSource;
        private int completed;

        public OpenGLOffscreenRenderRequest(OpenGLOffscreenRenderContext context, DrawCommandList drawCommandList, bool autoDispose, CancellationToken cancellationToken)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            this.drawCommandList = drawCommandList ?? throw new ArgumentNullException(nameof(drawCommandList));
            this.autoDispose = autoDispose;
            CancellationToken = cancellationToken;

            taskSource = new TaskCompletionSource<IImage>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public OpenGLOffscreenRenderContext Context { get; }

        public CancellationToken CancellationToken { get; }

        public Task<IImage> Task => taskSource.Task;

        /// <summary>在 drain（上下文 current）内执行本次渲染。</summary>
        public void Execute(DefaultOpenGLRenderManagerImpl manager)
        {
            if (Interlocked.Exchange(ref completed, 1) != 0)
                return;

            IImage image = null;
            Exception error = null;

            try
            {
                image = Context.RenderCore(manager, drawCommandList);
            }
            catch (Exception ex)
            {
                error = ex;
            }
            finally
            {
                Finish();
            }

            // 先完成清理再唤醒调用方（理由同 Skia 请求）：任务完成即代表 autoDispose 的列表已释放完毕。
            if (error is null)
                taskSource.TrySetResult(image);
            else
                taskSource.TrySetException(error);
        }

        /// <summary>跳过渲染并以指定异常结束（队列关闭、上下文释放等）。</summary>
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
                // 与 Skia 请求相同：先 Dispose()（present 中只置 DisposeRequested）再 EndPresent()（唯一一次真正释放），
                // 避免调用方线程的并发 Dispose 与本次清理同时进入 DisposeCore。
                if (autoDispose)
                    drawCommandList.Dispose();

                drawCommandList.EndPresent();
            }
            catch (Exception ex)
            {
                // drain 是 WPF 渲染回调的一部分：清理异常绝不能穿透到 WPF 渲染循环。
                Log.LogError($"离屏渲染请求清理命令列表失败: {ex.Message}");
            }
        }
    }
}
