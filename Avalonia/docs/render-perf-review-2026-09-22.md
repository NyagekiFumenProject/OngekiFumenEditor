# Avalonia 渲染与性能复审（2026-09-22）

> **结论性质：** 本文件是对当前源码基线的**静态复审**，重点覆盖编辑器渲染相关逻辑（渲染循环、可见性判定、缓存与内存占用）。复审期间未修改业务代码。凡标为“静态确认”的结论均有源码行号证据；帧率、分配速率、GC 暂停时长与真实用户可感知影响**仍需实测**。本文件不替代 `performance-gc-audit-2026-09-09.md`，而是对其续接与纠偏。

## 1. 复审范围与方法

- 基线：当前 `avalonia` 分支工作区（含 2026-09-11 至 2026-09-21 的全部修复）。
- 重点目录：
  - `src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/Graphics/`（绘制目标、绘制助手、可见性查询）
  - `src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.Drawing.cs`（帧循环主体）
  - `src/OngekiFumenEditor.Avalonia/Kernel/Graphics/`（绘制命令、Skia 后端、对象池、性能监视器）
  - `src/OngekiFumenEditor.Avalonia/Base/Collections/`（区间树、BPM/Meter 缓存）
- 方法：逐文件深读 + 调用方反查（`grep` 全仓调用点）+ 与既有审计条目逐项对拍，确认哪些已修、哪些仍在、哪些是新发现。
- 行号均为**本文件撰写时的当前行号**，与 09-09 版本的基线行号不同。

## 2. 优先级定义

| 级别 | 定义 |
| --- | --- |
| P1 | 帧循环/交互高频路径上的重复分配或高复杂度，且已确认有真实调用方；或原生资源生命周期缺陷会造成确定性内存增长。 |
| P2 | 有界操作的可避免开销，或需特定规模（大谱面/长音频/多变速组）才显著；或有明确收益但需先测量。 |
| P3 | 冷路径、微优化、或必须先测量再动的项。 |

**本轮无 P0。**

## 3. 结论摘要

本轮共确认 **16 项**可优化内容，其中 **P1 四项**、**P2 九项**、**P3 三项**。
截至 2026-09-22，其中 **2 项已修复并签入**（`RND-C1`、`RND-C2`，均含新老实现基准），其余 14 项仍待处理。

其中 **四项是新发现**（既有审计清单未收录）：`RND-C1`、`RND-C2`、`RND-C3`、`DAT-C1`。其余十二项为既有条目的**续存确认**，并更新了行号与影响面判断。

最值得先做的三件事：

1. ~~**`RND-C1`（默认帧的可见性判定走 `ConcurrentDictionary` 枚举）**~~ —— **已修复（2026-09-22）**。实测 `CheckRangeVisible` 提升 26.6×–74.0×、`CheckVisible` 提升 1.9×–9.5×（逐调用量纲），分配分别降至 0 B 与 35%–74%。详见 §4.1 对应条目的「状态：已修复」小节。
2. ~~**`RND-C2`（限帧闸门在分配之后）**~~ —— **已修复（2026-09-22）**。实测被丢弃帧成本 970.4 ns → 1.3 ns（≈770×），分配 896 B → 0 B。详见 §4.1 对应条目。
3. **`RND-003`（绘制目标是进程级单例，纹理重复加载不释放）** —— 每次挂接编辑器都重新分配全部原生 `SKImage` 而从不释放旧的一份，是唯一的**确定性原生内存增长**项。**当前最优先项。**

次优先：**`BPM-C1` / `DAT-C1`（`GetCachedAllBpmUniformPositionList` 的“缓存”仍未命中即全表 `Aggregate`）** —— 名为缓存实为每次全表哈希，且落在逐帧时间签名查询链上。

---

## 4. RND：渲染循环、可见性与绘制（重点分区）

### 4.1 新发现

#### RND-C1 — P1：`CheckVisible` / `CheckRangeVisible` 每次调用都枚举 `ConcurrentDictionary`

**简述.** 两个最热的可见性判定入口把 `drawingContexts`（一个 `ConcurrentDictionary<int, DrawingTargetContext>`）当普通集合遍历，每次调用都要走并发字典的枚举器（快照桶 + 版本检查），而调用方在**每个子对象**上都会命中它。

**证据.**
- 定义：`ViewModels/FumenVisualEditorViewModel.Drawing.cs:699-719`
  - `CheckVisible(TGrid)` → `foreach (var ctx in drawingContexts.Values)` `:701`
  - `CheckRangeVisible(TGrid, TGrid)` → `foreach (var ctx in drawingContexts.Values)` `:712`
- 容器声明为 `ConcurrentDictionary<int, DrawingTargetContext>`：同文件 `:265`
- 逐帧、逐子对象的调用方：
  - `TargetImpl/VisibleLineVerticesQuery.cs:45` 起：对每个 connectable **start** 调一次 `CheckVisible` + 一次 `CheckRangeVisible`（`:45`、`:46`）
  - `TargetImpl/VisibleLineVerticesQuery.cs:94`：对 start 的**每个 child** 再调一次 `CheckVisible`（`:94` 在 `foreach (var childObj in start.Children)` 体内）
  - `TargetImpl/EditorObjects/Lane/CommonLaneEditorObjectDrawingTarget.cs:44`、`TargetImpl/OngekiObjects/CommonHorizonalDrawingTarget.cs:82`、`TargetImpl/OngekiObjects/BulletBell/ProjectileBatchDrawTargetBase.cs:263` 均为逐对象调用
- 调用量级：`CommonLinesDrawTargetBase.FillLine:25-32` 对**每条可见 lane** 调一次 `QueryVisibleLineVertices`，后者再对 lane 的全部 child 逐个 `CheckVisible`。长 lane（curve path 展开后可达数百个点）即数百次字典枚举/帧/条。

**性能影响.** 成本 = `可见 lane 数 × 平均 child 数 × contexts 数` 次并发字典枚举。每次枚举虽为 O(contexts)（通常 2–8 个变速组），但并发字典枚举器比普通 `List`/数组遍历贵数倍（需 `TryGetValue` 式快照与 `_tables` 版本读）。属于纯浪费：帧内对 `drawingContexts` 的写入全部发生在**帧首单线程段**（`:305` `Clear()` 与 `:408` 逐组写入），此后到帧末只读，不存在并发访问。

**推荐改法.**
1. 帧首在写完 `drawingContexts` 后，把 values 物化到一个帧内复用的 `DrawingTargetContext[]`（或 `PooledList`），例如在 `:411` 取得 `defaultDrawingTargetContext` 处顺带建立 `frameDrawingContexts`。
2. `CheckVisible`/`CheckRangeVisible` 改为遍历该数组；`CheckRangeVisible(ctx, …)` 内部还有一处同类问题：`:953` 直接 `drawingContexts.SelectMany(x => x.Value.VisibleTGridRanges)`，绕开了传入的 `context` 参数，应一并改走帧内数组。
3. 若仍希望保留 `drawingContexts` 的并发读取能力，可退一步：把只读视图换成 `FrozenDictionary`/普通 `Dictionary` + `ReaderWriterLockSlim`，但**推荐第 1 种**，因为帧内写入/读取本就是严格分段的。

