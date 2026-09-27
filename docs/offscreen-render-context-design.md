# 离屏渲染上下文设计（`IRenderManagerImpl.CreateOffscreenToImage`）

> 文档性质：设计讨论，实时记录
> 建立日期：2026-09-24
> 最后更新：2026-09-24（第 3 轮：加入 OpenGL 实现变体设计，GL-1…GL-8 + P14…P18）
> 当前状态：讨论中，尚未授权实施
> **目标工程：`OngekiFumenEditor`（WPF，`net10.0-windows`）——Avalonia 侧不在本次范围内**
> **后端范围：Skia 与 OpenGL 两个 `IRenderManagerImpl` 实现都要支持**
> 目标命名空间：`OngekiFumenEditor.Kernel.Graphics` / `...Kernel.Graphics.Skia` / `...Kernel.Graphics.OpenGL`
> 来源需求：`docs/DrawCommandList plan.md`「后续规划 2. 实现离屏渲染」

## 1. 目标与边界

### 1.1 目标

WPF 工程的 `IRenderManagerImpl` 增加离屏渲染入口：调用方显式指定输出图像参数（宽度、高度、像素格式、alpha 类型、颜色空间等），
拿到一个 `IOffscreenRenderContext`，由它决定「什么时候把 `DrawCommandList` 真正渲染掉」，并**由渲染方法直接返回产出图像**（`Task<IImage>`）。

- **Skia 实现**（`DefaultSkiaDrawingManagerImpl`）：raster `SKSurface`，结果交 `SkiaImage`（`SKImage` 快照）。
- **OpenGL 实现**（`DefaultOpenGLRenderManagerImpl`）：FBO + 颜色纹理，结果交 `DefaultOpenGLTexture`（**零拷贝，不经 `glReadPixels`**）。
- 两个实现共用完全相同的公共 API、`autoDispose`/present 语义与「每次渲染产出一张独立图像、所有权归调用方」的模型；
  差异集中在执行位置与图像删除语义（见 §5 与 §6），且必须在文档中明示。

### 1.2 非目标（本期不做）

1. **不接入编辑器/波形的渲染循环**（把编辑器内容渲染到离屏目标是后续接入项；命令列表由调用方提供）。
2. **不做像素回读与编码**（PNG/JPEG 导出、`ReadPixels`）——已确认暂不考虑。
3. **不做 GPU 加速的 Skia 离屏**（Skia 侧只做 raster）。
4. **不改 UI 渲染路径的现有行为**（Skia 的 `PaintSurface` 订阅/`LimitFPS` 闸门/present 语义；GL 的 `GLWpfControl.Render` 流程）。
5. **不动 Avalonia 工程**（`Avalonia/` 下的同名实现不在本次范围内）。
6. **不为 GL 引入第二 GL 上下文**（理由见 GL-1）。

## 2. 仓库现状与硬约束

### 2.1 Skia 侧事实（WPF）

| 位置 | 事实 | 与本设计的关系 |
| --- | --- | --- |
| `Kernel/Graphics/IRenderManagerImpl.cs:13-75` | 管理接口（`InitializeRenderControl` / `GetOrCreateRenderContext` / `GetRenderContexts` / `RemoveRenderContext` / `LoadImageFromStream` / `CreateDrawCommandListBuilder` / `Post` / `Swap` / `Present`）；**无默认接口成员** | 新入口 `CreateOffscreenToImage` 加在这里；两个实现都要实现 |
| `Kernel/Graphics/IRenderContext.cs:8-23` | `OnRender`、`LimitFPS`、`PerfomenceMonitor`、`Name`、`PostDrawCommandList`、`StartRendering` / `StopRendering` | 离屏**不**复用该接口（§11 决策 3）；重放内部需要 `IRenderContext` 实例 → 适配器 |
| `Kernel/Graphics/Skia/ISkiaRenderContext.cs:8-11` | **已存在** `ISkiaRenderContext : IRenderContext { SKCanvas Canvas { get; } }` | D1 直接复用，无需新接口 |
| `Kernel/Graphics/Skia/DefaultSkiaDrawingManagerImpl.cs:32,235-271,287-300` | Skia 侧自带 front/back 槽位；present 取 front 后置空（一次性），`AutoDispose` 时释放；`PresentCommands` **每次 present 新建 `SkiaDrawCommandListReplay`**（`using`） | 离屏复用 `PresentCommands`（改 internal），不进槽位 |
| `Kernel/Graphics/Skia/DefaultSkiaRenderContext.cs:14-114` | `Canvas => renderControl.CurrentRenderSurface?.Canvas`（只在 paint 期间有效）；`StartRendering` 订阅 `PaintSurface`；`LimitFPS` 闸门在 `TryUpdateRenderTime` | 离屏必须自持 surface canvas |
| `Kernel/Graphics/Skia/SkiaDrawCommandListReplay.cs:40-65,88-93` | 构造即按 `CleanColor` 清屏、建 8 个 backend 绘制对象；`Dispose` 释放 line/texture/string 三件 | 每次渲染 `using`，离屏上下文不持有 |
| `Kernel/Graphics/Skia/Drawing/CommonSkiaDrawingBase.cs:18-35` | `OnBegin` 取 canvas → `canvas.SetMatrix(mvp * flip * translate(V/2) * scale(RenderScale))`（**覆盖**矩阵） | 离屏**不得**再缩放 canvas（D7） |
| `Kernel/Graphics/Skia/Drawing/**`（10 处） | 全部 `((DefaultSkiaRenderContext)target.RenderContext).Canvas` | D1 改为 `ISkiaRenderContext`（清单见 §8.2） |
| `Kernel/Graphics/Skia/SkiaUtility.cs:9-13` | DEBUG 下 `CheckSkiaRenderContext` 只接受 `DefaultSkiaRenderContext` | 放宽为 `ISkiaRenderContext` |
| `Kernel/Graphics/Skia/RenderControls/SkiaRenderControlBase.cs:45-71,90-107` | `CanvasSize`（逻辑）、`CurrentRenderSurface`；`CreateSize` 给出「逻辑尺寸 + DPI scale」 | 换算约定依据（D7） |
| `Modules/AudioPlayerToolViewer/ViewModels/...WaveformDrawing.cs:145-146,218,309` | `viewWidth/Height = ActualWidth/Height`（逻辑）、`renderScale = DpiScaleX/Y` | 视口/缩放约定样张 |
| `Kernel/Graphics/DrawCommands/DrawCommandList.cs:50-70` | `internal TryBeginPresent()/EndPresent()` + 状态机（present 期间 `Dispose()` 延迟） | 离屏同程序集可用 |
| `Kernel/Graphics/Skia/Base/SkiaImage.cs:10-30` | **public**：`SKImage Image`、`Width/Height`、`Dispose` 置空 | 结果类型；harness 可读像素 |
| `Kernel/Graphics/ITexture.cs:9-13` | `IImage` 只有 `TextureWrapT/S` | 需要宽高 → D12 |

### 2.2 OpenGL 侧事实（WPF）

