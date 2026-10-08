# 谱面加载/解析性能审查（WPF 树）

- 日期：2026-10-08
- 对象：`OngekiFumenEditor/`（WPF 树）从"用户点开文件"到"编辑器可用"的整条加载链
- 本文档为**审查与优化清单**，尚未实施；实施后请按仓库约定回写 `Avalonia/WPF-MIGRATION-SYNC.md`

## 范围与方法

- 三条只读审查线：**解析文本热路径**（`Parser/Ogkr/**`）、**对象进入模型**（`Base/OngekiFumen.cs`、`Base/Collections/**`、对象基类）、**加载编排**（`Modules/FumenVisualEditor/**`、`Utils/DocumentOpenHelper.cs`、音频与波形）。
- 下列全部条目均经复读源码核验（少数标注"推测"）；已排除已有优化（见文末），不重复建议。
- 仅覆盖 WPF 树的 ogkr 路径。Nyageki 解析路径未逐条核（同型模式，未见额外问题）；Avalonia 树结构不同（例如音频走 `INAudioFileReaderFactory`），不在本次范围。

## 实测基线

真实官方谱面，取自编辑器自身的加载分步日志：

| 谱面 | 解析阶段 | 解析前（音频定位 + 整曲解码） | 打开全程 |
|---|---|---|---|
| `0836_03.ogkr` | **0.61 s** | 1.58 s | 2.61 s |
| `1192_03.ogkr` | **2.08 s** | 2.85 s | 5.38 s |

历史日志中 129 次加载的解析步多在 **0.4–2 s**，偶发 3–18 s（首次 JIT / 超大谱）。

结论：**解析与"音频重复解码"是打开耗时的两大块**，且解析路径存在多处 O(n²) 与重复切分。

测量方式说明：仓库自带 `OngekiFumenEditor.Benchmark`（BenchmarkDotNet）在本机因杀软杀子进程跑不出数；单独写的 in-process 探针在无头环境挂死。故本文以**代码机制核验 + 上表日志实测**为准，未给全语料总量。

## 优化点清单

### A. 解析文本热路径

1. **每行按目标类型被整行 Split 3~4 次** — `Parser/Ogkr/CommandArgs.cs:48`（`cacheDataArray` 按 `T` 缓存结果，但切分本身不缓存）→ 每行多付 1~3 次 `Trim()` 拷贝 + `string[]` + 全部子串。修法：按 `Line` 缓存一份 `string[]` 供所有 `T` 复用。**收益：大｜风险：低｜依据：已核验**
2. **数值转换全走 LINQ，Span+InvariantCulture 快速路径是死代码** — `IArgValueConverter` 的 6 个 Export（string/float/double/bool/int/long）使 `CommandArgs.cs:51-53` 的转换器分支恒命中，永远走 `inputs.Select(...).OfType<T>().ToArray()`（2 个迭代器 + 2 个数组，且未指定 InvariantCulture）；`CommandArgs.cs:57-90` 里现成的 Span 快速实现永不执行。**收益：中~大｜风险：中**（涉及扩展点保留与区域解析行为变更）
3. **按 RecordId 找 lane = IntervalTree 枚举 + 整表快照拷贝 + O(N) 扫描** — `Parser/Ogkr/CommandParserImpl/TapCommandParser.cs:19`，同型：`HoldCommandParser.cs:19`、`LaneCommandParser.cs:72`、`WallCommandParser.cs:43`、`LaneBlockCommandParser.cs:21`、`BeamCommandParser.cs:86`、`Editor/CurveControlCommand.cs:21-26`、`CurvePrecisionCommand.cs:20-25`；`Base/Collections/Base/RangeTree/IntervalTree.cs:55-66` 每次枚举都在锁内 `staging.ToArray()` 全量复制。每个 TAP/HLD 一次 → O(物件 × lane)。修法：维护 `Dictionary<int, LaneStartBase>`（含 BeamStart）索引并保持同步。**收益：大｜风险：低｜依据：已核验**
4. **Bullet/Bell 逐个线性扫描 BulletPalleteList** — `BulletCommandParser.cs:22`、`BellCommandParser.cs:25`，而 `Base/Collections/BulletPalleteList.cs:55` 已有 O(1) 字符串索引器（注意"找不到返回 null"语义与非法 id）。**收益：中｜风险：低｜依据：已核验**
5. **SvgPrefab 行 4~5 次整行切分（行内含 Base64 大字段），且多次解码 Base64** — `CommandParserImpl/Editor/SvgPrefabCommand.cs:26-39,56,73-77`；随第 1 条修复即可，进一步可只解码一次。**收益：中（视预置物数量）｜风险：低｜依据：已核验**
6. **`genObjList` 为每个对象保存 (obj, parser) 元组，只为跑一遍 `AfterParse`；全仓无任何覆写** — `Parser/Ogkr/DefaultOngekiFumenParser.cs:36,61,70-73`；以 `override void AfterParse` 全仓 grep 为 **0 命中**，该遍历当前是纯 GC 开销。**收益：小~中｜风险：中**（公开扩展点，建议改为按需收集而非删接口）
7. **热路径用 `Enum.Parse`** — `SoflanCommandParser.cs:65`（Easing）、`Editor/SvgPrefabCommand.cs:76`（FlowDirection）→ 改静态字典或 switch。**收益：小｜风险：低｜依据：已核验**
8. **枚举字段先 `ToUpperInvariant()` 再 switch** — `CustomBulletCommandParser.cs:26,35,46,56,64`、`CustomBellCommandParser.cs:27,38,48`、`BulletCommandParser.cs:27`、`EnemySetCommandParser.cs:22` → 改序数忽略大小写比较。**收益：小｜风险：低｜依据：已核验**
9. **颜色 Id 线性扫描 `AllColors`** — `LaneCommandParser.cs:43`、`Editor/SvgPrefabCommand.cs:40`（20 项 + 盒装枚举器）→ 静态字典或按 id 索引。**收益：小｜风险：低｜依据：已核验**

