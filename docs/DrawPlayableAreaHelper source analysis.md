# DrawPlayableAreaHelper 源码解析

本文档记录当前工作区中 `DrawPlayableAreaHelper` 的实际行为、数据流、几何处理流程和
极端条件检查点。它面向后续测试与修复，不替代已有的
`docs/DrawPlayableAreaHelper performance analysis.md` 性能分析。

本文依据的主文件：

- `OngekiFumenEditor/Modules/FumenVisualEditor/Graphics/Drawing/Editors/DrawPlayableAreaHelper.cs`

辅助源码：

- `OngekiFumenEditor/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.Drawing.cs`
- `OngekiFumenEditor/Modules/FumenVisualEditor/Graphics/IFumenEditorDrawingContext.cs`
- `OngekiFumenEditor/Modules/FumenVisualEditor/XGridCalculator.cs`
- `OngekiFumenEditor/Modules/FumenVisualEditor/TGridCalculator.cs`
- `OngekiFumenEditor/Base/Collections/SoflanList_CachedPositionList.cs`
- `OngekiFumenEditor/Base/Collections/ConnectableObjectList.cs`
- `OngekiFumenEditor/Base/OngekiObjects/ConnectableObject/ConnectableStartObject.cs`
- `OngekiFumenEditor/Base/OngekiObjects/ConnectableObject/ConnectableChildObjectBase.cs`
- `OngekiFumenEditor/Utils/MathUtils.cs`

## 职责边界

`DrawPlayableAreaHelper` 做两件事：

1. 在预览模式且启用 `EnablePlayFieldDrawing` 时，按左右墙轨生成可击打区域的填充多边形。
2. 在设计模式时，画一条红色音频结束线。

它不负责绘制墙轨本身。墙轨本体由 `WallLaneDrawTarget` 绘制，并且在预览模式下可被
`HideWallLaneWhenEnablePlayField` 隐藏。可击打区域的背景色也不在 helper 内设置，而是在
`FumenVisualEditorViewModel.Drawing.cs` 的 `GetCleanColor()` 中作为清屏色处理。

## 渲染入口

渲染管线在 `FumenVisualEditorViewModel.Drawing.cs` 中初始化 helper：

- `PrepareRenderLoop(...)` 创建 `playableAreaHelper = new DrawPlayableAreaHelper()`。
- 随后调用 `playableAreaHelper.Initalize(renderImpl)`。

每帧渲染时：

1. `OnEditorRender(...)` 先为每个 Soflan 组构造 `DrawingTargetContext`。
2. 构造可见区间前会读取 `GetViewportTGrid()`；如果 `EditorOffsetMs != 0`，会用
   `GetViewportAudioTime() + EditorOffsetMs` 重新计算可见区间基准 TGrid。
3. 取默认 Soflan 组 `drawingContexts[0]` 作为当前上下文。
4. 对默认上下文的每个 `VisibleTGridRanges` 调用
   `playableAreaHelper.DrawPlayField(this, builder, minTGrid, maxTGrid)`。
5. 之后调用 `playableAreaHelper.Draw(this, builder)`，设计模式下会画音频结束线。
6. 再继续画时间线、XGrid、谱面对象、判定线、玩家位置等内容。

因此可击打区域在大部分谱面对象之前画出，视觉上是底层填充。

## 初始化与设置

`Initalize(...)` 当前只做两件事：

- 调用 `UpdateProps()` 读取 `EditorGlobalSetting.Default`。
- 订阅 `EditorGlobalSetting.Default.PropertyChanged`。

传入的 `IRenderManagerImpl impl` 当前没有被使用。

`UpdateProps()` 读取：

- `EnablePlayFieldDrawing` 到 `enablePlayFieldDrawing`。
- `PlayFieldForegroundColor` 到 `playFieldForegroundColor`。

`Default_PropertyChanged(...)` 只响应：

- `EnablePlayFieldDrawing`
- `PlayFieldForegroundColor`

注意点：