| 位置 | 事实 | 与本设计的关系 |
| --- | --- | --- |
| `Kernel/Graphics/OpenGL/DefaultOpenGLRenderManagerImpl.cs:145-200` | `InitializeRenderControl`：`GLWpfControlSettings.ContextToUse = sharedContext`（静态）、`glView.Start(setting)`、`sharedContext ??= glView.Context` | **全应用共用一个 GL 上下文**，上下文只在控件回调里 current |
| 同上 `:203-207,210-216,277-280` | `CheckInitialization()`（DEBUG）、`LoadImageFromStream → new DefaultOpenGLTexture(bitmap)`、`CreateDrawCommandListBuilder` 用 `DefaultStringMeasure` | 离屏创建不依赖 `CheckInitialization`（见 §7.2） |
| 同上 `:239-256,283-318` | `RemoveRenderContext`；`Post/Swap` 走 `drawCommandListContextSlots`（`drawCommandListGate` 锁）；`PresentDrawCommandList` 要求 `context is DefaultOpenGLRenderContext` 并调用 `OpenGLDrawCommandListReplay.Present` | 离屏绕开该强类型判定，直接调静态 `Present` |
| `Kernel/Graphics/OpenGL/DefaultOpenGLRenderContext.cs:64-99,101-119` | `StartRendering` 订阅 `glView.Render`；`GlView_Render`：FPS 闸门 → `OnRender` → `SwapAndPresent`；`TryUpdateRenderTime` 与 Skia 同构 | **GL 的唯一可执行窗口**（context current）→ GL 采用 drain 模型（GL-2） |
| `Kernel/Graphics/OpenGL/OpenGLDrawCommandListReplay.cs:18-38` | **静态单例**：静态 drawings、静态矩阵栈、静态 `replayGate` 锁 | 进程内串行，天然不可跨上下文并行（GL-1） |
| 同上 `:40-52,95-116` | `Present(manager, IRenderContext, list)`：`lock(replayGate)` → `EnsureInitialized` → `Reset`（`GL.Viewport(0,0,ViewWidth*ScaleX,ViewHeight*ScaleY)` + 按 `CleanColor` 清 color/depth）→ 逐命令 | 离屏复用同一入口；viewport 尺寸公式与 D7 一致 |
| 同上 `:70-93,355-372` | `Dispose()` 释放静态 drawings；`ReplayDrawingContext` 持 `IRenderContext`（取 monitor） | 离屏需要自己的适配器上下文 |
| `Kernel/Graphics/OpenGL/Base/DefaultOpenGLTexture.cs:10-18,72-97,104-112` | `IImage` 实现：`_id`（GL 纹理名）、`Width/Height`、`Dispose → GL.DeleteTexture + InvalidateTexture`（**需 context current**） | 结果类型；需要延迟删除入口（GL-4） |
| `Kernel/Graphics/OpenGL/Base/OpenGLTextureBindingCache.cs:12-58` | 纹理单元绑定缓存（含 `InvalidateTexture` / `Reset`） | 离屏渲染前后需 `Reset()` 隔离状态（GL-6） |
| `Kernel/Graphics/OpenGL/Drawing/TextureDrawing/*`、`BeamDrawing/*` | 纹理路径硬转 `(DefaultOpenGLTexture)`（`DefaultTextureDrawing.cs:92`、`DefaultBatchTextureDrawing.cs:172`、`DefaultHighlightBatchTextureDrawing.cs:174`、`DefaultBeamDrawing.cs:82`，部分带 DEBUG 抛错） | **离屏结果必须是 `DefaultOpenGLTexture`**（GL-4） |
| `Kernel/Graphics/OpenGL/Drawing/StringDrawing/String/*` | `VertexArrayObject` / `BufferObject` / `Texture2DManager` 等按上下文创建的资源 | VAO 不跨上下文共享 → 第二上下文方案成本极高（GL-1） |
| GL 后端全量检索 | 无 `DepthTest` / `Scissor` / `CullFace` 使用；`Blend` 只在 `InitializeOpenGL` 设一次（`SrcAlpha/OneMinusSrcAlpha`） | 离屏 FBO **不需要 depth 附件**；混合状态可直接复用（GL-5/GL-6） |
| `Kernel/Graphics/OpenGL/GLUtility.cs:10-30` | DEBUG 错误检查工具 | 离屏 FBO 创建/附着时复用 |

### 2.3 四条硬约束

1. **Skia：canvas 来源写死控件上下文**（10 处强转），但 `ISkiaRenderContext` 已存在 → 改造是「换强转目标」。
2. **Skia：present 路径与控件绑定**（`PresentDrawCommandList` 要求控件上下文 + 走槽位）→ 离屏走 internal 的 `PresentCommands`。
3. **GL：只有一个 GL 上下文，且只在 `GLWpfControl.Render` 回调内 current**（`sharedContext` 单例 + 静态重放引擎 + VAO 不跨上下文共享）
   → GL 无法使用「专用渲染通道线程」，必须改为「队列 + UI tick 排空」（GL-2）。
4. **命令列表所有权语义已确立**：`TryBeginPresent/EndPresent` + `autoDispose` + 「present 期间 Dispose 延迟」，两个后端都必须沿用。

## 3. 接口草案（v1）

### 3.1 创建参数 `OffscreenRenderOptions`

```csharp
namespace OngekiFumenEditor.Kernel.Graphics;

/// <summary>离屏表面的创建参数。创建后由 IOffscreenRenderContext.Options 原样回读。</summary>
public sealed record OffscreenRenderOptions
{
    /// <summary>表面像素宽度（设备像素，必须 &gt; 0）。</summary>
    public required int Width { get; init; }

    /// <summary>表面像素高度（设备像素，必须 &gt; 0）。</summary>
    public required int Height { get; init; }

    /// <summary>像素格式，默认后端平台默认格式（Skia: PlatformColorType；GL: Rgba8）。</summary>
    public OffscreenPixelFormat PixelFormat { get; init; } = OffscreenPixelFormat.PlatformDefault;

    /// <summary>alpha 类型，默认预乘（Premul）。GL 无该概念，仅记录（见 GL-5）。</summary>
    public OffscreenAlphaType AlphaType { get; init; } = OffscreenAlphaType.Premul;

    /// <summary>颜色空间，默认 sRGB。GL 侧不做颜色管理，仅记录（见 GL-5）。</summary>
    public OffscreenColorSpace ColorSpace { get; init; } = OffscreenColorSpace.Srgb;

    /// <summary>
    /// 为 true 时，提交的命令列表必须满足 ViewWidth*RenderScaleX ≈ Width 且 ViewHeight*RenderScaleY ≈ Height，
    /// 否则抛 ArgumentException。默认 false（允许调用方用投影矩阵做分块/局部渲染）。
    /// </summary>
    public bool RequireMatchingViewport { get; init; }
}
```

枚举：

```csharp
public enum OffscreenPixelFormat { PlatformDefault, Rgba8888, Bgra8888, Rgb1010102, RgbaF16, RgbaF32, Gray8, Alpha8 }
public enum OffscreenAlphaType { Premul, Unpremul, Opaque }
public enum OffscreenColorSpace { Srgb, SrgbLinear, DisplayP3, Null }
```

跨后端映射（详见 D6）：

| 参数 | Skia | OpenGL |
| --- | --- | --- |
| `PlatformDefault` | `SKImageInfo.PlatformColorType` | `Rgba8`（`GL_RGBA8`） |
| `Rgba8888` / `Bgra8888` / `Rgb1010102` / `RgbaF16` / `RgbaF32` / `Gray8` | 同名 `SKColorType` | `Rgba8` / `Bgra8` / `Rgb10A2` / `Rgba16F` / `Rgba32F` / `R8` |
| `Alpha8` | `SKColorType.Alpha8` | 首版 `NotSupportedException`（旧式 internal format，缺少实际用途） |
| `AlphaType` | `SKAlphaType` | 不映射（GL 无 alpha type），仅记录到 `Options` |
| `ColorSpace` | `SKColorSpace`（`Null` → null） | 不映射、不做颜色管理（与屏幕 GL 路径一致），仅记录 |