**风险.** 低。语义等价，只需保证帧首物化发生在所有写入之后。注意 `:305` 的 `Clear()` 与 `:408` 的写入之间的顺序，以及 `:509-511` 会移除未用组——建议物化点放在 `:511` 之后（即 `unusedSoflanGroups` 清理完毕、组集合稳定后）。

**溯源（2026-09-22 补充）.**
容器为何是 `ConcurrentDictionary` 已查明：由 `258c32003`（"Fix Avalonia editor interaction workflows"，2026-08-07）引入，是**连带改动**而非针对本字段的分析结论——同一提交把 `hits`（`Dictionary<OngekiObjectBase, Rect>`）也换成了 `ConcurrentDictionary`，并在 `hits` 上留了注释「Rendering can run on Avalonia's compositor callback while pointer input is handled on the UI thread」。该前提在当前基线已不成立：`OnEditorLoop`（`Drawing.cs:250-256`）自述渲染回调就在 UI 线程，唯一读 `drawingContexts` 的 UI 侧代码（`UserInteractionActions.cs:1694` `UpdateCurrentCursorPosition`，调用点 `:1114`、`:1471`）也在 UI 线程；而真正跨线程的命中表后来已改为「帧末发布不可变快照」（`hitBuildBuffer` + `Volatile.Read/Write(ref publishedHits)`，`:1833`/`:1839`/`:1860`）。也就是说 `hits` 已随新方案从并发容器回退走完，`drawingContexts` 上的并发容器属于**遗留未回退**。

**状态：已修复（2026-09-22）.**

实施内容（`FumenVisualEditorViewModel.Drawing.cs`）：
1. `drawingContexts` 由 `ConcurrentDictionary<int, DrawingTargetContext>` 回退为 `Dictionary<int, DrawingTargetContext>`；写入点 `:305` `Clear()`、`:408` 逐组写入、`:533` `Remove()`、`:1245` `Clear()` 全部位于渲染线程。顺带移除了因此失效的 `using System.Collections.Concurrent;`。
2. 新增帧内缓存 `List<(TGrid minTGrid, TGrid maxTGrid)> mergedVisibleTGridRanges`，在帧首（原 `:433` 处）物化一次，同时供 `EnumerateAllDisplayableObjects` 的全局范围使用；`CheckRangeVisible(TGrid, TGrid)` 改为扫描该缓存，**不再触碰字典**。
3. `CheckRangeVisible(DrawingTargetContext, …)` 保留原签名与「忽略 context、查任一组」的既有语义，改为委托给无 context 重载。经全仓 grep 确认**没有任何调用方**使用该重载（唯一 `CheckRangeVisible` 的生产调用点 `LaneBlockerDrawingTarget.cs:143` 与 `VisibleLineVerticesQuery.cs:46` 走的都是无 context 版）。
4. `CheckVisible(TGrid)` 改为遍历 `Dictionary.Values`（struct 枚举器，无快照分配、无接口派发）。

**基准（新增 `benchmarks/.../Benchmarks/VisibleContextCheckBenchmarks.cs`）.**
量纲：**逐次调用**（单次 `CheckVisible` / `CheckRangeVisible`）；换算单帧总成本需乘该帧调用次数（≈ 可见子物体数）。**不是整帧加速比**，只代表 RND-C1 所辖那部分成本的消除比例。`Isolate_*` 两变体用于把总收益拆成「容器回退」与「去掉 SelectMany」两部分。基准内已内置新旧结果对拍（`GlobalSetup` 里逐条断言，不一致直接抛），保证对比建立在语义等价之上。
机器：Ryzen 7 5800X / .NET 11.0.0-preview.7 / `--job medium`（IterationCount=15, LaunchCount=2, WarmupCount=10）。

`CheckVisible`（Original → Optimized，ns / 加速比 / 分配）：

| 组数 | 区间/组 | 查询数 | Original | Optimized | 加速 | 分配(B) |
|---|---|---|---|---|---|---|
| 1 | 1 | 64 | 11,131.8 | 1,177.6 | **9.45×** | 8704 → 3072 |
| 1 | 1 | 512 | 66,721.9 | 9,431.9 | **7.07×** | 69632 → 24576 |
| 1 | 4 | 512 | 81,462.6 | 14,515.5 | **5.61×** | 69632 → 24576 |
| 4 | 1 | 512 | 135,415.9 | 30,065.2 | **4.50×** | 139616 → 82272 |
| 4 | 4 | 512 | 140,447.3 | 46,983.4 | **2.99×** | 137984 → 80640 |
| 16 | 1 | 512 | 176,621.0 | 94,691.8 | **1.87×** | 412640 → 306144 |
| 16 | 4 | 512 | 298,487.6 | 152,865.0 | **1.95×** | 410144 → 303648 |

`CheckRangeVisible`（同上）：

| 组数 | 区间/组 | 查询数 | Original | Optimized | 加速 | 分配(B) |
|---|---|---|---|---|---|---|
| 1 | 1 | 64 | 7,145.8 | 96.5 | **74.0×** | 11776 → **0** |
| 1 | 1 | 512 | 54,091.7 | 754.8 | **71.7×** | 94208 → **0** |
| 1 | 4 | 512 | 75,166.0 | 1,749.3 | **43.0×** | 94208 → **0** |
| 4 | 4 | 512 | 192,421.1 | 6,493.6 | **29.6×** | 149600 → **0** |
| 16 | 1 | 512 | 271,030.0 | 6,867.8 | **39.5×** | 372272 → **0** |
| 16 | 4 | 512 | 603,197.9 | 22,645.3 | **26.6×** | 367952 → **0** |

收益拆解（`Isolate_` 对比，加速比 = Concurrent / Plain）：
- **容器回退（并发字典 → 普通字典）贡献 1.4×–9.0×**，组数越少越显著（组=1 时 5.2×–9.0×；组=16 时 1.4×–2.0×，此时遍历 O(组数) 本身开始占主导）。
- **去掉每次现算的 `SelectMany` 贡献 26.6×–74.0×**，这是本轮最大的单项收益——原实现每次调用都要重新走两层迭代器状态机 + 委托调用并摊平全部组的区间。
- `CheckRangeVisible` 优化后分配降为 **0 B**（原先每次调用都要为 `SelectMany` 链、迭代器与 `Dictionary<K,V>.Enumerator` 装箱付钱）；`CheckVisible` 分配降至原来的 35%–74%（省掉了并发字典 `Values` 的快照数组）。

**验证.** Release 全量测试 808 passed / 0 errors / 0 failed；主工程与基准工程均 0 编译错误。

---

#### RND-C2 — P1：FPS 限帧闸门在**分配之后**才判断，被丢弃的帧仍付出完整构建器分配

**简述.** `OnEditorRender` 在进入限帧判断**之前**就创建了 `DrawCommandListBuilder` 及其一整套池化容器与原生对象；当帧被限帧丢弃（`goto End`）时，这些对象刚被创建就立刻 `Dispose` 归还。限帧越生效，浪费比例越高。

**证据.** `ViewModels/FumenVisualEditorViewModel.Drawing.cs:283-298`

```
var builder = renderImpl?.CreateDrawCommandListBuilder();   // :287 先创建

if (actualRenderInterval > 0)
{
    var ms = sw.ElapsedMilliseconds;
    if (ms < actualRenderInterval)
        goto End;                                            // :293 后判断，丢弃
    ...
}
```

