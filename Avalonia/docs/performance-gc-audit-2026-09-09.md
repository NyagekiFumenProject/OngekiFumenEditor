# Avalonia 全量性能与 GC 审核（2026-09-09，最终静态版）

> **结论性质：** 本文件是当前源码基线的静态审计报告，不是性能基准。审计期间未修改业务代码，未运行构建、测试、格式化、性能分析器或真实音频/图表/浏览器样本。代码中能够直接证明的分配、复制、订阅、循环和资源所有权问题标为“静态确认”；帧率、延迟、GC 暂停时长和实际用户影响均仍需测量。

## 1. 范围与证据边界

- 主应用范围：`Avalonia/src` 下 Core、Desktop、Browser、CommandLine 共 **1,198 个文件、95,549 行**。
- 延伸范围：实际由四个应用项目带入的 Gekimini、Dock、WindowManager、ToolBar、Injectio/本地化生成器、BrowserAudioWorklet、AcbGeneratorFuck、MigratableSerializer、Polyline2D、Earcut 等运行时库，共 **1,680 个文件、223,985 行**。
- 冻结清单合计 **2,878 个文件、319,534 行**；分区清单保存在 `local://perf-audit-*.txt`。行数按各文件实际逻辑行重新计算，不把结尾换行符额外算作空白行。
- `S` = `Avalonia/src/OngekiFumenEditor.Avalonia/`；`D` = `Avalonia/src/OngekiFumenEditor.Avalonia.Desktop/`；`B` = `Avalonia/src/OngekiFumenEditor.Avalonia.Browser/`；`G` = `Avalonia/Dependencies/Gekimini.Avalonia/`；`A` = `Avalonia/Dependencies/AcbGeneratorFuck/src/AcbGeneratorFuck.Share/Audio/`。
- RND、EDT、PRS、AUD、IO、UI、SVC、DSK、WEB、WAV、GEN、ACB-CONTAINER 的清单路径已逐项核对；DAT 的发现来自实现文件深读及其调用链，叶文件并非全部逐行深读；DCK 只对 ToolBar 1–16、WindowManager 506–517 及相关 Dock 跨边界路径做了完整复核，**不宣称 DCK 清单 17–505 的穷尽式逐文件结论**；ACB-AUDIO 714 项完成清单盘点、模式扫描及关键实现/调用方深读，第三方 API 未宣称全量内部审计。

## 2. 优先级定义

| 级别 | 定义 |
| --- | --- |
| P0 | 普通操作可直接造成无界增长、失控循环/任务或严重资源耗尽，且证据充分。本轮无 P0。 |
| P1 | 帧循环、输入/音频高频路径的重复分配或高复杂度；或大工程确定性峰值内存/UI 阻塞风险。 |
| P2 | 导入导出、扫描、切换文档、拖拽等有界操作的可避免开销，或需特定规模才显著的问题。 |
| P3 | 冷路径、构建路径、条件编译路径、低收益微优化，或必须先测量。 |

## 3. 优先处理顺序

1. **先处理 P1 项**（原 21 项，PERF-RND-002 已于 2026-09-11 修复）：PERF-RND-001/003/017、PERF-DAT-001/002、PERF-AUD-001/002、PERF-IO-001–005、PERF-SVC-001、PERF-DSK-001–003、PERF-FWK-001、PERF-DCK-001、PERF-ACB-AUDIO-001/006。它们覆盖失控循环/边界错误、文件完整性、UI 阻塞、渲染与音频高频分配，以及资源生命周期。
2. **再处理 P2 的确定性 O(n²)/全量复制/跨线程 UI 阻塞：** DAT、PRS、IO、DSK、WEB、FWK、DCK 以及 ACB 容器/编解码分区。
3. **P3、条件项和 dormant 项必须先用真实负载验证，不应与活动热路径混改。**

---

## 4. RND：渲染、绘制命令、纹理与几何

### 保留发现

- **PERF-RND-001 / RND-01 — P1，CPU/几何展开。** `S/Modules/FumenVisualEditor/Graphics/Drawing/TargetImpl/VisibleLineVerticesQuery.cs:45-46,92-118` 一旦范围相交就设置 `alwaysDrawing=true`，随后仍变换并发出每个 child/curve point，即使点已在视口外；调用方为 `S/.../CommonLinesDrawTargetBase.cs:25-39`、`S/.../Holds/HoldDrawingTarget.cs:129-130`。应保留跨界端点但对内部 child segment 做裁剪。
- **PERF-RND-002 / RND-02 — P1，O(n²) 列表移动。**〔**2026-09-11 已修复**，见下方「已修复项」〕`S/.../OngekiObjects/Holds/HoldDrawingTarget.cs:129-145` 先完整展开 lane 顶点，再反复 `RemoveAt(0)`；长 Hold 的屏外前缀会在每帧产生二次移动。改为索引窗口、一次压缩或直接查询有界几何。
- **PERF-RND-003 / RND-03+04（合并）— P1，原生纹理生命周期。** `S/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.Drawing.cs:232-235,1078` 在编辑器重新挂接时重新初始化全局 singleton target；`TextureLaneEditorObjectDrawingTarget.cs:27-34`、`LaneCurvePathControlDrawingTarget.cs:39-50`、`Kernel/Graphics/Skia/DefaultSkiaDrawingManagerImpl.cs:48-51` 覆盖旧 `SKImage`/纹理而未先释放，Tap map 还会强引用旧图片。`TapDrawingTarget.Initialize.cs:43-49,64-74`、`TapDrawingTarget.cs:152-158`、`FlickDrawingTarget.cs:33-49,93-97`、`IndividualSoflanAreaDrawingTarget.cs:32-35` 显示 Dispose 不完整；`Drawing.cs:1270-1278` 只清引用不处置 target，`LaneCurvePathControlDrawingTarget.cs:46-50` 还存在先置 null 后 Dispose 的路径。应让每一代 target 有明确 owner，在替换/Detach/Dispose 时释放所有图片和 map。
- **PERF-RND-004 / RND-05 — P2，帧节流后的空表面风险。**〔**2026-09-11 已修复**，见下方「已修复项」〕`S/Kernel/Graphics/AvaloniaSkiaRenderControl.cs:28-39`、`DefaultSkiaRenderContext.cs:52-59` 与 `S/Modules/FumenVisualEditor/.../Drawing.cs:313-320,669-670` 在 FPS cap 下可能构建命令后跳过提交/调度，而 custom surface 已被清理；需保留上一帧或明确 present 语义，避免空白/闪烁。
- **PERF-RND-005 / RND-06 — P2，逐实例纹理绘制。**〔**2026-09-11 已修复**，见下方「已修复项」〕`S/Kernel/Graphics/Skia/SkiaDrawCommandListReplay.cs:144-152` 到 `DefaultTextureDrawing.cs:23-56` 每个 `DrawTexture` 都 Save/变换/创建 `SKPaint`/DrawImage；`LaneCurvePathControlDrawingTarget.cs:126-128` 对每个控制点调用。可在语义允许时批量化并缓存 paint/变换状态。
- **PERF-RND-006 / RND-07 — P2，顶点堆分配。**〔**2026-09-11 已修复**，见下方「已修复项」〕`S/Kernel/Graphics/ILineDrawing.cs:8-14` 将 `LineVertex` 定义为 record class；`VisibleLineVerticesQuery.cs:29-36` 为 lane child/curve point 逐个 `new`，调用方为 `CommonLinesDrawTargetBase.cs:25-39`、`HoldDrawingTarget.cs:129-162`。若 ABI/可变性允许，改为 readonly record struct 或复用值型缓冲。
- **PERF-RND-007 / RND-08 — P2，合并可见范围重复静态对象。** `S/Modules/FumenVisualEditor/Graphics/Drawing/Drawing.cs:449-450,486-520,831-891` 对每个 merged visible range 重复枚举 Meter/BPM、Soflan/IndividualSoflan 并 AddRange 到 target/context map。应按 frame 缓存静态数据并去重。
- **PERF-RND-008 / RND-09 — P2，投射物查询范围过宽。** `Drawing.cs:542-583` 每帧从当前 TGrid 查询到 `TGrid.MaxValue`；`ProjectileBatchDrawTargetBase.cs:175-360` 还逐项检查并可能 `Parallel.ForEach`，`:304-312` 对每个敌方投射物重复查 lane。应按外观/视口上界限制并缓存 lane 查找。
- **PERF-RND-009 / RND-10 — P2，PlayableArea 多重扫描。**〔**2026-09-11 已修复**，见下方「已修复项」〕`DrawPlayableAreaHelper.cs:179-223,445-452` 为每个采样点构建 area sample，`:458-484` 扫所有候选墙 lane，`:501-543` 又调用 `GetChildObjectsFromTGrid/IsPathVaild` 并遍历 children；长墙成本为 samples×lanes×children。应缓存区间游标/有效路径。
- **PERF-RND-010 / RND-11 — P3，绘制命令粒度。** `CommonLinesDrawTargetBase.cs:25-39` 每 lane 发一个 `DrawSimpleLines`，replay `SkiaDrawCommandListReplay.cs:141-143` 与 `NewSkiaLineDrawing.DrawPolylineOrSegmentsRun` 每命令建立/结束路径。只有确认后端支持断开 strip 且基准证实后才聚合。
- **PERF-RND-011 / RND-12 — P3，paint/path effect 创建。**〔**2026-09-11 已修复**，见下方「已修复项」〕`DefaultSkiaLineDrawing.cs:72-97` 在 style 变化时创建/处置 `SKPaint` 和 dash `SKPathEffect`，`:43-45` 每次结束重置；可缓存常用 style，但需先测量。
- **PERF-RND-012 / RND-13 — P2，后端性能数据失真。**〔**2026-09-11 已修复**，见下方「已修复项」〕`SkiaDrawCommandListReplay.cs:214-226` 使用 `DummyPerformenceMonitor`，因此 `Drawing.cs:328-329,619-638` 的编辑器监控不能看到 replay 的实际 draw call/时间。传入真实 monitor 或公开 replay 指标。
- **PERF-RND-013 / RND-16 — P2，预览尺寸使用陈旧状态。**〔**2026-09-12 已修复**，见下方「已修复项」〕`DrawTimeSignatureHelper.cs:58-69,72,82` 在 preview 使用 `RectInDesignMode`，而 `Drawing.cs:429-430` 只在 design mode 赋值；preview 可能拿到默认/旧尺寸，导致错误范围和无效工作。改用当前 context/ViewWidth/Height。
- **PERF-RND-014 / RND-17 — P2，文字绘制高频 native 分配。**〔**2026-09-12 已修复**，见下方「已修复项」〕`DefaultSkiaStringDrawing.cs:46-70,73-99` 每次 Measure/Draw 创建 `SKPaint/SKFont/SKTypeface`；`DurationSoflanDrawingTarget.cs:217-230`、`CommonHorizonalDrawingTarget.cs:161-184` 在每帧大量调用。缓存字体、metrics 和 style。
- **PERF-RND-015 / RND-18 — P2，标记颜色错误兼性能浪费。** `DrawPlayerLocationHelper.cs:15` 默认 tuple 的 color 为 `Vector4.Zero`，`:62-65` 只更新位置/尺寸；`DefaultTextureDrawing.cs:48-51` 直接使用该颜色，marker 完全透明但仍被 replay。初始化非零颜色并避免无效命令。
- **PERF-RND-016 / RND-19 — P2，Stopwatch 单位错误。**〔**2026-09-11 已修复**，见下方「已修复项」〕`DefaultDebugPerfomenceMonitor.cs:149-199` 记录 `Stopwatch.GetTimestamp` 差值，`:318-319` 当作 `TimeSpan` ticks；`DefaultReleasePerfomenceMonitor.cs:65-69,118` 对 `Stopwatch.ElapsedTicks` 做同样转换。频率不等于 10,000,000 时 FPS/ms 全部缩放错误。使用 `Stopwatch.GetElapsedTime` 或显式除以 `Stopwatch.Frequency`。
- **PERF-RND-017 / RND-20 — P1，replay engine 每帧创建。** `DefaultSkiaDrawingManagerImpl.cs:85-99` 每次 present `new SkiaDrawCommandListReplay`；其构造 `SkiaDrawCommandListReplay.cs:20-35,41-60` 创建 target/drawing context、8 个 backend 对象和 3 个 `Stack<Matrix4>`，`NewSkiaLineDrawing` 还创建 List/paint/池；`SkiaDrawCommandListReplay.Dispose:210-213` 现只释放 line drawing。应按 render context 缓存 replay，逐帧 reset 矩阵/stack/state，并在 context 销毁时释放。

### 已修复项

- **PERF-RND-004 / RND-05 — 已修复（2026-09-11）：保留前端帧，释放推迟到 Swap/Remove。**
  - **根因（静态确认）：** 渲染控件在 `Render` 中自我 invalidate 形成持续合成循环（`S/Kernel/Graphics/Skia/AvaloniaSkiaRenderControl.cs:38-39`），每个自定义绘制回调都沿 `RenderFrame → OnRender → OnEditorRender` 构建并提交一帧（`DefaultSkiaRenderContext.cs:61-103`）。FPS cap 命中时 `OnEditorRender` 在构建后 `goto End` 丢弃该帧（`S/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.Drawing.cs:313-322,669-670`）。而 `DrawCommandListContextSlots.Present` 在呈现后即把 `Front` 置空并 `Dispose` 命令列表（原 `DrawCommandListContextSlots.cs:57-98`），因此被节流的合成帧没有可呈现的前端帧；`SwapAndPresentDrawCommandList` 又在 `Swap` 返回 false 时直接 return（原 `DefaultSkiaRenderContext.cs:56-57`），自定义绘制操作不产生像素，是否保留上一帧完全取决于 Avalonia 合成器行为。
  - **处理：**
    1. `Kernel/Graphics/DrawCommands/DrawCommandListContextSlots.cs`：`Present` 改为**只呈现、不清空、不释放**，`Front` 作为保留帧可重复呈现（`DrawCommandList.TryBeginPresent/EndPresent` 已支持重复呈现）。
    2. `Swap` 中既有的 `ReleaseSlot(oldFront)` 成为**唯一稳态释放点**（新帧取代旧帧时释放），`Remove` 负责上下文销毁时的末尾释放，`Post` 仍释放被顶掉的未呈现 `Back`。
    3. `DefaultSkiaRenderContext.SwapAndPresentDrawCommandList` 不再以 `Swap` 返回值拦截呈现：有则换新帧，无则重放保留帧，保证任何合成回调都有像素。
  - **未做/后续：** FPS cap 现在只省下构建/几何枚举，被节流帧仍会重放保留的命令列表，且 `PresentDrawCommandList` 每次 `new SkiaDrawCommandListReplay`（见 PERF-RND-017/RND-20，P1）。落地“按 render context 缓存 replay”后重放成本才会降到可忽略；在此之前该项与 RND-20 应成对评审。同路径的其余缺陷未在本轮一并处理：`OnEditorRender` 在 cap 判断前创建 `DrawCommandListBuilder`（`:313,670`）、跳过帧调用 `OnAfterRender` 但无配对 `OnBeforeRender`（`:329,672`）、`WaveformRenderSession.Render:206-207` 同构跳过。
  - **验证：** 新增 `tests/OngekiFumenEditor.Avalonia.Tests/Graphics/DrawCommandListContextSlotsTests.cs`，固定“呈现保留帧、Swap 时释放旧帧、Remove 释放末尾帧”的契约。Avalonia 是否保留未变脏 surface、以及真实节流是否仍闪烁，仍需按第 20 节未决问题 1/2 在真实负载下测量。既有 `Kernel/Graphics/DrawCommandListTests.cs` 中两个断言“present 即清空/释放 front”的用例（`ContextSlots_PostSwapPresentCycleClearsFront`、`ContextSlots_PresentException_AutoDisposesFrontList`）已按新契约改写为“保留到被取代 / 上下文移除”。

