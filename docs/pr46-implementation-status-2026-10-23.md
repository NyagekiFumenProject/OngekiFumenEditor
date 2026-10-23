# PR #46 功能实施记录（WPF，方案 E 基线）

- 日期：2026-10-23
- 需求来源：[`pr46-isa-placement-soflan-group-functional-requirements.md`](pr46-isa-placement-soflan-group-functional-requirements.md)
- 当前基线：`master` `1e269111` + 方案 E 组树（`1b0a0e53`）
- 范围：`OngekiFumenEditor/`（WPF 树）；Avalonia 未修改
- 性质：**按当前方案 E 重新实现 PR #46 的可落地部分**，不是回滚 PR 的代码恢复

## 1. 结论摘要

| 分组 | 结果 |
| --- | --- |
| A. ISA 两点放置 | **已实现**：编辑器放置状态机 + 查看器复选框 + 右下角精简面板（可选连续放置）；两点吸附、规范化、目标组锁定、完成选中、撤销/重做 |
| B. 中断与取消 | **已实现**：复选框、右键、Esc、切文档、切模式、批量模式、工具箱拖放、快速创建、窗口失焦均会退出放置 |
| C. 组属性编辑 | **已实现**：SoflanGroup 改为“点击按钮 → 弹出树状组选择对话框”（C-2/C-3/C-6），层级/颜色/#组号与查看器树一致，默认选中并滚动到当前组；支持双击直接确认和用默认名快速新建组；多选/统一赋值/只读沿用通用属性浏览器，组属性变更立即重建绘制归属缓存（C-7） |
| D. 新建变速组 | 沿用现有弹窗创建；D-1 的空名回退与内联输入框交互与 PR #46 不同（见 §4.1） |
| E. 删除变速组 | **已实现**：有引用时弹对话框，支持转移/连同引用删除/取消；整体可撤销，恢复组位置、引用对象与选中状态 |
| F. 持久化 | 组布局仍按方案 E 保存到项目文件；F-5/S-6 与 PR #46 的 SGWI 口径冲突，按项目现状保留（见 §4.2）；F-4 编号复用缺陷已修复 |
| G. 组色可视化 | **暂未实现** 15% 物件染色（G-1～G-7）；当前组色只用于 ISA 区域、变速线、判定线速度文本与树色块 |
| H. 界面与本地化 | ISA 复选框/三态 ToolTip、删除对话框、三语资源、撤销名已补齐；H-1 仍使用弹窗而非内联输入框 |
| U. 撤销与一致性 | ISA 创建、组属性、建组、删组、转移/连带删除均进入撤销栈；取消路径无副作用 |
| S. 范围 | S-1/S-2/S-3 满足；S-5/S-6 与方案 E 现状冲突并在 §4 记录 |

## 2. 逐条对照

### A. ISA 两点放置

