# Avalonia → WPF 编辑器渲染回移评审（2026-09-29）

> **方向：** 本文件是**反向**（Avalonia 移植树 → WPF 原树）的同步候选筛选，回答「Avalonia 侧编辑器渲染做了哪些 WPF 没有的改进/修复，哪些值得搬回去」。
> **基线：** 见 [`../WPF-MIGRATION-SYNC.md`](../WPF-MIGRATION-SYNC.md)。同步点 `8b2940785`（2026-09-19），Avalonia 线自同步 merge `b26b79118` 起另有 43 个提交，WPF 线另有 16 个提交待同步（`WPF-MIGRATION-SYNC.md` §3）。
> **方法：** 对 Avalonia 侧与编辑器渲染相关的提交逐条 `git show` 读原始 diff，再在 **WPF 树当前源码**上定位对应实现并给出 `文件:行号`；判定「WPF 是否存在同一缺陷 / 能否 1:1 回移」。分四个分区只读核验：渲染循环与可见性、集合与空间索引、帧时间与投射物、性能面板与渲染资源生命周期。
> **行号时点：** 2026-09-29 两树 HEAD 工作区。
> **用户举例对应项：** 「targetEditor 的谱面时间固定」= **A1**（`0708ca4b7`：一帧只读一次播放时间，整帧共用同一时间快照）。

## 1. 结论总览

| 编号 | Avalonia 改动 | WPF 现状 | 结论 |
| --- | --- | --- | --- |
| **A1** | `0708ca4b7` 帧内播放时间快照（`DrawingTargetContext.CurrentTime/CurrentTGrid`、`IFumenEditorDrawingContext.FrameTime/FrameTGrid`） | 13 处渲染路径中途现读（12 处有效），无快照字段 | **可回移**，中等工作量、机械 |
| **A2** | `8a0e1bc7b` 拍线几何改读当前 `DrawingTargetContext` | 仍读只在设计模式赋值的 `RectInDesignMode` | **可回移**，小 |
| **A3** | `0db071868` Meter/BPM 枚举按可见区间收敛 | 每个合并区间全表 `.Skip(1)` 枚举 | **可回移**，2 行 |
| **A4** | `8dbb123d3` 可见性判定帧内缓存（语义部分） | 3 参 `CheckRangeVisible` 每次调用 `SelectMany` 全部组 | **可回移**，小 |
| **A5** | `6ed334366` 监视器摘除时清空渲染统计 | `PerfomenceMonitor` 自动属性，停止渲染不清 | **可回移**，小 |
| **A6** | `5220f790` `BulletPalleteList` 下标递归修复 + 有序 backing | `this[int index] => this[index]` 必栈溢出；每枚举 `OrderBy` | **可回移**，小而高价值 |
| **A7** | `195525678` `MeterChangeList` 枚举/查询 O(log n) | 每枚举 `OrderBy`；三查询 `LastOrDefault/FirstOrDefault` 线性扫描 | **可回移**，小（需补二分原语） |
| **A8** | `1fdf9835c` `BpmList` 内容令牌（DAT-C1） | 命中判断仍每次 O(n) 全表哈希 | **可回移**，小 |
| **A9** | `7aa9e41ed` 沙盘墙边界帧内候选缓存 | 每次采样 `GetChildObjectsFromTGrid` + `IsPathVaild` | **可回移**，2 文件 |
| **A10** | `a1adec1c8`（B-042）关闭编辑器时释放渲染资源（可移植核心） | `DisposeRenderLoop` 只摘上下文，不释放 helper/不清理 targetMap | **可回移**，需适配 |
| **A11** | `62957153e` 绘制命令构建器的异常安全（残余项） | `DrawCircles`/`CopyToPooledList` 抛异常时池化列表泄漏 | **可回移**，局部 |
| **A12** | `b07edf916` QuadTree 硬化（三处小改） | `Add` 不去重重复订阅；属性变更无锁无校验；`Query` 空树竞态 | **可回移**，小 |
| **B1** | `c697fda80` + `ff19a917c` + `96dc4287a` 区间树并发安全 / `EnsureInSync` / 零分配 lane 查询 / 投射物帧内 lane 缓存 | 可变树 + 就地 `Release` 重建；并行投射物路径无屏障 | **需适配**（基础设施，成本最大） |
| **B2** | `62957153e` 帧体 try/finally 重构（结构性部分） | 内联清理，`End:` 的 `drawMap.Clear()` 是唯一清理 | **需适配**（先做重构，才能再删冗余） |
| **B3** | `9b76967f8` 限帧帧保留 front 命令列表 | 症状在 WPF 不发生；共享的 `if (!Swap) return` 形状仍在 | **暂缓**（需按后端重新论证） |
| **C1** | `5de442d4c`（RND-C2）限帧闸门前置于 builder 创建之前 | 闸门在 render context 内、`OnRender` 之前 | 不适用（WPF 结构已更好） |
| **C2** | `6c1504952`（RND-C3）删除 `End:` 冗余 `Clear()` | WPF 无 finally，该 `Clear()` 是必需品 | 不适用 |
| **C3** | `8dbb123d3` 容器 `ConcurrentDictionary → Dictionary` | WPF 本来就是 `Dictionary` | 不适用 |
| **C4** | `6fa0784f8` 每上下文渲染性能面板 | WPF 2026-05 已有等价面板、无 FPS 覆盖层 | 不适用（Avalonia 追平 WPF） |
| **C5** | `0266ac424` 编辑器滚动条常显 | Avalonia 的 `AllowAutoHide` 是平台属性 | 不适用 |
| **C6** | `41e4cdb9f` ISF 区域匹配（最早起点、半开区间） | 同一提交同时改了两棵树 | 不适用（WPF 已含） |
| **C7** | `b07edf916` Connectable 显示枚举（T-005 部分） | WPF 已是目标形态 | 不适用 |
| **C8** | 浏览器/WASM 渲染与输入修复（`bb2b6035f`、`523bf28fb`、`48873c0db` 等） | 平台专属 | 不适用 |
| **C9** | `be657f7f6` 编辑器打开时请求合成器时钟提升 | 平台专属（无 WPF 对应 API） | 不适用 |
| **D1** | RND-003 绘制目标单例纹理重复加载不释放 | **Avalonia 侧也尚未修复**（其复审文档列为当前最优先） | 不可回移（两边同缺陷） |
| **D2** | AUD-001 设置落盘 debounce | Avalonia 侧未实施 | 不可回移 |
| **D3** | AUD-003 `PeakPoint` 每点 `float[]` | Avalonia 侧未实施 | 不可回移 |
| **D4** | AUD-004 音频事件驱动方式 | Avalonia 侧未实施；WPF 机制本就不同（`AbortableThread`，非 1ms `DispatcherTimer`） | 不可回移 |

