# PR #43 合并代码风格与架构审查

- 日期：2026-10-26
- PR 链接：<https://github.com/NyagekiFumenProject/OngekiFumenEditor/pull/43>
- 审查对象：合并提交 `8fa0f771`（`Merge PR #43: 按 Grid 复制粘贴、吸附与批量移动`）及其在 `pr43` 分支上的后续修改（当前 HEAD `47f08c8f`）
- PR 原始提交：`66add27e`、`2afa6063`、`15bc5b23`、`4dae2ae6`、`40ba7bf1`、`44417889`
- 对照基线：`8fa0f771^1`（PR 合入前的 WPF 树）、`AGENTS.md`、仓库 `.editorconfig`、同模块既有实现
- 范围：`OngekiFumenEditor/` 与 `tests/grid-placement/`；Avalonia 树本次未修改
- 性质：静态审查 + 一处临时脚本行为验证；未修改生产代码

## 1. 结论摘要

PR #43 的功能与回归覆盖较完整（仓库内 `artifacts/grid_tests.log` 记录 228 项通过），主要问题不在功能缺失，而在**新逻辑的落点**与**既有框架的边界**：

1. 拖动事务、剪贴板几何、附轨解析三块领域逻辑分别落在 `ViewModels/Interactives`、`Kernel/DefaultImpl`、`Base` 三个层次，并互相引用，形成 `Kernel` 实现 ↔ `ViewModels` 管理器的双向耦合。
2. `PasteOption` 被复制成 `GridPlacementOption`，通过枚举数值强转连接；新增/重排枚举值会静默错位。
3. 为支撑无窗口回归，生产代码保留了测试专用成员（`CandidateQueryCount`、`TryMove`、`CalculateOriginalLaneX` 等），测试又通过反射/`GetUninitializedObject` 访问私有实现，形成双向强耦合。
4. 发现一处未覆盖的行为回归：`XGridZeroMirror` / `SelectedRangeCenterXGridMirror` 粘贴会平移时间，旧版不会；`XGridZeroMirror` 已用临时脚本复现，另一个由同一 `dt` 路径推导（见 E-1）。
5. 代码风格问题集中在全限定名、未清理 using/注释代码、魔法数、热路径分配、自动生成文件手工修改、resx BOM 等。

优先级建议见 §9。

## 2. 审查范围与对照方式

- 合并 diff：`git diff 8fa0f771^1 8fa0f771`，37 个文件、+3123/-620。
- 合并后修改：`git diff 8fa0f771..HEAD` 共 38 个文件，其中包含音频、依赖升级等非 PR #43 改动；与 PR 相关的本地修改主要来自 `88c14d8e`、`b0d1f714`、`457fe163`、`273c76a9` 等提交。
- 对照“原项目风格”的依据：
  - `ObjectInteractiveActionBase` 既有入口均为 `public abstract`；
  - UI 文案统一走 `Resources`，撤销统一走 `LambdaUndoAction` + `UndoRedoManager`；
  - 模块内实现类多为 `internal`（如 `LaneSwitchResolver`）；测试直接引用的 helper 按需保持 `public`（`GridClipboardGeometry`、`LaneGeometry`、`OngekiObjectComponents`）；
  - 测试工程按 `tests/<feature>/<Feature>.csproj` 独立调用，不加入解决方案；
  - `.editorconfig` 要求 4 空格、LF、UTF-8、单行语句不加花括号。

## 3. A 类：分层与设计框架

### A-1 `GridPlacementOption` 复制 `PasteOption` 并以数值强转连接（PR 新增）

- 状态：**已完成**（`94f7b5d3`）
- 位置：`OngekiFumenEditor/Modules/FumenVisualEditor/Base/GridPlacementOption.cs:3-10`；`Kernel/DefaultImpl/DefaultFumenEditorClipboard.cs:88`；`ViewModels/FumenVisualEditorViewModel.UserInteractionActions.cs:242-249`
- 现象：两个枚举的成员名和顺序完全相同；`Place(..., (GridPlacementOption)pasteOption)` 依赖数值一致。新增、删除或重排任一枚举值都会静默改变语义。
- 对照：`PasteOption` 是 `IFumenEditorClipboard.PasteObjects` 的公开参数，原本就是剪贴板选项的单一来源；项目没有为几何 helper 复制 UI 枚举的先例。
- 建议：删除 `GridPlacementOption`，`GridClipboardGeometry.Place` 直接接收 `PasteOption`；如必须解耦，提供显式映射方法并加测试，禁止强制转换。

### A-2 Kernel 剪贴板实现与 ViewModel 交互管理器双向依赖（PR 新增）

- 状态：**已处理（第一阶段）**（`5917cfb7`；A-3 已完成，但 Kernel 仍调用 `InteractiveManager.AdjustPastedObjects`，Kernel→ViewModels 尚未完全消除）
- 位置：`Kernel/DefaultImpl/DefaultFumenEditorClipboard.cs:89`（调用 `targetEditor.InteractiveManager.AdjustPastedObjects`；历史上的 `DockableObjectInteractiveAction.CreateOriginalLaneProjector` 已随 A-3 删除）；`ViewModels/Interactives/ObjectInteractiveManager.cs`（已不再引用 `Kernel.DefaultImpl`）
- 现象：`Kernel.DefaultImpl` 的具体实现调用 `ViewModels.Interactives`；`ViewModels` 又调用 `Kernel.DefaultImpl` 的静态 helper。两者不是单向依赖，而是循环引用。
- 对照：既有 `IFumenEditorClipboard` 的意义是让 ViewModel 只依赖 Kernel 接口；`DefaultImpl` 不应反向知道交互管理器。
- 建议：至少把 `GridClipboardGeometry` 移到 `Base`，消除 `ViewModels → Kernel.DefaultImpl`；若需进一步消除 `Kernel → ViewModels`，可把 `PasteObjects` 业务迁到 ViewModel 服务，或通过 `IPasteAdjuster` 接口注入。

### A-3 拖动事务依赖具体交互实现，绕过已抽出的 `LaneGeometry`（PR 新增）

- 状态：**已完成**（`446b1edf`；同时关闭 B-3）
- 位置：`ViewModels/Interactives/SelectionGridDrag.cs:294`（`DockableObjectInteractiveAction.CreateOriginalLaneProjector`）；`ObjectInteractiveManager.Selection.cs:122`；`DockableObjectInteractiveAction.cs:171-175`
- 现象：`SelectionGridDrag` 是拖动事务/快照模型，却直接调用 `ViewModels.Interactives.Impls` 的静态转发方法；`LaneGeometry` 已在 `Base` 提供相同能力。
- 对照：`Base` 层的 `LaneGeometry`/`LaneSwitchResolver` 是 PR 为解耦专门抽出的；`CreateOriginalLaneProjector` 只是对 `LaneGeometry.CreateProjector` 的二次转发。
- 建议：`SelectionGridDrag` 直接依赖 `LaneGeometry`（或注入 `Func`），删除 `CreateOriginalLaneProjector` 转发；交互 action 只保留 UI 适配。

### A-4 `internal virtual TryAdjustGrid` 破坏公开交互基类契约（PR 新增）

- 状态：**已完成**（`76ac02bf`）
- 位置：`ViewModels/Interactives/ObjectInteractiveActionBase.cs:5-7`；`tests/grid-placement/ArchitectureChecks.cs:25-28`
- 现象：基类其他入口都是 `public abstract`，唯独新增的整组预检入口是 `internal virtual`。外部程序集/插件派生类无法 override，会在群体拖动时静默跳过自定义吸附。
- 对照：`ArchitectureChecks` 专门断言“公开交互基类不要求外部派生类实现不可访问的抽象成员”，说明这是为兼容性打的补丁，而不是自然设计。
- 建议：改为 `public virtual`（默认返回 `true`）并补 XML 文档；或把整组预检移到 `ObjectInteractiveManager`，不让 action 基类承担。

### A-5 模型层吸收 UI 通知语义（PR 新增）

- 状态：**已完成**（`3381ddf9`）
- 位置：`Base/OngekiObjects/ConnectableObject/ConnectableStartObject.cs:249-290`；`Base/GridBase.cs:31-35`
- 现象：`ConnectableStartObject.OnPropertyChanged` 新增 `case nameof(GridBase.Unit)/nameof(GridBase.Grid)`，注释写明“属性面板直接修改坐标分量时”；`Unit/Grid` 通知无法区分 T/X，模型用 `NotifyRefreshMinMaxTGrid(e.PropertyName != nameof(TGrid))` 猜测范围是否变化。
- 对照：`GridBase` 只负责数据与通用属性通知；属性浏览器刷新属于 UI 适配层。把“属性面板”写进模型注释，违反既有分层。
- 建议：在属性浏览器/编辑器 ViewModel 的属性变更适配层处理 `Unit/Grid`，或为 `TGrid`/`XGrid` 提供语义化变更事件；模型层只保证 `TotalGrid` 计算正确。