| 编号 | 当前实现 | 状态 |
| --- | --- | --- |
| A-1 | `CanStartIsaPlacement` 要求设计模式、非批量模式、有谱面、当前激活编辑器；目标组取查看器选中组，未选中时默认组 0 | 已实现 |
| A-2 | 查看器工具栏 `IsIsaPlacementEnabled` 复选框；激活后编辑器右下角显示精简的 `IsaPlacementOverlayView`（`组名 · 阶段`、连续放置复选框、取消按钮） | 已实现 |
| A-3 | `OnMouseDown` 左键交给 `TryHandleIsaPlacementMouseDown` → `TryTakeIsaPlacementPoint`；第一点缓存，第二点创建 | 已实现 |
| A-4 | 取点复用 `DefaultObjectInteractiveAction.TryMoveCanvas`，沿用当前 TGrid/XGrid 磁吸与 `ForceXGridIntegerUnit` 设置 | 已实现 |
| A-5 | `IsaPlacementSession.TryTakePoint` 对 TGrid/XGrid 分别取 min/max，反序点击也会得到正确起止 | 已实现 |
| A-6 | 世界坐标 Y<0 与 `CheckAndNotifyIfPlaceBeyondDuration` 检查；超界时提示且不创建（沿用编辑器既有的“允许越界放置”全局设置） | 已实现 |
| A-7 | `IsaPlacementSession.TargetSoflanGroupId` 在激活时锁定，取点期间不变 | 已实现 |
| A-8 | 删除目标组时查看器调用 `CancelIsaPlacementIfTargetGroupRemoved`；完成前再次校验组存在 | 已实现 |
| A-9 | 默认完成或取消后 session 回到 `Inactive`；面板勾选“连续放置”后完成一个 ISA 仍保持 `WaitingFirstPoint`，直到取消 | 已实现（可选连续模式） |
| A-10 | 创建后清除其他选中、选中新 ISA、刷新属性浏览器、激活编辑器布局项 | 已实现 |
| A-11 | `LambdaUndoAction` 创建/移除；撤销移除对象，重做恢复并重新选中 | 已实现 |
| A 补充 1 | 放置期间左键事件被消费，不进入普通选择/框选/拖拽 | 已实现 |
| A 补充 2 | 吸附统一走编辑器现有交互器，不另建网格 | 已实现 |
| A 补充 3 | 查看器 ToolTip 与右下角面板区分“等待第一点/等待第二点” | 已实现 |
| A 补充 4 | 完成后清空第一点；下次激活从新第一点开始 | 已实现 |

### B. 中断与取消

| 编号 | 当前实现 | 状态 |
| --- | --- | --- |
| B-1 | 复选框再次取消（`IsIsaPlacementEnabled=false` → `CancelIsaPlacement`） | 已实现 |
| B-2 | 项目没有持续“工具”状态；快速创建、工具箱拖放等接管路径会取消（见 B-7） | 已实现（按现有工具模型） |
| B-3 | `EditorProjectData` 切换、`OnPlacementActiveEditorChanged`、`OnDeactivateAsync` 取消 | 已实现 |
| B-4 | `Window.Deactivated` 订阅（激活/视图加载时挂接），仅当前激活编辑器响应 | 已实现 |
| B-5 | 放置期间右键取消；Esc 走 `KeyboardAction_CancelSelectingObjects` 优先取消放置 | 已实现 |
| B-6 | `SwitchMode` 开头取消；`BatchModeSwitchCommandHandler` 切换批量模式前取消 | 已实现 |
| B-7 | `Grid_DragEnter`/`Grid_Drop`、`TryCreateObjectAtMouse`、`ToggleBatchProjectilePlacement` 取消 | 已实现 |
| B-8 | 取消只清空 session，不创建对象、不写撤销栈、不保留第一点 | 已实现 |

### C. 变速组属性编辑

