# 文档安全与撤销语义实施方案（2026-08-22）

## 1. 背景与范围

本方案来自 2026-08-21 对"仍未闭环的编辑器迁移功能"审计中的第 2 类缺口（文档安全与撤销语义），并补充了对 WPF 原项目关闭协议的对照分析。核对的一手证据：

- `docs/wpf-avalonia-full-migration-audit-2026-08-07.html`（迁移审计缺口清单）；
- `docs/code-audit-live-issues-2026-08-12.html` 中的 CA-013、CA-014、CA-017、CA-018、CA-029、CA-034、CA-061、CA-062、CA-064；
- 当前 `src/` 与 `Dependencies/Gekimini.Avalonia/` 生产代码；
- WPF 原项目：`F:/Source/OngekiFumenEditor/OngekiFumenEditor/` 与其定制 Gemini 框架 `F:/Source/OngekiFumenEditor/Dependences/gemini/`。

实施顺序建议：先做"基础语义"（P0），再修具体撤销动作（P1）。

## 2. 目标语义

1. 所有持久化修改必须通过文档所属的 `IUndoRedoManager`。
2. 撤销/重做应改变文档状态；回到保存点后文档应重新变干净。
3. 被拒绝、取消或校验失败的操作不能产生空撤销记录，也不能改变脏状态。
4. 保存完成后，只有"当前文档状态仍等于保存开始时状态"才能清除脏标记和恢复快照。
5. 所有关闭入口必须经过同一个保存/放弃/取消协议。

## 3. 现状证据（Avalonia 当前实现）

### 3.1 关闭入口分裂

| 入口 | 当前行为 | 位置 |
| --- | --- | --- |
| 菜单"关闭文件" | 直接移除文档，无脏确认 | `Dependencies/Gekimini.Avalonia/src/Gekimini.Avalonia/Modules/Shell/Commands/CloseFileCommandHandler.cs:26` → `ShellViewModel.cs:196`（`Factory.RemoveDocument`） |
| 重置布局 | 逐个调用同一无确认的 `CloseDocumentAsync` | `ShellViewModel.cs:216-221`（`ResetLayout`） |
| 标签页关闭 | 有完整"保存/放弃/取消"确认 | `ShellDockFactory.cs:156`（`CloseDockable`）→ `ShellDockFactory.cs:290`（`CanCloseDocument`） |
| 应用退出 | 逐文档走 `CanCloseDocument` 确认 | `ShellViewModel.cs:493`（`OnApplicationAskQuit`） |

确认对话框逻辑只存在于 Dock 工厂层（`SaveDirtyDocumentDialogViewModel`，Yes 时保存失败会弹错误并阻止关闭），Shell 层的 `CloseDocumentAsync` 与 `RemoveDocument` 完全不经过它。

### 3.2 脏状态与保存竞态

- `IsDirty` 是可写属性：`src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.cs:65`。
- 置位来源：工程数据属性变化（`:461-475`）、谱面对象修改事件（`:477-488`，排除选中态）、撤销数量变化（`:490-493`，`UndoActionCount` 变化即置脏，只置 true，永远不会因回到保存点而变回干净）。
- 保存成功后无条件清理：`FumenVisualEditorViewModel.cs:341-369`（`Save()` 在 `:364` 直接 `IsDirty = false`，`:357` 删除恢复快照）。若保存期间发生新修改，脏标记与恢复快照都会被错误清除。
- 撤销管理器只维护 `UndoActionCount`/`RedoActionCount` 计数，没有保存点概念：`Dependencies/Gekimini.Avalonia/src/Gekimini.Avalonia/Framework/UndoRedo/DefaultImpl/DefaultUndoRedoManager.cs:20`、`:38`。

### 3.3 撤销动作缺陷（对应 CA 编号）

