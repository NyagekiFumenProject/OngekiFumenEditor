# DrawPlayableAreaHelper_new 重构计划

本文档用于记录 `DrawPlayableAreaHelper_new.cs` 的设计决策、问题追踪和实现约束。
它以 `docs/DrawPlayableAreaHelper source analysis.md` 为当前实现行为基线，并结合
`D:\sddt155\docs\field-object-area-shape-algorithm.md` 中记录的 MU3 运行时 FieldObject 算法进行取舍。

## 当前共识

- 新实现目标文件暂定为 `OngekiFumenEditor/Modules/FumenVisualEditor/Graphics/Drawing/Editors/DrawPlayableAreaHelper_new.cs`。
- 旧实现 `DrawPlayableAreaHelper.cs` 先保留，用于对照、截图回归和逐步切换。
- 当前 helper 是编辑器侧 2D polygon 填充算法，直接从 `WallLeft` / `WallRight` 墙轨生成左右边界点列，再用 Earcut 三角化。
- MU3 运行时算法是 `FieldObject.AreaData -> JointField.LimitParam` 的截面序列算法，核心语义是 `placeBef/placeAft`、`prev.Aft -> next.Bef` 插值、可见范围采样和 Soflan 后的 Z 轴显示。

## 实现记录

- 已新增 `DrawPlayableAreaHelper_new.cs`。
- 已将 `FumenVisualEditorViewModel.Drawing.cs` 的主渲染路径从 `DrawPlayableAreaHelper` 切换到 `DrawPlayableAreaHelper_new`。
- 新 helper 第一版保持旧公开方法形状：`Initalize(...)`、`Draw(...)`、`DrawPlayField(...)`。
- 新 helper 第一版在预览模式始终尝试绘制 Playfield，设计模式只绘制音频结束线。
- 新 helper 第一版使用默认 Soflan 组、`WallLeft` / `WallRight`、实时局部采样、相邻截面双三角形绘制。
- 新 helper 第一版不读取 `EditorGlobalSetting`，填充色使用配置默认值语义的不透明黑色。
- 旧 `DrawPlayableAreaHelper.cs` 保留在代码库中作为源码对照，但不再被主渲染路径实例化。

## 待确认决策

### D1: 新实现的兼容目标

问题：`DrawPlayableAreaHelper_new.cs` 应该优先像素级兼容当前 `DrawPlayableAreaHelper.cs`，还是优先贴近 MU3 运行时 `FieldObject/AreaData/JointField` 算法？

推荐答案：优先贴近 MU3 运行时算法；旧 helper 仅作为回归参考，不作为必须复刻的行为。原因是当前 helper 的交叉修复、Soflan 分段和多边形填充是编辑器特化实现，已和运行时 FieldObject 产生结构性差异。如果新 helper 的目标仍是复刻旧 helper，重构收益主要是代码整理，而不是算法一致性提升。

“运行时算法一致性”在本计划中的含义：

- 不要求复刻 Unity/MU3 的具体渲染 API、材质、世界坐标数值或最终像素。
- 要求复刻运行时生成场地形状的语义模型：`WallLeft/WallRight -> Border/Area -> LimitParam`。
- 要求保留 `placeBef/placeAft` 的前后截面语义，而不是只保留一条屏幕空间折线。
- 要求关键帧之间按 `prev.Aft -> next.Bef` 插值。
- 要求可见范围采样点遵循运行时思路，包括可见边界、当前帧附近、出现线附近和关键 Area 截面。
- 当旧 helper 输出和运行时语义冲突时，新 helper 优先选择运行时语义，旧 helper 只作为问题定位和回归参考。

状态：用户倾向运行时算法一致性优先，但仍在澄清实现边界。

### D2: AreaData 的计算范围

问题：新 helper 是否需要在加载谱面时预先计算完整谱面的 AreaData，还是每帧只对当前可见范围实时计算局部 AreaData？

推荐答案：每帧实时计算局部 AreaData，但需要带上下文扩展，不应只截取严格视口范围。原因是编辑器内谱面对象会频繁编辑，完整缓存需要复杂失效逻辑；现有集合已经支持 `GetVisibleStartObjects(min, max)` 和按 `TGrid` 计算墙轨 XGrid，适合局部构建。风险是运行时插值依赖相邻关键截面，局部构建必须包含视口前一个有效截面和视口后一个有效截面，否则 `prev.Aft -> next.Bef` 会在边界处断裂。

建议方案：

- 新增局部构建器，例如 `EditorFieldAreaDataBuilder`。
- 每帧输入 `visibleMinTGrid`、`visibleMaxTGrid`、`currentTGrid` 和 Soflan/屏幕采样需求。
- 构建器只枚举“绘制所需范围 + 上下文 padding”内的 `WallLeft` / `WallRight`。
- 对 `visibleMinTGrid` 之前最近的左右边界状态做一次查询，作为插值前置截面。
- 对 `visibleMaxTGrid` 之后最近的左右边界状态做一次查询，作为插值后置截面。
- 输出不是完整谱面 AreaData，而是当前绘制帧需要的 Area/LimitParam 截面列表。
- 如果后续性能不足，再加版本号缓存：按 fumen edit version、visible range、Soflan group 缓存局部结果。