`CreateDrawCommandListBuilder` 的实现（`Kernel/Graphics/Skia/DefaultSkiaDrawingManagerImpl.cs:95-98`）是

```
return new DrawCommandListBuilder(new DefaultSkiaStringDrawing(this));
```

而 `DrawCommandListBuilder` 构造函数（`Kernel/Graphics/DrawCommands/DrawCommandListBuilder.cs:37-47`）每次都从对象池租用：2 个 `PooledDictionary<Type,int>`、4 个 `PooledList`（commands / 3 个矩阵栈）。`DefaultSkiaStringDrawing` 构造函数（`Kernel/Graphics/Skia/Drawing/StringDrawing/DefaultSkiaStringDrawing.cs:29-31`）则新建 **3 个原生对象**：`textPaint`、`decorationPaint`（`SKPaint`）与 `reusableFont`（`SKFont`）。

**性能影响.** 每次被丢弃的帧：约 6 次池租借 + 1 次分配（builder 本身）+ 3 次原生 Skia 对象构造与销毁。以 `LimitFPS=60` 且合成器以 120Hz 回调为例，一半的帧会被丢弃，即每秒约 60 次无意义构建。原生 `SKPaint`/`SKFont` 的构造/析构涉及非托管分配，比托管分配更贵，且会把终结压力推给 GC。

**推荐改法.** 有两条路，建议按顺序考虑：
1. **把闸门提到创建之前**（最小改动）：在 `:287` 之前先算 `actualRenderInterval`/`sw` 判断，只有确定要出帧时才创建 builder。注意 `goto End` 分支当前依赖 `builder?.Dispose()`（`:657`）——提前判断后该分支不再有 builder，`builder` 需保持 `null`。同时需核对被跳过的帧是否仍需执行 `:303` 的 `ClearHitObjects()` 与 `:305` 的 `drawingContexts.Clear()`（现行为是**跳过**，因为 `goto End` 在它们之前；保持等价即可）。
2. **让 builder 按 render context 复用**：给 render context 挂一个长生命周期 builder，逐帧 `ResetFrameState()` 而非重建。这一步能同时消掉 3 个原生对象与全部池租借，但改动面更大，建议先做第 1 步并测量。

**风险.** 第 1 步低（纯控制流重排）；第 2 步中（需保证 `GetDrawCommandList()` 交出所有权后 builder 状态可安全重置，且与 `PERF-RND-017 / RND-20` 的 replay 缓存保持同一套生命周期纪律）。

**状态：已修复（2026-09-22，按方案 1）.**

实施内容（`FumenVisualEditorViewModel.Drawing.cs`，+7/-1）：把限帧闸门整体移到 `CreateDrawCommandListBuilder()` **之前**——先判断是否出帧，通过后才创建 builder；被丢弃的帧直接 `goto End`，builder 保持 `null`。

实现细节（对应上面「注意」里点出的两个坑）：
- `builder` 的**声明**留在闸门之前（`IDrawCommandListBuilder builder = null;`），赋值移到闸门之后。这是必需的：`goto End`（`:314`）会跳过赋值，若沿用 `var builder = ...` 的写法，编译器在 `End:` 处的 `builder?.Dispose()` 报 **CS0165「使用了未赋值的局部变量」**（实测确实报了，不是理论风险）。声明为 `null` 后 `End:` 的 `builder?.Dispose()` 是空操作，与「被丢弃帧没有 builder」语义一致。
- 已核对跳过帧的既有行为**保持不变**：`goto End` 仍位于 `ClearHitObjects()`（`:326`）与 `drawingContexts.Clear()`（`:328`）之前，即被丢弃帧照旧不清命中表、不清绘制上下文——改动前后一致。
- `CreateDrawCommandListBuilder()` 无副作用（只租池对象 + 构造原生对象），移动它不改变任何编辑器状态。

**基准（新增 `benchmarks/.../Benchmarks/EditorRenderFpsGateBenchmarks.cs`）.**
量纲：**一次被丢弃帧的构建成本**（逐帧量纲）。出帧帧的成本两种实现相同，故只测被丢弃帧这一条路径——这**不是**整帧加速比。基准走**真实生产路径**（真 `DefaultSkiaDrawingManagerImpl.CreateDrawCommandListBuilder()` → 真 `DrawCommandListBuilder` + 真 `DefaultSkiaStringDrawing`），非合成复刻；`Isolate_*` 用于拆分成本来源。因会构造原生 SkiaSharp 对象，按既有 Skia 基准惯例挂 `InProcessNoEmit`（故输出里每个方法有两组 job，数值以 `MediumRun`/DefaultJob 为准，`ShortRun` 仅 3 次迭代可忽略）。
机器：Ryzen 7 5800X / .NET 11.0.0-preview.7 / `--job medium`。

| 方法 | Mean | 分配 | 说明 |
|---|---|---|---|
| `Original_DiscardedFrame` | **970.4 ns** | 896 B | 修复前：先建 builder 再判断丢弃，随后立即 Dispose |
| `Optimized_DiscardedFrame` | **1.3 ns** | **0 B** | 修复后：闸门先行，根本不创建 |
| `Isolate_NativeStringDrawingCtor` | 529.9 ns | 552 B | 只量 `DefaultSkiaStringDrawing` 的 3 个原生对象 |
| `Isolate_PooledBuilderCtor` | 118.7 ns | 344 B | 只量 `DrawCommandListBuilder` 的 6 次池租借 |

- 单次被丢弃帧成本 **970.4 ns → 1.3 ns（≈770×，节省 969 ns）**，分配 **896 B → 0 B**。
- 成本构成：原生 Skia 对象构造（530 ns）占 **55%**，池租借（119 ns）占 12%，余下约 33% 是 builder 自身分配与随后的释放路径。也就是说**主要代价确实来自原生对象**，正如原分析所推测。
- 换算：`LimitFPS=60` + 合成器 120Hz 回调（约一半帧被丢弃）→ 每秒消除约 **58 µs** 的无意义构建与 896 B/帧 的分配压力。绝对值不大，但这是**纯浪费**且改动只有一处控制流重排，属于「白拿的收益」。

**验证.** Release 全量测试 808 passed / 0 errors / 0 failed；主工程与基准工程均 0 编译错误。

---

#### RND-C3 — P2：`drawMap` 每个成功帧被 `Clear()` 两次，且清空时机破坏池化收益

**简述.** `drawMap` 是「绘制目标 →（帧内上下文 → 对象列表）」的池化字典映射。它在同一个成功帧内被清空**两次**（`finally` 与 `End:` 标签各一次），且 `finally` 中的 `drawingCollectionDisposables` 已经把所有内层池化对象 `Dispose`（归还对象池）后，`drawMap.Clear()` 只是再清一次外层字典的条目。

**证据.** `ViewModels/FumenVisualEditorViewModel.Drawing.cs`
- 声明：`:54` `private readonly Dictionary<IFumenEditorDrawingTarget, IPooledDictionary<DrawingTargetContext, IPooledList<OngekiObjectBase>>> drawMap = new();`
- 成功路径：`:641-648` 的 `finally` 逐个 `Dispose` 内层池化对象，随后 `:648 drawMap.Clear()`
- 紧接着：`:656-658` 的 `End:` 再次 `drawMap.Clear()`
- 另有 `:1210` 在 `DisposeRenderResources` 中第三次调用

