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
    /// <summary>A pending GL object deletion; a field equal to 0 means no object of that kind.</summary>
    internal readonly record struct OpenGLPendingDeletion(int TextureId, int FramebufferId);

    /// <summary>
    /// Shared OpenGL offscreen render queue: all offscreen contexts share the same queue, and renders are drained inside the
    /// render callback of any active GL control (with the OpenGL context current).
    /// It also serves as the <b>delayed deletion queue</b> for GL objects -- every GL deletion must run while a context is current.
    /// </summary>
    internal sealed class OpenGLOffscreenRenderQueue
    {
        /// <summary>Upper bound on renders per drain, so unbounded backlog does not stretch a UI frame.</summary>
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

        /// <summary>Number of GL objects currently queued for deletion.</summary>
        public int PendingDeletionCount => pendingDeletions.Count;

        /// <summary>Submits a render request; throws <see cref="ObjectDisposedException"/> when the queue has been closed.</summary>
        public void Submit(OpenGLOffscreenRenderRequest request)
        {
            if (request is null)
                throw new ArgumentNullException(nameof(request));

            if (IsTerminated)
                throw new ObjectDisposedException(nameof(OpenGLOffscreenRenderQueue), "The offscreen render queue has been closed, no new render requests can be submitted.");

            requests.Enqueue(request);
        }

        /// <summary>Registers a GL object to delete (performed on the next drain or on shutdown).</summary>
        public void EnqueueDeletion(in OpenGLPendingDeletion deletion)
        {
            pendingDeletions.Enqueue(deletion);
        }

        /// <summary>
        /// Drains inside a GL control render callback (with the context current): delayed deletions first, then a bounded number of render requests.
        /// The whole method must sit inside a try/catch boundary (a render exception must not escape into the WPF render loop).
        /// </summary>
        public void Pump(DefaultOpenGLRenderManagerImpl manager)
        {
            ExecutePendingDeletions();

            for (var i = 0; i < MaxRendersPerDrain && requests.TryDequeue(out var request); i++)
            {
                if (IsTerminated)
                    request.End(new OperationCanceledException("The offscreen render queue has been closed."));
                else if (request.CancellationToken.IsCancellationRequested)
                    request.EndCanceled();
                else
                    request.Execute(manager);
            }
        }

        /// <summary>
        /// Ends queued requests that have not started with the given exception; when <paramref name="context"/> is null all contexts are handled.
        /// Re-queued requests keep their relative order (absolute cross-context order is not guaranteed after a cancellation, but each context stays ordered).
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
        /// Closes the queue: cancels queued requests; runs delayed deletions when the current thread has a current GL context, otherwise drops them and logs.
        /// Completes synchronously and never waits for a render tick (the shutdown path relies on this).
        /// </summary>
        public void Terminate()
        {
            lock (gate)
            {
                if (terminated)
                    return;
                terminated = true;
            }

            CancelQueued(null, new OperationCanceledException("The offscreen render queue has been closed; queued render requests were cancelled."));

            if (DefaultOpenGLRenderManagerImpl.HasCurrentGlContext())
                ExecutePendingDeletions();
            else
                Log.LogWarn($"No current GL context while closing the offscreen render queue; {pendingDeletions.Count} pending GL deletions were dropped (they are released when the process exits).");
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
    /// One OpenGL offscreen render request. Its lifetime is strictly paired with the present state of <see cref="DrawCommandList"/>:
    /// the submitting thread calls <c>TryBeginPresent()</c>, the drain thread calls <c>EndPresent()</c> exactly once when it
    /// finishes, and the command list is then released according to <c>autoDispose</c>.
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

        /// <summary>Runs this render inside a drain (with the context current).</summary>
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

            // Finish the cleanup before waking the caller (same reasoning as the Skia request): a completed task means the autoDispose list has already been released.
            if (error is null)
                taskSource.TrySetResult(image);
            else
                taskSource.TrySetException(error);
        }

        /// <summary>Skips the render and ends with the given exception (queue closed, context disposed, etc.).</summary>
        public void End(Exception exception)
        {
            if (Interlocked.Exchange(ref completed, 1) != 0)
                return;

            Finish();
            taskSource.TrySetException(exception);
        }

        /// <summary>Skips the render and ends as cancelled (the cancellation token supplied at submission time has been triggered).</summary>
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
                // Same as the Skia request: Dispose() first (while presenting it only sets DisposeRequested) and then the single
                // real EndPresent() release, so a concurrent Dispose from the caller thread cannot enter DisposeCore at the same time.
                if (autoDispose)
                    drawCommandList.Dispose();

                drawCommandList.EndPresent();
            }
            catch (Exception ex)
            {
                // A drain is part of the WPF render callback: a cleanup exception must never escape into the WPF render loop.
                Log.LogError($"Failed to clean up the command list for an offscreen render request: {ex.Message}");
            }
        }
    }
}
