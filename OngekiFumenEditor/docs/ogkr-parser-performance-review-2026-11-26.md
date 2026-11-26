# ogkr 解析性能审核摘要（2026-11-26）

> **来源**：WorkBuddy 分享页《审查项目可改进点》
> <https://workbuddy.link/p/qVPlwMpLHWvY1Ldz5ZBke6?shareId=729c8531-766b-430e-9d33-3f8cb00f2a2d&ext2=copy_link>
>
> **性质**：对上述页面内容的整理稿，**不是完整实施记录**。页面为 AI 会话产出，本文档未在仓库中逐条复核其余结论。
>
> **更新（2026-11-27）**：P-1 已采纳并落地，实施与验证记录见 §8；其余条目仍未复核、未实施。
>
> **标识符**：`C-*` / `P-*` / `B-*` / `M-*` / `V-*` / `N-*` 供后续讨论精确引用，与页面原文的序号无关。

---

## 0. 结论概述

- **C-0**：审核定位了 3 处 O(n²) 级热点，集中在 `IntervalTree` / `ConnectableObjectList` 的「每次取值都全量快照」，而非解析主循环本身。
- **C-1**：另外发现 1 处死代码（手写 span 解析永不执行）和 1 个 locale 正确性 bug（`float.TryParse` 未用 InvariantCulture）。
- **C-2**：720 个官方谱面（260MB）实测端到端 **68.8s → 14.9s（−78.3%）**，分配 **41.8GB → 7.6GB（−81.8%）**。

---

## 1. 三个 O(n²) 热点

### P-1 `Lanes.FirstOrDefault(...)` 每次查找都复制整张轨道表

> **状态：已采纳（2026-11-27）**，实施与验证记录见 §8。

- **P-1.1 调用链**：`ConnectableObjectList.GetEnumerator()` → `IntervalTreeWrapper` → `IntervalTree.Values` → `lock (writeGate) staging.ToArray()` + `snapshot.Select(i => i.Value)`。
- **P-1.2 实际代价**：一个「单条查找」= 复制全部 2488 条轨道 + 分配约 60KB 数组 + 线性扫描。
- **P-1.3 规模**：解析器里有 5 处这种查找；大谱面上 8000+ 次查找 × 2488 条 ≈ **2000 万次元素复制**。
- **P-1.4 现成方案**：`ConnectableObjectList` 内部已有 `startObjectByRecordId` 字典和 `TryGetStartObject`，解析器只是没用。
- **P-1.5 优化动作**：解析路径改用 `TryGetStartObject`，彻底绕开 `Values` 快照。

### P-2 解析期做 dockable 重定位 —— 全程空转

- **P-2.1 触发点**：`OngekiFumen.AddObject` 每加一条轨道都调用 `RelocateDockableObjects`。
- **P-2.2 代价**：内部 `GetAllDisplayableObjects(min, max)` 是 15 路 `Concat` + `Distinct`；其中 soflan / svg prefab / beam 三路完全无视范围参数，永远全量遍历。
- **P-2.3 为什么是白算**：解析时新加的轨道上不可能挂任何东西——引用它的 Tap/Hold 都在后面的行里才出现。每个墙子段（WLN/WRN）都会触发一次。
- **P-2.4 优化动作**：解析期跳过重定位（或加「批量/加载中」开关，解析结束后统一处理一次）。

### P-3 区间索引的二次方写入

- **P-3.1 路径**：每加一个子段 → `InsertChildObject` → `NotifyRefreshMinMaxTGrid` → `IntervalTreeWrapper.OnItemPropChanged` → `Remove`（`staging.FindIndex`，O(n)）+ `Add`（`staging.Any`，O(n)）。
- **P-3.2 规模**：6962 个子段 × 2488 条轨道 × 2 ≈ **3400 万次比较**。
- **P-3.3 现成设施**：`IntervalTreeWrapper` 已有 `BeginBatchAction` / `EndBatchAction`，但没人调用；且 `EndBatchAction` 只做 `NotifyDirty()`，批量期间被跳过的范围变化没有补算。
- **P-3.4 优化动作**：新增 `IntervalTree.RefreshRanges()`，批量写入后一次性补算范围，替代逐条 Remove+Add。这是本次改动的核心。

---

## 2. 顺带发现的 Bug

### B-1 死代码 + 三层 LINQ

- **B-1.1**：`CommandArgs.GetDataArray<T>` 里手写的 span-based `TryParse` 是死代码。
- **B-1.2 原因**：构造函数把 `IArgValueConverter` 全导入了，内置 string/int/float/double/bool/long 都有对应 converter，永远先命中 converters 分支。
- **B-1.3 实际执行**：`Select` + `OfType` + `ToArray` 三层 LINQ。
- **B-1.4 优化动作**：删掉死分支，或让高频路径真正走 span 解析；避免「看起来有快路径、实际没用上」。