### A-6 滚动指标抑制逻辑塞进 `ScrollViewer` partial（PR 新增）

- 状态：**已完成**（`fb14d45a`）
- 位置：`ViewModels/FumenVisualEditorViewModel.ScrollViewer.cs:118-136`；`ViewModels/FumenVisualEditorViewModel.cs:165-170`
- 现象：`ExecuteGridDragUpdate` 与滚动无关，实际职责是“拖动期间延迟 `RecalculateScrollMetrics`”；`gridDragUpdateDepth` 被两个 partial 共享，`OnFumenObjectModifiedChanged` 又用 `e.PropertyName == nameof(ITimelineObject.TGrid)` 特例决定是否重算。
- 对照：`FumenVisualEditorViewModel` 已按功能拆 partial（Drawing、ScrollViewer、UserInteractionActions、SelectionGridDrag）；事务级抑制应放在拖动事务或独立 partial，而不是滚动条 partial。
- 建议：新建 `FumenVisualEditorViewModel.DragTransactions.cs`，命名 `SuspendScrollMetrics`/`ResumeScrollMetrics`；`OnFumenObjectModifiedChanged` 不再按属性名特例判断。

## 4. B 类：重复实现与耦合

### B-1 三处重复构建“持续物件关联集合”（PR 新增）

- 状态：**已完成**（`e253fe20`）
- 位置：`ViewModels/Interactives/ObjectInteractiveManager.Selection.cs:40-75`；`ViewModels/Interactives/SelectionGridDrag.cs:40-56`；`SelectionGridDrag.cs:226-250`
- 现象：Hold/HoldEnd、Soflan/EndIndicator、LaneBlockArea/EndIndicator、IndividualSoflanArea/EndIndicator 的配对逻辑在 `related` 集合、`changedLanes`、尾节点校验三处各写一遍。
- 建议：复用模型既有 `GetDisplayableObjects()` 作为组件枚举来源，新增 `OngekiObjectComponents`（`EnumerateSelfAndComponents` / `GetHeadObject` / `GetTailObject` / `GetLaneStartObject`），`GridClipboardGeometry.Enumerate` 也复用它。

### B-2 两套“同一 T 的水平轨段”算法（PR 新增一套，既有算法未合并）

- 状态：**已完成**（`83c70558`）
- 位置：`Base/LaneGeometry.cs:15-51`；`ViewModels/Interactives/Impls/DockableObjectInteractiveAction.cs:250-306`
- 现象：两者都判断 `PrevObject.TGrid == child.TGrid`、遍历 `PathControls` 要求同 T、计算 X min/max；`LaneGeometry` 还额外做了路径采样。
- 建议：`LaneGeometry` 暴露 `TryGetHorizontalRange(lane, t, out min, out max)`，`DockableObjectInteractiveAction` 删除 `TryGetHorizontalSegmentXRange`，只保留像素换算。

### B-3 `CalculateOriginalLaneX` 为死代码，仅测试反射调用（PR 新增）

- 状态：**已完成（随 A-3）**（`446b1edf`）
- 位置：`DockableObjectInteractiveAction.cs:171-172`；`tests/grid-placement/GridInteractionChecks.cs:449`
- 现象：生产代码无任何引用；测试通过 `GetMethod("CalculateOriginalLaneX", NonPublic)` 反射调用。
- 建议：删除该方法，测试改用 `LaneGeometry`/`GridClipboardGeometry` 的 public 入口；当前 `LaneGeometry.CreateProjector` 已公开。

### B-4 测试专用成员泄漏到生产类型（PR 新增）

- 状态：**已完成**（`dc6fddec`）
- 位置：`Base/LaneSwitchResolver.cs:18`（`CandidateQueryCount`）；`ViewModels/Interactives/SelectionGridDrag.cs:63-67`（`TryMove`、`TryMoveWithSnapping`）
- 现象：`CandidateQueryCount` 仅 `LaneSwitchChecks.cs:317` 反射读取；`TryMove`/`TryMoveWithSnapping` 仅 `SelectionGridDragChecks.cs` 与 `GridInteractionChecks.cs:211` 使用；生产只调用 `TryMoveInEditor`。
- 对照：项目既有测试通常测公开行为，不把计数器/测试入口挂在生产类型上。
- 建议：把拖动几何提取成纯函数/静态类供测试；`CandidateQueryCount` 改为 `#if DEBUG` 或测试专用程序集；公开 API 只保留生产入口。

### B-5 吸附阈值 `8` 三处硬编码（PR 新增 + 既有）

- 状态：**已完成**（`40b0873d`）
- 位置：`LaneSwitchResolver.cs:12`（`SnapDistance = 8`）；`DefaultFumenEditorClipboard.cs:119`（`distance <= 8`）；`DockableObjectInteractiveAction.cs:147`（`magneticDockDistance = ... : 8`）
- 现象：三处表示“附轨/换线的画布像素阈值”，但各自定义；修改一处不会同步其他两处。
- 建议：统一为 `LaneSnapThreshold`（或编辑器设置项）；`DefaultObjectInteractiveAction.CheckAndAdjustX` 的 `4` 也应在同一处说明语义。

### B-6 `BulletPallete` 等价比较复制字段清单（PR 新增）

- 位置：`GridClipboardGeometry.cs:15-20`
- 现象：手工比较 `TypeValue/TargetValue/ShooterValue/PlaceOffset/RandomOffsetRange/SizeValue/Speed/EditorName`；`BulletPallete` 新增字段时必须同步，否则会把不同调色板误判为相同并复用。
- 建议：在 `BulletPallete` 实现 `IsEquivalentTo`/`Equals`，或按序列化键比较；`GridClipboardGeometry` 只调用它。

### B-7 `GridClipboardGeometry` 成为跨职责工具类（PR 新增）

- 位置：`Kernel/DefaultImpl/GridClipboardGeometry.cs` 全文件
- 现象：同时负责类型分派克隆、持续物件枚举、参考点计算、坐标平移/镜像、调色板比较；新增物件类型必须改 `Clone`/`Enumerate` 的 switch。
- 对照：`OngekiObjectBase.CopyNew` + 各类型 `CopyEntire` 已是多态；PR 在其上又加一层集中式类型分支。
- 建议：把“可剪贴板克隆/家族枚举”下沉到对象模型接口；几何操作拆为 `ClipboardPlacement`；可见性降为 `internal`（同批 `LaneGeometry` 已是 internal）。

### B-8 `PasteObjects` 单方法职责过载（PR 重写）

- 位置：`DefaultFumenEditorClipboard.cs:60-243`
- 现象：60-243 行共 184 行，同时完成坐标计算、Grid 放置、吸附、轨道预检、`BulletPallete` 去重/追加、`redo`/`undo` 多播组装、选择恢复；3 个局部函数捕获循环变量。
- 建议：拆成 `ClipboardPastePlan`（纯数据与校验）和 `ClipboardPasteCommit`（撤销注册）；`BulletPalleteResolver` 独立；局部函数改为私有方法，减少闭包。

### B-9 拖动框架双轨制（PR 新增）

- 状态：**已完成**（`905a0fc7`）
- 位置：`UserInteractionActions.cs:1248-1265`（新 `CommitSelectionDrag` 与旧 `BeginCombineAction` + `GetInteractive(obj).OnDragEnd` 并存）；`DockableObjectInteractiveAction.cs:29-54`（Tap/Hold 走新事务，其他走 `base.OnDragStart`）；`DefaultObjectInteractiveAction` 的 `dragInfoMap`
- 现象：同一 `OnMouseUp` 入口按 `InteractiveManager.IsSelectionDragging` 分流到两套状态/撤销路径；`OnDragStart`/`OnDragEnd` 策略入口只对部分类型生效。
- 对照：`ObjectInteractiveActionBase` 的 `OnDragStart/Move/End` 原本是统一策略入口。
- 建议：让 `SelectionGridDrag` 成为所有可拖对象的统一事务，逐步删除 `dragInfoMap`；迁移期间在旧路径加注释和 TODO，并保证 `UndoRedoManager` 只有一种提交方式。

## 5. C 类：代码风格与可维护性