- 属性浏览器写回绕过撤销栈（CA-014）：`src/OngekiFumenEditor.Avalonia/Modules/FumenObjectPropertyBrowser/UIGenerator/UndoablePropertyInfoWrapper.cs:32`（`ProxyValue` setter 直接改模型）。
- 元信息编辑绕过脏状态（CA-017）：`src/OngekiFumenEditor.Avalonia/Modules/FumenMetaInfoBrowser/ViewModels/OngekiFumenModelProxy.cs:18`。
- 首拍号双权威来源（CA-018）：`src/OngekiFumenEditor.Avalonia/Base/OngekiFumen.cs:101`、`:415`。
- 渲染控制重排跨文档错栈（CA-029）。
- Lane 合并撤销只迁回部分节点（CA-034）：`src/OngekiFumenEditor.Avalonia/Modules/FumenObjectPropertyBrowser/ViewModels/MultiLanesOperationViewModel.cs:61`。
- 被拒绝的 HoldEnd 拖放仍改写关系（CA-061）：`src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.UserInteractionActions.cs:1024`、`src/OngekiFumenEditor.Avalonia/Base/OngekiObjects/Hold.cs:64`。
- 越界快速新增制造空撤销记录（CA-062）：`UserInteractionActions.cs:1001`。
- 快捷新增不可逆覆盖已有 HoldEnd（CA-064）。

## 4. P0：先修关闭和保存竞态

### P0-1. 统一关闭协议 `TryCloseDocumentAsync`

1. 在 Shell 增加统一的 `TryCloseDocumentAsync`，返回 `RequestDocumentCloseResult`（`Closed / Cancelled / SaveFailed`）结果。
   - 菜单关闭、标签页关闭、应用退出（含 `ResetLayout`）全部调用它。
   - `RemoveDocument` 改成仅供确认成功后的内部移除。
   - 迁移现有确认逻辑：对话框交互从 `ShellDockFactory.CanCloseDocument`（`ShellDockFactory.cs:290`）移入或委托给 Shell 协议；`CloseDockable` 与 `OnApplicationAskQuit` 改为调用统一入口。
2. 保存失败必须返回 `SaveFailed` 且文档保持打开（保留现有 `ShellDockFactory.cs:307-313` 的语义）。

### P0-2. 文档保存点追踪器

1. 引入文档保存点追踪器，不再仅由 `IsDirty = true` 驱动。
2. 保存点至少包含撤销历史身份和当前游标。
3. 保存开始时捕获 `saveToken`。
4. 保存成功后只有 `CurrentToken == saveToken` 才执行：

   ```csharp
   IsDirty = false;
   DeleteRecoverySnapshot();
   ```

5. 替换当前无条件清理状态的位置：`FumenVisualEditorViewModel.cs:341-369`。

### P0-3. `IsDirty` 改为计算结果

将 `IsDirty` 改成"当前状态是否等于保存点"的计算结果，而不是只监听撤销数量变化。当前撤销管理器只维护 `UndoActionCount/RedoActionCount`：`DefaultUndoRedoManager.cs:20`、`:38`。语义要求：保存后撤销一步应重新变脏；继续撤销回保存点应重新变干净。

## 5. P0-1 的 WPF 原项目对照

### 5.1 WPF 原项目的关闭协议

WPF 原项目的三个关闭入口最终都汇入同一条 Caliburn.Micro 关闭链，由文档自身（`IGuardClose`）裁决能否关闭：

```
菜单关闭  CloseFileCommandHandler.Run（Dependences/gemini/src/Gemini/Modules/Shell/Commands/CloseFileCommandHandler.cs:20-29）
          → ShellViewModel.CloseDocumentAsync = DeactivateItemAsync(document, true)（Gemini/Modules/Shell/ViewModels/ShellViewModel.cs:172）
标签关闭  LayoutItem.CloseCommand → TryCloseAsync（Gemini/Framework/Document.cs:33-37；绑定于 Gemini/Modules/Shell/Views/ShellView.xaml:102-112）
          → conductor.CloseItemAsync → 同一 DeactivateItemAsync
应用退出  WindowConductor.Closing（Caliburn.Micro/src/Caliburn.Micro.Platform/Platforms/net46-netcore/WindowConductor.cs:103-139）
          → MainWindowViewModel/ShellViewModel 的 Conductor.CanCloseAsync
          （Caliburn.Micro/src/Caliburn.Micro.Core/ConductorWithCollectionOneActive.cs:147-149，对全部文档执行 CloseStrategy）

三者共同下游：
DeactivateItemAsync / CanCloseAsync
→ DefaultCloseStrategy.ExecuteAsync（Caliburn.Micro/src/Caliburn.Micro.Core/DefaultCloseStrategy.cs:25-55，逐个询问，任一否决则整体不关闭）
→ 文档的 CanCloseAsync（脏确认在这里）
```