**性能影响.** 单次 `Dictionary.Clear()` 在条目数少（绘制目标数量级，约 20–40）时成本很小，**两倍仍然很小**——所以这项本身不是热点，列为 P2 的原因在于它揭示了结构问题：外层 `drawMap` 是**普通 `Dictionary` 且永不重建**，而内层 `IPooledDictionary` 每帧从对象池租还。于是每帧都在做「把 N 个池化字典挂到同一个外层字典上，用完再全部摘掉」。这既让外层字典的条目长期无效，也让内层字典的池化收益打折扣（池化字典被反复租还，但外层键值对的生命周期管理成了纯开销）。

**推荐改法.**
1. 删掉 `End:` 处重复的 `drawMap.Clear()`（`:658`）——`finally` 已覆盖成功与异常两条路径，`End:` 的这次是冗余的（注意 `goto End` 的两个早退分支 `:293`/`:322` **不经过** `finally`，因为 `try` 从 `:424` 才开始，所以 `:658` 只能覆盖「`try` 之前早退」的情形，而那时 `drawMap` 本就为空）。
2. 更有价值的做法：把「目标 → 帧内上下文 → 列表」的中间层改为帧内固定的二级结构（例如 `drawMap` 复用同一批内层字典，帧首只 `Clear()` 内层而不重新租还），或直接按 (target, context) 建立帧内线性索引。**此项需先测量**绘制目标数与其在各 frame 的出现率，再决定是否值得改。

**风险.** 低（第 1 步）；中（第 2 步，涉及池化所有权语义）。

---

#### DAT-C1 — P2：BPM 位置“缓存”每次调用都对整表做 `Aggregate`

**简述.** `GetCachedAllBpmUniformPositionList` 是名字里的“缓存”，但命中判断本身就要遍历整张 BPM 表做 `Aggregate` 求哈希。缓存只在**真的过期**时才省下重算，命中路径的开销与表长线性相关。

**证据.** `Base/Collections/BpmList.cs:148-162`

```
var hash = this.Aggregate(0, (x, e) => HashCode.Combine(x, calcHash(e)));  // :151 每次调用都跑
hash = HashCode.Combine(hash);                                             // :152
if (hash != cachedBpmContentHash) { UpdateCachedAllBpmUniformPositionList(); cachedBpmContentHash = hash; }
return cachedBpmUniformPosition;                                           // :161
```

对比同仓的 `MeterChangeList.GetCachedAllTimeSignatureUniformPositionList`（`Base/Collections/MeterChangeList.cs:205-216`）用 `bpmList.cachedBpmContentHash` 做 O(1) 版本号判断——说明本仓已有正确的做法，BPM 侧没跟上。

**性能影响.** 命中路径 = O(n) 委托调用 + 装箱（`Aggregate` 的 `int` 累加器在泛型 `Aggregate<int,int>` 下不装箱，但每项一次委托调用与 `HashCode.Combine` 是实打实的）。唯一调用点 `TGridCalculator.cs:279`（`GetAllBpmUniformPositionList`）本身不热，但它的兄弟 `GetCurrentTimeSignature`（`:269-274`）走的是 `meterList.GetCachedAllTimeSignatureUniformPositionList(bpmList)`，后者**内部依赖 `bpmList.cachedBpmContentHash`**（`MeterChangeList.cs:207`）——也就是说 BPM 版本号是两者共同的失效依据。当前 BPM 侧没有维护「变更即递增」的版本号，只有一个随机初始值（`BpmList.cs:52,118` 的 `RandomHepler.Random(...)`），所以 BPM 侧只能靠全表哈希。

**推荐改法.** 给 `BpmList` 引入单调递增的脏版本号（与 `MeterChangeList` 一致的做法）：在 `Add`/`Remove`/`Clear` 及各 `BPMChange` 的 `TGrid`/`BPM` 变更回调里 `version++`，`GetCachedAllBpmUniformPositionList` 改为比较版本号而非全表哈希。这样命中路径降为 O(1)。注意 `MeterChangeList` 已把 `cachedBpmContentHash` 当失效依据，改造时需同步其语义（把「内容哈希」改为「版本号 + 必要时回退哈希」，或让 Meter 侧改读版本号）。

**风险.** 中。必须确保**所有**能让 BPM 内容变化的路径都递增版本号，否则会出现陈旧缓存。当前 `cachedBpmContentHash` 已是「内容哈希」语义，改造要覆盖 `BpmList` 全部变更入口；建议保留一处 `Log` 或在 DEBUG 下断言。

---

### 4.2 续存确认（既有条目仍然存在）

#### RND-003 — P1：绘制目标是进程级单例，纹理重复加载且旧的原生对象从不释放

**简述.** 所有 `IFumenEditorDrawingTarget` 以 `RegisterSingleton` 注册，是**进程级共享实例**。`PrepareRenderLoop` 每次挂接编辑器都对全部目标重新 `Initialize`，而各目标的 `Initialize`/`Dispose` 既不释放上一代资源，也不把字段置空。

**证据.**
- 单例注册（抽样）：`TargetImpl/EditorObjects/Lane/NormalLaneEditorObjectDrawingTarget.cs:6`、`WallLaneEditorObjectDrawingTarget.cs:6`、`BeamEditorObjectDrawingTarget.cs:6`、`TargetImpl/OngekiObjects/Holds/HoldDrawingTarget.cs:17`、`FlickDrawingTarget.cs:13`、`CommonHorizonalDrawingTarget.cs:18`、`TargetImpl/EditorObjects/LaneCurvePathControlDrawingTarget.cs:16` 等，均为 `[RegisterSingleton<IFumenEditorDrawingTarget>]`。
- 每次挂接都重新 `Initialize`：`ViewModels/FumenVisualEditorViewModel.Drawing.cs:209-212`
  ```
  drawingTargets = availableDrawingTargets.ToArray();
  foreach (var drawTarget in drawingTargets)
      drawTarget.Initialize(renderImpl);      // :212 无 Dispose 前置
  ```
  调用链入口为 `:193`（`IoC.GetAll<IFumenEditorDrawingTarget>()`，即取**同一批单例**）。
- `Initialize` 覆盖字段而不释放：`TargetImpl/EditorObjects/Lane/TextureLaneEditorObjectDrawingTarget.cs:27-34`
  ```
  startEditorTexture = LoadTextrueFromDefaultResource(impl, startEditorTextureName);   // :31
  nextEditorTexture  = LoadTextrueFromDefaultResource(impl, nextEditorTextureName);    // :32
  endEditorTexture   = LoadTextrueFromDefaultResource(impl, endEditorTextureName);     // :33
  ```
- 纹理是原生对象：`Utils/ResourceUtils.cs:27-31`（`OpenReadTextureFromResource` → `impl.LoadImageFromStream`）→ `Kernel/Graphics/Skia/Base/SkiaImage.cs`：`public SKImage Image { get; private set; }`，`Dispose()` 才释放。
- `Dispose` 不置空：`TextureLaneEditorObjectDrawingTarget.cs:36-41`
  ```
  public void Dispose() { startEditorTexture.Dispose(); nextEditorTexture.Dispose(); endEditorTexture.Dispose(); }
  ```
  三个字段保持指向已释放对象；再次 `Dispose`（或再次 `Initialize` 前的任何读取）会作用于已处置的 `SKImage`。
