# RND-008 投射物批处理专项审计（2026-09-22）

> **性质声明：** 本文件是 `render-perf-review-2026-09-22.md` §4.2 **RND-008** 的专项深挖，
> 用**真实谱面**与**代码构造的极端谱面**把该条目的三个推荐改法逐条量化，并给出并发只读改造的设计。
> 全部数字均为本机实测（BenchmarkDotNet），未改动业务代码、未提交任何修复。
> 文中「未覆盖」一节明确列出未建模/未实测的部分；凡属推断均标注 `[INFERENCE]`。

## 1. 范围与方法

- 被测实现（均复刻生产形状）：
  - **原实现**：
    - 预筛选：`Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.Drawing.cs:532-533`
      （预览模式 `BinaryFindRange(curTGrid, TGrid.MaxValue)`，取到谱面末尾）
    - 逐项 lane 查询：`.../ProjectileBatchDrawTargetBase.cs:310`
      `fumen.Lanes.GetVisibleStartObjects(objTGrid, objTGrid).OfType<EnemyLaneStart>().LastOrDefault()`
    - 并行：同文件 `:343-360`，`K >= ParallelCountLimit`（默认 **3000**）时 `Parallel.ForEach`，
      `DOP = Math.Max(2, ProcessorCount - 2)`（本机 16 逻辑核 → **14**）
    - 位置/裁剪：同文件 `:215-335` `_Draw`（`appearOffsetTime = height/objSpeed`、Y 裁剪、`precent > 1 && !CheckVisible`）
    - Y 映射真相：`TGridCalculator.ConvertTGridUnitToY_PreviewMode(tGridUnit, soflanList, bpmList, scale)`
      → `SoflanList.GetCachedSoflanPositionList_PreviewMode(bpmList)` 的分段线性 Y（**预览模式用带符号 speed**，
      `SoflanList_CachedPositionList.cs:182`），因此负速 soflan 会让 Y 随 TGrid **递减**
  - **改法1**：预筛选范围收紧（两种规则，见 §5）
  - **改法2**：帧内 `TGrid → EnemyLaneStart` 缓存
  - **改法3**：把共享查询移出并行区 / 重建收敛为一次
- 三个基准（均在 `benchmarks/OngekiFumenEditor.Avalonia.Benchmark/`）：
  | 文件 | 内容 | 谱面来源 | Job |
  |---|---|---|---|
  | `Benchmarks/ProjectileBatchRealChartBenchmarks.cs` | 真实谱面基线与逐帧形状对比 | `Data/FumenSamples/8089_10.ogkr`、`8090_10.ogkr` | `--job medium` |
  | `Infrastructure/ExtremeFumenFactory.cs` | 代码构造的 8 个极端谱面 | 程序化装配（不过解析器） | — |
  | `Benchmarks/ExtremeProjectileScenarioBenchmarks.cs` | 10 方法 × 8 场景 = 80 基准 | 上述工厂 | `--job short` |
- 量纲（重要，互不可换算）：
  - **逐项** = 单枚弹丸一次操作；**逐帧** = 一帧内全部弹丸（一个播放位置）的工作；
  - 全部**不是**整帧渲染加速比：本项只覆盖 RND-008 所辖的预筛选 / lane 查询 / 位置计算；
    帧内还有插值、可见性判定、缓冲合并、命令构造等，均未在本次对比范围内。
- 机器：Ryzen 7 5800X / .NET 11.0.0-preview.7 / Windows 11 26200；运行日志见
  `BenchmarkDotNet.Artifacts/OngekiFumenEditor.Avalonia.Benchmark.Benchmarks.*.log`。

## 2. 结论摘要

1. **改法2（帧内 lane 缓存）与谱面无关地稳定有效**：逐项 ≈200×、整帧 1.3–1.6×（对现状并行形状），
   分配降到 1/3–1/4；8 个极端场景 × 129 播放位置的逐量位置对比**零不一致**。零几何风险，建议先落。
2. **改法3（脏树并发重建）实测确认是真缺陷**：脏帧里 14 个工作线程**各自重建一次整树**
   （分配 11–12×），并会抛 `NullReferenceException`（未捕获时直接把基准进程打崩）。
   收敛为「一次重建」后时间降 2.2–2.9×、分配降 ≈12×、异常归零，且语义零变化。
3. **改法1（范围收紧）不是普适优化，且原文写法不完整**：收益完全由**最慢弹速**与
   **是否存在 speed ≤ 0 的 soflan 段**决定 —— 典型谱面 K 可降 187×，含 0.005 弹速时只降 1.6×，
   含静止/倒车段时**退化为 1.0×（上界 = 整条谱面）**。朴素规则（假设 spd=1、单组）在极端场景下
   **漏画 451 / 755 个物件**（占该帧应画量的 30% / 18%）。
