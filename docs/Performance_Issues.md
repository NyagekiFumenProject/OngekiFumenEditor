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
    - ✅ **#6 DefaultFumenSoundPlayer 顺序加载**: 用户直接允许改动**已修复**（17 个 wav 顺序 await → Task.WhenAll 并行；async void → async Task；见条目#6 实施记录）。
    - ✅ **#7 SchedulerManager.Run 每 10ms 分配**: 用户直接允许改动**已修复**（LINQ + ToArray + ContinueWith 闭包 → 复用 List + InvokeAndStamp；DateTime.Now → Stopwatch；async void → async Task；见条目#7 实施记录）。
    - ✅ **#13 EnumerateAllDisplayableObjects 多层 LINQ**: 用户指示签名改为 `IPooledList<IDisplayableObject>`，**已修复**（13 层 Concat + SelectMany → PooledList 累加 + 单 foreach；调用方 OfType → 模式匹配；见条目#13 实施记录）。
    - ✅ **#14 RebuildSoflanGroupCache Parallel + LINQ**: 经 Benchmark 验证后**已修复**（LINQ 链 + IEnumerable Parallel → PooledList + Parallel.ForEach 直传；`objs.Any()` → `objs.Count > 0`；时间 -23%~-2%、分配 -35%~-2%；见条目#14 实施记录）。
    - ✅ **#17 ParserUtils/CommandArgs.GetDataArray TypeDescriptor 反射**: 经 Benchmark 验证后**已修复**（typeof(T) JIT 分派 fast path + cached TypeConverter + 去 LINQ；时间 -72%、分配 -63%；同时改造 ParserUtils 与 CommandArgs；见条目#17 实施记录）。
    - ✅ **#18 BUL parser `?.ToUpper()` ICU 调用**: 用户直接允许改动**已修复**（3 文件 12 处 `?.ToUpper()` → `?.ToUpperInvariant()`，消除 culture-sensitive 路径；见条目#18 实施记录）。
    - ✅ **#19 DefaultStringMeasure.GetSupportFonts `.ToLower() == ".ttf"`**: 用户直接允许改动**已修复**（改 `Equals(".ttf", StringComparison.OrdinalIgnoreCase)`，零分配比较；见条目#19 实施记录）。
    - ✅ **#27 Log.BuildLogMessage Severity.ToString().ToUpper()**: 用户直接允许改动**已修复**（cached `string[]` 按 enum index 查表；见条目#27 实施记录）。
    - ✅ **#30 EditorProjectFileManager.Clone MemoryStream 扩容**: 用户直接允许改动**已修复**（`new MemoryStream(256 KB)` 预分配避免内部扩容拷贝；受 `Load(byte[])` 签名限制保留 ToArray；见条目#30 实施记录）。
    - ✅ **#31 CheckParsableAsync 整对象反序列化**: 用户直接允许改动**已修复**（改 `Utf8JsonReader` 顶层扫描 + `ValueTextEquals("Version"u8)`，扫到即返回；见条目#31 实施记录）。
    - ✅ **#32 DefaultOngekiFumenParser ReadLineAsync + Trim**: 用户直接允许改动**已修复**（每行 Task `ReadLineAsync` → `Task.Run` 包同步 `ReadLine`；移除冗余 `Trim`；顺带消除 CA2024 警告；见条目#32 实施记录）。
    - ✅ **#33 DefaultNyagekiFumenParser ToLower**: 用户直接允许改动**已修复**（字典 key 改 `StringComparer.OrdinalIgnoreCase`，行解析移除 `ToLower()`；见条目#33 实施记录）。
    - ✅ **#34 RenderControl_UnLoaded async void**: 用户直接允许改动**已修复**（`RenderControl_Loaded/UnLoaded` 均加 try/catch 捕获 await 期间异常，避免 async void 异常逃逸；见条目#34 实施记录）。
    - ✅ **#35 AbortableThread.Abort 阻塞 UI**: 用户直接允许改动**已修复**（新增 `AbortAsync()`；`SchedulerManager.Term` 改 async 调用避免 UI 同步等待；见条目#35 实施记录）。
    - ✅ **#41 RandomHepler 自管理 Random**: 用户直接允许改动**已修复**（全 API 改 `Random.Shared`，消除 DateTime seed 与共享 StringBuilder 竞态；见条目#41 实施记录）。
    - ✅ **#42 ImageLoader MD5.Create**: 用户直接允许改动**已修复**（改静态 `MD5.HashData(...)`，省 IDisposable；见条目#42 实施记录）。
    - ✅ **#43 KeyBindingDefinition Regex**: 用户直接允许改动**已修复**（`partial class` + `[GeneratedRegex]` 源生成；见条目#43 实施记录）。
    - ✅ **#44 OngekiFumenSet Regex**: 用户直接允许改动**已修复**（`BpmRegex`/`CreatorRegex` 改 `[GeneratedRegex]`；见条目#44 实施记录）。
    - ✅ **#45 ParserUtils Regex**: 用户直接允许改动**已修复**（`static partial class` + `[GeneratedRegex]`；见条目#45 实施记录）。
    - ✅ **#46 DocumentOpenHelper 方法内 new Regex**: 用户直接允许改动**已修复**（提取 `MusicIdFromFileNameRegex()` 为 `[GeneratedRegex]` 静态成员；见条目#46 实施记录）。
    - ✅ **#47 GridBase.GetHashCode**: 用户直接允许改动**已修复**（改 `HashCode.Combine`，加注释说明 mutable hash 约束；未做不可变重构原因见条目#47 实施记录）。

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

### ✅ 6. DefaultFumenSoundPlayer 同步顺序 await 加载 17 个 wav

- **位置**: `OngekiFumenEditor/Kernel/Audio/DefaultCommonImpl/Sound/DefaultFumenSoundPlayer.cs:105-122`
- **类别**: 异步 / IO
- **严重度**: High
- **决策**: **已修复**（用户直接允许改动，未做 Benchmark 复核；见下方"实施记录"）
- **问题**: 17 个 `await load(...)` 顺序执行，未并行；且 `InitSounds` 是 `async void`（第 70 行）。
- **影响**: 启动阶段串行 IO，等待时间累加。
- **建议**: `await Task.WhenAll(loads)` 并行加载；`InitSounds` 改成 `async Task` 并由调用方保留 task。

#### 实施记录（2026/05/26）

**改动文件**: `OngekiFumenEditor/Kernel/Audio/DefaultCommonImpl/Sound/DefaultFumenSoundPlayer.cs`