### B-2 Locale 正确性 bug

- **B-2.1**：converter 分支里 `float.TryParse(x)` 使用当前区域设置；在小数点为 `,` 的系统上会把 `1.5` 解析错。
- **B-2.2**：ogkr 是固定格式，**必须 `InvariantCulture`**。
- **B-2.3**：同一行还会按类型各 `Split` 一次（TAP 要 string+float+int，就切 3 遍）——可合并为一次切分。

---

## 3. 实测数据（720 个官方谱面，260MB）

| 指标 | 基线 | 优化后 | 变化 |
| --- | --- | --- | --- |
| 总耗时 | 68,764 ms | 14,946 ms | −78.3%（4.6×） |
| 总分配 | 41,830 MB | 7,622 MB | −81.8%（5.5×） |

- **M-1 单谱面**（258KB / 13,530 行）：666ms → 163ms；364MB → 44MB。
- **M-2 小谱面**（715 行）：−33%——非线性项本来就是大谱面才放大。
- **M-3 关联解释**：41.8GB → 7.6GB 的分配量，正好解释编辑器加载后要手动 `GC.Collect()` + LOH `CompactOnce` 收那约 300MB 碎片——根源在此，而非 GC 参数。

---

## 4. 验证方式（可信度依据）

- **V-1 结果摘要对比**：对每个对象取 `TGrid / XGrid / RecordId / ReferenceLaneStart` + 各集合计数算摘要，720 个谱面逐项 **0 差异**。
- **V-2 集合语义自测 13 项**：批量写入后区间索引是否涵盖新的 Min/MaxTGrid、字典命中、增删路径、非批量路径。
- **V-3 改动规模**：12 个文件，+199 / −70，主工程 0 错误。

---

## 5. 留作下一轮（页面给出的 backlog）

- **N-1** `IntervalTree.Add` 的 `staging.Any(x => x.Value.Equals(value))` 仍是 O(n) 查重，换 `HashSet` 可再省一截。
- **N-2** `ConnectableObjectList.GetNewRecordId()` 的 `Select(x => x.RecordId).Max()` 也会走 `Values → ToArray()` 全量复制；MCP / 编辑器每次新建轨道都会踩到。
- **N-3** `GetAllDisplayableObjects` 的 15 路 `Concat` + `Distinct` 是编辑期绘制路径的固定开销，值得单独立项。
- **N-4** 解析主循环仍无 `try/catch`：一行坏数据整份加载失败，且异常不带行号（官方谱面没事，自制/损坏谱面会踩）。
- **N-5** 测试工具目前在 `.workbuddy/tmp/ogkr-perf/`（`--selftest` 跑集合自测，传目录跑基准并输出 digest 便于前后对比），既没进 sln 也没进 `tests/`。页面最后抛出的待定问题：**是否整理成 `tests/ogkr-parse-perf/` 正式工程**，便于以后改解析器时做回归。（按仓库约定，测试工程不进 sln，需按 csproj 单独调用。）

---

## 6. 建议落地顺序

1. **P-1 + P-2 + P-3** 一起做：它们是同一批「解析期不该走的通用路径」，分开改容易互相覆盖收益。
2. **B-2 立即修**：这是正确性问题，不是性能问题，且影响所有非 `,`/`.` 小数点的区域设置用户。
3. **N-1 / N-2** 属于低成本高确定性收益，可紧随其后。
4. **N-3 / N-4** 单独排期：一个影响编辑期绘制，一个影响容错体验，与解析性能正交。

---

## 7. 待决策项

- **D-1** 是否按 N-5 的建议，把临时基准工具整理为 `tests/ogkr-parse-perf/` 正式工程（不进 sln，按 csproj 单独调用）。
- **D-2** 除 P-1 外，本文档所列结论均为页面转述，动手前需在仓库中复核对应代码与调用链。

---

## 8. 实施记录

### 8.1 P-1（2026-11-27）

- 已把 5 处解析期 `Lanes.FirstOrDefault(x => x.RecordId == ...)` 改为 `Lanes.TryGetStartObject(...)`，并用返回的 `bool` 做 `if` 判断：
  - `Parser/Ogkr/CommandParserImpl/TapCommandParser.cs`
  - `Parser/Ogkr/CommandParserImpl/HoldCommandParser.cs`
  - `Parser/Ogkr/CommandParserImpl/LaneCommandParser.cs`
  - `Parser/Ogkr/CommandParserImpl/WallCommandParser.cs`
  - `Parser/Ogkr/CommandParserImpl/LaneBlockCommandParser.cs`（用 `out` 变量取 `LaneType`）
