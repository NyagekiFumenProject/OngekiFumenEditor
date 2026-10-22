# 变速分组查看器功能审查（WPF 树）

> **状态更新（2026-10-22）**：PR #46 已回滚，本文件的问题清单基于 `9171b339`（含 PR #46），不适用于当前 `master`。当前树状显示审查与重构建议见 `docs/soflan-group-tree-display-refactor-2026-10-22.md`。

- 日期：2026-10-22
- 基线：`master` `4c9bff35` + PR #46 `9171b339`（合并提交 `af5d65ee`）
- 范围：`Modules/FumenSoflanGroupListViewer/**` 及其依赖的组模型（`SoflanGroupWrapItem*`、`IndividualSoflanAreaListMap`）、SGWI 持久化（`DefaultOngekiFumenFormatter` / `SoflanGroupWrapItemCommandParser`）、组色（`SoflanGroupTintBuilder`）、ISA 放置与组选择器
- 方法：源码核验 + 无窗口临时探针（未入库）+ 现有 `tests/isa-placement` 回归
- 性质：**审查清单，未修改功能代码**；实施后按仓库约定回写 `Avalonia/WPF-MIGRATION-SYNC.md`

## 结论摘要

查看器的主流程（组树、Design/Preview/Soflans 三列、新建/删除组、右侧变速点明细、撤销）可用，构建与现有 53 项回归均通过。但存在 **2 个数据正确性缺陷、4 个功能缺口** 和若干体验/健壮性问题；其中 SG-1、SG-2 会直接影响谱面内容或组结构。

| 编号 | 级别 | 问题 |
|---|---|---|
| SG-1 | 高 | 仅由 `SFL` 引用的组不进入组树：查看器看不到、无法管理、组名不写 SGWI |
| SG-2 | 高 | 同名变速组保存后合并，分组结构丢失 |
| SG-3 | 中 | 保存无条件写 `[SGWI] default 0`，所有谱面多一行 |
| SG-4 | 中 | 组层级不持久化：嵌套组重载后被拍平 |
| SG-5 | 中 | 组名无法修改：Name 列只读，建错只能删除重建 |
| SG-6 | 中 | 新建组默认不勾选 Soflans，设计模式看不到该组变速线 |
| SG-7 | 中 | 撤销新建组后单选/选中状态可能不一致 |
| SG-8 | 中 | 拖动/编辑时组归属缓存全量重建（性能/GC） |
| SG-9 | 中 | 组色跨会话随机、查看器无颜色图例 |
| SG-10 | 中 | 渲染线程重建与 UI 线程并发访问组缓存/组列表 |
| SG-11 | 低 | 删除无引用组无确认；删除按钮无 ToolTip；无选中无反馈 |
| SG-12 | 低 | Checked 事件中的异常可能崩 UI |
| SG-13 | 低 | 列表刷新导致滚动位置重置 |
| SG-14 | 低 | `default` 容器名硬编码、界面中英混杂；魔法字符串 |
| SG-15 | 低 | ISA 组变更不通知查看器刷新 |
| SG-16 | 低 | 右侧明细的 `EditorShowSoflanPointList` 开关未实现 |
| SG-17 | 低 | 组容器不可拖拽/重命名/删除；层级不可编辑 |
| SG-18 | 低 | 现有测试未覆盖 SG-1~SG-7；测试未接入 CI |

---

## 现状梳理

- **数据模型**：`IndividualSoflanAreaListMap` 持有 `SoflanGroupWrapItemGroupRoot`（含固定 `defaultItemGroup`）与 `cachedSoflanGroupWrapItemMap`；组树节点为 `SoflanGroupWrapItemGroup`（容器）与 `SoflanGroupWrapItem`（组项，`SoflanGroupId` 不可变）。
- **UI**：左侧 `ListView` 扁平显示 `DisplayableItemSource`，Name 列用缩进表示层级；Design/Preview 是两态 CheckBox；Soflans 列是全局互斥 RadioButton，绑定 `IsDisplaySoflanDesignMode`。右侧明细（`DisplaySoflanPointList`）与标题跟随 `CurrentSelectedSoflanGroupWrapItem`。
- **Soflans 列的真实作用**：不是切换右侧明细，而是控制画布上绘制哪一组的变速线（`DurationSoflanDrawingTarget` 过滤 `IsDisplaySoflanDesignMode`）与判定线颜色（`DrawJudgeLineHelper` 读取 `CurrentSoflansDisplaySoflanGroupWrapItem`）。右侧明细始终跟随 `CurrentSelected`。两套选择状态并存，容易误解。
- **持久化**：SGWI 行格式为 `[SGWI]\t<组名>\t<组ID...>`；formatter 按 `Parent.DisplayName` 分组，parser 从 root 直接子组按组名查找/新建。
- **关联**：ISA 两点放置读取查看器的 `CurrentSelectedSoflanGroupWrapItem`；属性浏览器的组属性使用 `SoflanGroupTypeUIViewModel` + `SelectionDisplayName`；组色由 `IndividualSoflanAreaDrawingTarget.CalculateColorBySoflanGroup` 提供。

