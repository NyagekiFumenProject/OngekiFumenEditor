# OngekiFumenEditor.Benchmark

测量 OngekiFumenEditor 中与 UI 无关的性能热点。基于 BenchmarkDotNet,通过 `new App(false) + new AppBootstrapper(false)` 在不打开窗口的前提下激活 MEF/IoC 容器,然后调用主项目的纯计算入口。

## 测量类

运行时自动发现程序集中所有带 `[Benchmark]` 方法的公开类,菜单编号按类名排序。当前包含:

| 类 | 覆盖范围 |
| --- | --- |
| `CollectionQueryBenchmarks` | 集合范围查询:`BinaryFindRange` / `GetVisibleStartObjects` / Soflan 区间查询 |
| `LaneBoundaryXGridQueryBenchmarks` | 渲染期边界求值分配:旧 `CalulateXGrid`(List+XGrid) vs 新索引区间+TotalUnit 数值返回,含逐点等价性校验 |
| `BulletBellQueryBenchmarks` | 预览模式子弹/Bell 分桶查询(性能报告 §8 P2 落地验证):旧 `BinaryFindRange(yield)` + `Where` 逐 target 重枚举 vs 已落地的索引区间 + 直写循环;另测「帧内列表复用」「group 可见性缓存」两种未采纳形态,含逐元素等价性校验 |
| `DisplayableEnumerationBenchmarks` | `OngekiFumen.GetAllDisplayableObjects()`、范围内枚举、`ConnectableStartObject.GetDisplayableObjects()` |
| `GridNotificationAllocationBenchmarks` | 网格属性通知分配:Caliburn 闭包/args 逐次分配 vs `CommonPropertyChangedBase` 的 args 复用 + 变更检测(含旧实现逐行复刻对照与等价性校验) |
| `HoldJudgeTickEnumerationBenchmarks` | 预览期 Hold 判定刻度枚举(性能报告 §8 P2 落地验证):旧「每步 GridOffset(24 B)+TGrid(56 B) 双分配」vs 新「私有累计实例原地推进 + yield 复制」,含逐步位级等价性校验 |
| `PlayfieldAreaSamplingAllocationBenchmarks` | 可击打区域采样链路(性能报告 §8 P2 落地验证):旧「每采样 2×TGrid 物化 + 无效路径 List/XGrid」vs 新「复用 sampleTGrid + 索引扫描/TryCalulateXGridTotalUnit」,含位级等价对拍与反射直调已落地私有方法 |
| `DrawPlayableAreaHelperNewP1BoundaryBenchmarks` | new playfield helper P1: boundary 查询重复、候选墙轨缓存、LINQ/数组分配 |
| `DrawPlayableAreaHelperNewP1SampleCollectionBenchmarks` | new playfield helper P1: 墙轨节点采样收集的全谱扫描与索引查询 |
| `LaneCurvePathBenchmarks` | Lane 曲线路径生成:多次 LINQ 枚举 vs 单次扫描 + PooledList(独立 DTO 模拟) |
| `LaneEndPointsCollectBenchmarks` | 收集 lane 起点 Y / 末 child Y 的 LINQ vs for 循环写法 |
| `ParserUtilsBenchmarks` | `SplitEmptyChar` 解析链:TypeConverter 缓存 / 去 LINQ / `int` 快路径 |
| `ParsingBenchmarks` | OGKR / Nyageki / 全部样本的反序列化 |
| `PolylineIntersectScanBenchmarks` | 折线相交扫描:暴力 / 现状剪枝 / 规范化剪枝 / sweep line |
| `PostDrawBatchingBenchmarks` | `SKCanvas` 批量提交折线(真实 Skia 渲染):`DrawPoints` vs `SKPath.AddPoly` |
| `PropertyChangedSubscriptionLeakBenchmarks` | 属性变更订阅泄漏:现状 / 退订 / WeakEvent |
| `RebuildSoflanGroupBenchmarks` | Soflan 分组重建:LINQ + `Parallel.ForEach` vs PooledList + Partitioner |
| `SkPathReuseBenchmarks` | `SKPath` 每次 new/Dispose vs 成员复用 / 池化 |
| `SkiaLineDrawingBenchmarks` | Skia 线段绘制:旧逐段实现 vs 池化 + Run 合并 + dash 缓存 |
| `SortByVsSortComparisonBenchmarks` | `SortBy` 扩展 vs `List`/`Array.Sort` 静态比较器 |