**计数：** A 类 12 项、B 类 3 项、C 类 9 项、D 类 4 项。

---

## 2. A 类：可直接回移

### A1 — 帧内播放时间快照（`0708ca4b7`；即「谱面时间固定」）

- **Avalonia：** `Graphics/Drawing/DrawingTargetContext.cs:31,36` 新增 `CurrentTime`/`CurrentTGrid`；`Graphics/IFumenEditorDrawingContext.cs:20,26` 暴露 `FrameTime`/`FrameTGrid`（带 `CurrentPlayTime` 回退）；`ViewModels/FumenVisualEditorViewModel.Drawing.cs:353-354` 帧首只读一次 `CurrentPlayTime`，`:424-425` 写入每个绘制上下文；消费方 `DrawJudgeLineHelper.cs:27`、`DrawPlayerLocationHelper.cs:56`、`DrawHitObjectEffectHelper.cs:42,46`、`DrawTimeSignatureHelper.cs:61`、`DrawPlayableAreaHelper.cs:286`、`BeamLazerDrawingTarget.cs:40,101`、`ProjectileBatchDrawTargetBase.cs:184`；`GetViewportTGrid()`/`GetViewportAudioTime()` 在 Avalonia 已删除。
- **WPF：** 无 `CurrentTime`/`CurrentTGrid`/`FrameTime`/`FrameTGrid`。`DrawingTargetContext.cs:15-35`、`IFumenEditorDrawingContext.cs:13`（只有 `CurrentPlayTime`）。
- **WPF 现状的 13 处中途读取**（`OnEditorRender` 起于 `Drawing.cs:293`，读取点见下表）：

| # | file:line | 读取 | 用途 |
| --- | --- | --- | --- |
| 1 | `FumenVisualEditorViewModel.Drawing.cs:324` | `GetViewportTGrid()` | 帧原点 |
| 2 | `…Drawing.cs:328` | `GetViewportAudioTime()` | `EditorOffsetMs` 分支 |
| 3 | `…Drawing.cs:510` | `GetCurrentTGrid()` | bullet/bell 预筛 |
| 4 | `…Drawing.cs:750` | `GetCurrentTGrid()` | `EnumerateAllDisplayableObjects` 判定过滤 |
| 5 | `DrawJudgeLineHelper.cs:28` | `GetViewportTGrid()` | 裁判线 Y/标签 |
| 6 | `DrawPlayableAreaHelper_new.cs:252` | `GetViewportTGrid()` | 当前生效的沙盘 helper |
| 7 | `DrawPlayableAreaHelper.cs:178` | `GetViewportTGrid()` | **死类**（全仓无实例化），可不动 |
| 8 | `DrawHitObjectEffectHelper.cs:40,44` | `GetCurrentTGrid()` + `CurrentPlayTime` | 打击特效窗口 |
| 9 | `DrawPlayerLocationHelper.cs:57,58` | `CurrentPlayTime` ×2 | 玩家位置 |
| 10 | `DrawTimeSignatureHelper.cs:58` | `CurrentPlayTime` | 预览拍线 |
| 11 | `BeamLazerDrawingTarget.cs:41,102` | `GetCurrentTGrid()` + `GetViewportTGrid()` | 激光进度/Y |
| 12 | `LaneBlockerDrawingTarget.cs:116` | `GetViewportTGrid()` | 补采样 |
| 13 | `ProjectileBatchDrawTargetBase.cs:180` | `ConvertAudioTimeToTGrid(CurrentPlayTime)` | 投射物批原点 |

- **为什么 WPF 也需要：** WPF 的 `CurrentPlayTime` 由 UI 线程的 `ScrollTo(...)` 写入（`ScrollViewer.cs:106`、`:176`），而编辑器渲染在 `D3D9On12` 后端跑在**专用渲染线程**（`Skia/RenderControls/Backends/DirectX/SkiaRenderControl_D3D9On12.cs:120-143`）；跨帧的 `RenderDataLock`（`ViewModels/FumenVisualEditorViewModel.RenderDataLock.cs:9-21`）并不覆盖 `ScrollTo` 的这次写入。帧原点（#1）与裁判线（#5）读到不同时刻就会出现 Avalonia 提交信息里描述的「裁判线高度偏一个播放步长、可见抖动」。该文件自身的 TODO 也写着 *"Replace this lock-based bridge with immutable per-frame render snapshots."*——本项正是它。
- **移植注意：** WPF **不能**照抄 Avalonia 的「删除两个 helper」——`ScrollViewer.cs:178`、`UserInteractionActions.cs:1732,1769` 在渲染路径外仍在使用；回移应保留两方法，只把渲染路径（上表 12 处有效点）换成快照。预览模式下 `GetViewportTGrid()` 走 `previewScrollPositionMs`、`GetCurrentTGrid()` 走 `CurrentPlayTime`，目前 `SetPreviewScrollPosition` 会同时写两者（`ScrollViewer.cs:176-178`），回退分支需保留该语义。
- **验收参考：** Avalonia 侧 `DrawingFrameSnapshotTests`（3 项）+ 本文件 A1 的 12 处逐一改读。