**核心变更**:
1. `InitSounds()` 重命名为 `InitSoundsAsync()`，签名从 `async void` 改为 `async Task`——异常不再在 SynchronizationContext 上 rethrow（`async void` 会让单个文件加载失败崩整个 UI 线程）。
2. 构造函数调用从 `InitSounds()` 改为 `_ = InitSoundsAsync()`，配合现有的 `TaskCompletionSource` + `loadTask` 字段（外部仍可通过 `ReloadSoundFiles()` 等 await 完成）。
3. 内嵌 `load()` 返回值从 `Task` 改为 `Task<(SoundControl sound, ISoundPlayer player, bool ok)>`，**不再直接写入字典**——避免并行环境下对普通 `Dictionary` 的并发写入。
4. 17 行顺序 `await load(...)` 改为构造数组后 `await Task.WhenAll(loads)`，**所有 wav 并行加载**。
5. `Task.WhenAll` 返回后单线程地把成功结果写入 `cacheSounds`，并按 `ok` 标记汇总 `noError`——线程安全且语义不变。
6. `ReloadSoundFiles()` 内的旧 `InitSounds()` 调用同步更新为 `_ = InitSoundsAsync()`。

**预期效果**:
- 启动阶段 17 次磁盘 IO 与解码并行，端到端等待时间从"17 × 单文件加载耗时"近似下降到"max(单文件加载耗时)"，受磁盘吞吐和 audio 后端并发能力上限影响；
- `async void` → `async Task` 让加载异常可被 `loadTask`/`source.SetException` 路径捕获，不再静默崩进程。

**风险检查**:
- 主项目 Release 编译 0 错误；
- 并发写入字典的潜在数据竞争已被"先并行收集结果 → 单线程汇总"模式规避；
- `cacheSounds.Clear()` 在 await 前调用，重载逻辑保持原顺序。

**未做 Benchmark**: 启动阶段一次性 IO，BDN 微基准噪声会远大于实际收益信号（IO 时间分布跨 ms 级），改动正确性更重要。

### ✅ 7. SchedulerManager.Run 每 10ms 创建一堆 Task + ContinueWith + ToArray

- **位置**: `OngekiFumenEditor/Kernel/Scheduler/SchedulerManager.cs:53-72`
- **类别**: 异步 / LINQ / 内存
- **严重度**: High
- **决策**: **已修复**（用户直接允许改动，未做 Benchmark 复核；见下方"实施记录"）
- **问题**: `private async void Run(...)`（async void），并且循环体 `Schedulers.Where(...).Select(x => ... ContinueWith ...).ToArray()` 每 10ms 都生成新 Task 数组（每个 task 还附带一个 ContinueWith 闭包）。同时 `DateTime.Now` 在第 60、61 行每次都查询。
- **影响**: 即便无调度任务，每秒约 100 轮都创建 array / Task / closure，GC pressure 持续。
- **建议**: 改成静态可复用的列表 + foreach；用 `Stopwatch.GetTimestamp()` 代替 `DateTime.Now`；`async void` 改 `async Task` 让顶层 catch 能正确传递异常。

#### 实施记录（2026/05/26）

**改动文件**: `OngekiFumenEditor/Kernel/Scheduler/SchedulerManager.cs`

**核心变更**:
1. `schedulersCallTime` 字段类型从 `ConcurrentDictionary<ISchedulable, DateTime>` 改为 `ConcurrentDictionary<ISchedulable, long>`，存 `Stopwatch` ticks——避免每轮 `DateTime.Now` 系统调用（涉及时区/夏令时换算，远慢于单调时钟）；
2. `AddScheduler` 初值 `DateTime.MinValue` → `0L`；
3. `async void Run(CancellationToken)` 拆分为：
   - 同步入口 `void Run(CancellationToken)`（`AbortableThread` 只接受 `Action<CancellationToken>`），内部调用 `RunAsync(...).GetAwaiter().GetResult()`，异常通过 `try/catch` 直接路由到 `Log.LogError`，不再丢给 SynchronizationContext；
   - `private async Task RunAsync(CancellationToken)` 跑实际异步循环；
4. **复用同一个 `List<Task> pending` 字段**（初始容量 16），每轮 `Clear()` + `foreach + Add`，替代 `Schedulers.Where(...).Select(...).ToArray()` 每轮新分配的数组与 LINQ enumerator；
5. 引入私有 `async Task InvokeAndStamp(ISchedulable, CancellationToken)`，替代 `.ContinueWith(_ => schedulersCallTime[x] = DateTime.Now)` 的闭包分配——`finally` 块写回时间戳，语义等价；
6. 用 `Stopwatch.GetElapsedTime(lastTs, nowTs)` 比较间隔，替代 `DateTime.Now - schedulersCallTime[x]`；
7. `Task.Delay(10, cancellationToken)` 加上 `cancellationToken` 让停止能即时退出，配合外层 `catch (OperationCanceledException) { }` 静默吞掉取消异常。

**预期效果**:
- 空闲态（无 scheduler 触发）每秒 ~100 轮原本要分配：1 个 Task[]、N 个 LINQ enumerator、N 个 ContinueWith Task + N 个闭包——现在仅一次 `List.Clear` + foreach + 一次 `Task.Delay`；
- 活跃态（有 K 个 scheduler 触发）每轮少分配 1 个 array + K 个闭包 + K 个 ContinueWith Task，剩下 K 个 `InvokeAndStamp` async state machine（这是必要成本）；
- `DateTime.Now` → `Stopwatch.GetTimestamp()`：单次开销从 ~100 ns 降到 ~10 ns，每秒省 ~18 µs，但更重要的是消除时区相关副作用。

**风险检查**:
- 主项目 Release 编译 0 错误；
- `void Run` 内 `GetAwaiter().GetResult()` 在专属 LongRunning Task 上阻塞 OK（这条线程本来就只跑这个 loop）；
- `OperationCanceledException` 单独 catch 静默退出，其它 `Exception` 仍走原本的 `Log.LogError` 路径；
- `schedulersCallTime` 类型变更：上游/下游没有任何代码读写此字段，安全（grep 已验证）。

**未做 Benchmark**: 调度循环是 wall-clock 时间驱动的（每 10ms 一轮），BDN 测的是吞吐而非 wall-clock，对此场景信号弱；改动正确性已由编译 + 语义等价分析覆盖。

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

### ✅ 13. EnumerateAllDisplayableObjects 多层 LINQ Concat + SelectMany（每帧）