---

## 问题清单

### SG-1（高）仅由 `SFL` 引用的组不进入组树

- **位置**：`OngekiFumen.Setup`（`Base/OngekiFumen.cs:146-174`）、`SoflanCommandParser`（`Parser/Ogkr/CommandParserImpl/SoflanCommandParser.cs:33/49/67`）、`IndividualSoflanAreaListMap.GetSoflanGroups`（`Base/Collections/IndividualSoflanAreaListMap.cs:159-174`）
- **机制**：解析时 `Soflan.SoflanGroup` 在 `fumen.AddObject` 之前设置，不触发 `SoflansMap` 的 `SoflanGroup` 属性事件；`Setup` 只对 `SoflansMap.Keys` 创建 ISA 列表（`_ = IndividualSoflanAreaMap[...]`），不调用 `TryGetOrCreateSoflanGroupWrapItem`。因此组树中没有该组的 item。
- **复现**（临时探针）：向 `OngekiFumen` 添加 `SoflanGroup=3` 的 `Soflan` 后，`GetSoflanGroups()` 只有 `0`；运行时把 `SoflanGroup` 改为 `4` 后才出现 `0,4`；formatter 输出 `[SGWI] default 0 4`，组 3 完全不出现。
- **影响**：
  - 查看器看不到、无法选中该组，也就无法把它作为 ISA 放置目标、无法勾选 Soflans、无法删除或转移引用；
  - 该组的组名不会写入 SGWI（旧版本同样不写，但 PR #46 的“空组持久化/组管理”未覆盖此场景）；
  - 用户新建组会分配一个新 ID（`GetNextSoflanGroupId` 会避开 `SoflansMap.Keys`），无法接管已有组 3。
- **建议**：在 `OngekiFumen.AddObject` 的 `ISoflan` 分支（或解析完成后的统一补齐步骤）调用 `TryGetOrCreateSoflanGroupWrapItem(soflan.SoflanGroup)` 并挂到 `default` 容器；同时按 SG-2/SG-3 的格式策略决定是否把这些组写入 SGWI。

### SG-2（高）同名变速组保存后合并

- **位置**：`SoflanGroupOperations.CreateAddAction`（`Modules/FumenSoflanGroupListViewer/SoflanGroupOperations.cs:13-31`）、`DefaultOngekiFumenFormatter`（`:82-84`）、`SoflanGroupWrapItemCommandParser`（`:21-32`）
- **机制**：`CreateAddAction` 不校验重名；formatter 用 `GetSoflanGroups().GroupBy(x => x.Parent?.DisplayName)`；parser 用 `FirstOrDefault(x => x.DisplayName == groupName)`。
- **复现**（临时探针）：创建两个名为 `dup` 的组（ID 1、2），保存得到 `[SGWI]\tdup\t1\t2`；重载后并为一个 `dup` 组。
- **影响**：组结构/名称丢失；删除或转移引用时可能跨组误操作。空名新建组会使用编号，降低重名概率，但同名仍可输入。
- **建议**：新建时校验名称唯一（资源提示）或自动追加后缀；更稳妥的是 SGWI 以组 ID 为稳定键（保留旧“按名解析”作为兼容 fallback）。

### SG-3（中）保存无条件写默认组行

- **位置**：`DefaultOngekiFumenFormatter.cs:80-86`；`GetSoflanGroups()` 包含 `defaultItemGroup` 下的默认组 item 0。
- **复现**（临时探针）：空 `OngekiFumen` 序列化得到 `[SGWI]\tdefault\t0`；旧实现只在组有 ISA 时写。
- **影响**：所有保存的谱面都会新增一行；与旧版本输出不一致，产生无意义 diff。parser 可解析，不影响加载。
- **建议**：formatter 排除 `SoflanGroupId == 0` 的 item（默认组无需 SGWI），并补充“空谱面不写 SGWI”的回归断言。

### SG-4（中）组层级不持久化