### B. 对象进入模型（每个物件都走）

10. **`IntervalTree.Add` 的线性去重** — `IntervalTree.cs:124`：每次 Add 在 `writeGate` 内 `staging.Any(x => x.Value.Equals(value))`；Holds/Soflans/LaneBlocks/Lanes 等全走此路 → 累计 O(n²)。修法：改 HashSet 去重，或仅在 DEBUG 保留校验。**收益：大｜风险：中｜依据：已核验**
11. **排序集合逐条 `List.Insert`（O(n) 搬移），解析期从不批处理** — `Base/Collections/Base/SortableCollection.cs:42-45`；`BeginBatchAction/EndBatchAction`（`:63-72`）经全仓 grep 确认**仅在音频峰值计算中使用**（`Kernel/Audio/PeakPointCollection.cs:39/79`、`DefaultSamplePeak.cs:28/46`）；解析期开 batch（`items.Add` + 末尾一次 `Sort`）即降到 O(n log n)。**收益：大~中｜风险：低｜依据：已核验**
12. **轨道子段插入的 `children.Contains` 线性扫描** — `Base/OngekiObjects/ConnectableObject/ConnectableStartObject.cs:170`；长曲线轨道逐段插入 → 单条 lane O(N²)。**收益：中｜风险：中**（需保留防重复语义）
13. **每物件 ≥2 路 PropertyChanged 订阅（Soflan 达 4 路）** — `Base/OngekiFumen.cs:259-262` + `RemindableSortableCollection.cs:32`、`IntervalTreeWrapper.cs:40`、`SoflanList.cs:54`、`SoflanListMap.cs:51`；每次 `+=` 分配委托。**收益：中｜风险：中｜依据：已核验**
14. **`TGrid` setter 每次分配闭包并读写全局静态 `ConcurrentDictionary`** — `Base/OngekiTimelineObjectBase.cs:18` → `Utils/PropertyChangedBaseExtensionMethod.cs:21-38`。**收益：中｜风险：中｜依据：已核验**
15. **每个时间轴物件预分配一个 `TGrid`，随后常被解析器覆盖为垃圾** — `OngekiTimelineObjectBase.cs:10`。**收益：小~中｜风险：低**（注意 `SoflanEndIndicator` 等对 null 语义的依赖）
16. **`AddObject` 长 `is` 链（Tap 要过 10 个判断才命中）** — `OngekiFumen.cs:174-263`。**收益：小｜风险：低｜依据：已核验**
17. **`GetAllDisplayableObjects` 每次重建 16 段 Concat+Distinct 管道，无缓存** — `OngekiFumen.cs:410-427`；编辑期热点（轨道增删触发的 `ConnectableObjectDockingHelper.cs:34` 全 fumen 扫描在解析期多因 `NextObject` 为空早退，影响小）。**收益：中（编辑期）｜风险：低｜依据：已核验**

### C. 加载编排（决定"点开到可用"的墙钟）