状态：已确认。采用实时局部 AreaData/LimitParam 构建，不预先计算完整谱面；局部范围必须带前后上下文。

### D3: 缺失墙轨时的默认边界

问题：当局部绘制范围内没有某侧墙轨，或某个采样 TGrid 无法从该侧墙轨得到 XGrid 时，新 helper 应该如何回退？

结论：采用运行时语义。左边界缺失时回退 `-24`，右边界缺失时回退 `24`，左右独立处理。

实现要求：

- 默认左边界：`-24 * XGrid.DEFAULT_RES_X`。
- 默认右边界：`24 * XGrid.DEFAULT_RES_X`。
- 回退发生在 Area/Border 构建阶段，而不是最终屏幕多边形修补阶段。
- 缺左不影响右侧真实边界；缺右不影响左侧真实边界。
- 如果两侧都缺失，该段生成默认场地 `[-24, 24]`。

状态：已确认。

### D4: Field 边界数据源

问题：编辑器中的哪些谱面对象可以作为新 helper 的 Field 边界来源？

结论：第一版只使用 `LaneType.WallLeft` 和 `LaneType.WallRight`。它们分别对应运行时 `Reader.Field.create()` 使用的 `laneObjMatrix[3]` 和 `laneObjMatrix[4]`。

明确不纳入第一版边界源：

- `LaneType.SideLeft` / `LaneType.SideRight`。
- Side Tap / Side Hold 推导出的墙面显示范围。
- 普通 `LaneLeft` / `LaneCenter` / `LaneRight`。
- `LaneBlockArea` / oneway block。
- 旧 helper 的交叉修复结果或额外推断边界。

原因：

- 运行时 Field 边界源在文档中明确是 `WallLeft` / `WallRight`。
- Side Tap/Hold 相关逻辑属于墙面显示轨道或一方通行/可视化链路，不应污染 Field Area 的边界语义。
- 第一版需要先建立可验证的运行时一致性基础，再讨论扩展兼容旧编辑器显示。

状态：已确认。

### D5: 同一 TGrid 多条同侧墙轨的选择规则

问题：如果同一个 `TGrid` 上存在多条 `WallLeft` 或多条 `WallRight`，新 helper 应该选择哪一条作为 Field 边界？

结论：采用外侧边界语义。左侧取最小 XGrid，右侧取最大 XGrid。

实现要求：

- 查询某个采样 TGrid 的左边界时，枚举覆盖该 TGrid 的所有 `LaneType.WallLeft`，计算 XGrid 后取最小值。
- 查询某个采样 TGrid 的右边界时，枚举覆盖该 TGrid 的所有 `LaneType.WallRight`，计算 XGrid 后取最大值。
- 如果候选墙轨存在但某条无法在该 TGrid 计算 XGrid，则跳过该候选，不让它覆盖有效候选。
- 如果所有候选都无法计算 XGrid，则按 D3 回退默认边界。

状态：已确认。

### D6: 曲线墙轨的采样策略

问题：曲线墙轨是否需要在 AreaData 构建阶段预先展开成大量曲线采样点？

结论：不预展开整条曲线。AreaData/LimitParam 的 XGrid 在绘制所需的采样 TGrid 上实时计算。

实现要求：

- 墙轨节点时间可以作为关键采样来源。
- 可见边界、当前帧、出现线附近、Soflan/视口必要位置可以作为额外采样来源。
- 对每个采样 TGrid，调用墙轨现有计算逻辑取得 XGrid，例如 `CalulateXGrid(tGrid)` 或同等的多候选外侧查询函数。
- 不把 `GenAllPath()` 的所有曲线采样点直接提升为 Area 关键帧。
- `GenAllPath()` 可在必要时用于发现复杂曲线与采样范围的辅助信息，但不作为 AreaData 的主数据模型。

原因：

- 运行时 Field 算法本质是按截面查询/插值，不是先把所有曲线展开为屏幕折线。
- 编辑器内实时编辑频繁，全量曲线展开会增加每帧成本和缓存失效复杂度。
- 采样点由 Field 绘制需求决定，有利于让新 helper 接近 `moveNotesField()` 的截面序列语义。

状态：已确认。

### D7: 最终绘制拓扑

问题：新 helper 的最终绘制是否继续使用旧 helper 的“左右点列闭合为大 polygon，然后 Earcut 三角化”方案？

结论：不使用 Earcut 作为核心绘制拓扑。新 helper 采用运行时 `JointField` 思路，按相邻截面生成四边形段。

实现要求：

- 构建有序的 Field 截面列表，例如 `LimitParam[]`。
- 每两个相邻截面生成一个可击打区域四边形段。
- 当前截面使用 `xAftL` / `xAftR`。
- 下一个截面使用 `xBefL` / `xBefR`。
- 第一版不依赖 `AdjustLaneIntersection()` 交换左右尾段来保证几何正确性。
- 第一版不把所有点拼成单个外轮廓，也不调用 Earcut。
- 如果某个相邻截面生成退化四边形，局部跳过或降级处理，不影响整段可见区域。