| 编号 | 当前实现 | 状态 |
| --- | --- | --- |
| C-1 | Soflan / Keyframe Soflan / ISA / ISA 结束端点均暴露 `SoflanGroup`，属性浏览器可见 | 已有 |
| C-2 | 新增 `IPropertyUIGenerator` 优先机制与 `SoflanGroupTypeUIViewModel`；属性行显示当前组名按钮，点击弹出树状 `SoflanGroupSelectDialogView`，按组文件夹/组层级排列，带颜色块与 `#组号`；每次打开都从当前组树重新构建；底部「新建变速组」用默认名称直接生成待选组，不弹命名框 | 已实现 |
| C-3 | 单选时按钮显示当前组；对话框默认选中该组并滚动到该项；选择后通过 `IObjectPropertyAccessProxy.ProxyValue` 写入；双击组等同确认 | 已实现 |
| C-3 补充 | 多选对象组值不同时（`UnsetValue`）对话框不默认选中、不滚动；选择新组仍统一赋值 | 已实现 |
| C-3 快速新建 | 快速新建先在对话框内加入待创建节点（默认名、唯一组号、当前分类下），确认时才按默认名写入组树并进入撤销栈；连续快速新建分配不同组号 | 已实现 |
| C-4 | 多选混合值由通用 `MultiObjectsPropertyInfoWrapper` 返回 `DependencyProperty.UnsetValue`，属性按钮显示“多个值” | 已有 |
| C-5 | 多选统一赋值沿用 `UndoableMultiObjectPropertyInfoWrapper` | 已有 |
| C-6 | 只读属性沿用 `PropertiesUIGenerator` 的 `IsEnabled` 控制；对话框只列当前有效组，无法选择不存在的组 | 已实现 |
| C-7 | `OnFumenObjectModifiedChanged` 检测 `ISoflan.SoflanGroup` 后立即 `RebuildObjectSoflanGroupRecord()`；ISA 端点代理属性同样触发父对象通知 | 已实现 |
| C-8 | 组属性修改走 `PropertySetAction`，可撤销/重做（GUI 烟测验证） | 已有 |
| C 补充 1 | 属性按钮与选择对话框显示 `SoflanGroupDisplayName.FormatGroup`（默认组本地化名、空名 `#id`、否则组名） | 已实现 |
| C 补充 2 | 多选仍只显示共有属性（通用属性浏览器逻辑） | 已有 |
| C 补充 3 | 组属性修改不改动几何、速度等其他属性 | 已有 |

### D. 新建变速组

| 编号 | 当前实现 | 状态 |
| --- | --- | --- |
| D-1 | 工具栏“新建变速组”弹窗输入名称；空名回退资源 `SoflanGroupDefaultNewGroupName`（“新的变速组”），**不是 PR #46 的数字编号** | 部分实现（交互/文案差异） |
| D-2 | 允许空组，不要求引用 | 已有 |
| D-3 | 新组立即进入树并自动选中；属性浏览器组选择对话框在打开时刷新 | 已实现 |
| D-4 | `LambdaUndoAction` 新建/撤销/重做；新建位置为父级末尾 | 已有 |

### E. 删除变速组

| 编号 | 当前实现 | 状态 |
| --- | --- | --- |
| E-1 | 默认组 0 拒绝删除并提示 | 已有 |
| E-2 | 无引用组直接删除，撤销恢复节点与位置 | 已有 |
| E-3 | 有引用时弹出 `SoflanGroupDeleteDialogView`：显示引用数量、替代组下拉、“转移并删除 / 连同引用删除 / 取消” | 已实现 |
| E-4 | 对话框取消直接返回，不修改数据、不写撤销栈 | 已实现 |
| E-5 | 转移或连带删除都包在单个 `LambdaUndoAction` 中 | 已实现 |
| E-6 | 撤销恢复组节点、原位置、引用对象原组归属与选中状态；并重新选中被恢复组 | 已实现 |
| E-7 | “连同引用删除”撤销时重新 `AddObject` 恢复 Soflan/ISA 及其选中状态 | 已实现 |

### F. 持久化

| 编号 | 当前实现 | 状态 |
| --- | --- | --- |
| F-1 | 对象组归属随谱面保存；组定义/组名/组文件夹/顺序/空组随 `*.nyagekiProj` 的 `SoflanGroupTree` 保存 | 方案 E 口径（与 PR #46 不同） |
| F-2 | 打开项目恢复完整组树；单独打开 `.ogkr` 只恢复由对象引用推导出的组，名称/空组/文件夹不恢复 | 方案 E 口径 |
| F-3 | 旧谱面无组数据回退默认组 0；旧 `[SGWI]`/`[SGF]`/`[SGC]` 仍可读取迁移 | 已有 |
| F-4 | 删除组时把组 ID 放入 `reservedDeletedGroupIds`，新建组避开；撤销恢复后释放。模型回归覆盖 | 已修复 |
| F-5 | 谱面不再写 `[SGWI]`（方案 E 已改为项目文件） | **与 PR #46 冲突，按现状保留** |
| F-6 | 不改变原生 SFL 导出；谱面 formatter 不写组布局 | 已有 |

### G. 组色可视化