4. **改动建议顺序**：先落改法2 + 改法3 的廉价形态（进并行区前同步一次 + 并行区只读），
   即可拿到本项目前可见的全部收益；`IntervalTree` 的并发只读改造（§8）单独立项，
   用于把「显式同步」也省掉、并让其它共享集合具备同样的并发读安全。

## 3. 真实谱面基线

### 3.1 谱面事实

| | `8089_10.ogkr` | `8090_10.ogkr` |
|---|---|---|
| 文件 | 177 KB | 354 KB |
| lanes | 352 | 208 |
| bullets / bells | 4683 / 148 | **13758** / 70 |
| BPM 变更 / 拍号 | 2 / 11 | 2 / 2 |
| maxTGrid | 273 960 | 241 440 |

剩余弹幕规模 K（= `BinaryFindRange(curTGrid, TGrid.MaxValue)` 的子弹+bell 并集），D = 不同 TGrid 数：

| 播放位置 | 8089: K (D, D/K) | 8090: K (D, D/K) |
|---|---|---|
| 0% | 4831 (1220, 0.253) | **13 828** (2661, 0.192) |
| 25% | 4038 (1030, 0.255) | 12 305 (2436, 0.198) |
| 50% | 3072 (676, 0.220) | 7567 (1437, 0.190) |
| 75% | 648 (267, 0.412) | 3186 (676, 0.212) |
| 100% | 0 | 0 |

- **K 在 0%–50%（8090 到 75%）都 ≥ `ParallelCountLimit = 3000`** → 预览模式下并行路径是**常态**，
  不是边角情况（判定按每个绘制目标的批次计，预览模式通常只有默认组，批次 ≈ 整个剩余集合）。
- **D/K ≈ 0.19–0.26** → 每个不同 TGrid 平均被 4–5 枚弹丸共享，这正是改法2 命中率的来源。

### 3.2 逐帧形状对比（50% 播放位置，DOP=14，µs/帧，含分配）

| 形状 | 8089 | 8090 | 分配 8089 / 8090 |
|---|---|---|---|
| 原实现·顺序直查 | 1179.7 | 2246.1 | 528 / 1301 KB |
| **原实现·并行直查（现状真实形状）** | **403.9** | **838.8** | 533 / 1306 KB |
| 改法2·帧内字典（顺序） | 303.2 | 511.6 | **163 / 347 KB** |
| 改法2+3（顺序 miss + 并行查字典） | 377.2 | 543.6 | **140 / 294 KB** |
| 改法2·保持并行 miss（需 §8 改造） | **244.3** | **322.5** | 573 / 1064 KB |

逐项量纲：lane 直查 384 / 287 ns·项 → 缓存命中 **4.1 / 5.8 ns·项**（110× / 49×）；
直查分配 176 B/项（540.7 KB / 1.33 MB 每帧），命中 0 B。

附：这两张谱面各只有 **2 个 BPM 变更**，按已签入的 DAT-C1 基准（每 +1 个 BPM 变更 ≈ +6.2 ns/次命中）
推算，DAT-C1 在这类谱面上每次命中约省 19 ns、每帧几百次换算约 5–10 µs —— 比 RND-008 的
0.1–2 ms 量级低两个数量级。`[INFERENCE]` 未做整帧实测。

## 4. 极端场景矩阵

全部由 `ExtremeFumenFactory` 代码构造（不走解析器），刻意在每个维度做绝：

| 场景 | 弹丸 | lane | 组 | BPM 变更 | soflan 点 | 负速段 | 弹速 | 说明 |
|---|---|---|---|---|---|---|---|---|
| `uniform` | 3000 | 200 | 1 | 2 | 2 | 0 | 1..8 | 基准 |
| `spd_min` | 3000 | 200 | 1 | 2 | 2 | 0 | **0.005/0.01/0.015/0.02** | `appearOffsetTime = height/spd` → 窗口放大 200× |
| `spd_max` | 3000 | 200 | 1 | 2 | 2 | 0 | 100–400 | 窗口极小 |
| `soflan_slow` | 4000 | 200 | 1 | 2 | 9 | 0 | 1..6 | 变速 ×0.02..×0.1 |
| `soflan_fast` | 4000 | 200 | 1 | 2 | 9 | 0 | 1..6 | 变速 ×50..×400 |
| `soflan_reverse` | 4000 | 200 | 1 | 2 | **1926** | **653** | 1..6 | 正→0→负→0→正 + 静止段(×0) + `InterpolatableSoflan` 渐变 |
| `soflan_groups` | 4000 | 200 | **4** | 2 | 20 | 0 | 1..6 | ×0.05/×1/×20/×400，同 TGrid 不同组 Y 完全不同 |
| `kitchen_sink` | **8700** | 256 | **5** | **7**（BPM 1 / 20000 / 30 …） | **9628** | **3201** | 0.01–400 | 全叠加 + 同 TGrid 密集簇 400×3 |