### 3.2 离屏上下文（完全独立接口）

```csharp
namespace OngekiFumenEditor.Kernel.Graphics;

/// <summary>
/// 离屏渲染目标。创建时确定表面参数；调用方提交命令列表并等待渲染完成，产出图像即返回值。
/// 必须在用完后 Dispose；Dispose 后不得再提交渲染。
/// Dispose 会处理本上下文已提交的渲染（Skia：等待完成；GL：未开始的排队请求被取消），
/// 随后释放后端资源（范围见 D10 与 GL-7）；已返回给调用方的图像不受影响，仍由调用方自行释放。
/// </summary>
public interface IOffscreenRenderContext : IDisposable
{
    /// <summary>创建时使用的参数快照。</summary>
    OffscreenRenderOptions Options { get; }

    /// <summary>
    /// 提交命令列表，渲染并等待完成，返回本次渲染产出的图像。
    /// autoDispose 语义与 IRenderContext.PostDrawCommandList 一致：true = 渲染结束后由实现释放该列表。
    /// 返回的 IImage 所有权归调用方（见 D4）。
    /// Skia：渲染在 manager 的离屏通道线程上执行；GL：渲染在 GL 控件的渲染回调内执行（见 GL-2），
    /// 因此**禁止在 UI 线程上同步等待该 Task**（`.Result` / `.Wait()` 会死锁）。
    /// 渲染失败时 Task 以原异常结束，上下文仍可继续使用。
    /// </summary>
    Task<IImage> RenderToImageAsync(DrawCommandList drawCommandList, bool autoDispose = true);

    bool IsDisposed { get; }
}
```

不提供 `Name` / `PerfomenceMonitor` / `PostDrawCommandList` / `StartRendering` / `StopRendering` / `OnRender` / `LimitFPS`（§11 决策 3）：
离屏没有渲染循环、没有 FPS 闸门、也没有「投递后由别人 present」的时机；监视器如需暴露，放在各后端专属实现类上（D9）。

**方法命名**（§11 决策 5）：`PostDrawCommandListAndWaitForRendered` → **`RenderToImageAsync`**（理由见 §11）。

### 3.3 manager 入口

```csharp
public interface IRenderManagerImpl
{
    // ... 现有成员 ...

    /// <summary>创建离屏渲染目标。返回的上下文由调用方负责 Dispose。</summary>
    IOffscreenRenderContext CreateOffscreenToImage(OffscreenRenderOptions options);

    /// <summary>便捷重载：平台默认像素格式 + sRGB + Premul。</summary>
    IOffscreenRenderContext CreateOffscreenToImage(int width, int height);

    /// <summary>
    /// 关闭后台资源（Skia 的离屏通道线程、两个后端的在途/排队请求、GL 的延迟删除队列）。
    /// 由 AppBootstrapper.OnExit 调用（与 ISchedulerManager.Term() 同位）。
    /// </summary>
    Task Term();
}
```

- 两个实现都必须实现 `CreateOffscreenToImage` 与 `Term`（GL 不再只是 stub，但 **GL 的 `CreateOffscreenToImage` 在没有任何活动 GL 控件时抛 `InvalidOperationException`**，见 GL-2/P16）。
- `RemoveRenderContext` 保持「控件上下文」语义；离屏上下文走独立注销路径（D10）。

## 4. 运行流程

### 4.1 Skia：专用离屏通道线程 + FIFO 队列

```text
调用方                                    离屏上下文                 离屏通道线程（manager 唯一线程）      manager
  |  CreateOffscreenToImage(opts)  ------->  创建 SKSurface(ImageInfo) -----[懒启动通道]------------->  登记上下文
  |  using builder = impl.CreateDrawCommandListBuilder()
  |  builder.SetCleanColor(...) / SetViewport(W,H[, scaleX,scaleY]) / DrawXXX(...)
  |  using list = builder.GetDrawCommandList()
  |  var image = await ctx.RenderToImageAsync(list, autoDispose: true)
  |                                          ---- 入队(RenderRequest + TCS) ---->
  |                                             TryBeginPresent(list)（失败语义见 P5）
  |                                             manager.PresentCommands(this, list, surface.Canvas)
  |                                             snapshot = surface.Snapshot() -> SkiaImage
  |                                             EndPresent()；autoDispose -> list.Dispose()
  |                                             TCS.SetResult(image) / SetException
  |  <--------------------- await 返回 IImage（所有权归调用方）-------------------+
  |  ctx.Dispose()     // 等待已提交渲染 -> 释放 surface -> manager 注销
```

### 4.2 OpenGL：共享队列 + GL 控件 tick 内排空（drain）

```text
调用方                              离屏上下文(GLOffscreen)        GL 控件 Render 回调（UI 线程, context current）      manager
  |  CreateOffscreenToImage(opts) -->  记录 Options / 登记         （要求已有 StartRendering 的 GL 上下文）
  |  var image = await ctx.RenderToImageAsync(list)   --入队-->  共享离屏队列
  |                                                             GlView_Render: [FPS 闸门] -> OnRender -> present 屏幕帧
  |                                                             -> PumpOffscreenRenders():
  |                                                                 取队列项；懒建 FBO（每上下文一个）
  |                                                                 新颜色纹理 + FramebufferTexture2D
  |                                                                 OpenGLDrawCommandListReplay.Present(manager, adapter, list)
  |                                                                 解绑纹理 -> new DefaultOpenGLTexture(id, w, h, 延迟删除)
  |                                                                 恢复屏幕状态（FBO 0 / viewport / 绑定缓存）
  |                                                                 TCS.SetResult(image)
  |  <------------------- await 返回 IImage（纹理，零拷贝）-----------------------+
  |  调用方 Dispose 图像 --(延迟删除)--> 队列 -> 下一次 drain 时 GL.DeleteTexture
```

### 4.3 生命周期状态（两后端一致）

| 状态 | 允许操作 | 说明 |
| --- | --- | --- |
| `Created` | 提交渲染 | 后端资源按需惰性创建（Skia 创建时分配 surface；GL 首次渲染时建 FBO/纹理） |
| `Rendering` | 继续提交（排队）、Dispose | 同一上下文的提交按顺序执行 |
| `Disposed` | 无（提交抛 `ObjectDisposedException`） | 后端资源已释放/排队删除；已交出的图像不受影响 |

### 4.4 帧状态约定（两后端一致）

- 清屏：`FrameState.CleanColor` 非 null → 由各自的 replay 执行清屏（Skia 在 replay 构造函数；GL 在 `Reset`：`ClearColor` + `Clear(Color|Depth)`）；
  null → 不清屏，保留目标上一次内容（增量/覆盖式渲染）。
- 缩放：**两个后端都不缩放 canvas / 不额外设置投影缩放**——Skia 侧由 `CommonSkiaDrawingBase.OnBegin` 的 `SetMatrix(... scale(RenderScale))` 承担；
  GL 侧由 `Reset` 的 `GL.Viewport(0, 0, ViewWidth*ScaleX, ViewHeight*ScaleY)` 承担。离屏只要求目标尺寸满足 D7 的换算。
- 视口一致性：默认宽松（不校验），`RequireMatchingViewport = true` 时严格校验（两个后端同一公式）。

## 5. 关键设计点与推荐决策

### D1 Canvas 提供者（Skia）/ 目标绑定（GL）