- 卸载路径**不处置目标**：`ViewModels/FumenVisualEditorViewModel.Drawing.cs:1202-1213` 的 `DisposeRenderResources`
  ```
  drawingTargets = [];        // :1206 仅清引用
  drawTargetOrder = [];
  drawTargetMap.Clear();
  ```
  没有任何 `foreach (var t in drawingTargets) t.Dispose()`。

**性能影响.** 原生 `SKImage` 持有像素缓冲（编辑器纹理可达数十 KB 至数百 KB/张，lane 起点/中段/终点各一张，另有 tap/flick/beam/lane 曲线控制等）。**每次打开/重新挂接编辑器都会再分配一整代纹理，而上一代永不释放**——这是本报告中唯一的确定性原生内存增长项，长时间反复开关编辑器可持续累积。`SKImage` 若依赖终结器回收，还会给 GC 带来额外终结压力。

**推荐改法.**
1. **让 `Initialize` 幂等**：各目标在 `Initialize` 开头先释放已有的三张纹理（或抽出 `ReleaseTextures()` 统一调用），再做加载。
2. **让 `Dispose` 完整**：把字段置 `null`（`Interlocked.Exchange` 或简单置空），并加 `disposed` 保护，避免对已释放 `SKImage` 二次操作。
3. **在卸载路径显式处置**：`DisposeRenderResources` 中对 `drawingTargets` 逐个 `Dispose`——但**前提是**确认目标确为「每编辑器独占」。鉴于它们是 `RegisterSingleton`，更稳妥的做法是**不要在卸载时 Dispose 单例**，而是改成幂等的「重载即换新」语义（第 1 步），并把释放点收敛到 `Initialize` 与应用的最终 `Dispose`。这一点必须先确认所有权，否则会引出「编辑器 A 卸载把编辑器 B 正在用的纹理释放了」的新缺陷。
4. 同族的其它目标需一并核对：`TapDrawingTarget.Initialize.cs`、`TapDrawingTarget.cs`、`FlickDrawingTarget.cs`、`IndividualSoflanAreaDrawingTarget.cs`、`LaneCurvePathControlDrawingTarget.cs`、`DefaultSkiaDrawingManagerImpl.cs`（纹理/`SKImage` 覆盖处）。

**风险.** **中高**——所有权不清晰正是本项的存在原因，动手前必须先回答：「一个 `IFumenEditorDrawingTarget` 实例是否可能同时被两个编辑器使用？」若答案是肯定的（考虑到它们是 DI 单例，很可能），则释放策略必须改为「引用计数」或「改为每编辑器实例（`RegisterTransient`）」。建议先做所有权梳理，再落代码。

---

#### RND-001 — P1：可见范围相交后仍展开全部 child/curve point

**简述.** 一旦某个 connectable 的范围与视口相交（`alwaysDrawing=true`），其**全部** child 与 curve point 都会被变换并发出，包括明确在视口外的内部点。

**证据.** `TargetImpl/VisibleLineVerticesQuery.cs`
- `:46` `var alwaysDrawing = target.CheckRangeVisible(start.MinTGrid, start.MaxTGrid);`
- `:94` `var visible = alwaysDrawing || target.CheckVisible(childObj.TGrid);`
- `:109-125` `if (visible || prevVisible) { … PostPoint2 / PostObject … }` —— `alwaysDrawing` 为真时每个 child 都进这个分支

**性能影响.** 长 hold/长 lane 的成本与**总点数**成正比，而非与**可见点数**成正比。视口只显示整条 lane 的一小段时，仍要变换并输出全部点。此成本随缩放级别恶化（放大时可见比例更小）。

**推荐改法.** 保留跨界端点（保证线段连续），但对确定在视口外的**内部** child/curve segment 做裁剪：可先用 `CheckVisible` 得到 child 的可见性，仅在从不可见变可见、或从可见变不可见时补发边界点，中间连续不可见段整体跳过。注意现有去重/简化逻辑（`:131-156`）依赖顶点的邻接关系，裁剪后需保证不破坏简化前提。

**风险.** 中。几何裁剪容易引入视觉瑕疵（线段断裂、dash 相位错位）。

---

#### RND-007 — P2：合并可见范围时重复枚举静态对象

**简述.** 对每个 merged visible range 重复枚举 Meter/BPM 与各类 Soflan，并把结果 `AddRange` 进 target/context map，导致同一对象在多个 range 下被反复登记。

**证据.** `ViewModels/FumenVisualEditorViewModel.Drawing.cs`
- `:433` `var allVisibleTGridRanges = drawingContexts.Values.SelectMany(x => x.VisibleTGridRanges).Merge();`
- `:434` `using var visibleObjects = EnumerateAllDisplayableObjects(fumen, allVisibleTGridRanges);`
- `:435-468` 逐 displayable 填入 `map`（`obj.IDShortName` → soflanGroup → list）
- `EnumerateAllDisplayableObjects` `:752-834`：对每个 `(min,max)` range 逐类 `BinaryFindRange`/`GetVisibleStartObjects`，其中 `:767-768` 的 Meter/BPM 还额外 `.Except(filterMeterChange)` / `.Except(filterFirstBpm)`（`Except` 分配 `HashSet` + 迭代器）

**性能影响.** range 数 = 变速组可见区间之和（多变速组时可达数十）。每 range 都要走一遍全部集合族的区间查询与 `Except`。`Except` 在此为「排除单个首元素」而支付完整 `HashSet` 构建成本。

**推荐改法.**
1. `Except(filterMeterChange)` / `Except(filterFirstBpm)` 这类「排除单元素」改为在 `AppendDisplayables` 内做一次引用/O(n) 判等跳过，删掉 `Except`。
2. 对跨 range 稳定不变的静态集合（Meter/BPM/Soflan），按帧合并区间后**一次**查询，而非逐 range 查询再合并。
3. `map` 的填充可考虑直接按 (target, soflanGroup) 建帧内索引，减少中间层。

**风险.** 低–中。`Merge()` 的区间合并语义需保持（重叠区间合并后不能漏对象）。

---

#### RND-008 — P2：投射物预筛选范围过宽 + 逐项重复 lane 查询（且在并行体内）

**简述.** 预览模式下每帧按 `[curTGrid, TGrid.MaxValue]` 全量取出 bullet/bell，随后逐个判断可见性；敌方投射物还会为每一项重新做一次 lane 区间查询，而这段逻辑运行在 `Parallel.ForEach` 内。

**证据.**
- 范围过宽：`ViewModels/FumenVisualEditorViewModel.Drawing.cs:529-534`
  ```
  var curTGrid = GetCurrentTGrid();
  if (IsPreviewMode) {
      blts = EditorContext.Fumen.Bullets.BinaryFindRange(curTGrid, TGrid.MaxValue);   // :532
      bels = EditorContext.Fumen.Bells.BinaryFindRange(curTGrid, TGrid.MaxValue);     // :533
  }
  ```
