# DrawPlayableAreaHelper_new 优化分析

本文档记录对当前 `DrawPlayableAreaHelper_new.cs` 的静态检查结果。它只讨论优化方向和验证方案，不改变现有实现，也不同步修改 `DrawPlayableAreaHelper_new refactor plan.md`。

## 范围

主文件：

- `OngekiFumenEditor/Modules/FumenVisualEditor/Graphics/Drawing/Editors/DrawPlayableAreaHelper_new.cs`

只读参考：

- `OngekiFumenEditor/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.Drawing.cs`
- `OngekiFumenEditor/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.cs`
- `OngekiFumenEditor/Base/Collections/ConnectableObjectList.cs`
- `OngekiFumenEditor/Base/OngekiObjects/ConnectableObject/ConnectableStartObject.cs`

## 当前管线事实

当前 new helper 已接入主渲染路径，`FumenVisualEditorViewModel.Drawing.cs` 在每个 `CurrentDrawingTargetContext.VisibleTGridRanges` 区间调用一次 `DrawPlayField(...)`，之后再调用 `Draw(...)` 绘制设计模式音频结束线。

`DrawPlayField(...)` 的核心流程如下：

1. 检查设计模式、`EnablePlayFieldDrawing`、输入 `fieldMinTGrid` / `fieldMaxTGrid`。
2. 使用 `target.CurrentDrawingTargetContext.CurrentSoflanList` 作为当前 Soflan 上下文。
3. `CollectBaseSampleTGrids(...)` 收集采样 TGrid：
   - 可见区间最小和最大 TGrid。
   - 当前视口 TGrid。
   - 全谱 `WallLeft` / `WallRight` 的 start、end、start 自身和 child 节点。
   - 当前 Soflan position list 的断点。
   - 可见区间前最近和后最近的上下文采样点。
4. 对采样点排序，再由 `AddScreenDistanceSamples(...)` 按屏幕 Y 距离插入补采样点。
5. 对每个采样点调用 `BuildAreaSample(...)`：
   - 左墙调用 `QueryBoundaryXGridUnit(..., WallLeft, tGrid)`。
   - 右墙调用 `QueryBoundaryXGridUnit(..., WallRight, tGrid)`。
   - 每侧分别查询 `Bef` 和 `Aft`，再转成 `FieldAreaSample`。
6. `ConvertToLimitParam(...)` 将 XGrid unit 和屏幕 Y 转为 `FieldLimitParam`。
7. `DrawFieldQuads(...)` 对相邻 `FieldLimitParam` 生成双三角形：
   - 当前截面使用 `XAftL` / `XAftR`。
   - 下一个截面使用 `XBefL` / `XBefR`。
   - invalid、反转或过小高度的局部 quad 被跳过。

设置读取现状：

- `DrawPlayableAreaHelper_new.Initalize(...)` 会读取并订阅 `EditorGlobalSetting.Default.PropertyChanged`。
- new helper 当前响应 `EnablePlayFieldDrawing` 和 `PlayFieldForegroundColor`。
- `PlayFieldBackgroundColor` 仍由 `FumenVisualEditorViewModel` 用于预览背景。
- `HideWallLaneWhenEnablePlayField` 仍由 `FumenVisualEditorViewModel` 暴露给 `WallLaneDrawTarget`。

## 风险和优化项

### P0: `EditorGlobalSetting.PropertyChanged` 订阅无释放

`DrawPlayableAreaHelper_new.Initalize(...)` 订阅了 `EditorGlobalSetting.Default.PropertyChanged`，但 helper 没有 `Dispose()` 或其它释放入口。长期打开和关闭编辑器时，历史 helper 可能被全局 setting 对象持有，导致对象滞留，并在设置变化时继续执行 `UpdateProps()`。

建议改法：