**Skia**：10 处绘制 `((DefaultSkiaRenderContext)target.RenderContext).Canvas` → 改为 `((ISkiaRenderContext)target.RenderContext).Canvas`，
取到 null 时跳过绘制；`SkiaUtility.CheckSkiaRenderContext` 放宽为 `ISkiaRenderContext`；新增的 Skia 离屏上下文实现 `ISkiaRenderContext`。
**GL**：**不需要任何 canvas 抽象**——重放入口是静态的 `OpenGLDrawCommandListReplay.Present(manager, IRenderContext, list)`，
绘制侧只用 GL 当前状态；离屏只需保证调用时「FBO 已绑定 + context current」，因此 GL 侧改动最小（见 GL-3）。

### D2 接口层次（已定：完全独立 + 内部适配器）

`IOffscreenRenderContext` 独立于 `IRenderContext`。两个后端各自需要一个内部适配器实现 `IRenderContext`（Skia 侧还要实现 `ISkiaRenderContext`）：
`Name` 自动命名、`PerfomenceMonitor` 默认 `DummyPerformenceMonitor.Instance`、`OnRender` 永不触发、`Start/StopRendering`/`PostDrawCommandList` 抛 `NotSupportedException`（不可达，仅防御）。
`LimitFPS` 对离屏无意义（Skia 适配器仍需满足接口成员，恒为 -1）。

### D3 执行模型

- **Skia：manager 专用离屏通道线程 + FIFO 队列**（§11 决策 2），后台线程，`Term()` 关闭；
  `RenderRequest = (SkiaOffscreenRenderContext, DrawCommandList, bool AutoDispose, TaskCompletionSource<IImage>)`；复用 manager 的 `PresentCommands`（改 internal）。
- **GL：不做专用线程**，改为「共享离屏队列 + GL 控件 `Render` 回调内排空」（GL-1/GL-2）。这是两后端唯一的执行模型差异，
  对 API 使用者只体现在两点：GL 禁止 UI 线程同步等待；GL 的推进依赖存在活动 GL 控件。
- 队列容量/背压（P4）、关闭钩子（P2/P10）对两者统一。

### D4 产出图像的语义与所有权（已定：每次渲染一张独立图像，所有权归调用方）

- **Skia**：`surface.Snapshot()` → 新 `SkiaImage`（`SKImage` 快照，与 surface COW 解耦，surface 释放后仍有效）。
- **GL**：**每次渲染新建一张颜色纹理**（不复用、不池化），FBO 仅作为一次性渲染容器，渲染结束解绑纹理并把纹理包成 `DefaultOpenGLTexture` 交给调用方。
  与 Skia 的差异：GL 图像是 GPU 资源，`Dispose` 必须在 context current 处执行 → 走延迟删除（GL-4）。
- 共同契约：调用方负责 `Dispose`；调用方不得把图像提交进「可能继续被重放」的列表后再释放（D4-6 的原始表述保留）。

### D5 Present 语义（已定：直接用命令列表状态机，不走 manager 槽位）

```csharp
if (list.IsDisposed) { /* 无法产出图像；回报语义见 P5 */ }
if (!list.TryBeginPresent()) { /* 正被别处 present 或已进入 dispose 流程；回报语义见 P5 */ }
try { /* 后端渲染：Skia = PresentCommands + Snapshot；GL = FBO 内 Present + 交纹理 */ }
finally { list.EndPresent(); if (autoDispose) list.Dispose(); }
```

`EndPresent()` 已处理「present 期间调用方 `Dispose()`」的延迟释放（`DrawCommands/DrawCommandList.cs:59-70`）。
与 UI 路径的差异只有两点：不进槽位、不保留 front。

### D6 参数类型：中性枚举 + 后端映射

公共参数不把 `SKColorType` / GL enum 写进 `Kernel.Graphics` 契约；映射表见 §3.1。
**GL 的两个「记录性参数」必须在 XML 注释与文档中明说**：`AlphaType`（GL 无此概念）与 `ColorSpace`（GL 侧不做颜色管理，与屏幕路径一致，见 GL-5）。
需要「精确到 `SKImageInfo` / GL internal format」的场景走各后端专属类型，不进通用接口。

### D7 尺寸/缩放约定

- 换算（两后端同一公式）：`target.Width == ViewWidth * RenderScaleX`、`target.Height == ViewHeight * RenderScaleY`
  （例：逻辑 800×600、DPI 1.5 → 1200×900，`SetViewport(800, 600, 1.5f, 1.5f)`）。
- 离屏**不做额外缩放**（Skia 的 `SetMatrix` / GL 的 `GL.Viewport` 已包含 `RenderScale`）。
- 默认不校验（分块/局部放大是合法用法）；`RequireMatchingViewport = true` 时校验，误差 > 0.5 抛 `ArgumentException`；
  DEBUG 下不校验也记录一次不匹配日志。

### D8 失败语义

| 情况 | 行为 |
| --- | --- |
| `Width/Height <= 0` | 创建时抛 `ArgumentOutOfRangeException` |
| 格式/alpha/颜色空间组合非法 | Skia：创建时抛 `ArgumentException`（包装 Skia 异常）；GL：创建时抛 `NotSupportedException`（如 `Alpha8`）或首个渲染时抛 `InvalidOperationException`（FBO 不完整，带 `CheckFramebufferStatus` 结果） |
| Skia `SKSurface.Create` 返回 null | 创建时抛 `InvalidOperationException`，消息含 `Width/Height/PixelFormat` |
| GL 无活动 GL 控件 | `CreateOffscreenToImage` 抛 `InvalidOperationException`（P16） |
| 渲染时抛异常（如未知命令 `NotSupportedException`） | 该次 `Task` 以原异常结束；`autoDispose: true` 的列表仍在 `finally` 中释放；上下文可继续使用；**失败帧的目标内容未定义** |
| 上下文已 `Dispose` 后提交 | 抛 `ObjectDisposedException`（同步抛，不排队） |
| 列表已 disposed / 正被别处 present | 回报语义见 P5（推荐：`ObjectDisposedException` / `InvalidOperationException`） |
| 已提交的请求遇到 `Dispose` | Skia：照常执行完；GL：未开始的被取消（GL-7） |
| `Term()` 时尚未执行的排队请求 | `Task` 以 `OperationCanceledException` 结束；`autoDispose: true` 的列表仍被释放 |
| 参数为 null | 抛 `ArgumentNullException` |

### D9 监视器与 `GetRenderContexts()`

- 离屏上下文默认 `DummyPerformenceMonitor.Instance`；需要统计时由各后端离屏实现类暴露可写属性（接口保持独立）。
- **离屏上下文不加入 `GetRenderContexts()`**（两个后端一致）：该 API 语义是「控件上下文快照」，混入离屏会让性能面板与生命周期语义混乱。

### D10 `Dispose()` 的释放范围（两后端共同原则 + 差异）

原则：**只释放「这个渲染目标自己造出来的东西」**。

| 项 | Skia | OpenGL |
| --- | --- | --- |
| 拥有的后端资源 | raster `SKSurface` | 每个离屏上下文一个 FBO + 每次渲染一张颜色纹理（纹理随图像交给调用方） |
| 释放方式 | `surface.Dispose()`（即时） | FBO 与未交出/已归还的纹理走**延迟删除队列**，在下一次 drain（context current）执行（GL-7） |
| 已提交渲染 | 等完成、不取消 | 未开始的取消，在途（正在 drain 中）自然完成 |
| manager 侧登记项 | 内部注销 | 内部注销 |
| 不释放 | 已返回图像、`autoDispose: false` 的列表、命令内引用的纹理/字体、进程级字体缓存、通道线程（`Term()` 才停） | 同左；额外：**不**释放调用方已交出的纹理（由调用方 `Dispose` → 延迟删除） |
| `Dispose` 后 | `Options` 仍可读、`IsDisposed == true`、提交抛 `ObjectDisposedException`、已返回图像仍有效、幂等 | 同左 |