- **位置**：`DefaultOngekiFumenFormatter` 按 `Parent.DisplayName` 分组；`SoflanGroupWrapItemCommandParser` 只从 `SoflanGroupWrapItemGroupRoot.Children` 的直接子组中按名称查找。
- **机制/影响**：若把组项拖入子组（`DisplayableItemSource` 支持层级显示），保存时仍只写组名；重载时所有组名都变成 root 直接子组，层级被拍平；同名组还会合并（SG-2）。当前 UI 也只允许拖 item、不允许拖组容器，因此实际层级几乎只有 `default` 一层。
- **建议**：SGWI 增加父组/路径字段并让 parser 支持；或明确“仅支持一层”并在 UI 禁止把 item 拖入子组，避免展示与持久化不一致。

### SG-5（中）组名无法修改

- **位置**：`SoflanGroupWrapItem.DisplayName`（`Base/EditorObjects/SoflanGroupWrapItem.cs:24-28`，setter 为空实现）；列表 Name 列是只读 `TextBlock`（`FumenSoflanGroupListViewerView.xaml:194`）；组容器 `SoflanGroupWrapItemGroup.DisplayName` 可写，但没有任何编辑入口。
- **影响**：建错名字只能删除重建；空名生成纯编号后不可读；重命名无法撤销/无法持久化（组容器名会写 SGWI，但 UI 改不了）。
- **建议**：Name 列双击编辑组容器 `DisplayName`（带 `LambdaUndoAction` 与刷新）；item 名继续由 ID/默认组资源决定。

### SG-6（中）新建组默认不勾选 Soflans，设计模式看不到该组变速线

- **位置**：`SoflanGroupWrapItem.IsDisplaySoflanDesignMode` 默认 `false`（`SoflanGroupWrapItem.cs:53-58`）；`CreateAddAction` 未设置；`DurationSoflanDrawingTarget.cs:194` 只绘制 `IsDisplaySoflanDesignMode` 的组。
- **影响**：新建组后设计模式看不到该组变速点，用户需手动勾选 Soflans 列（全局单选，会取消原绘制组），容易误判“组里没有变速”。默认组 0 在 `Setup` 中被设为 `true`，所以只有非默认组受影响。
- **建议**：新建组时默认勾选（或沿用当前绘制组并在 changed 中通知）；若保持默认 false，至少在新建成功 Toast/提示中说明。

### SG-7（中）撤销新建组后单选/选中状态可能不一致

- **位置**：`SoflanGroupOperations.CreateAddAction` 的 undo 只移除 item/group，不恢复默认组 `IsSelected`；`FumenSoflanGroupListViewerViewModel.RebuildItemGroupRoot`（`:231-266`）在无 `IsSelected` 项时只 `??=` 设置 `CurrentSelected`，不设置 `IsSelected`。
- **影响**（静态分析，未做 GUI 验证）：新建组时 RadioButton 会把默认组的 `IsSelected` 置 false；撤销后默认组 item 回到树中但 `IsSelected` 仍为 false，列表可能没有任何单选按钮被选中，而右侧明细/ISA 目标已回落到默认组。行为正确但视觉误导。
- **建议**：`RebuildItemGroupRoot` 在找不到选中项时显式把默认组 `IsSelected = true`；或 undo 时恢复上一选中项。

### SG-8（中）拖动/编辑时组归属缓存全量重建（性能/GC）

- **位置**：`SoflanGroupCacheInvalidation`（`Modules/FumenVisualEditor/Base/SoflanGroupCacheInvalidation.cs:41-52`）、`FumenVisualEditorViewModel.Drawing.cs:349`、`UserInteractionActions.cs:1128`
- **机制**：任何对象的 `TGrid/XGrid/Unit/Grid/SoflanGroup/EndTGrid` 变化都会使缓存失效；普通 Tap/Hold/ISA 拖动每帧修改这些属性，下一渲染帧执行 `GetAllDisplayableObjects()`（LINQ/`Distinct`）+ `Parallel.ForEach` + 每对象 `QuerySoflanGroup` + `ToFrozenDictionary()`。
- **实测**（合成 5000 个 movable + 8 个 ISA）：重建本体 2.46 ms/帧（串行）、0.83 ms/帧（16 核并行）；不含枚举、ObjectPool 与每帧 FrozenDictionary 分配，实际更高。
- **建议**：区分“影响组归属的变化”（ISA 端点/组属性/组增删）与普通对象移动；普通对象移动只更新该对象的组归属（`QuerySoflanGroup(obj)`），或节流到固定间隔；避免每帧 `Freeze` 全量字典。

### SG-9（中）组色跨会话随机、查看器无颜色图例

