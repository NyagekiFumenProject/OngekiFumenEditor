# 离屏渲染上下文设计（`IRenderManagerImpl.CreateOffscreenToImage`）

> 文档性质：**已实施（首轮完成并通过验证）**；P1–P18 决策不变，实现假设修订见 §0，实现层发现见 §0.6
> 建立日期：2026-09-24
> 最后更新：2026-09-24（第 6 轮：实现完成；验证 harness 56 项检查全部通过；回填实现层发现 R12–R14）
> **目标工程：`OngekiFumenEditor`（WPF，`net10.0-windows`）——Avalonia 侧不在本次范围内**
> **后端范围：Skia 与 OpenGL 两个 `IRenderManagerImpl` 实现都要支持**
> 目标命名空间：`OngekiFumenEditor.Kernel.Graphics` / `...Kernel.Graphics.Skia` / `...Kernel.Graphics.OpenGL`
> 来源需求：`docs/DrawCommandList plan.md`「后续规划 2. 实现离屏渲染」
> 依赖版本（实测）：SkiaSharp 3.119.2、OpenTK 4.9.4、OpenTK.GLWpfControl 4.3.6（`GLWpfControl` 行为与上游 master 一致）

## 0. 第 5 轮可行性复核结论（实施门禁）

复核方法：源码逐行核对 + 依赖包元数据 + 独立探针工程（临时目录，未改动本仓库任何文件）：
`DrawCommandList` 状态机移植复现、SkiaSharp 3.119.2 格式/快照实验、原生 GL 4.5 实验、真实 WPF `GLWpfControl` 窗口实验、WPF `Application.Exit` 异步完成实验。
探针工位（可重跑）：`%TEMP%\ongeki-offscreen-review-LRxM6w`（`Probe.csproj` + `Program.cs`）。入口：`lifecycle` / `skia` / `gl` / `wpfgl` / `exit` / `glflip` / `glrestore` / `glrestore2` / `glshared` / `glattrib` / `glattrib2` / `exitpath <close|shutdown>`；`glflip` 会写出三张对比 PNG。

### 0.1 实测证据（可复现）

| 实验 | 结果 | 影响 |
| --- | --- | --- |
| `DrawCommandList` 状态机 | `TryBeginPresent=True`；第一次 `Dispose`（present 中）→ `IsDisposed=False, Count=1`；**第二次 `Dispose` → `IsDisposed=True, Count=0`，随后 `EndPresent()`** | 设计里「EndPresent + finally 再 Dispose」的伪代码会**提前清空命令**；且状态机无并发保护 |
| SkiaSharp 3.119.2 surface 创建 | `Rgba8888/Bgra8888/Rgba1010102/RgbaF16/RgbaF32/Alpha8 × {Premul,Unpremul,Opaque}` 均返回非 null；`Gray8` 回读 `AlphaType=Opaque`；**`SKColorType.Rgb1010102` 不存在**（实际是 `Rgba1010102`） | §3.1 映射表需修正；`Unpremul`/`Gray8` 需标注后端约束 |
| Skia 快照独立性 | `surface.Snapshot()` 后继续改 surface、再 `surface.Dispose()`，两张快照像素分别保持红/蓝 | D4「每次渲染独立图像」成立 |
| 原生 GL（core 4.5，NVIDIA 610.74） | FBO(无 depth)+Rgba8 = `FramebufferComplete`；`R8` 颜色附件 `Clear(0.2,0.4,0.6,0.5)` → 读回 `(51,0,0,255)`；`GL_BGRA8_EXT(0x93A1)` 分配 `NoError`；混合开启时半透明红读回 `127,0,0,63` | 确认深浅色/格式语义；GL 不做颜色管理（决策 26/P17）正确 |
| 真实 `GLWpfControl` 窗口 | 回调内绑定控件 FBO（id 实测会变：3/2/5，非固定 0/2）；**窗口状态决定渲染泵**：`Hide()` 后 400ms 内 **0 帧**（两次实验一致），**最小化仍在出帧**（400ms/42–1869 帧，速率不稳），重新显示立即恢复（339/527 帧） | GL-6 必须捕获-恢复绑定值；P16/§9.2「隐藏窗口 harness」不可行（R8）；「可见」的准确含义 = 在视觉树中且 `Visibility=Visible`，**与最小化无关** |
| WPF `Application.Exit` | `Exit` 处理器为 `async` 时，`Application.Run()` 返回后 awaited 工作**未完成**（`completed=False`） | `Term()` 的可靠性不能建立在「OnExit 会等待」上 |
| **GL 离屏 Y 方向**（`glflip`，复刻项目 `ViewProjection*Model*pos` + Bitmap 行序） | 控制组（Bitmap 行 0=图像顶）消费后 `upright=True`；离屏 FBO 产物消费后 `upright=False`（`FLIPPED=True`）；产物纹理 `t=0` 行 = 逻辑底端 | **R1 成立**：GL 离屏结果必须做一次 Y 翻转补偿，否则贴回/绘制上下颠倒 |
| **共享上下文 + 捕获/恢复**（`glshared`，`ContextToUse=共享`） | 离屏 FBO 在任一控件回调内 `FramebufferComplete`；每帧 `captured → restore` 全部 `match=True`、`glError=NoError`、恢复后屏幕 clear 正常；**同一控件的 FBO id 会变（实测 3 → 2，另一控件 5）** | R3：必须**捕获实际绑定值再恢复**，不能硬编码 0/2；所有 GL 控件必须共享 `sharedContext` |
| **跨上下文反例**（`glrestore2`，未设 `ContextToUse`） | 两个独立上下文时，在第二个控件回调里绑定第一个控件创建的 FBO ⇒ `GL_INVALID_OPERATION`（每帧一次） | FBO/纹理名是**上下文局部**的；反证 GL-1（多上下文成本），实现需校验共享上下文 |
| **WPF 关闭顺序**（`exitpath`，close 与 shutdown 两路） | 标记顺序恒为 `Closing → OnExit:enter → OnExit:sync-completed → Exit-event:enter`；**`Exit` 处理器中第一个 `await` 之后的代码从未执行**（`Run()` 返回后 300ms 仍缺失） | R6：`Term()` 必须放在 `OnExit` **任何 `await` 之前**并同步完成 |

### 0.2 阻塞项（不修订就会直接写出 bug）

| # | 问题 | 事实/证据 | 修订动作 |
| --- | --- | --- | --- |
| **B1** | `DrawCommandList` 状态机**无任何并发保护**，且 present 中的**第二次 `Dispose` 会立刻清空命令** | 上面实测；`DrawCommandList.cs:50-100` | 契约层声明「同一列表不得并发提交/Dispose」；按 R11 方案 A：`IsDisposed` 预检同步抛 + `TryBeginPresent()` 失败抛 `InvalidOperationException`（§5 D5） |
| **B2** | `EndPresent()` **非幂等**（`DisposeRequested` 分支会执行 `DisposeCore` 并置 `Disposed`；否则把 `PresentApplying` 改回 `Normal`） | `DrawCommandList.cs:59-70` | 明确「`EndPresent` 在 `finally` 中**只调用一次**」；呈现中异常时**不得**再 `Dispose()`（D5/D8） |
| **B3** | GL「每次渲染新建颜色纹理」与「`CleanColor=null` 保留上一次内容」**不可共存** | 新纹理 `TexImage2D(..., null)` 内容未定义；`OpenGLDrawCommandListReplay.Reset` 仅在 `CleanColor` 非 null 时清屏 | GL 语义降级：`CleanColor=null` = **未定义**（含第二次渲染）；§4.4 不再声称两后端一致（R2） |
| **B4** | GL「恢复屏幕状态 = 绑定 FBO 0」**错误** | 实测回调内绑定的是控件 FBO（`GLFramebufferHandle`），回调返回后库才 `BindFramebuffer(0)`；且**同一控件不同帧 id 会变**（实测 3→2，另一控件 5） | GL-6 改为**捕获-恢复实际绑定值** + 控件像素视口（R3 已定，`glshared` 实测） |
| **B5** | 离屏复用同一 `SKSurface` 时，**canvas 矩阵/clip 会跨帧泄漏**（异常路径无 `Restore`），与 D8「失败后上下文仍可用」矛盾 | `CommonSkiaDrawingBase.OnBegin` `Save()` / `OnEnd` `Restore()` 无 finally；`DefaultTextureDrawing` 的 `DrawImage` 无 finally；`SKCanvas.SaveCount/RestoreToCount` 可用 | 每帧渲染用 `SaveCount` + `RestoreToCount` 兜底（R4） |
| **B6** | GL 的「隐藏窗口 harness」**不可行** | 实测隐藏后不再有 `Render` tick；上游仅 `IsVisible=true` 时挂 `CompositionTarget.Rendering`，`D3dImage==null` 直接 return | 删除「隐藏窗口 harness」方案；GL 验证=真机手工（R8） |

### 0.3 需修订的后端能力映射（高）

| # | 问题 | 结论 |
| --- | --- | --- |
| **H1** | `Rgb1010102` 在 SkiaSharp 3.119.2 不存在 | Skia 侧映射改用 `SKColorType.Rgba1010102`；中性枚举成员改名 `Rgba1010102` |
| **H2** | `Unpremul` 官方标注「仅支持输入图像，渲染无法输出该类型」 | Skia 侧创建时抛 `NotSupportedException`（请求值仍在 `Options` 中记录） |
| **H3** | 桌面 core GL 无标准 BGRA8 internal format，且本期不做回读，BGRA 无语义 | GL 侧 `Bgra8888` → `NotSupportedException` |
| **H4** | GL `Gray8→R8` 采样得 `(r,0,0,1)`，经 `texture*color` 只输出红通道，**观感≠灰度** | GL 侧 `Gray8` → `NotSupportedException`（R5 已定：拒绝而非降级，保持两后端语义一致） |
| **H5** | 浮点格式作颜色附件需 `EXT_color_buffer_float`（非核心保证） | `RgbaF16/RgbaF32` 在 GL 侧以 `CheckFramebufferStatus` 兜底，不支持则创建/首帧抛 `NotSupportedException` |
| **H6** | 适配器与公开离屏类**都**实现 `ISkiaRenderContext` 会产出两个 canvas 出口 | 只有内部 adapter 实现 `ISkiaRenderContext`；公开类不实现（R7） |
| **H7** | 9 个 Skia 绘制子类在基类「canvas 缺失即跳过」后会 NRE | 收口方式二选一：`OnBegin` 返回 `bool`，或基类暴露 `isCanvasAvailable` 由子类提前 return（R7） |
| **H8** | GL 离屏结果纹理的 Y 方向与 Bitmap 纹理路径**相反** | **`glflip` 实测确认**：控制组（Bitmap 行序）消费后 upright，离屏 FBO 产物消费后翻转（`FLIPPED=True`） | R1 已定：离屏 pass 施加一次 Y 翻转补偿（见 §4.4/GL-3） |
| **H9** | `AppBootstrapper.OnExit` 是 `async void`（框架 `OnExit` 同），`Term()` 可能来不及跑完 | **`exitpath` 实测**：`OnExit` 同步段完整执行，第一个 `await` 之后的代码从不执行（close/shutdown 两路一致） | R6 已定：`Term()` 同步完成 + 调用点放在 `OnExit` **首个 `await` 之前**（§7.4） |

### 0.4 中等/低（实现时收口）