原因：

- 文档中的 `JointField.set()` 是相邻 `LimitParam` 成段连接，而不是任意多边形三角剖分。
- 继续使用 Earcut 会把新实现重新绑定到旧 helper 的多边形修补模型。
- 按段绘制更容易定位错误：某个截面或某个段错了，不会污染整个 polygon。

状态：已确认。

### D8: 第一版绘制层级

问题：第一版 `DrawPlayableAreaHelper_new.cs` 是否需要复刻运行时 `JointField` 的所有视觉层？

结论：第一版只绘制可击打区域填充，也就是 inner field quad strip。

第一版包含：

- 相邻截面之间的场地内侧填充。
- 当前截面使用 `xAftL` / `xAftR`。
- 下一截面使用 `xBefL` / `xBefR`。
- 使用现有 `PlayFieldForegroundColor` 作为填充色。

第一版不包含：

- 左右外侧伤害区域。
- 左右墙体。
- 关键截面处 `Bef != Aft` 的横向墙。
- Ground 网格。
- 运行时淡入淡出 `_widthAmp`。
- one-way 墙可视化。

原因：

- 当前 helper 的核心职责是 Playfield 填充。
- 先验证 Area/LimitParam 语义和采样规则，再扩展视觉层。
- 避免把 Field 几何正确性和完整运行时视觉复刻混在同一轮实现。

状态：已确认。

### D9: Bef/Aft 截面语义

问题：编辑器墙轨对象在某个 TGrid 上通常只能直接查询一个 XGrid，新 helper 应该如何生成运行时 `Area` 的 `placeBef*` / `placeAft*`？

结论：第一版普通采样默认 `Bef == Aft`。当采样 TGrid 位于墙轨 start/end 边界、同一墙轨在同一 TGrid 上存在连续多个 child 节点，或能从现有对象模型可靠推导进入/离开截面时，生成 `Bef != Aft`。

实现要求：

- 对普通采样 TGrid：
  - `placeBefL = placeAftL = leftX`
  - `placeBefR = placeAftR = rightX`
- 对墙轨节点或断点，如果无法可靠判断运行时 `xFore/xRear`，仍保持 `Bef == Aft`。
- 对同一 TGrid 上连续多个 child 节点：
  - 同一条墙轨先保持 child 顺序，首个同刻 child 的 XGrid 作为 `Bef`。
  - 末个同刻 child 的 XGrid 作为 `Aft`。
  - 多条同侧墙轨重叠时，再对 `Bef` / `Aft` 分别按外侧边界聚合：左侧取最小值，右侧取最大值。
- 对墙轨 start/end 边界：
  - `Bef` 查询使用在该 TGrid 前侧仍有效的墙轨。
  - `Aft` 查询使用在该 TGrid 后侧仍有效的墙轨。
  - 某侧前后任一方向没有有效墙轨时，按 D3 回退默认边界。
- 不为了模拟运行时厚截面而猜测或伪造 `Bef/Aft`。
- 后续如果找到明确的编辑器对象到 `xFore/xRear` 映射，再扩展该逻辑。

原因：

- 文档里的 `Border.xFore/xRear` 是运行时 Reader 侧数据结构。
- 当前编辑器 `ConnectableStartObject` / `ConnectableChildObjectBase` 更像连续路径模型，直接查询通常只得到一个 XGrid。
- 错误伪造 `Bef/Aft` 会导致局部横向墙或四边形段比保守退化更难调试。

状态：已确认，并已在 `DrawPlayableAreaHelper_new` 中实现同 TGrid 多 child 与 start/end 边界的 `Bef/Aft` 拆分。

### D10: 局部截面采样点集合

问题：每帧实时构建局部 Area/LimitParam 时，采样点集合应该包含哪些 TGrid？

结论：第一版采样点包含关键边界、墙轨节点、Soflan 断点和受控间隔补采样。采样点去重后按 TGrid 升序排序。

第一版采样来源：

- `visibleMinTGrid`。
- `visibleMaxTGrid`。
- `currentTGrid`，如果落在或需要投影到当前绘制范围。
- 可见范围内 `LaneType.WallLeft` / `LaneType.WallRight` 的 start TGrid。
- 可见范围内 `LaneType.WallLeft` / `LaneType.WallRight` 的 end TGrid。
- 可见范围内墙轨 child 节点 TGrid。
- 可见范围内默认 Soflan 组的断点 TGrid。
- 受控间隔补采样点，用于避免曲线或长斜线被过少截面粗略连接。

约束：

- 不直接把 `GenAllPath()` 的所有点提升为 Area 采样点。
- 所有采样点必须 clamp 或过滤到局部构建范围内。
- 相邻重复或非常接近的采样点需要合并，避免零高度四边形。
- 受控间隔补采样的具体策略仍需确认：按 TGrid 固定步长、按拍/小节、还是按屏幕像素误差。

状态：已确认。

### D11: 受控间隔补采样策略

问题：D10 中的受控间隔补采样应该按固定 TGrid、拍/小节，还是屏幕像素距离控制？

