using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Utils;

namespace OngekiFumenEditor.Kernel.Graphics.Skia
{
    /// <summary>
    /// Skia offscreen render lane: the single background thread inside the manager plus an <b>unbounded</b> FIFO queue.
    /// All offscreen contexts share this thread and render sequentially on it; the thread starts with the manager's first
    /// offscreen context and lives until <c>Term()</c>/process exit.
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

        /// <summary>Whether the lane has entered termination (no further submissions are accepted afterwards).</summary>
        public bool IsTerminated => Volatile.Read(ref terminating) != 0;

        /// <summary>Submits a render request. Throws <see cref="ObjectDisposedException"/> when the lane has been terminated.</summary>
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
                throw new ObjectDisposedException(nameof(SkiaOffscreenRenderLane), "The offscreen render lane has been closed, no new render requests can be submitted.");
            }
        }

        /// <summary>
        /// Closes the lane and waits: queued requests that have not started end immediately (cancelled), and this method only
        /// returns once in-flight requests have finished. It is synchronous (no message pump involved) and can be called
        /// directly on the shutdown path.
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
                    request.End(new OperationCanceledException("The offscreen render lane has been closed; queued render requests were cancelled."));
                else if (request.CancellationToken.IsCancellationRequested)
                    request.EndCanceled();
                else
                    request.Execute();
            }
        }
    }

    /// <summary>
    /// One offscreen render request. Its lifetime is strictly paired with the present state of <see cref="DrawCommandList"/>:
    /// the submitting thread calls <c>TryBeginPresent()</c>, the render lane thread calls <c>EndPresent()</c> exactly once when
    /// it finishes, and the command list is then released according to <c>autoDispose</c>.
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

        /// <summary>Runs this render on the render lane thread.</summary>
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

            // Finish the cleanup before waking the caller: when the caller observes the completed task, an autoDispose list must
            // already be fully released; otherwise the caller thread (e.g. a using block) could enter DisposeCore again while the
            // render thread is still inside DisposeCore, cleaning up the same pooled list concurrently.
            if (error is null)
                taskSource.TrySetResult(image);
            else
                taskSource.TrySetException(error);
        }

        /// <summary>Skips the render and ends with the given exception (lane closed, etc.).</summary>
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
                // Dispose() must come before EndPresent(): the list is presenting here, so Dispose only sets DisposeRequested and
                // the single following EndPresent() performs the real release. The reverse order would briefly return the state to
                // Normal, letting a concurrent Dispose from the caller thread also enter DisposeCore, with both threads cleaning the
                // same pooled list.
                if (autoDispose)
                    drawCommandList.Dispose();

                drawCommandList.EndPresent();
            }
            catch (Exception ex)
            {
                // A cleanup failure must not kill the render lane thread, otherwise every later request would hang forever.
                Log.LogError($"Failed to clean up the command list for an offscreen render request: {ex.Message}");
            }

            context.OnRequestFinished();
        }
    }
}