1. `DefaultOpenGLTexture` 的 `TextureWrapS/T`、`ID` 等**直接调 GL**，必须在 current 上下文下使用；`[Serializable]` 与释放委托字段并存需注意（M8）。
2. `GLUtility.CheckError` 在 DEBUG 抛异常；**drain 必须是 try/catch 边界**，异常不得穿透 WPF 渲染循环；`TaskCompletionSource` 用 `RunContinuationsAsynchronously`（M6）。
3. 单次 drain 应设**条数上限**，防止无界积压卡住 UI 帧（M4）。
4. `OpenGLDrawCommandListReplay.Dispose()` 全仓**无调用者**，若 `Term()` 要收口资源需显式接线（L）。
5. §9.1 载体：BenchmarkDotNet 子进程与 SkiaSharp3 native 已知不兼容（仓库已强制 `InProcessNoEmit`）→ 断言类用例改普通可执行 harness（R9）。
6. `DefaultSkiaTextureDrawing.cs` 实际文件名是 `DefaultTextureDrawing.cs`（§8.2 笔误）。
7. `RenderControl_UnLoaded`/`DisposeRenderLoop` 已正确调用 `StopRendering()/RemoveRenderContext`（`FumenVisualEditorViewModel.Drawing.cs:1020-1021,1065-1070`）→ P16 的「停止时结束挂起请求」有接线点。
8. FBO/纹理名是**上下文局部**的（实测未共享上下文时跨回调绑定 → `GL_INVALID_OPERATION`）→ 离屏 GL 对象必须只属于 `sharedContext`；实现里若发现控件未共享上下文，应显式报错而非静默失败。
9. 渲染回调返回后线程可能**仍保留 current 上下文**（探针在回调外 `DeleteTexture` 也报 `NoError`）→ 「回调外调用 GL 恰好成功」不可作为实现依据；延迟删除必须显式在 drain（context-current）内执行。
10. 渲染泵**不受 vsync 限帧**：可见时实测数百帧/400ms、最小化时 42–1869 帧/400ms（速率不稳，窗口状态切换期会出现突发）→ drain 也应设「每帧条数/时间预算」（与 §7.2 的单次上限同源），避免离屏任务在高频 tick 下挤占 UI。

### 0.5 实施前实验（三项，已完成）

| 实验 | 命令（探针目录内） | 结论 | 落点 |
| --- | --- | --- | --- |
| GL 离屏 Y 方向 | `dotnet run -- glflip` | 控制组 upright、离屏产物翻转（`FLIPPED=True`）；三张 PNG 已用视觉模型复核 | R1 → §4.4 / GL-3 / GL-6 |
| GL 捕获-恢复 + 共享上下文 | `dotnet run -- glshared`（另 `glrestore2` 为跨上下文反例） | 同上下文下跨控件离屏可用、恢复 `match=True`、`glError=NoError`；FBO id 会变（3/2/5） | R3 → GL-6 / §2.3(7)(8) |
| 退出路径 | `dotnet run -- exitpath close` / `exitpath shutdown` | `OnExit` 同步段完整执行；首个 `await` 之后的代码从不执行（两路一致） | R6 → §7.4 / §8.2 |
| 渲染泵 vs 窗口状态 | `dotnet run -- wpfgl2` | `Visibility=Hidden` → 0 帧（两次一致）；**最小化仍出帧**（42–1869 帧/400ms）；重新显示立即恢复 | §2.2 / §2.3(4) / GL-2 / R10 |

实现后仍需一次真机复核（见 §9.3）：在 `Term()` 内加临时日志，关闭主窗口确认日志在进程退出前写完。

### 0.6 实现与验证结果（第 6 轮）

**状态：已实施并通过验证**（源码改动全部位于 `OngekiFumenEditor/`；`Avalonia/` 未改动）。

| 项 | 内容 |
| --- | --- |
| 新增 | `Kernel/Graphics/OffscreenRenderOptions.cs`、`OffscreenPixelFormat.cs`、`OffscreenAlphaType.cs`、`OffscreenColorSpace.cs`、`IOffscreenRenderContext.cs`；`Skia/SkiaOffscreenRenderContext.cs`、`SkiaOffscreenRenderLane.cs`、`SkiaOffscreenReplayContextAdapter.cs`；`OpenGL/OpenGLOffscreenRenderContext.cs`、`OpenGLOffscreenRenderQueue.cs`、`OpenGLOffscreenReplayAdapter.cs` |
| 修改 | `IRenderManagerImpl`（+3 成员）、两个 manager（离屏接线 + `Term`）、`DefaultOpenGLRenderContext`（每 tick drain）、`OpenGLDrawCommandListReplay`（`flipY` 重载）、`CommonDrawingBase` + `DrawingTargetContext`（`FlipY` 注入点）、`DefaultOpenGLTexture`（离屏构造 + 释放委托）、`ITexture`（`IImage.Width/Height`）、`AppBootstrapper.OnExit`（首个 `await` 之前的 `Term()`）、Skia 画布抽象重构（10 处强转 + 9 子类 null 收口） |
| 验证 | ① 仓库外 harness（`%TEMP%\ongeki-offscreen-verify`，引用主工程）：**Skia 40 项 + GL 16 项断言全部通过**，覆盖清屏/几何、参数矩阵（含 `Unpremul` 拒绝）、取消、autoDispose、释放竞态回归（200 次）、上下文生命周期与 `Term`、GL 的 Y 方向与端到端贴回方向、延迟删除、屏幕帧不受 drain 影响；② **真实编辑器人工验证通过**（GL 与 Skia 均「无可辨差异」，Skia 另有 562×659 的离屏 PNG 逐项核验，详见 §9.2.1） |
| 实现层新发现 | R12（清理必须先于唤醒）、R13（GL 初始化必须先于可见/加载）—— 均已在代码落地 |

### 0.7 已知非本功能问题（记录备查）

- GL 的 instanced 圆形绘制在 `hollowLineWidth = 0`（`isSolid`）时不产生像素：该 shader 只按环宽绘制。离屏与屏幕表现一致，不属于本次改动引入。

## 1. 目标与边界

### 1.1 目标

WPF 工程的 `IRenderManagerImpl` 增加离屏渲染入口：调用方显式指定输出图像参数（宽度、高度、像素格式、alpha 类型、颜色空间等），
拿到一个 `IOffscreenRenderContext`，由它决定「什么时候把 `DrawCommandList` 真正渲染掉」，并**由渲染方法直接返回产出图像**（`Task<IImage>`）。

- **Skia 实现**（`DefaultSkiaDrawingManagerImpl`）：raster `SKSurface` + `SKImage` 快照（COW，实测与 surface 解耦），由 manager 的离屏通道线程推进。
- **OpenGL 实现**（`DefaultOpenGLRenderManagerImpl`）：FBO + 每帧新颜色纹理（零拷贝，不经 `glReadPixels`），在 GL 控件 `Render` 回调内排空执行。

**公共 API 相同，但后端能力/语义并非完全一致**（B3/H1–H5/H8）：差异必须在此文档与 XML 注释中明示，禁止在调用方假设「两后端完全等价」。

### 1.2 非目标（本期不做）

1. **不接入编辑器/波形的渲染循环**（命令列表由调用方提供）。
2. **不做像素回读与编码**（PNG/JPEG 导出、`ReadPixels`）。
3. **不做 GPU 加速的 Skia 离屏**；**不做 GL 第二上下文**（GL-1 给出排除理由）。
4. **不改 UI 渲染路径的现有行为**（Skia 的 `PaintSurface` 订阅/`LimitFPS` 闸门/present 语义；GL 的 `GLWpfControl.Render` 流程）。
5. **不动 Avalonia 工程**。
6. **不提供「投递但不等待」入口**（P8）。
7. **不做 GL 的持久画布**（否则与「每帧独立纹理 + 零拷贝」冲突，B3/R2）。

## 2. 仓库现状与硬约束

### 2.1 Skia 侧事实（WPF）

| 位置 | 事实 | 与本设计的关系 |
| --- | --- | --- |
| `Kernel/Graphics/IRenderManagerImpl.cs:13-75` | 管理接口；**无默认接口成员** | 新入口 `CreateOffscreenToImage` + `Term` 加在这里；两个实现都要实现 |
| `Kernel/Graphics/IRenderContext.cs:8-23` | `OnRender`、`LimitFPS`、`PerfomenceMonitor`、`Name`、`PostDrawCommandList`、`StartRendering`/`StopRendering` | 离屏**不**复用该接口（决策 3）；重放内部需要 `IRenderContext` 实例 → 内部适配器 |
| `Kernel/Graphics/Skia/ISkiaRenderContext.cs:8-11` | **已存在** `ISkiaRenderContext : IRenderContext { SKCanvas Canvas { get; } }` | D1 复用；但**只有内部适配器实现它**（H6） |
| `Kernel/Graphics/Skia/DefaultSkiaDrawingManagerImpl.cs:245-300` | front/back 槽位；present 取 front 后置空（一次性），`AutoDispose` 时释放；`PresentCommands`（287-300）为 **private**，每次 present `using` 新建 `SkiaDrawCommandListReplay` | 离屏复用 `PresentCommands`（改 internal），不进槽位（P11） |
| `Kernel/Graphics/Skia/DefaultSkiaRenderContext.cs:106-114` | UI present 被 `Canvas.Save()/Restore()` 包裹整帧 | **离屏也必须整帧包裹**（B5） |
| `Kernel/Graphics/Skia/SkiaDrawCommandListReplay.cs:40-65,88-93` | 构造即按 `CleanColor` 清屏、建 8 个 backend 绘制对象；`Dispose` 释放 line/texture/string 三件；**无 canvas 兜底** | 离屏每帧 `using`；需外层 `RestoreToCount` |
| `Kernel/Graphics/Skia/Drawing/CommonSkiaDrawingBase.cs:18-35` | `OnBegin` 取 canvas → `canvas.Save()` → `SetMatrix(mvp * flip * translate(V/2) * scale(RenderScale))`（**覆盖**矩阵）；`OnEnd` → `Restore()` | 离屏**不得**再缩放 canvas（D7） |
| `Kernel/Graphics/Skia/Drawing/**`（10 处） | `((DefaultSkiaRenderContext)target.RenderContext).Canvas` | D1 改为 `ISkiaRenderContext`；**9 个子类需在 canvas 缺失时提前返回**（H7） |
| `Kernel/Graphics/Skia/SkiaUtility.cs:9-13` | DEBUG 下 `CheckSkiaRenderContext` 只接受 `DefaultSkiaRenderContext` | 放宽为 `ISkiaRenderContext` |
| `Kernel/Graphics/Skia/RenderControls/SkiaRenderControlBase.cs:45-71,90-107` | `CanvasSize`（逻辑）、`CurrentRenderSurface`、`CreateSize`（逻辑尺寸+DPI） | 换算约定依据（D7） |
| `Kernel/Graphics/Skia/Base/SkiaImage.cs:10-30` | **public**：`SKImage Image`、`Width/Height`、`Dispose` 置空 | 结果类型；harness 可读像素 |
| `Kernel/Graphics/DrawCommands/DrawCommandList.cs:50-100` | `internal TryBeginPresent()/EndPresent()` + 状态机；`Dispose` 在 present 中只置 `DisposeRequested` | **无锁**；`EndPresent` 非幂等（B1/B2） |
| `Kernel/Graphics/DrawCommands/DrawCommandListBuilder.cs:434` | `ResetState()` 默认 `cleanColor = 不透明黑` | §9 用例「全透明黑」须显式 `SetCleanColor(null)` |
| `Kernel/Graphics/ITexture.cs:9-13` | `IImage` 只有 `TextureWrapT/S` | 补 `Width/Height`（P6） |
| `Modules/AudioPlayerToolViewer/ViewModels/...WaveformDrawing.cs:145-146,218,309` | `viewWidth/Height = ActualWidth/Height`（逻辑）、`renderScale = DpiScaleX/Y` | 视口/缩放约定样张 |