### C-1 全限定类型名与 `using` 混用（PR 新增）

- 状态：**已完成**（`2d0e7943`）
- 位置：- 位置：`GridClipboardGeometry.cs:27-29`（`OngekiFumenEditor.Base.OngekiObjects.Lane.Base.LaneStartBase`，文件已 `using` 该命名空间）；`DefaultFumenEditorClipboard.cs:91`（`OngekiFumenEditor.Properties.Resources.PasteInvalidGridPosition`，已 `using OngekiFumenEditor.Properties`）；`:149`（`new System.Action`，已 `using System`）
- 建议：统一使用短名；若为避免歧义，添加 using 别名而不是全限定名。

### C-2 未清理的 using、空 override、注释代码（PR 修改文件时未清理）

- 位置：`HoldObjectInteractiveAction.cs:5`（`using OngekiFumenEditor.Properties;` 在删除 `OnDragEnd` 后已无引用）；`:9-12`（`PickDockableObjects` 仅 `return base.PickDockableObjects(editor);`，PR 前既有）；`DockableObjectInteractiveAction.cs:131-146`（整段注释掉的旧实现，PR 前既有）
- 建议：删除未使用 using；空 override 若无扩展意图应删除；注释代码如确需保留，写明保留原因和删除条件。

### C-3 热路径中 `new DefaultObjectInteractiveAction()` 绕虚分派（PR 新增）

- 位置：`DockableObjectInteractiveAction.cs:69`
- 现象：`OnMoveCanvas` 每次鼠标移动新建一个无状态 action，只为调用基类 `CheckAndAdjustY`，避免调用本类 override；产生每帧分配，并让阅读者难以判断实际调用的实现。
- 建议：在基类提取 `protected static double? CheckAndAdjustYCore(...)` 或 `protected` 非虚入口；热路径不分配。

### C-4 `LaneGeometry` 缓存策略与文件整洁度（PR 新增）

- 状态：**已完成**（`00fd17ce`）
- 位置：- 位置：`LaneGeometry.cs:53`（`interpolated` 无界字典）；`:100-101`（类尾多余空行）；类无注释
- 现象：`LaneSwitchResolver.candidates` 有 256 上限，`LaneGeometry` 没有；长拖动中 T 值多时缓存持续增长。缓存生命周期只写在 `LaneSwitchResolver` 注释里。
- 建议：统一缓存上限/清理策略，或把缓存提升到 `LaneSwitchResolver`；补类级注释说明“仅拖动期间复用”。

### C-5 `ReferencePoint` 数值类型混用、溢出检查缺失（PR 新增）

- 位置：`GridClipboardGeometry.cs:129-148`
- 现象：`xs.Min() + xs.Max()` 用 long，ISA 中心用 `((long)TGrid.TotalGrid + EndIndicator.TGrid.TotalGrid) / 2d`，`ts` 为 double；`Math.Round` 后直接转 `int`，未像 `Place` 那样预检溢出。
- 建议：统一用 long/checked 运算，提取 `ReferenceTime`/`ReferenceX` 方法；`ReferencePoint` 返回失败标志或使用与 `Place` 相同的预检。

### C-6 `Place` 的镜像分支索引耦合、复杂度高（PR 新增）

- 位置：`GridClipboardGeometry.cs:185-225`
- 现象：`points`、`controls`、`children[i-1]` 三套索引；先清空所有 control 再按 reversed 顺序 `AddControlObject`；嵌套 `values.Single(...)` 查询；不读测试难以确认顺序约束。
- 建议：抽出 `MirrorConnectableStart(start, values)`，用局部函数分步说明“重排节点—恢复控制点”；补一条直接的镜像顺序测试。

### C-7 `SelectionGridDrag` 快照契约不完整（PR 新增）

- 位置：`SelectionGridDrag.cs:20-28`（`Position` 可变字段）；`:313-319`（`CaptureCurrent` 不填 `Selected`/`LaneAffected`）；`:262`（`Apply` 的 `dt/dx` 生产恒为 0）
- 现象：`FinishSelectionDrag` 用 `after` 与 `Before` 比较后调用 `Apply(after)`，依赖“`Apply` 只在 0 偏移下调用”的隐含契约；新增 `Position` 字段容易漏填。
- 建议：`Position` 改为不可变 record/初始化后只读；`CaptureCurrent` 返回完整快照；拆成 `RestoreSnapshot` 与 `ApplyOffset` 两个明确方法。

### C-8 直接快照整个撤销栈来推断外部修改（PR 新增）

- 状态：**已完成**（`e24496d0`）
- 位置：- 位置：`ObjectInteractiveManager.Selection.cs:93-95`、`:153-155`
- 现象：每次拖动开始 `ActionStack.Cast<object>().ToArray()` 复制全部动作；取消时用 `SequenceEqual` 判断是否发生外部撤销/重做；`UndoActionCount` 已能覆盖大部分场景。
- 建议：在 `DefaultEditorUndoManager` 暴露 `Version`/`Changed` 事件（`IUndoRedoManager` 来自 Gemini，不修改外部接口），或在 `Selection` 中订阅 `ActionStack.CollectionChanged`；避免全栈快照与 `SequenceEqual`。

### C-9 注释语言与格式在同文件内混杂（PR 新增中文注释与既有英文注释混排）

- 状态：**已完成**（`2b2588fa`）
- 位置：- 位置：`DockableObjectInteractiveAction.cs:170-175`（中文行内注释）与 `:183-184`、`:226-231`、`:247-249`（英文 XML 注释）；`LaneSwitchResolver.cs:9,39,51`（中文）；`GridClipboardGeometry.cs`（中文）；`SelectionGridDrag.cs:16-17`（中文）
- 现象：原项目允许中文注释，但同一文件/同一方法内中英混排，XML 文档注释只覆盖部分方法。
- 建议：按文件统一语言；公共成员补 XML summary；行内注释说明“为什么”而非重复代码。

### C-10 `FumenVisualEditorViewModel.SelectionGridDrag.cs` 过度拆分、可见性不一致（PR 新增）

- 状态：**已完成**（`58449c50`）
- 位置：- 位置：该文件 21 行；`Begin/Update/Commit` 为 private，`Abort` 为 public；`ObjectInteractiveManager.Selection.cs:33` 的 `IsSelectionDragging` 为 public
- 现象：partial 只放 4 个转发方法；`Abort` 被 `DeleteSelection`、`CancelSelecting`、`OnMouseUp` 等多处调用，与各入口的模式判断重复。
- 建议：合并到 `UserInteractionActions` 或相邻 partial；统一 public/internal；把“取消拖动”收敛到单一入口。

### C-11 `AbortSelectionDrag` 调用点分散（PR 新增）

- 状态：**已完成**（`a0aa4354`）
- 位置：- 位置：`UserInteractionActions.cs:61,793,877,1225,1335,1356,1498,1660,1687,1793,1810`；`FumenVisualEditorViewModel.cs:490,526`；`Drawing.cs:1063`；`IsaPlacement.cs:94`；`ProjectilePlacement.cs:41`；`BatchModeSwitchCommandHandler.cs:41`
- 现象：模式切换、删除、取消选择、鼠标抬起/按下/移动、快速创建等十余处防御式调用；`OnMouseUp` 与 `OnMouseDown` 都重复判断 `!IsDesignMode || IsBatchMode`。
- 建议：新增统一入口 `CancelTransientInteractions()`（内部依次 `AbortSelectionDrag`、`CancelIsaPlacement`、`CancelBatchProjectilePlacement`），模式切换/文档切换/工具箱拖入/失焦只调用它；交互入口只在“即将改变模型”处中止并注释原因。

### C-12 轨道预检提示硬编码类型名（PR 新增）

- 位置：`DefaultFumenEditorClipboard.cs:96-100`
- 现象：`Resources.CantPlaceObjectsByNoSuiteLanes.Format("Tap / Hold")` 把英文类型名拼进本地化资源；日/中文界面也会出现英文。
- 建议：资源文案改为固定表述（“无法附着到轨道”），或按对象类型从资源取名称。

### C-13 粘贴位置校验重复计算（PR 新增）

- 状态：**已完成**（`81dd757c`）
- 位置：`DefaultFumenEditorClipboard.cs:79-85`
- 现象：`rawX` 只用于有限性/范围检查，随后 `ConvertXToXGrid(target.X, ...)` 又算一次；两个 API 的边界条件不一致时容易漏检。
- 建议：直接调用一次并检查返回值；如需要 `TotalUnit` 预检，使用同一个 `TryConvert`。