- helper 不读取 `PlayFieldBackgroundColor`，背景色由 editor view model 管。
- 当前没有取消订阅逻辑。多次打开编辑器后，旧 helper 可能继续被全局设置对象引用。
- `Initalize` 拼写保持源码现状。

相关设置默认值来自 `EditorGlobalSetting.settings`：

- `PlayFieldForegroundColor = -16777216`，即 ARGB 黑色。
- `PlayFieldBackgroundColor = -16758145`，由 editor view model 用作背景色。
- `EnablePlayFieldDrawing = True`。
- `HideWallLaneWhenEnablePlayField = False`。

## 成员与缓存

helper 的实例字段和静态字段用途如下：

- `debugLeftColor` / `debugRightColor`：只在 `PLAYFIELD_DEBUG` 下绘制左右边界调试线和点文字。
- `playFieldForegroundColor`：运行时填充色，来自 `PlayFieldForegroundColor`。
- `enablePlayFieldDrawing`：运行时开关，来自 `EnablePlayFieldDrawing`。
- `vertices`：长度为 2 的复用数组，只用于设计模式下的音频结束线。
- `tessellatePoints`：复用的 Earcut 输入点数组，按 `[x0, y0, x1, y1, ...]` 存储。
- `tessellateHoleIndices`：复用的 Earcut hole 索引数组；当前始终清空且不添加 hole。
- `tessellateList`：复用的 Earcut 输出 index 数组，每 3 个 index 表示一个三角形。
- `PolylineByStartYComparer`：按线段起点 Y 排序的 comparer，用于减少交点扫描范围。

这些复用容器都是 helper 实例字段。渲染循环通常在 UI/render 线程串行调用；如果未来把同一个
helper 实例跨线程复用，这些字段不是线程安全的。

## 坐标体系

该 helper 混用三种坐标概念：

- `TGrid.TotalGrid`：谱面时间轴上的整数 grid 值，常用于几何计算和 path 点的 Y。
- `XGrid.TotalGrid`：谱面横向 grid 值，常用于墙轨 path 点的 X。
- view-relative 像素坐标：最终传给绘制命令的屏幕相对坐标。

横向转换：

- `XGridCalculator.ConvertXGridToX(xGridUnit, editor)` 把 XGrid unit 转为屏幕 X。
- helper 内经常先做 `totalXGrid / XGrid.DEFAULT_RES_X`，再传给转换函数。
- 默认左右边界是 `XGrid = -24` 和 `XGrid = 24`，源码里写成：
  - `defaultLeftX = -24 * XGrid.DEFAULT_RES_X`
  - `defaultRightX = 24 * XGrid.DEFAULT_RES_X`

纵向转换：

- `target.ConvertToViewRelativeY(tGridUnit, soflanGroup)` 先通过当前 editor 模式把 TGrid unit 转为 world Y，
  再减去 `CurrentDrawingTargetContext.ViewRelativeOriginY`。
- `DrawPlayFieldInternal(...)` 使用 `target.CurrentDrawingTargetContext.CurrentSoflanList` 作为纵向转换的 Soflan 组。
- 但 `DrawPlayField(...)` 分段时只看默认 Soflan 组：
  `fumen.SoflansMap.DefaultSoflanList.GetCachedSoflanPositionList_PreviewMode(...)`。
- `DrawPlayFieldInternal(...)` 内部的 `currentTGrid` 直接来自 `target.Editor.GetViewportTGrid()`；
  它不会重复套用 `EditorOffsetMs`。因此可见区间可能基于 offset 后的 TGrid，
  边界补点判断却基于未 offset 的 viewport TGrid。

这意味着当前可击打区域逻辑实际上以默认 Soflan 组为核心。源码注释也写着
`todo 暂时显示默认的变速组`。

## 顶层流程：DrawPlayField

`DrawPlayField(...)` 的入口条件：

- 如果是设计模式，直接返回。
- 如果 `enablePlayFieldDrawing` 为 `false`，直接返回。

之后使用默认 Soflan 组的 preview position list：