### 2.2 OpenGL 侧事实（WPF）

| 位置 | 事实 | 与本设计的关系 |
| --- | --- | --- |
| `DefaultOpenGLRenderManagerImpl.cs:145-200` | `GLWpfControlSettings.ContextToUse = sharedContext`（**静态**）、`glView.Start(setting)`、`sharedContext ??= glView.Context` | 全应用共用一个 GL 上下文；只在控件回调内 current |
| 同上 `:210-216,277-280` | `LoadImageFromStream → new DefaultOpenGLTexture(bitmap)`；`CreateDrawCommandListBuilder` 用 `DefaultStringMeasure` | 离屏创建不依赖 `CheckInitialization`（§7.2） |
| 同上 `:239-256,283-318` | `RemoveRenderContext`；`Post/Swap` 走槽位（`drawCommandListGate`）；`PresentDrawCommandList` 要求 `DefaultOpenGLRenderContext` | 离屏绕开强类型判定，直接调静态 `Present` |
| `DefaultOpenGLRenderContext.cs:64-119` | `StartRendering` 订阅 `Render`；`GlView_Render`：FPS 闸门 → `OnRender` → `SwapAndPresent` | **GL 唯一可执行窗口** → drain 模型（GL-2） |
| `OpenGLDrawCommandListReplay.cs:18-38,40-52,95-116` | **静态单例**（静态 drawings/矩阵栈/`replayGate`）；`Present(manager, IRenderContext, list)` → `EnsureInitialized` → `Reset`（viewport + 按 `CleanColor` 清 color/depth）→ 逐命令 | 离屏复用同一入口；**每次 present 不重建 drawings**（与 Skia 不同） |
| `DefaultOpenGLTexture.cs:10-18,20-70,77-97,104-112` | `IImage`：`_id`、`Width/Height`；wrap/filter getter+setter **直接调 GL**；Bitmap 路径 `Scan0`（首行=顶部）直接上传；`Dispose → GL.DeleteTexture + InvalidateTexture`（**需 current**） | 结果类型；需延迟删除入口 + 线程约束（GL-4/M8） |
| `OpenGLTextureBindingCache.cs:12-58` | 纹理单元绑定缓存（`InvalidateTexture`/`Reset`） | 离屏前后需 `Reset()`（GL-6） |
| `Drawing/TextureDrawing/DefaultTextureDrawing.cs:39-61,80-118` | texcoord(0,0) 对应**上方**顶点 `(-0.5,+0.5)`；frag=`texture*diffuse`，无 Y 翻转 | Bitmap 纹理正向；**FBO 产出的纹理方向相反**（H8/R1） |
| GL 后端全量检索 | 无 `DepthTest`/`Scissor`/`CullFace`；`Blend` 只在 `InitializeOpenGL` 设一次 | 离屏 FBO 无需 depth；混合状态可直接复用 |
| 上游 `GLWpfControl`（4.3.6≈master） | 回调内绑定**控件 FBO**（id 实测会变：3/2/5）+ 控件像素 viewport；`Samples>1` 时先渲染到 MSAA FBO，回调后 Blit 到共享纹理，再 `BindFramebuffer(0)`、`DXUnlock`；仅 `IsVisible=true` 时挂 `CompositionTarget.Rendering`；`RenderContinuously` **默认 true** → 每个 tick 都 `InvalidateVisual()`（**不受 vsync 限帧**，实测可见时达数百帧/400ms）；`D3dImage==null` 直接 return；`Unloaded → ReleaseFramebufferResources()` | GL-6 恢复目标（B4）；活性与 P16（B6）；MSAA 前提 |
| `OpenGLDrawCommandListReplay.Dispose()` | 全仓**无调用者** | 静态资源目前永不释放；`Term()` 若要收口需显式接线 |

### 2.3 八条硬约束

1. **Skia：canvas 来源写死控件上下文**（10 处强转）→ 换强转目标为 `ISkiaRenderContext`，且必须收口 9 个子类的 null 路径。
2. **Skia：present 路径与控件绑定** → 离屏走 internal 的 `PresentCommands`。
3. **GL：单一 `sharedContext`，只在控件回调内 current** → GL 无法使用专用线程，必须「队列 + tick 排空」（GL-2）。
4. **GL 的 tick 是外部条件**：控件不在视觉树 / `Visibility != Visible` / 从未 `OnRender` 过 → 无 tick（实测 `Hide()` 后 0 帧；**最小化仍出帧**）；创建时校验不够（P16 修订为 R10）。
5. **命令列表所有权语义**：`TryBeginPresent/EndPresent` + `autoDispose` + 「present 期间 Dispose 延迟」，**且该状态机无锁**（B1）→ 调用方契约必须写死。
6. **WPF 退出不保证等待异步工作**（H9）→ `Term()` 需同步完成，且调用点必须在 `OnExit` 首个 `await` 之前（R6，`exitpath` 实测）。
7. **GL 对象名是上下文局部的**：FBO/纹理跨上下文无效（`glrestore2` 实测 `GL_INVALID_OPERATION`）→ 所有 GL 控件必须共享 `sharedContext`（应用现状如此），离屏对象只属于该上下文。
8. **GL 绑定值不稳定 + 回调外 current 状态不可依赖**：控件 FBO id 会变（3/2/5 实测），恢复必须「捕获-恢复」；回调返回后线程恰好仍 current，不代表回调外调用合法（延迟删除必须走 drain）。

## 3. 接口草案（v1，已定稿 + 本轮修订）

### 3.1 创建参数 `OffscreenRenderOptions`

```csharp
namespace OngekiFumenEditor.Kernel.Graphics;

/// <summary>离屏目标的创建参数。创建后由 IOffscreenRenderContext.Options 原样回读（请求值，不代表后端实际生效值）。</summary>
public sealed record OffscreenRenderOptions
{
    /// <summary>目标像素宽度（设备像素，必须 &gt; 0）。</summary>
    public required int Width { get; init; }

    /// <summary>目标像素高度（设备像素，必须 &gt; 0）。</summary>
    public required int Height { get; init; }

    /// <summary>像素格式，默认后端平台默认格式（Skia: PlatformColorType；GL: Rgba8）。后端不支持时创建即抛 NotSupportedException。</summary>
    public OffscreenPixelFormat PixelFormat { get; init; } = OffscreenPixelFormat.PlatformDefault;

    /// <summary>
    /// alpha 类型，默认预乘（Premul）。
    /// Skia：Unpremul 不支持（官方：仅输入图像，渲染无法输出）→ 创建时抛 NotSupportedException；
    /// GL：无 alpha 概念，仅记录。
    /// </summary>
    public OffscreenAlphaType AlphaType { get; init; } = OffscreenAlphaType.Premul;

    /// <summary>颜色空间，默认 sRGB。GL 侧不做颜色管理，仅记录（见 GL-5）。</summary>
    public OffscreenColorSpace ColorSpace { get; init; } = OffscreenColorSpace.Srgb;

    /// <summary>
    /// 为 true 时，每次提交都要求 FrameState 满足 ViewWidth*RenderScaleX ≈ Width 且 ViewHeight*RenderScaleY ≈ Height，
    /// 否则该次提交抛 ArgumentException（校验发生在提交时刻/渲染线程，不在创建时刻）。
    /// </summary>
    public bool RequireMatchingViewport { get; init; }
}
```

枚举与跨后端映射（**本轮修订**：`Rgba1010102`、能力标注均为实测/官方文档结论）：

```csharp
public enum OffscreenPixelFormat { PlatformDefault, Rgba8888, Bgra8888, Rgba1010102, RgbaF16, RgbaF32, Gray8, Alpha8 }
public enum OffscreenAlphaType { Premul, Unpremul, Opaque }
public enum OffscreenColorSpace { Srgb, SrgbLinear, DisplayP3, Null }
```

| 参数 | Skia（3.119.2 实测/文档） | OpenGL（core 4.5） |
| --- | --- | --- |
| `PlatformDefault` | `SKImageInfo.PlatformColorType` | `GL_RGBA8` |
| `Rgba8888` | `SKColorType.Rgba8888` | `GL_RGBA8` |
| `Bgra8888` | `SKColorType.Bgra8888` | **`NotSupportedException`**（无标准 BGRA8 internal format；本期无回读，无语义） |
| `Rgba1010102` | `SKColorType.Rgba1010102`（**原名 `Rgb1010102` 不存在**） | `GL_RGB10_A2` |
| `RgbaF16` / `RgbaF32` | 同名 `SKColorType`（实测可建 surface） | `GL_RGBA16F`/`GL_RGBA32F`，**需 `EXT_color_buffer_float`**，否则抛 `NotSupportedException` |
| `Gray8` | `SKColorType.Gray8`（实测 alpha 回读恒为 `Opaque`） | **`NotSupportedException`**（`GL_R8` 采样只出红通道，与 Skia 灰度语义不一致；R5 已定） |
| `Alpha8` | `SKColorType.Alpha8`（实测可建；只有 alpha 通道，内容无颜色） | `NotSupportedException` |
| `AlphaType` | `Premul`/`Opaque` → 同名；`Unpremul` → **`NotSupportedException`** | 不映射、仅记录（GL 无 alpha type） |
| `ColorSpace` | `Srgb`→`CreateSrgb`；`SrgbLinear`→线性 transfer + sRGB gamut；`DisplayP3`→`CreateRgb(Srgb, SKColorSpaceXyz.DisplayP3)`（实测可用）；`Null`→null | 不映射、不做颜色管理，仅记录（决策 26/P17） |

**不暴露表面高级属性**（P9）：Skia 不给 `SKSurfaceProps`；GL 不给 MSAA。

### 3.2 离屏上下文（完全独立接口）

```csharp
namespace OngekiFumenEditor.Kernel.Graphics;

/// <summary>
/// 离屏渲染目标。创建时确定目标参数；调用方提交命令列表并等待渲染完成，产出图像即返回值。
/// 必须在用完后 Dispose；Dispose 后不得再提交渲染。
/// 线程契约：
///   1) 同一 IOffscreenRenderContext 允许从多线程提交，执行顺序=入队顺序；
///   2) 同一 DrawCommandList 不得并发提交或与 Dispose 并发（状态机无锁，见 DrawCommandList.cs:50-100）；
///   3) 渲染完成后的 IImage：Skia 可在任意线程使用；GL 的 DefaultOpenGLTexture 只有 Dispose 是线程安全的，
///      其余访问（wrap/filter/ID）需在 UI/drain 线程。
/// Dispose 语义（后端不同）：
///   Skia：已提交渲染等完成（正常返回结果）；GL：未开始的排队请求以 ObjectDisposedException 结束。
/// 已返回给调用方的图像不受 Dispose 影响。
/// </summary>
public interface IOffscreenRenderContext : IDisposable
{
    /// <summary>创建时使用的请求参数快照（不代表后端实际生效值）。</summary>
    OffscreenRenderOptions Options { get; }

    /// <summary>
    /// 提交命令列表，渲染并等待完成，返回本次渲染产出的图像（所有权归调用方）。
    /// autoDispose 语义与 IRenderContext.PostDrawCommandList 一致：true = 本次渲染确定结束后由实现释放该列表。
    /// <para>
    /// 取消语义（token 只在「未开始渲染」时生效）：
    /// 提交时 token 已取消 → 不排队，返回已取消的 Task；autoDispose: true 的列表立即释放；
    /// 排队期间取消 → 出队时跳过渲染（Skia 通道取用/GL drain 取用各检查一次），列表按 autoDispose 处理，Task 以取消结束；
    /// 已开始渲染 → 取消无效，渲染照常完成并返回图像。
    /// </para>
    /// <para>
    /// Skia：渲染在 manager 的离屏通道线程上执行；GL：渲染在 GL 控件的渲染回调内执行（GL-2）。
    /// 因此**禁止在 UI 线程上同步等待该 Task**（`.Result` / `.Wait()` 会死锁）。
    /// GL 还要求存在可见且持续 tick 的 GL 控件，否则渲染可能长时间挂起（R10）。
    /// 渲染失败时 Task 以原异常结束，上下文仍可继续使用（Skia 侧保证帧状态不残留，见 D8/R4）。
    /// </para>
    /// </summary>
    Task<IImage> RenderToImageAsync(DrawCommandList drawCommandList, bool autoDispose = true, CancellationToken cancellationToken = default);

    bool IsDisposed { get; }
}
```