样本数据(`Data/FumenSamples/*.ogkr|*.nyageki`)以嵌入资源形式打进程序集,运行时不依赖磁盘上的样本目录。

## 运行

```powershell
dotnet run -c Release --project .\OngekiFumenEditor.Benchmark
```

程序启动后会列出所有 Benchmark 类,要求用户输入编号选择:

```
请选择要测量的 Benchmark 类:
  [0] 全部
  [1] CollectionQueryBenchmarks
  [2] DisplayableEnumerationBenchmarks
  [3] DrawPlayableAreaHelperNewP1BoundaryBenchmarks
  ...
  [15] SortByVsSortComparisonBenchmarks
输入编号(多个用逗号分隔,直接回车 = 全部):
```

也可以传 `--filter` 走 BenchmarkSwitcher CLI 路径跳过交互菜单:

```powershell
dotnet run -c Release --project .\OngekiFumenEditor.Benchmark -- --filter *ParsingBenchmarks*
```

加速试跑(`Dry` job 单次迭代,适合冒烟):

```powershell
dotnet run -c Release --project .\OngekiFumenEditor.Benchmark -- --job dry
```

支持的 `--job` 预设:`dry` / `short` / `medium` / `long` / `verylong` / `default`。

## 目标框架与 .NET 10 → 11 对比

项目 TFM 为 `net11.0-windows`（2026-11-15 由 `net10.0-windows` 升级）。`Program.cs` 不硬编码 TFM：它读取 csproj 注入的 `TargetFrameworkMoniker` 程序集元数据来构造 BDN toolchain，后续再升级目标框架不必改代码。

同一台机器上比较两个运行时的方法：

```powershell
# A 侧：TFM 仍是 net10.0-windows 时跑
dotnet run -c Release --project .\OngekiFumenEditor.Benchmark -- --filter "*GridNotificationAllocationBenchmarks*" --job short
# 把 BenchmarkDotNet.Artifacts/results/ 归档为 results-net10/

# B 侧：TFM 改成 net11.0-windows 后跑同一组 filter，归档为 results-net11/
```

两份 CSV 用 `OngekiFumenEditor.Benchmark/Tools/compare-tfm.py` 对齐比较（按类 / 方法 / 参数对齐，输出几何平均比、CV≤15% 的高置信子集、分配归零用例数）。

```powershell
python .\OngekiFumenEditor.Benchmark\Tools\compare-tfm.py .\BenchmarkDotNet.Artifacts\results-net10 .\BenchmarkDotNet.Artifacts\results-net11
```

实测结论（15 个自包含类、357 个对齐用例、ShortRun、同机同 SDK）：net11/net10 的 Mean 几何平均比为 **0.9546**（高置信子集 0.9530），分配几何平均比 0.9951，另有 59 个用例的分配量从非零降到 0 B。

## 已知限制

### 继承 `FumenBenchmarkBase` 的用例在当前环境跑不起来（既有问题，与具体用例无关）

BDN 在子进程里以 `dotnet <generated>.dll` 启动，于是 `Environment.ProcessPath` 指向
`C:\Program Files\dotnet\dotnet.exe`，而主项目有不少路径是相对「可执行文件目录」解析的
（`OngekiFumenEditor/Utils/AppDirectoryHelper.cs:6`）：