### C-14 `Tag` 重复赋值（PR 新增）

- 状态：**已完成**（`0cf61804`）
- 位置：`GridClipboardGeometry.cs:82,116`
- 现象：`copy.Tag = source.Tag` 后，`copiedNodes` 循环又逐节点覆盖 `node.Tag = sourceNodes[i].Tag`；根节点被赋两次。
- 建议：只保留循环赋值；若根 `Tag` 有特殊语义，写明原因。

### C-15 手工修改自动生成的 `Resources.Designer.cs`（PR 新增）

- 状态：**已完成**（`63e4227e`）
- 位置：`Properties/Resources.Designer.cs:9392`
- 现象：文件头明确“Changes to this file may cause incorrect behavior and will be lost if regenerated”；新增属性只有单行表达式，没有生成器惯用的 `/// <summary>`，与 `CantPlaceObjectsByNoSuiteLanes`（`:1030-1035`）格式不一致。
- 建议：通过 resx + ResGen/IDE 重新生成，避免手工编辑；若必须手改，保持生成器格式并加注释。

### C-16 三个 resx 的 BOM 被移除（PR 附带变更）

- 状态：**已完成**（`d6604d24`）
- 位置：`Properties/Resources.resx`、`Resources.ja.resx`、`Resources.zh-Hans.resx` 第 1 行
- 现象：PR 前三个文件均带 UTF-8 BOM，PR 删除；属于功能无关的编码变更，制造额外 diff，也可能影响依赖 BOM 的旧工具链。
- 建议：恢复 BOM，或在独立提交中全仓库统一；功能 PR 不做无关格式变更。

## 6. D 类：测试工程与文档

### D-1 `GridPlacement.csproj` 与既有 tests 约定不一致（PR 新增）

- 位置：`tests/grid-placement/GridPlacement.csproj`
- 现象：缺少 `ImplicitUsings`/`Nullable`；缺少 `RequireEditor` 前置错误；缺少 `None Include="$(EditorAssemblyDirectory)/*.dll" CopyToOutputDirectory`；默认 `EditorAssemblyDirectory` 指向 Release，而 README 要求显式传参；`FrameworkReference Microsoft.AspNetCore.App` 未说明用途。
- 对照：`tests/hit-ranges/HitRanges.csproj` 具备 `ImplicitUsings`、`Nullable`、`RequireEditor` 与 `None Copy`；`tests/bullet-placement` 使用 `ProjectReference`。
- 建议：对齐 `tests/hit-ranges` 的约定；README 命令与 csproj 默认值保持一致；补充 `FrameworkReference` 的必要性说明或移除。

### D-2 测试通过反射与未初始化对象耦合私有实现（PR 新增）

- 位置：`ClipboardInteractionChecks.cs:40-75`（`GetUninitializedObject`、反射 `editorProjectData`、`<Setting>k__BackingField`、`renderDataLock`）；`SelectionGridDragChecks.cs:11-26`（同名包装类 + 反射私有字段/方法）；`LaneSwitchChecks.cs:317`、`GridInteractionChecks.cs:449`
- 现象：测试直接依赖私有字段名、编译器生成的 backing field 名、嵌套类型名和私有方法名；重命名/重构即失败，且测试绕过真实构造流程。
- 建议：优先为测试提供最小 public 测试 API 或真实构造工厂；当前已删除 `InternalsVisibleTo("GridPlacement")`，不要为测试重新扩大 internal 访问；逐步删除 `GetUninitializedObject` 与 backing-field 反射。

### D-3 `ArchitectureChecks` 用字符串包含判断依赖（PR 新增）

- 位置：`ArchitectureChecks.cs:20-28`
- 现象：`t.ToString().Contains("ViewModels")` / `Contains("System.Windows")` 只能看字段/参数/构造参数类型，无法发现方法体内部依赖；对泛型、嵌套类型的字符串格式敏感。
- 建议：用 Roslyn/MetadataLoadContext 分析类型引用，或直接断言允许的类型集合；至少把方法体中的静态调用纳入检查（`LaneSwitchResolver` 当前已改为 `LaneGeometry`，但测试无法防止回退）。

### D-4 测试 README 混入一次性验证数据（PR 新增）

- 位置：`tests/grid-placement/README.md`
- 现象：写入“旧 PR 提交的新缓存回归出现 19 项失败”“PR 版 198 项、本地版 30 项”“45.12 ms / 39.26 ms”等一次性评审/性能数字；其他 tests README（如 `tests/hit-ranges/README.md`）只保留运行方式。
- 建议：README 只保留运行方式、覆盖范围与已知限制；历史验证数据移到 `docs/` 审查报告（即本文档）。

### D-5 `Program.cs` 手工聚合与重复计数（PR 新增）

- 位置：`tests/grid-placement/Program.cs`
- 现象：6 组检查各自重复 `count++`、失败列表、异常拼接；新增检查必须改 `Program`；与 `tests/hit-ranges/Program.cs` 的单一 `Assert` 风格不同。
- 建议：抽取共享 `CheckRunner`（放 `tests/` 公共文件）或统一 `Assert` 风格；每个检查文件返回结果，由 runner 汇总。

### D-6 未覆盖 X 轴镜像的时间语义（PR 新增）

- 位置：`ClipboardInteractionChecks.cs`（仅覆盖 `SelectedRangeCenterTGridMirror`）；`GridClipboardChecks.cs:124,141`（`XGridZeroMirror` 只测溢出；`SelectedRangeCenterXGridMirror` 的测试把 `targetT` 设为 `anchor.t`，因此时间偏移恰好为 0）
- 现象：没有“X 镜像粘贴后 TGrid 保持不变”的断言，因此 E-1 的回归不会被现有 228 项检查发现。
- 建议：补两条回归：`XGridZeroMirror` 与 `SelectedRangeCenterXGridMirror` 在鼠标位置远离锚点时 TGrid 不变。

## 7. E 类：行为/语义风险

### E-1 X 轴镜像粘贴会平移时间（PR 行为回归，已复现）

- 位置：`GridClipboardGeometry.cs:167-168`；`DefaultFumenEditorClipboard.cs:76-86`；`UserInteractionActions.cs:267-271`
- 现象：`dt` 只对 `Direct` 置 0；`XGridZeroMirror`/`SelectedRangeCenterXGridMirror` 会得到 `targetT - anchor.t`。`PasteObjects` 对非 `Direct` 的粘贴用鼠标 Y 计算 `targetT`，而两个 X 镜像菜单传入右键位置，因此时间被平移到右键处。`XGridZeroMirror` 已实测；`SelectedRangeCenterXGridMirror` 走同一 `dt` 分支，未单独实测。
- 复现：临时控制台调用 `GridClipboardGeometry.Place([Tap{T=1000,X=500}], targetX=0, targetT=2000, XGridZeroMirror)`，结果 `T=2000, X=-500`；旧版 `CalculateYMirror` 只在 `SelectedRangeCenterTGridMirror` 返回，X 镜像不改变 T。
- 建议：`dt` 仅对 `None`/`SelectedRangeCenterTGridMirror` 计算；或在 `PasteObjects` 中为 X 镜像传入 `anchor.t`；补 D-6 的回归。

### E-2 粘贴参考点从画布坐标改为 TGrid 坐标（PR 语义变化）

- 状态：**已完成（按 Grid 语义）**（`72b09281`）
- 位置：`GridClipboardGeometry.cs:129-148`；对照 `8fa0f771^1:...DefaultFumenEditorClipboard.cs` 的 `CalculateRangeCenter`/`CalculateXMirror`/`CalculateYMirror`
- 现象：旧版在 canvas X/Y 空间计算范围中心与镜像轴（Y 受 `VerticalDisplayScale`、BPM、拍号影响，X 受横向缩放影响）；新版直接使用 `XGrid.TotalGrid`/`TGrid.TotalGrid`。在变速/变拍谱面上，同样鼠标位置得到的锚点不同。
- 影响：PR 的目标是“按 Grid 复制粘贴”，新语义可能是有意为之；但需要 UI/回归确认，不能默认与旧版等价。
- 建议：在 PR 说明与测试中明确“以 Grid 为准”；若需兼容旧版，按画布坐标计算参考点，或在设置中提供选择。

### E-3 剪贴板只校验集合非空，不校验源文档（PR 行为差异）