- 逐项 soflanGroup 过滤：`:535-544`（两个 `Where` + 闭包）
- 逐项 lane 查询：`TargetImpl/OngekiObjects/BulletBell/ProjectileBatchDrawTargetBase.cs:310`
  ```
  var enemyLane = fumen.Lanes.GetVisibleStartObjects(objTGrid, objTGrid).OfType<EnemyLaneStart>().LastOrDefault();
  ```
  `GetVisibleStartObjects` → `ConnectableObjectList.cs:80-83` `startObjects.QueryInRange(min,max)` → `IntervalTreeWrapper.cs:72-79` → `IntervalTreeNode.Query(from,to)`（`IntervalTreeNode.cs:141-148`，每次调用租一个 `PooledList` 并递归）
- 并行执行：同文件 `:342-360`
  ```
  if (totalCount < parallelCountLimit) { foreach … } else { Parallel.ForEach(objs, parallelOptions, RentBuffer, …, MergeAndReturnBuffer); }
  ```

**性能影响.**
1. `BinaryFindRange(curTGrid, MaxValue)` 到谱面末尾，返回量随剩余谱面线性增长；这些对象随后才在 `_Draw` 里被可见性剔除。
2. 每个敌方投射物一次区间查询 + `OfType` + `LastOrDefault`，成本 O(log n + k) 加每次一个 `PooledList` 租还。
3. **并行体内调用共享集合**：`Parallel.ForEach` 下多线程同时调 `fumen.Lanes.GetVisibleStartObjects`，即对同一个 `IntervalTree` 并发 `Query`。`Query` 在 `isInSync == false` 时会调 `RebuildInternal()`（`IntervalTree.cs:68-74`）——即**并发触发整树重建**。这在渲染线程并行时是真实的数据竞争窗口。

**推荐改法.**
1. 范围收紧：按外观/视口上界（含 `appearOffsetTime` 与子弹最高速度）算出真实 `maxTGrid`，而非 `TGrid.MaxValue`。
2. 缓存 lane 查询：敌方投射物的 lane 只依赖 `objTGrid`，在同一帧内按 `TGrid` 建一个小的 (TGrid → enemyLane) 缓存即可，避免逐项重查（同一 TGrid 常有多发子弹）。
3. **把并发体内的共享查询移出并行区**（或确认 `Query` 在只读期不会触发 `RebuildInternal`）。这一条比性能更重要——它是正确性问题。

**风险.** 中–高。范围收紧需保证不漏画（与 `spd < 1`、soflan 变速的提前出现语义耦合）；并行区重排需保持输出顺序。

---

#### RND-010 — P3：绘制命令粒度为「每 lane 一条」

**简述.** `CommonLinesDrawTargetBase.FillLine` 对每条 lane 单独发一条 `DrawSimpleLines` 命令，replay 时每条命令各自建立/结束绘制路径。

**证据.** `TargetImpl/CommonLinesDrawTargetBase.cs:25-40`
```
public void FillLine(IFumenVisualEditorDrawingContext target, IDrawCommandListBuilder builder, T start) {
    using var list = ObjectPool.GetPooledList<LineVertex>();
    VisibleLineVerticesQuery.QueryVisibleLineVertices(…, list);
    builder.DrawSimpleLines(list, LineWidth);     // :31 每 lane 一条命令
}
public override void DrawBatch(…) { foreach (var laneStart in starts) FillLine(target, builder, laneStart); }   // :34-40
```
命令构造：`Kernel/Graphics/DrawCommands/DrawCommandListBuilder.cs:206-220`（每次 `CopyToPooledList` 再 `RentCommand<DrawSimpleLinesCommand>`）。

**性能影响.** 命令数 = 可见 lane 数（长谱面数百）。每条命令的固定成本包括：一次池化列表租用 + `AddRange` 拷贝 + 一条命令对象租用 + replay 时一次路径建立/结束。此外每个 lane 的 `LineWidth` 与颜色可能不同，聚合并非无条件可行。

**推荐改法.** 仅在后端确认支持「断开 strip」（同一命令内多个不连续的 polyline run）且基准证实收益后再聚合。可先按 (`color`, `LineWidth`) 分组，把同组多 lane 合成一条 `DrawLines`（若该命令已支持多 run，见 `SkiaDrawCommandListReplay` 中的 `NewSkiaLineDrawing.DrawPolylineOrSegmentsRun`）。

**风险.** 中。需先确认后端语义。

---

#### RND-015 — P2：玩家位置标记颜色为 `Vector4.Zero`

**简述.** `DrawPlayerLocationHelper` 的实例数组初始为 `default`，其 `color` 为 `Vector4.Zero`，而绘制路径只更新 `position` 与 `size`，从不写 `color`。

**证据.** `Graphics/Drawing/Editors/DrawPlayerLocationHelper.cs`
- `:15` `private (Vector2 size, Vector2 position, float rotation, Vector4 color)[] arr = { default };` —— `default` 元组的 `color` = `Vector4.Zero`
- `:63-64` 只写 `arr[0].position` 与 `arr[0].size`
- `:66` `builder.DrawTexture(texture, arr);`
- 消费侧 `Kernel/Graphics/Skia/Drawing/TextureDrawing/DefaultTextureDrawing.cs:41` `paint.Color = color.ToSKColor();`

**性能影响.** 需与性能分开看两层：
1. **正确性存疑**：`color = Vector4.Zero`（alpha=0）。04-09 版审计的「附带发现」（见 `performance-gc-audit-2026-09-09.md` 第 4 节 PERF-RND-005 条目末）实证当前 SkiaSharp 3.x 下 `SKPaint.Color` 不调制 `DrawImage` 输出，因此标记**未必**真的透明——即该项可能是「无害的冗余赋值」而非功能缺陷。但这需要按真实路径复核确认。
2. **性能**：无论哪种情形，`DrawTexture` 命令都会被生成并被 replay（`DefaultTextureDrawing.cs` 每帧一次 `OnBegin/OnEnd` + `SKPaint`）。

**推荐改法.** 先确认期望语义（标记应有颜色还是不透明？）：
- 若应带颜色 → 在 `:15` 初始化一个非零颜色（如 `Vector4.One`）。
- 若本就不需要染色 → 去掉 `color` 参与，避免误导。
同时若标记在特定状态下不可见，应在 `Draw` 早退，避免生成无效命令。

**风险.** 低。

---

## 5. EDT / DAT / AUD：与渲染相邻的高频路径

#### EDT-003 — P2：拖动时全量选择扫描

**简述.** 拖动交互每次移动都对全部可显示对象做 `OfType/Where/Distinct`，并每次 `SelectObjects.ToArray()`。

**证据.** `ViewModels/FumenVisualEditorViewModel.UserInteractionActions.cs:119-120`（`GetAllDisplayableObjects()` 后的筛选链）、`:1543-1549`（拖动路径重复边缘工作与 `ToArray()`）。相关：`:1491-1495`（每个边缘 pointer 事件创建随机 generation + `Task.Delay(1000/60)` + 递归 `OnMouseMove`）。

**性能影响.** 拖动是持续高频事件（可达每帧多次）。`Distinct` 分配 `HashSet`，`ToArray` 每次分配新数组，`GetAllDisplayableObjects` 本身会走 `EnumerateAllDisplayableObjects` 的区间查询族。