1. 用 `LastOrDefaultIndexByBinarySearch(fieldMinTGrid, x => x.TGrid)` 找到视口起点所在的 Soflan 点。
2. 用同样方式找到 `fieldMaxTGrid` 所在的 Soflan 点。
3. 构造 `rangeInfos`，第一项是 `(fieldMinTGrid, minIdx 对应 speed)`。
4. 从 `minIdx + 1` 扫到 `maxIdx`，只有当 Soflan speed 与上一段 speed 符号相反时才添加新分段点。
5. 如果最后一个分段点不是 `fieldMaxTGrid`，追加 `(fieldMaxTGrid, 上一段 speed)`。
6. 遍历相邻分段，负 speed 分段直接跳过，正 speed 分段进入 `DrawPlayFieldInternal(...)`。

分段标志：

- 第一个分段带 `FieldRangeParam.FirstRange`。
- 最后一个分段带 `FieldRangeParam.LastRange`。
- 如果视口内只有一个正向分段，它可能同时带 First 和 Last。

关键语义：

- 反向变速段不单独画。源码注释认为它们已被正向段覆盖。
- speed 为 0 时不会因为符号相反而分段，也不会被 `< 0` 跳过，所以会按非反向段进入内部绘制。
- 分段只在 speed 正负号变化处切开，不在同符号但倍率变化处切开。

极端条件关注：

- 如果默认 Soflan position list 为空，`soflanList[minIdx]` 会越界。正常谱面应至少有初始 BPM/Soflan 点。
- 如果可见区间完全落在负 speed 段，可能完全不画可击打区域。
- 如果 speed 为 0，内部纵向换算可能产生重合 Y 或不可见覆盖，需要专门测试。
- First/Last 标志按 `rangeInfos` 原始索引判断，即使中间有负 speed 段被跳过，边界补点逻辑仍按可绘制段自身收到的 flag 执行。

## 内部流程：DrawPlayFieldInternal

`DrawPlayFieldInternal(...)` 的核心步骤：

1. 分别枚举左墙点和右墙点。
2. 修正左右点列交叉，使左墙边界尽量保持在右墙左侧。
3. 将左点列和反向右点列拼成一个闭合外轮廓。
4. 使用 Earcut 做三角剖分。
5. 将三角形顶点提交给 `builder.DrawPolygon(Primitive.Triangles, ...)`。

内部没有创建 hole，`tessellateHoleIndices` 每次清空后保持为空。

可击打区域最终轮廓顺序是：

1. `leftPoints` 从下到上或按枚举顺序加入。
2. `rightPoints` 从末尾到开头反向加入。

这要求左右点列本身顺序稳定，且交叉修正后不产生严重自交，否则 Earcut 的结果可能异常。

## 左右墙点枚举：EnumeratePoints

`EnumeratePoints(bool isRight, IList<Vector2> result)` 对左右墙执行同一套逻辑：

- `isRight == false` 时处理 `LaneType.WallLeft`，默认边界是 `-24`。
- `isRight == true` 时处理 `LaneType.WallRight`，默认边界是 `24`。

### 1. 合并墙轨覆盖区间

先查询 `[minTGrid, maxTGrid]` 范围内可见的墙轨 start object：

```csharp
fumen.Lanes.GetVisibleStartObjects(minTGrid, maxTGrid)
```

再按 `LaneType` 过滤，并把每条墙轨的 `[MinTGrid.TotalGrid, MaxTGrid.TotalGrid]`
变成 `CombinableRange<int>`。这些 range 会被 `CombineRanges(...)` 合并重叠区间。

结果排序规则：

- 右墙按 `range.Max` 排序。
- 左墙按 `range.Min` 排序。

注意：`CombineRanges(...)` 内部已经按 `Min` 排序并合并，外面又按左右规则重新排序。

如果某一侧完全没有墙轨，`ranges` 为空。后续仍会插入 `minTGrid` 和 `maxTGrid`，
所以通常能生成默认 `±24` 的竖边。

### 2. 收集关键 Y 点

对每个合并后的 `curRange`：