### A2 — 拍线（拍号）改读当前绘制上下文（`8a0e1bc7b`；PERF-RND-013 / RND-16）

- **Avalonia：** `Editors/DrawTimeSignatureHelper.cs:42` 取 `target.CurrentDrawingTargetContext`，`:52-53` 用 `WorldRect.MinY/MaxY`、`:68` 用 `ViewHeight`、`:75,127-128` 用 `ViewRelativeRect.Width`。
- **WPF：** `Editors/DrawTimeSignatureHelper.cs:49-50,65,72,82,122-123` 全部读 `target.Editor.RectInDesignMode`；而 `RectInDesignMode` 只在设计模式赋值（`FumenVisualEditorViewModel.Drawing.cs:393-394`）→ 预览模式下拍线长度/纵向范围取自陈旧值。
- **替换关系（设计模式等价，已核对）：** `RectInDesignMode.MinY/MaxY → drawingContext.WorldRect.MinY/MaxY`；`.Height → drawingContext.ViewHeight`；`.Width → drawingContext.ViewRelativeRect.Width`。设计模式下 `WorldRect == ViewRelativeRect`（`Drawing.cs:367-368,372-386`）且 `RectInDesignMode = defaultDrawingTargetContext.WorldRect`（`:394`），无回归。
- **注意：** `RectInDesignMode` 本身被交互代码大量使用（`UserInteractionActions.cs` 十余处、`FastPickLaneCommandHandler.cs:28`、`DefaultObjectInteractiveAction.cs:169`），**只改渲染 helper**。
- **验收参考：** Avalonia 侧 `DrawTimeSignatureHelperTests`（3 项）。

### A3 — Meter/BPM 枚举按可见区间收敛（`0db071868`）

- **Avalonia：** `FumenVisualEditorViewModel.Drawing.cs:812-818` 先取 `filterFirstBpm`/`filterMeterChange`，再 `MeterChanges.BinaryFindRange(min,max).Except(filterMeterChange)` / `BpmList.BinaryFindRange(min,max).Except(filterFirstBpm)`。
- **WPF：** `FumenVisualEditorViewModel.Drawing.cs:754-757` 在 `foreach (visibleRanges)` 内仍 `fumen.MeterChanges.Skip(1)` / `fumen.BpmList.Skip(1)` —— **每个合并区间整表枚举一次**，`Skip(1)` 是 LINQ 迭代器，长谱面 + 多变速组时是纯浪费。
- **附带：** `Base/OngekiFumen.cs:396-397`（WPF）与 `Avalonia/.../Base/OngekiFumen.cs:395-396`（Avalonia）**两边都还是** `.Skip(1)`，不在本次提交范围内，可选顺带处理。
- **成本：** 2 行，无依赖。

### A4 — 可见性判定的帧内缓存（`8dbb123d3` 的语义部分）

- **Avalonia：** `FumenVisualEditorViewModel.Drawing.cs:274` 普通 `Dictionary`、`:281` 帧内 `mergedVisibleTGridRanges`、`:459-465` 帧首物化一次、`:743-750` `CheckVisible` 遍历 `Values`、`:759-767` `CheckRangeVisible` 扫描缓存列表、`:1010-1011` 3 参重载委托给 2 参。
- **WPF：** 容器本来就是 `Dictionary`（`Drawing.cs:281`，RND-C1 的容器部分不需要回移）；但 `:661-670` 的 2 参 `CheckRangeVisible` 走 `:665` 的 3 参重载，而 `:961-970` 的 3 参重载**每次调用**都 `drawingContexts.SelectMany(x => x.Value.VisibleTGridRanges)` —— 即 O(组数²) 摊平 + 迭代器分配。调用方为 `VisibleLineVerticesQuery.cs:43`（逐 lane、逐 child）与 `LaneBlockerDrawingTarget.cs:208`；全仓无其它 3 参调用点，可安全折叠。
- **成本：** 小。保持「任一组可见即算可见」的既有语义。
- **验收参考：** Avalonia 侧 `VisibleContextCheckBenchmarks`（含新旧对拍断言；`CheckRangeVisible` 26.6×–74×）。

### A5 — 监视器摘除时清空渲染统计（`6ed334366`）

- **Avalonia：** `Kernel/Graphics/Skia/DefaultSkiaRenderContext.cs:28-42` 监视器 setter 先 `value?.Clear()`；`:64-90` `StartRendering`/`StopRendering` 都清。
- **WPF：** `Kernel/Graphics/Skia/DefaultSkiaRenderContext.cs:25` 是自动属性（set 时不清）；`:57-84` 启动/停止渲染均不清；只在移除上下文/关闭编辑器时把字段赋成 `DummyPerformenceMonitor.Instance`（`DefaultSkiaDrawingManagerImpl.cs:161`、`DefaultOpenGLRenderManagerImpl.cs:261`、`FumenVisualEditorViewModel.Drawing.cs:1068`），保留上一轮样本。
- **成本：** 小、自包含（OpenGL 上下文同理）。