内存视角（Skia）：raster 快照与 surface 是 COW 关系，仍被持有的旧快照会在表面继续绘制时被复制 → 持有 N 张旧图最坏 ≈ `(N+1) × W×H×bytes`。
内存视角（GL）：每张未释放图像 = 一份 GPU 纹理内存 `W×H×bytes`；GPU 显存比系统内存更敏感，**旧图更要尽快释放**。

### D11 平台与线程约束

- **Skia**: raster，与 WPF 后端（CPU/DirectX/OpenGL 渲染控件）无关，不依赖控件 surface。
- **GL**：依赖 `sharedContext` 的 current 状态（只在控件回调内成立）→ GL 的离屏渲染发生在 UI 线程（GL-2）；GL 调用（含纹理删除）不得在无 current 上下文时执行（GL-4）。
- **两个后端共同的线程纪律**：重放路径不得触碰 `DispatcherObject`（已核对：Skia 绘制代码与 GL 绘制代码都不依赖 WPF 类型；
  `using System.Windows.*` 为历史残留）。
- 不设内存/显存硬上限，靠后端创建失败抛错（Skia `SKSurface.Create` 返回 null；GL `CheckFramebufferStatus` 不完整）。

### D12 `IImage` 补 `Width/Height`

**推荐补**：两个后端的结果类型其实**都已有** `Width`/`Height`（`SkiaImage.cs:19-20`、`DefaultOpenGLTexture.cs:17-18`），只是 `IImage` 接口没暴露。
消费方（写盘、对齐、尺寸换算、GL 纹理尺寸）必须能从接口拿到尺寸 → 补接口成员，实现类零改动。

## 6. OpenGL 实现变体设计

### GL-1 为什么 GL 不能用「专用渲染通道线程 + 第二 GL 上下文」（已核对，排除）

1. **全应用只有一个 GL 上下文**：`sharedContext` 静态字段由首个控件创建，后续控件以 `ContextToUse` 复用它
   （`DefaultOpenGLRenderManagerImpl.cs:172,185`）；上下文只在 `GLWpfControl.Render` 回调内 current。
2. **VAO/FBO 不跨共享上下文共享**（GL 规范），而 GL 后端的绘制对象把 VAO/VBO/纹理缓存成实例/静态状态
   （`Drawing/**`、`StringDrawing/String/{VertexArrayObject,BufferObject,Texture2DManager}.cs`），GL 重放还是**静态单例**
   （`OpenGLDrawCommandListReplay.cs:18-38`）。要支持第二上下文，等于把整个 GL 后端改造成「按上下文实例化」。
3. **代价**：隐藏窗口 + `WGL_ARB_create_context` 共享组 + 线程 `MakeCurrent` + 资源按上下文隔离 + 双重状态机（屏幕/离屏）——
   对一个「生成图片」的功能而言收益不抵成本。

**结论**：GL 侧采用「共享队列 + UI tick 排空」（GL-2），把渲染安排在 GL 上下文已经 current 的时刻。

### GL-2 执行模型：共享队列 + GL 控件 tick 内排空

- **共享队列**：manager 持有 `ConcurrentQueue<OpenGLOffscreenRequest>`（`OpenGLOffscreenRequest = (ctx, list, autoDispose, TCS)`），
  与 `drawCommandListGate` 同级加锁；所有 GL 控件的 tick 都能排空它（FIFO）。
- **排空点**：`DefaultOpenGLRenderContext.GlView_Render` 的**末尾**（`SwapAndPresentDrawCommandList()` 之后），
  由 context 调用 `manager.PumpOffscreenRenders()`。理由：
  ① 屏幕帧优先，离屏工作不挤占它的构建阶段；
  ② 放在末尾时，屏幕帧已经用完了它需要的 GL 状态，离屏改动的状态不会污染本帧；
  ③ `TryUpdateRenderTime` 早退（限帧丢弃）时**仍需排空**——因此 `PumpOffscreenRenders()` 要放在 FPS 闸门之前的分支之外
     （实现方式：`GlView_Render` 先在 `try/finally` 里保证 `PumpOffscreenRenders()` 一定执行，再走原有的闸门/present 逻辑）。
- **无活动控件时**：`CreateOffscreenToImage` 时若没有任何已 `StartRendering` 的 GL 上下文 → 抛 `InvalidOperationException`（P16）；
  运行中最后一个 GL 上下文 `StopRendering`/被 `RemoveRenderContext` 时，把队列中属于其登记上下文的请求以异常结束（避免永久挂起）。
- **可显式 pump**：`manager.PumpOffscreenRenders()` 设为 `internal`，允许在「自有的 context-current 作用域」内（Claude：测试/CLI）手动推进。
- **禁止 UI 线程同步等待** `RenderToImageAsync`（`.Result` / `.Wait()` 会死锁）——写进接口 XML 注释（§3.2）。

### GL-3 渲染目标：FBO + 颜色纹理，结果直接交纹理（零拷贝）

每次渲染：

1. 取一个**新颜色纹理**（`GL.GenTextures` + `TexImage2D(internalFormat, W, H, ..., null)`，参数 Clamp + Linear）；
2. 附着到该离屏上下文的 FBO（惰性创建一次，`FramebufferTexture2D(ColorAttachment0, Texture2D, texId, 0)`），
   `CheckFramebufferStatus` 校验；**无需 depth 附件**（GL 后端不使用深度测试，§2.2）；
3. `OpenGLDrawCommandListReplay.Present(manager, adapter, list)`——`Reset` 会设置 `GL.Viewport` 并按 `CleanColor` 清屏；
4. 完成后 `FramebufferTexture2D(..., 0)` 解绑，把纹理包成 `DefaultOpenGLTexture` 交给调用方；
5. 恢复屏幕状态（GL-6）。

**为什么每次新建纹理**：与 D4「每次渲染产出一张独立图像」一致；若复用同一纹理，先前交出的图像会被后续渲染覆盖（活视图语义，已明确否决）。

### GL-4 图像类型与删除语义（必须解决的硬点）

- GL 纹理绘制路径**硬转 `(DefaultOpenGLTexture)`**（部分带 DEBUG 抛错），所以离屏结果必须是 `DefaultOpenGLTexture` 本身；
- `DefaultOpenGLTexture` 目前只有「从 `Bitmap` 上传」的构造 + `Dispose()` 立即 `GL.DeleteTexture`（`DefaultOpenGLTexture.cs:104-112`，需要 current context）；
- **改动**：增加 internal 构造入口承载「外部纹理 + 尺寸 + 释放委托」，例如

```csharp
internal DefaultOpenGLTexture(int textureId, int width, int height, Action<int> releaseTexture, string name = "OffscreenTexture")
// Dispose(): 若 releaseTexture 非空则调用它（而不是直接 GL.DeleteTexture），并把 _id 置空
```

- **延迟删除队列**：manager 维护 `pendingTextureDeletes`（纹理 id 列表）。所有 GL 对象删除（离屏图像的 `Dispose`、FBO 释放、未交出纹理）
  都只入队；下一次 drain 在 context current 下执行 `GL.DeleteTexture` + `OpenGLTextureBindingCache.InvalidateTexture`。
  这样调用方可以在任意线程 `Dispose()` 图像，而不会触发「无 current context 的 GL 调用」。
- **例外**：若调用方 `Dispose` 时恰好在 context current 的作用域（不强制、不可探测），仍走队列（简单一致优先）。
- `Term()` / GL 上下文销毁时：清空队列并统一删除（`OpenGLDrawCommandListReplay.Dispose()` 已有静态资源释放路径，可挂在此处或 manager 的 `Term`）。

