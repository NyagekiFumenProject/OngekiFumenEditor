# DrawCommandList implementation notes

## 已确认决策

1. `DrawCommandList` 使用不可变快照语义。
   - `IDrawCommandListBuilder` 内部可以使用可变集合收集命令。
   - `GetDrawCommandList()` 返回之后，后续 builder 追加、清空或复用都不能影响已经取得或提交的列表。
   - 该语义用于支撑后续渲染线程、front/back list swap、离屏渲染和多 `IRenderContext`。
2. `PresentDrawCommandList` 不以 `IDrawingContext` 作为执行入口。
   - MVP 矩阵等渲染状态由 `IDrawCommandListBuilder` 设置，并作为 `DrawCommand` 进入命令列表。
   - 目标方向是实际命令渲染代码不依赖 `IDrawingContext`。
3. 首版 `PresentDrawCommandList` 暂不调用 `IDrawing.Draw`。
   - `IDrawing.Draw` 是最终的实际渲染入口，未来 `DrawCommand` 会转发到对应 `IDrawing.Draw`。
   - 当前实现阶段只搭建命令列表、builder、post/swap/present 骨架。
   - `PresentDrawCommandList` 遍历命令的位置先保留 TODO 注释，不接入真实绘制。
4. `DrawCommandList` 保存帧级渲染状态。
   - 命令列表不能只保存 command 序列，还需要携带 viewport、render scale、清屏色、默认 view/projection 等执行所需状态。
   - 初步状态字段包括 `CleanColor`、`ViewWidth`、`ViewHeight`、`RenderScaleX`、`RenderScaleY`、`ModelMatrix`、`ViewMatrix`、`ProjectionMatrix`。
   - 具体矩阵变更仍作为 command 记录。
5. 矩阵替换 API 使用 `SetCurrent*Matrix` 命名。
   - `SetCurrentViewMatrix(matrix)`、`SetCurrentProjectionMatrix(matrix)`、`SetCurrentModelMatrix(matrix)` 表示替换当前矩阵。
   - `Push*Matrix(matrix)` / `Pop*Matrix()` 表示矩阵栈作用域。
   - 不使用 `Apply*Matrix` 命名，避免和 push/pop 语义混淆。
   - `SetCurrent*Matrix`、`Push*Matrix`、`Pop*Matrix` 都会生成对应状态变更 command，并更新 builder 内部当前状态。
   - `SetCleanColor(Vector4? color)`、`SetViewport(float viewWidth, float viewHeight, float renderScaleX = 1, float renderScaleY = 1)` 更新帧级状态，不作为绘制 command。
6. `IDrawCommandListBuilder` 内部维护当前渲染状态，但 `DrawCommandList` 通过顺序命令流表达状态变化。
   - builder 可维护当前矩阵栈，用于校验、调试和未来查询。
   - 每个 draw command 首版不强制附带完整 MVP/state snapshot。
   - 执行侧未来线性扫描命令流并维护当前状态；合批/编译优化后续再做。
7. `DrawCommand` 使用抽象基类/接口加 sealed record 子类表达。
   - 不使用 enum + nullable payload 的形态。
   - 不同绘制命令各自持有自己的强类型参数。
   - 该形态便于后续 pattern matching、合批检查和调试输出。
8. `DrawCommandList` 和 `DrawCommand` 实现 `IDisposable`。
   - 内部集合优先使用 `ObjectPool.GetPooled*()`。
   - `Dispose()` 时清空并归还 pooled 集合。
   - public 暴露面使用 `IReadOnlyList<T>` 等只读接口，避免调用方直接修改内部 pooled 集合。
   - builder 收到 `IEnumerable<T>` 后应复制进命令拥有的 pooled list，而不是保留原始 enumerable。
   - `Dispose()` 必须幂等；多次调用不能出错。
   - `Dispose()` 后对外暴露的 `IReadOnlyList<T>` 等集合视图应表现为空集合。