1. 再次查询这个 range 内可见且类型匹配的墙轨。
2. 对每条 lane 调用 `GenAllPath()`，把相邻 path 点组成线段。
3. 线段只保留 `pos.Y` 落在当前 `DrawPlayFieldInternal` 的 `[minTGrid.TotalGrid, maxTGrid.TotalGrid]`
   内的部分。
4. 所有线段按起点 Y 排序。
5. 两两测试线段是否相交，相交则把交点的 Y 加入 `points`。
6. 把 lane 起点 Y 和最后一个 child 的 Y 加入 `points`。

交点扫描有一个边界行为：如果 path 点落在 `[minTGrid.TotalGrid, maxTGrid.TotalGrid]`
之外，当前 lane 的 `hasPrev` 会被重置。也就是说，跨越绘制边界、但只有一个端点在范围内的
线段不会参与这一步的墙轨间交点扫描。

这里的 `GenAllPath()` 来自 `ConnectableStartObject`：

- 它遍历所有 child 的 `GetConnectionPaths()`。
- `GetConnectionPaths()` 对直线段会返回前驱点和当前 child 点。
- 对曲线段会按 `CurvePrecision` 采样贝塞尔点。
- 默认会过滤连续重复点。

因此 `points` 中的分割 Y 包括：

- 墙轨头尾。
- 曲线采样线段之间的交点。
- 多条墙轨交叉导致的交点。

### 3. 排序并补上绘制边界

`points` 是 `PooledSet<float>`。之后：

1. 过滤出严格位于 `(minTGrid.TotalGrid, maxTGrid.TotalGrid)` 内的点。
2. 排序。
3. 用 `InsertBySortBy` 插入 `minTGrid.TotalGrid`。
4. 用 `InsertBySortBy` 插入 `maxTGrid.TotalGrid`。

得到 `sortedPoints` 后，用相邻两点构造小区间 `[fromY, toY]`。

注意：这里不会去重 `minTGrid` 与 `maxTGrid`。如果两者相等，仍可能形成零高度区间。

### 4. 每个小区间选择代表墙轨

对每个 `[fromY, toY]`：

1. 取 `midY = (fromY + toY) / 2`。
2. 转成 `midTGrid = TGrid.FromTotalGrid((int)midY)`。
3. 查询 `midTGrid` 处可见且类型匹配的墙轨。
4. 对每条候选 lane 调 `calculateXGrid(lane, midTGrid)`。该函数会先取
   `start.GetChildObjectsFromTGrid(midTGrid)`，再对同一 TGrid 命中的 child 计算 XGrid；
   右墙取 child XGrid 最大值，左墙取 child XGrid 最小值。
5. 如果是右墙，选择 XGrid 最大的 lane。
6. 如果是左墙，选择 XGrid 最小的 lane。

这个规则用于处理同一水平层面有多条墙轨重叠或交叉的情况。它尝试取最外侧的墙：

- 左边界取最左。
- 右边界取最右。

如果选中 `pickLane`：

1. 在 `fromY` 处计算 lane X 并添加点。
2. 把 `pickLane.GenAllPath()` 中 Y 位于 `[fromY, toY]` 的 path 点加入结果。
3. 在 `toY` 处计算 lane X 并添加点。

这里选择 `pickLane` 时使用局部 `calculateXGrid(...)`，会在同一 TGrid 命中多个 child
时按左右规则取最外侧 child；但真正添加 `fromY/toY` 端点时调用的是
`pickLane.CalulateXGrid(...)`，它走 `GetChildObjectFromTGrid(...).FirstOrDefault()`。
如果同一 TGrid 上有多个 child，选 lane 与端点计算可能不是完全同一个 child 语义。

如果没有选中 lane：

- 使用默认 `XGrid = -24` 或 `24`，在 `fromY` 和 `toY` 各添加一个点。

### 5. 边界插值补点

枚举完普通点后，源码有一段“解决变速过快过慢导致的精度丢失问题”的补点逻辑。

它只在当前内部绘制段带 First 或 Last 标志时执行：

- LastRange 且 `currentTGrid <= maxTGrid` 时，处理上边界。
- FirstRange 且 `minTGrid <= currentTGrid` 时，处理下边界。

