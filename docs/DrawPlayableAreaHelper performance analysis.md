# DrawPlayableAreaHelper 性能问题分析

## 背景

`DrawPlayableAreaHelper.DrawPlayField` 在编辑器渲染管线里**每一帧、每一段 visible TGrid
区间都会被调用**(`FumenVisualEditorViewModel.Drawing.cs:555-556`),负责计算并填充
黑色可击打区域多边形。

它的内部实现把"左墙点列表"和"右墙点列表"两次几乎独立地枚举一遍,再做交点修正、
三角剖分。本文档**仅做静态分析**(读源码,不实际 profile),列出在大型谱面下最可能
成为热点的开销以及对应的修改方向,方便后续决定是否要动手优化以及优化哪几个点。

## 涉及文件

主文件:
- `OngekiFumenEditor/Modules/FumenVisualEditor/Graphics/Drawing/Editors/DrawPlayableAreaHelper.cs`

辅助路径(只读引用):
- `Base/Collections/ConnectableObjectList.cs` — `GetVisibleStartObjects`
- `Base/OngekiObjects/ConnectableObject/ConnectableStartObject.cs:411` — `GenAllPath`(yield-return generator)
- `Utils/LinqExtensionMethod.cs` — `ToListWithObjectPool`、`SequenceConsecutivelyWrap`、`InsertBySortBy` 等
- `Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.Drawing.cs:555` — 调用入口

## 发现的性能问题(按影响从高到低)

### 1. LINQ 链 + `ToListWithObjectPool` 在热循环里大量分配/枚举(高频)

位置: `DrawPlayFieldInternal` → `EnumeratePoints`(被左右各调用一次,内部还有
`ranges.Count` 次内循环)。

典型片段:

```csharp
using var lanes = fumen.Lanes
    .GetVisibleStartObjects(...)
    .Where(x => x.LaneType == type)
    .ToListWithObjectPool();

using var polylines = lanes
    .SelectMany(x => x.GenAllPath()
        .Where(...).Select(...).SequenceConsecutivelyWrap(2).Select(...))
    .ToListWithObjectPool();
```

- `GenAllPath()` 是 `yield return` 迭代器,每次调用都分配新 enumerator;在
  `EnumeratePoints` 中**同一条 lane 至少被 `GenAllPath()` 3 次**(polylines 阶段一次、
  pick 阶段一次、interpolate 阶段一次)。
- `SequenceConsecutivelyWrap(2)` 内部按帧分配 `T[2]` 数组(见
  `LinqExtensionMethod.cs:457`),每帧上千次。
- 这层 LINQ 链在每个 visible 区段内都会重新展开。

**改进方向**:把 polylines/lanes 缓存到 pooled list,在 `EnumeratePoints` 顶部计算一
次后复用;避免对同一 lane 多次调用 `GenAllPath()`(用一次 enumerator,把
`(pos.X, pos.Y)` 拷到 `Span<Vector2>`/pooled buffer 里)。

### 2. `EnumeratePoints` 内部 `IList.Insert(index, ...)` 的中间插入是 O(N)

`appendPoint3(list, ..., insertIdx)` 调用 `list.Insert(insertIdx, p)`;在
`postInterpolatePoint` 中 `insertIdx = 0`(`FirstRange`)更是**头部插入**,每次都把
后面所有点平移。

```csharp
appendPoint3(result, a.X, a.Y, flag switch {
    FieldRangeParam.LastRange  => result.Count,
    FieldRangeParam.FirstRange => 0,
});
```

若 `result.Count` 达到几百(长谱面 + 多 lane),`FirstRange` 路径每次插入都是 O(N)。

**改进方向**: 用两段缓冲(`prepend` 端用反向 append 到独立 list,最后再 reverse + 
concat),或者整体改用 `List<Vector2>` 并在末尾构造、最后做一次 reverse;消除中间
插入。

### 3. `AdjustLaneIntersection` 的 `tryExchange` 是 O(N) swap,且在内层频繁触发

`DrawPlayableAreaHelper.cs:629-696`:

```csharp
tempLeft.AddRange(leftPoints.Skip(li));
tempRight.AddRange(rightPoints.Skip(ri));
leftPoints.RemoveRange(li, tempLeft.Count);
rightPoints.RemoveRange(ri, tempRight.Count);
leftPoints.AddRange(tempRight);
rightPoints.AddRange(tempLeft);
```

- `Skip(li)` 是 enumerator 而不是 ArraySegment,每个 `AddRange` 都重新枚举。
- 整个交换路径在 while 主循环里被多次触发(CASE A/B/C/V/A 形 5+ 次)。
- 主循环本身是 while-step,大多数分支都以 `leftIdx++; rightIdx++` 收尾,但
  CASE C 会嵌套 4 个 `tryGetLine` + `GetLinesIntersection`,每次都 `Contains` 一
  次 `Vector2` set(浮点 hash,容易抖)。

**改进方向**:

- `Skip(li)`/`AddRange` 改为 `CopyTo` + `List<T>.RemoveRange` + `InsertRange`;
  或者干脆用两个 `Vector2` ring buffer 索引,避免反复 List 整体重排。
- `intersectionPoints` 改为 `(int gridX, int gridY)` 量化键,或者把它从 set 改为
  单帧累计的 list 并配合 `Vector2` 的相等(已有 `==`)直接线性比较 —— 浮点 hash 
  在 set 里其实只有微弱的去重,可能不抵 hashing 成本。

### 4. 同一谱面对象重复多次查询/解算

- `EnumeratePoints` 顶部已经做了一次 `GetVisibleStartObjects(minTGrid, maxTGrid)`,
  但每个 `curRange` 还会再做一次 `GetVisibleStartObjects(curRange.Min, curRange.Max)`;
  两者结果集大概率高度重叠。