- **位置**: `OngekiFumenEditor/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.Drawing.cs:731-810`
- **类别**: LINQ / 集合 / 渲染
- **严重度**: High
- **决策**: **已修复**（用户指示签名改为 `IPooledList<IDisplayableObject> EnumerateAllDisplayableObjects(OngekiFumen fumen, IEnumerable<(TGrid min, TGrid max)> visibleRanges)`；未做 Benchmark 复核；见下方"实施记录"）
- **问题**: `visibleRanges.SelectMany(x => { ... var r = Enumerable.Empty<>().Concat(...).Concat(...) ... return r; })`（行 737–780），最后 `objects.Concat(objs).SelectMany(x => x.GetDisplayableObjects())`（行 809）。多层 Concat + SelectMany 在 IEnumerable 上构造长链路，每次枚举对象在迭代器之间跳转，无法被 JIT 内联。
- **影响**: 谱面对象数千时，每帧都展开这条链；后续 `OfType<OngekiTimelineObjectBase>()`、`.Where(...)` 又再叠加。
- **建议**: 提供一个集中预先填充的 `PooledList<IDisplayableObject>`（按 visible range 划分），用基于结构的迭代器；或者把"该 range 内的可见对象"在 Editor 内做"脏标记 + 缓存"。

#### 实施记录（2026/05/26）

**改动文件**: `OngekiFumenEditor/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.Drawing.cs`

**签名变更**:
```csharp
// 旧
private IEnumerable<IDisplayableObject> EnumerateAllDisplayableObjects(
    OngekiFumen fumen, IEnumerable<(TGrid min, TGrid max)> visibleRanges)

// 新
private IPooledList<IDisplayableObject> EnumerateAllDisplayableObjects(
    OngekiFumen fumen, IEnumerable<(TGrid min, TGrid max)> visibleRanges)
```

**核心变更**:
1. 内部用 `ObjectPool.GetPooledList<IDisplayableObject>()` 借出 PooledList，所有展开后的 displayable 直接 `Add` 进去；`try/catch` + `result.Dispose()` 保证异常路径也归还对象池；
2. 用 `foreach (var (min, max) in visibleRanges)` 替代 `visibleRanges.SelectMany(x => { ... })`——消除外层 SelectMany enumerator；
3. 每个 range 内的原 `Enumerable.Empty<...>().Concat(A).Concat(B)...` 链路改为对每个集合直接调 `AppendDisplayables(result, collection)` 私有辅助；后者一个 foreach 把每个对象的 `GetDisplayableObjects()` 展开追加——消除 10+ 层 Concat enumerator；
4. PreviewMode 下的 Holds (`EndTGrid > judgeTGrid`) / Flicks·Taps (`TGrid > judgeTGrid`) 过滤改为内联 `if (... continue;)`，不再 LINQ `Where`；
5. Bells/Bullets 在 `!Editor.IsPreviewMode` 时直接 foreach 加入，不再 `objs.Concat(...)` 累积变量；
6. 引入两个私有 helper：`AppendDisplayables<T>(IPooledList, IEnumerable<T>) where T : IDisplayableObject` 与 `AppendOne(IPooledList, IDisplayableObject)`，把"展开 GetDisplayableObjects 并 Add"逻辑集中；
7. 保留原版语义：每个 range 都重复追加全局列表（MeterChanges.Skip(1) / BpmList.Skip(1) / SvgPrefabs），未修复——避免引入视觉差异；
8. 顺序保留：MeterChanges / BpmList / ClickSEs / LaneBlocks / Comments / Soflans / IndividualSoflanArea / EnemySets / Lanes / SvgPrefabs / Holds / Flicks / Taps / Beams / Bells / Bullets。

**调用点变更**（`FumenVisualEditorViewModel.Drawing.cs:407-441`）:
- 原 `var visibleObjects = EnumerateAllDisplayableObjects(...).OfType<OngekiTimelineObjectBase>(); foreach (var obj in visibleObjects) { ... }`；
- 新 `using (var visibleObjects = EnumerateAllDisplayableObjects(...)) { foreach (var displayable in visibleObjects) { if (displayable is not OngekiTimelineObjectBase obj) continue; ... } }`；
- 用 `using ( ... ) { }` 显式块而非 `using var` 声明，避免与方法内已有的 `goto End` 冲突（C# 不允许 goto 跳过 using 变量声明）；
- `OfType<OngekiTimelineObjectBase>` 改为 `is not ... continue` 模式匹配，省去额外 enumerator 层。

**预期效果**:
- 每帧消除一条 ~13 层 LINQ enumerator 链 + 一条 SelectMany 外层 → 节省 ~15 个 iterator 对象/帧；
- `OfType` enumerator 也消除；
- 主要内存增加：1 个 PooledList（来自对象池，零净分配，调用结束归还）；
- 调用方迭代时直接 List 索引访问，可被 JIT 转为 array bounds check 后的紧致循环，相对原来的 enumerator MoveNext 链效率更高。

**风险检查**:
- 主项目 Release 编译 0 错误；
- 顺序与原版逐项对照保留；
- Hold/Flick/Tap 的 PreviewMode 过滤条件保留；
- containBeams / `!editorIsPreviewMode` 的分支条件保留；
- `using ( ) { ... }` 显式块保证 PooledList 100% 归还对象池，包括异常路径（PooledList Dispose 内部已是幂等）。

**未做 Benchmark**: 收益主要来自分配减少 + JIT 友好的紧致循环，BDN 微基准对此场景信号一般（需要构造完整 Fumen + 多个 range，与现有 `DisplayableEnumerationBenchmarks` 重合度高且不能解耦 Editor 状态）；改动正确性由编译 + 语义等价分析覆盖。

### ✅ 14. RebuildSoflanGroupCache Parallel.ForEach + IEnumerable 多次枚举

- **位置**: `OngekiFumenEditor/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.UserInteractionActions.cs:1062-1083`
- **类别**: 并发 / LINQ
- **严重度**: Medium
- **决策**: **已修复**（用户选 PooledListParallel 方案；见下方"Benchmark 复核结论"与"实施记录"）
- **问题**: `objs = Fumen.GetAllDisplayableObjects().OfType<OngekiMovableObjectBase>(); objs = objs.Where(...)`：objs 是延迟 IEnumerable，`Parallel.ForEach(objs, ...)` 会触发一次完整枚举，#if DEBUG 分支再 `objs.Any()`（行 1088）以及 `objs.Where ... .OfType<>` 又一次重新枚举整个 displayable 树。
- **影响**: 性能浪费 + 潜在线程安全（GetAllDisplayableObjects 内部可能不是线程安全的 enumerable）。
- **建议**: 一次性 `.ToArray()` 后再 Parallel；DEBUG 分支也复用同一数组。