- 状态：**已完成**（`7dc66e4b`）
- 位置：`DefaultFumenEditorClipboard.cs:27`（`ContainPastableObjects => currentCopiedSources.Count > 0`）；`:40-54`（先 `Clear` 再 `CloneSet`）
- 现象：旧版要求 `sourceEditor is not null`；新版保存克隆对象，通常更安全，但 `CopyObjects` 先 `Clear` 再 `CloneSet`，克隆过程中异常会清空旧剪贴板且新内容未写入，`ContainPastableObjects` 变为 false。
- 建议：先构建到临时列表，成功后再替换；或给 `ContainPastableObjects` 增加源文档有效性校验。

## 8. 已由本分支后续提交修正的项（不作为遗留问题）

| 编号 | 初版问题 | 当前状态 |
| --- | --- | --- |
| F-1 | PR 初版 `FumenVisualEditorView.xaml.cs` 新增 53 行鼠标/键盘事件处理；后续 PR 提交又让 ViewModel 反向订阅 View 的 `InteractionInterrupted` | 已由 `b0d1f714` 等提交移出 View；当前 `FumenVisualEditorView.xaml.cs` 仅保留 `InitializeComponent`，`Drawing.cs` 不再引用 View |
| F-2 | 初版把群体拖动快照/策略作为 `FumenVisualEditorViewModel` 的嵌套类型 | 已由 `44417889` 重构到 `ObjectInteractiveManager` + 独立 `SelectionGridDrag`，`ArchitectureChecks` 增加断言 |
| F-3 | 初版 `BatchModeSwitchCommandHandler` 使用非 Allman 大括号 | 已由 `40ba7bf1` 整理 |
| F-4 | 初版 `SelectionGridDrag` 与 `BeginSelectionGridDrag` 直接放在 `FumenVisualEditorViewModel` partial 内 | 现由 `ObjectInteractiveManager` 持有事务；生产只走 `TryMoveInEditor`，`TryMove*` 仍仅测试使用（见 B-4） |

## 9. 建议处理顺序

| 优先级 | 编号 | 处理建议 | 状态 |
| --- | --- | --- | --- |
| P0 | E-1 | 修复 X 镜像时间平移，补 D-6 回归；这是用户可见行为回归 | 未开始 |
| P0 | E-2 | 明确并固定以 Grid 为准的粘贴参考点/镜像轴 | **已完成** `72b09281` |
| P1 | E-3 | 复制克隆失败不得清空旧剪贴板 | **已完成** `7dc66e4b` |
| P0 | A-1 | 删除重复枚举/强转，消除新增/重排枚举的静默错位风险 | **已完成** `94f7b5d3` |
| P0 | A-2 | 拆分 Kernel ↔ ViewModel 双向依赖，至少把 `GridClipboardGeometry` 移出 `DefaultImpl` | **已处理（第一阶段）** `5917cfb7`；A-3 已完成，Kernel→VM 未完全消除 |
| P1 | A-4 | `TryAdjustGrid` 改为 public virtual 或在管理器层实现，恢复插件兼容契约 | **已完成** `76ac02bf` |
| P1 | A-5 | 把属性面板通知语义移出 `ConnectableStartObject`/`GridBase` | **已完成** `3381ddf9` |
| P1 | B-3/B-4 | 删除生产死代码与测试专用成员，测试改用纯函数/InternalsVisibleTo | B-3 **已完成（随 A-3）** `446b1edf`；B-4 **已完成** `dc6fddec` |
| P1 | C-8 | 用撤销管理器版本号替代全栈快照与 `SequenceEqual` | **已完成** `e24496d0` |
| P2 | A-3/A-6/B-1/B-2/B-5/B-7/B-8/B-9 | 收敛重复逻辑、拆分事务、统一阈值与缓存策略 | A-3、A-6、B-1、B-2、B-5、B-9 **已完成**；B-7/B-8 未开始 |
| P2 | C-1..C-16 | 代码卫生与格式统一，可随上述重构一并处理 | C-1/C-4/C-9/C-10/C-11/C-13/C-14/C-15/C-16 **已完成**；其余未开始 |
| P2 | D-1..D-5 | 对齐 tests 工程约定与文档职责 | 未开始 |

## 10. 验证与限制

- 基线证据：仓库内 `artifacts/grid_tests.log` 记录“通过 228 项 Grid 放置回归检查”（既有产物，本次未重跑 GUI）。
- 已复现：E-1 的 X 镜像时间平移，临时控制台项目位于 `.tmp/xcheck`，验证后已删除；生产代码未修改。
- 未执行：完整 GUI 拖拽/粘贴手测；E-2 的 BPM/拍号差异只做静态对照，未做视觉对比。
- 本文档只记录审查结论，不改变 Avalonia 同步点；Avalonia 树未修改。

## 11. 复核清单（按编号）

- [x] A-1 枚举合并
- [x] A-2 Kernel/ViewModel 依赖方向（第一阶段）
- [x] A-3 拖动事务依赖具体 action
- [x] A-4 `TryAdjustGrid` 可见性
- [x] A-5 模型层 UI 通知
- [x] A-6 滚动指标抑制位置
- [x] B-1 关联集合去重
- [x] B-2 水平段算法去重
- [x] B-3 `CalculateOriginalLaneX` 删除
- [x] B-4 测试成员下沉
- [x] B-5 阈值常量
- [ ] B-6 `BulletPallete` 等价接口
- [ ] B-7 `GridClipboardGeometry` 拆分
- [ ] B-8 `PasteObjects` 拆分
- [x] B-9 拖动框架统一
- [x] C-1 全限定名统一
- [ ] C-2 未使用 using/空 override/注释代码
- [ ] C-3 热路径无分配
- [x] C-4 `LaneGeometry` 缓存与整洁度
- [ ] C-5 `ReferencePoint` 数值与溢出
- [ ] C-6 `Place` 镜像分支拆分
- [ ] C-7 快照契约
- [x] C-8 撤销栈快照
- [x] C-9 注释语言统一
- [x] C-10 partial 与可见性
- [x] C-11 `AbortSelectionDrag` 收敛
- [ ] C-12 预检提示本地化
- [x] C-13 位置校验去重
- [x] C-14 `Tag` 赋值
- [x] C-15 自动生成文件
- [x] C-16 resx BOM
- [ ] D-1 tests 工程约定
- [ ] D-2 反射/未初始化构造
- [ ] D-3 架构检查方式
- [ ] D-4 README 职责
- [ ] D-5 检查聚合方式
- [ ] D-6 X 镜像时间回归
- [ ] E-1 镜像时间回归
- [x] E-2 参考点语义确认
- [x] E-3 剪贴板半更新防护

## 12. 解决方案明细（与 §3-§7 编号一一对应）

> 实施顺序建议按 §9；`A-1`/`A-2`/`A-3` 可合为一个“剪贴板分层”重构，`E-1` 单独修复以便回滚。`F-1..F-4` 已在 §8 关闭，不再列方案。

#### A-1
- 状态：**已完成**（`94f7b5d3`）
- 方案：把 `PasteOption` 从 `FumenVisualEditorViewModel` 嵌套类型移到 `Modules/FumenVisualEditor/Base/PasteOption.cs`（public enum），`IFumenEditorClipboard`、`GridClipboardGeometry.Place`、`UserInteractionActions` 统一引用；删除 `GridPlacementOption` 与 `(GridPlacementOption)pasteOption` 强转。若暂不迁移枚举，至少改为显式 `switch` 映射函数，`default` 抛异常或返回失败。
- 验收：全仓库无 `GridPlacementOption`；新增 `PasteOption` 值时，`Place` 的映射 `switch` 有明确未覆盖处理（`default => throw`），不会静默落到默认平移分支。

#### A-2
- 状态：**已处理（第一阶段）**（`5917cfb7`；A-3 已完成，但 Kernel 仍调用 `InteractiveManager.AdjustPastedObjects`）
- 方案：第一步把 `GridClipboardGeometry` 移到 `Modules/FumenVisualEditor/Base`（或 `Base/Clipboard`），改为 `public static`（测试直接引用，不引入 `InternalsVisibleTo`）；`ObjectInteractiveManager` 与 `DefaultFumenEditorClipboard` 都只引用 Base，删除 `ViewModels → Kernel.DefaultImpl`。第二步（可选）把 `PasteObjects` 业务整体迁到 `ViewModels/FumenEditorClipboardService`，`DefaultFumenEditorClipboard` 仅做 `IFumenEditorClipboard` 转发与 MEF 装配。
- 验收：A-2+A-3 完成后，`grep -r "Kernel.DefaultImpl" Modules/FumenVisualEditor/ViewModels` 为空；`DefaultFumenEditorClipboard` 不引用 `ViewModels.Interactives.Impls` 的具体类型（仅通过 `IFumenEditorClipboard` 参数访问 ViewModel）。

