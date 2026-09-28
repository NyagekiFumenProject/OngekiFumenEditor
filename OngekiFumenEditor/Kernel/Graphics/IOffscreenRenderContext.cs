using System;
using System.Threading;
using System.Threading.Tasks;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands;

namespace OngekiFumenEditor.Kernel.Graphics
{
    /// <summary>
    /// Offscreen render target: a render context with a fixed size/format that is not part of the control drawing cycle.
    /// The caller submits a <see cref="DrawCommandList"/> and waits for the render to finish; the produced image is the
    /// result (every render produces its own image, owned by the caller).
    /// </summary>
    /// <remarks>
    /// Threading contract:
    /// <list type="number">
    /// <item>Submissions to one offscreen context may come from multiple threads; execution order equals enqueue order;</item>
    /// <item>One <see cref="DrawCommandList"/> must not be submitted concurrently, nor concurrently with <see cref="IDisposable.Dispose"/>
    /// (the command list state machine is lock-free, violating this contract is undefined behavior);</item>
    /// <item>The returned <see cref="IImage"/>: on the Skia backend it can be used from any thread; on the OpenGL backend only
    /// <see cref="IDisposable.Dispose"/> is thread-safe (wrap/filter/ID access must go back to the UI thread).</item>
    /// </list>
    /// Dispose semantics differ per backend:
    /// Skia -- already submitted renders still complete (the task returns its result normally);
    /// OpenGL -- queued requests that have not started yet end with <see cref="ObjectDisposedException"/>, a render in progress still completes.
    /// On either backend, Dispose does not affect images already returned to the caller.
    /// </remarks>
    public interface IOffscreenRenderContext : IDisposable
    {
        /// <summary>Snapshot of the request parameters used at creation (not necessarily the values the backend actually applied).</summary>
        OffscreenRenderOptions Options { get; }

        /// <summary>Whether this context has been disposed. No render may be submitted after Dispose.</summary>
        bool IsDisposed { get; }

        /// <summary>
        /// Submits a command list, renders it, waits for completion and returns the image produced by this render.
        /// <paramref name="autoDispose"/> has the same semantics as <see cref="IRenderContext.PostDrawCommandList"/>:
        /// true means the implementation releases the command list once this render has definitely finished.
        /// </summary>
        /// <remarks>
        /// Cancellation semantics (<paramref name="cancellationToken"/> only affects requests that have not started rendering):
        /// already cancelled when submitted -- not queued, the returned task is cancelled immediately, and the command list is
        /// released immediately when <paramref name="autoDispose"/> is true;
        /// cancelled while queued -- rendering is skipped, the command list is handled per <paramref name="autoDispose"/> and the task ends cancelled;
        /// rendering already started -- cancellation has no effect, the render completes normally and returns the image.
        /// <para>
        /// Execution site: on the Skia backend the render runs on the manager's offscreen render thread; on the OpenGL backend it runs
        /// inside the GL control's render callback, so <b>never wait for this task synchronously on the UI thread</b>
        /// (<c>.Result</c> / <c>.Wait()</c> would deadlock).
        /// The OpenGL backend also requires a visible GL control that keeps producing frames, otherwise a render may stay pending
        /// for a long time (disposing this context via <see cref="IDisposable.Dispose"/> ends pending requests).
        /// </para>
        /// <para>
        /// On render failure the task ends with the original exception and the context remains usable; the target contents of a failed frame are undefined.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="drawCommandList"/> is null.</exception>
        /// <exception cref="ObjectDisposedException">The context has been disposed, or the command list has been disposed.</exception>
        /// <exception cref="InvalidOperationException">The command list is being used by another render flow.</exception>
        Task<IImage> RenderToImageAsync(DrawCommandList drawCommandList, bool autoDispose = true, CancellationToken cancellationToken = default);
    }
}