**推荐改法.** 在拖动**开始**时建立一次索引/快照（`Dictionary<Id, Obj>` 或按 TGrid 排序数组），移动时只按增量几何更新候选集。边缘自动滚动改为单一 CTS + 调度循环，只保留最新位置（`:1491-1495`、`:1537-1549`）。

**风险.** 中。选择语义（尤其是 `Distinct` 与 tie 顺序）必须保持。

---

#### DAT-005 — P2：`IntervalTree` 变更触发整树重建

**简述.** 任何区间集合的增删或坐标变更都会把整棵树标记为脏，下次查询时**递归重建**，每个节点分配 4 个 `List`。

**证据.**
- 脏标记：`Base/Collections/Base/RangeTree/IntervalTree.cs:94`（`Add` → `NotifyDirty()`）、`:100`（`Remove`）、`:110`（批量 `Remove`）；`NotifyDirty() => isInSync = false;`（`:130`）
- 查询时重建：`IntervalTree.cs:62-63`（`Query(value)`）、`:70-71`（`Query(from,to)`）、`:78-79`（`QueryInto`）、`:124-125`（`GetEnumerator`）、`:34`/`:24`（`Min`/`Max` 属性）
- 重建分配：`IntervalTreeNode.cs:54-101` `BuildTree` 每层 `new List<TKey> endPoints` + `new List inner/left/right`（`:59,74,75,76`），并递归建子节点（`:96-99`）；`Release`（`:44-51`）只递归清引用，不复用缓冲
- 批量入口：`IntervalTreeWrapper.cs:81-90`（`BeginBatchAction`/`EndBatchAction` → `NotifyDirty`），`IsBatching` 期间变更只标记不处理（`:44-45`）

**性能影响.** 编辑拖拽时对象坐标高频变化 → 每次变化 `Remove`+`Add`（`IntervalTreeWrapper.cs:40-51`）→ 每次查询触发整树重建。重建成本 O(n log n) 加 O(n) 分配（每节点 3–4 个 List）。大谱面（数万对象）下这是编辑期卡顿的候选来源。

**推荐改法.** 优先使用已有的 `BeginBatchAction`/`EndBatchAction` 把一次交互的多处变更合并为一次重建（调用方现状需确认）。进一步可改为增量维护（节点分裂/合并）并复用 scratch 缓冲，但工程量大。

**风险.** 中。批量语义已存在，先确认调用方是否已正确使用。

---

#### AUD-003 — P2：波形点每毫秒一个 `float[]`

**简述.** 生成峰值点时，每 1ms 采样创建一个 `PeakPoint` 与一个 `float[channels]` 数组。

**证据.** `Kernel/Audio/SamplePeak/DefaultSamplePeak.cs:28` `var amplitudes = new float[channels];`（在 `for (var i = 0; i < samplesCount; i += samplesPerPoint)` 循环体首行，`:26`）。

**性能影响.** 3 分钟音频 = 180,000 个点 → 180,000 次数组分配（每声道数 1–2 个 float）。这些点数量级大且存活期长（跟随波形查看器），全部落到 Gen0/Gen1，并抬高常驻内存。同时 `PeakPoint` 每个都持有自己的数组，无法紧凑存储。

**推荐改法.** 改为连续的两条 `float[]`（min/max，按声道交错）或用 `(float min, float max)` 值类型的结构数组，使一个点的数据内联；`PeakPointCollection` 的索引访问相应改造。若需支持多分辨率，再叠加分辨率层级。

**风险.** 中。涉及 `PeakPointCollection`/`PeakPoint` 的公开形状与全部消费点（`DefaultWaveformDrawing` 等）。

---

#### AUD-004 — P2：1ms `DispatcherTimer` 驱动音频事件 + 每 tick 分配

**简述.** 音频事件推进用 1ms 间隔的 `DispatcherTimer`（UI 线程），并在 duration-stop 路径每次 tick 做一次 LINQ 筛选 + `ToArray()`。

**证据.**
- `Kernel/Audio/DefaultCommonImpl/Sound/DefaultFumenSoundPlayer.cs:47-51`：`updateTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(1) };`
- 同文件 `:345-374`：duration 事件处理中 `foreach (var durationEvent in currentPlayingDurationEvents.Where(x => currentTime < x.Time || currentTime > x.EndTime).ToArray())`（`:360` 附近）

**性能影响.** 1ms 定时器意味着每秒约 1000 次 UI 线程回调（实际受 Dispatcher 精度与优先级限制，但仍远高于帧率）。每次回调都要跑事件推进逻辑；`.Where(...).ToArray()` 每 tick 分配一个数组。把 UI Dispatcher 当音频时钟，也会让音频事件的时序精度受 UI 线程负载影响。

**推荐改法.** 改为按「下一个事件的音频时间」调度（算出最近的 `nextEventTime`，用一次 `Task.Delay` 或高精度定时器到点唤醒），而非固定 1ms 轮询；`ToArray` 改为复用池化列表或在遍历时收集待移除项。同时确保 `cacheSounds` 等共享状态有明确的线程纪律。

**风险.** 中。音频时序对可感知延迟敏感，需实测。

---

#### AUD-001 — P1：拖动控件每个值都做「序列化 + fsync + 文件替换」

**简述.** 音量等设置属性 setter 内直接 `Save()`，而桌面实现的 `Save` 每次都复制整张设置字典、两次 JSON 序列化、`Flush(flushToDisk:true)`（fsync）与原子替换。

**证据.**
- setter 内保存：`Kernel/Audio/NAudioImpl/NAudioManager.cs:47-50`（`SoundVolume`）、`:56-59`（`MusicVolume`）—— `AudioSetting.Default.Save();`
- `Models/Settings/SettingModelBase.cs:17-20`：`Save()` → `IoC.Get<ISettingManager>().SaveSetting((TSelf)this, JsonTypeInfoCore)`
- 桌面实现：`src/OngekiFumenEditor.Avalonia.Desktop/Platforms/Services/Settings/DesktopSettingManager.cs`
  - `:50` `var nextSettingMap = new Dictionary<string, string>(settingMap) { [key] = JsonSerializer.Serialize(obj, typeInfo) };`（复制整表 + 序列化一次）
  - `:52-54` `var content = JsonSerializer.Serialize(nextSettingMap, …)`（再序列化一次）
  - `:55` `WriteSettingFileAtomically(content);`
  - `:145-165` `WriteSettingFileAtomically`：新建临时文件 → `stream.Flush(flushToDisk: true)`（fsync）→ `File.Replace`/`File.Move`
- 调用侧拖动语义：`Modules/AudioPlayerToolViewer/ViewModels/AudioPlayerToolViewerViewModel.cs:160-228` 的多个 setter 同样直连保存

**性能影响.** 每次音量/倍率/偏移 setter 调用 = 2 次 JSON 序列化（整表）+ 1 次字典复制 + 1 次 fsync + 1 次文件替换。拖动滑块时 setter 每帧触发多次，即每帧多次 fsync——会直接吃满磁盘 I/O 并阻塞 UI 线程。

**推荐改法.** 引入写延迟（debounce）：setter 只更新内存模型并标记脏，在 `DragCompleted`/`LostFocus`/应用退出时统一落盘；或用一个「最后一次写入后 N 毫秒」的节流。`NextSettingMap` 的整表复制可改为就地更新（当前用复制是为了失败时不污染内存态，若改为延迟写则需保留这一保证）。