结论：第一版按屏幕 Y 距离控制。

实现要求：

- 对排序后的相邻采样 TGrid，使用当前绘制上下文的 Soflan 转换成 view-relative Y。
- 如果相邻两个截面的屏幕 Y 距离超过阈值，则插入中间 TGrid。
- 阈值第一版暂定 `32px`。
- 每个相邻区间设置最大插入数量，第一版暂定 `64`，避免异常 Soflan 或极长区间导致过量采样。
- 补点后需要再次去重排序。

原因：

- 新 helper 最终服务于编辑器屏幕绘制，视觉粗糙度由屏幕距离决定。
- 固定 TGrid 在高速/低速 Soflan 下会出现过密或过疏。
- 屏幕距离阈值能让采样密度跟当前视口缩放和变速效果同步。

状态：已确认。

### D12: Soflan 支持范围

问题：第一版是否需要支持所有 Soflan 组，还是只支持默认 Soflan 组？

结论：第一版只支持默认 Soflan 组，但底层构建/绘制函数应接收 Soflan context 参数，不把默认组硬编码进核心算法。

实现要求：

- `DrawPlayField(...)` 可以继续从 `fumen.SoflansMap.DefaultSoflanList` 获取默认组上下文。
- `EditorFieldAreaDataBuilder` 或同等构建器不应主动读取全局默认 Soflan；应由调用方传入当前绘制上下文需要的转换器/position list。
- 采样、屏幕 Y 补点、TGrid/Y 互转都使用传入的 Soflan context。
- 多 Soflan 组支持不纳入第一版，但保留后续扩展空间。

原因：

- 旧 helper 已以默认 Soflan 组为核心，第一版不同时扩大范围。
- 多 Soflan 组会引入一个屏幕 Y 对应多个 TGrid 的复杂情况，应该在核心 Area/LimitParam 算法稳定后再处理。
- 参数化 Soflan context 可以避免后续扩展时重写构建器。

状态：已确认。

### D13: 反向速度与零速度 Soflan

问题：第一版是否沿用旧 helper 的负速 Soflan 分段跳过策略？

结论：不沿用。第一版由渲染管线给出的可见 TGrid range 驱动，只要当前 range 被认为可见，就尝试生成截面并绘制。负速不特判跳过；零速或异常变速造成的退化段局部跳过。

实现要求：

- 不再因为 Soflan speed `< 0` 直接跳过某个绘制段。
- 对采样截面先按 TGrid 建模，再转换到屏幕 Y。
- 最终绘制前按屏幕 Y 顺序连接相邻截面，避免反向映射导致段顺序错乱。
- 如果相邻截面转换后 Y 相同或非常接近，且四边形面积退化，则跳过该局部段。
- 如果 X 或 Y 出现 NaN/Infinity，跳过对应截面或段，并输出 debug 计数。

原因：

- 运行时 Area 语义本身不等于“负速段不画”。
- 旧 helper 的负速跳过策略是多边形覆盖假设，不适合作为新截面算法核心。
- 局部退化跳过比整段跳过更容易保留可见范围内的有效区域。

状态：已确认。

### D14: 左右边界反转处理

问题：如果某个截面或相邻截面段出现 `leftX > rightX`，新 helper 是否应该像旧 helper 那样交换左右尾段来修补？

结论：不交换左右边界。第一版保留原始左右边界语义；绘制阶段对反转或退化的局部 quad 跳过，并记录 debug 计数。

实现要求：

- Area/LimitParam 构建阶段不交换 `WallLeft` 和 `WallRight` 的身份。
- 如果单个截面出现 `leftX > rightX`，该截面标记为 invalid 或 narrow-invalid。
- 如果相邻两个截面生成的 quad 任一端 `leftX > rightX`，跳过该局部 quad。
- Debug 模式下记录跳过数量和对应 TGrid 范围。
- 不调用旧 helper 的 `AdjustLaneIntersection()`，也不实现“交换剩余尾段”的等价逻辑。

原因：

- `leftX > rightX` 是 Field 边界语义反转或谱面异常，不应通过交换左右身份掩盖。
- 旧 helper 的交换逻辑服务于 Earcut 多边形三角化，不是运行时 FieldObject 语义。
- 局部跳过可以避免反转三角形，同时保留问题可观察性。

状态：已确认。

### D15: 新旧 helper 切换策略

问题：新 helper 完成后是否通过设置开关灰度启用，还是直接修改编辑器渲染入口引用？

结论：直接修改编辑器渲染入口引用。第一版不新增 `UseNewPlayFieldRenderer` 设置开关。

实现要求：

- 保留旧 `DrawPlayableAreaHelper.cs` 文件作为源码对照，但编辑器主渲染路径直接实例化并调用 `DrawPlayableAreaHelper_new`。
- 修改引用点集中在 `FumenVisualEditorViewModel.Drawing.cs`：
  - helper 字段类型。
  - `PrepareRenderLoop(...)` 或等价初始化处的 `new`。
  - 每帧 `DrawPlayField(...)` / `Draw(...)` 调用保持接口兼容时无需改调用参数。