不提供 `Name` / `PerfomenceMonitor` / `PostDrawCommandList` / `StartRendering` / `StopRendering` / `OnRender` / `LimitFPS`（决策 3），
也不提供「投递但不等待」入口（P8）。

### 3.3 manager 入口

```csharp
public interface IRenderManagerImpl
{
    // ... 现有成员 ...

    /// <summary>创建离屏渲染目标。返回的上下文由调用方负责 Dispose。GL 侧要求存在活动 GL 控件（P16/R10）。</summary>
    IOffscreenRenderContext CreateOffscreenToImage(OffscreenRenderOptions options);

    /// <summary>便捷重载：平台默认像素格式 + sRGB + Premul。</summary>
    IOffscreenRenderContext CreateOffscreenToImage(int width, int height);

    /// <summary>
    /// 关闭后台资源：Skia 的离屏通道线程、两个后端的排队请求、GL 的延迟删除队列。
    /// 由 AppBootstrapper.OnExit 调用（与 ISchedulerManager.Term() 同位）。
    /// **同步完成保证（R6 已定：方案 A）**：实现必须同步执行完全部清理；`Term()` 返回前，
    /// 所有未完成的 RenderToImageAsync Task 必须已结束（结果/异常/取消皆可），不得依赖异步续体。
    /// 无默认实现，两个实现都必须提供。
    /// </summary>
    Task Term();
}
```

- 两个实现都必须实现 `CreateOffscreenToImage` 与 `Term`（P2）。
- **GL 的 `CreateOffscreenToImage` 在没有活动 GL 控件时抛 `InvalidOperationException`**（P16/R10）。
- `RemoveRenderContext` 保持「控件上下文」语义；离屏上下文走独立注销路径（D10）。

## 4. 运行流程

### 4.1 Skia：专用离屏通道线程 + FIFO 队列（无界）

```text
调用方                                离屏上下文                   离屏通道线程(manager 唯一, 懒启动)        manager
  |  CreateOffscreenToImage(opts) --->  校验参数 -> SKSurface.Create(ImageInfo)
  |                                    （返回 null / Unpremul 等 -> 抛异常） -> 登记上下文
  |  using builder = impl.CreateDrawCommandListBuilder()
  |  builder.SetCleanColor(...) / SetViewport(W,H[, scaleX,scaleY]) / DrawXXX(...)
  |  using list = builder.GetDrawCommandList()
  |  var image = await ctx.RenderToImageAsync(list, autoDispose: true, token)
  |                                     ---- 入队(RenderRequest{list, autoDispose, tcs, token}) ---->
  |                                      [出队: 检查 token / 已 Disposed]
  |                                      var saveCount = surface.Canvas.SaveCount;            // R4 兜底
  |                                      try { TryBeginPresent? -> PresentCommands(...) -> snapshot }
  |                                      catch { tcs.SetException(ex); 按 autoDispose 释放列表 } // 不裸抛
  |                                      finally { surface.Canvas.RestoreToCount(saveCount) }
  |                                      （成功: EndPresent() 一次; 然后按 autoDispose 释放列表）
  |                                      TCS.SetResult(image)  // RunContinuationsAsynchronously
  |  <------------------- await 返回 IImage（所有权归调用方）-------------------+
  |  ctx.Dispose()   // 等已提交渲染完成 -> 释放 surface -> manager 注销
```

队列**无界**（P4）：每个挂起请求一直持有其 `DrawCommandList`（含池化缓冲）直到处理/取消/`Term()`——调用方需自行限制并发提交量。

### 4.2 OpenGL：共享队列 + GL 控件 tick 内排空（drain）

```text
调用方                         离屏上下文(GLOffscreen)   GL 控件 Render 回调(UI线程, context current, 控件FBO)   manager
  |  CreateOffscreenToImage(opts) -> 记录 Options/登记（要求活动 GL 控件，否则抛）
  |  var image = await ctx.RenderToImageAsync(list, token) --入队--> 共享队列（无界）
  |                                            GlView_Render（整体 try/finally：限帧早退也必须 drain）:
  |                                              FPS 闸门 -> OnRender -> present 屏幕帧
  |                                              PumpOffscreenRenders():        // 单次上限 N 条（建议）
  |                                                try { 取队列项（检查 token/Disposed）
  |                                                      懒建 FBO（每上下文一个，无 depth）
  |                                                      新颜色纹理 + FramebufferTexture2D
  |                                                      OpenGLDrawCommandListReplay.Present(manager, adapter, list)
  |                                                      解绑纹理 -> new DefaultOpenGLTexture(id, w, h, 延迟删除) }
  |                                                catch { tcs.SetException(ex) }          // 不穿透 WPF 渲染循环
  |                                                finally { BindFramebuffer(控件 FBO); Viewport(控件像素); BindingCache.Reset() }
  |                                              TCS.SetResult(image)   // RunContinuationsAsynchronously
  |  <------------------- await 返回 IImage（纹理，零拷贝）-----------------------+
  |  调用方 Dispose 图像 --(延迟删除)--> 队列 -> 下一次 drain 时 GL.DeleteTexture
```

注意（实测）：控件回调内绑定的是**控件 FBO**（探针读到 2），窗口隐藏/最小化/未 `OnRender` 过时**没有 tick**（R10）。

### 4.3 生命周期状态（两后端一致）

| 状态 | 允许操作 | 说明 |
| --- | --- | --- |
| `Created` | 提交渲染 | Skia 创建时分配 surface；GL 首次渲染建 FBO、每次渲染建纹理 |
| `Rendering` | 继续提交（排队）、Dispose | 同一上下文的提交按入队顺序执行；单次只渲染一个请求（两后端都串行） |
| `Disposed` | 无（提交抛 `ObjectDisposedException`） | 后端资源已释放/排队删除；**in-flight 请求的结果仍有效** |

### 4.4 帧状态约定（**本轮修订：后端语义不完全一致**）

| 项 | Skia | OpenGL |
| --- | --- | --- |
| `CleanColor != null` | 每帧清屏（replay 构造） | 每帧清屏（`Reset`：`ClearColor` + `Clear(Color|Depth)`） |
| `CleanColor == null` | **保留同一 surface 上一次的内容**（跨帧画布） | **未定义**（每次渲染是新纹理，无历史；第二帧也不保留，B3） |
| 缩放 | `CommonSkiaDrawingBase.OnBegin` 的 `SetMatrix(... scale(RenderScale))`（不额外缩放） | `Reset` 的 `GL.Viewport(0,0,ViewWidth*ScaleX,ViewHeight*ScaleY)`（不额外缩放） |
| 像素方向 | 与 `FrameState` 一致（+Y 向上，逻辑坐标） | **实现内做一次 Y 翻转补偿**（`glflip` 实测：不补偿则上下颠倒）——离屏 pass 使用翻转后的投影/view，使纹理行序与 Bitmap/Skia 图像一致（R1 已定） |
| 视口一致性 | `RequireMatchingViewport=true` 时每次提交校验，误差 > 0.5 抛 `ArgumentException` | 同左（同一公式） |

## 5. 关键设计点（结论）

### D1 目标抽象

- **Skia**：10 处 `((DefaultSkiaRenderContext)…).Canvas` → `((ISkiaRenderContext)…).Canvas`；
  **canvas 缺失时的跳过必须收口到全部绘制类**（`OnBegin` 返回 `bool` 或基类置 `isCanvasAvailable`，9 个子类在取 canvas 前检查），否则基类跳过、子类仍 NRE（H7）。
- **GL**：无需 canvas 抽象——重放入口是静态 `OpenGLDrawCommandListReplay.Present(manager, IRenderContext, list)`，离屏只需保证「FBO 已绑定 + context current」。

### D2 接口层次（完全独立 + **唯一**内部适配器）

`IOffscreenRenderContext` 独立于 `IRenderContext`。内部适配器承担全部重放所需的 `IRenderContext`/`ISkiaRenderContext` 职责：
`Name`（自动命名，GL 用于监视器）、`PerfomenceMonitor`（默认 `DummyPerformenceMonitor.Instance`）、`OnRender` 永不触发、其余成员抛 `NotSupportedException`（不可达，仅防御）。
**公开的 `SkiaOffscreenRenderContext`/`OpenGLOffscreenRenderContext` 不实现这两个接口**（H6）。

### D3 执行模型

- **Skia**：专用后台通道线程 + **无界** FIFO 队列（决策 2、P4）；懒启动、活到 `Term()`/进程退出（P3）。
- **GL**：共享队列 + GL 控件 `Render` 回调内**整帧 try/finally** 排空（决策 10、P14、P16；R10）；单次 drain 条数上限待实现时定（建议 1–4 条，防 UI 卡顿）。
- 两后端统一：`Term()`（P2）结束队列与在途工作，**并同步保证未完成 Task 已结束**；`Dispose` 对已提交请求的处理不同（Skia 等完成 / GL 取消未开始）。
- 重放身份：Skia 每次渲染新建 `SkiaDrawCommandListReplay`（P11）；GL 用静态 `OpenGLDrawCommandListReplay`（不重建 drawings）。

### D4 产出图像的语义与所有权（每次渲染一张独立图像，所有权归调用方）

- **Skia**：`surface.Snapshot()` → 新 `SkiaImage`（实测与 surface COW 解耦，surface 释放后仍有效）。
- **GL**：每次渲染新建颜色纹理，FBO 仅作一次性渲染容器；解绑后包成 `DefaultOpenGLTexture` 交给调用方；删除走延迟队列（GL-4）。
- 共同契约：调用方负责 `Dispose`；把图像提交进可能继续被重放的列表之前，必须先确保该列表不再被重放再释放图像。

### D5 Present 语义（**本轮修订：适配真实状态机**）

**正确顺序（每步都必要）**：

```csharp
if (cancellationToken.IsCancellationRequested)
    return Task.FromCanceled<IImage>(cancellationToken);      // 不排队；autoDispose -> list.Dispose()（此刻不在 present 中，安全）

if (list.IsDisposed)                                          // 预检：已释放 -> 明确报错
    throw new ObjectDisposedException(nameof(drawCommandList));

if (!list.TryBeginPresent())                                  // 失败原因只有两种：已在 present 中 / 已 DisposeRequested 或 Disposed
    throw new InvalidOperationException("DrawCommandList is already being presented or has a pending dispose.");

bool endPresentCalled = false;
try
{
    // 渲染：Skia = PresentCommands + Snapshot；GL = FBO 内 Present + 交纹理
}
catch (Exception ex)
{
    // 绝不裸抛：先结束 present，再按 autoDispose 释放（但不重复 Dispose 第二次，见 D8 表）
    // 由调用线程转成 Task 失败：tcs.SetException(ex)
}
finally
{
    if (!endPresentCalled) list.EndPresent();   // 只调用一次（EndPresent 非幂等）
    if (autoDispose) list.Dispose();            // present 结束后调用：若渲染中曾 DisposeRequested，这里一次性完成释放
}
```