| 位置 | 行为 |
| --- | --- |
| `Utils/DeadHandler/DumpFileHelper.cs:42` 创建 `.\Dumps` | bootstrap 阶段抛 `UnauthorizedAccessException`（`AppBootstrapper.Configure()`） |
| `AppBootstrapper.cs:124` 创建 `Plugins\` | 同上 |

表现：子进程以 `TargetInvocationException → UnauthorizedAccessException` 退出，结果表所有方法为 `NA`，
并提示 `Detected error exit code from one of the benchmarks`。

- **受影响**：所有继承 `FumenBenchmarkBase` 的类（`CollectionQueryBenchmarks`、`DisplayableEnumerationBenchmarks`、
  `DrawPlayableAreaHelperNewP1*` 等——它们都要经 `BenchmarkRuntime.EnsureInitialized()` 引导 IoC）。
- **不受影响**：自包含用例（`LaneBoundaryXGridQueryBenchmarks`、`GridNotificationAllocationBenchmarks`、
  `LaneCurvePathBenchmarks` 等），它们不触碰引导流程。
- **规避**：新用例写成自包含（自建数据，不调用 `BenchmarkRuntime`）。
- **修复方向**（均属主项目改动，二选一）：
  1. `AppDirectoryHelper.ExecutableDirectory` 优先取 `AppContext.BaseDirectory`（single-file 发布下与现在等价，
     `dotnet app.dll` 宿主下指向 dll 所在目录，正是期望值）；
  2. `DumpFileHelper.Init()` 与 `Plugins` 目录创建失败时降级为告警而非抛出。

在修复之前，不要以「全部可跑」为前提用 `[0] 全部`。

### Skia 用例的 native 加载陷阱（既有问题，与 TFM 无关）

`SkiaLineDrawingBenchmarks` / `SkPathReuseBenchmarks` / `PostDrawBatchingBenchmarks` 的类内 `[Config]` 强制 `InProcessNoEmitToolchain`，原因是 BDN 的 `HintPathRewrite` wrapper 只复制 host bin 根目录的托管 dll，不复制 `runtimes/<rid>/native/`：子进程因此找不到 `libSkiaSharp.dll`，回退到进程 PATH 搜索。若 PATH 上存在旧版本（本机实测命中 `C:\Program Files\ProxyBridge\libSkiaSharp.dll`，native 版本 88.1），就会以

```
The version of the native libSkiaSharp library (88.1) is incompatible with this version of SkiaSharp.
Supported versions of the native libSkiaSharp library are in the range [119.0, 120.0).
```

失败，该 job 的所有用例记为 `NA`。

另外 `Program.cs` 的全局 `AddJob` 会和类内 `[Config]` 合并，于是这些类会各跑两份 job：`HintPathRewrite` 那份在当前环境恒为 `NA`（net10 时同样如此），有效数据只来自 `InProcessNoEmitToolchain`。跑这类用例时逐类单独 `--filter`（不要一次筛多个 Skia 类），可避免 InProcess 编译链连续工作时的退化。

## 基线对比

每跑完一组 benchmark,程序会:

1. 从 `BenchmarkDotNet.Artifacts/Baselines/{ClassFullName}.json` 加载上次保存的基线
2. 与当次结果做差异对比,输出 Mean / Allocated / Δ% 表格,状态 `OK` / `REGRESSION` / `IMPROVED` / `NEW` / `GONE`
3. 询问 `Save current results as new baseline(s)? [y/N]`
   - `y`/`yes`:把当前结果写回同一个 JSON 文件(覆写)
   - 其它:跳过保存
   - stdin 被重定向(管道、CI)时自动跳过保存

默认判定阈值:Mean ±5%,Allocated bytes ±1%。

文件名严格按 plan.md 第 5 条要求,以**类全名**为基础,例如:

```
BenchmarkDotNet.Artifacts/
└── Baselines/
    ├── OngekiFumenEditor.Benchmark.Benchmarks.ParsingBenchmarks.json
    ├── OngekiFumenEditor.Benchmark.Benchmarks.DisplayableEnumerationBenchmarks.json
    └── OngekiFumenEditor.Benchmark.Benchmarks.CollectionQueryBenchmarks.json
```

颜色输出走 ANSI 转义,stdout 被重定向或环境变量 `NO_COLOR` 非空时自动关闭。

## 构建

```powershell
dotnet build .\OngekiFumenEditor.Benchmark\OngekiFumenEditor.Benchmark.csproj -c Release
```

注意:`csproj` 引用主项目时显式 `AdditionalProperties="DisableFody=true"`,绕开 Fody Costura 与 BenchmarkDotNet 工具链的冲突。`Program.cs` 通过自定义 `HintPathRewriteToolchain`(包装 BDN 默认 `CsProjCoreToolchain`)把 wrapper 工程里的 ProjectReference 重写为指向已构建主 Benchmark bin 目录的 `Reference` HintPath,避免 wrapper 重复构建 `Caliburn.Micro.Core` / `OngekiFumenEditor` 触发 CSC CS2012 文件锁,同时保留子进程隔离的测量精度(不再走 `InProcessEmit`)。前提是主 Benchmark dll 已 build 过一次;`dotnet run` 默认先 build,无需手动。