- **PERF-RND-005 / RND-06 — 已修复（2026-09-11）：非批量纹理绘制按命令复用 artist 状态与 paint。**
  - **根因（静态确认 + 基准确认）：** `DefaultSkiaTextureDrawing.Draw`（`S/Kernel/Graphics/Skia/Drawing/TextureDrawing/DefaultTextureDrawing.cs:23-57`）对每个实例调用私有 `Draw`，每个实例都执行一次 `OnBegin/OnEnd`（`CommonSkiaDrawingBase.cs:18-53`：`canvas.Save` + 完整 MVP 矩阵合成 + `Concat`）并 `new SKPaint()`。同文件的批量路径 `DefaultSkiaBatchTextureDrawing.cs:19-67` 与高亮批量路径已把 `OnBegin/OnEnd` 与单个 `SKPaint` 提到循环外，只有非批量路径没有。剩余非批量调用点为 `LaneCurvePathControlDrawingTarget.cs:128`（主纹理，N=可见控制点）与 `IndividualSoflanAreaDrawingTarget.cs:162`。
  - **处理：** `DefaultSkiaTextureDrawing.Draw` 把 `OnBegin/OnEnd` 与 `SKPaint` 提到实例循环外（`try/finally` 保证 `OnEnd`），使非批量路径与既有 batch 路径同构；每实例只保留一次 `Save/Translate/Rotate/Scale/DrawImage/Restore`。顺带在 texture 非 `SkiaImage` 时提前返回，避免 `OnBegin` 已 Save 后抛异常导致 canvas save 栈失衡。各实例自身 `Save/Restore` 平衡，外层一次性 `Save+Concat` 与逐实例执行净变换相同，像素输出不变。
  - **基准**（新增 `benchmarks/OngekiFumenEditor.Avalonia.Benchmark/Benchmarks/SkiaTextureDrawingBenchmarks.cs`；ShortRun/InProcessNoEmit；24×24 白纹理、1920×1080 CPU 画布；`Original_PerInstanceArtist` vs `Optimized_HoistedArtist`，`Reference_ExistingBatchPose` 为既有 batch 参照）：

    | 实例数 | 原实现 | 新实现 | 时间比 | 原分配 | 新分配 |
    |---:|---:|---:|---:|---:|---:|
    | 64 | 95.00 µs | 66.78 µs | 0.70× | 5,632 B | 88 B |
    | 512 | 1,461.22 µs | 859.41 µs | 0.59× | 45,056 B | 88 B |
    | 2048 | 3,557.16 µs | 2,225.86 µs | 0.63× | 180,224 B | 88 B |

    原实现恰为 88 B/实例（来自 `new SKPaint()`），新实现整条命令固定 88 B，分配由 O(N) 降为 O(1)；时间约 −30%~−41%。注：`SKMatrix44` 在 SkiaSharp 3.x 为 `struct`，无堆分配，MVP 合成是纯 CPU 成本。此为合成基准，真实控制点数/GPU 后端下的绝对值仍需实测。
  - **验证：** 新增 headless 像素测试 `SkiaRenderSmokeTests.SkiaTextureDrawing_ReusesArtistAcrossInstancesWithoutChangingPixels`：经真实 replay 绘制一条含 2 个实例的 `DrawTextureCommand`，断言两枚 sprite 落在左右两半且 clean color 完整填充背景（可捕捉实例丢失/变换累积/save 栈失衡）。`SkiaRenderControl_CleanFrame_*` 与 `SkiaStringDrawing_*` 单独运行通过。
  - **附带发现：** 该像素测试实证当前 SkiaSharp 3.x 下 `SKPaint.Color` 不调制 `DrawImage` 输出（白纹理不会被染成红/蓝）。这影响 PERF-RND-015 / RND-18 关于“marker 因 `color=Zero` 而完全透明”的推断，应按真实 `DrawPlayerLocationHelper` 路径复核后另行处理。

- **PERF-RND-006 / RND-07 — 已修复（2026-09-11）：`LineVertex` / `VertexDash` 改为 `readonly record struct`。**
  - **根因（静态确认 + 基准确认）：** `S/Kernel/Graphics/ILineDrawing.cs:8-13` 把 `VertexDash`/`LineVertex` 定义为 `record`（即 class）。`VisibleLineVerticesQuery.cs:34` 的 `PostPoint2` 对每个 lane child / curve point 执行 `new LineVertex(...)`，`:131-156` 的去重与调用方 `HoldDrawingTarget.cs:129-162` 只以引用搬动；调用方 `ObjectPool.GetPooledList<LineVertex>()` 复用的是 List 底层数组而非元素本身，因此每帧可见 lane/hold/beam 的每个顶点都是一次堆分配（`DrawSimpleLines/DrawLines` 再引用进命令缓冲）。
  - **处理：** `ILineDrawing.LineVertex` 与 `VertexDash` 改为 `readonly record struct`。record 的值相等（query 去重、Hold 比较）与 `with` 语法（`DurationSoflanDrawingTarget.PushLine:62-77`）均保留；全部调用点已核对无 null 检查、`ReferenceEquals`、字典/HashSet 键或类型测试。顶点由 48 B/个堆对象变为内联值，池化 List/数组/命令缓冲不再为元素分配。
  - **基准**（新增 `benchmarks/.../LineVertexAllocationBenchmarks.cs`；ShortRun/InProcessNoEmit；复刻“逐点 `new` + optimize vertices 去重”流程，`New_StructReusedBuffer` 为预分配数组缓冲变体）：

    | 顶点数 | 原实现 (class) | struct + List | struct + 预分配缓冲 | 原分配 | 新分配 |
    |---:|---:|---:|---:|---:|---:|
    | 256 | 5.20 µs | 2.03 µs（0.39×） | 1.49 µs（0.29×） | 12,288 B | 0 B |
    | 2048 | 51.73 µs | 16.27 µs（0.32×） | 12.34 µs（0.24×） | 98,304 B | 0 B |

    原实现恰为 48 B/顶点；2048 顶点时 Gen0 5.86 次/千次、Gen1 2.62 次/千次，新实现均为 0。`with` 表达式路径 17.7 ns/48 B → 3.9 ns/0 B。此为合成基准，真实可见顶点数下的绝对值仍需实测。
  - **验证：** `CommonHorizonalDrawingTargetTests`（真实 `DrawSimpleLines` 命令 + 顶点断言）与 `DrawCommandListTests`（含顶点构造与命令缓冲）全绿；headless `SkiaRenderControl_CleanFrame_*` 通过。
  - **行为差异（已核对，无调用点依赖）：** `VertexDash` 由引用相等变为值相等，线绘制的 `dash == VertexDash.Solider` 现按值匹配（`(100,0)` 即视作 solid，见 `NewSkiaLineDrawing.IsDashed`）；`default(LineVertex).Dash` 从 null 变为零值。

- **PERF-RND-002 / RND-02 — 已修复（2026-09-11）：Hold 顶点裁剪改为索引窗口 + 池化裁剪列表。**
  - **根因（静态确认）：** `HoldDrawingTarget.Draw` 对完整展开的 lane 顶点反复 `RemoveAt(0)`/`RemoveAt(Count-1)` 并 `Insert(0, ...)`，长 Hold 的屏外前缀/后缀每帧产生 O(n²) 搬移（与 WPF `459f72177` 同源）。
  - **处理：** 按 WPF 终态改为 `startIdx`/`endIdx` 索引窗口，只对窗口内顶点做水平丢弃判定；用池化 `clippedList` 一次拼出 `[holdPoint, ...窗口, holdEndPoint]` 交给 `builder.DrawLines`；`list.Count == 0` 分支不变。
  - **验证：** 20 万组随机输入的新旧算法属性对照：所有正常 Hold（`holdEnd.Y ≥ holdPoint.Y`）输出逐点一致；唯一差异是退化的"倒置 Hold"（end 早于 start），旧实现只剩 1 个不可见顶点，新实现（与 WPF 终态一致）输出 2 点线段。Release 全量 689/689 通过。

- **PERF-RND-011 / RND-12 — 已修复（2026-09-11）：线绘制切换为 WPF 终态的池化实现。**
  - **根因（静态确认）：** 原 `DefaultSkiaLineDrawing` 每次 `DrawPath` `new SKPath()`，每次样式变化新建 `SKPaint` 与 dash `SKPathEffect`；WPF 终态 replay 早已使用带 `Stack<SKPath>` 池、dash effect 缓存、Run 合并与 Mesh 渐变回退的实现，Avalonia 未迁移。
  - **处理：** 移植 `Kernel/Graphics/Skia/Drawing/LineDrawing/NewSkiaLineDrawing.cs`（`pathPool`/`MaxPooledPaths=32`、`dashPathEffectCache`、`BuildSegmentsAndRuns` 合并、`MeshGradientSegmentThreshold=256` 三角网回退、单实例 `strokePaint`/`meshPaint`），replay 改用它并在 `Dispose` 中释放；删除旧 `DefaultSkiaLineDrawing`。
  - **验证：** 新增 headless 像素测试 `SkiaLineDrawing_RendersSolidAndDashedLinesThroughReplay`（经真实 replay 画实线 + 虚线，断言两条带像素与未触及区域背景），通过；`(100,0)` 仍按 solid 处理（`IsDashed` 语义与删除前一致）。

- **PERF-RND-012 / RND-13 — 已修复（2026-09-11）：replay 接入 render context 上的真实 monitor。**
  - **根因（静态确认）：** `SkiaDrawCommandListReplay` 的 `ReplayDrawingContext` 硬编码 `new DummyPerformenceMonitor()`，且 `IRenderContext` 没有 monitor 属性，编辑器监控看不到 replay 的 draw call 与耗时。
  - **处理：** `IRenderContext` 增加 `IPerfomenceMonitor PerfomenceMonitor { get; set; }`（`DefaultSkiaRenderContext` 默认 `DummyPerformenceMonitor.Instance`）；编辑器在 `StartRenderContext` 与 `IsDisplayFPS` 切换时安装自身 monitor，`StopRenderContext` 复位为 Dummy；replay 统一读 `RenderContext.PerfomenceMonitor ?? Dummy`；`DummyPerformenceMonitor` 新增共享 `Instance`。
  - **验证：** 新增 `SkiaLineDrawing_ReplayDrawCallsReachInstalledPerfomenceMonitor`：在 context 上安装 `DefaultReleasePerfomenceMonitor`，经真实 replay 画线后断言 `AveDrawCall > 0`。
  - **未做/后续：** WPF 终态的 `OnBeforePresent/OnAfterPresent` 与 `RenderPerfomenceMeasurePanel` 仍缺；present 阶段计数目前与构建阶段共用同一批样本，面板落地时需成对补齐。

- **PERF-RND-016 / RND-19 — 已修复（2026-09-11）：监视器计时单位与空样本 NRE。**
  - **根因（静态确认）：** 两个默认 monitor 把 `Stopwatch` 频率单位的原始差值当作 `TimeSpan.Ticks` 记录（debug：`GetTimestamp` 差值；release：`timer.ElapsedTicks - currentBeginRenderTick`），频率非 10,000,000 时 FPS/ms 全部缩放错误。
  - **处理：** debug 侧绘制/目标绘制/整帧三处改用 `Stopwatch.GetElapsedTime(...).Ticks`；release 侧改用 `timer.Elapsed.Ticks` 并删除恒为 0 的 `currentBeginRenderTick`；同时把 release 侧 `MostUIRenderSpendTicks`/`MostSpendTicks` 的 `GroupBy(...).FirstOrDefault().Key` 换成空安全的 `MostFrequentValue`（安装后无样本时不再 NRE）。
  - **验证：** 全解决方案重建 0 error；Release 全量主测试项目 689/689、Desktop 测试项目 148/148 通过（测试命令与 headless 运行限制见 `wpf-to-avalonia-migration-status.md` 的「测试命令」）。