补点原因：

- `minTGrid/maxTGrid` 由可见 TGrid 区间决定。
- 实际视口上下边界是像素 Y。
- 高速或低速 Soflan 下，TGrid 区间边界和屏幕边界可能不重合。
- 如果不按实际屏幕边缘插值，填充多边形可能在视口边缘露出缝隙。

`interpolate(...)` 的流程：

1. 把目标 view-relative Y 加上 `ViewRelativeOriginY` 变成 world Y。
2. 调 `ConvertYToTGrid_PreviewMode(actualWorldY)`，一个 Y 可能对应多个 TGrid。
3. 对这些 TGrid 查询可见墙轨。
4. 额外检查 lane 的 `ConvertToViewRelativeY(MinTGrid/MaxTGrid)` 是否覆盖 `actualY`。
5. 按左右规则选择最外侧 lane。
6. 如果选中 lane，就遍历它的 path，在 view Y 坐标中找到跨过 `actualY` 的相邻点并线性插值出 X。
7. 如果没有 lane，则返回默认 `±24` 边界点。

插值查找使用 `curPy > actualY` 判断跨越点。如果 `actualY` 正好等于最后一个 path 点的
view Y，循环不会进入插值返回分支；在已选中 lane 的情况下也不会回退到默认边界。

`postInterpolatePoint(...)` 根据插值结果决定插到结果头部还是尾部：

- LastRange 插到尾部。
- FirstRange 插到头部。

如果插值不是来自 lane，而是默认边界，它还会额外补一个默认边界点，用来连接已有边界。

### 6. 点列简化

最后遍历 `result` 做两类简化：

1. 删除连续完全相同的点。
2. 如果连续三个点同 Y 或同 X，删除中间点。

这会减少重复点和直线上的冗余点，但也意味着某些极端情况下的“水平折返”或“竖向停顿”
可能被合并掉。测试时要确认这种合并没有改变预期轮廓。

## 添加点的局部规则

`appendPoint2(...)` 把谱面 grid 坐标转为 view-relative 像素坐标，再调用 `appendPoint3(...)`。

`appendPoint3(...)` 的去重逻辑是：

```csharp
var po = list.ElementAtOrDefault(insertIdx);
if (po.X == px && po.Y == py)
    return;
list.Insert(insertIdx, p);
```

这有一个重要边界：

- 当 `insertIdx == list.Count` 时，`ElementAtOrDefault` 返回 `default(Vector2)`。
- 如果要插入的点正好是 `(0, 0)`，会被误判为已存在并跳过。
- 当列表为空并插入 `(0, 0)` 时也会发生。

这不一定是当前线上问题，但它是测试极端条件时应覆盖的点，尤其是：

- 视口左上角附近的点。
- XGrid 转换后 X 为 0。
- Y 转换后 view-relative Y 为 0。

## 交叉修正：AdjustLaneIntersection

`AdjustLaneIntersection(...)` 负责修正左右边界点列。它假设输入的 `leftPoints` 和
`rightPoints` 都至少有一个点。

内部临时结构：

- `tempLeft`、`tempRight`：交换尾段时暂存点。
- `intersectionPoints`：记录已经处理过的交点，避免重复插入。

入口会先调用一次 `tryExchange(0, 0)`，用于处理起点处左右已经反了的情况。

### tryExchange 的语义

`tryExchange(li, ri)` 判断从 `li` 和 `ri` 开始，左右剩余点列是否需要整体交换。

判断过程：

1. 如果当前左右点相等，就同步向后找第一个不同点。
2. 分别取左右当前点的前一个点，起点没有前一个点时用 `(X, Y - 10)` 构造一个向上的虚拟前点。
3. 得到 `leftVector` 和 `rightVector`。
4. 计算二维叉积。
5. 如果近似共线，则当 `lp.X > rp.X` 时交换。
6. 如果叉积大于 0，也交换。
7. 否则不交换。

交换本身是：