| 编号 | 当前实现 | 状态 |
| --- | --- | --- |
| G-1 | 无物件染色机制；ISA 区域/变速线仅设计模式绘制 | 未实现 |
| G-2 | 无“非默认组才染色”的物件 tint；ISA 区域与变速线对组 0 也使用组色 | 未实现/部分冲突 |
| G-3~G-7 | 无 15% 混色、alpha 保留、批次隔离、源色保护逻辑 | 未实现 |

原因：当前绘制目标按对象类型各自决定颜色，没有统一的物件颜色入口；实现 15% tint 需要改动所有 draw target 或引入跨批次的颜色注入，视觉回归风险高。按用户“问题严重的功能可暂不做但记录”的要求，本次保留现状。组色仍用于 ISA 区域、变速线、判定线速度文本与树色块。

### H. 界面与本地化

| 编号 | 当前实现 | 状态 |
| --- | --- | --- |
| H-1 | 查看器工具栏提供新建组/新建文件夹/重命名/删除/移动到根；组名仍用弹窗输入，未恢复内联输入框 | 部分实现（按现有交互） |
| H-2 | 复选框 ToolTip 根据未激活/等待第一点/等待第二点显示三语文案 | 已实现 |
| H-3 | 删除对话框显示引用数量、替代组与三个操作 | 已实现 |
| H-4 | 默认组不可删、文件夹非空、引用数量、名称必填/重名、目标组失效、无法启动等均有提示 | 已实现 |
| H-5 | 新增文案覆盖 `Resources.resx`、`Resources.zh-Hans.resx`、`Resources.ja.resx` 与 Designer；默认组显示名统一为「默认变速组」/「Default soflan group」/「既定変速グループ」 | 已实现 |
| H-6 | ISA 创建、建组、删组、转移/连带删除使用本地化撤销名 | 已实现 |

### U. 撤销与状态一致性

| 编号 | 当前实现 | 状态 |
| --- | --- | --- |
| U-1 | ISA 创建、组属性修改、新建组、删除组、转移/连带删除均进入撤销栈 | 已实现 |
| U-2 | 取消放置、取消对话框、无效操作不修改谱面、不产生撤销项 | 已实现 |
| U-3 | 撤销恢复组归属、对象选中状态、组节点位置；删除后重建选中；ID 保留避免恢复冲突 | 已实现 |
| U-4 | 放置会话、组删除/转移/恢复均为单次状态切换，无半成品 | 已实现 |

### S. 范围与非目标

| 编号 | 结论 |
| --- | --- |
| S-1 | 满足：不改变原生 SFL 导出 |
| S-2 | 满足：仅 WPF；Avalonia 未改，只回写 `Avalonia/WPF-MIGRATION-SYNC.md` |
| S-3 | 满足：未涉及 Unity/安装程序/批量修改用户谱面 |
| S-4 | 基本满足：未扩展属性浏览器对外接口；新增的 `IPropertyUIGenerator` 是内部生成器扩展点 |
| S-5 | **与现状冲突**：方案 E 已提供重命名、拖拽移动、根级组文件夹；本次未新增任意层级嵌套或手动组色配置 |
| S-6 | **与现状冲突**：组布局保存在项目文件，不写 SGWI；按方案 E 保留 |

### V. 验收要点

| 编号 | 结果 |
| --- | --- |
| V-1 | GUI 烟测：两点创建、边界规范化、自动选中、撤销/重做通过 |
| V-2 | 代码路径覆盖复选框/模式切换；真实鼠标右键/Esc/失焦未用自动化输入驱动（见 §5） |
| V-3 | GUI 烟测：单选对话框改归属、撤销通过；多选混合值沿用通用回归 |
| V-4 | 空组随项目文件往返；新建/删除撤销由模型与 GUI 烟测覆盖 |
| V-5 | GUI 烟测：转移、连同引用删除、取消三条路径与撤销恢复通过 |
| V-6 | 默认组保护、旧谱面回退、删除编号保留（模型测试）通过 |
| V-7 | 未实现 15% 染色，未验收 |
| V-8 | `tests/soflan-group-tree` 290 项通过；构建 0 error |