脏确认实现分两层：

- 框架默认 `PersistedDocument.CanCloseAsync`（`Gemini/Framework/PersistedDocument.cs:66-88`）：`IsDirty` 时弹同步 MessageBox（是/否/取消）；"是"返回 `await SaveInternal()` 的结果，"否"直接放行，"取消"否决关闭。
- 主编辑器 override `FumenVisualEditorViewModel.CanCloseAsync`（`OngekiFumenEditor/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.cs:321-345`）："是"分支 `IsNew` 时走 `DoSaveAs`，否则 `await Save(FilePath)` 后**无条件 return true**。

WPF 侧的已知缺陷（新项目已修复或需避免）：

1. 主编辑器 override 在保存失败时仍返回 true，文档照样关闭（`DoSave` 只弹框不抛异常，基类无法感知）。
2. 基类 `PersistedDocument.Save`（`:121-130`）在 `DoSave` 返回后**无条件** `IsDirty = false`，即使内部保存失败也清脏标记。
3. `IsDirty` 是纯 bool，无保存点；撤销/重做完全不更新 `IsDirty`，"撤销回保存点变干净"不存在。
4. 应用退出对每个脏文档逐个弹独立 MessageBox，无批量确认。

### 5.2 与 Avalonia 新项目的异同

相同点：

1. 都有"保存/放弃/取消"三选确认语义；"取消"否决关闭，"放弃"直接放行。
2. 都要求保存失败时文档保持打开（Avalonia 已在 `ShellDockFactory.cs:307-313` 正确实现；WPF 框架层正确、主编辑器 override 有缺陷）。
3. 应用退出都是逐文档确认、任一否决即中止整体退出（WPF：`DefaultCloseStrategy` 否决窗口关闭；Avalonia：`OnApplicationAskQuit` 返回 false）。

不同点：

| 维度 | WPF 原项目 | Avalonia 新项目（当前） |
| --- | --- | --- |
| 协议汇聚点 | 三入口全部汇入 `DeactivateItemAsync → CloseStrategy → IGuardClose.CanCloseAsync`，协议在文档与 Conductor 层 | 协议分裂：确认逻辑只在 Dock 工厂层，菜单关闭与 `ResetLayout` 完全绕过 |
| 裁决者 | 文档自身实现 `IGuardClose`（面向对象协议） | Dock 工厂检查 `document.Context is IPersistedDocumentViewModel`（视图层检查） |
| 菜单关闭 | 有脏确认（走同一链） | 无脏确认，直接移除（本方案 P0-1 要修的缺口） |
| 确认 UI | 同步 `MessageBox.Show`（阻塞） | 异步 `DialogManager` + `SaveDirtyDocumentDialogViewModel` |
| 关闭时保存 | `IsNew` 时转 SaveAs 对话框 | 无 SaveAs（项目文件夹作用域另存为未实现，`Save()` 直接写当前工程文件） |
| 保存失败语义 | override 有缺陷（仍关闭）；基类清脏标记 | 保存失败保持脏、保持打开（正确）；但保存成功后无条件清脏（竞态，P0-2 要修） |
| 退出确认范围 | 窗口级 `WindowConductor` 拦截 `Closing` 事件 | 消息级 `ApplicationAskQuitEvent` 处理器 |