- 删除 4 个文件因此不再需要的 `using System.Linq;`；`LaneCommandParser` 仍用 LINQ，保留。
- 验证：
  - `dotnet build OngekiFumenEditor/OngekiFumenEditor.csproj -c Debug`：0 error。
  - 临时 harness 解析 5 个官方谱面（含 2465 条轨道、9570 个子段的大谱面），共 10950 项检查：每个 lane 的字典命中与枚举结果引用一致；Tap/Hold 的 `ReferenceLaneStart` 全部解析；子段对象数与 LLN/CLN/WLN 等命令行数一致；LaneBlock 左右方向计数与 WLS 定义一致。
  - 未重跑页面所述的 720 谱面全量基准（其工具不在仓库内）。
- 临时 harness 已删除，未入库。

### 8.2 P-1 新老实现对比（2026-11-27）

- 方法：父提交 `48f38545`（旧实现）与 `54115ba5`（新实现）分别做 Release 构建；同一临时 harness、同一 4 个官方谱面；每谱面预热 2 次、测量 5 次（每次前强制 GC），取中位数；输出 SHA-256 摘要确认两版解析结果一致。
- 端到端结果：

  | 谱面 | 轨道 | 旧耗时 ms | 新耗时 ms | 变化 | 旧分配 MB | 新分配 MB | 变化 |
  | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
  | 28903_04 | 1217 | 2604.49 | 2542.47 | −2.4% | 3235.93 | 3037.74 | −6.1% |
  | 20996_04 | 2465 | 2372.96 | 2246.35 | −5.3% | 2828.18 | 2446.14 | −13.5% |
  | 20993_04 | 282 | 41.06 | 39.65 | −3.4% | 57.47 | 47.30 | −17.7% |
  | 20995_03 | 682 | 917.56 | 857.36 | −6.6% | 1099.66 | 988.67 | −10.1% |

- 微基准（每谱面约 1 万次查找）：`TryGetStartObject` 稳定在约 4–8 ns/次、0 分配；`FirstOrDefault` 约 1.1–13.2 µs/次，每万次分配 65–558 MB（每次复制整张轨道表快照）。单次查找差距约 150–3000×。
- 结论：仅 P-1 使端到端耗时下降 2.4%–6.6%、分配下降 6.1%–17.7%；页面所述的 4.6× 是 P-1 + P-2 + P-3 合计，P-1 的收益集中在消除查找与快照分配。
- 摘要对比：4 个谱面的序列化 SHA-256 摘要两版完全一致。
- 临时 harness 与旧版 worktree 已删除，未入库。

### 8.3 P-3（2026-11-27）

- `IntervalTree` 新增 `RefreshRanges`：批量写入后按当前值一次性重算 staging 中所有范围，只递增一次版本号。
- `IntervalTreeWrapper`：批量期间记录 dirty；`EndBatchAction` 一次性刷新，移除原来每个子段触发的逐条 `Remove` + `Add`。
- `ConnectableObjectList` / `LaneList` 暴露 `BeginBatchAction` / `EndBatchAction` / `IsBatching`。
- `DefaultOngekiFumenParser.DeserializeAsync` 在解析循环外 `fumen.Lanes.BeginBatchAction()`，`finally` 中 `EndBatchAction()`。
- 为让批量期间不触发区间查询，`OngekiFumen.AddObject` 在 `Lanes.IsBatching` 时跳过 `RelocateDockableObjects`（P-2 的最小前置；`GetAllDisplayableObjects` 本身未改）。
- 验证：Debug/Release 构建 0 error；4 个官方谱面摘要与 P-1 完全一致；9304 项检查（每个 lane 的区间查询命中自身、Tap/Hold 引用、子段数）。
- 基准（P-3 前 `a037c659` vs P-3 后，Release，4 谱面 × 5 轮中位数）：

  | 谱面 | 轨道 | 前耗时 ms | 后耗时 ms | 变化 | 前分配 MB | 后分配 MB | 变化 |
  | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
  | 28903_04 | 1217 | 2570.57 | 535.54 | −79.2% | 3038.35 | 155.58 | −94.9% |
  | 20996_04 | 2465 | 2902.06 | 141.49 | −95.1% | 2446.91 | 119.39 | −95.1% |
  | 20993_04 | 282 | 43.46 | 10.65 | −75.5% | 47.36 | 11.83 | −75.0% |
  | 20995_03 | 682 | 870.03 | 95.73 | −89.0% | 989.42 | 73.80 | −92.5% |

- 结论：P-3 是页面所述 4.6× 的主要来源；本次 P-3 单独实测 4.1–20.5×，分配下降 75%–95%。`28903_04` 的 P-3 后耗时波动较大（min 163.58 / max 717.97 ms），中位数仍为 4.8×。
- 临时 harness 与 worktree 已删除，未入库。