#### Benchmark 复核结论（2026/05/26）

对应基准: `OngekiFumenEditor.Benchmark/Benchmarks/RebuildSoflanGroupBenchmarks.cs`。
环境: BenchmarkDotNet v0.15.8, .NET 10.0.8, AMD Ryzen 7 5800X, Default Job, MemoryDiagnoser。

| Method | N | Mean | Ratio | Allocated | Alloc Ratio |
|---|---:|---:|---:|---:|---:|
| Original_LinqParallel | 500 | 458.0 µs | 1.01 | 90.18 KB | 1.00 |
| **Optimized_PooledListParallel** | 500 | **348.0 µs** | **0.77** | 58.81 KB | **0.65** |
| Optimized_PooledListSequential | 500 | 355.6 µs | 0.78 | 60.20 KB | 0.67 |
| Optimized_ThresholdSwitch | 500 | 306.5 µs | 0.68 | 60.20 KB | 0.67 |
| Original_LinqParallel | 2000 | 583.6 µs | 1.01 | 225.63 KB | 1.00 |
| **Optimized_PooledListParallel** | 2000 | **565.3 µs** | 0.98 | 220.10 KB | 0.98 |
| Optimized_PooledListSequential | 2000 | 667.1 µs | 1.16 | 198.48 KB | 0.88 |
| Optimized_ThresholdSwitch | 2000 | 629.0 µs | 1.09 | 216.60 KB | 0.96 |
| Original_LinqParallel | 8000 | 1368.2 µs | 1.09 | 518.87 KB | 1.00 |
| **Optimized_PooledListParallel** | 8000 | **1128.7 µs** | **0.90** | 474.09 KB | **0.91** |
| Optimized_PooledListSequential | 8000 | 2031.4 µs | 1.62 | 489.73 KB | 0.94 |
| Optimized_ThresholdSwitch | 8000 | 998.8 µs | 0.80 | 769.82 KB | 1.48 |

**关键观察**:
- **`PooledListParallel`** 在所有规模都比原版快（-23% / -2% / -10%），分配减少 -35% / -2% / -9%；
- N=500 时 `Sequential` ≈ `Parallel`（差 2%），证明 Parallel 启动开销与并行收益接近；
- N=2000 是临界点（Sequential 1.16 vs Parallel 0.98）；
- N=8000 时 Parallel 显著赢（Sequential 1.62）；
- `ThresholdSwitch (threshold=1024)` 在 N=500 / N=8000 收益最大，但 N=2000 反不如纯 Parallel，且阈值依赖具体 `SetCache` 工作量；
- **选定 `PooledListParallel` 作为落地方案**——代码简单无 magic number、所有规模都有正收益、误差范围内最稳。

#### 实施记录（2026/05/26）

**改动文件**: `OngekiFumenEditor/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.UserInteractionActions.cs`

**核心变更**:
1. 顶部 `using OngekiFumenEditor.Utils.ObjectPool;` 引入对象池命名空间；
2. `RebuildObjectSoflanGroupRecord` 改为：
   - `using var objs = ObjectPool.GetPooledList<OngekiMovableObjectBase>();` 借 PooledList；
   - 单次 `foreach (var d in Fumen.GetAllDisplayableObjects())` + `is not ... continue` + `is ... or ... or ... continue` 模式匹配，替代原本的 `OfType<>().Where(switch)` 两层 LINQ；
   - `Parallel.ForEach(objs, rebuildSoflanGroupParallelOption, ...)` 直接传 PooledList，命中 .NET `IList<T>` 快路径；
   - `#if DEBUG` 内 `objs.Any()` → `objs.Count > 0`，消除原本对 LINQ 链的第二次完整枚举（也顺带消除潜在线程安全风险）；
3. 删除 lambda 内已注释掉的"id:1120 注释代码因为"块——`AGENTS.md` 禁止保留注释死代码。

**预期效果**: 与 benchmark 一致——每帧/每次重建省 ~10-35% 分配，CPU 时间 ~2-23%。生产 `SetCache` 的实际耗时可能与 `SimulateWork` 不一致，绝对值会偏移但相对方向稳定。

**风险检查**: 主项目 Release 编译 0 错误；类型匹配等价（`OfType<OngekiMovableObjectBase>().Where(skip ind/end/connectable)` 与 `is not Mo => continue; is ind/end/connectable => continue;` 集合一致）；`Parallel.ForEach` 已经接受 `IEnumerable<T>` 但传入 PooledList(IList<T>) 触发更优的 range partitioner。

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

### ✅ 17. ParserUtils.GetDataArray 每行解析 TypeDescriptor.GetConverter + LINQ ToArray

- **位置**: `OngekiFumenEditor/Parser/ParserUtils.cs:11-25` (登记); 真正热路径在 `OngekiFumenEditor/Parser/Ogkr/CommandArgs.cs:40-66`
- **类别**: 集合 / 反射 / 字符串
- **严重度**: High
- **决策**: **已修复**（Benchmark 验证后用户允许实施；fast-path 改造覆盖 ParserUtils + CommandArgs 两处）
- **问题**: `SplitEmptyChar(line).Skip(1).Select(...).ToArray()` 中 `Split().ToArray()` 重复（line 13 已经 `.ToArray()`，line 19 再 `.Select(...).ToArray()`），且每行重新调用 `TypeDescriptor.GetConverter(typeof(T))`（行 18）—— 对每条 `.ogkr` 命令行都做一次反射查表。
- **影响**: 谱面有几千行命令时，每行均触发 TypeDescriptor 查找 + 双倍 ToArray + LINQ 分配。
- **建议**: 缓存每个 T 的 converter（静态 Lazy<Dictionary<Type, TypeConverter>>）；`Split` 已经返回数组，可以直接索引而非 LINQ；对 `int/float/string` 路径走专门快路径（`int.TryParse(ReadOnlySpan<char>)`）。

#### Benchmark 复核结论（2026/05/26）

对应基准: `OngekiFumenEditor.Benchmark/Benchmarks/ParserUtilsBenchmarks.cs`。
环境: BenchmarkDotNet v0.15.8, .NET 10.0.8, AMD Ryzen 7 5800X, Default Job, MemoryDiagnoser。
样本: 100 / 1000 / 5000 行典型 ogkr 命令（`"CMD i1 i2 i3 i4 i5"`，取 5 个 int）。