### A6 — `BulletPalleteList` 下标递归与枚举排序（`5220f790`；PERF-DAT-009 / DAT-12）

- **WPF：** `Base/Collections/BulletPalleteList.cs:46` `public BulletPallete this[int index] => this[index];` —— 无条件自调用，任何下标读取都以**不可捕获的** `StackOverflowException` 结束进程，而该类型公开实现 `IReadOnlyList<BulletPallete>`。`:51` 每次枚举 `palleteMap.Values.OrderBy(...)`（解析期逐条 bullet/bell 命令都会走一遍）。`:92-99` `RemovePallete` 退订的是传入实例，而 `AddPallete:85` 订阅的是入表实例（退订泄漏）。
- **Avalonia：** `:39-51` 有序 backing 列表（`Count`/`this[int]`/枚举器共用）、`:64-77` 二分插入、`:120-130` 退订修正。
- **成本：** 文件局部，低风险；顺带消掉解析热路径的排序开销。
- **验收参考：** Avalonia 侧 `BulletPalleteListAccessBenchmarks`（256 项按下标 5.64 ms/11.06 MB → 209 ns/0 B）。

### A7 — `MeterChangeList` 枚举与查询 O(log n)（`195525678`；PERF-DAT-010 / DAT-13）

- **WPF：** `Base/Collections/MeterChangeList.cs:71-76` `foreach (var item in changedMeterList.OrderBy(x => x.TGrid))`（每次枚举重排）；`:80-88` `this.LastOrDefault(x => x.TGrid <= time)` / `FirstOrDefault(...)` 线性扫描。`Base/SortableCollection.cs` 缺 `LowerBoundIndex`/`UpperBoundIndex`（只有 `BinarySearchBy`，其内部还有等值段线性尾部）。
- **Avalonia：** `MeterChangeList.cs:69-76,78-107` + `SortableCollection.cs:106-150` 的二分原语；枚举器改为直接走已排序 backing。
- **成本：** 2 个方法 + 2 个约 15 行的二分 helper；`TGridSortList<T>` 派生自 `RemindableSortableCollection<T,TGrid>`，在 `SortableCollection` 层加即可覆盖 BpmList/MeterChangeList。
- **注意：** 枚举器由快照变 live 的语义变化（边枚举边改会抛 `InvalidOperationException`）与 Avalonia 一致。
- **验收参考：** `MeterChangeListQueryBenchmarks`。

### A8 — `BpmList` 内容令牌（`1fdf9835c`；DAT-C1）

- **WPF：** `Base/Collections/BpmList.cs:150-169` 命中判断每次仍 `foreach (var bpm in this)` 算全表哈希（`:152-160`），`:162` 比对；`cachedBpmContentHash` 字段（`:120`，变更处 `:52` 赋值）实际上没起到 O(1) 命中判断的作用。BPM 位置缓存被 `ConvertTGridToAudioTime`/`ConvertAudioTimeToTGrid` **逐对象**调用（帧内热路径）。
- **Avalonia：** `BpmList.cs:58-67,161-186`（`ContentVersion` 令牌 + O(1) 比较 + `#if DEBUG` 全表哈希对拍）、`Utils/NonceGenerator.cs`，`MeterChangeList`/`SoflanList` 缓存同读该令牌。
- **成本：** 一个 getter + 两处调用方（`MeterChangeList.cs:178-189`、`SoflanList_CachedPositionList.cs:201-218`）。WPF 已有等价的随机初值原语 `Utils/RandomHepler.cs:18-21`，`NonceGenerator` 可选；令牌字段/DEBUG 对拍约 10 行。
- **验收参考：** `BpmUniformPositionCacheBenchmarks`（1–1024 个 BPM 变更：11×–4559×，命中 0 分配）。

### A9 — 沙盘（PlayableArea）墙边界帧内候选缓存（`7aa9e41ed`）

- **Avalonia：** `Editors/DrawPlayableAreaHelper.cs:73-95`（`WallBoundaryCandidate` + 帧内候选构建）、`:133-138` 缓存左右候选、`:490,521-533` 按候选查询、`:546-557` 走 `ConnectableStartObject.TryGetValidPathChildRange`（`:328`）/`GetChildObjectAt`（`:374`）。
- **WPF：** 生效文件是 `Editors/DrawPlayableAreaHelper_new.cs`（`Drawing.cs:60,253` 实例化；旧 `DrawPlayableAreaHelper.cs` 为死代码）。`FieldAreaFrameContext`（`:72-84`）已有池化候选集合，但 `CalculateBoundaryXGridUnit`（`:499-541`）对每个（lane, sample, edge）都调 `lane.GetChildObjectsFromTGrid(tGrid)`（`:502`，有效路径上分配 List）与 `lane.IsPathVaild()`（`:503`，`children.All(...)`）；`ConnectableStartObject.cs` 只有 `GetChildObjectsFromTGrid`（`:353-381`）/`IsPathVaild`（`:409`），**缺** `TryGetValidPathChildRange`/`GetChildObjectAt`。
- **映射注意（重要）：** WPF 的 `DrawPlayableAreaHelper_new.cs` ≡ Avalonia 的 `DrawPlayableAreaHelper.cs`（移植时去掉了 `_new` 后缀），文件名不同、代码形状一一对应。
- **成本：** 2 文件（helper + `ConnectableStartObject` 共享基类）。
- **验收参考：** `DrawPlayableAreaProductionBenchmarks`（8×256 墙：2.40 ms/555 KB → 0.35 ms/203 KB）、`DrawPlayableAreaHelperNewP1Benchmarks`。

### A10 — 关闭编辑器时释放渲染资源（`a1adec1c8`；B-042）