### GL-5 参数映射与 GL 语义差异

| 参数 | 处理 |
| --- | --- |
| `PixelFormat` | 见 §3.1 映射表；`Alpha8` → `NotSupportedException`；`Bgra8` 需要驱动支持（core profile 4.5 下通常可用；不支持时 `CheckFramebufferStatus`/错误检查会暴露，返回明确异常） |
| `AlphaType` | **不映射**（GL 没有 alpha type 概念）：仅记录在 `Options`，并在文档/注释里说明它不改变 GL 输出 |
| `ColorSpace` | **不映射、不做颜色管理**：GL 屏幕路径同样没有 sRGB 管理，因此离屏保持同一观感。不启用 `GL_FRAMEBUFFER_SRGB`、不使用 `SRGB8_ALPHA8`（P17 备选方案） |
| `RequireMatchingViewport` | 与 Skia 同一公式与默认值 |

### GL-6 状态隔离与恢复

离屏渲染在离屏 FBO 内进行，会改动：当前 FBO 绑定、viewport、纹理绑定缓存（着色器采样器）、可能的 program/VAO（各绘制对象自己绑定）。

drain 结束后必须恢复（因为下一 tick 的屏幕帧仍假设自己接着上一帧的状态）：

1. `GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0)`；
2. `GL.Viewport(0, 0, 控件像素宽, 控件像素高)`（控件尺寸从触发 drain 的 GL 控件取，DPI 换算与 `CreateSize` 一致）；
3. `OpenGLTextureBindingCache.Reset()`（避免缓存里记录的「已绑定纹理」与实际绑定不一致）；
4. blend 状态无需恢复（`SrcAlpha/OneMinusSrcAlpha` 全局仅设置一次，离屏不改）；
5. depth/scissor/cull 无需恢复（GL 后端不使用）。

`replayGate` 是静态锁：离屏 `Present` 与屏幕 `Present` 在同一线程顺序执行、不会嵌套（`PumpOffscreenRenders` 不在 `Present` 内部被调用），无死锁风险。

### GL-7 `Dispose()`（GL 变体）

1. 置 `Disposed`，拒绝新提交；
2. **取消**尚未开始的排队请求（`Task` 以 `ObjectDisposedException` 结束；`autoDispose: true` 的列表在此释放）；
   ——与 Skia 的「等完成」不同，理由：GL 的推进依赖 UI tick，不能假设它会继续 tick，等完成可能永久挂起；
3. 在途请求（正好在执行中的那一个）自然完成，其结果图像照常交给调用方；
4. FBO 与尚未交出的纹理入延迟删除队列；manager 注销该上下文；
5. 幂等；`Options` 仍可读。

### GL-8 与 Skia 变体的差异汇总

| 维度 | Skia | OpenGL |
| --- | --- | --- |
| 执行位置 | manager 专用后台线程 | UI 线程（GL 控件 `Render` 回调内） |
| 推进依赖 | 无（线程自驱） | 存在正在 `StartRendering` 的 GL 控件 |
| `CreateOffscreenToImage` 前置条件 | 无（raster 不需要初始化） | 至少一个活动 GL 控件，否则抛 |
| 产出图像 | `SkiaImage`（CPU/COW 快照） | `DefaultOpenGLTexture`（GPU 纹理，零拷贝） |
| 图像 `Dispose` | 随时、任意线程 | 随时、任意线程（内部走延迟删除队列） |
| `Dispose` 与在途请求 | 等完成、不取消 | 未开始的取消，在途完成 |
| 后端资源释放 | 即时（`SKSurface.Dispose`） | 延迟到下一次 drain |
| 参数语义 | 全部生效（含颜色空间） | `AlphaType`/`ColorSpace` 仅记录 |

## 7. 实施前待决策清单

### 7.1 需要拍板（影响公共 API 或对外行为）

| # | 问题 | 选项 | 推荐 | 影响 |
| --- | --- | --- | --- | --- |
| P1 | manager 侧命名 | ① 保持 `CreateOffscreenToImage` / `IOffscreenRenderContext` ② 另立命名族 | ① | §3.2、§3.3 |
| P2 | 是否给 `IRenderManagerImpl` 加 `Term()` | ① 加（`AppBootstrapper.OnExit` 调用） ② 不做，各实现自管理 | ① | §3.3、D3、§8.2 |
| P3 | Skia 通道线程生命周期 | ① 懒启动、活到 `Term()`/进程退出 ② 最后一个上下文释放即回收（引用计数） | ① | D3、D10 |
| P4 | 队列容量与背压 | ① 有界（容量如 8）+ 提交端 `await` 写入 ② 无界 | ① | D3、D8 |
| P5 | 提交被跳过时如何回报 | ① 抛异常（已 disposed → `ObjectDisposedException`；正被 present → `InvalidOperationException`） ② 返回 `null` ③ 返回空白图 | ① | D5、D8 |
| P6 | 是否补 `IImage.Width/Height`（D12） | ① 补 ② 不补，消费方强转 | ① | D12、§8.2 |
| P7 | `RenderToImageAsync` 是否加重载 `CancellationToken` | ① 首版不加 ② 加（只能取消未开始的排队请求） | ① | §3.2、D8 |
| P8 | 是否要「投递但不等待」入口 | ① 不提供 ② 提供 | ① | §3.2 |
| P9 | 是否暴露表面属性选项（Skia `SKSurfaceProps` / GL 采样数 MSAA） | ① 首版不给 ② 暴露（GL 的 MSAA 需要额外 resolve，成本不小） | ① | §3.1 |
| P10 | `Term()` 等待在途渲染是否设上限 | ① 无上限 ② 上限 N 秒（默认 5s） | ② | D3 |
| P11 | Skia 离屏重放是否按上下文缓存 replay | ① 与 UI 路径一致：每次渲染新建 ② 缓存 + 帧重置（Avalonia 的 PERF-RND-017 做法） | ① | D3、D10 |
| **P14** | GL 执行模型 | ① UI tick 内排空 + FBO 纹理直交（本文推荐） ② 第二共享 GL 上下文 + 专用线程（需 GL 后端按上下文实例化） ③ GL 管理器内部委托 Skia raster 出图（实现最省，但用 Skia 渲染，与屏幕 GL 观感可能不一致） | ① | GL-1、GL-2、GL-3、D3 |
| **P15** | GL 图像删除语义 | ① 增加 internal 构造 + 延迟删除队列（调用方可任意线程 Dispose） ② 要求调用方在 context current 时 Dispose（把 GL 纪律推给调用方） ③ 立即 `GL.DeleteTexture`（无 current 上下文时会崩） | ① | GL-4、GL-7 |
| **P16** | GL 无活动控件时的行为 | ① 创建即抛 + 最后一个上下文停止时结束挂起请求 ② 创建成功但永久挂起等待下次 tick | ① | GL-2、D8 |
| **P17** | GL 的 `ColorSpace` 处理 | ① 记录性参数、不做颜色管理（与屏幕 GL 一致） ② 启用 `SRGB8_ALPHA8` + `GL_FRAMEBUFFER_SRGB`（会改变混合与观感） | ① | GL-5、D6 |
| **P18** | GL 验证方式 | ① harness 里隐藏窗口 + 真 `GLWpfControl` 强制 tick，读回像素断言 ② 仅真机手工验证（切 OpenGL 后端 + 导出/贴回） ③ 不验证 GL（仅编译） | 先 ② 保底，条件允许再补 ① | §9 |
| ~~P12~~ | ~~OpenGL 只做 stub~~ | **作废**：GL 要实现完整离屏（本轮决定） | — | — |
| ~~P13~~ | 验证载体 | 见 §9：Skia 用 `OngekiFumenEditor.Benchmark` 宿主（含 `BenchmarkRuntime.EnsureInitialized()`）；GL 见 P18 | — | — |