要点：
1. **`EndPresent()` 只调用一次**：非幂等（`DrawCommandList.cs:59-70`）。渲染中调用方 `Dispose()` 只是标记 `DisposeRequested`；最后一个 `EndPresent()` 会完成真正的释放。
2. **不要在渲染中额外 `Dispose()`**：present 中的**第二次** `Dispose()` 会立刻清空命令（实测 B1 复现），导致后续重放读到空列表。
3. **`TryBeginPresent()` 返回 false 时不能只看 `IsDisposed`**：`DisposeRequested`（present 中）时 `IsDisposed == false`。按 R11 决定是否需要精确区分（需在 `DrawCommandList` 增加 internal 只读查询）；最小实现=统一抛 `InvalidOperationException`（若 `IsDisposed` 则抛 `ObjectDisposedException`）。
4. **检查顺序（R11 已定：方案 A）**：`IsDisposed` 预检 → 同步抛 `ObjectDisposedException`；`TryBeginPresent()` 失败 → 同步抛 `InvalidOperationException`（P5 语义）。
5. **清理先于唤醒（R12，实现阶段实测）**：`Dispose()` 必须在 `EndPresent()` **之前**调用，且任务完成信号必须在两者**之后**才发出。原因：`EndPresent()` 会把状态退回 `Normal`（自动释放场景为 `Disposed`），若调用方线程（例如 `using` 块，被 `SetResult` 提前唤醒）此时也执行 `Dispose()`，两个线程会同时进入 `DisposeCore()` 并操作同一个池化列表 ⇒ 实测抛 `InvalidOperationException: Collection was modified during enumeration`。反序 + 后发信号后，调用方观察到任务完成时列表必然已完全释放。

### D6 参数类型：中性枚举 + 后端映射（见 §3.1）

GL 的「记录性参数」（`AlphaType`、`ColorSpace`）必须在 XML 注释与文档中明说（P17）：GL 无 alpha type 概念，且不做颜色管理（实测确认）。

### D7 尺寸/缩放约定

- 换算（两后端同一公式）：`target.Width == ViewWidth * RenderScaleX`、`target.Height == ViewHeight * RenderScaleY`
  （例：逻辑 800×600、DPI 1.5 → 1200×900，`SetViewport(800, 600, 1.5f, 1.5f)`）。
- 默认不校验；`RequireMatchingViewport = true` 时**在每次提交（渲染线程）校验 `FrameState`**，误差 > 0.5 抛 `ArgumentException`（该次 Task 失败，列表按 `autoDispose` 处理）。
- 目标尺寸与提交时的 `FrameState` 可以不同（离屏不复用控件尺寸）；`RequireMatchingViewport` 只是可选的一致性检查。

### D8 失败与取消语义（**本轮修订**）

| 情况 | 行为 |
| --- | --- |
| `Width/Height <= 0` | 创建时抛 `ArgumentOutOfRangeException` |
| 参数组合非法/不支持 | **创建时抛**：Skia `Unpremul`、GL `Alpha8`/`Bgra8888`/`Gray8`、GL 浮点格式无 `EXT_color_buffer_float`、`PixelFormat` 枚举值非法 → `NotSupportedException`；Skia `SKSurface.Create` 返回 null → `InvalidOperationException`（附 `Width/Height/PixelFormat`） |
| GL 无活动 GL 控件 | `CreateOffscreenToImage` 抛 `InvalidOperationException`（P16） |
| GL 运行中失去 tick（不可见/未 OnRender 过/控件卸载） | 请求**保持挂起**（可被 `Dispose`/`Term` 结束）；文档与 Xml 注释明示该前提（R10） |
| 提交时 token 已取消 | 不排队；`autoDispose: true` 的列表立即释放；返回已取消的 `Task`（**不同步抛**） |
| 排队期间取消 | 出队时跳过渲染；列表按 `autoDispose` 处理；`Task` 以取消结束 |
| 已开始渲染后取消 | 取消无效；渲染照常完成并返回图像 |
| 渲染时抛异常 | 该次 `Task` 以原异常结束；`autoDispose: true` 的列表按 D5 顺序释放；**Skia 目标表面帧状态已兜底恢复**（R4），GL 状态亦恢复（GL-6）；失败帧内容未定义 |
| 上下文已 `Dispose` 后提交 | 抛 `ObjectDisposedException`（同步抛，不排队） |
| 提交时列表已 disposed | 抛 `ObjectDisposedException`（预检，P5/R11） |
| 提交时列表正被 present | 抛 `InvalidOperationException`（P5/R11） |
| 同一列表并发提交/Dispose | **调用方契约违反**：行为未定义（状态机无锁），实现不保证检测 |
| 已提交请求遇到上下文 `Dispose` | Skia：照常执行完（Task 正常返回）；GL：未开始的以 `ObjectDisposedException` 结束（与 GL-7 一致） |
| `Term()` 时未执行的排队请求 | `Task` 以 `OperationCanceledException` 结束；`autoDispose: true` 的列表仍被释放 |
| `Term()` 返回时 | **保证所有未完成 `RenderToImageAsync` Task 均已结束**（R6） |
| 参数为 null | `drawCommandList` → `ArgumentNullException` |

### D9 监视器与 `GetRenderContexts()`

- 离屏上下文默认 `DummyPerformenceMonitor.Instance`；各后端离屏实现类可暴露可写属性。
- **离屏上下文不加入 `GetRenderContexts()`**（两后端一致）。

### D10 `Dispose()` 的释放范围与 `Term()`

原则：**只释放「这个渲染目标自己造出来的东西」**。

| 项 | Skia | OpenGL |
| --- | --- | --- |
| 拥有的后端资源 | raster `SKSurface` | 每上下文一个 FBO + 每帧颜色纹理（纹理随图像交给调用方） |
| 释放方式 | `surface.Dispose()`（即时） | FBO 与未交出/已归还纹理入延迟删除队列，下次 drain（context current）执行（GL-7） |
| 已提交渲染 | 等完成、不取消（Task 正常返回） | 未开始的以 `ObjectDisposedException` 结束；在途（正在 drain 中）完成 |
| 不释放 | 已返回图像、`autoDispose: false` 的列表、命令内引用资源、字体缓存、通道线程 | 同左；额外不释放调用方已交出的纹理（其 `Dispose` → 延迟删除） |
| `Dispose` 后 | `Options` 可读、`IsDisposed == true`、提交抛 `ObjectDisposedException`、已返回图像仍有效、幂等 | 同左 |
| `Term()` | 停止接收 → 排队请求以 `OperationCanceledException` 结束 → 等在途完成（**无超时**，P10）→ Join 通道线程 → **返回时所有 Task 已结束** | 结束排队请求 → 清空延迟删除队列（有 current 上下文则执行，否则放弃并记日志）→ **绝不等待 tick**（否则与 UI 线程死锁） |

内存视角：Skia 快照与 surface 是 COW；GL 每张未释放图像 = 一份 GPU 纹理内存 —— 两个后端都要求旧图尽快释放。

### D11 平台与线程约束（**本轮补充**）

- **Skia**：raster，重放路径不触碰 WPF（已核对：`Drawing/**` 无 Dispatcher 依赖；`ProgramSetting.Default` 仅只读）；离屏渲染在专用后台线程。
- **GL**：所有 GL 调用（含纹理删除、`DefaultOpenGLTexture` 的 wrap/filter/ID 读写）必须在 current 上下文内 → UI/drain 线程；调用方拿到图像后**只能用 `Dispose()`**（其余访问需自行切回 UI 线程）。
- 共同纪律：重放路径不得触碰 `DispatcherObject`；不设内存/显存硬上限，靠后端创建失败抛错。

### D12 `IImage` 补 `Width/Height`（P6）

两个后端结果类型都已实现（`SkiaImage.cs:19-20`、`DefaultOpenGLTexture.cs:17-18`）→ 补 `int Width { get; }` / `int Height { get; }`。

## 6. OpenGL 实现变体设计

### GL-1 为什么 GL 不能用「专用渲染通道线程 + 第二 GL 上下文」（已核对，排除）

1. **全应用只有一个 GL 上下文**（`sharedContext` 静态 + `ContextToUse`），只在控件回调内 current。
2. **VAO/FBO 不跨共享上下文共享**，而 GL 后端把 VAO/VBO/纹理缓存成实例状态，重放还是静态单例 → 支持第二上下文等于把整个 GL 后端改成「按上下文实例化」。
3. 隐藏窗口 + WGL 共享组 + 线程 `MakeCurrent` + 双重状态机，且**隐藏窗口根本没有 tick**（实测），方案不成立。

### GL-2 执行模型：共享队列 + GL 控件 tick 内排空（P14）

- 共享无界队列（P4）；排空点在 `DefaultOpenGLRenderContext.GlView_Render` 内，**`try/finally` 必须包住整个回调体**（含 `TryUpdateRenderTime` 早退与 `SwapAndPresent` 早退），否则限帧丢弃的 tick 不 drain。
- **单次 drain 条数上限**（实现时定，建议 1–4）：防止无界积压让 UI 帧卡死。
- 前置条件与活性（**P16 修订为 R10**）：
  - 创建时：无活动 GL 控件 → 抛 `InvalidOperationException`；
  - 运行中：最后一个 GL 上下文 `StopRendering`/`RemoveRenderContext` → 把队列中属于其登记上下文的请求以 `OperationCanceledException` 结束；
  - 运行中控件**不在视觉树 / `Visibility != Visible` / 未 `OnRender` 过**（实测 `Hide()` 后 0 帧）：请求保持挂起；调用方可 `Dispose` 上下文或等待重新可见；文档必须写明「GL 离屏要求在视觉树中且 `Visibility=Visible` 的控件（最小化仍可推进）」。
- `PumpOffscreenRenders()` 为 `internal`，允许在自有的 context-current 作用域内手动推进（测试/CLI）。
- **禁止 UI 线程同步等待** `RenderToImageAsync`（已写入接口注释）。

### GL-3 渲染目标：FBO + 颜色纹理，结果直接交纹理（零拷贝）

每次渲染：
① 新颜色纹理：`GL.GenTextures` + `GL.TexImage2D(TextureTarget2d, 0, internalFormat, W, H, 0, format, type, IntPtr.Zero)`（**三者必须配对**：`Rgba8→(Rgba, UnsignedByte)`、`Rgba16F→(Rgba, HalfFloat)`、`Rgba32F→(Rgba, Float)`、`R8→(Red, UnsignedByte)`），Clamp + Linear；
② 附着到该上下文的 FBO（惰性创建一次，`FramebufferTexture2D(ColorAttachment0)` + `CheckFramebufferStatus`；**无 depth 附件**）；
③ `OpenGLDrawCommandListReplay.Present(manager, adapter, list)`（`Reset` 负责 viewport 与清屏；**离屏 pass 需注入翻转后的投影/view**（R1）——现有 `Present` 只读 `drawCommandList.FrameState`，实现需加一个「帧状态覆盖」的 internal 重载）；
④ 解绑纹理并包成 `DefaultOpenGLTexture` 交给调用方；
⑤ 恢复屏幕状态（GL-6）。

### GL-4 图像类型与删除语义（P15：延迟删除）