9. `PostDrawCommandList` 增加默认参数 `autoDispose = true`。
   - render manager 需要随命令列表一起记录该 dispose 策略。
   - `autoDispose: true` 表示由 render manager 在合适时机自动调用 `Dispose()`。
   - `autoDispose: false` 表示 `DrawCommandList` 必须由用户手动调用 `Dispose()`。
   - 默认行为偏向一次性提交，一次 present 后自动释放 pooled 资源。
   - 当 `autoDispose: false` 时，manager 在 present 前检查该 `DrawCommandList` 是否已经 `Dispose()`；如果已释放，则跳过 present。
   - 禁止 `PresentDrawCommandList` 执行过程中并发 `Dispose()` 同一个 `DrawCommandList`，实现时需要锁或等价同步机制。
   - 替换 back slot、swap 替换 front slot、present 后释放 front slot 时，仅对 `autoDispose: true` 的 slot 自动调用 `Dispose()`。
   - 对 `autoDispose: false` 的 slot，manager 只断开引用或跳过 present，不主动释放。
   - `PresentDrawCommandList` 后总是清空 front slot；`autoDispose` 只控制是否自动调用 `Dispose()`。
   - 如果 front list 在 present 前已经 disposed，manager 跳过 present，并清空 front slot。
10. `DrawCommandList` 内部使用状态机处理 present/dispose 竞态。
   - 状态枚举：`Normal`、`PresentApplying`、`DisposeRequested`、`Disposed`。
   - present 前仅当状态为 `Normal` 时继续，并切换为 `PresentApplying`。
   - `Dispose()` 如果发现状态为 `PresentApplying`，不立即清理，而是切换成 `DisposeRequested`。
   - `Dispose()` 如果不是 `PresentApplying`，则直接清理并切换为 `Disposed`。
   - present 结束后，如果状态为 `DisposeRequested`，则调用 `Dispose()` 完成清理；否则切回 `Normal`。
   - 状态转换由 `DrawCommandList` 内部封装，manager 不直接写 `Status`。
   - manager 只调用类似 `TryBeginPresent()` / `EndPresent()` 的内部方法表达 present 生命周期。
11. `IDrawCommandListBuilder.GetDrawCommandList()` 转移内部 pooled 命令集合所有权给 `DrawCommandList`。
   - builder 不复制命令集合。
   - `DrawCommandList` 同时保存调用当下的帧级状态快照。
   - 转移后 builder 重新租用新的 pooled list。
   - builder 的当前矩阵栈和帧级状态重置到默认值，避免下一帧隐式继承上一帧状态。
12. `IRenderManagerImpl` 按 `IRenderContext` 独立维护 front/back draw command list。
   - 每个 context 有自己的 front/back slot。
   - slot 需要记录 `DrawCommandList` 与 `AutoDispose`。
   - 不能使用 manager 全局唯一 front/back，避免编辑器、波形或后续离屏渲染互相覆盖。
13. front/back slot 管理使用通用 helper，而不是在 OpenGL/Skia manager 内重复实现。
   - helper 放在 `Kernel.Graphics.DrawCommands`。
   - OpenGL 和 Skia manager 各自持有 helper 实例并转发接口方法。
   - 不优先使用 manager 基类，避免牵动现有初始化、MEF export 和 backend 差异逻辑。
14. `SwapDrawCommandList(IRenderContext context)` 返回 `bool`。
   - back slot 有列表时，移动/交换到 front，back 置空，返回 `true`。
   - back slot 为空时，不修改当前 front，返回 `false`。
   - 如果成功 swap 时替换掉旧 front，旧 front 按其 `autoDispose` 策略处理。
15. 首版 `IDrawCommandListBuilder` 覆盖现有主要绘制入口。
   - 包含 lines/simple lines、texture/batch texture/highlight batch texture、circle、polygon、string、beam、static VBO draw 等命令类型。
   - `DrawString` 不携带 `measureTextSize` 输出参数。
   - 文字测量后续作为独立测量能力实现，不放进 command list 绘制命令。
   - `DrawStringCommand` 首版直接持有 `IFontHandle`，不把字体转换成 family name 等可序列化描述。
   - 首版只记录/绘制已有 static VBO handle，不实现 `GenerateVBOWithPresetPoints` 之类 VBO 生成能力。
16. 命令实例参数使用强类型 `record struct`。
   - texture 使用类似 `TextureInstance(Size, Position, Rotation, Color)`。
   - circle 使用类似 `CircleInstance(Point, Color, IsSolid, Radius, HollowLineWidth)`。
   - polygon 使用类似 `PolygonVertex(Point, Color)`。
   - 不在 command list API 中继续暴露 tuple 作为主要参数类型。
17. builder 和 manager 采用明确参数校验。
   - null context/list/texture/text/集合参数抛 `ArgumentNullException`。
   - 非法数值如 `lineWidth <= 0`、`radius < 0`、`fontSize <= 0` 抛 `ArgumentOutOfRangeException`。
   - 空集合允许，但 builder 跳过，不生成无意义 command。
   - `Pop*Matrix()` 在无可弹出矩阵时抛 `InvalidOperationException`。
   - `SetCurrent*Matrix` 首版不做 NaN 检查。