## 3. 主要改动

### 3.1 新增文件

| 文件 | 作用 |
| --- | --- |
| `OngekiFumenEditor/Modules/FumenVisualEditor/Models/IsaPlacementSession.cs` | 无 UI 的两点状态机与边界规范化 |
| `OngekiFumenEditor/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.IsaPlacement.cs` | 放置进入/取点/完成/取消、窗口失焦、覆盖层状态 |
| `OngekiFumenEditor/Modules/FumenVisualEditor/Views/IsaPlacementOverlayView.xaml(.cs)` | 右下角放置面板 |
| `OngekiFumenEditor/Modules/FumenSoflanGroupListViewer/Models/SoflanGroupDisplayName.cs` | 树/属性按钮/对话框共用的组显示名 |
| `OngekiFumenEditor/Modules/FumenSoflanGroupListViewer/Models/SoflanGroupOption.cs` | 组选项 |
| `OngekiFumenEditor/Modules/FumenSoflanGroupListViewer/Models/SoflanGroupReferenceSnapshot.cs` | 引用收集、转移、删除、恢复与选中快照 |
| `OngekiFumenEditor/Modules/FumenSoflanGroupListViewer/Models/SoflanGroupDeleteChoice.cs` | 删除对话框选择 |
| `OngekiFumenEditor/Modules/FumenSoflanGroupListViewer/ViewModels/SoflanGroupDeleteDialogViewModel.cs` | 删除对话框逻辑 |
| `OngekiFumenEditor/Modules/FumenSoflanGroupListViewer/Views/SoflanGroupDeleteDialogView.xaml(.cs)` | 删除对话框 UI |
| `OngekiFumenEditor/UI/Controls/ObjectInspector/UIGenerator/IPropertyUIGenerator.cs` | 按属性语义定制属性编辑器 |
| `OngekiFumenEditor/Modules/FumenObjectPropertyBrowser/UIGenerator/SoflanGroupPropertyUIGenerator.cs` | SoflanGroup 属性生成器 |
| `OngekiFumenEditor/Modules/FumenObjectPropertyBrowser/ViewModels/SoflanGroupTypeUIViewModel.cs` | 组选择按钮 VM |
| `OngekiFumenEditor/Modules/FumenObjectPropertyBrowser/Views/SoflanGroupTypeUIView.xaml(.cs)` | 组选择按钮 UI |
| `OngekiFumenEditor/Modules/FumenObjectPropertyBrowser/ViewModels/Dialogs/SoflanGroupSelectDialogViewModel.cs` | 树状组选择对话框逻辑、默认选中 |
| `OngekiFumenEditor/Modules/FumenObjectPropertyBrowser/ViewModels/Dialogs/SoflanGroupSelectNodeViewModel.cs` | 对话框树节点（层级/颜色/#组号） |
| `OngekiFumenEditor/Modules/FumenObjectPropertyBrowser/Views/Dialogs/SoflanGroupSelectDialogView.xaml(.cs)` | 组选择对话框 UI（MetroWindow + TreeView + 默认滚动） |
| `OngekiFumenEditor/Modules/FumenSoflanGroupListViewer/ViewModels/SoflanGroupColorBrush.cs` | 组树与对话框共用的冻结组色画刷 |

### 3.2 修改文件