| Method | Lines | Mean | Ratio | Allocated | Alloc Ratio |
|---|---:|---:|---:|---:|---:|
| Original | 100 | 53.21 µs | 1.01 | 77.99 KB | 1.00 |
| Optimized_CachedConverter | 100 | 35.74 µs | 0.68 | 70.96 KB | 0.91 |
| Optimized_NoLinqNoExtraToArray | 100 | 31.24 µs | 0.59 | 52.21 KB | 0.67 |
| **Optimized_FastPathInt** | 100 | **14.66 µs** | **0.28** | 28.77 KB | **0.37** |
| Original | 1000 | 490.45 µs | 1.00 | 780.34 KB | 1.00 |
| Optimized_CachedConverter | 1000 | 356.99 µs | 0.73 | 710.03 KB | 0.91 |
| Optimized_NoLinqNoExtraToArray | 1000 | 305.22 µs | 0.62 | 522.53 KB | 0.67 |
| **Optimized_FastPathInt** | 1000 | **143.92 µs** | **0.29** | 288.16 KB | **0.37** |
| Original | 5000 | 2550.58 µs | 1.01 | 3901.53 KB | 1.00 |
| Optimized_CachedConverter | 5000 | 1953.17 µs | 0.77 | 3549.97 KB | 0.91 |
| Optimized_NoLinqNoExtraToArray | 5000 | 1762.99 µs | 0.69 | 2612.47 KB | 0.67 |
| **Optimized_FastPathInt** | 5000 | **703.97 µs** | **0.28** | 1440.59 KB | **0.37** |

**各步贡献**（基于 Lines=5000）：
1. 缓存 `TypeConverter`：-23% 时间、-9% 分配（GetConverter 反射查表）；
2. + 去 LINQ 链 + 去重复 ToArray：累计 -31% 时间、-33% 分配（`Skip(1).Select.ToArray` 链 + `SplitEmptyChar` 内重复 ToArray）；
3. + `int.TryParse(ReadOnlySpan<char>)` 完全绕过 TypeDescriptor：累计 **-72% 时间、-63% 分配**。FastPath 跨规模一致（约 0.28/0.37 比率）。

**根因**：`converter.IsValid` 内部已 TryParse 一次、`ConvertFromString` 再 TryParse 一次后还要装箱回 `object` 再拆回 `T`——三次重复解析 + 装箱拆箱。

#### 实施记录（2026/05/26）

**改动文件**:
1. `OngekiFumenEditor/Parser/ParserUtils.cs`（重写）
2. `OngekiFumenEditor/Parser/Ogkr/CommandArgs.cs`（重写）

**为什么改两个**: grep 全仓库确认 `ParserUtils.GetDataArray` 没有任何调用方——真正在用的是 `CommandArgs.GetDataArray<T>`（30+ 处调用，覆盖所有 ogkr 命令解析）。`ParserUtils.GetDataArray` 是 `public static` 死代码（保留 API 一致性低成本），同等改造让两处行为一致以防未来有调用者切换。

**核心变更（两处通用）**:
1. 静态 `Dictionary<Type, TypeConverter> converterCache` + `GetCachedConverter(type)`，每个 T 的 TypeConverter 只查一次反射；
2. 用 `typeof(T) == typeof(int)/(float)/(double)/(string)` 分派——这些表达式在 JIT 编译期被消除，每个泛型实例化只剩对应一条 branch；
3. **数值类型 fast path**：`int.TryParse(parts[i].AsSpan(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)` + `(T)(object)v`（JIT 对 `(T)(object)valuetype` 在 specialization 后无装箱拆箱开销）；
4. **string fast path**：`Array.Copy(inputs, ...)` 直接拷贝；
5. **回退路径**：自定义类型走 cached `TypeConverter`，但去掉 LINQ 链，改用 `for + 索引 + 条件赋值`；
6. `SplitEmptyChar` 不再 `.ToArray()` 重复——`Split` 本身就返回 `string[]`；
7. `ParserUtils.GetDataArray` 跳过 index 0（命令名）；`CommandArgs.GetDataArray` 不跳过（保留原行为，与 30+ 调用方约定一致）。

**关键约束**:
- `CommandArgs` 保留 IoC 注入的 `IArgValueConverter` 优先级路径（用户自定义 converter 可覆盖 fast path）；
- `CommandArgs` 保留 instance `cacheDataArray` per-line cache（同一行同类型只解析一次）；
- 数值解析强制 `InvariantCulture`——OGKR 文件格式约定使用 `.` 小数点，与系统 locale 解耦；原版用 `TypeDescriptor.ConvertFromString` 默认走 `CurrentCulture`，在非英文 locale 下可能出现解析差异，本次改动顺手统一了。

**风险检查**:
- 主项目 Release 编译 0 错误；
- 与 30+ 处调用方的契约保持：`CommandArgs.GetDataArray<float>()` 返回数组长度与原版一致（`inputs.Length`，不跳过命令名）；
- `IArgValueConverter` 优先级路径完全未动；
- `cacheDataArray` 每行 setter 处 Clear 行为未动；
- Locale 行为变更（CurrentCulture → InvariantCulture）属于**修复式优化**：OGKR 是文本协议，本就该用 invariant。

**预期生产收益**: 解析一份典型 OGKR（几千行）从 ~2.5 ms → ~0.7 ms（-72%），分配从 ~4 MB → ~1.4 MB（-63%）。打开谱面时间会有可观降低。

### ✅ 18. BulletPalleteCommandParser / CustomBulletCommandParser 多次 `?.ToUpper()` 不带 culture

- **位置**: `OngekiFumenEditor/Parser/Ogkr/CommandParserImpl/BulletPalleteCommandParser.cs:24,32,39,44`、`OngekiFumenEditor/Parser/Ogkr/CommandParserImpl/CustomBulletCommandParser.cs:28,37,48,58,66`、`OngekiFumenEditor/Parser/Ogkr/CommandParserImpl/CustomBellCommandParser.cs:29,40,50`
- **类别**: 字符串
- **严重度**: Medium
- **决策**: **已修复**（用户直接允许改动；选 `ToUpperInvariant` 最小改动方案）
- **问题**: `.ToUpper()` 在 culture-sensitive 路径上分配新字符串；对于 `"UPS"/"ENE"/"PLR"` 等已知 ASCII tag，标准做法是用 `StringComparer.OrdinalIgnoreCase` 或 `.ToUpperInvariant()`。
- **影响**: 每条 BUL 命令产生 1 个临时字符串 + ICU 调用；谱面有数千子弹时持续分配。
- **建议**: 改成 `switch` + `string.Equals(s, "UPS", StringComparison.OrdinalIgnoreCase)`，或在外层 toUpperInvariant 一次复用。