- 纹理绘制路径硬转 `(DefaultOpenGLTexture)` → 结果类型只能是它；
- `DefaultOpenGLTexture` 增加 internal 构造：`internal DefaultOpenGLTexture(int textureId, int width, int height, Action<int> releaseTexture, string name = "OffscreenTexture")`，
  在 drain 内设定尺寸 + Clamp/Linear；`Dispose()` 在 `releaseTexture` 非空时调用它（而非直接 `GL.DeleteTexture`）并清空 `_id`；
- manager 维护 `pendingTextureDeletes`：所有 GL 对象删除（图像 `Dispose`、FBO 释放、未交出纹理）只入队，
  下一次 drain 在 context current 下执行 `GL.DeleteTexture` + `OpenGLTextureBindingCache.InvalidateTexture`；
- `Term()`/上下文销毁时清空队列（有上下文则执行，无则放弃并记日志）；
- **线程约束**（M8）：交出后 `TextureWrapS/T`、`TextureMinFilter/MagFilter`、`ID` 需 current 上下文；`[Serializable]` 与委托字段并存的注意项记入实现注释。

### GL-5 参数映射与语义差异（P17 + 本轮修订）

| 参数 | 处理 |
| --- | --- |
| `PixelFormat` | §3.1 映射表；`Alpha8`/`Bgra8888`/`Gray8` → `NotSupportedException`；`RgbaF16/RgbaF32` 需 `EXT_color_buffer_float`，否则 `NotSupportedException` |
| `AlphaType` | **不映射**，仅记录（GL 无 alpha type） |
| `ColorSpace` | **不映射、不做颜色管理**（实测：禁用混合后 sRGB 编码值原样写入） |
| `RequireMatchingViewport` | 与 Skia 同一公式与默认值 |

### GL-6 状态隔离与恢复（**本轮修订：错误已修**）

drain 结束必须恢复（实测回调内绑定的是控件 FBO，值为 2）：

1. 恢复**进入离屏前捕获的绑定值**：`GL.GetInteger(DrawFramebufferBinding/ReadFramebufferBinding)` → 离屏工作 → 原样恢复（实测同一控件不同帧可为 3/2、另一控件为 5 ⇒ **绝不能硬编码 0 或 2**）；
2. `GL.Viewport(0, 0, 控件 FrameBufferWidth, FrameBufferHeight)`（设备像素；`CreateSize` 是 Skia 的概念，此处用控件属性）；
3. `OpenGLTextureBindingCache.Reset()`（纹理创建用裸 `GL.BindTexture`，绕过缓存）。

**前提**：当前未启用 MSAA（`GLWpfControlSettings.Samples` 未设置）；若将来启用，drain 发生在库的 MSAA resolve（`BlitFramebuffer`）之前，本恢复顺序仍成立，但需重新评估。
`replayGate` 是静态锁：离屏与屏幕 `Present` 同线程顺序执行、不嵌套，无死锁风险。
**异常边界**：drain 的 try/catch 必须包住整次离屏渲染（`GLUtility.CheckError` 在 DEBUG 会抛）；异常转成该次 Task 失败，绝不穿透 WPF 渲染循环。

### GL-7 `Dispose()`（GL 变体，P15）

1. 置 `Disposed`，拒绝新提交；
2. **取消**尚未开始的排队请求（`Task` 以 `ObjectDisposedException` 结束；`autoDispose: true` 的列表在此释放）——与 Skia「等完成」不同，理由：GL 推进依赖 UI tick，不能假设它继续 tick；
3. 在途请求（正在 drain 执行中）自然完成，结果图像照常交给调用方；
4. FBO 与未交出纹理入延迟删除队列；manager 注销该上下文；
5. 幂等；`Options` 仍可读；**`Term()` 绝不等待 tick**（OnExit 时无 current 上下文，等待即死锁）。

### GL-8 两后端差异汇总（**本轮扩容**）

| 维度 | Skia | OpenGL |
| --- | --- | --- |
| 执行位置 | manager 专用后台线程 | UI 线程（GL 控件 `Render` 回调内） |
| 推进依赖 | 无 | 在视觉树中且 `Visibility=Visible` 的 GL 控件（`Hide()`=0 帧实测；最小化仍出帧） |
| `CreateOffscreenToImage` 前置条件 | 无 | 至少一个活动 GL 控件，否则抛 |
| 产出图像 | `SkiaImage`（COW 快照） | `DefaultOpenGLTexture`（GPU 纹理，零拷贝） |
| 像素方向 | 与 FrameState 一致 | **实现内 Y 翻转补偿后与 Skia 一致**（`glflip` 实测未补偿即翻转；R1 已定） |
| `CleanColor == null` | 保留上一次内容（同一画布） | 未定义（每帧新纹理） |
| 图像 `Dispose` | 随时、任意线程 | 随时、任意线程（内部延迟删除） |
| 图像其他访问 | 任意线程 | 仅 UI/drain 线程（wrap/filter/ID） |
| `Dispose` 与在途请求 | 等完成、不取消 | 未开始取消（`ObjectDisposedException`）、在途完成 |
| 后端资源释放 | 即时 | 延迟到下次 drain；无 current 时放弃并记日志 |
| 参数语义 | `Premul`/`Opaque`；`Unpremul` 不支持 | `AlphaType`/`ColorSpace` 仅记录；`Alpha8`/`Bgra8888`/`Gray8` 不支持 |
| 验证方式 | 可无窗口 harness（读回像素） | **必须真机**（无窗口/隐藏窗口无 tick） |

## 7. 决策记录与实施默认值

### 7.1 决策索引

**P1–P18（用户确认，本轮复核未改动结论）**：

| # | 议题 | 结论 |
| --- | --- | --- |
| P1 | manager 侧命名 | 保持 `CreateOffscreenToImage` / `IOffscreenRenderContext` |
| P2 | `IRenderManagerImpl.Term()` | **加**，`AppBootstrapper.OnExit` 调用（另有同步保证，见 R6） |
| P3 | Skia 通道线程生命周期 | 懒启动，活到 `Term()`/进程退出 |
| P4 | 队列容量 | **无界队列**（说明内存含义） |
| P5 | 提交被跳过的回报 | 抛异常（`ObjectDisposedException` / `InvalidOperationException`）——R11 已定：预检同步抛（方案 A） |
| P6 | `IImage.Width/Height` | **补** |
| P7 | `CancellationToken` 重载 | **加**（仅对未开始的请求生效） |
| P8 | 「投递但不等待」入口 | 不提供 |
| P9 | 表面高级属性（Skia props / GL MSAA） | 都不给 |
| P10 | `Term()` 等待在途渲染上限 | **无上限**（等已提交工作完成） |
| P11 | Skia replay 缓存 | 每次渲染新建 |
| P12 | ~~OpenGL 只做 stub~~ | 作废（GL 要实现完整离屏） |
| P13 | ~~验证载体~~ | 并入 §9 |
| P14 | GL 执行模型 | 共享队列 + 控件 `Render` 回调内排空 |
| P15 | GL 图像删除语义 | 延迟删除队列 |
| P16 | GL 无活动控件 | 创建即抛 + 停止时结束挂起请求（**活性范围经本轮实测修订**为 R10） |
| P17 | GL 的 `ColorSpace` | 记录性参数，不做颜色管理 |
| P18 | GL 验证方式 | 真机手工保底（**隐藏窗口 harness 经实测不可行**，见 R8） |

**R1–R11（本轮可行性复核新决策）**：

| # | 议题 | 结论 | 备注 |
| --- | --- | --- | --- |
| R1 | **GL 结果纹理的 Y 方向** | **已定：翻转成立，采用「离屏 pass 施加一次 Y 翻转」**（使结果纹理行序与 Bitmap/Skia 图像一致；不动采样侧、不要求消费者适配） | `glflip` 实测 `FLIPPED=True`（控制组 upright、产物翻转），PNG 证据随探针产出 |
| R2 | GL 的 `CleanColor == null` | **降级为「内容未定义」**（不承诺保留），文档/注释明示；不引入持久画布（避免破坏零拷贝） | B3 |
| R3 | GL 状态恢复目标 | **捕获-恢复实际绑定值**（draw/read 分开捕获）+ 控件像素 viewport + 绑定缓存；FBO id 实测会变（3/2/5），不得硬编码 | B4 + `glshared` 实测 |
| R4 | Skia 帧状态兜底 | 每帧 `SaveCount` + `try/finally: RestoreToCount`；渲染中异常不裸抛，转 Task 失败；随后上下文仍可用 | B5 |
| R5 | GL 的 `Gray8` | **`NotSupportedException`**（拒绝而非降级；两后端语义一致） | H4 |
| R6 | `Term()` 的同步完成保证 + 退出兜底 | **方案 A（已定）**：`Term()` 同步完成清理并保证所有未完成 Task 已结束；且**调用点必须在 `OnExit` 的第一个 `await` 之前** | `exitpath` 实测：`OnExit` 同步段完整执行，首个 `await` 之后的代码不执行 |
| R7 | Skia adapter/公开类的接口归属 + 子类 null 收口 | 只有内部 adapter 实现 `ISkiaRenderContext`；`OnBegin` 返回 `bool`（或等价标志）由 9 个子类提前 return | H6/H7 |
| R8 | GL 验证方式**修订** | 删除「隐藏窗口 harness」；=真机手工（切 OpenGL 后端 + 离屏渲染 + 贴回/写盘肉眼确认 + 状态检查清单） | B6 实测 |
| R9 | 验证载体 | 断言类用例用**普通可执行 harness**（`OngekiFumenEditor.CommandLine` 模板），BDN 仅用于计时且必须 `InProcessNoEmit` | 仓库既有注释 |
| R10 | GL 活性契约 | 创建时校验 + 失去 tick 时保持挂起（可 Dispose/Term 结束）+ 文档明示「需 `Visibility=Visible` 的控件」 | `Hide()` 后 0 帧（两次实测）；最小化仍出帧 |
| R11 | P5 检查顺序 | **方案 A（已定）**：`IsDisposed` 预检抛 `ObjectDisposedException`；`TryBeginPresent` 失败抛 `InvalidOperationException` | B1 |

### 7.2 实施默认值（无需再讨论，除非反对）

| 事项 | 默认做法 |
| --- | --- |
| 创建前置条件（Skia） | 不调用 `CheckInitialization()`，不要求 `InitializeRenderControl`/WPF 窗口 |
| 创建前置条件（GL） | 要求至少一个活动 GL 上下文（P16/R10），不要求调用线程 current |
| 枚举映射 | §3.1；不支持项创建时抛 `NotSupportedException` |
| `Options` 语义 | 记录请求值；实际生效值由后端实现类暴露 |
| 资源分配时机 | Skia 创建时分配 surface；GL 首次渲染建 FBO、每次渲染建纹理 |
| 运行时改尺寸/格式 | 不支持，`Dispose` 后重建 |
| 失败帧目标内容 | 未定义；GL 未清屏时内容未定义（含第二帧） |
| 同一列表提交到多个上下文 | 顺序允许；并发为调用方契约违反（行为未定义） |
| 列表重复提交 | 允许，每次渲染产出独立图像 |
| Skia canvas 不可用时 | 所有绘制类（基类 + 9 子类）统一跳过 |
| GL FBO depth 附件 | 不附加 |
| GL 状态恢复 | GL-6 三步（控件 FBO + 控件 viewport + 绑定缓存） |
| GL 纹理参数 | 新纹理默认 Clamp + Linear（在 drain 内设定） |
| `GetRenderContexts()` | 不包含离屏上下文 |
| 性能监视器 | 默认 `DummyPerformenceMonitor.Instance`，后端实现类可暴露属性 |
| TCS 选项 | 一律 `TaskCreationOptions.RunContinuationsAsynchronously` |
| drain 异常 | try/catch 边界，转 Task 失败，不穿透 WPF 渲染循环 |
| 缓存/池化 | 首版都不做 |