- **WPF 缺口：** `PrepareRenderLoop` 只 `Initialize` 不 `Dispose`（`Drawing.cs:229-232`）；`DisposeRenderLoop`（`:1059-1072`）只摘渲染上下文、重置监听，既不释放每次挂接新建的 helper（`playableAreaHelper`/`playerLocationHelper`/`hitObjectEffectHelper`），也不清理 `drawTargetMap`；`IRenderManagerImpl` 无 `ReleaseRenderControl`（`Kernel/Graphics/IRenderManagerImpl.cs:39-44`）。
- **Avalonia：** 新增 `DisposeDrawingHelpers()`（`Drawing.cs:1237-1245`）、`DetachRenderControl()`、`IRenderManagerImpl.ReleaseRenderControl`（`:19-21`）、CTS/版本号保护的挂接，`DisposeRenderResources()`（`Drawing.cs:1254-1262`）串起来。
- **可移植核心：** helper 释放 + `drawingTargets`/`drawTargetMap` 清引用；**不要**顺手 Dispose 单例绘制目标——那正是 D1（RND-003）未决的所有权问题（Avalonia 自己也没做，`:1254-1262` 同样只清引用）。
- **成本：** 中；需新增 `ReleaseRenderControl` 并接到 Avalonia 式「挂接/卸载带取消令牌」的路径上。

### A11 — 绘制命令构建器的异常安全（`62957153e` 残余项）

- **背景：** `62957153e`（"reduce allocations in editor drawing"）的绝大部分在 WPF 树已存在（`VisibleLineVerticesQuery.cs:14,58-84`、池化 `DrawTimeSignatureHelper`/`DrawXGridHelper`/`CommonLinesDrawTargetBase`/`HoldDrawingTarget`/`LaneBlockerDrawingTarget`/`DurationSoflanDrawingTarget`/`IndividualSoflanAreaDrawingTarget`、`CommonHorizonalDrawingTarget` 分桶、`IList<LineVertex>` 契约、池化 `drawMap`、`DefaultSkiaStringDrawing` 的 `IStringMeasure` 接线……）。
- **WPF 仅缺：** `Kernel/Graphics/DrawCommands/DrawCommandListBuilder.cs:276-295` `DrawCircles` 先 `foreach` 校验再 `commands.Add(...Initialize(instanceList))`，抛异常时池化实例列表不归还；`:466-474` `CopyToPooledList` 同样无异常路径释放。Avalonia 用 `ownershipTransferred` try/finally 修掉。
- **成本：** 局部、低风险。

### A12 — `QuadTreeWrapper` 硬化（`b07edf916` 的三处小改）

- **WPF 缺口：** `Base/Collections/Base/QuadTreeWrapper.cs:52-57` `Add` 不去重（重复 Add 会重复订阅 `PropertyChanged`，`Remove` 后仍被强引用）；`:89-93` `OnItemPropChanged` 无锁、不校验注册表；`:68-75` `Query` 在 `CheckAndBuild()` 后直接解引用 `tree`，与并发的 `Add/Remove`（把 `tree` 置空）存在 NRE 竞态。
- **Avalonia：** `NotQuadTreeWrapper.cs:60-70`（去重）、`:72-82`（加锁 + 注册校验）、`:96-100`（空树容忍）。
- **不要顺带做的事：** 泛型 `TX/TY`/`IBounded` 重写属于实现风格差异、背后没有缺陷；WPF 的空集合短路、惰性构建、真实删除、端点正规化、`MaxDepth` 退化上限都已具备。

---

## 3. B 类：需适配

### B1 — 区间树并发安全 + `EnsureInSync` + 零分配 lane 查询 + 投射物帧内缓存（RND-008 三连）

三件事是一套，必须按顺序落地：

1. **`c697fda80` 区间树并发只读安全。** Avalonia `Base/Collections/Base/RangeTree/IntervalTree.cs:10-24,98-102,104-170,172-199`：写者私有 `staging`、单飞重建、`IntervalTreeNode` 不可变（删掉会就地清空活树的 `Release`）、`Volatile.Write` 原子发布；`IntervalTreeNode.cs:16-43`。
   WPF 现状：`IntervalTree.cs:11-14`（可变 `items` + `bool isInSync`）、`:61-88`（每次查询内联 `RebuildInternal`）、`:131-139`（`IntervalTreeNode.Release(root)` 就地清空后重建）、`IntervalTreeNode.cs:33-52`（递归抹除活节点）。WPF 的并行投射物路径（`ProjectileBatchDrawTargetBase.cs:353` `Parallel.ForEach`，`:313` 每项查 lane 区间）会在脏树上撞到这个窗口 → 正是文档记录的 RND-008 NRE 类。
2. **`ff19a917c` 暴露 `EnsureInSync` + 零分配查询。** Avalonia `IntervalTreeWrapper.cs:81-90`（`QueryInRangeInto`/`EnsureInSync`）、`ConnectableObjectList.cs:88-99`（`QueryVisibleStartObjectsInto`/`EnsureInSync`）；WPF 两处均**缺失**（全仓 grep 0 命中）；WPF 调用方仍是 `GetVisibleStartObjects(...).OfType<...>().LastOrDefault()`（分配 + 池化列表）。
3. **`96dc4287a` 投射物帧内 lane 缓存。** Avalonia `ProjectileBatchDrawTargetBase.cs:33`（`DrawBuffer.EnemyLaneCache`）、`:104,130,145`（清/建/租用点，必须逐一对齐以免跨帧串味）、`:350`（并行区前 `fumen.Lanes.EnsureInSync()`）、`:382-411`（按 `TGrid.TotalGrid` 命中、未命中走 `QueryVisibleStartObjectsInto`）。

