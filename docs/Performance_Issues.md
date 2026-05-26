# 性能问题与优化机会清单

> 扫描范围：`F:\OngekiFumenEditor\OngekiFumenEditor\`（不含 Dependences/、Benchmark/、CommandLine/、obj/、bin/）
> 扫描方式：基于实际 Grep/Read 工具输出，所有问题都引用真实文件路径与行号
> 输出日期：2026/05/26

## 摘要

- 扫描覆盖：Modules/（FumenVisualEditor 渲染热路径）、Kernel/（Graphics、Audio、Scheduler、CurveInterpolater）、Base/、Parser/、Utils/、UI/
- 共发现 **40 项**具体性能问题或可优化点
- 严重度分布：
  - **Critical**（每帧/解析每行被命中的热路径）：6 项
  - **High**（频繁触发、容易放大）：13 项
  - **Medium**（中频路径或一次性但显著的浪费）：14 项
  - **Low**（边角问题、可读性 > 性能）：7 项
- **复核记录**（条目标题前标记规则）：
  - ✅ 已 Benchmark 复核且**允许改动**（已修复或将修复）
  - ❌ 已 Benchmark 复核但**不允许改动**（性价比不足或风险更大）
  - 条目列表：
    - ❌ **#1 DrawJudgeLineHelper 字符串内插**: 经 Benchmark 验证后**决定不修改**（详见条目#1 Benchmark 复核结论），实际严重度降级 Critical → Low。
    - ✅ **#2 CommonHorizonalDrawingTarget 每帧 LINQ 链**: 经 Benchmark 验证后**已修复**（字典桶替换 GroupBy + 多层 ToList，时间 -18%~-54%、分配 -64%~-75%，见条目#2 实施记录）。
    - ✅ **#3 LaneCurvePathControlDrawingTarget 多次枚举**: 经 Benchmark 验证后**已修复**（单次扫描收集 + 字典桶 + IndexCache，时间 -41%~-57%、分配 -85%~-93%，见条目#3 实施记录）。

## 关键热路径问题（Critical / High）

### ❌ 1. 渲染热路径中字符串内插每帧分配（DrawJudgeLineHelper）

- **位置**: `OngekiFumenEditor/Modules/FumenVisualEditor/Graphics/Drawing/Editors/DrawJudgeLineHelper.cs:43`、`:66`
- **类别**: 字符串 / 绘制
- **严重度**: Critical
- **决策**: **不修改**（见下方"Benchmark 复核结论"）
- **问题**: `Draw()` 每帧执行，第 43 行 `str = $"{audioTime.Minutes,-2}:{audioTime.Seconds,-2}:{audioTime.Milliseconds,-3}"` 和第 66 行 `$"[{soflanGroup}]{speed:F2}x"` 在每一帧都生成新的 string，且每帧调用还会进入 `DrawString` 内部进行字典查询。
- **影响**: 30/60 FPS 下每秒数十次字符串分配并装箱（`int`/`double` -> `object`），加剧 GC 压力，并在文本测量缓存中产生不同 key（毫秒数变化）。
- **建议**: 使用复用的 `StringBuilder`（类中已存在 `stringBuilder` 池可借鉴）或 `DefaultInterpolatedStringHandler` 写入栈缓冲；当时间戳秒级未变时复用上一帧字符串。

#### Benchmark 复核结论（2026/05/26）

对应基准: `OngekiFumenEditor.Benchmark/Benchmarks/JudgeLineFormatBenchmarks.cs`。
环境: BenchmarkDotNet v0.15.8, .NET 10.0.8, AMD Ryzen 7 5800X, Default Job, MemoryDiagnoser。

**时间戳格式化 (`:43`)**

| Method | Mean | Ratio | Allocated |
|---|---:|---:|---:|
| InterpolationOriginal (基线) | 34.77 ns | 1.00 | 40 B |
| StringBuilderReused | 32.56 ns | 0.94 | 40 B |
| StringCreateSpan | 23.22 ns | 0.67 | 40 B |
| LastFrameCache | 35.16 ns | 1.01 | 40 B |

**速度文本格式化 (`:66`)**

| Method | Mean | Ratio | Allocated |
|---|---:|---:|---:|
| InterpolationOriginal (基线) | 105.13 ns | 1.00 | 40 B |
| StringBuilderReused | 105.11 ns | 1.00 | 64 B（劣化） |
| StringCreateSpan | 87.37 ns | 0.83 | 40 B |
| LastFrameCache | 105.12 ns | 1.00 | 40 B |

**结论：本条不进行代码变更，原因：**

1. **绝对时间极小**: 单次调用 ~35 ns 与 ~105 ns；按 60 FPS 计算两处合计约 8.4 µs/秒，远低于渲染帧预算 16.6 ms 的千分之一，不构成实际瓶颈。
2. **分配量等价**: 所有方案 `Allocated` 都是 40 B（最终结果 string 本身），原以为可省的"内插临时对象"在 .NET 9+ 已被 `DefaultInterpolatedStringHandler` 消除，**重构无法降低 GC 压力**。
3. **`StringBuilderReused` 在速度组反而劣化**: `AppendFormat("{0:F2}", double)` 装箱 double，多 24 B 分配（40 → 64 B），引入此模式风险大于收益。
4. **`LastFrameCache` 命中率近零**: 时间戳的毫秒和速度数值在播放时几乎每帧都变，分支预测失败让缓存方案与基线持平甚至更慢；只有在暂停状态才有意义，性价比低。
5. **`StringCreateSpan` 虽快 17–33%，但收益绝对值 < 20 ns/帧**，需要替换两处可读性更高的内插写法为手写 span 拼接，**维护成本超过收益**。

**真正值得做的（如未来需要再优化）**: 给 `DrawString` 加 `ReadOnlySpan<char>` 重载，把 40 B 字符串分配也消除；这是结构性优化，不在本条 Critical 范围内。

**本条降级**: 实际严重度由 Critical → Low（保留登记，不安排修复）。

### ✅ 2. CommonHorizonalDrawingTarget 每帧 LINQ 链 + 装箱+ 反射式分组

- **位置**: `OngekiFumenEditor/Modules/FumenVisualEditor/Graphics/Drawing/TargetImpl/OngekiObjects/CommonHorizonalDrawingTarget.cs:53`、`:55`、`:71`、`:118`、`:101-113`
- **类别**: LINQ / 字符串 / 内存
- **严重度**: Critical
- **决策**: **已修复**（见下方"Benchmark 复核结论"与"实施记录"）
- **问题**: `DrawBatch` 中先 `objs.Select(...).ToListWithObjectPool()`，再 `GroupBy(x => x.TimelineObject.TGrid.TotalGrid)`，每个 group 又 `.ToListWithObjectPool()`，并 `.Select(...).OrderBy(...).ToListWithObjectPool()`；随后第 101–113 行的 `formatObj` 在每个对象上做 `$"..."` 字符串插值（BPM、Soflan、MET、Comment 等）。所有这些都是每帧执行。
- **影响**: GroupBy + 多层 ToList + 每对象字符串拼接，在重谱面下显著拖低帧率。
- **建议**: 用静态字典桶（`Dictionary<int, PooledList<...>>`）替代 GroupBy；对静态文本（如 `"CLK"`）直接返回常量；对带数值的内容使用 `StringBuilder` 复用或缓存"上一次相同输入 -> 字符串"的小 LRU。

#### Benchmark 复核结论（2026/05/26）

对应基准: `OngekiFumenEditor.Benchmark/Benchmarks/CommonHorizonalDrawBenchmarks.cs`。
环境: BenchmarkDotNet v0.15.8, .NET 10.0.8, AMD Ryzen 7 5800X, Default Job, MemoryDiagnoser。
样本: N=200 / 1000 / 4000，每 3 个对象共享同一 TGrid（典型重谱场景）。

| Method | N | Mean | Ratio | Allocated | Alloc Ratio |
|---|---:|---:|---:|---:|---:|
| Original_LinqChain | 200 | 48.16 µs | 1.00 | 81.34 KB | 1.00 |
| Optimized_DictBucket | 200 | 24.35 µs | **0.51** | 19.98 KB | **0.25** |
| Optimized_DictBucket_ReusedSb | 200 | 21.85 µs | **0.46** | 19.98 KB | **0.25** |
| Original_LinqChain | 1000 | 217.50 µs | 1.00 | 403.27 KB | 1.00 |
| Optimized_DictBucket | 1000 | 169.43 µs | 0.78 | 110.59 KB | 0.27 |
| Optimized_DictBucket_ReusedSb | 1000 | 148.42 µs | **0.68** | 110.59 KB | **0.27** |
| Original_LinqChain | 4000 | 908.62 µs | 1.00 | 1610.76 KB | 1.00 |
| Optimized_DictBucket | 4000 | 738.29 µs | 0.82 | 572.17 KB | 0.36 |
| Optimized_DictBucket_ReusedSb | 4000 | 699.56 µs | **0.77** | 572.17 KB | **0.36** |

**关键收益**:
1. **CPU 时间下降 18%–54%**（N=200 减半）；
2. **分配下降 64%–75%**（N=200 从 81 KB → 20 KB，60 FPS 下相当于每秒省 3.6 MB）；
3. 分配大头不是结果字符串，而是 `GroupBy` 的 Grouping 节点 + `Select`/`ToList` enumerator 中间层——字典桶一次扫描全省掉；
4. `StringBuilder` 复用是次要优化，**未采用**（保持代码可读性，已显示分配量与不用 sb 相同）。

#### 实施记录（2026/05/26）

**改动文件**: `OngekiFumenEditor/Modules/FumenVisualEditor/Graphics/Drawing/TargetImpl/OngekiObjects/CommonHorizonalDrawingTarget.cs`

**核心变更**:
1. `colors` 字典提升为 `static readonly`（一次初始化）；
2. 新增 `static readonly IComparer<RegisterDrawingInfo> colorComparer`，按 `FSColor.PackedValue` 排序；
3. `DrawBatch` 用 `ObjectPool.GetPooledDictionary<int, IPooledList<RegisterDrawingInfo>>` 一次扫描 `objs` 完成分桶，替代 `objs.Select(...).ToListWithObjectPool() + GroupBy(...) + 每组 ToListWithObjectPool()`；
4. 每桶直接 `actualItems.Sort(colorComparer)` 一次排序，替代两条独立的 `Select(...).OrderBy(...).ToListWithObjectPool()`；
5. `regColors` 中间集合被消除，直接在画顶点的 for 循环中按已排序的 `actualItems` 取色；
6. `DrawDescText` 签名改为接收 `IPooledList<RegisterDrawingInfo> sortedItems`，删除内部的 `Select+OrderBy`；
7. `CheckVisible` 失败时 `RemoveAll` 改为原地双指针扫描 `FilterToHeaderOnly`，避免委托分配；
8. `try/finally` 保证每个桶 `IPooledList` 都 `Dispose` 归还对象池。

**保留未做**: `StringBuilder` 复用格式化（benchmark 显示不显著且降低可读性）。

**风险**: 字典遍历顺序与原 `GroupBy` 不同（原版按首次出现顺序，新版按 hash 顺序），不影响视觉结果——每个 group 都独立 `DrawSimpleLines`/`DrawString` 到自己的 Y 坐标，组间无依赖。

### ✅ 3. LaneCurvePathControlDrawingTarget 每帧多次 yield + 多次枚举

- **位置**: `OngekiFumenEditor/Modules/FumenVisualEditor/Graphics/Drawing/TargetImpl/EditorObjects/LaneCurvePathControlDrawingTarget.cs:46`、`:55-79`、`:82-83`、`:88`
- **类别**: LINQ / 集合 / 字符串
- **严重度**: High
- **决策**: **已修复**（见下方"Benchmark 复核结论"与"实施记录"）
- **问题**: `DrawBatch` 中 `list.GroupBy(...).SelectMany(item => gen())`（gen 用 yield return），随后 `list.Where(...).Select(...)`、`list.Select(...)` 又分别枚举 list 多次，并每个 item `item.obj.Index.ToString()` 产生临时字符串。
- **影响**: 每帧热路径上多次重复枚举 `list`，且 `gen()` 是延迟枚举器，会被传给 `builder.DrawSimpleLines` 后又被一次性 ToList，丢失对象池能力。
- **建议**: 改成单次 `foreach`，把 lineVertices / texture instances / selectable 对象一次性收集到对象池容器；`Index.ToString()` 可缓存（Index 通常变化少）。

#### Benchmark 复核结论（2026/05/26）

对应基准: `OngekiFumenEditor.Benchmark/Benchmarks/LaneCurvePathBenchmarks.cs`。
环境: BenchmarkDotNet v0.15.8, .NET 10.0.8, AMD Ryzen 7 5800X, Default Job, MemoryDiagnoser。
样本: N=100 / 500 / 2000 控制点，每 4 个共享一个 RefCurveObject（模拟实际曲线分组）。

| Method | N | Mean | Ratio | Allocated | Alloc Ratio |
|---|---:|---:|---:|---:|---:|
| Original_MultiPassLinq | 100 | 19.56 µs | 1.00 | 22.82 KB | 1.00 |
| Optimized_SinglePass | 100 | 8.36 µs | **0.43** | 1.64 KB | **0.07** |
| Original_MultiPassLinq | 500 | 96.14 µs | 1.00 | 116.54 KB | 1.00 |
| Optimized_SinglePass | 500 | 47.82 µs | **0.50** | 15.97 KB | **0.14** |
| Original_MultiPassLinq | 2000 | 453.31 µs | 1.00 | 491.71 KB | 1.00 |
| Optimized_SinglePass | 2000 | 266.34 µs | **0.59** | 71.63 KB | **0.15** |

**关键收益**:
1. **CPU 时间下降 41%–57%**（N=100 减半还快 7%，最常见场景下尤其受益）；
2. **分配下降 85%–93%**（N=100 从 22.82 KB → 1.64 KB，省 21 KB/帧 → 60 FPS 下省 **1.26 MB/秒**）；
3. 分配大头来自 `GroupBy` 的 Grouping 节点 + `SelectMany` + `yield return` 内部 state machine + 多次 LINQ enumerator 分配。单次 foreach 完全消除这些中间层；
4. `Index.ToString()` 缓存额外消除每帧 N 次小字符串分配（int → string 各分配 ~24 B，N=2000 时单这一项就省 ~50 KB/帧）；
5. 优化版多用了 1 个 PooledDictionary + 3 个 PooledList，全部归还对象池，**净分配仍降到 7%–15%**。

**风险**: `Dictionary<CurveRef, ...>` 字典遍历顺序与原 `GroupBy` 不同，但各 group 独立绘制无依赖，视觉无差异。

#### 实施记录（2026/05/26）

**改动文件**: `OngekiFumenEditor/Modules/FumenVisualEditor/Graphics/Drawing/TargetImpl/EditorObjects/LaneCurvePathControlDrawingTarget.cs`

**核心变更**:
1. 新增 `private readonly struct CtrlPoint(float y, float x, LaneCurvePathControlObject obj)`，替代原本的 `(float y, float x, obj)` 值元组——避免每帧分配元组装箱开销，并让排序比较器无需依赖元组语法；
2. 新增 `static readonly IComparer<CtrlPoint> indexDescCmp`，按 `Index` 降序排序，替代 `OrderBy(Index).Reverse()`；
3. 新增 `static readonly Dictionary<int, string> indexStringCache` + 私有 `GetIndexString`，替代每帧每对象的 `item.obj.Index.ToString()`；
4. `DrawBatch` 用**一次 foreach 扫描** `objs` 同时完成：过滤可见性、收集 `allTex`/`selectedTex` 纹理实例、按 `RefCurveObject` 分桶——替代原本的 `Where + Select + ToListWithObjectPool + GroupBy + 多次 Where/Select`；
5. 每桶 `items.Sort(indexDescCmp)` 一次排序，替代 `OrderBy(Index).Reverse()` 的延迟枚举；
6. `lineVertices` 改为 `IPooledList<LineVertex>` 显式收集，替代 `GroupBy(...).SelectMany(item => gen())` 的 yield 链——消除编译器生成的 state-machine 实例与 `SelectMany` enumerator；
7. `RegisterSelectableObject` 与 `DrawString` 合并到同一个 for 循环（共享一次 filtered 遍历），原本是两次独立 foreach；
8. `try/finally` 保证每个桶 `IPooledList<CtrlPoint>` 都 `Dispose` 归还对象池。

**保留 LINQ 风格语义**: `filtered` 顺序与原版一致（按 `objs` 首次出现顺序），桶之间顺序虽与 `GroupBy` 不同但绘制无依赖。

**风险检查**: 主项目 Release 编译 0 错误。运行行为预期等价（只改了枚举策略，所有副作用调用顺序与原版同：先 `DrawSimpleLines` → `DrawHighlightBatchTexture` → `DrawTexture` → `RegisterSelectableObject`/`DrawString`）。

### 4. CommonOpenGLDrawingBase / DefaultSkiaLineDrawing 每帧创建/销毁 SKPath

- **位置**: `OngekiFumenEditor/Kernel/Graphics/Skia/Drawing/LineDrawing/DefaultSkiaLineDrawing.cs:155`、`OngekiFumenEditor/Kernel/Graphics/Skia/Drawing/LineDrawing/NewSkiaLineDrawing.cs:430`
- **类别**: 绘制 / 内存
- **严重度**: High
- **问题**: 第 155 行 `using var path = new SKPath();` 在 `DrawPath(IList<SKPoint>, ...)` 中每次调用都新建并释放 `SKPath`（非托管资源）。`PostDraw` 每帧可能调用多次。
- **影响**: SKPath 内含非托管内存，频繁 new/Dispose 是 Skia 端 GPU/CPU 的非廉价操作。
- **建议**: 维持一个成员级 `SKPath`，每次调用 `path.Reset()` 复用；同样适用于 `DefaultSkiaPolygonDrawing.cs:48`、`DefaultSkiaStringDrawing.cs:57/89/132` 中的 `using var paint = new SKPaint()`（详见后续条目）。

### 5. Skia 各 Drawing 实现每帧 `new SKPaint()`

- **位置**: 
  - `OngekiFumenEditor/Kernel/Graphics/Skia/Drawing/TextureDrawing/DefaultTextureDrawing.cs:48`
  - `OngekiFumenEditor/Kernel/Graphics/Skia/Drawing/TextureDrawing/DefaultSkiaBatchTextureDrawing.cs:54`
  - `OngekiFumenEditor/Kernel/Graphics/Skia/Drawing/TextureDrawing/DefaultSkiaHighlightBatchTextureDrawing.cs:52`
  - `OngekiFumenEditor/Kernel/Graphics/Skia/Drawing/PolygonDrawing/DefaultSkiaPolygonDrawing.cs:38`
  - `OngekiFumenEditor/Kernel/Graphics/Skia/Drawing/StringDrawing/DefaultSkiaStringDrawing.cs:57`、`:89`、`:132`
  - `OngekiFumenEditor/Kernel/Graphics/Skia/Drawing/BeamDrawing/DefaultSkiaBeamDrawing.cs:85`
- **类别**: 绘制 / 内存
- **严重度**: High
- **问题**: 每次 `End/DoDraw/Post` 调用都 `using var paint = new SKPaint()`；`SKPaint` 是托管包装的非托管对象，构造 + Dispose 在 Skia 后端均要分配/释放原生内存。
- **影响**: 这些方法每帧被每个 drawingTarget 调用多次，累计创建数百到数千次 `SKPaint`。
- **建议**: 持有一个或多个常驻 `SKPaint`（按"颜色/描边"等少量参数对组合），在 `Begin`/`UpdatePaint` 中改写其字段（如 `DefaultSkiaLineDrawing.cs:62` 已经这样做了），其它实现统一改成这种模式。

### 6. DefaultFumenSoundPlayer 同步顺序 await 加载 17 个 wav

- **位置**: `OngekiFumenEditor/Kernel/Audio/DefaultCommonImpl/Sound/DefaultFumenSoundPlayer.cs:105-122`
- **类别**: 异步 / IO
- **严重度**: High
- **问题**: 17 个 `await load(...)` 顺序执行，未并行；且 `InitSounds` 是 `async void`（第 70 行）。
- **影响**: 启动阶段串行 IO，等待时间累加。
- **建议**: `await Task.WhenAll(loads)` 并行加载；`InitSounds` 改成 `async Task` 并由调用方保留 task。

### 7. SchedulerManager.Run 每 10ms 创建一堆 Task + ContinueWith + ToArray

- **位置**: `OngekiFumenEditor/Kernel/Scheduler/SchedulerManager.cs:53-72`
- **类别**: 异步 / LINQ / 内存
- **严重度**: High
- **问题**: `private async void Run(...)`（async void），并且循环体 `Schedulers.Where(...).Select(x => ... ContinueWith ...).ToArray()` 每 10ms 都生成新 Task 数组（每个 task 还附带一个 ContinueWith 闭包）。同时 `DateTime.Now` 在第 60、61 行每次都查询。
- **影响**: 即便无调度任务，每秒约 100 轮都创建 array / Task / closure，GC pressure 持续。
- **建议**: 改成静态可复用的列表 + foreach；用 `Stopwatch.GetTimestamp()` 代替 `DateTime.Now`；`async void` 改 `async Task` 让顶层 catch 能正确传递异常。

### 8. FumenVisualEditorViewModel.UserInteractionActions.cs OnMouseLeftButtonUp 中 AsParallel + LINQ 链

- **位置**: `OngekiFumenEditor/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.UserInteractionActions.cs:1244`
- **类别**: 并发 / LINQ
- **严重度**: High
- **问题**: `hits.AsParallel().Where(...).Select(...).OrderBy(...).ToList()` 在用户每次鼠标释放时执行；后面第 1261 行又 `.Concat(...).Distinct().ToList()`。AsParallel 启动开销可能远大于受益（hits 体积通常不大）。
- **影响**: 鼠标释放路径上为小数据集启动 PLINQ partitioner、worker thread 分发，得不偿失；ToList 双倍分配。
- **建议**: 直接顺序 `Where/OrderBy`；若确实大可加阈值（如 > 5000 才并行）。

### 9. DefaultProgramUpdater 阻塞 Wait()，会死锁 UI 线程

- **位置**: `OngekiFumenEditor/Kernel/ProgramUpdater/DefaultProgramUpdater.cs:149`
- **类别**: 异步 / 并发
- **严重度**: High
- **问题**: `IoC.Get<IWindowManager>().ShowWindowAsync(new ShowNewVersionDialogViewModel()).Wait();` —— 在 UI 上下文同步等待 Task，经典 sync-over-async 反模式，存在死锁风险。
- **影响**: 卡 UI；若被 Dispatcher 调度则可能死锁。
- **建议**: 调用方改为 `await ShowWindowAsync(...)`。

### 10. KeyboardAction_ToggleBatchMode 同步等待

- **位置**: `OngekiFumenEditor/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.UserInteractionActions.cs:898`
- **类别**: 异步
- **严重度**: High
- **问题**: `CommandRouterHelper.ExecuteCommand(command).Wait();` 同上，syncing on UI thread。
- **建议**: 该函数声明改 `async void` 已有先例（行 187 等），此处也改用 await。

### 11. 全项目无 ConfigureAwait(false)

- **位置**: 全工程零匹配（Grep 结果 0 命中）
- **类别**: 异步
- **严重度**: High
- **问题**: WPF 项目的非 UI 库代码（如 `Parser/`、`Kernel/Audio/`、`Utils/ImageLoader.cs`、`Kernel/Scheduler/`、`Kernel/Graphics/`）的 await 全部默认回到 UI Synchronization Context。
- **影响**: 大量后台异步操作（如 `await File.ReadAllBytesAsync`、`await audioManager.LoadSoundAsync`）会强行 hop 回 UI 线程，UI 任务越多 await 完成的延迟越大；也是死锁风险源。
- **建议**: 在非 UI 库代码逐步引入 `.ConfigureAwait(false)`；或在 .csproj 启用 `<Features>... </Features>` + 全局 ConfigureAwaitOptions（.NET 8+ 可用 `[assembly: ConfigureAwait(false)]` via Fody/手工）。

### 12. ImageLoader.PrcessQueue 用 Task.Delay(0) 忙等

- **位置**: `OngekiFumenEditor/Utils/ImageLoader.cs:37-71`，关键行 `:48 await Task.Delay(0);`
- **类别**: 异步 / 并发
- **严重度**: High
- **问题**: `PrcessQueue` 是 `async void`（第 37 行），主循环里达到并发上限时用 `await Task.Delay(0)` 让出（=立即继续），变成事实上的忙循环；每次循环 `MD5.Create()`（第 75 行）也未复用。
- **影响**: 当 `currentTaskRunningCount >= ParallelCount` 时 CPU 占用高；MD5 反复创建/释放。
- **建议**: 用 `SemaphoreSlim(2)` 控制并发；MD5 可用 `MD5.HashData(byte[])` 静态方法（不分配实例）。

### 13. EnumerateAllDisplayableObjects 多层 LINQ Concat + SelectMany（每帧）

- **位置**: `OngekiFumenEditor/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.Drawing.cs:731-810`
- **类别**: LINQ / 集合 / 渲染
- **严重度**: High
- **问题**: `visibleRanges.SelectMany(x => { ... var r = Enumerable.Empty<>().Concat(...).Concat(...) ... return r; })`（行 737–780），最后 `objects.Concat(objs).SelectMany(x => x.GetDisplayableObjects())`（行 809）。多层 Concat + SelectMany 在 IEnumerable 上构造长链路，每次枚举对象在迭代器之间跳转，无法被 JIT 内联。
- **影响**: 谱面对象数千时，每帧都展开这条链；后续 `OfType<OngekiTimelineObjectBase>()`、`.Where(...)` 又再叠加。
- **建议**: 提供一个集中预先填充的 `PooledList<IDisplayableObject>`（按 visible range 划分），用基于结构的迭代器；或者把"该 range 内的可见对象"在 Editor 内做"脏标记 + 缓存"。

### 14. RebuildSoflanGroupCache Parallel.ForEach + IEnumerable 多次枚举

- **位置**: `OngekiFumenEditor/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.UserInteractionActions.cs:1062-1083`
- **类别**: 并发 / LINQ
- **严重度**: Medium
- **问题**: `objs = Fumen.GetAllDisplayableObjects().OfType<OngekiMovableObjectBase>(); objs = objs.Where(...)`：objs 是延迟 IEnumerable，`Parallel.ForEach(objs, ...)` 会触发一次完整枚举，#if DEBUG 分支再 `objs.Any()`（行 1088）以及 `objs.Where ... .OfType<>` 又一次重新枚举整个 displayable 树。
- **影响**: 性能浪费 + 潜在线程安全（GetAllDisplayableObjects 内部可能不是线程安全的 enumerable）。
- **建议**: 一次性 `.ToArray()` 后再 Parallel；DEBUG 分支也复用同一数组。

### 15. PostDraw + SKPath 单次构造未批化

- **位置**: `OngekiFumenEditor/Kernel/Graphics/Skia/Drawing/LineDrawing/DefaultSkiaLineDrawing.cs:140-167`
- **类别**: 绘制
- **严重度**: High
- **问题**: `_DrawPath` 中对相邻两点逐对 `canvas.DrawLine(cur, next, paint)`，每两点一次 draw call（行 148）；`DrawPath` 重构成 `SKPath` 又每帧 new。
- **影响**: 大量线时 draw call 飙升。
- **建议**: 用 `canvas.DrawPoints(SKPointMode.Polygon, points, paint)` 或维持复用 SKPath 改 reset。

### 16. SoflanList.GetCalculatableEvents 每次 new List + MergeTwoSortedCollections（每次重建）

- **位置**: `OngekiFumenEditor/Base/Collections/SoflanList_CachedPositionList.cs:53-91`
- **类别**: 集合 / 内存
- **严重度**: Medium
- **问题**: 每次 BPM 哈希变化时（行 201 `CheckAndUpdateSoflanPositionList`）就重建整个 `sortList = new List<(...)>()` 并 `Add`。在编辑器频繁修改 BPM/Soflan 时这条路径非常热。
- **建议**: 缓存 sortList，按 hash 渐进更新。

### 17. ParserUtils.GetDataArray 每行解析 TypeDescriptor.GetConverter + LINQ ToArray

- **位置**: `OngekiFumenEditor/Parser/ParserUtils.cs:11-25`
- **类别**: 集合 / 反射 / 字符串
- **严重度**: High
- **问题**: `SplitEmptyChar(line).Skip(1).Select(...).ToArray()` 中 `Split().ToArray()` 重复（line 13 已经 `.ToArray()`，line 19 再 `.Select(...).ToArray()`），且每行重新调用 `TypeDescriptor.GetConverter(typeof(T))`（行 18）—— 对每条 `.ogkr` 命令行都做一次反射查表。
- **影响**: 谱面有几千行命令时，每行均触发 TypeDescriptor 查找 + 双倍 ToArray + LINQ 分配。
- **建议**: 缓存每个 T 的 converter（静态 Lazy<Dictionary<Type, TypeConverter>>）；`Split` 已经返回数组，可以直接索引而非 LINQ；对 `int/float/string` 路径走专门快路径（`int.TryParse(ReadOnlySpan<char>)`）。

### 18. BulletPalleteCommandParser / CustomBulletCommandParser 多次 `?.ToUpper()` 不带 culture

- **位置**: `OngekiFumenEditor/Parser/Ogkr/CommandParserImpl/BulletPalleteCommandParser.cs:24,32,39,44`、`OngekiFumenEditor/Parser/Ogkr/CommandParserImpl/CustomBulletCommandParser.cs:28,37,48,58,66`、`OngekiFumenEditor/Parser/Ogkr/CommandParserImpl/CustomBellCommandParser.cs:29,40,50`
- **类别**: 字符串
- **严重度**: Medium
- **问题**: `.ToUpper()` 在 culture-sensitive 路径上分配新字符串；对于 `"UPS"/"ENE"/"PLR"` 等已知 ASCII tag，标准做法是用 `StringComparer.OrdinalIgnoreCase` 或 `.ToUpperInvariant()`。
- **影响**: 每条 BUL 命令产生 1 个临时字符串 + ICU 调用；谱面有数千子弹时持续分配。
- **建议**: 改成 `switch` + `string.Equals(s, "UPS", StringComparison.OrdinalIgnoreCase)`，或在外层 toUpperInvariant 一次复用。

### 19. DefaultStringMeasure.GetSupportFonts 启动期 Directory.GetFiles + LINQ ToLower 反复

- **位置**: `OngekiFumenEditor/Kernel/Graphics/OpenGL/Drawing/StringDrawing/DefaultStringMeasure.cs:128-135`
- **类别**: IO / 字符串
- **严重度**: Medium
- **问题**: `Path.GetExtension(x).ToLower() == ".ttf"`（行 134）—— ToLower 创建新串。
- **建议**: `Path.GetExtension(x).Equals(".ttf", StringComparison.OrdinalIgnoreCase)`，避免分配。

### 20. CachedSvgRenderDataManager.OnScheduleCall LINQ Where + ToArray 删除

- **位置**: `OngekiFumenEditor/Modules/FumenVisualEditor/Graphics/Drawing/TargetImpl/EditorObjects/SVG/Cached/DefaultImpl/CachedSvgRenderDataManager.cs:55-57`
- **类别**: 集合 / LINQ
- **严重度**: Low
- **问题**: `cachedDataMap.Where(...).ToArray()` 用于在迭代中删除项，正确，但每次调度都 ToArray 即便没需要删除的项。
- **建议**: 先 foreach 检查存在再 ToArray；或用 `cachedDataMap.RemoveAll(...)`（Dictionary 没有，需 helper）。

## 中等问题（Medium）

### 21. SaveRenderOrderVisible / LoadRenderOrderVisible 多层 LINQ + reflection 取类型名

- **位置**: `OngekiFumenEditor/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.Drawing.cs:158`、`:194-196`、`:723`
- **类别**: LINQ / 反射
- **严重度**: Medium
- **问题**: `drawTargetMap.Values.SelectMany(x => x).Distinct().ToArray()` + `targets.ToDictionary(x => x.GetType().Name, ...)`；以及 `ResortRenderOrder` 在每次顺序错乱时（来自渲染循环）也 `.SelectMany(...).OrderBy(...).Distinct().ToArray()`（行 723）。
- **影响**: 顺序错乱时在渲染循环中触发；`GetType().Name` 反射查询非缓存。
- **建议**: 缓存 type name；用预排序的固定数组结构。

### 22. PostDrawCommandList 异常路径 double-dispose 风险

- **位置**: `OngekiFumenEditor/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.Drawing.cs:828-844`
- **类别**: 内存
- **严重度**: Low
- **问题**: 不是直接的性能问题，但 try/catch + finally `builder?.Dispose()` 与 `RenderContext.PostDrawCommandList(drawCommandList, autoDispose: true)` 在 autoDispose 时由 receiver 接管命令列表所有权，逻辑稍复杂，可能在 throw 之后由 receiver 也 dispose 造成双重释放。
- **建议**: 改成显式 ownership flag 或者使用 try/finally with sentinel。

### 23. SelectionAreaKind.Select 用 objs.ToArray() 再 objs.Count() == 1

- **位置**: `OngekiFumenEditor/Modules/FumenVisualEditor/ViewModels/SelectionArea.cs:131-134`
- **类别**: 集合 / LINQ
- **严重度**: Low
- **问题**: 既然第 131 行 `objs = objs.ToArray()`，第 133 行直接 `((Array)objs).Length == 1` 比 `objs.Count()` 略快且不再走 LINQ Count 检查。
- **建议**: 改为 `var arr = objs.ToArray(); if (arr.Length == 1) ...`。

### 24. SelectionArea.GetRangeObjects 使用 Distinct + Where（每次鼠标释放）

- **位置**: `OngekiFumenEditor/Modules/FumenVisualEditor/ViewModels/SelectionArea.cs:88-91`
- **类别**: LINQ / 集合
- **严重度**: Medium
- **问题**: `editor.Fumen.GetAllDisplayableObjects().OfType<OngekiObjectBase>().Distinct().Where(Check)`。Distinct 在 IEnumerable 上需要内部 HashSet 分配；如果上游已经唯一，Distinct 是浪费。
- **建议**: 检查 `GetAllDisplayableObjects` 语义；如果保证唯一可去掉 Distinct。

### 25. DefaultFumenSoundPlayer.cacheSounds 使用 enum 作 Dictionary key 引发装箱

- **位置**: `OngekiFumenEditor/Kernel/Audio/DefaultCommonImpl/Sound/DefaultFumenSoundPlayer.cs:62`
- **类别**: 集合 / 内存
- **严重度**: Low
- **问题**: `Dictionary<SoundControl, ISoundPlayer>`：自 .NET 9 内置 EqualityComparer 已优化 enum，无装箱，但若构造时未提供 `EqualityComparer<SoundControl>.Default`，部分老路径仍可能慢 path。
- **建议**: 显式 `new Dictionary<SoundControl, ISoundPlayer>(EqualityComparer<SoundControl>.Default)`，保险。

### 26. CachedSvgRenderDataManager.GetRenderData 每次 DateTime.Now

- **位置**: `OngekiFumenEditor/Modules/FumenVisualEditor/Graphics/Drawing/TargetImpl/EditorObjects/SVG/Cached/DefaultImpl/CachedSvgRenderDataManager.cs:107`
- **类别**: 性能监控
- **严重度**: Low
- **问题**: `var curTime = DateTime.Now;`。GetRenderData 是渲染热路径调用；DateTime.Now 内部读取本地时区。
- **建议**: 改 `DateTime.UtcNow`（地区无关、稍快），或用 `Environment.TickCount64`。

### 27. Log.BuildLogMessage 反复 ToString().ToUpper() 严重度名

- **位置**: `OngekiFumenEditor/Utils/Log.cs:67-68`
- **类别**: 字符串
- **严重度**: Low
- **问题**: `record.Severity.ToString().ToUpper()` —— enum.ToString() 已经分配一次字符串，再 ToUpper 又一次。
- **建议**: 用预定义的 `static readonly string[]` 按 enum 索引返回；或 `nameof` 已知常量。

### 28. SoflanList.SoflanPoint.ToString 用 interpolated string 在 perf 输出会被命中

- **位置**: `OngekiFumenEditor/Base/Collections/SoflanList_CachedPositionList.cs:30`
- **类别**: 字符串
- **严重度**: Low
- **问题**: `public override string ToString() => $"Y:{Y} TGrid:{TGrid} SPD:{Speed} BPM:{Bpm.BPM}";` — 仅在 debug 输出时调用，但 release 中编译器会按需调用；不会主动调用，但若调试器 visualizer / log 在循环里被触发会很贵。
- **建议**: 标记 `[DebuggerDisplay(...)]` 而非 override；或保留 ToString 但避免在热路径中调用。

### 29. DrawPlayableAreaHelper 几乎每个内部函数都通过闭包 + GenAllPath + LINQ

- **位置**: `OngekiFumenEditor/Modules/FumenVisualEditor/Graphics/Drawing/Editors/DrawPlayableAreaHelper.cs:178-273`、`:286`、`:294`、`:331-340`
- **类别**: LINQ / 集合
- **严重度**: High
- **问题**: `EnumeratePoints` 中嵌套 lanes.SelectMany(...).Select(...).SequenceConsecutivelyWrap(2).Select(...).ToListWithObjectPool()，对每条 lane 重复 `Lanes.GetVisibleStartObjects(...).Where(x => x.LaneType == type)`（行 232–234、行 292–294）。
- **影响**: 高复杂度多次 LINQ 嵌套；预览模式每帧绘制。
- **建议**: 在 `DrawPlayField` 顶层一次性 filter `lanes by type`，把结果传入 EnumeratePoints；显式 for 循环替代 SelectMany 链。

### 30. EditorProjectFileManager.Clone 用 MemoryStream + ToArray 拷贝

- **位置**: `OngekiFumenEditor/Modules/FumenVisualEditor/Kernel/EditorProjectFile/EditorProjectFileManager.cs:42-47`
- **类别**: 内存 / 序列化
- **严重度**: Medium
- **问题**: `var ms = new MemoryStream(); await manager.Save(...); return await manager.Load<...>(ms.ToArray());` — ToArray 把整个 buffer 拷一份。
- **建议**: 用 `ms.GetBuffer()` + length；或者 `ms.TryGetBuffer(out var seg)`，或直接传 ReadOnlySpan。

### 31. CommonEditorProjectFileSerializer.CheckParsableAsync 每次 new MemoryStream

- **位置**: `OngekiFumenEditor/Modules/FumenVisualEditor/Kernel/EditorProjectFile/Serializers/CommonEditorProjectFileSerializer.cs:60-63`
- **类别**: 序列化 / 内存
- **严重度**: Low
- **问题**: 反序列化整个 buffer 只为读取一个 `Version` 字段。
- **建议**: 用 `Utf8JsonReader` 直接读到 `Version` 即可，不必反序列化整对象。

### 32. DefaultOngekiFumenParser 单线程逐行读取 + dictionary trim

- **位置**: `OngekiFumenEditor/Parser/Ogkr/DefaultOngekiFumenParser.cs:40-54`
- **类别**: 字符串 / IO
- **严重度**: Medium
- **问题**: 每行 `await reader.ReadLineAsync()` + `commandArg.GetData<string>(0)?.Trim()`（Trim 产生新串）。
- **建议**: 用 `ReadOnlySpan<char>` API + `MemoryExtensions.Trim`；命令名字典查询用 span lookup（.NET 9 `Dictionary<string,_>.GetAlternateLookup<ReadOnlySpan<char>>()`）。

### 33. DefaultNyagekiFumenParser 同样问题 + ToLower

- **位置**: `OngekiFumenEditor/Parser/DefaultImpl/Nyageki/DefaultNyagekiFumenParser.cs:24`、`:37`
- **类别**: 字符串
- **严重度**: Medium
- **问题**: 字典 key 用 ToLower（构造时分配），每行 `seg[0].ToLower().Trim()` 再次分配。
- **建议**: 字典用 `StringComparer.OrdinalIgnoreCase`，行解析就不再需要 ToLower。

### 34. FumenVisualEditor RenderControl_UnLoaded async void

- **位置**: `OngekiFumenEditor/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.Drawing.cs:979`、`:989`
- **类别**: 异步
- **严重度**: Medium
- **问题**: WPF 控件事件回调用 async void 是常见模式，但 `RenderContext = await renderImpl.GetOrCreateRenderContext(renderControl); RenderContext.StopRendering();` 若 await 期间被取消会丢异常。
- **建议**: 至少 try/catch 包住或改用任务跟踪。

### 35. AbortableThread.Abort 用 task.Wait() 阻塞

- **位置**: `OngekiFumenEditor/Utils/AbortableThread.cs:34`
- **类别**: 异步 / 并发
- **严重度**: Medium
- **问题**: `task?.Wait();` 在 UI 线程上同步等待——SchedulerManager.Term 中调用，关闭时可能卡 UI。
- **建议**: 提供 `Task AbortAsync()`。

### 36. ObjectInspector / FumenObjectPropertyBrowser MultiObjectsPropertyInfoWrapper 每属性反射查找

- **位置**: `OngekiFumenEditor/Modules/FumenObjectPropertyBrowser/MultiObjectsPropertyInfoWrapper.cs:70`、`:119`、`:35-38`
- **类别**: 反射
- **严重度**: Medium
- **问题**: 每次构造均 `objType.GetProperty(propertyName, BindingFlags...)` 反射；`Activator.CreateInstance(propertyInfo.PropertyType)`（行 119）在每次访问 DefaultValue 时调用。
- **建议**: 缓存 PropertyInfo（按 (Type, propertyName) key）；为值类型生成 lambda activator（与 `LambdaActivator` 同样套路）。

### 37. LambdaActivator.GetMatchingConstructor 每调用都用 LINQ 选 ctor

- **位置**: `OngekiFumenEditor/Utils/LambdaActivator.cs:119-126`
- **类别**: 反射 / LINQ
- **严重度**: Low
- **问题**: `var types = from arg in args where arg != null select arg.GetType(); return type.GetConstructor(types.ToArray());`。`CacheLambdaActivator` 已缓存（行 131），但 `CreateInstance(type, params object[] args)`（行 42）走的是非缓存路径。
- **建议**: 对 (type, ctor signature) 做缓存键。

### 38. SchedulerManager.AddScheduler / RemoveScheduler 用 FirstOrDefault 在 List 上线性扫描

- **位置**: `OngekiFumenEditor/Kernel/Scheduler/SchedulerManager.cs:40`、`:96`
- **类别**: 集合
- **严重度**: Low
- **问题**: List `FirstOrDefault(x => x.SchedulerName.Equals(...))` 是 O(N)；项目里 scheduler 数量有限，但既然有 ConcurrentDictionary 已存在，可考虑同步存名字。
- **建议**: 维持名字 -> scheduler 的 dictionary。

### 39. FumenVisualEditorViewModel.UserInteractionActions MenuItemAction_MirrorSelectionXGridZero 多次 OfType + ToList

- **位置**: `OngekiFumenEditor/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.UserInteractionActions.cs:270`、`:282`、`:876`
- **类别**: LINQ
- **严重度**: Low
- **问题**: `SelectObjects.OfType<OngekiMovableObjectBase>().ToList()` 反复构造列表用于一次性遍历。
- **建议**: 可以直接 foreach；如需 Count 用 `Count(predicate)` 或 `Where(...).Any()`。

### 40. DefaultOngekiFumenFormatter 多个 OrderBy + 字符串内插

- **位置**: `OngekiFumenEditor/Parser/Ogkr/DefaultOngekiFumenFormatter.cs:92`、`:276`、`:282`、`:286`、`:307`、`:316`、`:329` 等（Grep 计数 52 处 `$"..."`）
- **类别**: 字符串 / LINQ
- **严重度**: Medium
- **问题**: Save 时全表 `OrderBy(x => x.TGrid)` + 对每个对象做 `$"{o.IDShortName}\t{o.TGrid.Serialize()}\t..."`，未复用 StringBuilder.AppendFormat。`o.TagTblValue.ToString().ToUpper()` 又一层 ToString+ToUpper 分配。
- **影响**: 保存大谱面时显著；不是每帧但偶发卡顿源。
- **建议**: 用 `sb.Append(...).Append('\t')...` 拼接代替 `$"..."`；缓存 IDShortName + TGrid 字符串。

## 低优先级（Low）

### 41. RandomHepler 用 DateTime.Now.GetHashCode() 作种子

- **位置**: `OngekiFumenEditor/Utils/RandomHepler.cs:11`、`OngekiFumenEditor/Base/OngekiObjects/BulletPallete.cs:12`
- **类别**: 杂项
- **问题**: 既不是 cryptographic 也不是 monotonic 种子；并发场景下 DateTime.Now 分辨率低，会得到重复种子。
- **建议**: `Random.Shared` 即可（线程安全，自带高熵）。

### 42. ImageLoader.PrcessQueue 每次 MD5.Create + Convert.ToHexString

- **位置**: `OngekiFumenEditor/Utils/ImageLoader.cs:75-76`
- **类别**: 内存 / 字符串
- **问题**: 每次新建 MD5 实例。
- **建议**: 用 `MD5.HashData(...)` 静态方法。

### 43. KeyBindingDefinition.regex 编译选项缺失

- **位置**: `OngekiFumenEditor/Kernel/KeyBinding/KeyBindingDefinition.cs:80`
- **类别**: 正则
- **问题**: `new Regex(@"(\s*\w+\s*\+\s*)?(\w+)")` 无 `RegexOptions.Compiled`，也未 `[GeneratedRegex]`。
- **建议**: .NET 7+ 用 `[GeneratedRegex(@"...", RegexOptions.Compiled)]` 生成器。

### 44. OgkiFumenSet 中 Regex 无 Compiled 标志

- **位置**: `OngekiFumenEditor/Modules/OgkiFumenListBrowser/Models/OngekiFumenSet.cs:90`、`:91`
- **类别**: 正则
- **问题**: `static Regex BpmRegex = new Regex(...)` 无 RegexOptions.Compiled。
- **建议**: 改 `[GeneratedRegex]`。

### 45. ParserUtils Regex 也未编译

- **位置**: `OngekiFumenEditor/Parser/DefaultImpl/Nyageki/CommandImpl/ParserUtils.cs:25`
- **类别**: 正则
- **建议**: 同上。

### 46. DocumentOpenHelper.cs 内部 new Regex 在方法内

- **位置**: `OngekiFumenEditor/Utils/DocumentOpenHelper.cs:157`
- **类别**: 正则
- **问题**: `var match = new Regex(@"(\d+)_\d+").Match(...)` 在方法里临时实例化。
- **建议**: 提为 static + Compiled / GeneratedRegex。

### 47. GridBase.GetHashCode 用累加 hash 但未 readonly

- **位置**: `OngekiFumenEditor/Base/GridBase.cs:129-139`
- **类别**: 集合
- **问题**: Hashcode 基于 mutable 字段 Unit/Grid/GridRadix。把 GridBase 用作 Dictionary key 后修改会破坏 invariant。
- **建议**: 显式标注或在 mutation 前删除-再插入；或建模为不可变结构体。

## 跨切关注点 / 系统性建议

1. **全局缺少 ConfigureAwait(false)**（详见条目 11）。 推荐统一在非 UI 库代码加上，或使用 Fody.ConfigureAwait 编译期植入。
2. **大量 `async void`**（Grep 命中 90+ 处，参见前述列表）：除 UI 事件 handler 外，许多业务逻辑（如 `EditorSetting.RequestSave`、`Log.AwakeLogger` 中的 Task.Run 闭包、`ImageLoader.PrcessQueue`、`DefaultFumenSoundPlayer.InitSounds`、`SchedulerManager.Run`、`DefaultMusicPlayer.Play/Pause/Stop/Dispose`）均为 `async void`，使异常无法被捕获。建议统一为 `async Task`，由 caller fire-and-forget 时用 `.NoWait()`（已有的 helper）或显式 `_ = ...`。
3. **Skia 实现大量 `using var paint = new SKPaint()`** 模式（条目 5）—— 应抽出复用 paint 池。
4. **多处 LINQ 在每帧渲染路径上**（条目 1/2/3/13/29）。建议建立一个 `[HotPath]` 注释/分析器规则，并把热路径迁移到 foreach + 对象池 + ArrayPool。
5. **正则全部未使用 `[GeneratedRegex]`/`RegexOptions.Compiled`**（条目 43–46）。.NET 9 支持源生成正则，零开销切换。
6. **解析器 ToUpper/ToLower 大量出现**（条目 18、19、27、33、40）。使用 `OrdinalIgnoreCase` 或 `Span<char>.ToUpperInvariant` + comparer 可以省下整条字符串拷贝。
7. **TypeDescriptor.GetConverter** 在 parser 热路径未缓存（条目 17）；对所有 string -> primitive 的 hot path，应改用 `int.TryParse(ReadOnlySpan<char>, ...)`/`float.TryParse(...)` 显式。
8. **同步等待异步方法**（条目 9、10、35）：UI 线程上的 `.Wait()` 是 deadlock 风险源；建议代码评审中 grep `.Wait()`/`.Result`/`GetAwaiter().GetResult()` 并审视每一处。
9. **`DateTime.Now`** 在每帧或调度循环里出现（条目 7、26）。建议用 `Stopwatch.GetTimestamp()` 或 `Environment.TickCount64`。
10. **可考虑引入 ArrayPool / ObjectPool 的位置**: `DefaultSkiaLineDrawing.PostDraw` 中的 `SKPath`、`DefaultStringDrawing` 的 `renderer`、`DrawPlayableAreaHelper` 的 `polygonVertices`（已部分对象池化），其余每帧 list/array 还可继续覆盖。

## 结论与建议优先级

按"投入产出比 / 修复复杂度"建议处理顺序：

1. **立即修（低风险高收益）**:
   - 条目 5：Skia paint pool（每帧分配，立竿见影）
   - 条目 11：批量加 ConfigureAwait(false) 到非 UI 库代码
   - 条目 17：ParserUtils 缓存 TypeConverter + 移除多余 ToArray
   - 条目 18/33：ToUpper/ToLower → Ordinal comparer
   - 条目 43-46：Regex → [GeneratedRegex]
   - 条目 9/10/35：移除 `.Wait()`/`.GetResult()`

2. **下一轮重构**:
   - 条目 1-4、13、15、29：渲染热路径上的 LINQ/字符串去除（最大 FPS 提升）
   - 条目 7：SchedulerManager 替换 async void + DateTime.Now
   - 条目 12：ImageLoader Semaphore 重写

3. **长期演进**:
   - 条目 8、14、20：并行/枚举使用规范化
   - 条目 36-37：反射缓存层
   - 条目 41-42：Random/MD5 现代化

各条目下都有具体行号和文件路径，可作为 PR 的精细 backlog 来逐项推进。