### 7.3 实现细节，编码时决定（无需讨论）

内部类命名、队列数据结构、DEBUG 日志文案、`PresentCommands` 的可见性做法、
GL 状态恢复的具体调用顺序、`DefaultOpenGLTexture` 新构造的参数名、`OpenGLOffscreenRequest` 字段组织、harness 命名、单次 drain 条数上限的具体值。

### 7.4 退出路径约定（R6 已定：方案 A）

背景（实测）：`AppBootstrapper.OnExit` 是 `async void`，WPF 不等待其中的异步工作 → `Term()` 不能依赖「OnExit 会被等到最后」。

**实现约定**：
1. `Term()`（两个后端）内部**同步完成**全部清理：Skia = 停止接收 → 结束排队请求 → 等在途完成 → Join 通道线程；GL = 结束排队请求 → 尝试清空延迟删除队列（无 current 上下文则放弃并记日志）。
2. `Term()` 返回前，所有未完成的 `RenderToImageAsync` Task 必须已结束（结果/异常/取消），实现不得使用需要 Dispatcher 泵继续推进的异步等待。
3. **调用位置硬约束：`OnExit` 的第一条语句（任何 `await` 之前）**，直调 `IoC.Get<IRenderManager>()` 当前的 `IRenderManagerImpl.Term()`。实测（`exitpath`，close/shutdown 两路）：`OnExit` 的**同步段完整执行**，而**第一个 `await` 之后的代码从未执行**（含 `Exit` 事件处理器里的续体）——因此它**不能**放在现有 `await TryStopMcpServerAsync()` / `await IoC.Get<ISchedulerManager>().Term()` 之后。
4. 若后续需要更强保证，可另加主窗口 `Closing`/`SessionEnding` 预关闭钩子（本轮不做）。

## 8. 实施拆解（文件级清单，待批准后执行）

### 8.1 新增（均在 `OngekiFumenEditor/` 下）

| 文件 | 内容 |
| --- | --- |
| `Kernel/Graphics/OffscreenRenderOptions.cs` | 参数类型 + 校验 |
| `Kernel/Graphics/OffscreenPixelFormat.cs` / `OffscreenAlphaType.cs` / `OffscreenColorSpace.cs` | 三个枚举 |
| `Kernel/Graphics/IOffscreenRenderContext.cs` | 离屏上下文接口（独立，含取消/线程/后端差异注释） |
| `Kernel/Graphics/Skia/SkiaOffscreenRenderContext.cs` | surface / 提交 / 快照产出 / Dispose（**不实现** `ISkiaRenderContext`） |
| `Kernel/Graphics/Skia/OffscreenReplayContextAdapter.cs` | Skia 重放适配器（internal，**唯一**实现 `ISkiaRenderContext`） |
| `Kernel/Graphics/Skia/OffscreenRenderLane.cs` | Skia 离屏通道线程 + 无界 FIFO 队列 + 关闭（internal） |
| `Kernel/Graphics/OpenGL/OpenGLOffscreenRenderContext.cs` | Options / 登记 / 提交 / Dispose（GL） |
| `Kernel/Graphics/OpenGL/OpenGLOffscreenRenderQueue.cs` | 共享队列 + `PumpOffscreenRenders()` + 延迟删除队列（internal） |
| `Kernel/Graphics/OpenGL/OffscreenReplayContextAdapter.cs` | GL 重放适配器（internal） |
| `Tools/` 或 `OngekiFumenEditor.CommandLine` 下的离屏 harness（名称待定） | §9.1 验证载体（普通可执行，非 BDN） |

### 8.2 修改

| 文件 | 改动 |
| --- | --- |
| `Kernel/Graphics/IRenderManagerImpl.cs` | `CreateOffscreenToImage` ×2、`Term()`（含同步保证注释） |
| `Kernel/Graphics/Skia/DefaultSkiaDrawingManagerImpl.cs` | `CreateOffscreenToImage` / `Term` / `PresentCommands` 可见性 / 离屏登记与注销 |
| `Kernel/Graphics/OpenGL/DefaultOpenGLRenderManagerImpl.cs` | `CreateOffscreenToImage` / `Term` / `PumpOffscreenRenders` / 活动上下文登记与校验 / 延迟删除队列接线 |
| `Kernel/Graphics/OpenGL/DefaultOpenGLRenderContext.cs` | `GlView_Render` 内加 drain（**整帧 try/finally**，限帧丢弃的 tick 也 drain） |
| `Kernel/Graphics/OpenGL/OpenGLDrawCommandListReplay.cs` | 新增 internal「帧状态覆盖」重载（`Present(..., in DrawCommandListFrameState overrideState)`），供离屏 pass 注入 Y 翻转后的 projection/view（R1）；`Dispose()` 接线到 `Term()`（当前全仓无调用者） |
| `Kernel/Graphics/Skia/Drawing/CommonSkiaDrawingBase.cs:23` | 强转改 `ISkiaRenderContext`；`OnBegin` 返回 `bool`（或等价标志）供子类提前 return |
| `Kernel/Graphics/Skia/SkiaUtility.cs:9-13` | `CheckSkiaRenderContext` 放宽为 `ISkiaRenderContext` |
| `Kernel/Graphics/Skia/Drawing/**`（9 个子类） | 强转改 `ISkiaRenderContext` + canvas 缺失提前 return：`BeamDrawing/DefaultSkiaBeamDrawing.cs:69`、`CircleDrawing/DefaultSkiaCircleDrawing.cs:24`、`LineDrawing/DefaultSkiaLineDrawing.cs:48`、`LineDrawing/NewSkiaLineDrawing.cs:113`、`PolygonDrawing/DefaultSkiaPolygonDrawing.cs:30`、`StringDrawing/DefaultSkiaStringDrawing.cs:87`、`TextureDrawing/DefaultSkiaBatchTextureDrawing.cs:29`、`TextureDrawing/DefaultSkiaHighlightBatchTextureDrawing.cs:30`、**`TextureDrawing/DefaultTextureDrawing.cs:32`（原笔误为 `DefaultSkiaTextureDrawing.cs`）** |
| `Kernel/Graphics/OpenGL/Base/DefaultOpenGLTexture.cs` | 新增 internal 构造（外部纹理 + 尺寸 + 释放委托）；`Dispose` 走委托；`[Serializable]` 与委托字段的处理 |
| `Kernel/Graphics/ITexture.cs` | `IImage` 增加 `Width` / `Height` |
| `Kernel/Graphics/DrawCommands/DrawCommandList.cs`（可选，取决于 R11） | 若需精确区分 `TryBeginPresent` 失败原因：加 internal 只读查询；否则不改 |
| `AppBootstrapper.cs:563-572` | `OnExit` **第一条语句**处调用当前 `IRenderManagerImpl.Term()`（必须早于任何 `await`；R6 + `exitpath` 实测） |

### 8.3 实施顺序（每步可独立编译）

0. ~~实施前实验~~ **已完成**（见 §0.1/§0.5）：Y 方向、FBO 捕获/恢复、退出路径三项门禁全部关闭；实现时只需按结论落地并在真机复核一次。
1. D1（Skia 10 处 `ISkiaRenderContext` + 9 子类 null 收口）+ `IImage.Width/Height`：纯重构，先确认 UI 路径无回归。
2. `OffscreenRenderOptions` / 枚举 / `IOffscreenRenderContext` + `IRenderManagerImpl` 两个新成员（先 `throw new NotSupportedException()` 占位保证编译）。
3. Skia：`PresentCommands` 可见性 → 通道 + 适配器 + `SkiaOffscreenRenderContext` → manager 接线 + `Term`。**Skia 侧闭合，先验证。**
4. GL：`DefaultOpenGLTexture` 新构造 + 延迟删除队列 → `OpenGLOffscreenRenderContext` + 队列 + 适配器 → `GlView_Render` drain → manager 接线（含活动控件校验）。
5. `AppBootstrapper.OnExit` 钩子 + GL `Term`（按 R6 方案）。
6. 验证（§9）+ 文档状态更新。

## 9. 验证计划

### 9.1 Skia（普通可执行 harness，读回像素断言）

载体（R9）：`OngekiFumenEditor.CommandLine` 模板式 `[STAThread]` 可执行（`new App(false)` + `new AppBootstrapper(false)` + `IoC.GetAll<IRenderManagerImpl>().First(x => x.Name == "Skia")`）；
**不要**用 `LoadImageFromStream`（DEBUG 下要求 `InitializeRenderControl`），纹理直接用公开 `new SkiaImage(SKImage.FromEncodedData(stream))`。
BDN 仅在有计时需求时使用，且必须 `InProcessNoEmit`。

| 用例 | 断言 |
| --- | --- |
| 基础渲染 | 64×64、`SetCleanColor(红)` + 实心线 → 像素取样符合预期 |
| 参数生效 | `RgbaF16 + Premul + SrgbLinear` → `SkiaImage.Image.Info` 的 `ColorType/AlphaType/ColorSpace` 与请求一致；`IImage.Width/Height` 匹配 |
| 格式矩阵 | 每种 `OffscreenPixelFormat × OffscreenAlphaType × OffscreenColorSpace`：支持→返回图像；不支持→`NotSupportedException`（含 `Unpremul`） |
| 初始/复用 | 显式 `SetCleanColor(null)` 新表面为全透明（**注意 builder 默认 cleanColor 是不透明黑**）；全红后再以 `CleanColor=null` 画小块 → 旧区域仍红 |
| 缩放 | 逻辑 64×64 + `RenderScale 2` → 目标 128×128，几何按 2 倍像素落位 |
| 顺序与串行 | 连续提交 3 次（不同清屏色）→ 内容与提交顺序一致；并发提交不交叉 |
| autoDispose | `true` → 完成后 `IsDisposed`；`false` → 不释放 |
| 无法 present | 已 disposed / 正被 present 的列表 → 抛 `ObjectDisposedException` / `InvalidOperationException` |
| 取消 | 提交前取消 → 不排队、返回已取消 Task、列表按 `autoDispose` 释放；排队中取消 → 跳过渲染；开始后取消 → 结果照常返回 |
| **帧状态兜底（R4）** | 构造一次渲染中途抛异常（如引用已释放的 `SkiaImage`）→ `await` 抛原异常；**下一帧输出不受污染**（矩阵/clip 无残留） |
| 失败传播 | 不支持的命令 → `await` 抛原异常；随后仍能正常渲染 |
| Dispose | 在途渲染时 Dispose → 正常完成；之后提交抛 `ObjectDisposedException`；幂等；`Options` 可读 |
| 图像寿命 | 渲染 → 取图 → Dispose 上下文 → 图像仍可绘制 |
| 隔离性 / Term / 视口策略 | `GetRenderContexts` 不含离屏；`Term()` 返回时挂起请求已结束、通道线程退出；`RequireMatchingViewport` 校验行为 |

### 9.2 OpenGL（R8：真机手工保底）

1. 把 `ProgramSetting.Default.DefaultRenderManagerImplementName` 切到 `OpenGL`（默认值本就是 OpenGL，见 `App.config`），用临时入口（调试菜单/CLI）跑：
   创建离屏上下文 → 渲染一帧 → 结果纹理贴回屏幕/写盘，肉眼确认；检查日志中 `CheckFramebufferStatus` 与 GL 错误检查无异常。