**本轮覆盖到的边缘条件**：弹速 0.005（`appearOffsetTime` ≈ 1.8e5）、负速倒车、speed = 0 静止段、
渐变跨零、多变速组、极端 BPM（1 / 20000）、同 TGrid 密集簇（D/K → 1）、大 K（4150）、脏树并发。

## 5. 正确性结果

### 5.1 预筛选收紧是否漏画（播放位置 50%，`Fix1_MissedDraws_*`）

判据：原实现（全量预筛选 + 完整 Y 裁剪）会画、而收紧后不再进入管线的物件数。

| 场景 | 安全规则 | 朴素规则 |
|---|---|---|
| `uniform` / `spd_max` / `soflan_slow` / `soflan_fast` / `soflan_reverse` / `soflan_groups` | **0** | 0 |
| `spd_min` | **0** | **451 / 1500（30%）** |
| `kitchen_sink` | **0** | **755 / 4150（18%）** |

- **安全规则** = 「按场景最慢弹速放大窗口 `ΔY ≤ (rectMaxY-baseY)/(spd_min·scale)`」
  + 「对每个 soflan 组分别反解 TGrid（逐段线性/二分）」+「段内 Y 已整体落在目标之下时保守取段末」。
  8/8 场景零漏画，包括 653 / 3201 个负速段与静止段。
- **朴素规则** = 「假设 spd = 1、只看默认组」→ 在弹速下限低或存在极端变速时**大面积漏画**。

### 5.2 物件位置计算对比（`PositionCompare_Mismatch`）

**129 个播放位置 × 全部弹丸**，逐量比较：`该画否` / `timeY`（精确到 1e-9）/ `lane 解析对象`（引用相等）。
8 个场景全部 **0 不一致**；该结论由 `[GlobalSetup]` 断言强制（非 0 直接抛、整个场景失败）。
一次完整 sweep 的成本 21.9–74.3 ms（诊断用途，非每帧成本）。

### 5.3 脏树 + 并行查询（`DirtyStorm_*`）

| 场景 | 现状（改树后立刻并行查询） | 收敛为一次重建 | 倍数 |
|---|---|---|---|
| `8089_10.ogkr` | 667.9 µs / 3080.7 KB | 295.3 µs / 232.0 KB | 2.26× / 13.3× 分配 |
| `8090_10.ogkr` | 348.5 µs / 1395.6 KB | 158.9 µs / 125.2 KB | 2.19× / 11.1× 分配 |
| 8 个极端场景 | 599.7–663.8 µs / 2.55–2.70 MB | 226.1–255.0 µs / 231 KB | 2.4–2.9× / ≈12× 分配 |

- 分配倍数 ≈ `DOP − 1` → **14 个线程各自重建了一次整树**（墙钟只 2.2–2.9× 是因为它们并行跑，CPU 白烧 ≈14×）。
- **异常实测**：一次未捕获版本在两个真实谱面上都在**第一个测量调用**把基准进程打崩，栈为
  `IntervalTreeNode.QueryInto` → `NullReferenceException`；捕获后统计为
  8089: 2/4 次每 28 749 次风暴、8090: 92/98 次每 57 421 次（≈1e-4 – 1.6e-3 每脏帧），
  极端场景 3–10 次每 9293 次。
- 机制（读码确认）：`IntervalTree.RebuildInternal()` 先 `Release(root)`，而 `IntervalTreeNode.Release`
  **递归就地清空** `items/leftNode/rightNode`（`ClearValue`）→ 正在遍历该树的线程随后解引用已置空字段。
- **可达性** `[INFERENCE]`：并行区只读、UI 线程被 `Parallel.ForEach` 阻塞，因此「读-写重叠」在渲染路径上
  不可达；可达的是「脏树 + 多读线程」（重建互相清空）。一帧内若更早的 lane 查询（lane 绘制目标、
  `DrawPlayableAreaHelper` 等）已把树喂干净，则不会命中 —— 这也是实测 NRE 频率低的原因；
  但只要该帧更早没有 lane 查询，首个查询恰好落在并行区内即命中。