- **PERF-RND-009 / RND-10 — 已修复（2026-09-11）：帧内缓存墙轨边界描述符 + 无分配子节点区间查询。**
  - **根因（静态确认 + 基准确认）：** `S/.../DrawPlayableAreaHelper.cs` 的 `QueryBoundaryXGridUnit` 对每个采样点线性扫过全部候选墙轨，活跃边再调 `CalculateBoundaryXGridUnit`；后者每次执行 `lane.GetChildObjectsFromTGrid(tGrid)`（有效路径下 `children.GetRange(...)` 分配 List）**且**再调一次 `lane.IsPathVaild()`（`children.All(...)`，O(子节点数)），而 `GetChildObjectsFromTGrid` 内部本身又调一次 `IsPathVaild()`。采样点数正比于候选墙轨子节点总数（`AddWallCandidateSamples` 把每条候选墙的每个 child TGrid 都加入采样集），故长墙成本为 samples×lanes×children，并伴随每 (墙, 采样, 边) 约 1 个 List + 2 个装箱枚举器的分配。
  - **处理：**
    1. `Base/OngekiObjects/ConnectableObject/ConnectableStartObject.cs`：抽出无分配的 `TryGetValidPathChildRange(tGrid, out start, out count)` 与 `GetChildObjectAt(index)`；`GetChildObjectsFromTGrid` 改为复用前者（返回 `List` 与无效路径 LINQ 分支保持原样），使选择逻辑单一来源。
    2. `DrawPlayableAreaHelper.cs`：新增 `WallBoundaryCandidate` 描述符，`FieldAreaFrameContext` 构造时对每条候选墙轨只算一次 `IsPathVaild()` 与 `MinTGrid/MaxTGrid.TotalGrid`；`IsActiveAtBoundaryEdge` 与边界查询改用描述符。
    3. 有效路径新增 `CalculateValidPathBoundaryXGridUnit`：走 `TryGetValidPathChildRange` + `GetChildObjectAt`，去掉 `GetRange` 分配与重复 `IsPathVaild()`；无效路径保留原 `CalculateBoundaryXGridUnitLegacy`，行为不变。
  - **基准**（新增 `benchmarks/.../DrawPlayableAreaProductionBenchmarks.cs`，直接驱动真实 `DrawPlayableAreaHelper.DrawPlayField`；合成墙轨图，恒等 TGrid→Y 映射使屏幕补采样保持惰性；Release / ShortRun / 同机同参数，优化前后各一次）：

    | 墙数 | 子节点/墙 | 优化前 | 优化后 | 时间比 | 优化前分配 | 优化后分配 |
    |---:|---:|---:|---:|---:|---:|---:|
    | 2 | 16 | 12.97 µs | 8.19 µs | 0.63× | 19.6 KB | 14.2 KB |
    | 2 | 64 | 68.83 µs | 30.06 µs | 0.44× | 73.6 KB | 51.7 KB |
    | 2 | 256 | 649.25 µs | 124.47 µs | 0.19× | 289.6 KB | 201.7 KB |
    | 8 | 16 | 36.50 µs | 15.76 µs | 0.43× | 37.5 KB | 15.7 KB |
    | 8 | 64 | 227.10 µs | 63.78 µs | 0.28× | 141.0 KB | 53.2 KB |
    | 8 | 256 | 2,399.60 µs | 352.49 µs | 0.15× | 555.0 KB | 203.2 KB |

    最坏组合 2.40 ms → 0.35 ms（约 6.8×），分配 555 KB → 203 KB（−63%），Gen0/千次 33.2 → 12.2。残余分配主要来自每采样点的 `TGrid.FromTotalGrid` 与 `CalulateXGrid` 内的 `new XGrid`，属算法固有，需 A2（区间游标/累加器）或值类型化才能再降。
  - **验证：** 新增 `tests/OngekiFumenEditor.Avalonia.Tests/Graphics/DrawPlayableAreaHelperTests.cs` 24 项行为契约（真实 editor + stub `IFumenEditorDrawingContext` + 真实 `DrawCommandListBuilder`，断言实际发出的多边形顶点：默认/多墙取最外、斜墙插值、节点处 Prev 取首/Next 取末、区间半开进出、共线合并且端点保留、可见 Y 裁剪、曲线多段、无效路径退化、门控与非法区间），另加 `tests/.../Base/OngekiObjects/ConnectableStartObjectChildRangeTests.cs` 2 项区间一致性。Release 全量 **692/692 通过**。
  - **未做/后续：** 每个采样点仍线性扫过全部候选墙轨（samples×lanes），本次只消除 children 因子与分配；墙轨数占主导时再按区间游标/累加器优化。WPF 侧同算法文件 `DrawPlayableAreaHelper_new` 仅存在于 `.tmp` 草稿（未进入 WPF 工程），WPF 应用仍用原版 Earcut/交点实现，若其落地可复用本修复。

- **PERF-RND-013 / RND-16 — 已修复（2026-09-12）：拍线/拍号文字改用当帧绘制上下文，不再读 design-only 的 `RectInDesignMode`。**
  - **根因（静态确认）：** `RectInDesignMode` 只在设计模式赋值（`FumenVisualEditorViewModel.Drawing.cs:432-433`，取自该帧 `defaultDrawingTargetContext.WorldRect`），进入预览后不再刷新。`DrawTimeSignatureHelper.DrawLines` 的预览分支把 `RectInDesignMode.Height` 当作 `viewHeight` 传给 `GetVisbleTimelines_PreviewMode`，横向端点又用 `RectInDesignMode.Width`。若从未进入设计模式，该字段是 `default(VisibleRect)`（宽高 0）：可见窗口退化（`SoflanList_CachedPositionList.cs:269-275` 以 `viewHeight/scale` 构造 `[minY, minY+height]`），拍线枚举不出来且端点全部落在 x=0；若曾进入设计模式，则取到旧尺寸 → 预览窗口/端点错位，并可能枚举过多变速段做无效工作。
  - **处理：** `DrawTimeSignatureHelper.DrawLines` 改从 `target.CurrentDrawingTargetContext` 取尺寸——design 分支用 `WorldRect.MinY/MaxY`，preview 分支用 `ViewHeight`，横向用 `ViewRelativeRect.Width`（== 当帧 `ViewWidth`）；`RectInDesignMode` 不再参与渲染（其设计模式语义保留给交互代码）。顺带修复同文件缺陷：`DrawTimeSigntureText` 原先直接使用世界 Y（`drawLines` 存 `y`），而拍线顶点已减 `ViewRelativeOriginY`，滚动后文字整体偏移一个相机原点；现 `drawLines` 存视口相对 Y，文字与线同空间。
  - **验证：** 新增 `tests/OngekiFumenEditor.Avalonia.Tests/Graphics/DrawTimeSignatureHelperTests.cs` 3 项：预览且从未设置 design rect 时仍产出拍线且最大 X == `ViewWidth`（旧实现为 0 / 无命令）；设计模式忽略被置为离屏空窗的陈旧 `RectInDesignMode`；`DrawTimeSigntureText` 文本 Y == 拍线 Y + 10（视口相对）。Release 全量 **695/695 通过**。

- **PERF-RND-014 / RND-17 — 已修复（2026-09-12）：静态 `SKTypeface` 缓存 + 实例级复用 `SKFont`/`SKPaint`，稳定态零 native 对象构造。**
  - **根因（静态确认 + 基准确认）：** `DefaultSkiaStringDrawing.MeasureString` 与 `Draw` 每次调用都 `new SKPaint()`、`new SKFont()`，并按 `(family, bold, italic)` 再执行一次 `SKTypeface.FromFamilyName(...)`（字体匹配 + 新包装 + 引用计数）；`Draw` 还额外为字符串重复测量一次（`out measureTextSize` 在 replay 被丢弃，`SkiaDrawCommandListReplay.cs:160` 的 `out _`），Underline/Strike 再加一个 `SKPaint`。两个实例（builder 的 measurer 由 `DefaultSkiaDrawingManagerImpl.cs:65` 每帧新建、replay 的 drawer 由 `SkiaDrawCommandListReplay.cs:59` 每次 present 新建）都是短命对象，因此"实例字段缓存"必须先补齐释放接线。
  - **处理：**
    1. `DefaultSkiaStringDrawing`：新增进程级 `ConcurrentDictionary<TypefaceKey, SKTypeface>`（键 = 解析后的 family + bold + italic，键空间有界；`SKTypeface` 不可变、可跨帧跨线程共享）；实例持有复用的 `SKFont`、填充 `SKPaint`、装饰线 `SKPaint`，每次调用只重设 `Typeface/Size` 与 `ColorF/Color/StrokeWidth`（标量写入）；`Dispose()` 实装为幂等释放这三个 native 对象。语义与原实现逐点对齐（含 null 文本、`FromFamilyName(null)` 回退、装饰线 paint 的默认 Fill/非抗锯齿）。
    2. 释放接线：`SkiaDrawCommandListReplay.Dispose()` 追加 `stringDrawing.Dispose()`；`DrawCommandListBuilder.Dispose()` 在置空 `stringMeasurer` 前 `(stringMeasurer as IDisposable)?.Dispose()`（builder 由工厂独占持有该 measurer，`StubStringMeasure` 等非 IDisposable 不受影响）。
  - **基准**（新增 `benchmarks/.../SkiaStringDrawingBenchmarks.cs`，真实 `SKBitmap`/`SKCanvas` 逐行复刻修复前后核心循环，字符串形态取自 `CommonHorizonalDrawingTarget`/`DurationSoflanDrawingTarget`；Release / DefaultJob + ShortRun(InProcess)，同机同参数，`[Params]` 16/64/256）：

    | 字符串数 | 方法 | 优化前 | 优化后 | 时间比 | 优化前分配 | 优化后分配 |
    | --- | --- | ---: | ---: | ---: | ---: | ---: |
    | 64 | Measure | 241.9 µs | 18.5 µs | 0.08× | 22.0 KB | 0 B |
    | 64 | Draw | 472.6 µs | 192.2 µs | 0.41× | 32.0 KB | 10.0 KB |
    | 64 | Frame（Measure+Draw） | 696.6 µs | 212.5 µs | 0.31× | 54.0 KB | 10.0 KB |
    | 256 | Measure | 789.2 µs | 63.1 µs | 0.08× | 88.0 KB | 0 B |
    | 256 | Draw | 1,547.5 µs | 681.9 µs | 0.44× | 128.0 KB | 40.0 KB |
    | 256 | Frame（Measure+Draw） | 2,371.8 µs | 760.3 µs | 0.32× | 216.0 KB | 40.0 KB |

    每字符串分配：Measure 由 352 B 降至 0 B；Draw 由 512 B 降至 160 B（残量来自 `SKCanvas.DrawText(string,...)` 的文本编码，非本次目标）。16 字符串行同比例（Measure 0.08× / Draw 0.44×）。
  - **验证：** Release 全量 **698/698 通过**，含 `SkiaRenderSmokeTests.SkiaStringDrawing_RendersAsymmetricGlyphUpright` 像素断言（复用后字形/朝向不变）与 `CommonHorizonalDrawingTargetTests`。
  - **未做/后续：** `Draw` 仍逐字符串经 `SKCanvas.DrawText(string,...)` 分配（~160 B/字符串），改走 `SKTextBlob` 或 `ReadOnlySpan<ushort>` 重载可消除；`MeasureString` 与 `Draw` 仍各测量一次同一字符串，实例级有界 memoize 可再省一次 shaping（需限制容量，避免 Comment 等可变文本无界增长）。实例级缓存与 PERF-RND-017/RND-20（replay 每帧新建）成对评审：RND-020 落地后这些缓存才真正跨帧存活。

- **渲染帧时间快照（2026-09-12，非审计项／正确性修复）：多线程渲染下裁判线高度抖动。**
  - **根因（静态确认）：** `CurrentPlayTime` 由 UI 线程的 15 ms `DispatcherTimer` 推进（`AudioPlayerToolViewerViewModel.cs:260-274` → `ScrollTo` → `ScrollViewer.cs:89-92`），而 `OnEditorRender` 在 Avalonia 渲染线程执行（`AvaloniaSkiaRenderControl.SkiaDrawOperation.Render` → `DefaultSkiaRenderContext.RenderFrame`）。同一帧内帧原点取 `GetViewportTGrid()`、裁判线取 `GetCurrentTGrid()`，是两次独立读取；读间被改写时线高 = `JudgeLineOffsetY + (ConvertToY(t2) - ConvertToY(t1))`，随交错情况在两个值之间跳变。
  - **处理：** `DrawingTargetContext` 新增帧快照 `CurrentTime`/`CurrentTGrid`；`OnEditorRender` 帧首只读一次播放时间并写入每个 soflan group 的 context；新增 `IFumenEditorDrawingContext.FrameTime`/`FrameTGrid`（帧外回退活值）。渲染期时间消费者全部改用快照：裁判线、player location、hit effect、拍线、playable area 采样、beam、projectile。随之删除已无调用者的 `GetViewportTGrid()`/`GetViewportAudioTime()`。
  - **验证：** 新增 `tests/OngekiFumenEditor.Avalonia.Tests/Graphics/DrawingFrameSnapshotTests.cs` 3 项（快照覆盖活值、未填充回退、`DrawJudgeLineHelper` 输出取快照而非活时间）；Release 全量 **698/698 通过**。
  - **未做/后续：** `ViewRelativeOriginY` 仍按套用 `EditorOffsetMs` 的 tGrid 计算，故 `EditorOffsetMs != 0` 时裁判线的常量偏置保持不变（本次只消除抖动）；`Render(TimeSpan)` 与 `RenderFrame` 的互斥、`DrawJudgeLineHelper.vertices` 共享缓冲、`CurrentPlayTime` 的原子发布仍未加固。

### Dormant/合并项

- `RND-04` 已并入 PERF-RND-003，不重复计数。
- `SkiaRenderControl_OpenGL.cs:80-103`、`DirectX.cs:61-85` 的每帧 GPU→CPU copy，以及 `GlxContext.cs:122-143` 未关闭 X display 属被当前 csproj `:67-71` 排除的 dormant 路径，不按活动问题排名。

---

## 5. EDT：时间轴、交互、选择与文档生命周期