### 7.2 文档已给推荐，默认按此实现（除非反对）

| 事项 | 默认做法 |
| --- | --- |
| 创建前置条件（Skia） | 不调用 `CheckInitialization()`，不要求 `InitializeRenderControl` / WPF 窗口 |
| 创建前置条件（GL） | 要求至少一个活动 GL 上下文（P16），不要求调用线程 current |
| 枚举映射 | 见 §3.1；不支持项抛 `NotSupportedException`（GL `Alpha8`） |
| `Options` 语义 | 记录请求值；实际生效值由后端实现类暴露（Skia 的 `SKImageInfo` 只读属性等） |
| surface/纹理分配时机 | Skia 创建时分配 surface；GL 首次渲染时建 FBO、每次渲染建纹理 |
| 运行时改尺寸/格式 | 不支持，`Dispose` 后重建 |
| 失败帧目标内容 | 未定义；要确定性结果就重新提交并自行清屏 |
| 同一列表提交到多个上下文 | 顺序允许；并发报错（同 P5-① 判定） |
| 列表重复提交 | 允许，每次渲染产出独立图像 |
| Skia canvas 不可用时 | drawing 跳过绘制；`CommonSkiaDrawingBase.OnBegin/OnEnd` 成对判空 |
| GL FBO depth 附件 | 不附加（GL 后端不使用深度测试） |
| GL 状态恢复 | 见 GL-6 的五步；实现时按屏幕路径假设逐项核对 |
| GL 纹理参数 | 新纹理默认 Clamp + Linear（与 `DefaultOpenGLTexture(Bitmap)` 一致） |
| `GetRenderContexts()` | 不包含离屏上下文 |
| 性能监视器 | 默认 `DummyPerformenceMonitor.Instance`，后端实现类可暴露属性 |
| 缓存/池化 | 首版都不做（Skia surface、GL 纹理均为一次性） |
| 初始像素 | Skia 依赖 raster 全零；GL 由 `CleanColor` 或未定义内容决定 → 文档明示「GL 未清屏时内容未定义」 |

### 7.3 实现细节，编码时决定（无需讨论）

内部类命名、队列数据结构、DEBUG 日志文案、`PresentCommands` 的可见性做法、GL 状态恢复的具体调用顺序、
`DefaultOpenGLTexture` 新构造的名字与参数名、harness 命名、`OpenGLOffscreenRequest` 的字段组织。

## 8. 实施拆解（文件级清单，待批准后执行）

### 8.1 新增（均在 `OngekiFumenEditor/` 下）

| 文件 | 内容 |
| --- | --- |
| `Kernel/Graphics/OffscreenRenderOptions.cs` | 参数类型 + 校验 |
| `Kernel/Graphics/OffscreenPixelFormat.cs` / `OffscreenAlphaType.cs` / `OffscreenColorSpace.cs` | 三个枚举 |
| `Kernel/Graphics/IOffscreenRenderContext.cs` | 离屏上下文接口（独立） |
| `Kernel/Graphics/Skia/SkiaOffscreenRenderContext.cs` | surface / 提交 / 快照产出 / Dispose；实现 `ISkiaRenderContext` |
| `Kernel/Graphics/Skia/OffscreenReplayContextAdapter.cs` | Skia 重放所需适配器（internal） |
| `Kernel/Graphics/Skia/OffscreenRenderLane.cs` | Skia 离屏通道线程 + FIFO 队列 + 关闭（internal） |
| `Kernel/Graphics/OpenGL/OpenGLOffscreenRenderContext.cs` | Options / 登记 / 提交 / Dispose（GL） |
| `Kernel/Graphics/OpenGL/OpenGLOffscreenRenderQueue.cs` | 共享队列 + `PumpOffscreenRenders()` + 延迟删除队列（internal） |
| `Kernel/Graphics/OpenGL/OffscreenReplayContextAdapter.cs` | GL 重放所需 `IRenderContext` 适配器（internal） |
| `OngekiFumenEditor.Benchmark/Benchmarks/OffscreenRenderContextBenchmarks.cs`（名称待定） | §9 的验证载体 |

### 8.2 修改

| 文件 | 改动 |
| --- | --- |
| `Kernel/Graphics/IRenderManagerImpl.cs` | `CreateOffscreenToImage` ×2、`Term()` |
| `Kernel/Graphics/Skia/DefaultSkiaDrawingManagerImpl.cs` | `CreateOffscreenToImage` / `Term` / `PresentCommands` 可见性 / 离屏登记与注销 |
| `Kernel/Graphics/OpenGL/DefaultOpenGLRenderManagerImpl.cs` | `CreateOffscreenToImage` / `Term` / `PumpOffscreenRenders` / 活动上下文登记与「无活动控件」校验 / 延迟删除队列接线 |
| `Kernel/Graphics/OpenGL/DefaultOpenGLRenderContext.cs` | `GlView_Render` 内加 drain（保证 FPS 闸门丢弃的 tick 也会 drain） |
| `Kernel/Graphics/OpenGL/Base/DefaultOpenGLTexture.cs` | 新增 internal 构造（外部纹理 + 尺寸 + 释放委托）；`Dispose` 走委托 |
| `Kernel/Graphics/ITexture.cs` | `IImage` 增加 `Width` / `Height`（P6） |
| `Kernel/Graphics/Skia/SkiaUtility.cs:9-13` | `CheckSkiaRenderContext` 放宽为 `ISkiaRenderContext` |
| `Kernel/Graphics/Skia/Drawing/CommonSkiaDrawingBase.cs:23` | 强转改 `ISkiaRenderContext` + null canvas 跳过（`OnEnd` 成对判空） |
| `Kernel/Graphics/Skia/Drawing/BeamDrawing/DefaultSkiaBeamDrawing.cs:69` | 同上 |
| `Kernel/Graphics/Skia/Drawing/CircleDrawing/DefaultSkiaCircleDrawing.cs:24` | 同上 |
| `Kernel/Graphics/Skia/Drawing/LineDrawing/DefaultSkiaLineDrawing.cs:48` | 同上 |
| `Kernel/Graphics/Skia/Drawing/LineDrawing/NewSkiaLineDrawing.cs:113` | 同上 |
| `Kernel/Graphics/Skia/Drawing/PolygonDrawing/DefaultSkiaPolygonDrawing.cs:30` | 同上 |
| `Kernel/Graphics/Skia/Drawing/StringDrawing/DefaultSkiaStringDrawing.cs:87` | 同上 |
| `Kernel/Graphics/Skia/Drawing/TextureDrawing/DefaultSkiaBatchTextureDrawing.cs:29` | 同上 |
| `Kernel/Graphics/Skia/Drawing/TextureDrawing/DefaultSkiaHighlightBatchTextureDrawing.cs:30` | 同上 |
| `Kernel/Graphics/Skia/Drawing/TextureDrawing/DefaultSkiaTextureDrawing.cs:32` | 同上 |
| `AppBootstrapper.cs:563-572` | `OnExit` 中遍历 `IoC.GetAll<IRenderManagerImpl>()` 调 `Term()`（或经 `IRenderManager` 门面） |

### 8.3 实施顺序（每步可独立编译）