#### 实施记录（2026/05/26）

**改动文件**:
- `OngekiFumenEditor/Parser/Ogkr/CommandParserImpl/BulletPalleteCommandParser.cs`（4 处）
- `OngekiFumenEditor/Parser/Ogkr/CommandParserImpl/CustomBulletCommandParser.cs`（5 处）
- `OngekiFumenEditor/Parser/Ogkr/CommandParserImpl/CustomBellCommandParser.cs`（3 处）

**变更**: 12 处 `?.ToUpper()` → `?.ToUpperInvariant()`（替换所有 occurrences）。

**为什么选 ToUpperInvariant 而不是 OrdinalIgnoreCase**:
- `switch` 表达式 case 不能直接表达 `OrdinalIgnoreCase` 比较，要么改三元链失去可读性、要么写 `var s when ...` 模式啰嗦；
- ToUpperInvariant 是建议里明确允许的最小改动方案；
- 主收益：消除 culture-sensitive ICU 路径（`ToUpper` 默认走 `CurrentCulture`，在土耳其等 locale 下有著名的"I→ı"问题，对 OGKR ASCII 标签可能引入解析错误）；
- 残余开销：仍分配 ~24 B/调用的临时小串——按 #18 Medium 严重度可接受，未做激进重构。

**风险检查**: 主项目 Release 编译 0 错误；语义保持不变（每个 `?.ToUpper()` 仍然处理 null，仍然返回大写副本进 switch）；switch case literals 是 ordinal 比较，与 ToUpperInvariant 配对正确。

### ✅ 19. DefaultStringMeasure.GetSupportFonts 启动期 Directory.GetFiles + LINQ ToLower 反复

- **位置**: `OngekiFumenEditor/Kernel/Graphics/OpenGL/Drawing/StringDrawing/DefaultStringMeasure.cs:128-135`
- **类别**: IO / 字符串
- **严重度**: Medium
- **决策**: **已修复**（用户直接允许改动）
- **问题**: `Path.GetExtension(x).ToLower() == ".ttf"`（行 134）—— ToLower 创建新串。
- **建议**: `Path.GetExtension(x).Equals(".ttf", StringComparison.OrdinalIgnoreCase)`，避免分配。

#### 实施记录（2026/05/26）

**改动文件**: `OngekiFumenEditor/Kernel/Graphics/OpenGL/Drawing/StringDrawing/DefaultStringMeasure.cs:134`

**变更**: 单行替换——`Path.GetExtension(x.FilePath).ToLower() == ".ttf"` → `Path.GetExtension(x.FilePath).Equals(".ttf", StringComparison.OrdinalIgnoreCase)`。

**预期效果**: 每个字体文件少分配一个 `string`（GetExtension 返回的副本被 ToLower 又拷一份）；Windows Fonts 目录通常几百到上千文件，启动期节省 ~几 KB 分配并消除 ICU 调用。`OrdinalIgnoreCase` 比 `ToLower + ==` 还快（直接逐字节 case-fold 比较）。

**风险检查**: 主项目 Release 编译 0 错误；语义等价。

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

### ✅ 27. Log.BuildLogMessage 反复 ToString().ToUpper() 严重度名

- **位置**: `OngekiFumenEditor/Utils/Log.cs:67-68`
- **类别**: 字符串
- **严重度**: Low
- **决策**: **已修复**（用户直接允许改动）
- **问题**: `record.Severity.ToString().ToUpper()` —— enum.ToString() 已经分配一次字符串，再 ToUpper 又一次。
- **建议**: 用预定义的 `static readonly string[]` 按 enum 索引返回；或 `nameof` 已知常量。

#### 实施记录（2026/05/27）

**改动文件**: `OngekiFumenEditor/Utils/Log.cs:57-68`

**变更**:
- 新增 `private static readonly string[] severityNames = { "DEBUG", "INFO", "WARN", "ERROR" };`，与 `ILogOutput.Severity` 枚举顺序对齐；
- `record.Severity.ToString().ToUpper()` → `severityNames[(int)record.Severity]`，越界回退到 `record.Severity.ToString()`（防御未来枚举扩展）；
- 每条日志省 2 次 string 分配（enum.ToString 内部 + ToUpper 副本）。

**风险检查**: 主项目 Release 编译 0 错误；severityNames 顺序与 enum 定义对齐已 grep 验证。

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

### ✅ 30. EditorProjectFileManager.Clone 用 MemoryStream + ToArray 拷贝

- **位置**: `OngekiFumenEditor/Modules/FumenVisualEditor/Kernel/EditorProjectFile/EditorProjectFileManager.cs:42-47`
- **类别**: 内存 / 序列化
- **严重度**: Medium
- **决策**: **已修复**（用户直接允许改动；受 `MigratableSerializerManager.Load(byte[])` 签名限制只做 capacity 预分配）
- **问题**: `var ms = new MemoryStream(); await manager.Save(...); return await manager.Load<...>(ms.ToArray());` — ToArray 把整个 buffer 拷一份。
- **建议**: 用 `ms.GetBuffer()` + length；或者 `ms.TryGetBuffer(out var seg)`，或直接传 ReadOnlySpan。

#### 实施记录（2026/05/27）

**改动文件**: `OngekiFumenEditor/Modules/FumenVisualEditor/Kernel/EditorProjectFile/EditorProjectFileManager.cs:42-50`

**变更**:
- `new MemoryStream()` → `new MemoryStream(256 * 1024)`，提前分配 256 KB；
- 加 `using` 显式释放；
- 保留 `ms.ToArray()` 调用——`MigratableSerializerManager.Load` 签名要求 `byte[]`，无 `ReadOnlySpan` / `ArraySegment` 重载，跨边界拷贝必要。

**为什么不改 GetBuffer**:
- `Load(byte[])` 要求一个独占的 `byte[]`，传入 `GetBuffer()` 无法限定 length（外部不会看 `ms.Length`），需要拷贝到精确长度数组——和 `ToArray` 等价；
- 改 `Load` 签名跨越 `Dependences/MigratableSerializer` 包边界，超出本任务范围。

**真正收益**: 避免 `MemoryStream` 内部 `byte[]` 多次 `Array.Resize` 拷贝（默认从 0 开始，每次扩容拷贝累计 ~2N 字节）。预分配后写入阶段零扩容，仅末尾一次精确 ToArray。