- **PERF-EDT-001 / EDT-REC-OWN-001 — P2，池对象所有权。** `S/Modules/FumenVisualEditor/Base/PlayerLocationRecorder.cs:14-19,29-41` 把 `defaultRecord` sentinel 放入排序 list；`Trim:35-42` 在 `list.Count>0` 时会把 sentinel 退回共享 `ObjectPool<Record>`，之后 `Commit:45-58` 可能再次租到并改变同一对象，`GetLocationXUnit:62-90` 仍把它当基线。只在 `Count>1` 时 trim，Clear 时单独重置 sentinel。
- **PERF-EDT-002 / EDT-VM-ASYNC-002 — P2，边缘自动滚动任务潮。** `S/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.UserInteractionActions.cs:1491-1495` 每个边缘 pointer event 创建随机 generation、`Task.Delay(1000/60)` 和递归 `OnMouseMove`；`:1537-1549` 重复边缘工作，旧任务仍会唤醒再比较共享 id。用一个 CTS/调度循环只保留最新位置。
- **PERF-EDT-003 / EDT-SEL-SCAN-003 — P2，拖动时全量选择扫描。** `.../FumenVisualEditorViewModel.UserInteractionActions.cs:119-120` 对 `GetAllDisplayableObjects()` 做 `OfType/Where/Distinct`，拖动 `:1543-1549` 每次 `SelectObjects.ToArray()`。在拖动开始建立索引/快照，移动时只更新增量集合。
- **PERF-EDT-004 / EDT-DOCK-SNAP-004 — P2，拖动吸附 LINQ 排序。** `S/Modules/FumenVisualEditor/ViewModels/Interactives/Impls/DockableObjectInteractiveAction.cs:14-17,29-43` 每次 `OnMoveCanvas` 枚举全部 dockable，按距离 `GroupBy`/排序，成本 O(L log L)+迭代器分配。缓存活动拖动的 lane 几何或用一次线性最近点扫描。
- **PERF-EDT-005 / EDT-CV-01 — P2，插值命令重复排序。** `S/Modules/FumenVisualEditor/Commands/OgkrImpl/InterpolateAll/InterpolateAllCommandHandlerBase.cs:47-54,64-90` 已物化 generated lanes 后仍为每个 dockable 重算 XGrid、`OrderBy` 并选首项。复用已有区间数据并做一次 min-X 扫描，保留 tie 语义。
- **PERF-EDT-006 / EDT-MODEL-EVENT-005 — P2，持久模型订阅泄漏。** `S/Modules/FumenVisualEditor/Models/EditorProjectDataModel.cs:35` 及旧 `EditorProjectDataModel_V0_5_2.cs:41` eager-create `EditorSetting`；其 ctor 在 `Models/EditorSetting.cs:12-15` 订阅 singleton `EditorGlobalSetting.Default.PropertyChanged`，解除只在 `:343-350`，模型不 IDisposable，`EditorContext.Dispose:89-102` 不处理。每次反序列化/clone 可留下全局 subscriber。让 model/setting 具备明确 Dispose，或移除持久数据对象的运行时订阅。
- **PERF-EDT-007 / EDT-TGRID-LINEAR-009 — P2，时间轴线性查找。** `S/Modules/FumenVisualEditor/TGridCalculator.cs:60-70` 对 sorted Y 使用 `LastOrDefault(x=>x.Y<=pickY)`；调用在 `UserInteractionActions.cs:1665-1686`、`DefaultObjectInteractiveAction.cs:143-155`，BPM conversion `TGridCalculator.cs:26-34,41-54` 也有 predicate scan。改为带边界处理的 keyed binary search。
- **PERF-EDT-008 / EDT-SVG-REDUNDANT-006 — P3、条件项。** 在 `#if ENABLE_SVG_PREFAB_OBJECTS` 下，`S/Modules/FumenVisualEditor/Base/SvgProjectFileImporter.cs:24-30,35-47,60-69` 对同一 SVG 初读/哈希、已有 entry 重读重解析、新 entry 写后再读重解析。只有启用该 feature 且 readback policy 可放宽时才优化。
- **PERF-EDT-009 / EDT-DROP-ALLOC-007 — P3，一次性拒绝路径分配。** `S/Modules/FumenVisualEditor/Base/DropActions/EditorAddObjectDropAction.cs:12-21` 先 `GetDisplayObject()` 再在 `:20-21` 判断 duration 越界，拒绝 drop 的对象立即丢弃。把范围检查移到 factory 前。
- **PERF-EDT-010 / EDT-CLIP-RETAIN-010 — P2，singleton clipboard 根住关闭文档。** `S/Modules/FumenVisualEditor/Kernel/DefaultImpl/DefaultFumenEditorClipboard.cs:24-33,54-56,128-165` 保存 `sourceEditor` 和复制的对象图；没有 document-destroy 清理，关闭后可通过 singleton 保留 VM/context/fumen/audio，直到下一次 Copy。关闭源 editor 时清 clipboard，或存脱离 editor 的最小 paste metadata。
- **PERF-EDT-011 / EDT-TOAST-QUEUE-008 — P3，Toast burst 排队。** `S/Modules/FumenVisualEditor/Views/UI/Toast.xaml.cs:61-72,85-95` 每条消息创建 CTS/异步状态，首个 `Dispatcher.UIThread.InvokeAsync` 无取消 token；版本检查只能防止过时内容显示，不能阻止队列中的 closure。合并一个 pending UI callback；现有 finally `:114-127` 可防永久泄漏。

---

## 6. DAT：模型、集合、曲线与几何

> 本分区是“实现文件深读 + 调用链确认”的 targeted audit；不把未逐行深读的叶文件宣称为完整审计。

- **PERF-DAT-001 / DAT-01 — P1，零步/反向无限循环。** `S/Base/EditorObjects/InterpolatableSoflan.cs:32-35` 接受任意整数，`:109-112` 用 `1920/count` 的截断步长推进。count=0 可除零；count<0 向反方向走；count>1920 步长为 0，循环永不接近终点。校验 `1..1920`，并对持久化/UI 输入做防御性拒绝。
- **PERF-DAT-002 / DAT-02 — P1，下界错误和 dense-key 扫描。** `S/Base/Collections/Base/SortableCollection.cs:71-83` 用返回“最后一个相等 key”的 `BinarySearchBy` 同时作 min/max；lower bound 会丢掉同 TGrid 的更早对象，且 helper 会扫描整段相等 key。实现独立 lower_bound/upper_bound。
- **PERF-DAT-003 / DAT-03 — P2，Hold hit-effect 重放历史 tick。** `S/Base/OngekiObjects/Hold.cs:146-169` 从 Hold 起点逐 tick 推到 `minTGrid`；调用 `S/Modules/FumenVisualEditor/Graphics/Drawing/Editors/DrawHitObjectEffectHelper.cs:118-124` 在 preview effect 开启时每帧触发。按 BPM segment 算首个可见 tick 或缓存 tick positions。
- **PERF-DAT-004 / DAT-05 — P2，墙 lane 查找重复线性化。** `S/Base/OngekiObjects/ConnectableObject/ConnectableStartObject.cs:308-337,372` 先 O(children) `IsPathVaild`，再 binary search，再 `GetRange` 分配；`DrawPlayableAreaHelper.cs:194-214,458-477,504-505` 对每个 sample 又扫描/验证。缓存有效性和 index/range view。
- **PERF-DAT-005 / DAT-07 — P2，BPM“缓存”仍遍历全表。** `S/Base/Collections/BpmList.cs:148-159` 每次 `GetCachedAllBpmUniformPositionList` 都对所有 BPM 做 `Aggregate`；调用 `S/Modules/FumenVisualEditor/TGridCalculator.cs:26-34,41-53` 广泛使用。用 monotonic dirty/version，命中时不重算。
- **PERF-DAT-006 / DAT-08 — P2，区间树变更触发整树重建。** `S/Base/Collections/Base/IntervalTreeWrapper.cs:40-50` 坐标变更线性 remove/add 并 dirty；`.../RangeTree/IntervalTreeNode.cs:54-105` rebuild 时递归创建 endpoint/inner/left/right lists 和 node，`Release:44-52` 只清引用。拖拽编辑时批量更新或增量维护索引，并复用 scratch。
- **PERF-DAT-007 / DAT-10 — P2，未使用 interval cache。** `S/Base/Collections/IndividualSoflanAreaListMap.cs:12-17,70-85` 维护 `cacheTree`，但没有查询/读取调用；每个 area 仍付出 RangeValuePair/event subscription 和变更 remove/add。删除 dead cache 或接入真实查询。
- **PERF-DAT-008 / DAT-11 — P2，默认 RecordId 分配二次复杂度。** `S/Base/Collections/ConnectableObjectList.cs:22-34` 每个负 ID start 都枚举所有 start 并求 Max；批量生成 N 个 lane 为 0+1+… 扫描。维护 max-ID/free-ID allocator。
- **PERF-DAT-009 / DAT-12 — P2 潜在正确性项（无当前直接 caller）。** `S/Base/Collections/BulletPalleteList.cs:42-44` `this[int] => this[index]` 无限递归，任何 indexer consumer 会 stack overflow。修复 backing ordered list 或移除错误的 `IReadOnlyList` 语义；因未找到直接 caller，不把它当活动热路径。
- **PERF-DAT-010 / DAT-13 — P2，Meter 查询重复排序/线性扫描。** `S/Base/Collections/MeterChangeList.cs:69-86` 每次枚举对已有 sorted list `OrderBy`，GetMeter/GetPrev/GetNext 再线性 Last/First。直接枚举并提供 binary predecessor/successor。

### 纠正/不单列

`DAT-04` 的 Bezier scratch array 与 `PERF-SVC-002` 合并；`DAT-06` SVG recolor 的 LINQ/closure/iterator 分配仅在 `ENABLE_SVG_PREFAB_OBJECTS` 开启时相关，当前 checkout dormant，列为条件性 P3 opportunity；`DAT-09` 的 `OrderByDescending(...).FirstOrDefault()` 在现代 .NET 对 extremum 有 single-pass 优化，撤销 O(k log k) 结论。

---

## 7. PRS：解析、标准化、格式化与 SVG 转换

- **PERF-PRS-001 / PRS-01 — P2，PROGJUDGE_BPM=0 可卡死统计。** `S/Parser/Ogkr/FumenStatisticsCalculator.cs:75-80` 在 Hold + progress BPM 为 0 时以 0 倍增，条件永不结束；输入入口 `S/Parser/Ogkr/CommandParserImpl/MetaInfo/ProgJudgeBpmCommandParsers.cs:11-14` 接受 0。校验有限且正的 judgment BPM，并防 tick 溢出。
- **PERF-PRS-002 / PRS-02 — P2，同步转换伪异步阻塞 UI。** `S/Modules/FumenConverter/ViewModels/FumenConverterViewModel.cs:127-134` 直接 await conversion；`S/Parser/DefaultImpl/DefaultOngekiFumenFormatter.cs:29-75` 同步统计/格式化/UTF8 后才 `Task.FromResult`；SVG handler `:42` 同样同步。对快照/锁定模型做受控后台 CPU 工作。
- **PERF-PRS-003 / PRS-03 — P2，引用解析反复全表扫描。** OGKR `TapCommandParser.cs:16-19`、`HoldCommandParser.cs:16-20`、`LaneCommandParser.cs:51-52`、`WallCommandParser.cs:43-44`、`BeamCommandParser.cs:64-65`、`Editor/CurveControlCommand.cs:20-27` 以及 Bell/Bullet/Nyageki 对 lane/palette 做 FirstOrDefault 全扫描；标准化/SVG clone 会再次支付。建立保持 first-duplicate 语义的 RecordId/StrID 索引。
- **PERF-PRS-004 / PRS-04 — P2，InterpolateAll 二次查找。** `S/Utils/Ogkr/InterpolateAll.cs:15-23` 对每个 laneMap 结果回找 curveStarts，`:27-34` 对每个 tap/hold/end 调 `Any` 并为每个 Hold 分配二项数组；调用 `S/Utils/Ogkr/StandardizeFormat.cs:160-165` 和 command handler `:67-87`。保留 before/generated pair 并用 RecordId HashSet。
- **PERF-PRS-005 / PRS-05 — P2，Nyageki formatter O(C²)。** `S/Parser/DefaultImpl/Nyageki/DefaultNyagekiFumenFormatter.cs:125-141` 对每个 curve child `Children.ToList().FindIndex`。沿 outer enumeration 携带 index，OGKR formatter `:195-206` 已采用类似方式。
- **PERF-PRS-006 / PRS-06 — P2，GetData<T> 重复物化整行。** `S/Parser/Ogkr/CommandArgs.cs:52-86` 每次按 index 先生成完整 typed array；Tap/Hold 又分别请求 float/int/string，造成 token、array 和转换重复。按需解析 token/span。
- **PERF-PRS-007 / PRS-07 — P2，全图 serialize→parse clone。** `S/Utils/Ogkr/StandardizeFormat.cs:29-35` 序列化完整 OGKR 后重新解析；`service.cs:70-81` 又序列化，SVG generator `:29-34` 即使默认 Soflan 不变也 clone。引入 typed snapshot/read-only SVG model，避免双 graph 常驻。
- **PERF-PRS-008 / PRS-08 — P3，SVG point 字符串/数组。** `S/Modules/PreviewSvgGenerator/Kernel/DefaultPreviewSvgGenerator.cs:112-123,164-175` 每点建字符串/数组再 Join，最终 `:68` ToString+UTF8。改为直接 append 到 builder/writer。
- **PERF-PRS-009 / PRS-09 — P3，坐标 Split。** `S/Parser/DefaultImpl/Nyageki/CommandImpl/ParserUtils.cs:27-36` trim char[]、Split、substring，调用 `LaneCommandParser.cs:25-32`、`CurveControlPointCommandParser.cs:35-39`。使用 Span/TryParse wrapper。

---

## 8. AUD：应用层音频、波形与音频设置

- **PERF-AUD-001 / AUD-01 — P1，拖动控件每个值都 durable save。** `S/Kernel/Audio/NAudioImpl/NAudioManager.cs:42-65` 每个音量 setter 调 `AudioSetting.Default.Save()`；`S/Modules/AudioPlayerToolViewer/ViewModels/AudioPlayerToolViewerViewModel.cs:160-228` 每个 resample/scale/duration/offset/FPS setter 保存。`SettingModelBase.cs:17-20` 直达保存，D 的 `DesktopSettingManager.cs:45-60,145-165` 复制/序列化整 map、flush-to-disk、replace。拖动时 debounce 或在 drag complete/Apply 保存。
- **PERF-AUD-002 / AUD-02 — P1，整轨 PCM 多重物化。** `S/Kernel/Audio/NAudioImpl/Music/DefaultMusicPlayer.cs:102-149` 通过 `ToWaveProvider().ToArray()` 保留完整 byte[]；`S/Kernel/Audio/NAudioImpl/Utils/MethodExtensions.cs:13-72` 先收集 chunks 再建 final float/byte[]；`AudioCompatibilizer.cs:40-70` 可能再建 resampled 与 mono→stereo arrays。长轨产生 LOH/峰值内存和启动延迟。改 bounded/ring stream 或一次写入 presized final store。
- **PERF-AUD-003 / AUD-03 — P2，毫秒波形点对象。** `S/Kernel/Audio/SamplePeak/DefaultSamplePeak.cs:9-42` 每 1ms 创建 `PeakPoint` 和 `float[channels]`；默认 `ResampleSize=0` 于 `Models/Settings/AudioPlayerToolViewerSetting.cs:16-18`；`DefaultWaveformDrawing.cs:100-141` 每帧扫描可见点并发两顶点。使用连续 min/max buffer、分辨率层级和 geometry cache。
- **PERF-AUD-004 / AUD-04 — P2，1ms DispatcherTimer 与临时数组。** `S/Kernel/Audio/DefaultCommonImpl/Sound/DefaultFumenSoundPlayer.cs:47-51,345-374` 每 1ms `UpdateInternal`，duration-stop 路径 `:360` `Where(...).ToArray()`。按下一个事件/音频时间调度，复用 scratch，避免把 dispatcher 当音频时钟。
- **PERF-AUD-005 / AUD-05 — P2，低采样率导致 step=0。** `S/Kernel/Audio/SamplePeak/DefaultSamplePeak.cs:15-27` 以 `(int)(sampleRate*0.001f*channels)` 为步长，`AudioSetting.cs:20-22` 和 `AudioSettingView.axaml:39-45` 未限制输入；小于 `1000/channels` 时步长可截为 0。验证探针：499Hz/2ch 的整数步长为 0。边界校验并 clamp 至 1。
- **PERF-AUD-006 / AUD-06 — P2，ACB 候选记录先全量缓冲。** `S/Kernel/Audio/AcbConverter.cs:121-165` 先按 `FileLength` 租/读完整 record，才在 `:144-145` 探测 HCA；非 seekable 输入 `:198-213` 还先复制完整 stream。先探测有界 header，匹配后流式读，非 seekable 用有界临时文件。