- 让 `DrawPlayableAreaHelper_new` 实现 `IDisposable`，释放时执行 `EditorGlobalSetting.Default.PropertyChanged -= Default_PropertyChanged`。
- 在 `FumenVisualEditorViewModel` 的关闭路径释放 helper，例如 `TryCloseAsync(...)` 或明确的 render loop teardown。
- 同轮最好审计 `FumenVisualEditorViewModel` 自身对 `EditorGlobalSetting.Default.PropertyChanged` 的订阅，因为 viewmodel 构造函数也订阅了该事件。若目标是解决编辑器实例滞留，只修 helper 可能不完整。

验证重点：

- 反复打开/关闭编辑器后，旧 helper 不再响应 `PlayFieldForegroundColor` 或 `EnablePlayFieldDrawing` 变化。
- 若可用内存分析器，确认关闭后旧 `FumenVisualEditorViewModel` / `DrawPlayableAreaHelper_new` 不再被 setting 事件链持有。

### P1: 每个采样点重复查询左右墙和 Bef/Aft

当前 `BuildAreaSample(...)` 对一个采样点会查询左、右两侧，每侧又分别查询 `Bef` 和 `Aft`。也就是每个采样点最多调用 4 次：

```csharp
target.Editor.Fumen.Lanes.GetVisibleStartObjects(tGrid, tGrid)
```

每次查询后又执行 `Where` 过滤、`CalculateBoundaryXGridUnit(...)`、`ToArray()`、`Min()` 或 `Max()`。当 `AddScreenDistanceSamples(...)` 插入大量补点时，这条路径会成为主要 CPU 和分配热点。

建议改法：

- 引入帧内或单次 `DrawPlayField(...)` 内的 `FieldAreaFrameContext`。
- 在 context 中一次性缓存当前绘制范围所需的候选墙轨，按 `LaneType.WallLeft` / `LaneType.WallRight` 分组。
- 为 `(laneType, totalTGrid)` 或 `(laneType, totalTGrid, edge)` 做 boundary memo，避免同一 TGrid 的重复查询。
- 更进一步可以一次循环同时算左/右、`Bef`/`Aft`，输出完整 `BoundarySample`。

注意事项：

- 不能丢失当前 `Bef` / `Aft` 的边界语义：
  - `Bef` 使用 `MinTGrid < t <= MaxTGrid`。
  - `Aft` 使用 `MinTGrid <= t < MaxTGrid`。
- 同一 TGrid 多 child 的顺序语义要保留。
- 非 exact TGrid 的 valid path 仍应使用当前段插值，不能把下一组同刻 child 聚合成外侧 min/max。

### P1: `CollectBaseSampleTGrids(...)` 每个可见段遍历全谱墙轨节点

当前采样收集会枚举 `fumen.Lanes.Where(x => x.LaneType is WallLeft or WallRight)`，再访问每条墙轨的 start、end 和全部 child。这个逻辑每个可见 TGrid 区间都会执行一次，复杂度接近全谱墙轨节点数乘以可见区间数量。

建议改法：

- 短期：先用 `GetVisibleStartObjects(min, max)` 收集可见区间内墙轨，再单独补最近前置和后置上下文。
- 中期：为墙轨节点时间建立按 TGrid 排序的帧内索引，支持范围查询和 nearest-before / nearest-after 查询。
- 长期：如果谱面编辑版本号可用，把墙轨节点索引挂在 fumen/edit version 上，只有墙轨结构变化时失效。

必须保持的行为：

- 仍要加入可见区间前最近和后最近的上下文截面，避免高倍 Soflan 或极短可见区间导致 quad 缺失。
- Soflan 断点仍要作为采样来源。
- `fieldMinTGrid`、`fieldMaxTGrid`、当前视口 TGrid 仍要纳入候选。

### P1: `CalculateBoundaryXGridUnit(...)` 热路径分配较多

当前实现中存在多处 `ToArray()` 和 LINQ 链：

- `lane.GetChildObjectsFromTGrid(tGrid).ToArray()`
- `children.Where(...).ToArray()`
- `values.Select(...).Where(...).Select(...).ToArray()`
- 上层 candidates 的 `.ToArray()`