#### A-3
- 状态：**已完成**（`446b1edf`；同时关闭 B-3）
- 方案：`SelectionGridDrag`、`DefaultFumenEditorClipboard` 直接调用 `LaneGeometry.CreateProjector`；删除 `DockableObjectInteractiveAction.CreateOriginalLaneProjector` 与 `CalculateOriginalLaneX`（同时关闭 B-3）。`LaneGeometry` 改为 `public`，测试直接引用；不再使用 `InternalsVisibleTo`。
- 验收：无 `CreateOriginalLaneProjector`/`CalculateOriginalLaneX` 引用；换线、粘贴、回滚测试全部通过。

#### A-4
- 状态：**已完成**（`76ac02bf`）
- 方案：`ObjectInteractiveActionBase.TryAdjustGrid` 改为 `public virtual`，默认返回 `true`，补 XML doc（“返回 false 表示整组拒绝，默认 true”）；`ArchitectureChecks` 改为断言所有公开抽象成员都能被外部程序集 override。备选是把整组预检移到 `ObjectInteractiveManager`，由 `IGridAdjustmentPolicy` 承担，但改动面更大。
- 验收：外部测试程序集派生一个 action 并 override `TryAdjustGrid`，群体拖动时自定义吸附生效。

#### A-5
- 状态：**已完成**（`3381ddf9`）
- 方案（实际采用）：`OngekiTimelineObjectBase`/`OngekiMovableObjectBase` 订阅网格对象的 `Unit/Grid` 分量变化时，对外统一转发为 `TGrid`/`XGrid`；`ConnectableStartObject` 删除 `GridBase.Unit/Grid` 的 UI 语义分支，只处理 `TGrid`/`XGrid`，并简化 `NotifyRefreshMinMaxTGrid()`。未采用新增 `GeometryChanged` 事件的方案。
- 验收：模型层不再出现“属性面板”注释；属性面板修改 `TGrid.Unit/Grid` 后路径与时间索引刷新；`tests/grid-placement` 新增 `GeometryNotificationChecks` 断言不泄漏 `Unit`/`Grid` 通知。

#### A-6
- 状态：**已完成**（`fb14d45a`）
- 方案：新建 `FumenVisualEditorViewModel.DragTransactions.cs`，把 `gridDragUpdateDepth`、`gridDragMetricsPending`、`ExecuteGridDragUpdate` 移入，并暴露 `IsGridDragTransactionActive`；`ScrollViewer` 只保留 `RecalculateScrollMetrics` 并在入口查询该属性；`OnFumenObjectModifiedChanged` 用属性替换字段名判断。
- 验收：`ScrollViewer.cs` 不再声明拖动字段；拖动期间滚动指标只重算一次（沿用现有性能回归）。

#### B-1
- 状态：**已完成**（`e253fe20`）
- 方案：新增 `Base/OngekiObjectComponents.cs`，以模型既有 `GetDisplayableObjects()` 为组件来源：
  - `EnumerateSelfAndComponents()`：自身 + 持续物件尾节点 + 轨道子节点/曲线控制点；子节点/控制点单独作为根时只返回自身。
  - `GetHeadObject()`：尾节点找回所属头对象。
  - `GetTailObject()`：头对象找回尾节点。
  - `GetLaneStartObject()`：轨道子节点/控制点找回 `ConnectableStartObject`。
  `GridClipboardGeometry.Enumerate` 转发到 `EnumerateSelfAndComponents`；`SelectionGridDrag` 与 `ObjectInteractiveManager.Selection` 删除重复的 Hold/End/轨道类型分支，轨道附轨对象查询保留在管理器。
- 验收：`tests/grid-placement` 新增 `ObjectComponentsChecks`，覆盖 Hold/Soflan/LaneBlockArea/ISA/轨道家族、无尾 Hold、子节点单独为根；现有复制粘贴/拖动回归全过。

#### B-2
- 状态：**已完成**（`83c70558`）
- 方案：在 `LaneGeometry` 提取 `BuildHorizontalRanges(lane)` 与 `TryGetHorizontalRanges(lane, totalT, out ranges)`；`CreateProjector` 复用同一构建结果；`DockableObjectInteractiveAction.TryGetHorizontalSegmentXRange` 改为调用它并做 `ConvertXGridToX`，删除重复的 `PrevObject/PathControls` 遍历。
- 验收：同 T 水平段（含 Bezier 采样）测试通过；水平段判断逻辑只在 `LaneGeometry` 一处。

#### B-3
- 状态：**已完成（随 A-3）**（`446b1edf`）
- 方案：删除 `CalculateOriginalLaneX`；测试改用 `GridClipboardGeometry.Place` 行为或直接调用 `public` 的 `LaneGeometry.CreateProjector`。`CreateOriginalLaneProjector` 随 A-3 一并删除。
- 验收：无反射 `GetMethod("CalculateOriginalLaneX")`；贝塞尔镜像测试通过。

#### B-4
- 状态：**已完成**（`dc6fddec`）
- 方案（实际采用）：删除 `SelectionGridDrag.TryMove`/`TryMoveWithSnapping` 与 `LaneSwitchResolver.CandidateQueryCount`；测试改为反射调用私有 `TryMoveCore`，候选查询次数改用私有 `candidates` 缓存计数。生产只保留 `TryMoveInEditor`。
- 验收：生产类型无测试专用入口；`grep CandidateQueryCount` 为空；原有拖动/换线用例通过。

#### B-5
- 状态：**已完成**（`40b0873d`）
- 方案：在 `LaneGeometry` 定义 `public const double LaneSnapDistancePixels = 8d` 与 `XGridMagneticDistancePixels = 4d`；`LaneSwitchResolver`、`DefaultFumenEditorClipboard`、`DockableObjectInteractiveAction`、`DefaultObjectInteractiveAction` 统一引用。
- 验收：阈值字面量只在常量定义处出现；换线/磁吸测试通过。

#### B-6
- 方案：在 `BulletPallete` 增加 `bool IsEquivalentTo(BulletPallete other)`，字段比较集中在该方法（不重写 `Equals`，避免影响集合/哈希）；`GridClipboardGeometry.PalettesEquivalent` 改为转发或删除。
- 验收：新增 `BulletPallete` 字段时只改一处；粘贴复用/追加测试不变。

#### B-7
- 方案：拆分 `GridClipboardGeometry` 为 `ClipboardObjectCloner`（Clone/CloneSet）、`ClipboardReferencePoint`（TryGet）、`ClipboardPlacement`（Place）；`BulletPallete` 等价比较归 B-6。拆分后可见性按调用方重新评估；当前因测试直接引用保持 `public`。
- 验收：每个类职责单一；A-1/A-2 的命名空间调整后无循环引用；现有测试通过。

#### B-8
- 方案：定义 `ClipboardPastePlan`（copies、target、BulletPallete 决策、预检错误）与 `ClipboardPasteCommitter`（undo/redo 注册、选择恢复）；`PasteObjects` 只做 `TryBuild` + `Commit`；`BulletPalleteResolver` 独立。
- 验收：`PasteObjects` 控制在 40 行内；原子拒绝、撤销/重做、选择恢复测试通过。

#### B-9
- 状态：**已完成**（`905a0fc7`）
- 方案（实际采用）：`DefaultObjectInteractiveAction.OnDragStart/Move/End` 统一委托 `ObjectInteractiveManager.Begin/Update/CommitSelectionDrag`；删除 `DefaultObjectInteractiveAction`/`ConnectableObjectInteractiveAction` 的 `dragInfoMap` 与独立附轨 undo；删除 `DockableObjectInteractiveAction` 的 Tap/Hold 分支和 `IndividualSoflanAreaInteractiveAction`；`OnMouseUp` 删除 `BeginCombineAction` 旧路径；`SelectionGridDrag` 增加单角 ISA 越界规范化。
- 验收：所有拖动只生成一个 `LambdaUndoAction`；`grep dragInfoMap` 为空；拖动/ISA/附轨回归通过。

#### C-1
- 状态：**已完成**（`2d0e7943`）
- 方案：`GridClipboardGeometry` 使用 `LaneStartBase` 短名；`DefaultFumenEditorClipboard` 使用 `Resources` 短名，并用 `SystemAction` 别名处理与 Caliburn `Action` 的冲突。
- 验收：目标生产文件无 `OngekiFumenEditor.` 前缀成员类型名。