---

## 9. IO：项目文件、存储、扫描和序列化

- **PERF-IO-001 — P1，AWB 替换失败破坏旧文件。** `S/Modules/FumenVisualEditor/Base/ExternalAwbImporter.cs:146-149,164-171` 假定 staging copy transactional，但 `S/Platforms/Services/FileSystem/Providers/AvaloniaStorageProviderSimpleFile.cs:145-163` 直接 truncate/write；取消/失败可留下 partial AWB，且 importer 不恢复。使用同目录临时文件+atomic commit 或 restore。
- **PERF-IO-002 — P1，打开单 chart 却 clone 全 game root。** `S/Modules/OgkiFumenListBrowser/ViewModels/OgkiFumenListBrowserViewModel.cs:530-585` 重新打开 root bookmark，`AvaloniaStorageProviderFileSystemBuilder.cs:65-75,104-126` 递归索引所有目录/文件。改 file-level bookmark/直接 locator。
- **PERF-IO-003 — P1，刷新每次重建完整树。** `.../OgkiFumenListBrowserViewModel.cs:291-294` 先 Refresh，builder `:95-119,190-204` 全量枚举和 metadata，scanner `:323-339` 才过滤。增量刷新或枚举时过滤。
- **PERF-IO-004 — P1，UI continuation 同步 link/attribute 检查。** picker `S/Modules/FumenVisualEditor/ViewModels/Dialogs/AvaloniaEditorProjectSetupFilePicker.cs:29-33` 进入 builder；`AvaloniaStorageProviderFileSystemBuilder.cs:180-195,254-263` 同步 `IsLocalLink/File.GetAttributes`。移出 UI thread。
- **PERF-IO-005 — P1，浏览器结果列表全量布局。** `S/Modules/OgkiFumenListBrowser/Views/OgkiFumenListBrowserView.axaml:309-459` 使用 ScrollViewer+ItemsControl+StackPanel，结果和嵌套 song/difficulty template 全量 materialize。启用虚拟化或分页。
- **PERF-IO-006 — P2，目录项任务无界排队。** `AvaloniaStorageProviderFileSystemBuilder.cs:112-119` 对每个 directory item 建 Task 后 WhenAll，递归重复；semaphore 只限制 provider call，不限制 task/object 数。使用有界 worker queue。
- **PERF-IO-007 — P2，difficulty 读取绕过全局并发上限。** `S/Modules/OgkiFumenListBrowser/OgkiFumenListBrowserScanner.cs:155-168` 内层 `Task.WhenAll`，外层四项限制在 `:441-458` 之外失效。统一 bounded queue。
- **PERF-IO-008 — P2，jacket 加载完整读/哈希后 UI 解码。** `OgkiFumenListBrowserView.axaml.cs:18-27` 由 viewport 触发，decoder `:33-45` 在 cache lookup 前读/哈希完整源，VM `:643-648` await 后同步 `Bitmap.DecodeToWidth`。限流并将 hash/decode 放后台。
- **PERF-IO-009 — P2，duration 目的导致三处重复音频复制解码。** setup `EditorProjectSetupDialogViewModel.cs:360-375`、creation `EditorProjectCreationTransaction.cs:451-477`、attach `FumenVisualEditorViewModel.cs:261-279` 均全量 MemoryStream/解码。复用已验证 metadata/player。
- **PERF-IO-010 — P2，随机 seek 将非 seekable 流逐步物化。** `SeekableStream.cs:11-16,173-202` 从 byte 0 扩缓存；`AwbContentComparer.cs:38-50,70-87` 先随机采样再完整比较。使用 bounded spool 或禁用非 seekable 随机采样。
- **PERF-IO-011 — P2，保存同时保留多份完整项目数据。** `EditorProjectDataUtils.cs:303-324,326-350` 同时保留 cloned project、serialized fumen/project 和 rollback 原文。改磁盘 staging，避免大图表峰值。
- **PERF-IO-012 — P2，每次保存 clone 固定 256KiB+ToArray。** `EditorProjectDataUtils.cs:303-305` 每次调用 clone；`EditorProjectFileManager.cs:65-70` 先固定 MemoryStream 再 `ToArray`。使用 typed clone/合适容量/池。
- **PERF-IO-013 — P2，picker-owned file 未释放。** `ExternalAwbImporter.cs:88-106,99-176` 不 Dispose `picked`；picker `AvaloniaEditorProjectSetupFilePicker.cs:78-96` 返回独立 wrapper。导入完成或失败都释放 owner。
- **PERF-IO-014 — P2，删除失败被吞并丢失重试机会。** `ExternalAwbImporter.cs:148-160` 把 staging 置 null，helper `:246-255` swallow delete errors。保留失败路径并在 finally/启动清扫重试。
- **PERF-IO-015 — P2，AWB 验证 WAV 双份 buffer。** `ExternalAwbImporter.cs:219-236` 生成 WAV，`B/Platforms/Services/FileSystem/Providers/BrowserTemporaryFolderProvider.cs:130-149` MemoryStream+ToArray。使用 chunked temp stream。
- **PERF-IO-016 — P2，path-keyed lock 表单调增长。** `S/Platforms/Services/FileSystem/Providers/TemporaryFolderProviderBase.cs:156-170` `GetOrAdd` 每个 relativePath 建 `SemaphoreSlim`，Delete/Clear `:312-320,85-89` 后不移除。用带等待者引用计数的 keyed-lock 生命周期，不能简单无条件 TryRemove。
- **PERF-IO-017 — P2，Setup 每次按根递归寻找资源。** `EditorProjectSetupDialogViewModel.cs:742-750,764-770` 在 audio/AWB/fumen resolve 时递归 project root/source roots。建立一次索引或按 locator 直接解析。
- **PERF-IO-018 — P2，未缓存文件读取双份完整复制。** `AvaloniaStorageProviderSimpleFile.cs:240-249` MemoryStream 收全文件后 `ToArray` 再复制。使用直接返回 owner buffer/流式 API。
- **PERF-IO-019 — P2，文件夹选择同步 durable save。** `S/Modules/OgkiFumenListBrowser/ViewModels/OgkiFumenListBrowserViewModel.cs:370-397` 调 `setting.Save()`；D/DesktopSettingManager `:38-59,145-171` 同步序列化/flush/replace。debounce 或后台持久化。
- **PERF-IO-020 — P2，关键字过滤重复 lower-case 和 Levenshtein 数组。** `S/Modules/OgkiFumenListBrowser/ViewModels/OgkiFumenListBrowserViewModel.cs:180-206,299-305,702-745` 在 UI thread 计算完整 O(mn)，每次比较建两个 int arrays。缓存 normalized fields、threshold-banded/off-thread。
- **PERF-IO-021 — P2，打开项目做三遍 DFS。** `FumenVisualEditorProvider.ProjectIO.cs:38-40,295-300` 分别找 project/fumen/audio，均经 `EditorProjectPathResolver.cs:30-49`，child access `AvaloniaStorageProviderSimpleDirectory.cs:33-35` 还复制 arrays。单次 traversal 建 extension index。

---

## 10. UI：控件、属性面板、行为与主题

- **PERF-UI-001 — P2，Redo snapshot 累积。** `S/Modules/FumenObjectPropertyBrowser/ViewModels/MultiLanesOperationViewModel.cs:76-80,96-98` CombineLanes execute 追加 `RedockedObjects`，undo 遍历但不清；重复 Ctrl+Z/Y 保留 duplicate refs。每次 execute clear/rebuild immutable snapshot。
- **PERF-UI-002 — P2，selection filter 每项重复统计。** `S/Modules/FumenEditorSelectingObjectViewer/ViewModels/SelectionFilterViewModel.cs:113-122` 每次 refresh 跑所有 option；`.../SelectionFilterOptions.cs:540-572` 对每个 bullet/bell 扫所有 palettes 并通知。一次 refresh 聚合并单次发布。
- **PERF-UI-003 — P2，拖动 insertion adorner 重建。** `S/UI/Behaviors/DataGridRowReorderBehavior.cs:204-216,336-354,366-384` 每个 DragOver 都遍历/order realized rows，目标未变也清旧 adorner、new/mount Border。缓存 target/position 并复用 indicator。
- **PERF-UI-004 — P2，整图 checker 在 UI thread。** `S/Modules/FumenCheckerListViewer/ViewModels/FumenCheckerListViewerViewModel.cs:113-120` 同步枚举全部 rules；`WallConflictCheckRule.cs:273-317` nested wall/line work 和临时 arrays。安全 snapshot 后后台计算，批量提交结果。
- **PERF-UI-005 — P2，palette option collection 逐项通知。** `S/Modules/FumenEditorSelectingObjectViewer/Base/SelectionFilter/SelectionFilterOptions.cs:501-513` RemoveAt loop+逐项 Add，handler `:443-460` 每项触发 subscription/summary/selection notifications。使用 diff 或单次 reset/replace。
- **PERF-UI-006 — P3，Enum.GetValues 重复。** `S/Modules/FumenBulletPalleteListViewer/ValueConverters/EnumValuesGenerator.cs:6-11` 每次 conversion `Enum.GetValues`；四个 palette-grid binding 在对应 axaml `:101,118,135,143`。按 enum Type 缓存只读数组。

---

## 11. SVC：设置、调度、日志与公共工具

- **PERF-SVC-001 / SVC-01 — P1，共享 Grid 的 PropertyChanged 账本冲突。** `S/Utils/PropertyChangedBaseExtensionMethod.cs:27-45` 用 `RuntimeHelpers.GetHashCode(source)` 作全局 key，只保留一个 WeakReference delegate；`S/Base/OngekiObjects/OngekiMovableObjectBase.cs:22-35`、`OngekiTimelineObjectBase.cs:44-56` 的 Copy 会共享 XGrid/TGrid，第二 owner 覆盖账本，替换值时可能解除另一 owner 的委托，并让旧 owner 继续收到无效通知。按 owner 保存实际 delegate 或以 owner+source 为 key。
- **PERF-SVC-002 / SVC-02 — P2，Bezier scratch 数组每个 sample 创建。** `S/Utils/BezierCurve.cs:9-20` 每次 `CalculatePoint` new/copy `Vector2[points.Count]`；`S/Base/OngekiObjects/ConnectableObject/ConnectableChildObjectBase.cs:168-200` 按 CurvePrecision 每 sample 调一次，cache 在 `:103-138` 失效时拖动/插值反复发生。整条曲线复用 scratch Span/ArrayPool；此项已吸收 DAT-04。
- **PERF-SVC-003 / SVC-03 — P2，临时路径 keyed lock 不回收。** `S/Platforms/Services/FileSystem/Providers/TemporaryFolderProviderBase.cs:19,156-170` 和 Delete/Clear `:312-320,85-89` 为每个写/删 relativePath 永久保留 string+SemaphoreSlim，GUID 临时文件使表单调增长。实现等待者计数生命周期锁或固定分片锁。
- **PERF-SVC-004 / SVC-04 — P2，5ms scheduler 任务潮。** `S/Kernel/Scheduler/SchedulerManager.cs:74-86,117-159` 每个 scheduler 固定 5ms 扫描，到期 `StartNew(...).Unwrap`、状态包装、ContinueWith；具体 `S/Kernel/Audio/NAudioImpl/Music/DefaultMusicPlayer.cs:56-58,172-186,255-270` 请求 60Hz。按最近 deadline 等待并在注册变化唤醒，保留现有不重入/取消/退出等待。
- **PERF-SVC-005 / SVC-05 — P2，无效 auto-save 间隔造成持续到期。** `S/Models/Settings/EditorGlobalSetting.cs:17-18` 对应自由 TextBox `S/UI/.../FumenVisualEditorGlobalSettingView.axaml:31-35`；`DefaultEditorDocumentManager.cs:163-170` 直接 `TimeSpan.FromMinutes`，scheduler `:107-115` 比 elapsed。0/负数会每轮到期，触发序列化/I/O/log。设置边界拒绝非正值。
- **PERF-SVC-006 / SVC-06 — P2，日志 drain 竞态。** `S/Utils/Log.cs:138-150` volatile `isRunning` 的 check+set 非原子，多个 Emit 可启动多个 `Task.Run(ProcessLogRecords)`；`:153-175,214-219` 还可能在另一 worker 写入时误报 drain 完成；Desktop console `D/Platforms/Services/Logging/DesktopConsoleLogOutput.cs:10-23` 并发改全局颜色。用 Interlocked/单 consumer 协议。
- **PERF-SVC-007 / SVC-07 — P2，共享 StringBuilder 并发污染。** `S/Utils/Logging/MELTransportLoggerProvider.cs:43-55` 所有 TransportLogger 共用非 ThreadStatic builder，生产者直接 Clear/Append/ToString；并发 exception log 可混合/截断。改为局部/线程局部 builder 或同步格式化。
- **PERF-SVC-008 / SVC-08 — P2，日志 outputs 普通 List 并发修改。** `S/Utils/Log.cs:18,30,42-52,61-65,153-166` worker 枚举普通 List，`D/Utils/ConsoleWindowHelper.cs:50-56` UI thread 增删 sink；版本异常被空 catch 吞掉，记录可能丢失。不可变 snapshot 或锁内复制、锁外写。

明确不列：ImageLoader lost-wakeup、AbortableThread/Md5Helper/WithTimeout、reflection cache、DiscardTemporaryFolderProvider 的 latent 问题均未找到当前生产热 caller；Scheduler 的 overlap/cancel/await 本身已有保护。

---

## 12. DSK：Desktop、CommandLine、更新与发布