## 6. 收益结果

### 6.1 改法1：K 与预筛选时间（`--job short`）

| 场景 | K(full) | K(安全收紧) | 上界占比 | 原实现预筛选+位置计算 | 收紧后 |
|---|---|---|---|---|---|
| `uniform` | 1500 | **8（187×）** | 50.2% | 84.0 µs | 14.7 µs |
| `spd_max` | 1500 | 3 | 50.0% | 87.8 µs | 15.4 µs |
| `soflan_fast` | 2000 | 4 | 50.0% | — | — |
| `soflan_slow` | 2000 | 76 | 51.8% | 138.6 µs | 25.9 µs |
| `soflan_reverse` | 2000 | 4 | 50.1% | 195.6 µs | 70.9 µs |
| `soflan_groups` | 2000 | 80 | 51.8% | — | — |
| **`spd_min`** | 1500 | **921（1.6×）** | **80.6%** | 82.3 µs | 58.9 µs |
| **`kitchen_sink`** | 4150 | **4150（1.0×）** | **100.0%** | — | — |

原因：窗口宽度 ∝ `1/最慢弹速`（`appearOffsetTime = height/objSpeed`），弹速 0.005 时是 speed=1 的 200 倍；
存在 **speed ≤ 0（静止/倒车）段**时 Y 不再单调，「按 Y 窗口反解 TGrid 上界」只能保守取整条谱面，
否则就是漏画（见 §5.1 朴素规则的 451 / 755）。

> 注：该方法的实现是「遍历全部弹丸再按上界过滤」（O(N) 扫描），生产用的是 `BinaryFindRange`（O(log n + k)），
> 因此上表的**时间**差异只是判据数量差异的下界，K 才是本项真正收益的度量。

### 6.2 改法2 / 改法3 的简洁总结（跨全部测试）

**改法2（帧内 `TGrid → enemyLane` 缓存）** —— 与谱面无关的稳定收益，零几何风险：

| 量纲 | 原实现 | 改法2 | 倍数 |
|---|---|---|---|
| 逐项（真实谱面 8089 / 8090） | 384 / 287 ns·项 | **4.1 / 5.8 ns·项** | 110× / 49× |
| 逐项（8 个极端场景） | 487–715 µs/帧 | **2.1–3.4 µs/帧** | **≈200×** |
| 整帧（真实谱面 8089 / 8090） | 403.9 / 838.8 µs（现状并行） | **303.2 / 511.6 µs**（帧内字典，含其分配） | 1.33× / 1.64× |
| 分配 | 176 B/项（528 KB–1.3 MB/帧） | 命中 0 B；帧内字典 163–347 KB/帧 | 降至 1/3–1/4 |

命中率有保证：真实谱面 D/K ≈ 0.19–0.26；极端场景中同 TGrid 密集簇使其更极端。
正确性：8 场景 × 129 播放位置的逐量对比 0 不一致。代价：每帧一个字典的分配（可池化）。

**改法3（把共享查询挪出并行区 / 重建收敛为一次）** —— 语义零变化，纯安全 + 性能：

| 量纲 | 现状 | 改法3 | 倍数 |
|---|---|---|---|
| 真实谱面 8089 / 8090 | 668 / 349 µs | 295 / 159 µs | 2.26× / 2.19× |
| 8 个极端场景 | 600–664 µs | 226–255 µs | 2.4–2.9× |
| 分配（≈并发重建次数） | 2.55–2.70 MB/帧（≈11–13 次重建） | 231 KB（1 次） | **≈11–12×** |
| 异常 | NRE 3–10 次 / 9293 次风暴；未捕获时崩进程 | 0 | — |

**组合结论**

| 形状 | 8089 | 8090 |
|---|---|---|
| 现状（并行直查） | 403.9 µs / 533 KB | 838.8 µs / 1306 KB |
| **改法2+3**（顺序 miss + 并行查字典） | **377.2 µs / 140 KB** | **543.6 µs / 294 KB** |
| 改法2·保持并行 miss（需 §8） | 244.3 µs | 322.5 µs |

改法2+3 等于或快于现状、分配降到 ≈1/4，并同时消除 NRE 与重复重建 → **建议直接落地**。
唯一更快的是「保持并行 miss」，但它要求 lane 树可并发只读（当前不满足）——见 §8。

## 7. 改法1 的落地约束（结论）