- **位置**：`IndividualSoflanAreaDrawingTarget.CalculateColorBySoflanGroup`（`Graphics/Drawing/TargetImpl/OngekiObjects/IndividualSoflanAreaDrawingTarget.cs:42-77`）的 `colorSeed = RandomHepler.Random(int.MinValue, int.MaxValue)`；`SoflanGroupTintBuilder.TintColor`（`:71`）以 15% 混色。
- **影响**：同一组 ID 每次启动颜色不同，用户无法靠颜色记忆组；查看器没有颜色列/图例；组 0 的 ISA 区域有随机色，而组 0 的对象不做 tint，视觉不一致。
- **建议**：去掉进程随机 seed，改用组 ID 的确定性哈希（或把 seed 持久化到设置）；Name 列前加颜色块；组 0 明确统一策略。

### SG-10（中）渲染线程重建与 UI 线程并发访问组缓存/组列表

- **位置**：`SwitchMode`（UI 线程）直接调用 `RebuildObjectSoflanGroupRecord`（`UserInteractionActions.cs:1118`），与渲染线程 `RefreshIfNeeded`（`Drawing.cs:349`）同时写 `_cacheSoflanGroupRecorder`（`Clear/SetCache/Freeze` 无锁）；渲染线程 `RebuildObjectSoflanGroupRecord` 对缺失组调用 `Fumen.SoflansMap[group]`，会写 `SoflanListMap` 的 `Dictionary` 并触发事件（`SoflanListMap.cs:26-38`）。
- **影响**：`ConcurrentDictionary` 本身安全，但快照可能混合；`SoflanListMap` 非线程安全，与 UI 线程读取/写入并发时可能读到不一致状态。`QuadTreeWrapper` 内部有锁，不是问题。
- **建议**：把重建统一放到 `OnEditorUpdate`（写锁内）或 UI 线程；解析/加载阶段预创建所有组列表；或给 `SoflanListMap` 索引器加锁。

### SG-11（低）删除无引用组无确认；删除按钮无 ToolTip；无选中无反馈

- **位置**：`FumenSoflanGroupListViewerViewModel.DeleteSelectedGroup`（`:127-157`）在 `references.Length == 0` 时直接 `ExecuteAction` 删除；`FumenSoflanGroupListViewerView.xaml:63` 的删除按钮没有 ToolTip；`CurrentSelected` 为空（或选中组容器）时方法直接 return。
- **建议**：删除空组时确认（可撤销但仍是破坏性操作）；补 ToolTip；无选中时 Toast 说明。

### SG-12（低）Checked 事件中的异常可能崩 UI

- **位置**：`OnItemChecked`（`:220-229`）与 `OnItemChecked_IsDisplaySoflanDesignMode`（`:209-218`）都有 `throw new Exception(...)`。
- **影响**：若 WPF 在绑定写回之前触发 `Checked`，`IsSelected`/`IsDisplaySoflanDesignMode` 仍为 false，异常会从 UI 事件冒泡导致崩溃（旧代码已有；PR #46 扩展了流程但没有消除）。
- **建议**：改为 Log/忽略，或在事件中直接使用控件状态；避免 UI 事件抛异常。

### SG-13（低）列表刷新导致滚动位置重置

- **位置**：`RebuildItemGroupRoot` 先把 `DisplaySoflanGroupItemGroupRoot = default` 再赋 root（`:233-240`），`ListView` 重新绑定；组增删、撤销、切换 Fumen 都会触发。
- **建议**：原地刷新（保持 `DisplayableItemSource` 实例或恢复滚动偏移）；展开状态已保存在组对象上，不受影响。

### SG-14（低）`default` 容器名硬编码、界面中英混杂；魔法字符串

- **位置**：`IndividualSoflanAreaListMap` 构造 `DisplayName = "default"`（`:41-51`）；`SoflanGroupWrapItem.SelectionDisplayName` 用 `"default"`/`"root"` 判断（`:30`）；parser 也按 `"default"` 找组。
- **影响**：截图中 `default` 与 `默认组（0）` 中英混杂；内部标识与显示名耦合，改显示名会破坏查找。
- **建议**：内部用对象引用/枚举标识容器，显示名走本地化资源。

### SG-15（低）ISA 组变更不通知查看器刷新

- **位置**：`IndividualSoflanAreaListMap.Add` / `OnChildPropertyChangedEvent` 创建 wrap item，但不触发 `IndividualSoflanAreaMap.PropertyChanged`；只有 `SoflanListMap` 的 Soflan 组变更会通过 `OngekiFumen.SoflansMap_OnSoflanPropertyChangedEvent` 通知。
- **影响**：ISA 的组属性变化（撤销/程序化/未来属性编辑器选择新组）新建的组 item 不会立即出现在列表，需要切换 Fumen 或触发其他重建。
- **建议**：统一在组树变化时 `NotifyOfPropertyChange(nameof(SoflanGroupWrapItemGroupRoot))`。