这些路径处于每个采样点、每侧、每个 edge 的热循环里。即使用了对象池，LINQ iterator 和数组分配仍会放大补采样点数量带来的开销。

建议改法：

- 将 `QueryBoundaryXGridUnit(...)` 和 `CalculateBoundaryXGridUnit(...)` 改成单次循环。
- 对候选墙轨循环时同时累计 `bef` 和 `aft`。
- 左墙使用 running min，右墙使用 running max，不创建 candidates 数组。
- 对 child 查询结果优先使用局部循环判断 exact child 首尾，只有底层 API 必须返回 `IEnumerable` 时再考虑局部小缓冲。

语义保护点：

- `children.Length == 0` 时仍回退到 `lane.CalulateXGrid(tGrid)` 或 `lane.XGrid`。
- exact TGrid 多 child 时仍按 child 顺序取首个为 `Bef`、末个为 `Aft`。
- valid path 的非 exact 采样点仍取 `children.FirstOrDefault()?.CalulateXGrid(tGrid)`。
- invalid path 的复杂情况仍允许聚合多个 child 的计算值。

### P2: `AddScreenDistanceSamples(...)` 中间 `Insert` 和重复 Y 转换

`AddScreenDistanceSamples(...)` 在遍历 `sortedTGrids` 时使用 `sortedTGrids.Insert(...)` 插入中间点。中间插入会移动后续元素，长列表和大量补点时成本较高。随后 `ConvertToLimitParam(...)` 又会再次对每个采样 TGrid 调用 `ConvertToViewRelativeY(...)`，和补采样阶段的 Y 计算重复。

建议改法：

- 改为输入已排序列表，输出新的 pooled list，按段 append 原点和补点，避免中间插入。
- 在同一个 context 内缓存 `totalTGrid -> viewRelativeY`。
- `AddScreenDistanceSamples(...)` 生成补点时写入 Y 缓存，`ConvertToLimitParam(...)` 直接复用。
- 补点后做一次线性去重即可，不需要在循环中 `RemoveAt`。

### P2: `DrawFieldQuads(...)` 可预估容量

每个有效相邻截面最多输出 6 个 `PolygonVertex`。当前 pooled list 没有预估容量，极端情况下会多次扩容。

建议改法：

- 如果 pooled list 支持 `Capacity` 或 `EnsureCapacity`，按 `(limitParams.Count - 1) * 6` 预估。
- 如果没有直接 API，可先统计有效 quad 数量再分配容量，但这会多走一遍循环。是否值得取决于 profiler。

该项风险低，但收益也低，适合和其它改动一起做。

### P3: 移除未使用的 `AddTGrid` 并整理命名

`AddTGrid(...)` 当前没有被调用。后续优化时可以顺手移除，降低误读成本。

其它可读性整理：

- `FieldLimitParam` 名称可以继续保留，和运行时 `LimitParam` 概念一致。
- 若引入 context，可以把 `BuildAreaSample(...)`、`ConvertToLimitParam(...)` 的参数减少为 context + totalTGrid。
- 注释重点放在 `Bef` / `Aft` 边界语义、exact child 和非 exact child 的差异上。

## 分阶段建议

### Phase 1: 低风险清理和生命周期释放

- 为 helper 增加释放路径并取消 setting 订阅。
- 审计 viewmodel 自身 setting 订阅释放。
- 移除未使用的 `AddTGrid(...)`。
- 不改几何算法。

目标是先解决长期会话的对象滞留风险，并清理无争议代码。

### Phase 2: 帧内 `FieldAreaFrameContext` 和 boundary memo

- 新增单次 `DrawPlayField(...)` 内的 context。
- 缓存候选墙轨分组、采样 TGrid、`TGrid -> Y`、boundary 查询结果。
- 保持现有 `BuildAreaSample(...) -> ConvertToLimitParam(...) -> DrawFieldQuads(...)` 输出不变。