结论：P0-1 的 `TryCloseDocumentAsync` 本质上是**恢复 WPF 原项目"单一汇聚点"的协议形状**，但用显式的 `RequestDocumentCloseResult`（`Closed / Cancelled / SaveFailed`）结果枚举替代 WPF 的 bool 语义（bool 无法区分"用户取消"与"保存失败"），并把裁决从文档的 `IGuardClose` 接口移到 Shell 协议层（避免每个文档自己弹框，也方便统一日志与测试）。WPF 的两个已知缺陷（保存失败仍关闭、保存失败仍清脏）是新协议的验收反例。

## 6. P1：恢复属性和元信息撤销

1. `UndoablePropertyInfoWrapper.ProxyValue` 先捕获旧值，再提交 `PropertySetAction`；当前 setter 直接改模型：`src/OngekiFumenEditor.Avalonia/Modules/FumenObjectPropertyBrowser/UIGenerator/UndoablePropertyInfoWrapper.cs:32`（对应 CA-014）。
2. 多选属性保存每个对象的旧值和新值，使用一个组合动作，保证一次编辑可以完整撤销。
3. `OngekiFumenModelProxy` 不再只持有 `OngekiFumen`，应持有所属编辑器或一个文档编辑服务，使元信息修改进入同一撤销栈：`src/OngekiFumenEditor.Avalonia/Modules/FumenMetaInfoBrowser/ViewModels/OngekiFumenModelProxy.cs:18`（对应 CA-017；与 CA-013 的跨文档上下文修复共同验证）。
4. 首拍号只保留一个权威来源。当前 `FumenMetaInfo` 和 `MeterChanges.FirstMeter` 都可被修改，`Setup` 又会相互复制：`src/OngekiFumenEditor.Avalonia/Base/OngekiFumen.cs:101`、`:415`（对应 CA-018）。

## 7. P1：修复模型动作的原子性

1. Drop 工厂必须无副作用，先校验，再由撤销动作统一建立模型关系。当前 `HoldEnd` 在校验前已经挂到 `Hold`：`src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.UserInteractionActions.cs:1024`、`src/OngekiFumenEditor.Avalonia/Base/OngekiObjects/Hold.cs:64`（对应 CA-061）。
2. 快速新增只收集实际成功的 Drop；全部失败时不创建组合动作、不清空选择：`UserInteractionActions.cs:1001`（对应 CA-062）。
3. HoldEnd 替换动作保存 `oldEnd/newEnd`，`Execute/Undo/Redo` 同时维护双向引用（对应 CA-064）。
4. Lane 合并动作保存完整的子节点数组、父节点和所有重挂载对象；当前撤销只遍历部分节点：`src/OngekiFumenEditor.Avalonia/Modules/FumenObjectPropertyBrowser/ViewModels/MultiLanesOperationViewModel.cs:61`（对应 CA-034）。
5. 渲染控制重排动作记录所属文档和撤销管理器，禁止切换文档后把旧动作发给当前文档（对应 CA-029）。

## 8. 测试与提交边界

1. `document-close-safety`：菜单/标签/退出三入口，保存、放弃、取消、保存失败。
2. `document-save-point`：保存期间修改，保存成功后仍保持脏；下一次保存包含新修改；修改后撤销回保存点恢复干净。
3. `property-and-meta-undo`：单选、多选、元信息、首拍号的执行/撤销/重做/保存重载。
4. `mutation-transaction`：CA-029、CA-034、CA-061、CA-062、CA-064 的拒绝、部分成功和完整回滚。
5. `recovery-revision`：自动恢复快照只删除不再更新的版本，启动时提供恢复/忽略选择。

## 9. 与 `SupportCommandDefinitionTypes` 的边界

`SupportCommandDefinitionTypes` 不应承担这些职责；它只决定命令是否暴露，不能替代保存点、关闭协议或撤销事务（参见 2026-08-21 对该属性的两层职责分析：框架层命令发现 vs 编辑器层临时屏蔽 SaveAs）。