1. 不应无条件启用；建议加一条**廉价前置判据**（场景级、非每帧）：
   `弹速下限 ≥ 阈值` **且** `不存在 speed ≤ 0 的 soflan 段` → 才启用收紧；否则保持全量预筛选。
2. 若启用，安全形态必须同时具备：
   ① 按最慢弹速放大窗口；② 逐 soflan 组反解 Y；③ 对 speed ≤ 0 段退化为「取整条谱面」。
   缺任一条就会像朴素规则那样一次丢 451 / 755 个物件。
3. 交付前必须做差分校验：同帧同视口下，新旧实现的可见集合逐对象一致（本次的
   `PositionCompare_Mismatch` 即该形态的最小实现，可直接复用为回归门槛）。

## 8. 并发只读改造（若要让"并行 miss"安全）

前提澄清：**若接受"进并行区前同步一次 + 并行区内只读"，则不需要改库** ——
本次基准里的 `Optimized_Frame_ParallelMissAndLookup` 就是这种形状（先 `SyncLaneTree()` 再并行 miss），
实测 244.3 / 322.5 µs，无异常。只有想**保留"永不显式同步"的原形状**时，才需要下述改造。

### 8.1 重建改为「建新图 → 原子发布」，永不就地清空（关键，NRE 根因）

```csharp
private void RebuildInternal()
{
    if (isInSync) return;
    var newRoot = IntervalTreeNode<TKey, TValue>.BuildTree(items, comparer);  // 先建新
    Volatile.Write(ref root, newRoot);                                        // 再原子发布
    Volatile.Write(ref isInSync, true);
    // 旧 root 不再就地清空：交给 GC（或引用计数后再回池）
}
```

读者只会看到「旧树」或「新树」，两者都是完整结构 → 无 NRE、无静默截断；多线程同时重建时各自建一份，
最后发布者胜出（浪费一次构建，但正确）。同时把 `Query` / `QueryInto` / `GetEnumerator` 统一改为
`Volatile.Read(ref root)` 取一次本地引用后遍历。

### 8.2 重建去重（可选，仅省 CPU）

```csharp
private readonly object rebuildGate = new();
private void RebuildInternal()
{
    if (Volatile.Read(ref isInSync)) return;
    lock (rebuildGate) { if (isInSync) return; /* 建新图并发布 */ }
}
```

注意：**光加锁不够** —— 已通过检查、正在遍历旧树的读者仍会被并发 `Release` 打断；
必须与 8.1 一起用（8.1 之后锁只用于省重复构建，不再是正确性必需）。

### 8.3 写路径与 `items` 隔离（对应"边查边改"实测 98.8% 异常）

`items` 是 `List<RangeValuePair>`，`Add` / `Remove(T)` 直接改、`BuildTree` 直接枚举。做法三选一：

- 写侧 copy-on-write（`Remove(IEnumerable)` 已是该风格，`Add` / `Remove(T)` 跟进即可）；
- 或写侧持锁、重建在锁内对 `items` 取快照（`items.ToArray()`）后再建树，读者永不触碰 `items`；
- 或换不可变/分段结构（顺带解掉 DAT-007 提到的 `Remove` O(n) 扫描）。

### 8.4 调用点收口与不变量

- 热点调用点统一走已有的零分配 `QueryInto(from, to, pooledList)`（`Query` 每次会租一个池化列表并持有到枚举结束）；
- `IntervalTreeWrapper` 暴露显式 `Sync()` / `EnsureInSync()`，让"帧首同步"成为契约而非"随便先查一次"；
- `Add` / `Remove` 加 DEBUG 断言：处于「并行只读区」时直接抛，把"并行区只能只读"变成可强制的不变量。

### 8.5 成本、风险与验收

- 收益：允许"并行 miss"形状（8090: 838.8 → 322.5 µs = 2.6×；8089: 403.9 → 244.3 µs = 1.65×），
  并顺带消除脏帧的重复重建与 NRE。
- 成本/风险：改的是**全仓共享的底层容器**（Lanes / Bullets / Bells / Beams / Holds / Soflans /
  SoflanSegment / IndividualSoflanArea 的区间查询都走它），出错后果是「任意查询偶发漏项」这类**静默**故障，
  比性能问题贵。
- 验收门槛（可直接复用本次 harness）：
  ① 脏树 + 14 读线程风暴：NRE 与结果不一致均为 0；
  ② 边查边改（1 写线程 + 14 读线程）：异常为 0；
  ③ 「一轮风暴的分配量 ÷ 单次重建分配量」≈ 1（本次即用此法量出 11–13 次并发重建）。