#### C-2
- 方案：删除 `HoldObjectInteractiveAction` 的 `using OngekiFumenEditor.Properties;`；删除无扩展意图的 `PickDockableObjects` 空 override；删除 `DockableObjectInteractiveAction` 的注释旧实现，若需保留则改为 `// LEGACY: ...（原因/删除条件）`。
- 验收：`dotnet build` 无未使用 using 警告（如启用 IDE0005）；无大段注释代码。

#### C-3
- 方案：在 `DefaultObjectInteractiveAction` 提取 `protected static double? CheckAndAdjustYCore(ITimelineObject, double, FumenVisualEditorViewModel)`，`CheckAndAdjustY` 调用 core；`DockableObjectInteractiveAction` 直接调用 core，删除 `new DefaultObjectInteractiveAction()`。
- 验收：`OnMoveCanvas` 热路径无分配；拖动测试通过。

#### C-4
- 状态：**已完成**（`00fd17ce`）
- 方案：`LaneGeometry.interpolated` 增加 256 项上限并补类级生命周期注释，清理类尾空行。
- 验收：长拖动缓存有上限；代码整洁度检查通过。

#### C-5
- 方案：`ReferencePoint` 改为 `TryGetReferencePoint(..., out XGrid x, out TGrid t)`；内部统一 `long`/`checked`，ISA 中心与 `ts` 计算统一类型；溢出返回 `false`，`PasteObjects` 走现有失败提示。
- 验收：空集合、极大/极小坐标、ISA 中心测试通过；`Place` 的预检逻辑可复用。

#### C-6
- 方案：提取 `MirrorConnectableStart(ConnectableStartObject lane, Position[] values)`；用 `Dictionary<OngekiObjectBase, Position>` 替代 `values.Single(...)`；保留“先清空 control 再按 reversed 添加”的分步注释与直接测试。
- 验收：新增轨道镜像顺序测试；方法长度明显下降、索引关系可读。

#### C-7
- 方案：`Position` 改为 `readonly record struct`（或只读类）；`CaptureCurrent` 填齐 `Selected`/`LaneAffected`；拆成 `RestoreSnapshot(Position[])` 与 `ApplyOffset(Position[], long dt, long dx)`，删除 `Apply` 的默认偏移参数。
- 验收：取消/提交/回滚测试通过；新增 `Position` 字段时编译器强制所有构造点更新。

#### C-8
- 状态：**已完成**（`e24496d0`）
- 方案：拖动期间订阅 `ActionStack.CollectionChanged`，并补充 `IUndoRedoManager.PropertyChanged` 的 `UndoActionCount`/`RedoActionCount` 监听（撤销/重做只改计数，不触发集合变更）；删除全栈 `ToArray` 与 `SequenceEqual`。
- 验收：外部撤销/重做后取消拖动保留脏状态；拖动开始无 O(n) 快照。

#### C-9
- 状态：**已完成**（`2b2588fa`）
- 方案：删除 Dockable 交互中大段注释旧代码，合并重复说明；保留附轨、坐标恢复、调色板复用等关键“为什么”注释；未强制统一中英语言。
- 验收：目标路径无大段注释代码；关键约束仍可读。

#### C-10
- 状态：**已完成**（`58449c50`）
- 方案：把 `Begin/Update/Commit/AbortSelectionDrag` 合并到 `UserInteractionActions` 的 Drag Actions 区域，删除专用 partial；`AbortSelectionDrag` 保持 public 以兼容测试与命令调用。
- 验收：无 `SelectionGridDrag` 专用 partial；拖动入口集中在 `UserInteractionActions`。

#### C-11
- 状态：**已完成**（`a0aa4354`）
- 方案：新增 `CancelTransientInteractions()`，统一取消失效拖动、ISA 放置与批量投射物放置；模式切换/失焦/工具箱/文档切换调用它；删除鼠标 Up/Down/Move 的重复模式判断；删除/取消选择/新建等模型边界仍保留直接 Abort。
- 验收：直接 Abort 调用从十余处降到 6 处（其中 1 处为统一入口定义），交互测试通过。

#### C-12
- 方案：资源文案改为无占位符（“无法在任何合适轨道放置物件”），或新增 `ObjectTypeTapHold` 资源并 `Format(Resources.ObjectTypeTapHold)`；不要把英文类型名硬编码进调用点。
- 验收：英/日/中三语界面均显示完整语义；无硬编码 `"Tap / Hold"`。

#### C-13
- 状态：**已完成**（`81dd757c`）
- 方案：只调用一次 `ConvertXToXGridTotalUnit`，在同一结果上做 `TotalGrid` 范围检查并直接构造 `XGrid`，删除重复换算。
- 验收：非法位置/越界测试通过；无重复换算。

#### C-14
- 状态：**已完成**（`0cf61804`）
- 方案：删除根对象上的 `copy.Tag = source.Tag`，保留逐节点复制。
- 验收：克隆 `Tag` 行为不变；无重复赋值。

#### C-15
- 状态：**已完成**（`63e4227e`）
- 方案：将 `PasteInvalidGridPosition` 改为生成器惯用的多行 getter；当前环境无 ResGen，未重写整个生成文件。
- 验收：属性格式与相邻生成属性一致；构建通过。

#### C-16
- 状态：**已完成**（`d6604d24`）
- 方案：恢复三个 resx 的 UTF-8 BOM，保留新增资源键的内容差异。
- 验收：`git diff` 首行不再变化；XML 读取与构建正常。

#### D-1
- 方案：对齐 `tests/hit-ranges/HitRanges.csproj`：添加 `ImplicitUsings`、`Nullable`、`RequireEditor` target、`None Include="$(EditorAssemblyDirectory)/*.dll" CopyToOutputDirectory="PreserveNewest"`；默认 `EditorAssemblyDirectory` 与 README 保持一致（建议要求显式传入）；`FrameworkReference` 加注释或移除。
- 验收：不传参数时给出明确错误；README 命令可直接运行；输出目录 DLL 完整。

#### D-2
- 方案：csproj/AssemblyInfo 添加 `[assembly: InternalsVisibleTo("GridPlacement")]`；在 VM 上提供 internal 测试工厂（注入 `EditorProjectDataModel`、`EditorSetting`、`UndoRedoManager`），测试改用真实构造；逐步删除 `GetUninitializedObject` 与 `<Setting>k__BackingField` 反射。
- 验收：测试不再依赖私有字段名/backing field；重命名 VM 字段不会导致测试失败。

#### D-3
- 方案（推荐）：测试工程引用 `NetArchTest.Rules`，断言 `LaneGeometry`/`LaneSwitchResolver`/`GridClipboardGeometry` 不依赖 `ViewModels`/`System.Windows`（可读 IL 引用）。备选：Roslyn 分析源码；不建议继续用 `ToString().Contains`。
- 验收：把 `LaneSwitchResolver` 临时改回引用 `DockableObjectInteractiveAction` 时测试失败。

#### D-4
- 方案：README 删除 2026-10-18/10-21 等一次性验证段落，只保留运行命令、覆盖范围、已知限制；历史数据移到本文档 §10。
- 验收：README 与 `hit-ranges/README.md` 风格一致；无一次性性能数字。

#### D-5
- 方案：新增 `tests/CheckRunner.cs`（各 csproj 通过 `Compile Include` 或共享 props 引入），提供 `Run(name, Action<Action<bool,string>>)`；各 Check 类只实现检查，不重复计数；`Program` 只注册检查。
- 验收：新增检查只加一行注册；输出统一。

#### D-6
- 方案：在 `ClipboardInteractionChecks` 增加两条：`XGridZeroMirror` 与 `SelectedRangeCenterXGridMirror` 粘贴后所有对象 `TGrid` 与输入一致；再补一条鼠标 Y 远离锚点的用例。
- 验收：新测试在修复 E-1 前失败、修复后通过。

#### E-1
- 方案：修改 `GridClipboardGeometry.Place`：
  `var dt = option is GridPlacementOption.None or GridPlacementOption.SelectedRangeCenterTGridMirror ? (long)targetT.TotalGrid - anchor.t.TotalGrid : 0L;`
  （A-1 完成后替换为 `PasteOption`）；或在 `PasteObjects` 中为 X 镜像传入 `anchor.t`。补 D-6 测试。
- 验收：两种 X 镜像粘贴后 T 不变；普通粘贴与 T 镜像行为不变；grid-placement 全量回归通过。