- `DrawPlayableAreaHelper_new` 第一版尽量维持旧 helper 的公开方法形状：
  - `Initalize(IRenderManagerImpl impl)`。
  - `Draw(IFumenEditorDrawingContext target, IDrawCommandListBuilder builder)`。
  - `DrawPlayField(IFumenEditorDrawingContext target, IDrawCommandListBuilder builder, TGrid fieldMinTGrid, TGrid fieldMaxTGrid)`。
- 不新增用户配置项，不增加持久化设置。

原因：

- 用户明确要求直接修改编辑器代码引用。
- 引用点较少，切换成本低。
- 保持旧文件可用于后续人工对照，但不在主渲染路径分支选择。

状态：已确认。

### D16: 新 helper 公开接口兼容性

问题：`DrawPlayableAreaHelper_new` 是否保持与旧 helper 相同的公开方法形状？

结论：保持兼容。

实现要求：

- 新类提供 `Initalize(IRenderManagerImpl impl)`。
- 新类提供 `Draw(IFumenEditorDrawingContext target, IDrawCommandListBuilder builder)`。
- 新类提供 `DrawPlayField(IFumenEditorDrawingContext target, IDrawCommandListBuilder builder, TGrid fieldMinTGrid, TGrid fieldMaxTGrid)`。
- `Draw(...)` 继续承担设计模式音频结束线绘制，除非后续单独拆分。
- `DrawPlayField(...)` 的入口条件继续尊重设计模式和 `EnablePlayFieldDrawing`。
- 调用方只替换字段类型和构造类型，不改渲染管线调用顺序。

原因：

- 编辑器引用点少且固定，接口兼容能把切换风险限制在 helper 内部。
- 新算法复杂度不应外溢到 `FumenVisualEditorViewModel.Drawing.cs`。

状态：已确认。

### D17: Debug 信息策略

问题：新 helper 对反转边界、退化 quad、NaN/Infinity 等异常情况如何输出 debug 信息？

结论：第一版只在 `PLAYFIELD_DEBUG` 条件编译下进行屏幕调试绘制和轻量计数；默认构建不写每帧日志。

实现要求：

- 非 `PLAYFIELD_DEBUG` 构建：
  - 静默跳过 invalid 截面或退化 quad。
  - 不在每帧调用 `Log.LogDebug` / `Log.LogInfo`。
- `PLAYFIELD_DEBUG` 构建：
  - 可绘制采样截面点。
  - 可绘制被跳过的 quad 范围或截面位置。
  - 可显示 invalid/degenerate/skipped 计数。
  - 可显示当前采样点数量和输出 quad 数量。
- Debug 统计应为局部变量或每帧临时结构，避免污染运行状态。

原因：

- Playfield 绘制是每帧渲染路径，默认写日志会污染控制台并影响播放性能。
- 几何问题更适合通过屏幕 overlay 定位。
- 保持与旧 helper `PLAYFIELD_DEBUG` 调试习惯一致。

状态：已确认。

### D18: 第一版验证策略

问题：实现 `DrawPlayableAreaHelper_new` 后，第一版如何验证？

结论：第一版以构建验证和固定谱面/时间点截图人工验证为主，不强制承诺自动单元测试覆盖几何。

验证要求：

- 必须通过项目构建。
- 使用固定谱面和固定播放/视口时间点做截图或人工观察。
- 至少覆盖以下场景：
  - 无墙轨，默认 `[-24, 24]`。
  - 只有左墙。
  - 只有右墙。
  - 左右墙不交叉。
  - 曲线墙轨。
  - 反向 Soflan 或零速附近。
  - 历史问题样本：`musicId:0840/1119/1011/0591`。
- 如果后续发现稳定的截图自动化入口，再补截图回归。

原因：

- Playfield 绘制依赖 editor viewport、Soflan、绘制上下文和渲染后端，纯单元测试成本高。
- 第一版重点验证算法结构、渲染入口替换和典型视觉行为。
- 几何异常通过 `PLAYFIELD_DEBUG` overlay 辅助定位。

状态：已确认。

### D19: 第一版代码组织

问题：第一版是否拆出 `EditorFieldAreaDataBuilder`、`FieldAreaSample`、`FieldLimitParam` 等独立文件/独立类？

结论：第一版先把核心实现集中放在 `DrawPlayableAreaHelper_new` 中，方便整体采纳或整体删除；后续算法稳定后再考虑拆分。

实现要求：

- 第一版不新增单独的 builder 文件。
- 第一版不把模型类型提升为跨文件公共类型。
- 可在 `DrawPlayableAreaHelper_new` 内部使用 private nested struct/class 或局部方法组织逻辑。
- 保持新 helper 文件自包含，降低回滚和替换成本。
- 如果文件膨胀或后续需要复用，再按职责拆出 builder/model。

原因：

- 当前阶段仍是算法替换验证期，整体文件更便于对比、采纳和删除。
- 提前拆分会增加文件数量和引用面，反而提高回滚成本。
- 后续确定算法有效后，再做工程化拆分更稳妥。

状态：已确认。

### D20: 新文件与类名

问题：新实现的文件名和类名是否使用实验性质的 `DrawPlayableAreaHelper_new`？