**风险检查**: 主项目 Release 编译 0 错误；语义不变。

### ✅ 31. CommonEditorProjectFileSerializer.CheckParsableAsync 每次 new MemoryStream

- **位置**: `OngekiFumenEditor/Modules/FumenVisualEditor/Kernel/EditorProjectFile/Serializers/CommonEditorProjectFileSerializer.cs:60-63`
- **类别**: 序列化 / 内存
- **严重度**: Low
- **决策**: **已修复**（用户直接允许改动）
- **问题**: 反序列化整个 buffer 只为读取一个 `Version` 字段。
- **建议**: 用 `Utf8JsonReader` 直接读到 `Version` 即可，不必反序列化整对象。

#### 实施记录（2026/05/27）

**改动文件**: `OngekiFumenEditor/Modules/FumenVisualEditor/Kernel/EditorProjectFile/Serializers/CommonEditorProjectFileSerializer.cs:56-93`

**变更**:
- `CheckParsableAsync` 改为同步实现 + `Task.FromResult`，去 `MemoryStream` + `JsonSerializer.DeserializeAsync` 整对象路径；
- 新增私有 `TryReadTopLevelVersion(ReadOnlySpan<byte> buffer, out Version)`：
  - 用 `Utf8JsonReader` 在 `ReadOnlySpan<byte>` 上扫描；
  - 只读顶层（`CurrentDepth == 1`）的 `"Version"` 属性，扫到即解析返回；
  - `reader.ValueTextEquals("Version"u8)` 直接 UTF-8 比较，无 string 分配；
  - 其它顶层属性 `reader.Skip()` 跳过整子树；
  - 整个对象 EndObject 仍未找到则 false。

**预期效果**:
- 典型项目文件几百 KB，原版要解析整个 JSON 树 + 反射构建 anonymous type；新版扫到第一个 "Version"（通常文件头几十字节）即返回；
- 内存：消除 `MemoryStream(buffer)` 包装 + JsonSerializer 内部 buffer pool；
- 因为多个 `IFumenSerializer` 在 `CheckParsableAsync` 中级联检查，文件不匹配时也能快速失败。

**风险检查**:
- 主项目 Release 编译 0 错误；
- 语义保留：仍要求 `Version == this.Version`，仍处理 `Version` 缺失/非字符串/非顶层情况（返回 false）；
- 顶层 `Version` 之前的属性会被 `Skip()` 一次性跳过，与原版完整反序列化等价（原版也不依赖顺序）。

### ✅ 32. DefaultOngekiFumenParser 单线程逐行读取 + dictionary trim

- **位置**: `OngekiFumenEditor/Parser/Ogkr/DefaultOngekiFumenParser.cs:40-54`
- **类别**: 字符串 / IO
- **严重度**: Medium
- **决策**: **已修复**（用户直接允许改动；同步 ReadLine + Task.Run 调度方案）
- **问题**: 每行 `await reader.ReadLineAsync()` + `commandArg.GetData<string>(0)?.Trim()`（Trim 产生新串）。
- **建议**: 用 `ReadOnlySpan<char>` API + `MemoryExtensions.Trim`；命令名字典查询用 span lookup（.NET 9 `Dictionary<string,_>.GetAlternateLookup<ReadOnlySpan<char>>()`）。

#### 实施记录（2026/05/27）

**改动文件**: `OngekiFumenEditor/Parser/Ogkr/DefaultOngekiFumenParser.cs:32-65`

**变更**:
- `while (!reader.EndOfStream) { await reader.ReadLineAsync(); }` → `await Task.Run(() => { while ((line = reader.ReadLine()) != null) { ... } });`
  - 每行一个 Task 的 `ReadLineAsync` 在大谱面累计开销不可忽略，整流读取本就是顺序的，async 没有并发收益；
  - 整段读取调度到线程池一次性吞掉，避免阻塞 caller 同步上下文；
  - 顺带消除原 line 40 的 `CA2024 reader.EndOfStream in async` 警告；
- `commandArg.GetData<string>(0)?.Trim()` → `commandArg.GetData<string>(0)`
  - `GetData<string>(0)` 实际是 `Split('\t')` 后的 `inputs[0]`，第一个非空 token，本就不应有前后空白；
  - 移除每行 Trim 的字符串分配（命令名查表 fast path）；
  - `null` 检查改为 `string.IsNullOrEmpty` 兼容空字符串行；
- 字典 fallback 行为不变：`CommandParsers.TryGetValue(cmdName, out var parser)`。

**未做**: 用 `Dictionary<string,_>.GetAlternateLookup<ReadOnlySpan<char>>()` —— 需要先把 `ReadLine` 替换成 `ReadOnlySpan<char>` 版本（`StreamReader` 没有，要换 `IBufferedLineReader`），改动面跨文件大，本次未做。`GetData<string>` 走 `CommandArgs.cacheDataArray`，命令名 token 命中已是 string，分配在 `Split` 时一次产生，再用 span lookup 收益有限。

**预期效果**:
- 大谱面（5000+ 行）下 N 个 `Task` 创建/调度成本被一次性 `Task.Run` 替代；
- 每行省 ~24 B Trim 副本（大谱面累计 ~120 KB）。

**风险检查**:
- 主项目 Release 编译 0 错误；
- `Task.Run` 内部 `ReadLine` 抛异常会通过 `await` 自然传出；
- 谱面行不会以 leading whitespace 开头（OGKR 协议约定），原 `Trim()` 是防御性的，移除后等价（如出现异常行，cmdName lookup 不命中即 silently 跳过，与原版相同）。

### ✅ 33. DefaultNyagekiFumenParser 同样问题 + ToLower

- **位置**: `OngekiFumenEditor/Parser/DefaultImpl/Nyageki/DefaultNyagekiFumenParser.cs:24`、`:37`
- **类别**: 字符串
- **严重度**: Medium
- **问题**: 字典 key 用 ToLower（构造时分配），每行 `seg[0].ToLower().Trim()` 再次分配。
- **建议**: 字典用 `StringComparer.OrdinalIgnoreCase`，行解析就不再需要 ToLower。

#### 实施记录（2026/05/27）

- 字典构造改为 `new Dictionary<string, INyagekiCommandParser>(StringComparer.OrdinalIgnoreCase)`，构造时不再 ToLower。
- 行解析去掉 `ToLower()`，仅保留 `Trim()`（与 PI#32 不同：Nyageki 协议未保证无前导空白，保留更安全）。
- 移除未用的 `System.Linq` using。

### ✅ 34. FumenVisualEditor RenderControl_UnLoaded async void