| 文件 | 作用 |
| --- | --- |
| `FumenVisualEditorViewModel.cs` | 切谱面/失活/销毁取消放置；`SoflanGroup` 变更重建绘制归属 |
| `FumenVisualEditorViewModel.UserInteractionActions.cs` | 鼠标取点、Esc、切模式、快速创建、拖放取消 |
| `FumenVisualEditorViewModel.ProjectilePlacement.cs` | 切编辑器/批量炮弹取消放置 |
| `FumenVisualEditorViewModel.Drawing.cs` | 视图加载时补挂窗口失焦 |
| `FumenVisualEditorView.xaml` | 挂载 `IsaPlacementOverlayView` |
| `BatchModeSwitchCommandHandler.cs` | 切批量模式取消放置 |
| `IFumenSoflanGroupListViewer.cs` / `FumenSoflanGroupListViewerViewModel.cs` | 复选框状态、三态提示、删除对话框、ID 保留 |
| `FumenSoflanGroupListViewerView.xaml` | 工具栏复选框 |
| `SoflanGroupTreeNodeViewModel.cs` | 复用组显示名格式化 |
| `PropertiesUIGenerator.cs` | 属性生成器优先并统一只读状态 |
| `Properties/Resources*.resx` / `Resources.Designer.cs` | 三语新增文案 |
| `tests/soflan-group-tree/Program.cs` | 290 项回归 |

## 4. 冲突处理与保留决策

### 4.1 D-1：新建组交互与空名回退

- 现状保留：组名通过 `SoflanGroupNameDialogViewModel` 弹窗输入；空名使用“新的变速组”，不是 PR #46 的数字编号。
- 理由：方案 E 已统一使用弹窗（新建组/新建文件夹/重命名），改成内联输入框会引入第二套编辑入口；空名回退文案在方案 E 已三语化。
- 影响：需求 D-1 的“列表输入框”和“数字编号回退”不满足，但新建组核心行为（空名可用、立即出现、可撤销）满足。

### 4.2 F-5 / S-6：SGWI vs 项目文件

- 现状保留：组布局写入 `*.nyagekiProj` 的 `SoflanGroupTree`；谱面文件不写 `[SGWI]`/`[SGF]`/`[SGC]`，但保留读取迁移。
- 理由：这是方案 E 的既定设计，也是当前审查文档明确选择的持久化方式；改回 SGWI 会推翻组树布局/空组/文件夹的项目级持久化。
- 影响：F-1/F-2 的“组信息随谱面保存/重开谱面恢复”只在项目文件中成立；单独打开 `.ogkr` 不恢复组名/空组/文件夹。PR #46 的 F-5/S-6 不满足。

### 4.3 S-5：已存在的重命名/拖拽/组文件夹

- 现状保留：方案 E 已实现组重命名、拖拽移动、根级组文件夹；本次没有删除这些能力。
- 本次未新增：任意层级嵌套、手动组色配置。
- 影响：S-5 的“本 PR 未新增……”与当前分支实现不一致，记录为范围差异。

### 4.4 A-6：越界放置的全局设置

- 现状保留：`CheckAndNotifyIfPlaceBeyondDuration` 在 `EnablePlaceObjectBeyondAudioDuration` 开启时允许越界，不弹提示；这是编辑器既有行为。
- 影响：PR #46 的“越界时一律提示且不创建”在开启该设置时不成立；关闭该设置时满足。

### 4.5 G 组：15% 组色 tint

- 未实现，原因见 §2.G。
- 若后续实现，建议新增统一的“物件颜色后处理”入口，或按 draw target 注入组色；需要覆盖 Tap/Hold/Flick/Bell/Bullet/Lane 等全部目标，并验证预览模式不染色、默认组不染色、alpha/选中高亮不被污染。

## 5. 验证记录