结论：使用 `DrawPlayableAreaHelper_new.cs` 和 `DrawPlayableAreaHelper_new` 类名。

实现要求：

- 新文件路径：
  `OngekiFumenEditor/Modules/FumenVisualEditor/Graphics/Drawing/Editors/DrawPlayableAreaHelper_new.cs`。
- 新类名：
  `DrawPlayableAreaHelper_new`。
- 暂不重命名旧 `DrawPlayableAreaHelper`。
- 后续算法稳定并决定正式替换时，再考虑重命名或删除旧实现。

原因：

- 用户希望第一版方便整体采纳或删除。
- `_new` 后缀明确表达实验替换性质。
- 先降低代码组织变更成本，后续再做命名清理。

状态：已确认。

### D21: 设计模式音频结束线

问题：旧 helper 的 `Draw(...)` 会在设计模式绘制红色音频结束线，新 helper 是否保留？

结论：保留。`DrawPlayableAreaHelper_new.Draw(...)` 第一版继续在设计模式绘制音频结束线。

实现要求：

- `Draw(...)` 保持旧 helper 行为：仅在 `target.Editor.IsDesignMode` 时绘制音频结束线。
- 音频结束线位置继续使用 `target.Editor.TotalDurationHeight - ViewRelativeOriginY`。
- 线条颜色继续使用红色。
- Playfield 新算法只放在 `DrawPlayField(...)` 中，不影响设计模式音频结束线。

原因：

- 调用方仍会调用 helper 的 `Draw(...)`。
- 替换 helper 不应让设计模式已有辅助线消失。
- 该逻辑很小，保留有利于接口兼容。

状态：已确认。

### D22: 新 helper 的设置读取策略

问题：`DrawPlayableAreaHelper_new` 第一版是否复用旧 helper 的 `EditorGlobalSetting` 读取和订阅逻辑？

结论：复用旧 helper 的运行时设置读取和订阅逻辑。

实现要求：

- `Initalize(IRenderManagerImpl impl)` 调用 `UpdateProps()`。
- 订阅 `EditorGlobalSetting.Default.PropertyChanged`。
- 读取 `EditorGlobalSetting.Default.EnablePlayFieldDrawing` 到 `enablePlayFieldDrawing`。
- 读取 `EditorGlobalSetting.Default.PlayFieldForegroundColor` 到 `playFieldForegroundColor`。
- 只响应 `EnablePlayFieldDrawing` 和 `PlayFieldForegroundColor` 变化。
- `PlayFieldBackgroundColor` 仍由 `FumenVisualEditorViewModel` 更新并用于预览模式清屏色。
- `HideWallLaneWhenEnablePlayField` 仍由 `FumenVisualEditorViewModel` 暴露给 `WallLaneDrawTarget`，不放入新 helper。
- 设计模式音频结束线仍保持红色。

影响：

- 新 helper 会和旧 helper 一样响应 UI/配置中的 Playfield 开关和前景色设置。
- 背景色、隐藏墙轨等非几何设置继续走编辑器原有状态，不重复实现。
- 新 helper 仍然把 Playfield 几何算法集中在单文件内，方便后续整体采纳或删除。

状态：已确认。

### D23: 第一版填充色

问题：新 helper 的 Playfield 填充色使用什么值？

结论：使用 `EditorGlobalSetting.Default.PlayFieldForegroundColor` 的当前运行时值。

实现要求：

- 初始化时读取 `PlayFieldForegroundColor`。
- 设置变化时重新读取并转为 `Vector4`。
- `DrawFieldQuads(...)` 使用当前 `playFieldForegroundColor` 写入 polygon vertex。

原因：

- 用户切换前景色后，Playfield 填充色应与旧 helper 保持一致。
- 默认值仍由配置系统负责，helper 不再维护单独的默认色常量。

状态：已确认。

### D24: 第一版绘制开关行为

问题：新 helper 的 Playfield 绘制条件是什么？

结论：设计模式不绘制 Playfield；预览模式还必须满足 `EnablePlayFieldDrawing = true`。

实现要求：

- `DrawPlayField(...)` 中如果 `target.Editor.IsDesignMode` 为 true，直接返回。
- 如果 `enablePlayFieldDrawing` 为 false，直接返回。
- 预览模式下只要渲染管线调用 `DrawPlayField(...)`，就尝试绘制。
- 如果无有效截面或所有 quad 退化，则自然不提交 polygon。

原因：

- 新 helper 接回旧实现设置语义后，关闭 `EnablePlayFieldDrawing` 不应继续绘制 Playfield。
- 设计模式 Playfield 原本也不绘制，只保留音频结束线。

状态：已确认。

### D25: music1069 T[20,240] 同 TGrid 墙点折角

问题：`F:\refresh\package\mu3_Data\StreamingAssets\GameData\A000\music\music1069\1069_03.ogkr` 中，用户反馈 `T[20,240]` 的 `WallLeft` 轨道生成点缺少折角。

核对结果：