- `calculateXGrid` 内部 `start.GetChildObjectsFromTGrid(calcTGrid)` + 
  `Select(...CalulateXGrid(calcTGrid))` 在 segments 主循环以及 `interpolate()` 内都被
  反复调用,且 `interpolate()` 还会再次 `GenAllPath()`。
- 一段 `DrawPlayField` 调用 `EnumeratePoints(false/true)` 两次,左右墙的"全局开销"
  (查询、Soflan 缓存索引)是完全分开重算的。

**改进方向**:把 lanes/visible-objects 在 `DrawPlayFieldInternal` 顶部按 type 一次性
预取并缓存;`calculateXGrid` 的结果对 `(lane, tGrid)` 做帧内 memoize。

### 5. `points` 用 `PooledSet<float>` 后又 sort,语义上其实是 sorted list

`DrawPlayableAreaHelper.cs:187, 275-284`:

```csharp
using var points = ObjectPool.GetPooledSet<float>();
...
using var sortedPoints = ObjectPool.GetPooledList<float>();
foreach (var x in points) { if (...) sortedPoints.Add(x); }
sortedPoints.Sort(Comparer<float>.Default);
sortedPoints.InsertBySortBy(minTGrid.TotalGrid, x => x);
sortedPoints.InsertBySortBy(maxTGrid.TotalGrid, x => x);
```

- `HashSet<float>` 的去重收益很小(同一交点的浮点 Y 极少正好相等),却付出 hash 成本。
- 真正需要的是"已排序、去重的 Y 列表",可直接用 `List<float>` + `Sort` + linear unique pass。
- `InsertBySortBy` 在已知 min/max 时,其实就是 prepend / append(它们必然落在两端),
  不需要二分 + insert。

### 6. polyline O(N²) 交点扫描

`DrawPlayableAreaHelper.cs:248-264`:

```csharp
for (int r = 0; r < polylines.Count; r++)
  for (int t = r + 1; t < polylines.Count; t++)
    ... GetLinesIntersection(...)
```

对每个 visible range 都是平方级的 segment-pair 测试。已经做了
`a.Item2.Y < b.Item1.Y => break` 的剪枝(因为按 Y 排序),最坏情况仍是 O(N²),
且常数大(每对都构造 `ToSystemNumericsVector2` 等 4 个 Vector2)。

**改进方向**:这块算法上能优化空间有限,但可以(a) 把 `ToSystemNumericsVector2` 在
外层一次转好;(b) 给 `polylines` 加 `maxYSoFar` 提前跳过整段。

### 7. `Default_PropertyChanged` 永不取消订阅

`DrawPlayableAreaHelper.cs:45`:

```csharp
Properties.EditorGlobalSetting.Default.PropertyChanged += Default_PropertyChanged;
```

没有对应的 `-=`。每次打开新编辑器都会 `new DrawPlayableAreaHelper()` 并订阅,
旧 helper 永远不会被 GC,且 `UpdateProps` 会被对全部历史 helper 调用。
这是泄漏而不是单帧瓶颈,但在长开发会话下会逐渐拉慢属性变更广播。

**改进方向**:加 `IDisposable`,在编辑器卸载时 `-=`;或改用 weak event。

### 8. 其它小点(单独 ROI 不高,顺手能改)

- `target.ConvertToY(...)` 在 `interpolate`/`postInterpolatePoint` 里对同一 `pos.Y`
  反复算;可以对一条 lane 的 path 一次性把 (Y, py) 算成数组缓存。
- `result.LastOrDefault()` / `result.FirstOrDefault()` 走 LINQ,对 `IList<Vector2>`
  应直接索引。
- `polygonVertices` 的 `using var ... = ObjectPool.GetPooledList<PolygonVertex>()` 之
  后用 `for` 逐个 `Add`,可在前面 `EnsureCapacity(tessellateList.Count)`。
- `DrawAudioDuration` 每帧重建 `Vector4 color = new(1,0,0,1)`,可提到 `static readonly`
  (可忽略级别,但和上面 debug 颜色风格一致)。

## 建议的优化优先级

1. **#7 解决订阅泄漏** —— 一行修改,价值最高。
2. **#1 + #4 合并改造 `EnumeratePoints`**(缓存 lanes/visible objects/path 拷贝),
   预计是最大的单帧收益来源。
3. **#2 取消中间插入**(把 prepend 路径改成 append + 最终 reverse)。
4. **#3 重写 `AdjustLaneIntersection` 的 swap** —— 若 profile 显示它确实是热点再做,
   逻辑分支多、风险较高。
5. **#5 / #6 / #8 是较小的清理**,可以打包在主优化的同一 PR 里。

## 验证方式(将来真要改时)

- 先用 `dotnet-trace` 抓一段编辑大谱面 + 启用 PlayField 绘制的 trace,确认
  `EnumeratePoints` / `AdjustLaneIntersection` 在 CPU 占比里的实际权重,再决定是否
  值得动 #3。
- 重点关注 `musicId:0840/1119/1011/0591` 这几张图(文件顶部注释里点名的样本),改前
  后都用它们做对照。
- 视觉回归:打开同一张谱、相同视口,用截图工具对比改前/改后多边形几何是否一致
  (`EnablePlayFieldDrawing` 开/关 + 含变速反向的段落 sp1..sp4 都覆盖)。

## 备注

这只是基于源码的静态分析。**没有跑过 profiler**,具体热点占比未知;是否要真正动这个
文件、动到什么程度,建议先抓一次 trace 再决定。