18. **同一音频整曲解码 2 次——一次纯粹为取时长（★最优先）** — `Utils/DocumentOpenHelper.cs:279` 的 `CalcAudioDuration`（`:282-287`）调 `LoadAudioAsync` 只为拿 `Duration`，随后 `Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.cs:315` 再解一次；同型路径：列表浏览器 `OgkiFumenListBrowserViewModel.cs:309`→`:330`、新建工程 `EditorProjectSetupDialogViewModel.cs:31`、MCP `Kernel/Mcp/Tools/Editor/EditorDocumentTool.Helpers.cs:38/56`。每次都是"整曲解码 → 重采样 → 物化数十 MB PCM → 丢弃"。修法：提供只读时长入口（WAV 头 / ACB 元数据）或复用同一 player。**收益：大｜风险：中｜依据：已核验（日志侧亦有印证：解析前 1.6~2.9 s 即此项）**
19. **快速打开四段重活严格串行** — `DocumentOpenHelper.cs:171-199`：音频定位/解码 → 谱面解析 → 编辑器+GL 初始化 → 首帧，彼此几乎无依赖却顺序执行。**收益：中~大｜风险：中**（并行收益属机制推测）
20. **就绪前的阻塞式 `CollectLoadingGarbageAsync`（UI 线程全额 GC + LOH CompactOnce）** — `FumenVisualEditorViewModel.cs:325` + `EditorLoadingSession.cs:103-112`，压在对话框关闭之前；挪到 Ready 之后即可从"打开墙钟"里剔除。**收益：中｜风险：低｜依据：已核验**
21. **WASAPI latency=0 → 忙轮询线程自加载起占满约一个物理核** — `Kernel/Audio/NAudioImpl/NAudioManager.cs:115`（`new WasapiOut(AudioClientShareMode.Shared, 0)`）+ `:131` 构造即 `Play()`；默认输出类型为 Wasapi 的机器上必然命中（渲染 profile 文档 §3.3 已实测 ~44.8% CPU，并列为 P0 未做）→ 传 100–200 ms 延迟或走 WaveOut 分支。**收益：中｜风险：低**
22. **同一 Music.xml 一次打开被解析两次（XPath）** — `DocumentOpenHelper.cs:211`（`TryFormatOpenFileName`）与 `:229`（`GetAudioFilePath`）。**收益：小｜风险：低｜依据：已核验**
23. **渲染初始化（GL 控件 + 各绘制目标 Initialize + 渲染序 JSON + 反射）在 UI 线程串行且计入就绪等待** — `FumenVisualEditorViewModel.Drawing.cs:217-268`；至少把 `LoadRenderOrderVisible` 的 JSON/反射移出热路径（GL 初始化有线程亲和，勿盲目并行）。**收益：小~中｜风险：中**
24. **波形峰值每点 `new float[channels]`（约 13.8 万次）** — `Kernel/Audio/SamplePeak/DefaultSamplePeak.cs:31`、`PeakPointCollection.cs:51` → 复用缓冲或直写目标数组。**收益：小｜风险：低｜依据：已核验**
25. **列表浏览器刷新全目录枚举 + 每个 XML 单独 XPath 解析** — `OgkiFumenListBrowserViewModel.cs:201,239-253,269-288`。**收益：中｜风险：低**
26. **`package` 整包递归回退** — `DocumentOpenHelper.cs:266`；实测 A013 布局直查即命中（`option/A013/musicsource/musicsource0836`），该回退**现状不触发**，若触发代价为整包递归扫描（本机实测约 5.0 s）→ 加结果缓存/限制深度。**收益：低（现状）｜风险：低**

## 建议实施顺序

1. **#18 去掉重复整曲解码**（每次打开节省 1.6~2.9 s，最大单项）
2. **解析三件套：#3 RecordId 索引 + #1 列缓存 + #2 数值快速路径**（直接砍解析墙钟与 GC）
3. **#11 解析期批处理 + #10 线性去重**（消除隐藏 O(n²)，大谱受益最大）

其余条目可按模块顺手合并处理（例如 #5 随 #1、#7/#8/#9 可一并做一个"解析器小优化"批次）。

## 已做到位（不要重复建议）

- 惰性 `IntervalTree`：写侧只 append + 版本号，查询侧单飞重建（`IntervalTree.cs:105-131`），并提供零分配 `QueryInRangeInto` 系列。
- `BpmList`/`MeterChangeList`/`SoflanList` 用内容版本令牌替代内容哈希。
- `SortableCollection` 二分插入与批处理 API **均已实现**（只是解析期未使用，见 #11）。
- 命令分发是启动期 MEF 构建的 O(1) 字典；非基元类型走缓存的 TypeConverter。
- 解析器用同步 `ReadLine` + `Task.Run`（替代每行一个 Task），取消检查按 256 行一次。
- `BulletPalleteList` 有序 backing 列表 + id 字典，枚举不再排序。
- `CommonPropertyChangedBase.ArgsOf` 缓存 `PropertyChangedEventArgs`；`GridBase` 已缓存通知参数。
- acb 解码结果落盘缓存；波形/峰值按需后台计算且复用编辑器音频，不额外解码。
- 并发打开共享同一笔在途加载；加载对话框由最后一个退栈者关闭。
- 渲染侧已落地的一批零分配/直写循环优化（见 `docs/render-performance-profile-2026-09-29.md` §8）。

## 复测方式

- 仓库基准（需本机杀软放行子进程，否则 BDN 会报 benchmark error）：
  `dotnet run --project OngekiFumenEditor.Benchmark -c Release -- --filter "*Parsing*" --job short`
- 快速验证加载编排改动：对比编辑器日志中 `Editor loading session started` → 各 `Editor loading step` → `session finished: Ready` 的时间戳（即本页基线表的取数方式）。
- 实施任何条目后：跑一遍 `dotnet run --project OngekiFumenEditor.FumenCheckerTests -c Release`（75/75 基线），并核对 `docs/render-performance-profile-2026-09-29.md` §8 未落地项是否受影响。