#### E-2
- 状态：**已完成（按 Grid 语义）**（`72b09281`）
- 方案：保留 Grid 坐标，不增加 Canvas 回退；`ReferencePoint` 按复制根对象的 Grid 包围盒取 X/T 中心（纯轨道取起点、混合选择忽略轨道、ISA 取两角），替代普通物件的 `minT`；`SelectedRangeCenterTGridMirror` 改用 `2 * anchor` 反射中心，避免 anchor 中心化后镜像轴偏移。
- 验收：四物件两行两列参考点落在整体中心；跨 BPM 粘贴与 T 镜像回归通过。

#### E-3
- 状态：**已完成**（`7dc66e4b`）
- 方案：`CopyObjects` 先完成 `CloneSet`，成功后再 `Clear`/`AddRange`；新增抛异常的测试对象验证旧剪贴板保留。
- 验收：克隆异常时旧剪贴板仍可粘贴；无半更新状态。

## 13. 实施状态（2026-10-26）

| 编号 | 状态 | 提交 | 验证 |
| --- | --- | --- | --- |
| A-1 | 已完成 | `94f7b5d3` | `PasteOption` 已移到 `Base`，`GridPlacementOption` 删除；Debug/Release 0 error；grid-placement 228/228；MCP e2e 1236/1236；真实复制/粘贴 3 个 Bell 后数量 +3 |
| A-2 | 已完成（第一阶段） | `5917cfb7` | `GridClipboardGeometry` 移到 `Base` 并改为 public（测试直接引用），`ViewModels` 不再引用 `Kernel.DefaultImpl`；Debug/Release 0 error；grid-placement 228/228；MCP e2e 1236/1236；真实复制/粘贴 3 个 Bell 后数量 +3 |
| A-3 | 已完成 | `446b1edf` | 删除 `CreateOriginalLaneProjector`/`CalculateOriginalLaneX`，剪贴板与拖动直接使用 `LaneGeometry`；Debug/Release 0 error；grid-placement 249/249；MCP e2e 1236/1236；真实复制/粘贴 Hold 数量 +1，拖动 Hold 头 TGrid +90 |
| A-4 | 已完成 | `76ac02bf` | `TryAdjustGrid` 改为 public virtual，派生类同步 public override；Debug/Release 0 error；grid-placement 229/229；MCP e2e 1236/1236；真实拖动 Tap 后 TGrid 5280→5340，脚本外部程序集可调用 `TryAdjustGrid` |
| A-5 | 已完成 | `3381ddf9` | `Unit/Grid` 分量变化语义化转发为 `TGrid`/`XGrid`，`ConnectableStartObject` 删除 UI 语义分支；Debug/Release 0 error；grid-placement 255/255；MCP e2e 1236/1236；真实修改 XGrid.Grid 后路径起点同步 65636，TGrid.Unit 修改后 MinTGrid 更新 |
| B-1 | 已完成 | `e253fe20` | 新增 `OngekiObjectComponents` 并复用 `GetDisplayableObjects`；`GridClipboardGeometry.Enumerate`、`SelectionGridDrag`、`ObjectInteractiveManager.Selection` 去掉重复类型分支；Debug/Release 0 error；grid-placement 249/249；MCP e2e 1236/1236；真实复制/粘贴 Hold 数量 +1，拖动 Hold 头 TGrid +90 且仅拖头时尾不变 |
| A-6 | 已完成 | `fb14d45a` | 拖动事务状态移入 `FumenVisualEditorViewModel.DragTransactions.cs`，`ScrollViewer` 只查询 `IsGridDragTransactionActive`；Debug/Release 0 error；grid-placement 255/255；MCP e2e 1236/1236 |
| B-2 | 已完成 | `83c70558` | `LaneGeometry.BuildHorizontalRanges/TryGetHorizontalRanges` 与 `DockableObjectInteractiveAction` 共用同 T 水平段构建；Debug/Release 0 error；grid-placement 255/255；MCP e2e 1236/1236 |
| B-4 | 已完成 | `dc6fddec` | 删除 `SelectionGridDrag.TryMove/TryMoveWithSnapping` 与 `LaneSwitchResolver.CandidateQueryCount`；测试改用私有 `TryMoveCore`/`candidates` 反射；Debug/Release 0 error；grid-placement 255/255；MCP e2e 1236/1236 |
| B-5 | 已完成 | `40b0873d` | 新增 `LaneSnapDistancePixels=8`/`XGridMagneticDistancePixels=4`，四处阈值统一引用；Debug/Release 0 error；grid-placement 255/255；MCP e2e 1236/1236 |
| B-9 | 已完成 | `905a0fc7` | Default/Connectable/Dockable 拖动统一委托 SelectionGridDrag，删除 dragInfoMap/旧组合撤销/ISA 专用 action；Debug/Release 0 error；grid-placement 257/257；MCP e2e 1236/1236；真机 ISA 单角拖动 head=50/tail=100 且事务释放 |
| C-1 | 已完成 | `2d0e7943` | 清理生产代码全限定名，使用短名与 `SystemAction` 别名；Release 0 error；grid-placement 257/257 |
| C-4 | 已完成 | `00fd17ce` | `LaneGeometry.interpolated` 增加 256 上限、生命周期注释；Release 0 error；grid-placement 257/257 |
| C-8 | 已完成 | `e24496d0` | 拖动期间监听 `ActionStack.CollectionChanged` 与 `UndoActionCount`/`RedoActionCount`，删除全栈快照；Release 0 error；grid-placement 259/259 |
| C-9 | 已完成 | `2b2588fa` | 删除 Dockable 大段注释旧实现，合并重复说明，保留关键约束；Release 0 error；grid-placement 259/259 |
| C-10 | 已完成 | `58449c50` | 拖动转发方法合并到 `UserInteractionActions`，删除专用 partial；Release 0 error；grid-placement 259/259 |
| C-11 | 已完成 | `a0aa4354` | 新增 `CancelTransientInteractions`，模式/失焦/工具箱/文档切换统一取消；Release 0 error；grid-placement 259/259 |
| C-13 | 已完成 | `81dd757c` | 粘贴位置只做一次 `ConvertXToXGridTotalUnit` 并复用结果，删除重复换算；Release 0 error；grid-placement 259/259；真机非 Direct 粘贴数量 +2 |
| C-14 | 已完成 | `0cf61804` | 删除克隆根对象重复 `Tag` 赋值；Release 0 error；grid-placement 259/259 |
| C-15 | 已完成 | `63e4227e` | `PasteInvalidGridPosition` 对齐生成器多行 getter 格式；Release 0 error |
| C-16 | 已完成 | `d6604d24` | 恢复三个 resx 的 UTF-8 BOM；Release 0 error；grid-placement 259/259 |
| E-2 | 已完成（按 Grid 语义） | `72b09281` | 参考点改为根对象 Grid 包围盒中心，T 镜像用 `2*anchor`；Release 0 error；grid-placement 261/261；真机四物件两行两列粘贴中心为 T=300/X=0 |
| E-3 | 已完成 | `7dc66e4b` | `CopyObjects` 先克隆后替换，异常保留旧剪贴板；Release 0 error；grid-placement 262/262 |
| B-3 | 已完成（随 A-3） | `446b1edf` | `CalculateOriginalLaneX` 与测试反射调用删除，贝塞尔投影测试改用 `LaneGeometry.CreateProjector`；grid-placement 249/249 |

说明：

- A-2 只完成“移动 helper + 消除 `ViewModels → Kernel.DefaultImpl`”的第一阶段；A-3 已完成，`CreateOriginalLaneProjector` 已删除，但 `DefaultFumenEditorClipboard` 仍调用 `InteractiveManager.AdjustPastedObjects`（Kernel→ViewModels 未完全消除）。
- A-1/A-2 的单元回归使用 `tests/grid-placement`（228 项）；A-4 为 229 项；B-1/A-3 为 249 项；A-5/A-6/B-2/B-4/B-5 为 255 项；B-9 为 257 项；C 批为 259 项；E-2/E-3 最终为 262 项（四物件中心、异常克隆保留断言）。
- 测试直接引用的 helper 已改为 public，`InternalsVisibleTo("GridPlacement")` 已删除（`7f9f659b`）；`LaneSwitchResolver`/`SelectionGridDrag` 仍保持 internal，测试通过反射访问，不依赖友元。
- 真机测试使用自构建 Debug 实例（后台启动并最小化），先跑完整 `node tests/mcp-e2e/run.mjs`（1236 项），再通过 MCP 运行时脚本驱动真实剪贴板/拖动路径；临时脚本位于 `tests/mcp-e2e/.tmp/`（已清理）。
- 本轮不改变 Avalonia 同步点 `8b2940785`；Avalonia 树未修改。