1. 把 `leftPoints[li..]` 复制到 `tempLeft`。
2. 把 `rightPoints[ri..]` 复制到 `tempRight`。
3. 删除左右原尾段。
4. 把右尾段接到左边，把左尾段接到右边。

也就是说，一旦判断需要交换，交换的是从当前位置开始的整个剩余边界，而不是单个点。

### 主循环

主循环从 `leftIdx = 1`、`rightIdx = 1` 开始，每次比较：

- 左线段：`leftPoints[leftIdx - 1] -> leftPoints[leftIdx]`
- 右线段：`rightPoints[rightIdx - 1] -> rightPoints[rightIdx]`

如果两条线段完全相同，两个 index 同时前进。

否则先用 `GetLinesIntersection(...)` 求交。该函数：

- 对 `p1 == q1` 或 `p2 == q2` 直接返回端点。
- 对平行或共线线段返回 `null`。
- 对普通相交返回交点，并用 `epsilon = 1e-6f` 放宽端点判断。

### 水平同 Y 特殊处理

如果左右线段四个端点全在同一个 Y 上，进入特殊分支。源码注释画了 CASE A/B/C：

- CASE A 和 CASE B：水平重叠且左右关系反转时，尝试 `tryExchange(leftIdx, rightIdx)`。
- CASE C：当前线段本身没有足够信息时，检查前一条或后一条相邻线段与对侧当前线段是否相交。

CASE C 最多尝试四组邻近线段：

1. 前一条 left vs 当前 right。
2. 当前 left vs 前一条 right。
3. 下一条 left vs 当前 right。
4. 当前 left vs 下一条 right。

找到新交点后会插入到另一侧点列，并前进对应 index。

### 普通交点处理

如果发现新交点，先加入 `intersectionPoints` 并进入两类处理。

第一类是穿越交叉：

- 交点不等于任一线段端点时，认为是 cross。
- 如果左右线段的 `to` 同时等于交点，也强制视为 cross。
- 对水平参与的 cross，左右都插入交点，然后 `tryExchange(...)`。
- 对非水平 cross，先 `tryExchange(...)`，成功后再左右都插入交点。

第二类是端点接触或突出：

- 如果只有一侧线段端点命中交点，另一侧先补同一个交点。
- 根据交点是 from 还是 to 调整 index。
- 计算当前左右点 X 的中点，构造 `centerPoint = (centerX, intersectionPoint.Y)`。
- 左右两侧都尝试插入中点，用来把突出的局部轮廓拉回中间。
- 最后调用 `tryExchange(...)`。

如果左右都在端点命中，源码认为可能是 V 型或 A 型：

- V 型：某侧 `to.Y > intersectionPoint.Y`。
- A 型：某侧 `from.Y < intersectionPoint.Y`。
- 根据形态和交点位置回退 index，再 `tryExchange(...)`。

如果没有交点，主循环按 Y 覆盖关系决定前进哪一侧：

- 如果左线段终点 Y 落在右线段 Y 范围内，左 index 可以前进。
- 如果右线段终点 Y 落在左线段 Y 范围内，右 index 可以前进。
- 两者都满足则一起前进。
- 都不满足也一起前进。

这个推进逻辑隐含要求左右点列整体按 Y 接近单调。如果点列出现大幅回头，可能漏掉交点或错误交换。

## 三角剖分与绘制

交叉修正后：

1. 清空 `tessellatePoints`、`tessellateHoleIndices`、`tessellateList`。
2. 依次加入左点列的 `(X, Y)`。
3. 逆序加入右点列的 `(X, Y)`。
4. 调用 `Earcut.Tessellate(...)`。
5. 按 `tessellateList` 每三个 index 组成一个三角形。
6. 每个三角形顶点使用 `playFieldForegroundColor`。
7. 提交 `builder.DrawPolygon(Primitive.Triangles, polygonVertices)`。

在 `PLAYFIELD_DEBUG` 编译符号下：

- 每个三角形会按 HSL hash 生成不同颜色。
- 背景清屏色会被设为纯黑。
- 会画出左右边界线、三角形轮廓、点坐标和交点索引。

## 设计模式音频结束线

