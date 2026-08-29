# Gekimini.Avalonia / Dock 审计（2026-08-26）

## 范围与基线

- 审计对象：`Dependencies/Gekimini.Avalonia/Dependencies/Dock`，以及 `Dependencies/Gekimini.Avalonia/src/Gekimini.Avalonia` 的 Shell、布局序列化和应用集成。
- 本地 Dock 工作树基线：`03a70d9`；Gekimini 集成基线：`03a70d9`（`Dependencies/Gekimini.Avalonia`）。
- 只读检查了源码、提交历史、现有测试，并对 `tests/OngekiFumenEditor.Avalonia.Tests/OngekiFumenEditor.Avalonia.Tests.csproj` 运行了筛选测试：`DocumentCloseSafetyTests` 与 `ToolLayoutRestorationTests`，共 15 项通过。现有测试没有覆盖 Dock 拖动/置顶/隐藏、同一 ViewModel 类型的多个文档、浮动窗口恢复或 `EditorLayoutManager` 的流式 API。
- Dock 自带 `Dock.Avalonia.HeadlessTests` 全量运行结果为 `340/340` 通过；针对 `FactoryDockable`、`FactoryWindowManagement` 和 `DockControl` 的筛选结果为 `99/99` 通过。绿灯只说明既有场景未回归，不覆盖下文列出的跨模块事件语义和持久化边界。

## 发现（按严重性）

### P0：Dock 的结构性移除被当成关闭，拖动文档会触发 Dispose

**证据**