- **PERF-DSK-001 — P1，文件日志每条 open/bytes/flush。** `D/Platforms/Services/Logging/DesktopFileLogOutput.cs:70-77,156-174` 每记录串接 async Task、new FileStream、UTF8.GetBytes、Write、Flush；Desktop app `OngekiFumenEditorDesktopApp.cs:75-84` 最低级别 Debug。使用长生命周期 stream + 有界队列/批量 flush，退出 drain。
- **PERF-DSK-002 — P1，坏 setting.json 在 UI 线程 sync-over-async。** `D/Platforms/Services/Settings/DesktopSettingManager.cs:125-141` `Task.Run(...ShowMessageDialog...).Wait()`；G 的 `DefaultDialogManager.cs:54-63` 把对话框派回 UI。UI 首次加载时会互相等待。异步传播错误或使用不依赖 Dispatcher 的原生提示。
- **PERF-DSK-003 — P1，FastOpen 音频完整解码两次。** `D/Modules/FumenVisualEditor/FastOpen/DesktopFastOpenService.cs:155-173,214-222` 先完整 copy/构造 player 只取 Duration，随后 editor `S/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.cs:261-279` 再 Copy/LoadAudio。转交已加载 player 或让 attach 后唯一读取 Duration。
- **PERF-DSK-004 — P2，CLI 冷启动完整 Avalonia lifetime。** `Avalonia/src/OngekiFumenEditor.Avalonia.CommandLine/Program.cs:8-11`→`D/DesktopCommandLineHost.cs:11-17`→BuildAvaloniaApp/ClassicDesktopLifetime；`D/Program.cs:307-316` 平台/字体/trace，app `:63-84` 注册完整服务图。`--help`/参数错误也付出窗口系统和 DI。为 CLI 建最小 Generic Host，按命令按需初始化。
- **PERF-DSK-005 — P2，updater 异步接口同步阻塞。** `D/CommandLine/Commands/Updater/DefaultProgramUpdateService.cs:24-51,80-158` 同步 GetFiles/list 和 move/copy/delete，几乎不检查 token；`DefaultProgramUpdateProcessEnvironment.cs:21-32` 固定 Wait 30s、Kill 后无超时 WaitForExit。使用 WaitForExitAsync、linked token/timeout 和安全事务边界取消。
- **PERF-DSK-006 — P2，FastOpen 只取首个目录却先全量数组。** `D/Modules/FumenVisualEditor/FastOpen/DesktopFastOpenAudioResolver.cs:71-83` `Directory.GetDirectories(...AllDirectories).FirstOrDefault()`。改 `EnumerateDirectories` 并保留 root boundary/cancellation。
- **PERF-DSK-007 — P2，SVG rasterizer 多份峰值复制且像素无预算。** `D/CommandLine/Commands/Svg/DefaultSvgRasterizer.cs:18-25,38-62` SVG UTF8→string、SKSvg parse、XDocument/MemoryStream、native PNG SKData→managed byte[]，尺寸只到 int.MaxValue。用 long 像素/字节预算、单一解析/stream，直接把 SKData 写 FileStream。
- **PERF-DSK-008 — P2，ACB/Jacket 临时目录成功路径不清理。** `D/CommandLine/Commands/Acb/DefaultAcbGenerateService.cs:43-49,75-84`、`Jacket/DefaultJacketGenerateService.cs:48-61,100-110,132-140,196-197` 创建目录/asset 文件后直接返回；未找到 Desktop ClearAsync caller，根目录为 `%TEMP%/NagekiFumenEditorTempFolder`。以 try/finally 清理，启动清扫只作兜底。

---

## 13. WEB：Browser、OPFS、WASM 与 JavaScript 生命周期

- **PERF-WEB-001 — P2（大 staging 时可升 P1），启动被旧下载清理串行阻塞。** `B/wwwroot/main.js:61-78` 在 import/create .NET 前 await OPFS init；`B/wwwroot/opfsBrowser.js:545-554` 递归删除 `.ongeki-opfs-downloads`。把清理延后到 runtime ready/background。
- **PERF-WEB-002 — P2，Object URL 会话内保留。** `B/wwwroot/opfsBrowser.js:244-259,440-443` 把 preview/download URL 放进 retainedObjectUrls，只有 `:887-892` beforeunload 才 revoke。按 preview/download 生命周期及时 revoke。
- **PERF-WEB-003 — P2，Browser 文件日志逐记录排队/复制。** `B/Platforms/Services/Logging/BrowserFileLogOutput.cs:60-67,109-118` 每条串链并 UTF8 分配；`B/wwwroot/logFileSystem.js:62-75` 复制 Uint8Array；`opfs.js:231-240` 每条完整 append transaction。使用 bounded batch queue/backpressure。
- **PERF-WEB-004 — P2，OPFS read 多份 chunk copy。** `B/wwwroot/opfsBrowser.js:786-799` 每 chunk 建 ArrayBuffer/Uint8Array；`BrowserOpfsReadStream.cs:80-89` 再 GetPropertyAsByteArray/复制，service `:254-279` 每 256KiB 重复。使用 returned buffer 的 owner/view。
- **PERF-WEB-005 — P2，temporary file 全量缓冲。** `B/Platforms/Services/FileSystem/Providers/BrowserTemporaryFolderProvider.cs:102-166` MemoryStream/full ToArray；`temporaryFileSystem.js:80-97` 与 `opfs.js:210-228` 继续整数组复制。引入 chunked stream handle。
- **PERF-WEB-006 — P2，关闭竞态保留 polling/JS I/O。** `B/Modules/BrowserOpfsBrowser/ViewModels/BrowserOpfsBrowserViewModel.cs:178-195` 初刷 await 后无条件 StartPolling；`:437-452` token 只给 timer，`:466-521` service calls 不带 cancellation。close 后可能更新已关闭 VM。重检 isWindowOpen、保存/await task 并把 token 传到底层。
- **PERF-WEB-007 — P2，5秒无变化重排 O(n²)。** `BrowserOpfsBrowserViewModel.cs:637-658` 行/树对 each item 做 `IndexOf/IndexOfPath`，循环在 `:445-451`。用 keyed diff/index 并跳过未变化项。
- **PERF-WEB-008 — P2，bulk selection O(n²)+通知风暴。** `BrowserOpfsBrowserViewModel.cs:240-245,316-322` 逐行 set，`BrowserOpfsEntryViewModel.cs:42-50` 回调，VM `:744-755` 每次重新统计 collection。批量抑制 callback，最后一次发布。
- **PERF-WEB-009 — P2，download planner ancestor 扫描。** `BrowserOpfsDownloadPlanner.cs:30-39` 对每个 path 用 `Any` 扫之前 parents，第一次 async picker 在 `BrowserOpfsService.cs:81-89` 之前就承担 O(n²)。缓存 canonical path/ancestor index；不要假定每次 NormalizePath 都新分配。
- **PERF-WEB-010 — P2，manifest JSON 往返 churn。** `BrowserOpfsService.cs:104-119,133-136` request serialize/response deserialize；`opfsBrowser.js:687-755` parse/build/stringify/reparse，再做 metadata I/O。保留 JS manifest handle/parsed state。
- **PERF-WEB-011 — P2，Mutation/PerformanceObserver 未断开。** `B/wwwroot/startup.js:401-446` 创建 observer；ready 逻辑 `:350-377` 只 restore fetch，不 disconnect。跟踪结束时 disconnect，避免持续 DOM traversal/状态保留。
- **PERF-WEB-012 — P2，poll 每次 DirectoryExists+List+完整 JSON。** VM `:500-521` 先 exists 再 list，service `:45-66` 两次 JS call，`opfsBrowser.js:565-596` 每次 enumerate/getFile/build/stringify，即使未变。合并 NotFound-aware list 或增量 snapshot。
- **PERF-WEB-013 — P2，256KiB 一次 UI progress 回调。** VM `:373-383` 在 UI context 建 Progress，`:421-435` 每次格式化 property/string；service `:254-279` 每 chunk 报告。大文件可有数千 UI callbacks。coalesce/throttle，保留 final update。

健康：OPFS service `:254-286` 使用固定 256KiB ArrayPool；OutputStream `:90-110` finally 释放 handle；ReadStream `:102-116` Dispose；JS close/abort `:802-813,848-885`；VM `:437-489` 有 PeriodicTimer/zero-time gate；beforeunload guard 会 untrack。未将 DEBUG-only LogDebug、发布 roots、layout-ready 扫描或 bounded chunk copy 误列为泄漏。

---

## 14. FWK：Gekimini 运行时框架

- **PERF-FWK-001 / FWK-CMD-CACHE-001 — P1，singleton 强缓存动态命令。** `G/src/Gekimini.Avalonia/Framework/Commands/CommandService.cs:14-23,43-48` 强持有每个 Command/TargetableCommand；动态菜单 `Framework/RecentFiles/Commands/OpenRecentFileCommandHandler.cs:37-51`、`Modules/Shell/Commands/SwitchToDocumentListCommandHandler.cs:23-35` 每次刷新建新命令；`Modules/MainMenu/ViewModels/MenuItems/CommandMenuItemViewModel.cs:31-34,44,64-85` 订阅/重建。关闭文档和旧 menu VM 因此持续存活。稳定实例或显式 eviction/dispose。
- **PERF-FWK-002 / FWK-CMDKEY-001 — P2，Loaded 重复绑定快捷键。** `G/src/Gekimini.Avalonia/Modules/MainView/MainViewModel.cs:32-40` 每次 load 调 Bind；`Framework/Commands/CommandKeyGestureService.cs:27-40` 无条件 append KeyBinding；`Views/ViewLocator.cs:52-65` 每次 Loaded 触发。使绑定幂等或 unload 移除 owner binding。
- **PERF-FWK-003 / FWK-ROUTER-001 — P2，命令查找扫描视觉树。** `G/src/.../Framework/Commands/CommandRouter.cs:95-106` 每次 lookup 调 `ViewLocator.LocateForModel`，后者 `Views/ViewLocator.cs:121-128` LINQ 扫全部 TopLevel descendants；CommandManager requery `:52-80`，TargetableCommand `:21-30` 高频触发。缓存 active view/context。
- **PERF-FWK-004 / FWK-REQUERY-STUCK-001 — P2，异常会永久卡住 requery。** `G/src/.../Framework/Commands/CommandManager.cs:67-97` 设置 broadcast flag 后直接调用 subscribers，`:96` reset 无 finally；异常会让后续非强制 invalidation 在 `:70` 永久 return。finally reset 并隔离 subscriber failure。
- **PERF-FWK-005 / FWK-MENU-ASYNC-001 — P2，菜单更新 stale snapshot。** `G/src/.../Modules/MainMenu/Behaviors/MenuBehavior.cs:42-58` ToList 后逐项 await Update；`CommandMenuItemViewModel.cs:64-85` 可能替换 children，但旧 snapshot `:53-57` 仍继续更新。按 generation coalesce，只更新 live items。
- **PERF-FWK-006 / FWK-TOOLBAR-RELOAD-001 — P2，ToolBar load 累加。** `G/src/.../Modules/ToolBars/ViewModels/ToolBarsViewModel.cs:13-47` 每次 `OnViewAfterLoaded` BuildToolBars 并 append；`ToolBarBuilder.cs:40-50` 为累计 item 再建 AdaptiveToolBar，ViewLocator `:52-65` 会反复触发。初始化一次或 unload 移除。
- **PERF-FWK-007 / FWK-STATUS-RELOAD-001 — P2，StatusBar handler/VM 累积。** `G/src/.../Modules/StatusBar/ViewModels/StatusBarViewModel.cs:26-69` 每次 load 建三个 item 和 capturing handler，unload 只置 null；singleton item 会重复响应并根住旧 manager item。幂等 setup + detach/remove。
- **PERF-FWK-008 / FWK-HISTORY-SCAN-001 — P2，Undo 通知触发完整 history scan。** `G/src/.../Modules/UndoRedo/ViewModels/HistoryViewModel.cs:119-152` 每个 UndoActionCount notification 扫 ActionStack/HistoryItems；manager `Framework/UndoRedo/DefaultImpl/DefaultUndoRedoManager.cs:27-42,93-97` 每 ExecuteAction 增加通知。维护 index delta 或 batch refresh。
- **PERF-FWK-009 / FWK-HISTORY-VIEW-001 — P2，History 无虚拟化。** `HistoryViewModel.cs:86-96` 每 action 建 VM 并逐项 Add；`G/src/.../Modules/UndoRedo/Views/HistoryView.axaml:28-73` ScrollViewer+ItemsControl。大 history 全量 measure/retain。使用虚拟化或有界 projection。
- **PERF-FWK-010 / FWK-WINDOW-CLAMP-001 — P2，Clamp min>max 后 guard 卡住。** `G/src/.../Modules/Window/Views/WindowViewBase.cs:86-107` `isAdjusting=true` 后以 min 50 和 bounds-position 做 Math.Clamp；小/未测量 panel 可能 max<min 抛异常，且无 finally，后续 adjustment 被跳过。校验 bounds，finally reset。
- **PERF-FWK-011 / FWK-DRAG-STATE-001 — P2，取消 drag 不清 state。** `G/src/.../Framework/DragDrops/DefaultImpl/DefaultDragDropManager.cs:12,17-33` await `DoDragDropAsync` 无 finally；只有 drop handler `:44-50` 调 End。取消/失败且无 drop 时 stateStoreMap 留 DataContext。finally 清 token，End 保持幂等。
- **PERF-FWK-012 / FWK-RECENT-CLONE-001 — P2，recent mutation 深拷贝并同步持久化。** `G/src/.../Framework/RecentFiles/DefaultImpl/DefaultEditorRecentFilesManager.cs:206-231` 在 syncRoot 下 clone lists/InvalidRecordIds/完整 RecordInfoDataMap/每个 byte[] 后 SaveSetting；payload 在 `Models/Settings/RecentRecordInfoStoreSetting.cs:23-30`。用 versioned/delta copy，锁外持久化但保持原子提交。
- **PERF-FWK-013 / FWK-ICON-DECODE-001 — P2，URI bitmap 每次 Convert 解码。** `G/src/.../UI/ValueConverters/UriToBitmapConverter.cs:14-28` 每次开 stream/new Bitmap；菜单和 toolbox templates `Modules/MainMenu/Views/MainMenuView.axaml:24-29`、`Modules/Toolbox/Views/ToolboxView.axaml:63-71` 普遍绑定，Toolbox search `ToolboxViewModel.cs:83-103` 还会反复重建。缓存解码资产并明确 lifetime。
- **PERF-FWK-014 / FWK-TB-GESTURE-001 — P2，lazy command 取错导致 shortcut 缺失/重复查找。** `G/src/.../Framework/ToolBars/CommandToolBarItemDefinition.cs:21-31` 先以 nullable `_commandDefinition` 调 GetPrimaryKeyGesture，再才 force lazy property；首次访问得到空 gesture，后续重复 lookup。传入 `CommandDefinition` property。
- **PERF-FWK-015 / FWK-SETTINGS-CANCEL-001 — P2，Cancel 未丢弃 singleton 暂存值。** `G/src/.../Modules/MainMenu/ViewModels/MainMenuSettingsViewModel.cs:16-37` selected values 初始化一次；SettingsViewModel `:85-92` ApplyChanges，Cancel 只 close，ISettingsEditor `:3-10` 无 discard。按 dialog 隔离值或显式 reset。
- **PERF-FWK-016 / FWK-LOGGER-ALLOC-001 — P3，禁用日志仍插值。** `G/src/.../Utils/MethodExtensions/LoggerEx.cs:20-28,42-50` 在 ILogger level filtering 前构造 caller prefix/interpolated string。使用 IsEnabled/structured logging（仅在调用频率证实后优化）。