`Draw(...)` 在设计模式下调用 `DrawAudioDuration(...)`。

`DrawAudioDuration(...)`：

1. 用 `TotalDurationHeight - ViewRelativeOriginY` 得到音频结束位置的 view-relative Y。
2. 构造两个点 `(0, y)` 和 `(ViewWidth, y)`。
3. 用红色实线调用 `builder.DrawSimpleLines(vertices, 3)`。

该逻辑和可击打区域无关，但同在 helper 中。

## 与其他模块的关系

### 背景色

`FumenVisualEditorViewModel.Drawing.cs` 中：

- 设计模式或关闭 play field 时，清屏色是 `(16, 16, 16, 1)`。
- 预览模式且开启 play field 时，清屏色使用 `PlayFieldBackgroundColor`。
- `PLAYFIELD_DEBUG` 下强制黑色背景。

### 墙轨可见性

`WallLaneDrawTarget.DrawBatch(...)` 中：

- 如果是预览模式且 `HideWallLaneWhenEnablePlayField` 为 true，墙轨线条不画。
- 这个设置不影响 `DrawPlayableAreaHelper` 的边界计算。

### 离线 SVG 生成器

`DefaultPreviewSvgGenerator.SerializePlayField(...)` 有一段相似但被注释掉的历史逻辑：

- 使用同样的默认左右边界 `±24`。
- 也按墙轨区间、交点和最外侧 lane 生成左右点。
- 但它没有当前 helper 的 Soflan 边界插值和复杂交叉修正。

因此它只能作为算法来源参考，不能作为当前运行时行为的准确信息源。

## 当前实现中的弱点与疑点

这些点不一定都是 bug，但适合转成测试或修复前的断言。

1. `appendPoint3` 可能误跳过 `(0, 0)` 插入。
2. `DrawPlayField` 依赖默认 Soflan 组，暂不真正支持按各个 Soflan 组分别计算可击打区域。
3. 可见区间可能使用 `EditorOffsetMs` 后的 TGrid，`DrawPlayFieldInternal` 的 `currentTGrid`
   判断却使用未 offset 的 `GetViewportTGrid()`。
4. 反向 speed 段被完全跳过，假设它们已被正向段覆盖。
5. speed 为 0 会进入内部绘制，可能产生零高度或多点重合。
6. `interpolate(...)` 中 lane 覆盖判断使用 `laneMinY <= actualY && actualY <= laneMaxY`，如果 view Y 因反向或异常路径导致上下颠倒，可能漏选 lane。
7. `GetLinesIntersection(...)` 对共线返回 null，所以共线重叠必须依赖 `AdjustLaneIntersection` 的水平同 Y 特殊分支。
8. `AdjustLaneIntersection` 假设左右点列按 Y 大体单调。
9. 墙轨交点扫描会忽略跨越绘制边界但只有一个端点落在范围内的线段。
10. 选择代表 lane 时的 `calculateXGrid(...)` 与添加端点时的 `pickLane.CalulateXGrid(...)`
    在同 TGrid 多 child 场景下语义不完全一致。
11. `interpolate(...)` 对 `actualY` 等于最后一个 path 点 view Y 的情况可能不返回点。
12. `tryExchange` 一次交换剩余整段，局部误判会影响后续全部轮廓。
13. `AdjustLaneIntersection` 的局部 `insert(...)` 只检查 `list[idx]`，不检查 `list[idx - 1]`，
    可能插入与前一个点重复的点。
14. `tryGetLine(...)` 要求 `1 < lineIdx && lineIdx < linePoints.Count`，因此 `lineIdx == 1`
    这种本来可构造 `points[0] -> points[1]` 的线段不会被该辅助函数返回。
15. `intersectionPoints` 用 `HashSet<Vector2>` 精确记录浮点交点，近似相等的交点不会被合并。
16. Earcut 前没有显式验证多边形是否自交、面积是否为 0、点数是否足够。
17. `PLAYFIELD_DEBUG` 下 `debugDrawEnumeratedPoints(...)` 会把 `playFieldForegroundColor.W`
    改成 `0.4f`，该字段是实例状态。