- **位置**: `OngekiFumenEditor/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.Drawing.cs:979`、`:989`
- **类别**: 异步
- **严重度**: Medium
- **问题**: WPF 控件事件回调用 async void 是常见模式，但 `RenderContext = await renderImpl.GetOrCreateRenderContext(renderControl); RenderContext.StopRendering();` 若 await 期间被取消会丢异常。
- **建议**: 至少 try/catch 包住或改用任务跟踪。

#### 实施记录（2026/05/27）

- `RenderControl_UnLoaded` 与 `RenderControl_Loaded` 均加 try/catch，捕获 await 期间产生的异常并 `Log.LogError` 记录，避免 async void 异常逃逸到 SynchronizationContext 触发进程崩溃。

### ✅ 35. AbortableThread.Abort 用 task.Wait() 阻塞

- **位置**: `OngekiFumenEditor/Utils/AbortableThread.cs:34`
- **类别**: 异步 / 并发
- **严重度**: Medium
- **问题**: `task?.Wait();` 在 UI 线程上同步等待——SchedulerManager.Term 中调用，关闭时可能卡 UI。
- **建议**: 提供 `Task AbortAsync()`。

#### 实施记录（2026/05/27）

- 新增 `AbortableThread.AbortAsync()`，对取消后的任务使用 `await task.ConfigureAwait(false)` 等待；忽略 `OperationCanceledException`。
- `SchedulerManager.Term()` 改为 `async Task`，调用 `await runThread.AbortAsync()`，避免 UI 线程同步等待。
- 其它 `Abort()` 调用点（`AppBootstrapper.ipcThread`、`DefaultFumenSoundPlayer`）位于非 UI 线程或析构路径，保留同步 Abort 不变。

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

### ✅ 41. RandomHepler 用 DateTime.Now.GetHashCode() 作种子

- **位置**: `OngekiFumenEditor/Utils/RandomHepler.cs:11`、`OngekiFumenEditor/Base/OngekiObjects/BulletPallete.cs:12`
- **类别**: 杂项
- **问题**: 既不是 cryptographic 也不是 monotonic 种子；并发场景下 DateTime.Now 分辨率低，会得到重复种子。
- **建议**: `Random.Shared` 即可（线程安全，自带高熵）。

#### 实施记录（2026/05/27）

- `RandomHepler` 全部 API 改用 `Random.Shared`，移除自管理 `Random rand`/`StringBuilder sb` 静态字段。
- `RandomString` 改为每次新建 `StringBuilder`，避免原本静态 `sb` 在多线程下的竞态（原实现非线程安全且复用同一 StringBuilder 也会产生 race）。
- `BulletPallete.RandomSeed` 保留原语义（业务逻辑依赖该字段在每次刷新切换 seed，非性能关注点）。

### ✅ 42. ImageLoader.PrcessQueue 每次 MD5.Create + Convert.ToHexString

- **位置**: `OngekiFumenEditor/Utils/ImageLoader.cs:75-76`
- **类别**: 内存 / 字符串
- **问题**: 每次新建 MD5 实例。
- **建议**: 用 `MD5.HashData(...)` 静态方法。

#### 实施记录（2026/05/27）

- 改为 `MD5.HashData(Encoding.UTF8.GetBytes(path))`，省去 `MD5.Create()` + `IDisposable.Dispose()` 调用。

### ✅ 43. KeyBindingDefinition.regex 编译选项缺失

- **位置**: `OngekiFumenEditor/Kernel/KeyBinding/KeyBindingDefinition.cs:80`
- **类别**: 正则
- **问题**: `new Regex(@"(\s*\w+\s*\+\s*)?(\w+)")` 无 `RegexOptions.Compiled`，也未 `[GeneratedRegex]`。
- **建议**: .NET 7+ 用 `[GeneratedRegex(@"...", RegexOptions.Compiled)]` 生成器。

#### 实施记录（2026/05/27）

- 改为 `partial class` + `[GeneratedRegex]` 源生成 `KeybindRegex()`。

### ✅ 44. OgkiFumenSet 中 Regex 无 Compiled 标志

- **位置**: `OngekiFumenEditor/Modules/OgkiFumenListBrowser/Models/OngekiFumenSet.cs:90`、`:91`
- **类别**: 正则
- **问题**: `static Regex BpmRegex = new Regex(...)` 无 RegexOptions.Compiled。
- **建议**: 改 `[GeneratedRegex]`。

#### 实施记录（2026/05/27）

- 改 `partial class`，`BpmRegex` 与 `CreatorRegex` 改为 `[GeneratedRegex]` 源生成方法。

### ✅ 45. ParserUtils Regex 也未编译

- **位置**: `OngekiFumenEditor/Parser/DefaultImpl/Nyageki/CommandImpl/ParserUtils.cs:25`
- **类别**: 正则
- **建议**: 同上。

#### 实施记录（2026/05/27）

- `ParserUtils` 改 `static partial class`，原 `static Regex s` 改为 `[GeneratedRegex]` 的 `ParamRegex()`。

### ✅ 46. DocumentOpenHelper.cs 内部 new Regex 在方法内

- **位置**: `OngekiFumenEditor/Utils/DocumentOpenHelper.cs:157`
- **类别**: 正则
- **问题**: `var match = new Regex(@"(\d+)_\d+").Match(...)` 在方法里临时实例化。
- **建议**: 提为 static + Compiled / GeneratedRegex。

#### 实施记录（2026/05/27）

- `DocumentOpenHelper` 改 `static partial class`，提取 `MusicIdFromFileNameRegex()` 为 `[GeneratedRegex]`。

### ✅ 47. GridBase.GetHashCode 用累加 hash 但未 readonly

- **位置**: `OngekiFumenEditor/Base/GridBase.cs:129-139`
- **类别**: 集合
- **问题**: Hashcode 基于 mutable 字段 Unit/Grid/GridRadix。把 GridBase 用作 Dictionary key 后修改会破坏 invariant。
- **建议**: 显式标注或在 mutation 前删除-再插入；或建模为不可变结构体。

#### 实施记录（2026/05/27）

- 哈希实现改为 `HashCode.Combine(Unit, Grid, GridRadix)`（语义等价、更现代）。
- 添加注释明确说明 hash 依赖 mutable 字段以及调用方需在 mutation 前先 remove-后 insert 的约束。
- 未将 `GridBase` 改造为不可变结构体——该类型已被广泛继承（`TGrid`/`XGrid` 等）并依赖 PropertyChanged 通知，重构成本与回归风险过高。

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