- 原始 ogkr 在该时刻存在同一 `WallLeft` 轨道 `7` 的两个连续节点：
  - `WLN 7 20 240 4 0 0 0`
  - `WLN 7 20 240 -24 0 0 0`
- 同一时刻右墙存在：
  - `WRN 64 20 240 24 0 0 0`
- 原始文件中该时刻未发现 `WallLeft` 的 `X=28` 记录；如果后续仍期望 `X[28,0]`，需要确认它是运行时派生坐标、屏幕坐标、右墙坐标，还是其它轨道来源。

结论：这个样例不能用 `Bef == Aft` 表示。若把同一 TGrid 的多个 `WallLeft` child 先聚合为单个 X，`JointField` 的 `current.Aft -> next.Bef` 截面语义会丢失同一时刻的折角。

实现要求：

- 查询某侧边界时返回 `BoundarySample(Bef, Aft)`，而不是单个 X。
- 同一墙轨同一 TGrid 多 child 时，按 child 顺序取首个同刻节点作为 `Bef`，取末个同刻节点作为 `Aft`。
- 多候选墙轨聚合时，左侧分别对 `Bef` / `Aft` 取最小值，右侧分别取最大值。
- `FieldAreaSample` 和 `FieldLimitParam` 后续绘制继续使用 `current.Aft -> next.Bef`，保留折角。

状态：已修正并通过构建验证。

### D26: music1069 T[52,1440] 墙轨结束截面折角

问题：`F:\refresh\package\mu3_Data\StreamingAssets\GameData\A000\music\music1069\1069_03.ogkr` 中，用户反馈 `T[52,1440]` 的 `WallLeft` 轨道生成点缺少折角。

核对结果：

- 原始 ogkr 在该时刻存在 `WallLeft` 轨道 `18` 的结束节点：
  - `WLE 18 52 1440 0 0 0 0`
- 同一时刻右墙存在 `WallRight` 轨道 `75` 的结束节点：
  - `WRE 75 52 1440 24 0 0 0`
- 原始文件中该时刻未发现 `WallLeft` 的 `X=28` 记录；如果后续仍期望 `X[28,0]`，需要确认它是运行时派生坐标、屏幕坐标、右墙坐标，还是其它轨道来源。

结论：这个样例不是同一 TGrid 多 child，而是墙轨结束边界的前后截面问题。结束点本身的 `Bef` 应来自结束前的墙轨位置，`Aft` 应来自结束后的边界状态；如果结束后没有 WallLeft，`Aft` 回退默认左边界 `-24`。

实现要求：

- `Bef` 和 `Aft` 查询不能共用同一个 `GetVisibleStartObjects(t, t)` 结果后直接压成单值。
- `Bef` 侧只保留 `MinTGrid < t <= MaxTGrid` 的墙轨。
- `Aft` 侧只保留 `MinTGrid <= t < MaxTGrid` 的墙轨。
- 某侧没有候选时，按默认边界回退，左侧 `-24`，右侧 `24`。
- 后续绘制继续使用 `current.Aft -> next.Bef`，让墙轨结束瞬间形成折角。

状态：已修正并通过构建验证。

### D27: music1069 T[91,1510] 高倍 Soflan 后可见区间缺少后置截面

问题：`F:\refresh\package\mu3_Data\StreamingAssets\GameData\A000\music\music1069\1069_03.ogkr` 中，用户反馈在 `time = T[91,1510]` 时，从 `T[92,0]` 到顶部没有击打区域；到 `time = T[91,1873]` 时才正确绘制。

核对结果：

- 该区域存在极短高倍 Soflan：
  - `SFL 92 0 15 2000.000000 0`
- 左墙从 `T[91,0]` 延续到 `T[99,0]`，并在 `T[92,10]` 有同刻双节点：
  - `WLS 42 91 0 -12 0 0 0`
  - `WLN 42 92 10 -12 0 0 0`
  - `WLN 42 92 10 0 0 0 0`
- 右墙同样从 `T[91,0]` 延续到 `T[99,0]`，并在 `T[92,10]` 有同刻双节点：
  - `WRS 100 91 0 12 0 0 0`
  - `WRN 100 92 10 12 0 0 0`
  - `WRN 100 92 10 0 0 0 0`

结论：该样例不是边界值缺失，而是可见 TGrid range 在高倍 Soflan 断点附近可能非常短。若局部 AreaData 只收集严格可见区间内的采样点，`T[92,0]` 后的第一个有效墙轨截面 `T[92,10]` 不会被纳入，导致相邻截面不足或局部 quad 被跳过。

实现要求：

- 局部 AreaData 构建必须带最近前置和最近后置上下文截面。
- 严格可见范围内的采样点仍照常加入。
- 严格范围之前的所有候选节点只保留最近一个作为前置上下文。
- 严格范围之后的所有候选节点只保留最近一个作为后置上下文。
- 候选节点来源包括墙轨 start/end/child 节点、当前 TGrid 和 Soflan 断点。
- 这样即使可见范围被高倍 Soflan 压到不足一个有效墙轨段，也能用后置截面生成 `current.Aft -> next.Bef` 的可见 quad。

状态：已修正并通过构建验证。

### D28: music0840 T[64,614] 补采样误用下一组同刻 child