健康：CommandManager 使用 weak handlers 并清 dead entries；ToolBar command VM、History、DocumentContainer、DragDataContextOut 均有 detach；MainWindow TryExit 有 Interlocked guard。此前关于普通同步 CanExecute、descriptor cache、dialog cycle、serializer Save 和 provider backend latency 的候选均未保留。

---

## 15. DCK：Dock/ToolBar/WindowManager（有限边界审计）

以下结论只覆盖已深读的 ToolBar、WindowManager 和相关 Dock 调用链，不代表清单 17–505 的穷尽式审计。

- **PERF-DCK-001 / DCK-WM-01 — P1，关闭窗口未解绑 SizeChanged。** `G/Dependencies/Iciclecreek.Avalonia.WindowManager/source/.../ManagedWindow.axaml.cs:298-310` 每次 load 订阅 `WindowsPanel.SizeChanged`；close `:1191-1206` 只移除 WindowsPanel/MRU，不 `-=`。长期 panel 强持有 closed window，并可能重复 callback。close/unload detach 且保证单次订阅。
- **PERF-DCK-002 / DCK-TB-01 — P2，template replacement 不 Dispose 旧 ToolBarPanel。** `G/Dependencies/Avalonia.Controls.ToolBar/Controls/ToolBarPanel.cs:27-37` 有完整 Dispose；`ToolBar.cs:69-73` `OnTemplateChanged` 只 null old panels，不调用 Dispose。连接到 Items publisher 的旧 panel 可存活。替换前显式 Dispose（需验证 Avalonia runtime 是否自动处置 template part）。
- **PERF-DCK-003 / DCK-WM-02 — P2，OnApplyTemplate 累加 handler。** `ManagedWindow.axaml.cs:1423-1430` 每次 AddHandler KeyDown/PointerPressed，`:1491-1495` 每次 `Tapped +=`，无匹配移除。retemplate 后同一窗口重复处理输入。移除旧 handler 或 lifetime-scoped subscribe。
- **PERF-DCK-004 / DCK-WM-03 — P2，高频 pointer hover 创建 Cursor。** `ManagedWindow.axaml.cs:1935-1939` 每次 non-dragging PointerMoved `new Cursor(GetCursorForEdge(...))`。按 cursor type cache，仅 edge 变化时 assign。
- **PERF-DCK-005 / DCK-WM-04 — P2，Activate 重复排序枚举窗口。** `ManagedWindow.axaml.cs:2067-2071` deferred Where/Cast/OrderBy；BringToTop `:1776-1787` 两次枚举/ToList/filter，Activate `:847-851` 又枚举。维护 z/MRU order 或一次物化。
- **PERF-DCK-006 / DCK-TB-02 — P2，overflow Loaded handler 重复注册。** `G/Dependencies/Avalonia.Controls.ToolBar/Controls/ToolBar.Properties.cs:74-94` pre-load coercion 每次 true 都 `Loaded += OpenOnLoad`，无 guard/remove；使 dispatcher callback 累加。callback 内 `-=` 或 registration flag。
- **PERF-DCK-007 / DCK-TB-03 — P2，ToolBarTray membership O(T²)。** `ToolBarTray.cs:545-566` 对每项 `ToolBarCollection.Contains`，collection `:635-644` 为 Collection<T>；GenerateBands/InsertBand/CreateBand `:513-541,569-619` 继续扫描。维护 identity set/index。
- **PERF-DCK-008 / DCK-M-01 — P2，dry-run docking 复制 visible list。** `G/Dependencies/Dock/src/Dock.Model/DockManager.cs:180-231` 在 `DockDockableIntoDockVisible/DockDockIntoDock` ToList；HostWindowState `:74-102,168-188` 由 pointer candidate validation 反复调用。dry-run 只索引遍历，mutation 前再 snapshot。
- **PERF-DCK-009 / DCK-M-02 — P2，capability check 无条件建 diagnostic。** `DockCapabilityResolver.cs:32-71,81-83` 每次 IsEnabled 创建 evaluation/interpolated string；HostWindowState `:314-320,352-357` 与 hover validation 重复调用。成功快路径返回 bool，拒绝才构造诊断。
- **PERF-DCK-010 / DCK-M-03 — P2，sibling state 多次 Contains。** `FactoryBase.DockingWindowStateSync.cs:340-373,410-449` 对 ObservableCollection 每 child 调 VisibleDockables.Contains；`Dock.Model.Mvvm/Factory.cs:65` 创建 ObservableCollection；focus sync `FactoryBase.Init.cs:383-401` 对多个 owner 重复。一次 traversal 建 identity set。
- **PERF-DCK-011 / DCK-M-04 — P2，ActiveWorkspace comparer 不一致。** `DockWorkspaceManager.cs:31` 用 OrdinalIgnoreCase dictionary，Remove `:136-148` 却 case-sensitive 比较 ActiveWorkspace.Id；删除 `foo`/active `Foo` 后 active 仍指向未注册 workspace。用同 comparer 或 removed instance 清理。
- **PERF-DCK-012 / DCK-A-01 — P2，外部 drag detach 不走 Leave 清理。** `WindowDragHelper.cs:70-101` detach 后 `HostWindow.CancelExternalWindowDrag`；`HostWindow.axaml.cs:211-218` 只重置 flags，`HostWindowState.Leave` 的正常路径在 `:330-367`，pointer release `:257-269` 可能因 `_draggingWindow` reset 跳过；CaptureLost `HostWindowState.cs:478-482` 也只清 context。统一到幂等 RemoveAdorners/context reset。

健康：正常 HostWindow release 会 Leave/End/DropControl=null；ToolBarPanel 自身 Dispose 完整；Dock state sync 有 coalescing/depth guard；workspace tracking 有 StopTracking。未深读范围不作无问题保证。

---

## 16. GEN：生成器、本地化、序列化与小型支撑库

- **PERF-GEN-001 / GEN-01 — P2，localization cache 缺 culture 维度。** `G/Dependencies/SimpleTypedLocalizer/.../LocalizerManager.cs:37-50` cache 只按 resKey，在 `specifyCultureInfo` 前读；不同显式 culture 查询同 key 会复用首个结果；forwarder `LocalizerManager_Static.cs:23-27` 传 requested culture。cache key 改为 `(cultureName,resKey)`。普通 CurrentDefaultCultureInfo 切换会清 cache，因此不标 P1。
- **PERF-GEN-002 / GEN-02 — P2，启动初始化逐项 Base64 decode。** `G/Dependencies/SimpleTypedLocalizer/.../ImportTaskSourceWriter.cs:35-44,107-119,158-162` 为每个非空 translation 执行 Base64→byte[]→UTF8 string。生成 Roslyn-escaped literals 并预设 dictionary capacity；未测启动时长。
- **PERF-GEN-003 / GEN-03 — P3，生成输出不确定。** `ImportTaskRunner.cs:108-112` 的随机 provider suffix 与 `ImportTaskSourceWriter.cs:11-14` wall-clock header 使 rerun 输出不稳定，影响 build reproducibility/incremental behavior。使用稳定 path-derived identity，去除时间头；不宣称所有 unchanged build 必然失效。
- **PERF-GEN-004 / GEN-04 — P3，RESX 先尝试 JSON。** `ImportTaskRunner.cs:122-136` 有效 RESX 先经历 JSON deserialize/exception，再 XDocument。按 `.json/.resx` 扩展名选 parser；不是两个完整文件解析。
- **PERF-GEN-005 / GEN-06 — P3，同类型 load 仍走空 migration graph。** `Dependences/MigratableSerializer/MigratableSerializer/MigratableSerializerManager.cs:99-105` 对 parsed type==target type 仍 `GetMigrationChain`；`Base/Graph/DirectedGraph.cs:36-46` 建 List/HashSet 后立即成功且无 migration。像 Save `:168` 一样先返回 parsed object。
- **PERF-GEN-006 / GEN-08 — P3，异步 wrapper 额外状态机。** `Dependences/MigratableSerializer/MigratableSerializer/Wrapper/SerializerBase.cs:14-15` 对已有 `Task<T>`/WriteAsync 再 async/await；可直接转发相同类型 Task。不要把 Task<T>→Task<object> adapter（`:13`、MigrationBase `:16-17`）误删，接口不变性使其必要。
- **PERF-GEN-007 / GEN-09 — P3，生成 DI registration 无条件 HashSet。** `G/Dependencies/Injectio/src/Injectio.Generators/ServiceRegistrationWriter.cs:54-56` 即使无 tag 也建 HashSet。只有存在 tags 时生成。

撤销：TypeCollectedActivatorGenerator 的 all-type discovery（旧 GEN-05）是该功能固有语义，未证实增量退化；Migratable parser probe（旧 GEN-07）当前 consumer `CommonEditorProjectFileSerializer.cs:40-48` 在顶层 Version 即停止；Earcut node allocation 仅 legacy/root WPF caller，不属当前 Avalonia 热路径。

---

## 17. WAV：BrowserAudioWorklet 与 Emscripten bridge

- **PERF-WAV-001 — P2，非 SAB processor 每 quantum 分配并发送 snapshot。** `NAudio.BrowserAudioWorklet/src/NAudio.BrowserAudioWorklet/wwwroot/processor.js:174-180,247-250` 每 active quantum 创建/投递 snapshot，underrun 分支无进度；`host.js:404-406` 只覆盖字段无背压。48k/128 的 375 quantum/s 是代码推导，不是实测。使用有界 in-flight queue，stop/reset 保留最后确认。
- **PERF-WAV-002 — P2，realloc 直接覆盖导致 OOM 后丢指针。** `EmscriptenCompatibility/legacy-setjmp.c:45-46` 直接把 realloc 结果写回 table；NULL 时原指针丢失，下一轮 `:35-39` 可能非法访问。临时接收并检查；abort-on-OOM 是否由链接契约保证未证实。
- **PERF-WAV-003 — P3，首播重复 ConfigureRenderSource。** `NAudio.BrowserAudioWorklet/src/.../BrowserAudioWorkletPlayer.cs:424-426` 首播再次配置，前面 `PrepareCoreAsync:330,359-366` 已创建 format/resampler。复用预热结果，seek/stop 才 reset。

健康：SAB 快照有界；bridge grow-only buffer/span read；host 一次 transferable copy+ownership transfer；pool 上限 4；chunkHead 避免 shift；message consumer/close/abort/latency helper 均有清理。未将 processorerror stop 挂起列为已确认缺陷。

---

## 18. ACB-AUDIO：MP3Sharp、NVorbis、VGAudio、Opus、NWaves

### MP3Sharp / NVorbis

- **PERF-ACB-AUDIO-001 / ACB-MPV-01 — P1，短读请求可能零进度自旋。** `A/MP3Sharp/Buffer16BitStereo.cs:73-79` 把非 4 对齐 count 向下取整；`MP3Stream.cs:198-215` 在请求 count 未满足时继续循环但无 zero-progress exit。1–3 byte 请求且仍有 buffered data 时可永久自旋。修复 stream progress contract/ReadByte 边界。
- **PERF-ACB-AUDIO-002 / ACB-MPV-02 — P2，Mdct 每次 Reverse 分配。** `A/NVorbis/Mdct.cs:65-69` 每次建 `float[n/2]`，尽管 setup 已缓存；`Mapping.cs:184-190` 在 packet/energy channel 路径调用。保留 bounded workspace。
- **PERF-ACB-AUDIO-003 / ACB-MPV-03 — P2，Floor packet scratch。** `A/NVorbis/Mapping.cs:100-105` 每 packet 建 floorData/noExecuteChannel；`Floor1.cs:12,135-137,224-231` 还建 Data 与 int/bool/int[64]。按 packet/channel 复用并 reset。
- **PERF-ACB-AUDIO-004 / ACB-MPV-04 — P2，Residue scratch。** `A/NVorbis/Residue0.cs:127-130` 建 channel×partitionWords cache，`:180-184` 在 WriteVectors 的 stage/partition/channel loop 建 `int[steps]`。保留 bounded arrays。
- **PERF-ACB-AUDIO-005 / ACB-MPV-05 — P2，Layer I/II subband 重建。** `A/MP3Sharp/Decoding/Decoders/LayerIDecoder.cs:43-49,73-89`、`LayerIIDecoder.cs:24-41` 每 frame 重建 `ASubband[32]`/subband objects，虽然 `Decoder.cs:143-174` 已缓存 decoder。实例复用/reset。

### VGAudio / Opus