1. D1（Skia 10 处 `ISkiaRenderContext`）+ D12（`IImage` 尺寸）：纯重构，先确认 UI 路径无回归。
2. `OffscreenRenderOptions` / 枚举 / `IOffscreenRenderContext` + `IRenderManagerImpl` 两个新成员（先用 `throw new NotSupportedException()` 占位以保证编译）。
3. Skia：`PresentCommands` 可见性 → 通道 + 适配器 + `SkiaOffscreenRenderContext` → manager 接线 + `Term`。**此时 Skia 侧功能闭合，可先验证。**
4. GL：`DefaultOpenGLTexture` 新构造 + 延迟删除队列 → `OpenGLOffscreenRenderContext` + 队列 + 适配器 → `GlView_Render` drain → manager 接线（含无活动控件校验）。
5. `AppBootstrapper.OnExit` 钩子 + OpenGL 侧 `Term`。
6. 验证（§9）+ 本文档状态更新。

## 9. 验证计划（WPF）

前提：**WPF 工程没有测试工程**；`OngekiFumenEditor.Benchmark`（`net10.0-windows`、`UseWPF`、引用主工程，`Infrastructure/BenchmarkRuntime.cs:19-44`）
可用 `new App(false)` + `new AppBootstrapper(false)` 在无窗口进程内激活 MEF/IoC，因此 Skia 侧可跑真实路径。

### 9.1 Skia（harness，读回像素断言）

| 用例 | 断言 |
| --- | --- |
| 基础渲染 | 64×64、`SetCleanColor(红)` + 实心线 → 像素取样符合预期 |
| 参数生效 | `RgbaF16 + Premul + SrgbLinear` → `SkiaImage.Image.Info` 的 `ColorType/AlphaType/ColorSpace` 与请求一致；`IImage.Width/Height` 匹配 |
| 初始/复用 | 新表面全透明黑；全红后再以 `CleanColor = null` 画小块 → 旧区域仍红 |
| 缩放 | 逻辑 64×64 + `RenderScale 2` → 目标 128×128，几何按 2 倍像素落位 |
| 顺序与串行 | 连续提交 3 次（不同清屏色）→ 内容与提交顺序一致；并发提交不交叉 |
| autoDispose | `true` → 完成后 `IsDisposed`；`false` → 不释放 |
| 无法 present | 已 disposed / 正被 present 的列表 → 按 P5 结论 |
| 失败传播 | 不支持的命令 → `await` 抛原异常；随后仍能正常渲染 |
| Dispose | 在途渲染时 Dispose → 正常完成；之后提交抛 `ObjectDisposedException`；幂等；`Options` 可读 |
| 图像寿命 | 渲染 → 取图 → Dispose 上下文 → 图像仍可绘制 |
| 隔离性/通道关闭/视口策略 | 同前文（`GetRenderContexts` 不含离屏；`Term()` 后通道退出；`RequireMatchingViewport` 行为） |

### 9.2 OpenGL（P18）

难度：`GLWpfControl` 需要可视树 + `Start()`，纯 headless 进程拿不到 current 上下文。方案：

1. **保底（推荐先做）**：真机手工验证——把 `ProgramSetting.Default.DefaultRenderManagerImplementName` 切到 `OpenGL`，
   用一段临时入口（或 CLI/调试菜单）跑：创建离屏上下文 → 渲染一帧 → 把结果纹理贴回屏幕/写盘，肉眼确认；
   同时检查日志中 `CheckFramebufferStatus` 与 GL 错误检查无异常。
2. **进阶（条件允许）**：harness 里创建隐藏 `Window` + `GLWpfControl`，`Start()` 后用 `DispatcherFrame` 推动若干 tick，
   调用离屏 API，再用 `GL.GetTexImage` / `ReadPixels` 读回纹理断言像素（用例同 §9.1 的表，但「表面复用」改为「每次渲染独立纹理」）。
3. 无论哪种方式，**必须有至少一次真机 GL 运行**（离屏与屏幕混合渲染、连续多次渲染、Dispose 后台线程触发），因为 GL 状态污染类缺陷只有真机能暴露。

## 10. 术语

- **离屏上下文**：`IOffscreenRenderContext`，固定尺寸/格式的渲染目标，不参与控件的 paint 周期。
- **产出图像**：一次成功渲染返回的 `IImage`（Skia：`SkiaImage` 快照；GL：`DefaultOpenGLTexture` 纹理），所有权归调用方。
- **Skia 渲染通道**：Skia manager 内唯一的后台线程 + FIFO 队列。
- **GL drain（排空）**：在 GL 控件 `Render` 回调（context current）内执行离屏队列的机制。

## 11. 已确认决策

| # | 决策 | 结论 | 理由 / 影响 |
| --- | --- | --- | --- |
| 1 | 产出图像形态与所有权 | 去掉 `RenderTargetImage` 属性，改为 `Task<IImage> RenderToImageAsync(...)` 直接返回图像；所有权归调用方 | 每次渲染与每张图一一对应，消除「活视图 / 旧代释放」竞态。影响：§3.2、§4、D4、D8、§9 |
| 2 | Skia 执行模型 | manager 专用渲染通道线程 + FIFO 队列（全局串行） | 顺序确定、可预测。影响：§4.1、D3、D10、§8 |
| 3 | 接口层次 | `IOffscreenRenderContext` 完全独立（`IDisposable`），不继承 `IRenderContext` | 离屏无渲染循环 / FPS 闸门 / 延后 present；内部重放所需上下文用 internal 适配器。影响：§3.2、D2、D5 |
| 4 | 首版范围 | raster（Skia）+ FBO 纹理（GL）；像素回读/编码与 GPU-Skia 不做 | 控制复杂度。影响：§1.2、D11 |
| 5 | 渲染方法命名 | `PostDrawCommandListAndWaitForRendered` → **`RenderToImageAsync`**（2026-09-24 已确认） | 原名描述实现步骤而非调用方语义；与 `CreateOffscreenToImage` 同族。影响：§1.1、§3.2、§4 |
| 6 | `Dispose()` 的释放范围 | 只释放自身造出的资源；不释放已返回图像、`autoDispose: false` 的列表、命令内引用资源、进程级缓存、渲染通道；不走 `RemoveRenderContext` | 释放边界与所有权一一对应。影响：§3.2、D8、D10、§9 |
| 7 | 目标工程 | **仅 WPF（`OngekiFumenEditor`）**；Avalonia 不在范围 | 事实、拆解、验证按 WPF 落点。影响：全文 |
| 8 | Skia canvas 抽象 | 复用 WPF 已有 `ISkiaRenderContext` | 10 处强转改目标即可。影响：D1、§8.2 |
| 9 | **两个后端都要支持离屏**（2026-09-24 确认） | Skia 用 raster surface；GL 用 FBO + 纹理 | 原「GL 只做 stub」作废。影响：§1.1、§6、§7.1、§8 |
| 10 | **GL 执行模型** | 共享队列 + GL 控件 `Render` 回调内排空（UI 线程、context current），不做第二 GL 上下文/专用线程 | 单上下文 + VAO 不共享 + 静态重放引擎使多上下文方案成本过高。影响：GL-1、GL-2、D3、D8 |
| 11 | **GL 产出图像** | 每次渲染新建颜色纹理，零拷贝交给调用方（`DefaultOpenGLTexture`）；删除走延迟删除队列 | 与「每次渲染独立图像」模型一致；避免调用方在无 current 上下文时触发 GL 调用。影响：GL-3、GL-4、D4、D10 |

（后续轮次的决策继续追加到本表；每条记录「结论 / 理由 / 影响范围」。）