目标是减少重复 `GetVisibleStartObjects(t, t)` 和重复 Y 转换，优先拿到较大性能收益。

### Phase 3: 去 LINQ 和数组分配的单次边界查询

- 重写 boundary 查询热路径。
- 单次遍历候选墙轨，同时计算 `Bef` / `Aft`。
- 用 running min/max 代替 candidates 数组和 `Min()` / `Max()`。

目标是降低补采样密集场景下的 GC 压力和 CPU 常数。

### Phase 4: 采样数量预算与 Y 转换缓存

- 将 `AddScreenDistanceSamples(...)` 改为 append 输出新列表。
- 复用 context 中的 Y 缓存。
- 根据 profiler 数据调整 `MaxSampleScreenDistance = 32` 和 `MaxExtraSamplesPerSegment = 64` 是否合适。

目标是在视觉稳定的前提下控制采样点数量。

### Phase 5: 按 profiler 数据继续深挖

- 如果 boundary 查询仍是热点，考虑跨 visible range 的帧级 context。
- 如果墙轨节点枚举仍是热点，考虑 fumen/edit version 驱动的墙轨时间索引。
- 如果 polygon 构建成为热点，再评估容量预估、批量写入或更低层 draw command 支持。

## 验证方案

### 构建验证

真正执行代码优化后至少运行：

```powershell
dotnet build OngekiFumenEditor\OngekiFumenEditor.csproj -p:SkipRecommendedScriptVerification=true -p:OutDir=.codex-build\verify-settings\
```

本文档为纯 Markdown 变更，写入后只需要运行：

```powershell
git diff --check
```

### 视觉回归

重点覆盖已反馈样例：

- `music1069`：同 TGrid 双 child 折角，例如 `T[20,240]`。
- `music1069`：墙轨结束截面折角，例如 `T[52,1440]`。
- `music1069`：高倍 Soflan 后可见区间缺少后置截面，例如 `T[91,1510]` 到 `T[91,1873]` 附近。
- `music0840`：`T[64,614]` 附近补采样不能超出墙壁包裹范围。

观察点：

- 当前截面 `Aft` 到下一截面 `Bef` 的连接仍正确。
- exact TGrid 多 child 仍保留折角。
- 非 exact 补采样点不误用下一组同刻 child 的外侧范围。
- 可见区间边界仍有前后上下文，不出现顶部或底部缺区域。

### 设置回归

- `EnablePlayFieldDrawing` 关闭后 new helper 不绘制 playfield。
- `EnablePlayFieldDrawing` 打开后 new helper 正常绘制。
- `PlayFieldForegroundColor` 修改后 new helper 填充色更新。
- `PlayFieldBackgroundColor` 仍由 viewmodel/背景清屏路径生效。
- `HideWallLaneWhenEnablePlayField` 仍由 viewmodel/`WallLaneDrawTarget` 路径生效。

### 性能验证

对大谱面预览模式记录帧耗时或 profiler trace，重点看：

- boundary 查询：`GetVisibleStartObjects(t, t)`、`QueryBoundaryXGridUnit(...)`、`CalculateBoundaryXGridUnit(...)`。
- 墙轨枚举：`CollectBaseSampleTGrids(...)` 是否仍全谱扫描。
- 采样补点：`AddScreenDistanceSamples(...)` 插入和 Y 转换成本。
- vertex 构建：`DrawFieldQuads(...)` 输出顶点数量和 list 扩容。

建议用同一谱面、同一视口、同一播放时间点对比优化前后，避免 Soflan 和 visible range 变化污染数据。

## 结论

当前 new helper 的几何模型已经比旧 helper 更接近 `LimitParam` 截面连接管线，主要优化空间不在最终双三角形绘制，而在每帧采样和边界查询的重复工作。优先级应先解决生命周期释放，再引入帧内 context 和 boundary/Y 缓存，最后根据 profiler 决定是否建立更长期的墙轨时间索引。