- **PERF-ACB-AUDIO-006 / ACB-VGA-01 — P1，Ogg reader/rooted stream retention。** `A/LB_Common/Audio/VGAudio/Containers/Oggfile/OpusOggReadStream.cs:127-150` 的 local OggContainerReader 未 Dispose；`StreamReadBuffer.cs:24-44,62-70` 的 static source-wrapper dictionary 只在 Dispose 移除，可能根住 stream/backing buffer。统一 ownership/Dispose，避免静态表成为根。
- **PERF-ACB-AUDIO-007 / ACB-VGA-02 — P2，Opus interleaved tail copy 长度。** `A/LB_Common/Audio/VGAudio/Formats/Opus/OpusFormat.cs:123-130,148-150` 用 `encodeCount` 而非 `encodeCount*ChannelCount` 复制 partial interleaved frame。按样本/通道正确计算并保留 lookahead flush 语义。
- **PERF-ACB-AUDIO-008 / ACB-VGA-03 — P2，ADX 每 frame 临时数组。** `A/LB_Common/Audio/VGAudio/Codecs/CriAdx/CriAdxCodec.cs:76-97,107-146` 每帧建 `int[samplesPerFrame]`。hoist 到 codec workspace。
- **PERF-ACB-AUDIO-009 / ACB-OPUS-001 — P2，CELT per-call scratch。** `A/LB_Common/Audio/VGAudio/Codecs/Opus/Celt/Structs/CELTDecoder.cs:635-710`、`CeltEncoder.cs:259-296,639-664,812-847`、`Celt/MDCT.cs:49-75,163-188`、`KissFFT.cs:363-383`、`VQ.cs:181-187,337-348`、`Bands.cs:687-709,1012-1031,1295-1324,1489-1515` 在 frame/call 路径反复建数组。按 decoder/encoder 实例复用 bounded scratch。
- **PERF-ACB-AUDIO-010 / ACB-OPUS-002 — P2，SILK block/retry scratch。** `SilkChannelDecoder.cs:271-312`、`DecodeCore.cs:47-75`、`ShellCoder.cs:128-144,169-183` 以及 `EncodePulses.cs:238-247`、`DecodePulses.cs:64-70,94-104` 建 block/packet arrays；encoder retry `SilkChannelEncoder.cs:877-891,965-1005,1042-1069`、`SilkNSQState.cs:621-638,668-671,932-938` 还重复 state。按 channel/attempt 复用并 reset。
- **PERF-ACB-AUDIO-011 / ACB-OPUS-003 — P2，BoxedValue GC。** `Common/CPlusPlus/BoxedValue.cs:34-96` 与 `QuantizeLTPGains.cs:71-113` 在量化路径包装数值对象。改为值型/预分配 scratch，需以 profile 确认收益。
- **PERF-ACB-AUDIO-012 / ACB-OPUS-004 — P2，analysis buffer 重复。** `Opus/Analysis.cs:168-180,203-217`、`MultiLayerPerceptron.cs:75-94` 创建 analysis/MLP buffers；跨 frame 复用可减少短命数组。
- **PERF-ACB-AUDIO-013 / ACB-OPUS-005 — P2，数组 overlap 谓词使 bulk-copy 分支不可达。** `Common/CPlusPlus/Arrays.cs:122-218` 使用 OR 组合区间重叠条件；`Opus/Analysis.cs:226` 的 src480→dst0 len240 是具体 disjoint case。修正区间判定并为边界写测试/基准。未发现独立的输出并发缺陷。

### NWaves（清单 467–714，248 files）

- **PERF-ACB-AUDIO-014 / ACB-NW-01 — P2，逐样本捕获式 Sum。** `A/NWaves/Effects/ChorusEffect.cs:92-101`→`Effects/Base/AudioEffect.cs:23-27`→`Filters/Base/IFilterExtensions.cs:23-40,48-58` 每 sample 走 capturing Sum selector。改 indexed sum；分配/dispatch 实际量待测。
- **PERF-ACB-AUDIO-015 / ACB-NW-02 — P2，Resampler 无用计数/额外遍历。** `A/NWaves/Operations/Resampler.cs:144-184,187-223`、`Operations/Operation.cs:138-140` 每 interpolation tap 更新未使用 times/counters/min-max，之后又单独 clamp traversal。删除 dead work；并行 race 只影响 dead diagnostics，不影响返回 samples。
- **PERF-ACB-AUDIO-016 / ACB-NW-03 — P2，RLS covariance assignment。** `A/NWaves/Filters/Adaptive/RlsFilter.cs:54-63,73-123`（尤其 `:112`）把 covariance product 覆盖而非累加；`AdaptiveFilter.cs:31-36` 的 cubic per-sample loop 可能退化为 rank-one quadratic。无 in-manifest caller/numerical repro，先加行为验证再改。
- **PERF-ACB-AUDIO-017 / ACB-NW-04 — P2，online feature scratch/copy。** `OnlineFeatureExtractor.cs:96-123,162-186`→`FeatureExtractor.cs:147-177,201-204` 即使 supplied-output 仍按 callback/chunk 建 block scratch，`MemoryOperationExtensions.cs:62-67` range overload 又复制 fragment 两次。复用 caller workspace。
- **PERF-ACB-AUDIO-018 / ACB-NW-05 — P2，Spectral feature 重复 band 工作。** `A/NWaves/Features/Spectral.cs:182-220,232-262`、`FeatureExtractors/Multi/SpectralFeaturesExtractor.cs:145-151,210-233`、`Base/FeatureExtractor.cs:171-203` 每 frame 为 c1–c6 重扫/物化/排序 band，并重复解析固定 band ID。预计算 ranges、复用 scratch、partial-select。
- **PERF-ACB-AUDIO-019 / ACB-NW-06 — P2，Griffin-Lim 后续迭代保留无用 magnitude。** `Operations/GriffinLimReconstructor.cs:78-104,110-119`→`Transforms/Stft.cs:342-400` 后续默认迭代只需要 phase，却仍计算/分配被丢弃的 magnitude（默认约 19 次）。按迭代模式复用/省略。
- **PERF-ACB-AUDIO-020 / ACB-NW-07 — P2，Hilbert double copy。** `Transforms/HilbertTransform.cs:48-58`、`Utils/MemoryOperationExtensions.cs:23-25,117-121`、`Signals/ComplexDiscreteSignal.cs:46-59`、`Operations/Modulator.cs:161-181` 先 ToDoubles 建数组，再 `allocateNew:true` clone。转移 ownership 或直接传新数组。
- **PERF-ACB-AUDIO-021 / ACB-NW-08 — P2，PNCC ring slot alias。** `FeatureExtractors/PnccExtractor.cs:164-174,206-220,315-320,397-428`、`Filters/Fda/FilterBanks.cs:798-810`、`Base/FeatureExtractor.cs:171-203` ring slots 反复保存同一 spectrum reference，history 在累加时实际 alias 当前槽。为每槽提供独立预分配 storage。
- **PERF-ACB-AUDIO-022 / ACB-NW-09 — P3，FWT inverse 过度清零。** `Transforms/Wavelets/Fwt.cs:175-215`（clear `:196`，wrapper `:100-113`）每个 inverse level 清完整 output，实际只用 active prefix h；可由 O(N×levels) 降到 active prefix。无 in-manifest inverse caller，先测量。

健康：MP3 Layer III decoder/filter/output 复用；NVorbis packet 在 `StreamDecoder.cs:455-461,526-530` finally 释放，Mdct setup `Mdct.cs:11-20` 缓存；VGAudio 深层 entropy/history/mode tables 持久化；NWaves 的 caller-buffer ByteConverter、FIR/IIR/BiQuad/delay/FFT/STFT/MFCC workspace 和 WAV/CSV disposal 较好。撤销“Opus 额外 padding frame 必错”、无证据的 nested Parallel oversubscription、Ogg packet 无界增长等推断。

---

## 19. ACB-CONTAINER：ACB/AFS2/CPK/UTF 容器

- **PERF-ACB-CONTAINER-001 / C01 — P2，TrackMetadata 重复完整 HCA parse。** `A/../Xv2CoreLib/ACB/Generator.cs:303` 正常 Generate 进入 `ReplaceTrackOnWaveformWithoutUndo`；`ACB_File.cs:1348-1364`、`AFS2_File.cs:631-636,727-731` 对新 HCA 经临时 entry、AddEntry copy、trackMetadata、hcaMeta 多次 parse。`TrackMetadata.cs:50-67` 调 `HcaReader.ParseFile`，`HcaReader.cs:20-40,131-145` 读音频数据并按 frame 分配，即使只需 header scalar。改 `AudioReader.ReadMetadata(AudioReader.cs:20)` 并复用结果。
- **PERF-ACB-CONTAINER-002 / C02 — P2，Generate 计算未使用 duration。** `A/../ACB/Generator.cs:326-337` 计算 `wavFile[Channels.Sum].Duration`；`WaveFile.cs:446-463` 为全 channel/sample 建完整 float[] 并求和，而 `Generate:301` tuple deconstruction 后未使用 duration。删除 tuple/计算或读取既有 channel length；mono 不触发但 multichannel 会。
- **PERF-ACB-CONTAINER-003 / C03 — P2，MP3/OGG 交错 PCM 与分声道数组并存。** `A/../Audio/Generator.cs:101-137,146-166` 先 materialize interleaved `sampleData`，再为各声道建 `DiscreteSignal`；`DiscreteSignal.cs:56-58` IEnumerable ctor 又 `ToArray`。直接写入预分配/chunked channel buffers。
- **PERF-ACB-CONTAINER-004 / C04 — P2，Endian scalar 读取短数组。** `A/../CPK/CriPakTools/Endian.cs:33-98`、`UTF_File.cs:62-92,133-162,218-273` 的 BigEndianConverter 每次 CopyBytes 2/4/8 bytes，再 Reverse().ToArray；`BinaryConverter.cs:198-206`、`Endian.cs:130-167` 同类。使用 shifts/BinaryPrimitives/Span。
- **PERF-ACB-CONTAINER-005 / C05 — P2，embedded AWB 多层 payload copy。** `AFS2_File.cs:247-315,328-334` unsized `List<byte>`+returned array，UTF parent `UTF_File.cs:769-810`、`ACB_File.cs:756-763` 再 AddRange/full output；legacy CPK `AWB_CPK.cs:50-56,98-105,138-144` 也 contentBytes→bytes。预估大小并使用一个 addressable output；外部 streaming AFS2 路径健康。
- **PERF-ACB-CONTAINER-006 / C06 — P2、条件性，CPK reader handle 不确定关闭。** `CPK_Reader.cs:68-84` 保存 BinaryReader/FileStream；`Xv2FileIO.cs:79-99` Disable/Enable 替换而不 Dispose，多线程 extractor `CPK_Reader.cs:409-419` 每线程打开 reader 无 close。实现 IDisposable 并在 disable/replacement/extraction 完成处释放；普通 Generate 可达性仍需确认。
- **PERF-ACB-CONTAINER-007 / C07 — P2，AWB dedup/ID candidate O(n²)。** `ACB_File.cs:542-546` 对每个非空 track AddEntry；`AFS2_File.cs:621-667` `GetExistingEntry` 扫现有 entries，`:538-563` 线性 NextID；等大小、长公共前缀 payload 还会在 `Utils.cs:695-708` 触发更多 CompareArray。维护 ID/hash/length candidate index，最后保留 byte equality。
- **PERF-ACB-CONTAINER-008 / C08 — P2，ExtractAll 名称 IndexOf 二次扫描。** `CPK_Reader.cs:277-315`（尤其 `:285-294`）把 names 放 `List<object>`，每个文件 `IndexOf`；k distinct names 产生约 k(k−1)/2 comparisons。用按实际 destination path 的 HashSet，保持 overwrite order。

旧 F1–F14 中的 BinaryFormatter/undo、AddRange/deadlock、compressed+output coexistence、CPK “永久泄漏”均不单列：当前 Avalonia 热 caller/永久根证据不足；仅作为后续 profile/库契约核查项。

---

## 20. 验证、健康模式与未决问题

### 本轮已完成的验证

- 对关键 P1/P2 路径重新读取实现和调用链，核对了上述 file:line 锚点；各分区 reviewer 也按各自 manifest 做了 targeted full-context read。
- 对 16 份冻结清单重新对账：共 2,878 条路径、2,878 个唯一文件，交叉分区重复为 0、缺失路径为 0；按文件实际逻辑行复算为 319,534 行。
- 保留发现共 177 项，唯一标识符也是 177 个，重复为 0；每项均有优先级，其中 P1 21 项、P2 140 项、P3 16 项。
- 对低采样率边界做了独立算术探针：`int(sampleRate * 0.001 * channels)` 在 `499Hz × 2ch` 为 `0`，支持 PERF-AUD-005 的零步风险；这不是应用运行时测试。
- 对生成器、Parser、BrowserAudioWorklet、ACB 容器的条件编译/项目引用边界做了交叉检查，明确标注 dormant/conditional 项。
- 没有将一条失败的结构化 agent yield 当作代码证据；所有报告项均来自可重读的源码锚点和已发送的 reviewer 结论。

### 反复出现的健康模式

- ArrayPool/固定大小 buffer 的 finally 归还；WAV RIFF/layout validation；文件替换使用同目录 temp+atomic move。
- Render/audio/session/context 的显式 detach、Dispose、取消和版本检查；Scheduler 对同一 scheduler 有不重入、取消和退出等待保护。
- Source-generated JSON metadata、编译绑定、预建 parser dictionary、typed Span/struct、缓存 transform setup、稳定的 packet/FFT/workspace 复用。
- Browser OPFS read/write/abort close paths、SAB bounded snapshot、waveform stale-result version gate 和 worker cancellation。
- Dock 正常 release 的 Leave/End/DropControl 清理、ToolBarPanel 自身完整 Dispose、workspace tracking 的 StopTracking。

### 未决问题

1. 没有代表性 chart/音频/游戏目录/OPFS 文件大小和用户操作频率，不能把静态复杂度换算为 ms、FPS、GC pause 或峰值 MB。
2. 需要用真实长 Hold、密集 chord、SVG、长音频、ACB/CPK、长 history、browser large download 做 allocation/CPU/heap/handle profile。
3. 需要确认 `ENABLE_SVG_PREFAB_OBJECTS`、CPK reader 的实际发布可达性、DCK 清单未深读区间的 runtime 版本行为，以及 storage provider 的 file-level bookmark/atomic write contract。
4. ACB-AUDIO 深层第三方实现的实际 caller/cadence、Opus/NWaves 数组大小和音质/数值契约必须在改动前用回归样本验证。
5. `RND-018` marker 透明问题、`DAT-012` indexer recursion、`PRS-001`/`AUD-005`/`DAT-001` 等 correctness/infinite-loop 风险应先补最小回归用例，再做池化或并行化。

## 21. 交付状态

- 报告已按分区写入本文件；审核轮次（2026-09-09）未修改业务源代码。
- **2026-09-11 后续修复：** PERF-RND-004 / RND-05 已按“保留前端帧 + Swap/Remove 时释放”处理；PERF-RND-005 / RND-06 已把非批量纹理绘制的 `OnBegin/OnEnd` 与 `SKPaint` 提到实例循环外；PERF-RND-006 / RND-07 已把 `LineVertex`/`VertexDash` 改为 `readonly record struct`。三项均附基准对比与回归验证，见第 4 节「已修复项」。其余发现状态不变。
- 本轮没有 P0；P1 优先项已在第 3 节列出，P2/P3、条件项、撤销项和健康模式均已区分。
- 下一步应是针对 P1 集合建立小型可重复 benchmark/smoke corpus，再按测量结果实施修复；不要在没有 profile 的情况下同时改动所有 P2/P3 项。