- **成本：** 全套最大的一项（整文件重写 2 个 + 包装层 2 个 + 调用方 1 个）。
- **接口签名差异：** WPF `IIntervalTree.Remove` 返回 `bool`（`IIntervalTree.cs:44`），Avalonia 为 `void`；回移时保留 WPF 的 `bool`，否则 `IntervalTreeWrapper.Remove(:56-60)`、`IndividualSoflanAreaList.Remove(:45-55)` 会级联改动。
- **收益（Avalonia 实测）：** `8090_10.ogkr` 逐帧 **709.6 → 151.5 µs（4.68×）**、**1.34 MB → 135 KB/帧（9.9×）**；极端场景逐项约 200×；脏树并行未再 NRE。
- **验收参考：** `IntervalTreeConcurrencyBenchmarks`、`ProjectileBatchRealChartBenchmarks`、`ProjectileLaneCachePoolingBenchmarks`、`ExtremeProjectileScenarioBenchmarks`、`ProjectileBatchLaneCacheTests`。

### B2 — 帧体 try/finally 重构（`62957153e` 的结构性部分）

- **Avalonia：** `Drawing.cs:448` 起 `try { … } finally { …逐项 Dispose 内层池化对象 + drawMap.Clear() }`，`End:` 只负责 `builder?.Dispose()`。
- **WPF：** 无 try/finally——清理内联在成功路径（`Drawing.cs:621-629`），`End:`（`:633-635`）的 `drawMap.Clear()` 是**唯一**的帧末清理，早退帧（`:302-303`、`:315-318`）依赖它。
- **回移顺序：** 先把清理搬进 finally（并覆盖 `DisposeRenderResources` 语义），才轮到 C2 的「删除冗余 `Clear()`」；直接删 = 回归。

### B3 — 限帧帧保留 front 命令列表（`9b76967f8`）

- **Avalonia：** `Kernel/Graphics/DrawCommands/DrawCommandListContextSlots.cs:61-90` `Present` 不再消费/释放 front；`DefaultSkiaRenderContext.cs:92-102` 无条件 `Swap + Present`。
- **WPF：** `DrawCommandListContextSlots.cs:60-98` 仍是 pre-fix 形状（`:73` `slot.Front = null;`、`:95-96` AutoDispose 释放）；`DefaultSkiaRenderContext.cs:106-114` 与 `DefaultOpenGLRenderContext.cs:103-109` 都是 `if (!manager.SwapDrawCommandList(this)) return;`。
- **为什么不能直接回移：** Avalonia 修的是「闸门在命令构建内部、被丢弃的帧仍会走到 Present、把合成器画布留空」这一症状；WPF 的闸门在 `OnRender` 之前（见 C1），被丢弃的帧根本不进 Present。共享的 `if (!Swap) return` 形状仍会在「构建成功但 back 槽为空」的路径上跳过呈现，是否需要改必须按各后端的 surface 生命周期（SKElement 的 `PaintSurface` vs GLControl 后缓冲）重新论证。**暂缓，不作为第一批候选。**

---

## 4. C 类：不适用 / WPF 已等价

| 编号 | 说明 |
| --- | --- |
| C1 | **RND-C2 限帧闸门（`5de442d4c`）**：WPF 的闸门位于 render context，`TryUpdateRenderTime` 返回 false 时**根本不会** `OnRender?.Invoke`（`Skia/DefaultSkiaRenderContext.cs:67-75,86-104`、`OpenGL/DefaultOpenGLRenderContext.cs:85-101,111-129`），因此「先建 builder 再丢弃」在 WPF 结构上不可能发生。 |
| C2 | **RND-C3 冗余 `Clear()`（`6c1504952`）**：WPF 没有 finally，`End:` 的 `drawMap.Clear()`（`Drawing.cs:635`）是所有可达路径的唯一清理，删除即回归（见 B2）。 |
| C3 | **容器回退（`8dbb123d3` 的 `ConcurrentDictionary` 部分）**：WPF `Drawing.cs:281` 本来就是普通 `Dictionary`（并发容器只在 Avalonia 由 `258c32003` 引入过）。 |
| C4 | **每上下文渲染性能面板（`6fa0784f8`）**：WPF 2026-05 已有等价面板（`Kernel/Graphics/Performence/Views/RenderPerfomenceMeasurePanelView.xaml:65-70`、`ViewModels/RenderPerfomenceMeasurePanelViewModel.cs:31,219-259`），监视器选项、每上下文 Frame/OnRender/Present 分列计时均已具备；WPF 全仓无 `ShowFPS`/`IsDisplayFPS`/FPS 覆盖层。Avalonia 这次是**追平 WPF**。 |
| C5 | **滚动条常显（`0266ac424`）**：`AllowAutoHide` 是 Avalonia ScrollBar 的属性；WPF 竖直滚动条无可见性绑定（`FumenVisualEditorView.xaml:149-155`），本就常显。 |
| C6 | **ISF 区域匹配（`41e4cdb9f`）**：`git show --stat` 显示同一提交改了两棵树的 `IndividualSoflanAreaListMap.cs`，WPF 已是「最早起点 + 半开区间」形态（`:154-162`）。 |
| C7 | **Connectable 显示枚举（`b07edf916` 的 T-005 部分）**：WPF `ConnectableStartObject.cs:286-297`、`ConnectableChildObjectBase.cs:335-340`、`OngekiFumen.cs:391-413` 已是目标顺序（Start → 控制点 → child），Avalonia 的回归契约在 WPF 代码上本来成立。 |
| C8 | **浏览器/WASM 与多线程输入（`bb2b6035f`、`523bf28fb`、`48873c0db`、`05025e4c0`、`558155fe9` 等）**：平台专属。 |
| C9 | **桌面合成器时钟提升（`be657f7f6`）**：Avalonia 桌面平台 API，WPF 无对应物（若要改善 WPF 帧节奏应走别的路，不属本次回移清单）。 |