- Dock 的 `MoveDockable` 在同一 Dock 内重排、跨 Dock 移动时，先调用 `OnDockableRemoved`，再插回并调用 `OnDockableAdded`：`Dependencies/Gekimini.Avalonia/Dependencies/Dock/src/Dock.Model/FactoryBase.Dockable.cs:300-344`。[上游源码](https://github.com/wieslawsoltes/Dock/blob/master/src/Dock.Model/FactoryBase.Dockable.cs)
- 置顶和隐藏同样是结构变化：置顶路径在 `:618-632` 调用 `OnDockableRemoved`，隐藏路径在 `:1140-1164` 调用它；这不是用户关闭。
- Shell 把所有 `DockableRemoved` 都转换成 `DockableClosed`：`Dependencies/Gekimini.Avalonia/src/Gekimini.Avalonia/Modules/Shell/ViewModels/ShellViewModel.cs:478-497`。
- 应用桥接收到 `DockableClosed` 后调用 `NotifyDestory`：`src/OngekiFumenEditor.Avalonia/OngekiFumenEditorApp.cs:131-139`；文档管理器最终执行 `editor.Dispose()`：`src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/Kernel/DefaultImpl/DefaultEditorDocumentManager.cs:92-105`。

**复现路径**

1. 打开一个 `FumenVisualEditorViewModel` 文档。
2. 在文档标签上拖动重排，或拖到另一个 Dock 进行拆分。
3. Dock 先发出结构性 `Removed`，Shell 误发 `DockableClosed`，应用立即销毁仍在移动中的编辑器；随后 Dock 又发 `Added`，同一个已 `Dispose` 的实例重新出现在布局中。
4. 对工具执行 Pin/Unpin 或 Hide 也会走相同的误报路径；置顶工具会暂时从 `Shell.Tools` 消失并触发“关闭”事件。

**影响**

文档拖动、拆分、置顶等纯布局操作会清空编辑器上下文、音频和渲染资源，并可能把已销毁实例重新挂回 UI。外部订阅者（Browser before-unload、自动保存、编辑器文档管理器）也会收到错误的关闭/创建事件。该问题比单纯的布局显示错误更严重，因为它会破坏正在编辑的运行时对象。

**建议**

将“结构变化”和“真正关闭”分成两条事件语义：`DockableAdded/Removed` 只维护 Shell 的索引，`DockableClosed` 只能由成功的关闭协议或 Dock 的 `OnDockableClosed` 触发。对 Move/Swap/Pin/Hide 增加回归测试，断言编辑器实例未被 Dispose、`DockableClosed` 不触发且 `Shell.Documents/Tools` 最终保持不变。Dock 官方契约明确把 `DockableRemoved` 与 `DockableClosed` 作为两个独立事件：[IFactory.Events.cs](https://github.com/wieslawsoltes/Dock/blob/master/src/Dock.Model/Core/IFactory.Events.cs)。

**补充验证**

当前应用桥接的 `DockableClosed` 不是仅 UI 计数：`OngekiFumenEditorApp` 会把它映射到 `DefaultEditorDocumentManager.NotifyDestory`，后者在 finally 中调用编辑器 `Dispose()`。因此该项影响是资源/对象生命周期破坏，而非单纯事件命名问题。

### P1：文档容器缓存只用 ViewModel 类型名，两个同类型文档会关闭错对象

**证据**

- `GetId` 返回 `dockableViewModel.GetType().FullName`：`Dependencies/Gekimini.Avalonia/src/Gekimini.Avalonia/Modules/Shell/ViewModels/ShellViewModel.cs:398-402`。
- 打开文档时允许不同实例共存（判断是 `addedDocuments.Contains(model)` 的引用比较），但把容器写入同一个类型名键：`:167-193`。
- 关闭时按同一键取容器并删除：`:248-260`。
- 编辑器 Provider 的 `Create()` 每次通过 `Resolve<FumenVisualEditorViewModel>()` 创建实例：`src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/FumenVisualEditorProvider.cs:48`；因此两个不同文件可以有两个同类型编辑器。

**复现路径**

打开文件 A，再打开文件 B（两个都是 `FumenVisualEditorViewModel`）。`addedDocuments` 含 A、B，但 `cachedIdToDocumentContainerMap[FullName]` 已被 B 覆盖。关闭 A 时 `RemoveDocumentCore(A)` 实际移除 B 的容器并触发 B 的关闭事件；A 仍留在 Dock 树中却失去缓存，之后关闭 B 又找不到容器。

**影响**

用户可能关闭错误文件；另一个编辑器被销毁，剩余标签成为 Shell 无法管理的孤儿。脏文档确认、活动文档切换和 Browser before-unload 保护都会因此失真。

**建议**

使用每个运行时文档实例的稳定唯一 ID（或直接以实例引用作为缓存键），把“运行时容器 ID”和“用于恢复类型的 ContextType”分开。至少增加两个同类型文档的打开、分别关闭、取消关闭、保存失败和重开测试。

**补充验证**

当前编辑器打开命令每次都调用 Provider `Create()`，且失败的临时编辑器由调用方显式 Dispose：`OpenFileCommandListHandler.cs:45-55`、`NewFileCommandHandler.cs:49-59`。这确认文档实例不是按类型复用的 Singleton；类型名键碰撞不是理论上的不可达分支。

### P1：布局恢复明确丢弃所有文档

**证据**

- 序列化时确实记录文档的 `ContextType`：`Dependencies/Gekimini.Avalonia/src/Gekimini.Avalonia/Modules/Shell/Serializations/LayoutJsonSerializer.cs:60-64,108-114`。
- 反序列化 `LayoutDocument` 却直接返回 `null`：`:174-184`（其中 `LayoutDocument` 分支为 `return null`）。父 Dock 只在子对象非空时加入可见集合：`:281-303`。
- `ShellViewModel.LoadLayout` 只能从反序列化后的可见树重建 `addedDocuments`：`Dependencies/Gekimini.Avalonia/src/Gekimini.Avalonia/Modules/Shell/ViewModels/ShellViewModel.cs:348-375`，没有文档 Provider、路径或最近记录的恢复步骤。
- 应用桥接代码还专门处理“布局恢复时已经打开的文档”：`src/OngekiFumenEditor.Avalonia/OngekiFumenEditorApp.cs:142-149`，说明调用方预期布局加载能够产生文档。

**复现路径**

打开一个或多个文档，执行 `Shell.SaveLayout()`，再调用 `Shell.LoadLayout()`（或重启应用）。生成的 JSON 有 `LayoutDocument` 节点，但加载后这些节点全部被跳过，`Shell.Documents` 为空，文档编辑器和相关文件上下文不会恢复。

**影响**

布局持久化对文档标签、活动文件和用户工作上下文失效。文件本身没有被删除，但应用重启后用户必须重新定位并打开每个工程；任何依赖“打开文档即恢复”的自动保存、活动编辑器和工具关联都会丢失。

**建议**

为文档保存可恢复的稳定数据（项目路径、最近记录快照或平台存储句柄），在反序列化阶段通过 `IEditorProvider` 异步重建并校验文件；未知/失效文档应给出可见警告并保留布局回退，而不是静默返回 `null`。如果产品不支持文档恢复，应删除 `LayoutDocument` 的持久化承诺并在启动时明确说明。

### P1：隐藏/置顶工具不在布局 JSON 中，重启后永久消失

**证据**

- `LayoutRootDock` 将 Dock 官方根状态全部注释掉：`Dependencies/Gekimini.Avalonia/src/Gekimini.Avalonia/Modules/Shell/Serializations/Layouts/LayoutRootDock.cs:10-18`（`HiddenDockables`、四个 `PinnedDockables`、`PinnedDock`）。
- `LayoutJsonSerializer.SerializeToLayoutObject(IRootDock)` 只复制 `Windows` 和 `Window`，可见子树由 `IDock.VisibleDockables` 递归处理：`Dependencies/Gekimini.Avalonia/src/Gekimini.Avalonia/Modules/Shell/Serializations/LayoutJsonSerializer.cs:71-90,93-105`；反序列化同样只填充 `VisibleDockables`：`:252-301`。
- Dock 官方 `IRootDock` 契约将隐藏列表、四侧置顶列表和 `PinnedDock` 定义为根布局状态：[IRootDock.cs](https://github.com/wieslawsoltes/Dock/blob/master/src/Dock.Model/Controls/IRootDock.cs)；官方 `DockState.Save` 也逐一保存这些集合：[DockState.cs](https://github.com/wieslawsoltes/Dock/blob/master/src/Dock.Model/DockState.cs)。

**复现路径**

在工具标签上执行 Pin，或关闭工具（Shell 设置 `HideToolsOnClose = true`），然后保存布局并重新加载。工具会从可见 Dock 移到根的置顶/隐藏集合，但这些集合没有对应 DTO，因此新布局树完全不包含该工具。

**影响**

用户的工具栏位置、置顶状态和关闭后可恢复状态在重启/布局导入后丢失；如果修复 P0 后仍使用当前 `FactoryOnDockableRemoved`，置顶期间还会错误触发工具关闭事件。

**建议**

为根布局增加隐藏/置顶/预览 Dock 的 DTO 和 ContextType/唯一 ID，按官方 `DockState` 的顺序保存并恢复；恢复后重新初始化 Owner、PinnedDock 和 Shell 索引。增加 Pin、Hide、Unpin、Restore、重启往返测试。

### P1：Browser 自动保存过滤掉文档事件，文档布局变更不会写回

**证据**

- Browser 只订阅 Shell 的 `DockableOpened/Closed`：`src/OngekiFumenEditor.Avalonia.Browser/OngekiFumenEditorBrowserApp.cs:61-66`。
- `AutoSaveLayout` 对非工具直接返回：`:111-116`，所以文档打开/关闭不会触发保存；Dock 移动、调整比例、置顶等结构事件也没有订阅。
- 这与 Shell 的布局加载/保存承诺冲突：`Dependencies/Gekimini.Avalonia/src/Gekimini.Avalonia/Modules/Shell/ViewModels/ShellViewModel.cs:294-314`。

**复现路径**

Browser 中打开一个文档或拖动标签后，不触发工具事件，刷新页面/重新进入应用。localStorage 中的 `ShellLayout` 仍是旧值，刚才的文档/布局变化不会被保存。

**影响**

Browser 没有可靠的进程退出时机时，用户的布局变化在页面刷新后消失；结合“文档恢复丢失”问题，文档工作上下文无法通过浏览器会话保留。

**建议**

让 Shell 在实际布局状态变化（打开/关闭、Move/Swap、Pin/Hide、比例变化）后统一节流保存，或在 Browser 端订阅 Dock 的完整结构事件并在保存前过滤纯生命周期噪声。保存应是可等待且失败可观测的任务。

**边界**

该项只针对 Browser 的会话持久化；Desktop 在退出时另有 `ApplicationQuitEvent` 路径。它不能修复 P0 的错误关闭事件，也不能替代文档恢复协议。

### P1：`EditorLayoutManager` 的流式导入/导出和“建议布局”菜单是静默无操作

**证据**

- `LoadLayout(Stream)` 明确记录“ignores stream payload”，随后丢弃输入流并调用 Shell 的设置加载：`src/OngekiFumenEditor.Avalonia/Kernel/EditorLayout/EditorLayoutManager.cs:10-17`。
- `SaveLayout(Stream)` 只调用 Shell 保存，再 `FlushAsync()`，没有向输出流写入任何字节：`:19-30`。
- `ApplyDefaultSuggestEditorLayout()` 也只是加载当前持久化 Shell 布局：`:32-35`；菜单/命令仍对外暴露该功能：`src/OngekiFumenEditor.Avalonia/Kernel/EditorLayout/MenuDefinitions.cs:7-15`、`.../ApplySuggestEditorLayoutCommandHandler.cs:7-12`。
- Shell 视图保留的同名流式方法仍直接抛 `NotImplementedException`：`Dependencies/Gekimini.Avalonia/src/Gekimini.Avalonia/Modules/Shell/Views/ShellView.axaml.cs:22-33`。

**复现路径**

向 `IEditorLayoutManager.SaveLayout(new MemoryStream())` 传入流，流长度保持为 0；向 `LoadLayout` 传入另一份布局 JSON，实际仍读取 `GekiminiSetting.ShellLayout`；执行“应用建议布局”不会读取嵌入的建议布局资源。

**影响**

迁移自 WPF 的布局导入、导出和建议布局菜单给用户成功返回/无错误提示，但实际不改变目标布局，属于静默失败。

**建议**

使用同一个版本化布局 JSON 和 `IDockSerializer` 真正实现 Stream Load/Save，补充未知版本、损坏 JSON、缺失工具和回退默认布局测试；或者删除/隐藏这些尚未实现的 API 和菜单，避免继续对外宣称支持。现有迁移审计也记录了该缺口：[wpf-avalonia-full-migration-audit-2026-08-07.html](wpf-avalonia-full-migration-audit-2026-08-07.html#b-008)。

### P2：保存布局会无条件反序列化并构造一份丢弃的工具树

**证据**

- `LayoutJsonSerializer.Save` 序列化后又执行两次无用解析/反序列化，结果从未使用：`Dependencies/Gekimini.Avalonia/src/Gekimini.Avalonia/Modules/Shell/Serializations/LayoutJsonSerializer.cs:320-328`。
- `Deserialize<T>` 会实际调用 `DeserializeLayout`：`:143-149`；工具分支进入 `TryOpenTool`：`:182-211`，最终通过 `TryResolveToolViewModel` / `TypeCollectedActivatorHelper` 创建 ViewModel：`:214-249`。
- 生成的 TypeCollectedActivator 工厂使用 `ActivatorUtilities.CreateInstance`：`Dependencies/Gekimini.Avalonia/src/Gekimini.Avalonia.Generator/TypeCollectedActivatorGenerator.cs:163-183`。

**复现路径**

布局中存在未注册为 Singleton 的工具时，任何 `SaveLayout()`（退出、Browser 自动保存、手动保存）都会额外构造一份工具实例；该实例没有被加入布局，也没有释放。

**影响**

工具构造函数中的事件订阅、计时器、文件/音频资源和异步任务可能泄漏或重复运行；频繁 Browser 自动保存还会把这一开销放大。即使当前工具大多是 Singleton，这也是 serializer 的确定性副作用。

**建议**

删除 `Save` 中两次无用的 `JsonSerializer.Deserialize`/`Deserialize<IDockable>`，只写出 `Serialize(value)` 的 JSON。用带计数构造函数的测试工具验证保存不会创建第二个实例。

### P2：活动/焦点 Dock 状态未持久化，加载时强制选择最后一个子项

**证据**

- `LayoutDock` 明确没有 `ActiveDockable`、`FocusedDockable`、`DefaultDockable` 字段：`Dependencies/Gekimini.Avalonia/src/Gekimini.Avalonia/Modules/Shell/Serializations/Layouts/LayoutDock.cs:9-18`。
- 反序列化每加入一个工具/文档就覆盖一次 Active/Focused，循环结束后最后一个子项必然成为活动项：`Dependencies/Gekimini.Avalonia/src/Gekimini.Avalonia/Modules/Shell/Serializations/LayoutJsonSerializer.cs:287-300`。
- Dock 官方 `IDock` 将 Active、Default、Focused 定义为独立状态：[IDock.cs](https://github.com/wieslawsoltes/Dock/blob/master/src/Dock.Model/Controls/IDock.cs)。

**复现路径**

在一个工具 Dock 中打开多个工具，选择第一个后保存并加载。加载后的 `ActiveDockable/FocusedDockable` 是列表最后一个工具，而非保存时选中的工具。文档场景目前被“文档完全丢失”问题遮蔽。

**影响**

活动编辑器/工具相关命令、键盘路由和上下文绑定指向错误对象；若外部修复文档恢复而不同时修复此处，活动文档仍会错位。

**建议**

给每个 Dock 保存活动/焦点/默认子项的稳定 ID，先完整创建子树，再按 ID 恢复状态；不要在遍历中无条件把最后一个子项设为 Active/Focused。

### P2：`async void` 关闭和未等待的初始化使关闭确认存在竞态

**证据**

- Shell Dock 工厂覆盖 `CloseDockable` 为 `async void`：`Dependencies/Gekimini.Avalonia/src/Gekimini.Avalonia/Modules/Shell/ShellDockFactory.cs:152-183`。
- 现有测试只能通过固定延迟等待该流程：`tests/OngekiFumenEditor.Avalonia.Tests/Modules/Shell/DocumentCloseSafetyTests.cs:181-187,197-204,213-218`。
- Shell 加载布局和模块初始化也被丢弃：`Dependencies/Gekimini.Avalonia/src/Gekimini.Avalonia/Modules/Shell/ViewModels/ShellViewModel.cs:530-537`；构建输出已经报告 `CS4014`（`:537`）。

**复现路径**

连续点击同一标签的关闭按钮、在脏文档确认框打开时触发第二次关闭，或在 Shell 初次加载期间退出/打开工具。调用方无法等待第一个关闭任务，也无法观察其异常；布局初始化可能尚未完成。

**影响**

可能出现重复确认、重复移除、未观察异常或工具加入空布局。固定 `Task.Delay(50)` 只能降低测试抖动，不能建立生命周期保证。

**建议**

在 UI 事件边界使用可跟踪的 `Task`（必要时由命令层协调 `async void` 事件处理器），按文档实例串行化关闭请求；让 `OnViewAfterLoaded` 通过可等待的初始化任务完成布局和模块 PostInitialize，并在失败时显式回退。

## 未列为缺陷的观察

- `ShellDockFactory` 将文档和工具的 `CanFloat` 固定为 `false`（`ShellDockFactory.cs:136-144, AddDocument/AddTool`），因此浮动窗口恢复问题目前不是默认用户路径；但布局 DTO 仍不完整，未来打开浮动能力时会暴露。
- 现有 15 项关闭/工具恢复测试通过，说明统一脏文档确认和 Singleton 工具恢复本身已有覆盖；它们不证明上述结构性 Dock 事件和多实例布局场景安全。