18. 直接扩展 `IRenderManagerImpl`。
   - 增加 `CreateDrawCommandListBuilder()`。
   - 增加 `PostDrawCommandList(IRenderContext context, DrawCommandList drawCommandList, bool autoDispose = true)`。
   - 增加 `SwapDrawCommandList(IRenderContext context)`。
   - 增加 `PresentDrawCommandList(IRenderContext context)`。
   - OpenGL 和 Skia 两个现有实现都补齐这些方法，不额外引入新接口。
19. builder 默认帧级状态。
   - `CleanColor` 类型为 `Vector4?`，默认为纯黑。
   - 允许通过 `SetCleanColor(null)` 表示不清屏；该方法需要在 XML 注释中特地注明 null 语义。
   - `ViewWidth = 0`、`ViewHeight = 0`。
   - `RenderScaleX = 1`、`RenderScaleY = 1`。
   - Model/View/Projection 默认都是 `Matrix4x4.Identity`。
20. 文件组织。
   - 主目录：`OngekiFumenEditor/Kernel/Graphics/DrawCommands/`。
   - 默认 command 类型和参数类型放在子目录 `DefaultDrawCommands/`。
   - 各 sealed command 独立 `.cs` 文件。
   - `TextureInstance`、`CircleInstance`、`PolygonVertex` 等 record struct 也独立 `.cs` 文件，并放在 `DefaultDrawCommands/`。
   - 命名空间随目录拆分为 `OngekiFumenEditor.Kernel.Graphics.DrawCommands` 和 `OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands`。
21. `DrawCommand.Dispose()` 只释放 command 自己拥有的 pooled 集合。
   - 不释放 `IImage`、`IFontHandle`、`IStaticVBODrawing.IVBOHandle` 等外部渲染资源。
   - 外部资源生命周期由其创建方、缓存或 drawing target 管理。
22. `DrawCommandList.Dispose()` 级联释放命令。
   - 先调用每个 `DrawCommand.Dispose()`。
   - 再清空并归还自己的 command pooled list。
   - dispose 后对外暴露的 `Commands` 视图表现为空集合。
23. 首版 `PresentDrawCommandList(IRenderContext context)` 遍历 front commands 并保留 TODO 分支。
   - 需要按 command 类型写出 switch/pattern matching 分支。
   - 不调用 `IDrawing.Draw`，不接入真实绘制。
   - 仍然执行 present 生命周期、disposed 检查、front 清空和 `autoDispose` 处理。
24. 本次验证范围。
   - 不新增测试项目。
   - 实现后以 `dotnet build .\OngekiFumenEditor.sln` 编译通过为主要验证。

## 待确认问题

1. `IDrawCommandListBuilder` 是否也实现 `IDisposable`。
   - builder 在 `GetDrawCommandList()` 后会重新租用新的内部 `IPooledList<DrawCommand>`。
   - 若 builder 生命周期较长或被提前丢弃，需要明确是否由 `Dispose()` 释放当前尚未转移给 `DrawCommandList` 的内部集合。
2. front/back helper 自身的线程同步策略。
   - `DrawCommandList` 已有 present/dispose 状态机。
   - `PostDrawCommandList`、`SwapDrawCommandList`、`PresentDrawCommandList` 若未来跨线程调用，helper 的 context slot 字典也需要锁或等价同步策略。

## 代码库现状判断

1. `DrawCommandList` 应放在 `OngekiFumenEditor.Kernel.Graphics.DrawCommands` 文件夹/命名空间下，而不是 OpenGL/Skia 任一 backend 内部。
   - 现有 `IRenderManagerImpl`、`IRenderContext`、`IDrawing`、各类 drawing 接口都位于通用 `Kernel.Graphics`。
   - OpenGL 和 Skia 都实现同一套 drawing 接口，因此命令列表应记录 backend 无关的 drawing 调用和状态变更。
2. 现有 `IDrawing` 接口仍以 `IDrawingContext target` 作为参数，并通过 `target.CurrentDrawingTargetContext` 读取矩阵、视口和尺寸。
   - 若 `PresentDrawCommandList` 不接收 `IDrawingContext`，首版实现需要在内部适配现有 `IDrawing`，或同步扩展新的不依赖 `IDrawingContext` 的绘制入口。
   - 已确认首版不做这个适配，等后续真实执行 `DrawCommand` 时再处理。