---

## 5. D 类：Avalonia 侧仍是提案（不可回移，仅登记）

以下条目**在 Avalonia 侧也尚未落地**，其复审文档列为待办；WPF 存在同类缺陷，但现在没有「已实施的改动」可搬。

| 编号 | 条目 | 位置（Avalonia 复审文档） | WPF 现状 |
| --- | --- | --- | --- |
| D1 | RND-003 绘制目标是进程级单例、每次挂接重载纹理且旧原生对象不释放 | `render-perf-review-2026-09-22.md` §3/§4.2，列为**当前最优先** | 同一缺陷：`TextureLaneEditorObjectDrawingTarget.Initialize:27-34` 重载 3 张纹理不释放，`Dispose:36-41` 不置空；目标以 MEF `Shared` 注册 |
| D2 | AUD-001 设置属性 setter 内同步落盘（整表复制 + fsync + 替换） | §5 | `Kernel/Audio/NAudioImpl/NAudioManager.cs:48-59,61-72` setter 直连 `AudioSetting.Default.Save()`；WPF 已有可复用的合并保存器 `Modules/FumenVisualEditor/Models/EditorSetting.cs:15-23` `RequestSave()`，但音频 setter 没用它 |
| D3 | AUD-003 `PeakPoint` 每 1ms 点分配 `float[channels]` | §5 | `Kernel/Audio/SamplePeak/DefaultSamplePeak.cs:31` 同上 |
| D4 | AUD-004 1ms `DispatcherTimer` 驱动音频事件 + 每 tick `Where(...).ToArray()` | §5 | WPF 机制不同（`DefaultFumenSoundPlayer` 用 `AbortableThread`，非 1ms 定时器），但每 tick 的 `.ToArray()`（`:450`）同样存在 |

另有 RND-001（可见点裁剪）、RND-007（跨区间重复枚举）、RND-010（每 lane 一条命令）、RND-015（玩家位置标记 `Vector4.Zero`）、EDT-003（拖动全量扫描）、DAT-005（脏版本全树重建）等，均为**未实施建议**，等 Avalonia 落地并测量后再评估回移。

---

## 6. 跨向注意

1. **两个结构性差异会破坏 1:1 映射（回移时最容易踩的坑）：**
   - **限帧闸门位置：** WPF 在 `IRenderContext` 内、`OnRender` 之前（C1）；Avalonia 在 `OnEditorRender` 内。→ C1/C2/B3 都是它的连带结论。
   - **帧体清理结构：** WPF 内联清理 + `End:` 兜底；Avalonia try/finally + `End:` 只释放 builder。→ B2 必须先做。
2. **线程模型：** WPF 编辑器渲染可跑在专用渲染线程（`SkiaRenderControl_D3D9On12`）并靠 `RenderDataLock` 桥接；Avalonia 命令构建固定在 UI 线程（`PrepareFrame` 里 `Dispatcher.UIThread.VerifyAccess()`），回放在合成器线程。A1 的收益在 WPF 侧因此**更强**：`ScrollTo` 写 `CurrentPlayTime` 并不持写锁。
3. **反向待同步仍然存在：** WPF 树另有 16 个未同步提交（`WPF-MIGRATION-SYNC.md` §3：`ISkiaRenderContext` 抽象、后端无关离屏渲染、波形离屏图块、Skia 字形图集等）。其中图形/波形提交与 A9/A10/B1 触碰的目录相邻，回移 A 类时应逐个文件确认不冲突（本报告只读核对的是**当前** WPF 源码）。
4. **不要把「Avalonia 有、WPF 没有」等同于「回移」：** 本报告中有 9 项（C 类）是平台差异或 WPF 反而更优；Avalonia 的部分提交（`d18443342`、`6b1f3b5c5`、`f4b58c48d`、`c1b001006` 等 T-001/T-010/T-013/T-018「migrate WPF hot-path」）方向本来就是 WPF → Avalonia，不要反向回搬。

---

## 7. 建议顺序

- **第一批（文件局部、低风险、可独立验收）：** A6 → A7 → A8 → A3 → A4 → A1 → A2 → A5 → A11 → A12
  （A6/A7/A8 是纯数据结构，与渲染无关、随时可做；A1/A2/A3/A4 是编辑器渲染路径，建议同一批回归。）
- **第二批（跨文件但机械）：** A9 → A10
- **第三批（需设计，单独排期）：** B1（区间树并发 + 零分配查询 + 投射物缓存，一个整体）→ B2（帧体 try/finally）→ 之后再评估 B3
- **可复用的 Avalonia 验收资产**（`Avalonia/benchmarks/OngekiFumenEditor.Avalonia.Benchmark/Benchmarks/`）：`VisibleContextCheckBenchmarks`、`BpmUniformPositionCacheBenchmarks`、`BulletPalleteListAccessBenchmarks`、`MeterChangeListQueryBenchmarks`、`DrawPlayableAreaProductionBenchmarks`、`IntervalTreeConcurrencyBenchmarks`、`ProjectileBatchRealChartBenchmarks`、`ProjectileLaneCachePoolingBenchmarks`、`ExtremeProjectileScenarioBenchmarks`、`DisplayableEnumerationBenchmarks`；测试侧 `DrawingFrameSnapshotTests`、`DrawTimeSignatureHelperTests`、`ProjectileBatchLaneCacheTests`。
- **回归基线：** WPF 树改动后按 WPF 侧现有测试/手动冒烟验证（本报告不涉及 Avalonia 侧）。