问题：`F:\refresh\package\mu3_Data\StreamingAssets\GameData\A000\music\music0840\0840_03.ogkr` 中，用户反馈 `time = T[64,614]` 时，`T[64,840]` 到 `T[64,1200]` 的 playfield 绘制错误，超出了墙壁包裹范围。

核对结果：

- 该段左墙轨道 `9` 在 `T[64,840]` 到 `T[64,1200]` 先收窄：
  - `WLN 9 64 840 -8`
  - `WLN 9 64 1200 -24`
  - `WLN 9 64 1200 4`
- 该段右墙轨道 `19` 对称变化：
  - `WRN 19 64 840 8`
  - `WRN 19 64 1200 24`
  - `WRN 19 64 1200 20`
- `T[64,1200]` 是同一截面横向折墙：`Bef` 端为外侧 `[-24, 24]`，`Aft` 端为内侧 `[4, 20]`。

结论：问题不在最终 `current.Aft -> next.Bef` 地面连接，而在补采样点的边界查询。

`AddScreenDistanceSamples()` 会在 `T[64,840] -> T[64,1200]` 中间插入普通采样点。对这些非 exact TGrid，`GetChildObjectsFromTGrid(t)` 在 valid path 下会返回下一时间点的一组 child；如果下一时间点有同刻双节点，就会返回整组同刻 child。之前 `CalculateBoundaryXGridUnit()` 对非 exact 采样点仍把这一组 child 分别计算后聚合，左侧取 min、右侧取 max，于是中间补采样点被误算成下一折点的外侧 `[-24,24]`，导致填充越过实际斜墙包络。

实现要求：

- exact TGrid 上存在同刻多 child 时，继续按 child 顺序生成 `Bef/Aft`，保留折角。
- 非 exact TGrid 是普通插值截面，不能把下一组同刻 child 聚合成 min/max。
- 对 `lane.IsPathVaild()` 的非 exact 采样点，只使用 `children.FirstOrDefault()?.CalulateXGrid(tGrid)` 计算所在段插值。
- 非 valid path 的复杂曲线仍保留多 child 聚合逻辑，用于覆盖同一 TGrid 可能由多条路径片段命中的情况。
- 最终绘制仍使用运行时 `current.Aft -> next.Bef` 语义。

状态：已修正并通过构建验证。

## 实现约束草案

- 保持旧 helper 不删除，作为源码对照；编辑器主渲染路径直接改用新 helper。
- 新 helper 应避免继续依赖“左右边界交叉后交换尾段”的多边形修补作为核心正确性来源。
- 新 helper 的几何中间模型应显式表达场地截面，而不是只表达屏幕空间左右点列。
- 局部 AreaData 构建不得只使用严格视口内对象；必须考虑视口边界外的最近前置和后置边界状态。
- 默认边界回退必须使用运行时语义：左 `-24`、右 `24`，且左右独立。
- Field 边界源第一版仅允许 `LaneType.WallLeft` 和 `LaneType.WallRight`。
- 同一 TGrid 多条同侧墙轨重叠时，左取最小 XGrid，右取最大 XGrid。
- 曲线墙轨不预展开为完整 AreaData；只在绘制所需采样 TGrid 上实时计算 XGrid。
- 最终绘制采用相邻截面四边形段，不使用 Earcut 作为核心绘制拓扑。
- 第一版只绘制可击打区域填充，不复刻外侧伤害区、墙体、横向墙和网格。
- 第一版普通截面默认 `Bef == Aft`；同 TGrid 多 child、墙轨 start/end 边界或能可靠推导前后断面时生成 `Bef != Aft`。
- 局部截面采样点包含可见边界、当前帧、墙轨节点、Soflan 断点和受控间隔补采样。
- 受控间隔补采样按屏幕 Y 距离控制，第一版阈值暂定 `32px`，每段最多插入 `64` 点。
- 第一版只支持默认 Soflan 组；核心构建器接收 Soflan context 参数，避免硬编码默认组。
- 反向 Soflan 不直接跳过；零速或异常映射产生的退化段局部跳过。
- 左右边界反转时不交换边界，局部跳过绘制并记录 debug 信息。
- 不新增启用开关；`FumenVisualEditorViewModel.Drawing.cs` 直接引用新 helper。
- 新 helper 公开方法保持旧 helper 兼容，调用方只替换类型和构造。
- Debug 信息只在 `PLAYFIELD_DEBUG` 下屏幕绘制/计数，默认构建不写每帧日志。
- 第一版验证以构建和固定谱面/时间点截图人工验证为主。
- 第一版核心实现集中在 `DrawPlayableAreaHelper_new`，不提前拆出独立 builder/model 文件。
- 新文件和类名使用 `DrawPlayableAreaHelper_new`。
- 新 helper 保留设计模式音频结束线绘制行为。
- 新 helper 第一版写死配置，不读取或订阅 `EditorGlobalSetting`。
- 新 helper 第一版填充色使用配置默认值语义：不透明黑色。
- 新 helper 第一版预览模式始终绘制 Playfield，设计模式不绘制 Playfield。
