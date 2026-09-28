using System;
using System.Threading;
using System.Threading.Tasks;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands;

namespace OngekiFumenEditor.Kernel.Graphics
{
    /// <summary>
    /// 离屏渲染目标：固定尺寸/格式、不参与控件绘制周期的渲染上下文。
    /// 调用方提交 <see cref="DrawCommandList"/> 并等待渲染完成，产出图像即为返回值（每次渲染产出一张独立图像，所有权归调用方）。
    /// </summary>
    /// <remarks>
    /// 线程契约：
    /// <list type="number">
    /// <item>同一个离屏上下文允许从多线程提交，执行顺序等于入队顺序；</item>
    /// <item>同一个 <see cref="DrawCommandList"/> 不得并发提交，也不得与 <see cref="IDisposable.Dispose"/> 并发
    ///（命令列表状态机无锁，违反契约行为未定义）；</item>
    /// <item>返回的 <see cref="IImage"/>：Skia 后端可在任意线程使用；OpenGL 后端只有 <see cref="IDisposable.Dispose"/>
    /// 是线程安全的（wrap/filter/ID 等访问需回到 UI 线程）。</item>
    /// </list>
    /// Dispose 语义按后端不同：
    /// Skia —— 已提交的渲染会照常完成（任务正常返回结果）；
    /// OpenGL —— 尚未开始的排队请求以 <see cref="ObjectDisposedException"/> 结束，正在渲染的请求照常完成。
    /// 无论哪种后端，Dispose 都不影响已经返回给调用方的图像。
    /// </remarks>
    public interface IOffscreenRenderContext : IDisposable
    {
        /// <summary>创建时使用的请求参数快照（不代表后端实际生效值）。</summary>
        OffscreenRenderOptions Options { get; }

        /// <summary>是否已释放。Dispose 后不得再提交渲染。</summary>
        bool IsDisposed { get; }

        /// <summary>
        /// 提交命令列表，渲染并等待完成，返回本次渲染产出的图像。
        /// <paramref name="autoDispose"/> 语义与 <see cref="IRenderContext.PostDrawCommandList"/> 一致：
        /// true 表示本次渲染确定结束后由实现释放该命令列表。
        /// </summary>
        /// <remarks>
        /// 取消语义（<paramref name="cancellationToken"/> 只对「尚未开始渲染」的请求生效）：
        /// 提交时已取消 —— 不排队，立即返回已取消的任务，且 <paramref name="autoDispose"/> 为 true 时立即释放命令列表；
        /// 排队期间取消 —— 取用时跳过渲染，命令列表按 <paramref name="autoDispose"/> 处理，任务以取消结束；
        /// 已开始渲染 —— 取消无效，渲染照常完成并返回图像。
        /// <para>
        /// 执行位置：Skia 后端在 manager 的离屏渲染线程上执行；OpenGL 后端在 GL 控件的渲染回调内执行，
        /// 因此**禁止在 UI 线程上同步等待该任务**（<c>.Result</c> / <c>.Wait()</c> 会死锁）。
        /// OpenGL 后端还要求存在处于可见状态且持续出帧的 GL 控件，否则渲染可能长时间挂起
        ///（可通过对本上下文 <see cref="IDisposable.Dispose"/> 结束挂起的请求）。
        /// </para>
        /// <para>
        /// 渲染失败时任务以原异常结束，上下文仍可继续使用；失败帧的目标内容未定义。
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="drawCommandList"/> 为 null。</exception>
        /// <exception cref="ObjectDisposedException">上下文已释放，或命令列表已释放。</exception>
        /// <exception cref="InvalidOperationException">命令列表正在被其它渲染流程使用。</exception>
        Task<IImage> RenderToImageAsync(DrawCommandList drawCommandList, bool autoDispose = true, CancellationToken cancellationToken = default);
    }
}