- **建议顺序**：先落 §6.2 的改法2+3（含一行 `EnsureInSync()` 公开方法），拿满当前全部收益且零库改动；
  §8 的并发改造单独立项，带上 ①③ 与回归用例一起做。

## 9. 未覆盖与未决

1. **X 方向裁剪与屏幕投影未建模**（`rectMinX..rectMaxX`、`ConvertXGridToX`）。它只会**进一步剔除**物件，
   故本模型判定"该画"的集合是生产实现的**超集** —— §5 的"不漏画"结论因此**保守成立**。
2. **视口固定为单配置**（height 900 / judgeOffsetY 200 / scale 1）；未扫描其它视口/缩放组合。
3. **真实谱面的弹速分布未统计**：`ExtremeFumenFactory` 是刻意构造的极端值；§7 的阈值需要用真实谱面统计来定。
4. **无整帧端到端实测**：本次全部是 RND-008 所辖部分的量纲，不能当作整帧加速比。
5. **harness 局限**：BenchmarkDotNet 只输出耗时/分配，**不输出返回值**，故"漏画数 / 不一致数"
   以 `[GlobalSetup]` 断言（非 0 即抛、整场景失败）与 `PrintFacts` 打印体现，未作为基准数列出现。
   需要成为可见数列时，可把计数编码进返回值，或单独用 `--job dry` 跑一组"正确性基准"。
6. **延迟/抖动未测**：只有吞吐量（ns/µs per 操作），没有每帧时间分布或 99 分位。

## 10. 本次新增（未提交）

| 文件 | 说明 |
|---|---|
| `benchmarks/OngekiFumenEditor.Avalonia.Benchmark/Benchmarks/ProjectileBatchRealChartBenchmarks.cs` | 真实谱面基线与逐帧形状对比（`[Params]` 两张谱面分别出数） |
| `benchmarks/OngekiFumenEditor.Avalonia.Benchmark/Data/FumenSamples/{8089_10.ogkr, 8090_10.ogkr}` | 由 `F:\ongeki bright memory\package\option\A032\music\...` 复制进语料（csproj 的 `<EmbeddedResource Include="Data\FumenSamples\*.ogkr" />` 自动嵌入） |
| `benchmarks/OngekiFumenEditor.Avalonia.Benchmark/Infrastructure/ExtremeFumenFactory.cs` | 8 个极端场景谱面的代码构造 |
| `benchmarks/OngekiFumenEditor.Avalonia.Benchmark/Benchmarks/ExtremeProjectileScenarioBenchmarks.cs` | 10 方法 × 8 场景，含两条收紧规则、逐量位置对比、脏树并发 |