| 检查 | 结果 |
| --- | --- |
| `dotnet build OngekiFumenEditor.csproj -c Debug` | 0 error |
| `dotnet build OngekiFumenEditor.csproj -c Release` | 0 error |
| `tests/soflan-group-tree` | 290 项通过（原 241 + 新增 49） |
| GUI：查看器复选框 | UIAutomation TogglePattern 可勾选/取消；状态从 `Inactive` → `WaitingFirstPoint`，取消后回到 `Inactive` |
| GUI：右下角面板 | 激活后自动化树出现「ISA 放置」「默认变速组 · 等待第一点」「连续放置」 |
| GUI：两点创建 | 通过 `TryTakeIsaPlacementPoint`（与 `OnMouseDown` 同一路径）创建 1 个 ISA：T 520–920、X 4–30，组 0；`editor.undo`/`redo` 数量 1→0→1 |
| GUI：连续放置 | 勾选「连续放置」后连续完成 2 个 ISA，状态保持 `WaitingFirstPoint`；取消勾选后再放 1 个，状态回到 `Inactive`，`isfarea` 计数 1→2→3 |
| GUI：组属性对话框 | 选中 Soflan 后属性行显示 `SoflanGroup` 按钮（当前组名）；点击弹出「选择变速组」树状对话框：默认组、`Folder` 文件夹、组节点均显示名称与 `#组号`，组节点使用与查看器一致的组色；选择 Probe + 确认后 `soflanGroup=2`；撤销恢复 0 |
| GUI：对话框默认滚动 | 构造 20 个组并把当前组放在末尾；对话框打开后自动选中 `G1 #1`，其 TreeItem 位于 TreeView 可视区内（Y 257，TreeView 可视区 57–278） |
| 模型：快速新建 | 连续两次快速新建生成两个不同待创建 `#组号`、默认名「新的变速组」；确认前不在组树中，`ConfirmSelection` 后按默认名写入组树 |
| GUI：默认名与快速新建 | 对话框显示「默认变速组 #0」；点击底部「新建变速组」后出现待选「新的变速组 #1」，确认后 Soflan `soflanGroup=1` |
| GUI：删除有引用组 | 对话框显示 `变速组「Probe」仍被 1 个对象引用`；「连同引用删除」移除 Soflan 与组，撤销恢复 Soflan（group 2）与 Probe 节点；「转移并删除」把 Soflan 转到默认组并移除 Probe，撤销恢复 group 2 与 Probe |
| GUI：目标组失效 | 放置激活目标 Probe 时删除 Probe，放置状态变为 `Inactive`（A-8） |
| GUI：C-7 缓存 | Debug 日志出现 `RebuildObjectSoflanGroupRecord` 调用，说明组属性变更触发重建 |

**未覆盖的验证**：

- 真实鼠标路由（`OnMouseDown`/`OnMouseUp`）未由自动化输入驱动：UIAutomation 的合成 `SendMessage` 不会进入 WPF 输入管道；真实 `SendInput` 需要把窗口带到前台，违反仓库的后台测试约定。本记录用 `TryTakeIsaPlacementPoint` 覆盖同一核心路径，但右键取消、Esc、真实拖放、窗口失焦仍需人工 GUI 验收。
- 15% tint（G 组）未实现，未验收。
- 多选混合组值的 GUI 对话框显示依赖通用属性浏览器回归；本次未单独构造多类型多选场景。
- 对话框双击确认已接到 `TreeViewItem.MouseDoubleClick`，与确认按钮共用 `ConfirmSelection`；UIAutomation 的合成输入无法可靠触发 WPF 双击，仍需人工点两下验收。

## 6. 后续建议

1. 人工验收：真实鼠标两点放置、右键/Esc、切工具/切文档、窗口失焦、批量模式与工具箱拖放下的取消路径。
2. 若必须满足 PR #46 的 SGWI 口径，需要单独设计“项目文件 + SGWI 双写/迁移”方案，不能直接回退方案 E。
3. 若要做 G 组 tint，先定义统一的物件颜色注入点，再逐目标迁移并补视觉回归。
4. `RebuildObjectSoflanGroupRecord` 目前在 UI 线程全量重建，组属性频繁撤销时可能与渲染线程并发（审查文档 SG-10）；后续可考虑增量更新或统一到帧首重建。
5. 删除/转移有大量引用的组时，`SoflanGroupReferenceSnapshot` 逐对象设置 `SoflanGroup`，每个对象都会触发一次 `RebuildObjectSoflanGroupRecord`；引用数很大时需要批量抑制或合并重建，避免删除对话框确认后短暂卡顿。