2. 必须覆盖：
   - **Y 方向**：确认翻转补偿后「离屏结果贴回屏幕/写盘」方向与 Skia 一致（`glflip` 实测未补偿即上下颠倒）；
   - 两个 GL 控件共享上下文时，离屏 FBO 在任一控件回调内可用、捕获/恢复正确（`glshared` 实测）；
   - 未共享上下文的控件（跨上下文对象）不应静默失败（`glrestore2` 实测 `GL_INVALID_OPERATION`）；
   - 连续多次渲染（每次新纹理）；
   - 离屏渲染后**屏幕帧正常**（FBO/viewport/绑定缓存恢复正确，实测恢复目标是控件 FBO）；
   - `CleanColor=null` 的**未定义**语义（R2 文档一致）；
   - 后台线程 `Dispose` 图像（延迟删除生效）；
   - `Dispose` 上下文后已有图像仍可绘制；
   - 控件 `Hide()`（`Visibility != Visible`）→ 请求保持挂起；`Dispose`/`Term` 能结束挂起（R10）；最小化时仍会推进（实测）；
   - `RgbaF16/RgbaF32`（若驱动不支持，确认抛 `NotSupportedException` 而不是崩）。
3. **不做**隐藏窗口 harness（实测无 tick）。

### 9.2.1 验证结果（已完成，2026-09-28）

| 后端 | 方式 | 结果 |
| --- | --- | --- |
| OpenGL | 真实编辑器 + 临时自测入口（每帧「离屏渲染 → 整屏贴回」），开启约 4 分钟 | 与直接渲染**无可辨差异**（开关来回切换两次）；`离屏渲染自测失败` 0 次；内存 30s 采样 869,712 → 869,676 KB（平稳，无泄漏）；日志无 GL 错误 |
| Skia (CPU) | 同上，并额外写出离屏结果 PNG | PNG **562×659**（= 编辑器视图逻辑尺寸 × RenderScale）；内容完整（垂直轨道线、判定线 `T[0,480]`、顶部刻度尺、彩色物件、`EST:Wave1` / `[0]11.00x` 角标），无黑边/裁切/乱码；`离屏渲染自测失败` 0 次；贴回画面与直接渲染一致 |

验证用临时入口（菜单命令 + `PostDrawCommandList` 分支 + PNG 写盘）已在验证后**全部删除**（残留检查为空、构建 0 错误），确定性回归仍由 §9.1/§9.4 的 harness 覆盖（Skia 40/40、GL 16/16）。

### 9.3 退出路径验证（新增，R6）

框架层面已在探针中验证（`exitpath`，`close` 与 `shutdown` 两路）：`OnExit` 的同步段一定执行、首个 `await` 之后的代码一定不执行 ⇒ 实现按 R6/§7.4 的硬约束摆放 `Term()` 调用点即可。

**真机证据（2026-09-28）**：一次正常关闭的日志停在 `ISchedulerManager.Term()` 自身的 `SchedulerManager.Dispose()` 记录，**没有出现 `OnExit` 中 `await ...Term()` 之后才写的 `----------CLOSE FILE LOG OUTPUT----------`** ⇒ 「`OnExit` 首个 `await` 之后的代码不会执行」在真实应用同样成立，这正是把 `Term()` 放在首个 `await` 之前的原因。

`Term()` 内部现已补一条常驻 INFO 日志（`[离屏] ...已关闭`），下一次正常关闭编辑器即可直接在日志中确认收口动作是否执行，无需任何临时调试代码。

## 10. 术语

- **离屏上下文**：`IOffscreenRenderContext`，固定尺寸/格式的渲染目标，不参与控件 paint 周期。
- **产出图像**：一次成功渲染返回的 `IImage`（Skia `SkiaImage` 快照 / GL `DefaultOpenGLTexture` 纹理），所有权归调用方。
- **Skia 渲染通道**：Skia manager 内唯一的后台线程 + 无界 FIFO 队列。
- **GL drain（排空）**：在 GL 控件 `Render` 回调（context current，控件 FBO 已绑定）内执行离屏队列的机制。
- **延迟删除队列**：GL 对象删除请求的排队处，只在 context current 时真正执行。

## 11. 已确认决策

| # | 决策 | 结论 | 理由 / 影响 |
| --- | --- | --- | --- |
| 1 | 产出图像形态与所有权 | 去掉 `RenderTargetImage`，改为 `Task<IImage> RenderToImageAsync(...)` 直接返回；所有权归调用方 | 每次渲染与每张图一一对应 |
| 2 | Skia 执行模型 | manager 专用渲染通道线程 + FIFO 队列 | 顺序确定、可预测 |
| 3 | 接口层次 | `IOffscreenRenderContext` 完全独立，不继承 `IRenderContext` | 离屏无渲染循环/FPS 闸门/延后 present；内部适配器 |
| 4 | 首版范围 | raster（Skia）+ FBO 纹理（GL）；不做像素回读/编码与 GPU-Skia | 控制复杂度 |
| 5 | 渲染方法命名 | `RenderToImageAsync` | 与 `CreateOffscreenToImage` 同族 |
| 6 | `Dispose()` 释放范围 | 只释放自身造出的资源；不碰调用方资源与共享设施 | 边界与所有权一一对应 |
| 7 | 目标工程 | 仅 WPF（`OngekiFumenEditor`） | Avalonia 不在范围 |
| 8 | Skia canvas 抽象 | 复用已有 `ISkiaRenderContext`（仅内部 adapter 实现，R7） | 10 处强转改目标即可 |
| 9 | 两后端都要支持离屏 | Skia raster surface；GL FBO + 纹理 | 原「GL 只做 stub」作废 |
| 10 | GL 执行模型 | 共享队列 + 控件 `Render` 回调内排空 | 单上下文 + VAO 不共享 + 静态重放引擎 |
| 11 | GL 产出图像 | 每次渲染新建颜色纹理，零拷贝交出；删除走延迟队列 | 与「每次渲染独立图像」一致 |
| 12 | P1 命名 | 保持 `CreateOffscreenToImage` / `IOffscreenRenderContext` | 与既有命名族一致 |
| 13 | P2 关闭钩子 | 加 `IRenderManagerImpl.Term()`，`AppBootstrapper.OnExit` 调用 | 与 `ISchedulerManager.Term()` 同位 |
| 14 | P3 Skia 线程生命周期 | 懒启动、活到 `Term()`/进程退出 | 避免频繁起停抖动 |
| 15 | P4 队列容量 | **无界** | 实现最简；调用方自行限制并发提交 |
| 16 | P5 跳过语义 | 抛异常（`ObjectDisposedException` / `InvalidOperationException`） | R11 待定检查顺序 |
| 17 | P6 `IImage` 尺寸 | 补 `Width`/`Height` | 两后端结果类型已实现 |
| 18 | P7 取消令牌 | 加 `CancellationToken`（仅对未开始渲染生效） | 允许放弃排队项 |
| 19 | P8 便捷入口 | 不提供投递不等待入口 | 异常不可观察 |
| 20 | P9 表面属性 | 不暴露（Skia props / GL MSAA 都不给） | 两后端输出一致、实现最简 |
| 21 | P10 `Term()` 等待 | 无上限，等已提交工作完成 | 后台线程随进程退出 |
| 22 | P11 Skia replay | 每次渲染新建（对齐 UI 路径） | 无跨帧状态 |
| 23 | P14 GL 执行模型确认 | UI tick 内排空 + FBO 纹理直交 | 见决策 10 |
| 24 | P15 GL 删除语义 | 延迟删除队列 + `DefaultOpenGLTexture` internal 构造 | 调用方可任意线程 Dispose |
| 25 | P16 GL 前置条件 | 无活动控件时创建即抛；停止时结束挂起请求 | 活性范围修订为 R10 |
| 26 | P17 GL 颜色空间 | 记录性参数，不做颜色管理 | 与屏幕 GL 观感一致（实测确认） |
| 27 | P18 GL 验证 | 真机手工保底 | 隐藏窗口 harness 实测不可行（R8） |
| 28 | **R1 GL Y 方向** | **已定**：翻转成立；离屏 pass 施加一次 Y 翻转补偿，使结果与 Skia/Bitmap 行序一致 | `glflip` 实测（`FLIPPED=True` + PNG 证据） |
| 29 | **R2 `CleanColor=null`（GL）** | 降级为「内容未定义」 | 每帧新纹理与保留内容不可共存（B3） |
| 30 | **R3 GL 恢复目标** | **已定**：捕获（draw/read 分开）→ 恢复实际绑定值 + 控件像素 viewport + 绑定缓存；不得硬编码 | `glshared` 实测 id 会变（3/2/5），恢复 `match=True`、`glError=NoError` |
| 31 | **R4 Skia 帧兜底** | `SaveCount` + `RestoreToCount`；异常转 Task 失败 | 复用 surface 的跨帧污染（B5） |
| 32 | **R5 GL Gray8** | **`NotSupportedException`**（拒绝而非降级） | 观感≠灰度 |
| 33 | **R6 `Term()` 同步保证** | **已定（方案 A）**：`Term()` 同步完成且返回前所有未完成 Task 已结束；调用点= `OnExit` 首个 `await` 之前 | `exitpath` 实测两路 |
| 34 | **R7 adapter 归属 + 子类收口** | 仅内部 adapter 实现 `ISkiaRenderContext`；`OnBegin` 返回 `bool` | 两个 canvas 出口 + 子类 NRE |
| 35 | **R8 GL 验证修订** | 真机手工（删除隐藏窗口 harness） | 隐藏后无 tick |
| 36 | **R9 验证载体** | 普通可执行 harness；BDN 仅计时（`InProcessNoEmit`） | BDN 子进程与 SkiaSharp3 native 不兼容 |
| 37 | **R10 GL 活性契约** | 创建时校验 + 失去 tick 保持挂起 + 文档明示前提 | 实测隐藏后 0 帧 |
| 38 | **R11 P5 检查顺序** | **方案 A（已定）**：`IsDisposed` 预检抛 `ObjectDisposedException`；`TryBeginPresent` 失败抛 `InvalidOperationException` | 状态机实际行为（B1） |
| 39 | **R12 清理与唤醒顺序** | 先 `Dispose()`（仅置 `DisposeRequested`）→ 再唯一一次 `EndPresent()` → 最后才完成任务；两后端请求类统一 | 实现阶段 harness 实测并发清理竞态 |
| 40 | **R13 GL 初始化顺序** | `InitializeRenderControl`（内部调用 `GLWpfControl.Start`）**必须在控件 Loaded/可见之前**完成，否则上游只在 `IsVisibleChanged→true`/`Loaded` 时挂渲染泵 ⇒ 永不出一帧、离屏请求永久挂起 | harness 实测（先 Show 再 Start ⇒ 0 帧） |
| 41 | **R14 验证载体** | 仓库外 harness（`%TEMP%\ongeki-offscreen-verify`，引用主工程）：Skia 40 项 + GL 16 项断言 | 本轮验证 56/56 通过 |

（后续若有新决策，继续追加到本表。）

## 12. 下一步

1. **实现、自动化验证与人工验证均已完成**：harness 56/56（§9.1 / §9.4）+ 真实编辑器人工验证（§9.2.1：GL 与 Skia 均「无可辨差异」）；验证用临时入口与测试产物已清理（残留检查为空、构建 0 错误）。
2. **唯一可选收尾**：下一次正常关闭编辑器时，在日志中确认 `[离屏] ...已关闭` 出现（§9.3）；若缺失，说明 `Term()` 的调用点被挪到了某个 `await` 之后。
3. 已知非本功能问题（备查）：GL 的 instanced 圆形在 `hollowLineWidth = 0`（`isSolid`）时不产生像素；离屏与屏幕表现一致。