### SG-16（低）右侧明细的 `EditorShowSoflanPointList` 开关未实现

- **位置**：`FumenSoflanGroupListViewerView.xaml:279-288`，`IsEnabled="False"`，注释 `not support yet`。
- **建议**：实现或移除，避免留下永久禁用控件。

### SG-17（低）组容器不可拖拽/重命名/删除；层级不可编辑

- **位置**：`ListDragManager_ProcessDrop` / `CreateMoveAction`（`:276-342`）只处理 `SoflanGroupWrapItem`；组容器没有删除/重命名/拖拽入口。
- **影响**：与 SG-4/SG-5 叠加，用户只能新建平级组、删除组项，无法整理层级或修改容器名。
- **建议**：与 SG-4/SG-5 一并设计（容器双击重命名、拖拽重排/嵌套、删除空容器）。

### SG-18（低）测试覆盖缺口

- **现状**：`tests/isa-placement` 53 项覆盖 TwoPointPlacement、批量组属性、组增删/撤销、空组 SGWI 往返、缓存失效、着色；未覆盖：
  - 仅 Soflan 引用的组出现在查看器（SG-1）；
  - 同名组保存/重载（SG-2）；
  - 空谱面不写默认 SGWI 行（SG-3）；
  - 组层级往返（SG-4）；
  - 重命名（SG-5）；
  - 撤销后的选中态（SG-7）。
- **另外**：`CheckEmptyGroupsRoundTrip` 覆盖 `Caliburn.Micro.IoC.GetAllInstances` 后未恢复；`CheckCommonPropertySelection` 通过反射写私有字段、硬编码 dirty 计数，脆弱；测试工程未接入 CI。
- **建议**：补上述回归；恢复静态状态；CI 构建编辑器后运行。

---

## 非缺陷/现状说明

1. **Soflans 列不切换右侧明细**：它控制画布绘制（`DurationSoflanDrawingTarget` 过滤 + 判定线颜色），右侧明细始终跟随“当前组”（`CurrentSelected`）。设计如此，但 ToolTip/文档应明确，避免用户把两个选择状态混为一谈。
2. **默认组 0 会自动选中**：查看器首次创建时 `RebuildItemGroupRoot` 的 `??=` 会选中默认组，ISA 放置因此有默认目标；不需要用户先手动选组（除非没有加载谱面）。
3. **默认组 0 不可删除**：`DeleteSelectedGroup` 有明确提示，符合预期。
4. **新建组空名使用编号**：`CreateAddAction` 在空名时用组 ID 作容器名，三语提示已同步更新，属于设计行为。
5. **组项 `DisplayName` 只读**：item 显示编号/默认组资源，组名在容器上；这是刻意的模型设计，问题在于没有容器重命名入口（SG-5）。

---

## 验证记录

| 检查 | 结果 |
|---|---|
| `dotnet build OngekiFumenEditor.csproj -c Debug` | 0 error（89 个既有 nullable 警告） |
| `dotnet build OngekiFumenEditor.csproj -c Release` | 0 error |
| `tests/isa-placement` | 通过 53 项 |
| 空 `OngekiFumen` 的 SGWI 输出 | `[SGWI]\tdefault\t0`（SG-3） |
| 两个同名组（ID 1、2）的 SGWI 输出 | `[SGWI]\tdup\t1\t2`（SG-2） |
| 加载式添加 `SoflanGroup=3` 后的 `GetSoflanGroups()` | 仅 `0`（SG-1）；运行时改写为 4 后为 `0,4` |
| 组缓存重建基准（5000 movable + 8 ISA） | 串行 2.46 ms、并行 0.83 ms/次（16 核；不含枚举/Frozen 分配） |

未做 GUI 交互验证：SG-7、SG-11、SG-13 为静态分析；SG-6 的另一半（画布绘制过滤）依据 `DurationSoflanDrawingTarget` 的代码路径。

---

## 建议修复顺序

1. **SG-1、SG-2**：先保证“所有引用组可见、组结构不丢失”，这是数据正确性。
2. **SG-3、SG-4**：统一 SGWI 输出/层级格式，补往返测试。
3. **SG-6、SG-7、SG-9**：交互一致性（新建组可见性、撤销选中态、组色稳定/图例）。
4. **SG-8、SG-10**：性能与线程安全（增量重建、统一线程/锁）。
5. **SG-11~SG-18**：体验、健壮性与测试补强。