## 8. 未决问题

1. **A1 的预览模式语义：** WPF `GetViewportTGrid()`（预览走 `previewScrollPositionMs`）与 `GetCurrentTGrid()`（走 `CurrentPlayTime`）目前由 `SetPreviewScrollPosition` 同步写（`ScrollViewer.cs:176-178`）；回移后如果将来允许两者分离，`FrameTime`/`FrameTGrid` 的回退分支必须明确取哪一个。
2. **B1 的签名与所有权：** WPF `IIntervalTree.Remove` 的 `bool` 返回值与 Avalonia 的 `void` 不同（保留 `bool` 可避免级联）；`IntervalTreeNode` 改不可变后 WPF 侧是否有依赖 `Release` 的调用方需单独确认。
3. **A10 与 D1 的边界：** 关闭编辑器时释放资源（A10 可做）与「单例纹理是否可复用/是否应改 `RegisterTransient`」（D1 未决）必须分开决策，Avalonia 文档亦未回答所有权问题（`render-perf-review-2026-09-22.md` §8.1）。

---

## 9. 回移落地记录（2026-09-29）

**决策：** A1–A4、A6–A12 全部回移（**A5 按决策不做**）；B1、B2 回移（B3 暂缓，不做）。逐项独立提交，每项提交前跑 WPF 解决方案构建。

| 项 | WPF 提交 | 说明 / 验证 |
| --- | --- | --- |
| 报告 | `b785a155` | 本文件 + `WPF-MIGRATION-SYNC.md` 记录 |
| A6 | `6f29ea3b` | 有序 backing + 二分插入 + 退订修正；临时冒烟 11 项断言（顺序/索引器/计数自洽、去重、替换、删除、自动 StrID） |
| A7 | `921afa6e` | 二分查询 + 直接枚举已排序 backing；对旧 LINQ 语义做 14 个探测点的差分断言（含 firstMeter 非最小值的边界） |
| A8 | `d8031bc7` | `BpmList.ContentVersion` 令牌 + `NonceGenerator` + Debug 全表哈希对拍；冒烟覆盖 BPM 值/TGrid/子属性/Add/Remove 五条失效路径，Debug 对拍未触发 |
| A3 | `e370ba23` | Meter/BPM 按可见区间枚举；评审核对了 `BinaryFindRange` 闭区间语义与 FirstMeter/首 BPM 的排除关系 |
| A4 | `3cb8e6b1` + `45afda72` | 帧内合并区间缓存；后续提交按评审意见在剪除未用组后重建缓存（见下方「与 Avalonia 的有意偏差」） |
| A1 | `f3b5cac3` | 帧时间快照；评审逐条核对了 12 个活跃读取点与快照来源语义，确认与删掉的实时 getter 完全等价 |
| A2 | `dd66479f` | 拍线几何改读当帧绘制上下文；评审确认设计模式下与 `RectInDesignMode` 等价、预览模式修掉陈旧值 |
| A11 | `0e1c9615` | 构建器异常安全（`DrawCircles` + `CopyToPooledList`） |
| A12 | `34b61417` | QuadTree 三处硬化；冒烟覆盖去重、属性失效、移除后不再复活、空区间 |
| A9 | `66cab9b0` | 沙盘墙边界帧内候选 + `TryGetValidPathChildRange`/`GetChildObjectAt`；对旧算法做 16 探测点 × 2 车道（4 child 含重复 TGrid / 单 child）的差分断言，并逐点比对有效路径边界求值 |
| A10 | `39742873` | 三个 helper 变 `IDisposable` 且可重复初始化；`DisposeRenderLoop` 释放 helper + 本编辑器的目标映射与帧状态；绘制目标单例不动（RND-003 未决） |
| B1 | `bbe3bf29` | 区间树分阶段重建 + 不可变节点 + 原子发布；`EnsureInSync`/零分配查询/lane 帧内缓存；冒烟：131 个探测点三种查询与暴力参考一致、`QueryInto` 与 `Query` 顺序一致、4 读者 × 20k 次查询 vs 写者 20k 次增删 0 异常、敌方 lane 查询与旧 LINQ 表达式 37 个探测点一致 |
| B2 | `e5698abd` | 帧体 try/finally，`drawMap.Clear()` 收敛为一处；`End:` 只保留 builder 释放 |

**验证的边界（如实说明）：** 上述均为构建 + 针对被改代码的临时差分冒烟（脚本已删除），**没有**在真实 WPF 编辑器里做可视化/帧率验收；A1/A2/A3/A4/A9/B2 的最终画面等价性仍以 Avalonia 侧已验收行为 + 逐行核对为依据。

**独立评审结论（只读 reviewer，覆盖 A1–A4 四项回移）：** `overall_correctness: correct`，四项均与 Avalonia 原提交一致、对 WPF 行为保持；唯一发现为低优先级（见下）。

**与 Avalonia 的有意偏差（1 处）：** `45afda72` 在「剪除未用 soflan 组」之后重建帧内可见区间缓存。Avalonia 保留剪除前的区间，于是 `CheckRangeVisible` 可能代表一个已不参与的组回答 `true`，让 `LaneBlocker`/`VisibleLineVerticesQuery` 多做几何提交（评审判定无像素变化、代价有界）。WPF 侧选择与旧实现（调用时现读 `drawingContexts`）完全一致。

**未回移项：** A5（用户决策）、B3（限帧帧保留 front 命令列表：症状在 WPF 结构上不发生，需按后端另行论证）、D 类四项（Avalonia 侧自身尚未落地）。