18. 全局设置订阅没有释放路径，长期会话存在泄漏风险。
19. `curSoflanPoint` 被赋值但不参与后续逻辑；`prevX`、`prevY`、`insertLeftIdx`、
    `insertRightIdx` 当前无实际用途。

## 极端条件测试矩阵

建议把下面场景作为测试与截图回归的基础。

### 模式与设置

- 设计模式：只应画音频结束线，不画可击打区域。
- 预览模式且 `EnablePlayFieldDrawing = false`：不画可击打区域，背景回到普通深色。
- 预览模式且 `EnablePlayFieldDrawing = true`：画可击打区域。
- `HideWallLaneWhenEnablePlayField` 开/关：只影响墙轨线条，不应影响填充区域几何。
- 修改 `PlayFieldForegroundColor` 后，helper 应更新填充色。
- 修改 `PlayFieldBackgroundColor` 后，应由 editor view model 更新背景色。

### Soflan 与视口

- 无反向变速的普通段。
- 视口跨越正向到反向再到正向的段。
- 视口完全在反向段内。
- 视口边界刚好等于 Soflan 变化点。
- `EditorOffsetMs` 为 0、正数、负数时的同一视口。
- speed 为 0 的段。
- 极大 speed 和极小 speed，重点看视口上下边缘是否露缝。
- 同一个 Y 映射到多个 TGrid 的预览模式场景。
- `fieldMinTGrid == fieldMaxTGrid` 或非常短的可见区间。

### 墙轨形态

- 完全没有左墙或右墙：应退回默认 `±24`。
- 只有单侧墙轨。
- 左右墙都存在但不交叉。
- 左右墙交叉一次。
- 多次交叉。
- 水平同 Y 重叠。
- V 型接触。
- A 型接触。
- 左右同端点接触。
- 左右边界在某段出现 `left.X > right.X`。
- 曲线墙轨，包含不同 `CurvePrecision`。
- 无效曲线路径，即 `isVaild == false` 的 path。
- 连续重复点和连续三点同 X 或同 Y。

### 坐标边界

- 插入点刚好是 `(0, 0)`。
- XGrid 转换后落在视口左边界或右边界。
- Y 转换后落在 view-relative `0` 或 `ViewHeight`。
- 默认边界 `±24` 落到视口外。
- `ViewWidth == 0` 或 `ViewHeight == 0` 的渲染初始化边界。

## 修复时建议保持的行为不变量

后续修改 `DrawPlayableAreaHelper` 时，建议至少验证这些不变量：

- `leftPoints.Count >= 2` 且 `rightPoints.Count >= 2`。
- 点坐标不包含 NaN 或 Infinity。
- 点列中没有连续完全重复点。
- 对同一 Y 或很接近的 Y，左边界 X 不应大于右边界 X。
- FirstRange 和 LastRange 需要覆盖实际视口边缘，不能露出背景缝。
- 生成给 Earcut 的点数至少 3 个，且外轮廓面积非 0。
- `tessellateList.Count` 应为 3 的倍数。
- `tessellateList` 中所有 index 都在 `tessellatePoints.Count / 2` 范围内。
- 关闭 `EnablePlayFieldDrawing` 不应改变其他谱面对象绘制。
- `HideWallLaneWhenEnablePlayField` 不应改变可击打区域几何。

## 调试建议

- 临时启用 `PLAYFIELD_DEBUG` 可以看到左右点列、交点索引和 Earcut 三角形。
- 对疑难谱面记录以下计数：visible ranges 数量、左右 `ranges` 数量、`points` 数量、
  `leftPoints/rightPoints` 数量、交点数量、Earcut 三角形数量。
- 文件顶部注释提到的 `musicId:0840/1119/1011/0591` 应作为历史问题样本优先回归。
- 修复前后建议固定同一谱面、同一播放时间、同一视口大小截图对比。
- 性能问题另见 `DrawPlayableAreaHelper performance analysis.md`，不要把性能重构和几何修复混在同一次大改里，除非已经有截图或单元测试保护几何行为。