另：`%TEMP%\rnd008-sim\`（首轮合成模拟，240 lane 的临时脚本，非仓库内容）结论与上表一致，已被本基准取代。

## 11. 落地：改法 2·保持并行 miss（2026-09-23）

本节记录 §8 + §6.2 末行「改法2·保持并行 miss（需 §8）」的**实施结果与实测**。
（本节的「改前」数字来自上文各表；「改后」数字全部是本机 BenchmarkDotNet 实测，命令与产见文末。）

### 11.1 改了什么

| 文件 | 改动 |
|---|---|
| `Base/Collections/Base/RangeTree/IntervalTree.cs` | 写入只改**写者私有的 `staging`**（`writeGate` 保护），重建在 `rebuildGate` 内**单飞**、先建新图再**原子发布**（`Volatile.Write(ref root)` + 版本令牌 `version`/`syncedVersion`）；不再 `Release` 旧树。`Values`/`GetEnumerator` 改为持锁拷贝快照，读者永不触碰 `staging` |
| `Base/Collections/Base/RangeTree/IntervalTreeNode.cs` | 节点**不可变**（字段全 `readonly`），删除 `Release()`/`ClearValue()`（就是 NRE 根因；节点从不池化，就地清空省不下任何分配） |
| `Base/Collections/Base/RangeTree/IIntervalTree.cs` | 新增 `EnsureInSync()` |
| `Base/Collections/Base/IntervalTreeWrapper.cs` | 新增 `EnsureInSync()` 与零分配 `QueryInRangeInto()` |
| `Base/Collections/ConnectableObjectList.cs` | 新增 `EnsureInSync()` 与零分配 `QueryVisibleStartObjectsInto()` |
| `.../BulletBell/ProjectileBatchDrawTargetBase.cs` | 帧内 `TGrid→EnemyLaneStart` 缓存挂在 `DrawBuffer` 上（**每个并行工作线程一份**，池化复用、帧末/取用时清空），miss 仍在并行区内就地发生；lane 查询改走零分配 `QueryInto`；进并行区前 `EnsureInSync()` 一次（§6.2 改法 3 的廉价形态） |

关于 §8.4 的两点说明（有意偏离审计原文）：
1. **没有加「并行只读区里写入就 throw」的 DEBUG 断言**：改造后写入一个正被遍历的树已经是安全的（读者看的是不可变快照），
   假阳性来源却很实在——`SoflanList.GetCachedSoflanPositionList_PreviewMode` 会在帧内**惰性重建**自己那棵私有区间树（持锁建新树再发布，
   §8.5 里「其它共享集合」那条的同类形态），把它当成违规会让 DEBUG 构建在正常路径上抛。不变量改由注释 + §11.3 的并发用例固化。
2. §8.4 的「热点调用点统一走零分配 `QueryInto`」已落：`ProjectileBatchDrawTargetBase.QueryEnemyLane` 现在是池化列表 + 倒序扫描，
   等价于原 `OfType<EnemyLaneStart>().LastOrDefault()`（见 §11.4 的逐量等价断言）。

### 11.2 验收（§8.5 的三条门槛）

| 门槛 | 结果 | 证据 |
|---|---|---|
| ① 脏树 + 14 读线程风暴：异常与结果不一致均为 0 | **通过** | 真实谱面 8090 短跑：`DirtyStorm_CurrentShape` 36 941 次调用 **0 次 NRE**；`GlobalSetup` 硬断言（命中数必须等于干净帧）通过 |
| ② 边查边改（1 写 + 14 读）：异常为 0 | **通过** | `IntervalTreeConcurrencyBenchmarks.WriteWhileReading_Churn`：512/8192 项下读异常均 **0**；`GlobalSetup` 另断言每次读只能看到「写前完整集合」或「写后完整集合」 |
| ③ 一轮风暴分配 ÷ 单次重建分配 ≈ 1 | **通过** | 512 项：363 387 B ÷ 356 520 B ≈ **1.02**；8192 项：7 019 293 B ÷ 7 012 300 B ≈ **1.00**（改前实测为 DOP−1 ≈ 11–13）。耗时侧同结论：8192 项风暴 3.91 ms ≈ 单次重建 3.55 ms |

（`IntervalTreeConcurrencyBenchmarks` 的 `[GlobalSetup]` 把 ①② 做成硬断言——非 0 直接抛、整类失败；计数与比值由 RDN 全程复跑。）

### 11.3 性能（`--job short`，8090_10.ogkr，DOP=14，K=13 828 / D=2661，逐帧量纲）

| 方法 | Mean | 分配 | 相对 `Original_FrameDirectQueriesParallel` |
|---|---|---|---|
| `Original_LaneQueryPerItem`（逐项直查，基线） | 2 682.0 µs | 1 331 824 B | 3.78× |
| `Optimized_CachedLaneLookupPerItem`（逐项，缓存命中） | 42.7 µs | 32 B | 0.06× |
| `Optimized_PooledLaneQueryPerItem`（逐项，**生产**零分配查询） | 1 180.3 µs | 32 B | 1.66× |
| `Original_FrameDirectQueries`（顺序直查） | 1 812.1 µs | 1 331 792 B | 2.55× |
| **`Original_FrameDirectQueriesParallel`（改前现状形状）** | **709.6 µs** | **1 336 944 B** | **1.00×** |
| `Optimized_FrameWithFrameLocalCache`（顺序 + 帧内字典） | 435.3 µs | 355 128 B | 0.61× |
| `Optimized_Frame_SequentialMissThenParallelLookup`（改法2+3） | 505.2 µs | 301 403 B | 0.71× |
| `Optimized_Frame_ParallelMissAndLookup`（审计形状：共享 `ConcurrentDictionary` + 先 `SyncLaneTree()`） | 241.5 µs | 1 083 718 B | 0.34× |
| **`Fix2_ProductionShape_PreSyncThenParallelMiss`（本次落地）** | **151.5 µs** | **135 044 B** | **0.21×** |
| `Fix2_ParallelMiss_PerThreadCache_NoPreSync_OnDirtyTree`（脏帧、区内重建） | 271.9 µs | 259 810 B | 0.38× |
| `Fix2_ParallelMiss_SharedConcurrentCache`（对照：全线程共享缓存） | 180.3 µs | 158 631 B | 0.25× |
| `DirtyStorm_CurrentShape`（脏树 + 并行查询） | 213.1 µs | 136 205 B | 0.30× |
| `DirtyStorm_AfterSingleRebuild`（先同步再并行） | 146.6 µs | 134 547 B | 0.21× |

结论：
1. **落地形状比改前快 4.68×、分配降到 1/9.9**（709.6 → 151.5 µs；1 336 944 → 135 044 B），比审计里的并行 miss 形状再快 1.59×、分配降 8×。
2. **每线程一份缓存优于全线程共享一份**（151.5 µs / 135 KB vs 180.3 µs / 159 KB）：无 CAS 争用，且缓存随 `DrawBuffer` 池化复用，稳态零额外分配。
3. **零分配 lane 查询本身值 2.27×**（1 180.3 → 42.7 µs 是缓存命中的上限；miss 侧单看查询 2 682.0 → 1 180.3 µs，分配 1.33 MB → 32 B）。
4. **脏帧的代价是吸收一次重建**（271.9 µs vs 干净帧 151.5 µs，+120 µs ≈ 一次 lane 树重建），故生产路径仍在进并行区前 `EnsureInSync()`；
   注意审计 §6.2 最后一行「改法2·保持并行 miss 244.3 / 322.5 µs」那条基准**前置了 `SyncLaneTree()`**，
   量到的是「先显式同步 + 再并行 miss」；本节 `Fix2_ParallelMiss_PerThreadCache_NoPreSync_OnDirtyTree` 才是「并行区自己扛重建」。
5. 读路径没有退化：干净树全量查询 512 项 4.8 µs / 8192 项 81.0 µs，分配 112 B（池化列表），
   即读者快路径只多了两次 volatile 读（版本比较），量级上不可见。

### 11.4 测试（新增 11 例，`-c Release` 全量 819/819 通过）

| 文件 | 覆盖 |
|---|---|
| `tests/.../Base/Collections/IntervalTreeConcurrencyTests.cs` | 脏树 + 14 并发读者必须都看到完整索引；1 写 + 14 读不抛且只看到完整状态；交错增删后与线性扫描逐量对拍；`EnsureInSync` 语义；`Clear` 不影响已开始的枚举（快照语义）；`QueryInRangeInto` 与 `QueryInRange` 同结果 |
| `tests/.../Graphics/ProjectileBatchLaneCacheTests.cs` | 生产 `QueryEnemyLane` 与旧表达式 `OfType<EnemyLaneStart>().LastOrDefault()` 在 0..20 000 grid 上**逐一引用相等**；同 TGrid 多发弹丸命中缓存后落点不变；多 lane 命中时「最后一条胜出」与旧语义一致；**串行与并行两种形状坐标完全相同**；同一实例跨帧（含池化缓冲复用）不得残留上一帧的 lane 解析 |

测试全部断言在**可观测输出**上（画出来的坐标、查询返回的集合），不断言实现细节；帧级用例用真实的
`ProjectileBatchDrawTargetBase<Bullet>.DrawBatch` + 桩绘制上下文（`IsLocked=true` 即预览模式）驱动。

### 11.5 未覆盖 / 有意不做

1. **`SoflanList` 位置列表仍用「锁 + 就地重填同一个 `List`」**（`SoflanList_CachedPositionList.cs`）：并行区内惰性重建时会就地改写
   读者正在二分查找的列表，属 §8.5 所说的「其它共享集合」同类残留，本次未动（不在 RND-008 的投射物路径上，且改动面同样跨全仓）。
2. **写密集帧**：若同一帧内持续有写者（编辑线程）在改 lane，则每次查询都会重建一次（与改前行为相同，只是不再有 NRE 与就地清空）；
   生产上写发生在帧间，本次未做「脏标记合并/节流」。
3. X 方向裁剪、真实谱面弹速分布、整帧端到端、延迟分位等仍如 §9。

### 11.6 复现命令

```powershell
# 实现验证（基准，Release）——产物落在 benchmarks/OngekiFumenEditor.Avalonia.Benchmark/BenchmarkDotNet.Artifacts/
cd benchmarks/OngekiFumenEditor.Avalonia.Benchmark
dotnet run -c Release -- --job dry  --filter "*ProjectileBatchRealChartBenchmarks*"      # 门槛断言冒烟
dotnet run -c Release -- --job short --filter "*ProjectileBatchRealChartBenchmarks*8090*" # §11.3 的数字
dotnet run -c Release -- --job short --filter "*IntervalTreeConcurrencyBenchmarks*"       # §11.2 的 ①②③

# 测试（Release；Debug 全量会混入 headless 隔离噪声）
dotnet test tests/OngekiFumenEditor.Avalonia.Tests/OngekiFumenEditor.Avalonia.Tests.csproj -c Release --nologo
```