**风险.** 中。延迟写会在崩溃时丢失最后一次未落盘的设置，需与产品语义确认；退出路径必须确保 drain。

---

## 6. 已确认无需处理（本轮核对，避免重复投入）

以下条目在既有审计中列出，本轮核对后确认**已修复或已不是问题**，不再计入工作量：

| 条目 | 核对结论 |
| --- | --- |
| PERF-RND-002 / RND-02 | Hold 顶点裁剪已改为索引窗口（`HoldDrawingTarget.cs` 已无 `RemoveAt(0)`）。 |
| PERF-RND-004 / RND-05 | `DrawCommandListContextSlots.Present` 已改为只呈现不清空；`Swap`/`Remove` 负责释放（`DrawCommandListContextSlots.cs:65-105`）。 |
| PERF-RND-005 / RND-06 | `DefaultTextureDrawing.cs:33-66` 已把 `OnBegin/OnEnd` 与 `SKPaint` 提到实例循环外，与 batch 路径同构。 |
| PERF-RND-006 / RND-07 | `LineVertex`/`VertexDash` 已是值类型（`VisibleLineVerticesQuery.cs:34` 用 `new LineVertex(...)` 构造结构体，无堆分配）。 |
| PERF-RND-011 / RND-12 | 线绘制已切池化实现。 |
| PERF-RND-012 / RND-13 | replay 已接入真实 monitor（`DefaultTextureDrawing.cs:58` `target.PerfomenceMonitor.CountDrawCall()`）。 |
| PERF-RND-014 / RND-17 | 字体已缓存：`DefaultSkiaStringDrawing.cs:26` 进程级 `typefaceCache`、`:30-32` 实例级复用 `textPaint`/`decorationPaint`/`reusableFont`。 |
| PERF-RND-016 / RND-19 | 计时单位已修。 |
| PERF-RND-017 / RND-20 | replay 已按 render context 缓存。 |
| PERF-DAT-002 / DAT-02 | `SortableCollection` 已有独立 `LowerBoundIndex`/`UpperBoundIndex`（`MeterChangeList` 三查询已改二分）。 |
| PERF-DAT-004 / DAT-05、PERF-RND-009 / RND-10 | 墙轨边界已帧内缓存 + 无分配子节点区间查询。 |
| PERF-DAT-009 / DAT-12、PERF-DAT-010 / DAT-13 | 已修（`BulletPalleteList` 有序 backing、`MeterChangeList` 二分前驱后继）。 |
| PERF-PRS-001 / PRS-01 | 已修（`HoldTickStepCalculator` 唯一实现 + `progJudgeBpm` 入口校验）。 |
| PERF-FWK-004 | 已修（`CommandManager` copy-on-write 订阅表 + 异常隔离）。 |
| 命中表（hit rect） | 已改为帧末冻结快照：`FumenVisualEditorViewModel.UserInteractionActions.cs:1823-1854`（`RegisterSelectableObject` 写构建缓冲、`ClearHitObjects` 帧首清、`CommitHitObjects` 帧末排序发布）。**该设计本身健康**，不列为问题。 |
| `GlobalCacheSoflanGroupRecorder` | `Graphics/GlobalCacheSoflanGroupRecorder.cs:41-51` 用 `FrozenDictionary` 读路径，`GetCache` 为 O(1) 且无分配。**健康**。 |
| `MeterChangeList.GetCachedAllTimeSignatureUniformPositionList` | `MeterChangeList.cs:205-216` 用 `cachedBpmContentHash` 做 O(1) 失效判断。**健康**（对比 `DAT-C1`）。 |
| `DrawCommandListBuilder` 的命令对象 | 已池化：`DrawCommandListBuilder.cs:467-471` `RentCommand<TCommand>()`，`DrawSimpleLines`（`:218`）等均走池。 |
| `PooledList` | 基于 `Collections.Pooled.PooledList<T>`，`Dispose` 归还数组，且提供 `EnsureCapacity`（`Utils/ObjectPool/PooledList.cs:58-70`）。**健康**。 |

---

## 7. 建议实施顺序

1. ~~**`RND-C1`（可见性判定去并发字典枚举）**~~ —— **已完成（2026-09-22）**，含新老实现基准（`VisibleContextCheckBenchmarks`）。见 §4.1。
2. **`RND-003`（纹理生命周期）** —— 优先做**所有权梳理**（单例 vs 每编辑器），再决定「幂等重载」还是「改注册范围」。这是唯一的确定性原生内存增长项，**当前最优先**。
3. ~~**`RND-C2`（限帧闸门提前）**~~ —— **已完成（2026-09-22）**，含新老实现基准（`EditorRenderFpsGateBenchmarks`）。见 §4.1。
4. **`AUD-001`（设置落盘 debounce）** —— 拖动卡顿的直接原因，改动局部。
5. **`DAT-C1`（BPM 版本号）** + **`DAT-005`（区间树批量）** —— 编辑期卡顿的候选来源，需先确认调用方批量语义。
6. **`RND-008` 的并行区共享查询** —— 其中「并发触发 `IntervalTree.RebuildInternal`」是**正确性风险**，建议不等调优、单独先确认。
7. **`RND-C3`（`drawMap` 重复 Clear）** —— 已确认第 1 步（删冗余 Clear）纯属清理，可与其它项搭车；第 2 步需先测量。
8. 其余 P2/P3 按测量结果排。

**不要**在没有 profile 的情况下同时改动多个 P2/P3 项。已落地的 benchmark 可作体例参考：`VisibleContextCheckBenchmarks`（含新旧实现对拍断言）、`EditorRenderFpsGateBenchmarks`（真实生产路径 + `Isolate_*` 成本拆分），另可参考既有 `SkiaTextureDrawingBenchmarks`、`MeterChangeListQueryBenchmarks` 等。

## 8. 未决问题

1. `RND-003` 的所有权问题（单例是否可能被多编辑器共享）必须先回答，否则修复会引入新缺陷。本报告不预设答案。
2. `RND-015` 的语义需确认：当前 SkiaSharp 3.x 下 `SKPaint.Color` 不调制 `DrawImage`，因此 `color = Vector4.Zero` 是否真的导致标记不可见，需按真实 `DrawPlayerLocationHelper` 路径复核（既有审计已有此疑问，本轮未在真实负载下验证）。
3. `RND-008` 中 `Parallel.ForEach` 与 `IntervalTree` 的并发交互需实测确认是否真的会并发进入 `RebuildInternal`（取决于并行体内是否只读、以及是否有其它线程在同一帧内触发脏标记）。
4. 除 `RND-C1`、`RND-C2`（均有实测数据，分别为逐调用 / 逐被丢弃帧量纲）与本报告 §6 中引用既有实测的条目外，其余「性能影响」均为静态推理的量纲判断（每次调用/每帧/每对象），**没有**实测的帧时间、分配速率或 GC 数据。另外**尚无任何整帧级**（端到端）实测——`RND-C1`/`RND-C2` 的数值都不能直接当作整帧加速比。
5. 本轮未覆盖 Browser/WASM 侧的渲染差异（`src/OngekiFumenEditor.Avalonia.Browser`）与第三方依赖内部（Gekimini/Dock/ToolBar/WindowManager），这些在 09-09 审计中有独立分区，状态未在本轮复核。
