# MCP object mutation tools design discussion

## Scope

讨论 MCP 新增编辑器物件变更 tool 的设计。目标能力包括：

- 添加物件
- 删除物件
- 修改物件属性
- 单个物件操作
- 批量物件操作

本文档会随着讨论实时追加决策、理由和未解决问题。

## Codebase Facts

- 当前 MCP tool 位于 `OngekiFumenEditor/Kernel/Mcp/`。
- `McpServerHost` 显式注册 tool：`.WithTools<EditorTools>(editorTools)`、`.WithTools<ScriptTools>(scriptTools)`。新增 tool 类需要注入并注册到 host。
- 当前 `EditorTools` 只提供只读编辑器发现和摘要：
  - `editor.get_current`
  - `editor.list_opened`
  - `editor.get_current_summary`
- 当前可变更编辑器状态的 MCP 入口主要是 `ScriptTools`，通过 runtime script 间接执行。
- runtime script host 已处理：
  - `expectedEditorId` 防止目标编辑器切换
  - 用户授权和确认
  - 可选执行前 fumen 文件备份
  - WPF Dispatcher 切回 UI 线程
  - `UndoRedoManager.BeginCombineAction()` / `EndCombineAction()` 包装事务
- 谱面对象集合核心是 `OngekiFumen.AddObject(...)`、`RemoveObject(...)`、`AddObjects(...)`、`RemoveObjects(...)`。
- 编辑器现有交互不直接裸改集合，而是通过 `FumenVisualEditorViewModel.UndoRedoManager.ExecuteAction(...)` 和 `LambdaUndoAction` / `PropertySetAction` 进入撤销栈。
- `OngekiObjectBase.Id` 是运行时递增 ID，来自静态 `ID_GEN`，不是持久化到谱面文件的稳定 ID。
- 连线类对象还有 `RecordId`，但它只适用于 lane/beam 等 connectable 对象，不是所有对象的统一标识。
- 对象属性浏览器通过反射和 `IObjectPropertyAccessProxy` 读写属性，并已有单对象/多对象属性变更撤销包装。

## Recommended Baseline

新增 MCP 物件变更 tool 不应直接绕过编辑器事务系统修改集合。推荐基线：

- tool 层保持薄：参数、授权预览、目标编辑器校验、结果模型。
- 实际变更委托给 runtime automation/editor mutation service。
- 所有写操作切到 UI Dispatcher。
- 所有添加、删除、属性修改默认进入 `UndoRedoManager`。
- 批量操作默认作为单个 undo transaction。
- 每个写请求都支持 `expectedEditorId`，避免用户切换活动编辑器后误操作。

## Decision Log

### 1. Object identity for MCP mutations

Status: decided

Problem:

- 删除和修改属性都需要定位已有物件。
- 代码中没有持久化、跨会话稳定的统一对象 ID。
- `OngekiObjectBase.Id` 能在当前运行进程和当前对象实例内定位物件，但保存/关闭/重新打开后不应被当作稳定 ID。

Recommended answer:

- MVP 使用 `editorId + objectId` 作为会话内对象句柄，其中 `objectId` 对应 `OngekiObjectBase.Id`。
- 明确文档化这个句柄只在当前运行实例、当前打开文档生命周期内有效。
- 添加物件的响应返回创建出的对象句柄。
- 删除/修改请求必须带 `expectedEditorId`，并建议同时带 `expectedObjectType` 防止错改同 ID 之外的异常情况。
- 跨会话或根据位置/类型查找对象作为后续能力，通过单独 query/list tool 解决，而不是在第一版 mutation tool 里混入复杂选择器。

Question:

MCP 删除/修改已有物件时，是否接受这个 MVP 约束：只支持当前会话内的 `editorId + OngekiObjectBase.Id` 句柄，不承诺跨保存、关闭、重新打开后仍可用？

Answer:

同意。第一版使用当前会话内的 `editorId + OngekiObjectBase.Id` 作为对象句柄，不承诺跨保存、关闭、重新打开后仍可用。

### 2. MCP tool granularity

Status: decided

Problem:

- 用户目标包含添加、删除、修改属性，并且每类操作都支持单个和批量。
- 如果设计成一个万能 `objects.mutate` tool，参数会很灵活，但授权预览、schema 校验、错误定位和 MCP tool 描述都会变复杂。
- 如果设计成多个专用 tool，调用路径更清晰，但需要明确批量事务如何表达。

Recommended answer:

- 第一版使用 3 个专用写 tool：
  - `objects.add`
  - `objects.delete`
  - `objects.set_properties`
- 每个 tool 都直接支持数组输入；单对象只是数组长度为 1 的特例。
- 不额外提供 `objects.add_one` / `objects.delete_one` 这类单对象 tool，避免 API 膨胀。
- 暂不做万能 `objects.mutate`。跨类型、跨操作的事务后续可以单独设计 `objects.batch`，但不作为第一版入口。
- 每次 tool 调用默认生成一个 undo transaction；数组内多个对象要么整体作为一个 undo 步骤，要么失败时不写入，具体原子性另行决策。

Question:

第一版是否采用这 3 个专用 tool，并让每个 tool 原生接收数组，从而覆盖单个和批量操作：`objects.add`、`objects.delete`、`objects.set_properties`？

Answer:

采用。第一版提供 `objects.add`、`objects.delete`、`objects.set_properties` 三个专用 tool；每个 tool 原生接收数组，单个操作就是数组长度为 1。

### 3. Batch atomicity

Status: decided

Problem:

- 三个写 tool 都支持数组输入，因此必须定义批量操作中某一项失败时的行为。
- MCP 客户端通常更容易处理“请求整体成功/失败”的结果；部分成功会让调用方必须额外恢复状态或重新查询。
- 当前 `UndoRedoManager.ExecuteAction(...)` 以一个 `IUndoableAction` 为单位进入撤销栈。批量操作可以包装为一个 `LambdaUndoAction` 或 composite action，从而成为一个 undo 步骤。
- `ExecuteAction(...)` 在 action 执行成功后才加入撤销栈；因此新增 mutation service 应先完成参数解析和目标验证，再执行实际写入。

Recommended answer:

- 第一版批量操作采用 all-or-nothing。
- 执行前完成所有可验证项：
  - editor 是否存在且匹配 `expectedEditorId`
  - object id 是否存在
  - object type 是否匹配
  - property 是否存在、可写、未被对象属性浏览器标记为只读
  - value 是否能转换到目标属性类型
  - add 请求的 object type 是否在允许列表内
- 任一项验证失败时，不修改谱面，不产生 undo action。
- 如果执行阶段出现异常，service 尝试按已记录旧值/反向操作回滚，并返回失败；这属于防御路径，不作为正常控制流。
- 暂不提供 `allowPartial`。部分成功语义可以后续另开 `objects.batch` 或增加显式选项。

Question:

批量操作第一版是否采用 all-or-nothing：数组里任一对象/属性校验失败，则整个 tool 调用不修改谱面、不产生 undo 记录？

Answer:

同意。批量操作第一版采用 all-or-nothing：数组里任一对象/属性校验失败，则整个 tool 调用不修改谱面、不产生 undo 记录。

### 4. Object type names accepted by `objects.add`

Status: decided

Problem:

- `objects.add` 需要根据请求创建 `OngekiObjectBase` 派生类型。
- 直接接受任意 CLR type name 风险较高：可能误创建非谱面对象、内部辅助对象，或者暴露实现细节。
- 工具箱已有“用户可放置对象”的概念，但 MCP 第一版还需要覆盖脚本常见基础对象。
- 对象类型命名会成为外部 API，后续应稳定，不能轻易跟着类名重构变化。

Recommended answer:

- 第一版使用显式白名单的 `objectType` 字符串，不接受任意 CLR 类型名。
- `objectType` 使用稳定 API 名称，建议 lower snake/camel 风格，例如：
  - `tap`
  - `hold`
  - `hold_end`
  - `bell`
  - `flick`
  - `bullet`
  - `bpm_change`
  - `meter_change`
  - `soflan`
  - `lane_block_area`
  - `comment`
  - lane/beam 类对象再按实际支持情况单列
- 内部通过 registry 映射 `objectType` 到 CLR 类型、构造函数、默认值补齐和添加规则。
- 响应中返回 canonical `objectType`、CLR type name、object id，方便调试但不要求客户端依赖 CLR type name。
- 后续可以提供 `objects.list_supported_types` 只读 tool 或资源列出白名单、属性 schema 和示例。

Question:

`objects.add` 第一版是否只接受显式白名单里的稳定 `objectType` 名称，而不是允许客户端传任意 CLR 类型名？

Answer:

同意。`objects.add` 第一版只接受显式白名单里的稳定 `objectType` 名称，不允许客户端传任意 CLR 类型名。

### 5. Property value wire format

Status: decided

Problem:

- `objects.add` 和 `objects.set_properties` 都需要从 MCP JSON 参数转换成 CLR 属性值。
- 谱面对象常见属性既有基础类型，也有领域类型，例如 `TGrid`、`XGrid`、`RangeValue`、枚举等。
- 仓库当前只有 `ColorJsonConverter`，没有通用 `TGrid` / `XGrid` JSON converter。
- 直接接受任意反射反序列化会让错误信息、兼容性和安全边界都变差。

Recommended answer:

- MCP mutation 层定义自己的显式 value codec。
- 基础类型直接用 JSON 原生值：
  - `string`
  - `number`
  - `boolean`
  - `null`，仅当属性允许置空
- enum 接受字符串枚举名，第一版不接受数字枚举值。
- `TGrid` / `XGrid` 接受对象格式：
  - `{"unit": 12, "grid": 0}`
  - 可选支持 `{"totalGrid": 23040}` 作为便捷格式
- 第一版不接受 `T[12,0]` / `X[0,0]` 这类展示字符串作为输入，避免解析歧义。
- `RangeValue` 等复合对象优先通过子属性路径修改，例如 `PlaceOffset.currentValue`；是否第一版支持整对象赋值另行决策。
- 返回结果中统一输出 canonical JSON，例如 `TGrid` 输出 `{ "unit": ..., "grid": ..., "totalGrid": ... }`。

Question:

属性值输入第一版是否采用显式 JSON value codec：基础类型用 JSON 原生值，enum 用字符串名，`TGrid/XGrid` 用 `{ unit, grid }` 或 `{ totalGrid }`，不接受展示字符串？

Answer:

同意。属性值输入第一版采用显式 JSON value codec：基础类型用 JSON 原生值，enum 用字符串名，`TGrid/XGrid` 用 `{ unit, grid }` 或 `{ totalGrid }`，不接受展示字符串。

### 6. Property write eligibility

Status: decided

Problem:

- MCP 属性修改如果直接反射写任意 public setter，会绕过对象属性浏览器已有的隐藏、只读和条件只读规则。
- 现有属性浏览器规则集中在 attributes 和 `PropertyInfoWrapper.IsReadOnly`：
  - `ObjectPropertyBrowserHide`
  - `ObjectPropertyBrowserShow`
  - `ObjectPropertyBrowserReadOnly`
  - `ObjectPropertyBrowserReadOnlyForCondition`
  - `ObjectPropertyBrowserAllowSetNull`
  - `ObjectPropertyBrowserSingleSelectedOnly`
- 这些规则已经代表“编辑器 UI 允许用户改什么属性”。

Recommended answer:

- 第一版 `objects.set_properties` 复用对象属性浏览器的可写性规则。
- 默认只允许修改 UI 属性浏览器可见且非只读的 public instance property。
- 带 `ObjectPropertyBrowserHide` 的属性不允许通过 MCP 修改。
- 带 `ObjectPropertyBrowserReadOnly` 或条件只读当前为只读的属性不允许修改。
- 只有带 `ObjectPropertyBrowserAllowSetNull` 的引用类型属性允许传 `null`。
- `ObjectPropertyBrowserSingleSelectedOnly` 对单对象修改允许；对批量修改不允许，除非后续明确例外。
- 第一版不提供 `force` 绕过这些规则；如果确实需要内部属性，应新增专门 tool 或白名单例外。

Question:

`objects.set_properties` 第一版是否严格复用对象属性浏览器的可见/只读/允许 null 规则，并且不提供 `force` 绕过？

Answer:

同意。`objects.set_properties` 第一版严格复用对象属性浏览器的可见/只读/允许 null 规则，并且不提供 `force` 绕过。

### 7. Property paths and nested object mutation

Status: decided

Problem:

- 某些属性是复合对象：
  - `TGrid` / `XGrid`：UI 通过 `{Unit, Grid}` 创建新实例并替换整个属性值。
  - `RangeValue`：UI 直接修改 `CurrentValue`、`MinValue`、`MaxValue` 子属性，并通过 `ExecuteSubPropertySetAction` 生成撤销动作。
- 如果 MCP 完全不支持子属性路径，`RangeValue` 等属性修改会不方便。
- 如果 MCP 支持任意深度对象图路径，会扩大安全边界，也难以保证撤销和校验一致。

Recommended answer:

- 第一版支持有限属性路径：
  - 顶层属性：直接定义在谱面对象上的 public instance property，例如 `TGrid`、`XGrid`、`Tag`、`Speed` 等，不带点路径。
  - 一层子属性：例如 `RangeValue.CurrentValue`。
- 不支持任意深度路径、索引器、集合元素路径、方法调用。
- 顶层属性仍必须通过第 6 条可写性校验。
- 子属性只有在父属性本身可见且非只读，并且子属性在 explicit nested whitelist 中时才允许。
- `TGrid` / `XGrid` 不使用 `TGrid.Unit` / `TGrid.Grid` 路径修改，而是通过顶层属性整值替换，避免原地修改绕过父属性 setter 逻辑。
- `RangeValue` 第一版只允许改 `CurrentValue`；不允许改 `MinValue`、`MaxValue`、`IsLimitInt`。

Question:

`objects.set_properties` 第一版是否支持“顶层属性 + 一层白名单子属性路径”，例如允许 `RangeValue.CurrentValue`，但不支持任意深度路径/集合索引/方法调用？

Answer:

同意，但收窄 `RangeValue` 支持范围。顶层属性指直接定义在谱面对象上的 public instance property，例如 `TGrid`、`XGrid`、`Tag`、`Speed` 等；`RangeValue` 第一版只允许子属性 `CurrentValue`。

### 8. Add-object required fields and defaults

Status: decided

Problem:

- `objects.add` 需要创建不同类型对象。不同对象有不同的最小有效字段。
- 如果所有属性都可选并依赖构造默认值，MCP 客户端容易创建出不可见、位置错误或语义不完整的对象。
- 如果所有属性都强制填写，请求会很重，也会把很多对象内部默认值暴露成外部 API。

Recommended answer:

- 每个 `objectType` 在 registry 中定义 required fields、optional fields 和 default initialization。
- 通用规则：
  - 时间线对象必须提供 `TGrid`，除非该类型明确没有时间位置。
  - 可移动对象必须提供 `XGrid`，除非该类型有明确默认位置。
  - 其他属性尽量使用对象构造默认值或 registry 默认值。
- 第一版不要允许 `objects.add` 任意设置所有可写属性；只允许 registry 标记为 add-time 可设置的属性。
- 需要添加后再改的属性，调用方可以在同一个后续 `objects.set_properties` 调用中修改；未来如果需要跨 add+set 的单事务，再设计 `objects.batch`。
- add 响应返回每个创建对象的 `objectId`、`objectType`、实际 `TGrid/XGrid` 等核心字段。

Question:

`objects.add` 第一版是否按 `objectType` registry 定义“必填字段 + 可选字段 + 默认值”，而不是允许创建时设置任意可写属性？

Answer:

同意。`objects.add` 第一版按 `objectType` registry 定义“必填字段 + 可选字段 + 默认值”，不允许创建时设置任意可写属性。

### 9. Delete semantics for related objects

Status: decided

Problem:

- 当前 UI 删除入口 `FumenVisualEditorViewModel.DeleteSelection(...)` 会：
  - 通过 `UndoRedoManager.ExecuteAction(...)` 进入撤销栈。
  - 对 `LaneCurvePathControlObject`、`ConnectableChildObjectBase` 记录原索引，撤销时插回原位置。
  - 其他对象撤销时通过 `Fumen.AddObject(...)` 恢复。
- `Fumen.RemoveObject(...)` 对特殊对象已有内建行为：
  - `HoldEnd` 从 `RefHold` 脱离。
  - `ConnectableChildObjectBase` 从对应 start 的 children 中移除。
  - `ConnectableStartObject` 从 lane/beam 集合移除。
- 删除 start 类对象可能影响其 children 是否仍可从 fumen 枚举访问；这是高风险操作。

Recommended answer:

- 第一版 `objects.delete` 复用编辑器现有删除语义和撤销恢复逻辑，而不是直接裸调用 `Fumen.RemoveObject(...)`。
- 删除请求默认只删除明确传入的 object id。
- 对有子对象/关联对象的对象增加 `cascade` 参数：
  - 默认 `cascade = false`。
  - 当目标是 `ConnectableStartObject` 且存在 children 时，`cascade = false` 直接拒绝，提示需要显式 cascade。
  - `cascade = true` 时，删除 start 及其 children，以一个 undo transaction 恢复。
- 删除 `ConnectableChildObjectBase`、`LaneCurvePathControlObject`、`HoldEnd` 允许作为单独删除，沿用 UI 撤销恢复索引/关系的逻辑。
- 删除前可选校验 `expectedObjectType`，推荐客户端总是传。

Question:

`objects.delete` 第一版是否复用 UI 删除语义，并对带 children 的 `ConnectableStartObject` 要求显式 `cascade = true` 才允许删除整条 lane/beam？

Answer:

同意。`objects.delete` 第一版复用 UI 删除语义，并对带 children 的 `ConnectableStartObject` 要求显式 `cascade = true` 才允许删除整条 lane/beam。

### 10. Authorization, confirmation, and backup

Status: decided

Problem:

- 新增 `objects.add`、`objects.delete`、`objects.set_properties` 都是写操作。
- 现有 runtime script 入口已接入 `IMcpToolAuthorizationService`，支持：
  - 匿名客户端策略
  - 交互确认
  - 记住客户端授权
  - 执行前备份 fumen 文件
- `McpToolAuthorizationService` 的备份字段命名偏 script，但资源已有 `McpBackupFumenHintForNonScriptTool`，说明 UI 已考虑非脚本 tool。

Recommended answer:

- 三个 object mutation tool 全部接入 `IMcpToolAuthorizationService`。
- 默认 `requireConfirmation = true`。
- 请求参数允许传 `requireConfirmation = false`，但如果该客户端没有 remembered approval，则返回 `USER_CONFIRMATION_REQUIRED`，不执行。
- 复用现有“记住授权”和匿名客户端策略。
- 复用执行前 fumen 备份能力；如果用户在确认对话框勾选备份，则 mutation service 在写入前备份当前 fumen 文件。
- 授权预览要用结构化摘要而不是完整 JSON dump：
  - editor display name / editor id
  - operation
  - object count / property count
  - object type 分布
  - 是否 cascade
  - transaction name
- 后续可把 `backupFumenBeforeScriptExecutionEnabled` 内部字段重命名为更通用的 `backupFumenBeforeMutationEnabled`，但第一版功能上可先复用。

Question:

三个 object mutation tool 是否全部默认要求用户确认，并复用现有 MCP 客户端授权与执行前 fumen 备份机制？

Answer:

同意。三个 object mutation tool 全部默认要求用户确认，并复用现有 MCP 客户端授权与执行前 fumen 备份机制。

### 11. Target editor selection

Status: decided

Problem:

- 现有 script tool 同时提供：
  - `script.run_current_editor`
  - `script.run_editor`
- 第 2 条已决定 object mutation tool 不再拆成单对象/批量多个入口。
- 如果再按 current/specified editor 拆分，会从 3 个 tool 变成 6 个 tool。
- 如果只操作当前激活编辑器，自动化调用方需要担心用户切换窗口导致误写。
- 第 1 条已决定使用 `editorId + objectId` 作为会话内对象句柄，因此写 tool 天然需要理解 `editorId`。

Recommended answer:

- 三个 object mutation tool 都使用统一目标参数：
  - `editorId` 可选。
  - 未传 `editorId` 时操作当前激活编辑器。
  - 传 `editorId` 时操作指定已打开编辑器。
  - `expectedEditorId` 可选但推荐；如果传入则必须和实际目标 editor id 一致，否则返回 `EDITOR_CHANGED`。
- 对删除和修改已有对象，推荐客户端总是传 `editorId`，因为对象句柄本身是 `editorId + objectId`。
- 不新增 `objects.add_current` / `objects.add_editor` 这类重复 tool。

Question:

三个 object mutation tool 是否使用统一的可选 `editorId` 参数：不传则当前编辑器，传则指定已打开编辑器，而不是拆成 current/editor 两套 tool？

Answer:

同意。三个 object mutation tool 使用统一的可选 `editorId` 参数：不传则当前编辑器，传则指定已打开编辑器，不拆成 current/editor 两套 tool。

### 12. Result and error model

Status: decided

Problem:

- 写 tool 需要让 MCP 客户端明确知道是否成功、改了哪个 editor、创建/删除/修改了哪些对象。
- 批量 all-or-nothing 下，失败时需要指出失败项位置和原因，但不应该返回“部分成功”。
- 现有 editor read tool 使用匿名对象返回 `success/errorCode/errorMessage`；script tool 使用 `ScriptRunResult`。

Recommended answer:

- 为 object mutation 定义统一结果模型，例如 `ObjectMutationResult`。
- 顶层字段：
  - `success`
  - `errorCode`
  - `errorMessage`
  - `editorId`
  - `displayName`
  - `transactionName`
  - `operation`
  - `affectedCount`
  - `items`
  - `validationErrors`
  - `logs`
- 成功时：
  - `items` 返回每个对象的 canonical handle：
    - request index
    - `objectId`
    - `objectType`
    - CLR type name
    - 核心位置字段，如 `tGrid` / `xGrid` 存在时输出
  - `validationErrors` 为空。
- 失败时：
  - `success = false`
  - `affectedCount = 0`
  - `items` 为空或只返回可安全描述的目标摘要。
  - `validationErrors` 包含每个失败项：
    - request index
    - optional object id
    - field/property path
    - error code
    - message
- 推荐错误码：
  - `NO_ACTIVE_EDITOR`
  - `EDITOR_NOT_FOUND`
  - `EDITOR_CHANGED`
  - `AUTHORIZATION_DENIED`
  - `VALIDATION_FAILED`
  - `OBJECT_NOT_FOUND`
  - `OBJECT_TYPE_MISMATCH`
  - `UNSUPPORTED_OBJECT_TYPE`
  - `UNSUPPORTED_PROPERTY`
  - `READONLY_PROPERTY`
  - `VALUE_CONVERSION_FAILED`
  - `CASCADE_REQUIRED`
  - `FUMEN_BACKUP_FAILED`
  - `MUTATION_FAILED`

Question:

是否为三个 object mutation tool 定义统一 `ObjectMutationResult`，成功返回 affected item handles，失败返回 `validationErrors` 并保证 `affectedCount = 0`？

Answer:

同意。三个 object mutation tool 定义统一 `ObjectMutationResult`；成功返回 affected item handles，失败返回 `validationErrors` 并保证 `affectedCount = 0`。

### 13. Read/query support needed by mutation clients

Status: decided

Problem:

- 第 1 条决定删除/修改使用会话内 `editorId + objectId`。
- 当前 `editor.get_current_summary` 只返回计数，不返回对象列表。
- 如果没有只读查询能力，MCP 客户端很难：
  - 找到要删除/修改的 object id。
  - 知道当前支持哪些 `objectType`。
  - 知道每种类型 add-time 支持哪些字段。
  - 知道哪些属性可写。
- 用户原始目标只明确“添加新的 tool 可以添加删除物件，以及修改物件属性”，但实际可用性依赖查询和 schema。

Recommended answer:

- 第一版 mutation 同期至少补 2 个只读 tool：
  - `objects.list_supported_types`
    - 返回 object type registry、add required/optional fields、支持的 value format、可写属性 schema。
  - `objects.list`
    - 按 editor、object type、TGrid range、selection-only 等条件返回 object handles 和核心字段。
- 这两个只读 tool 不改变第 2 条的写 tool 颗粒度；它们是支撑 mutation 的 discoverability。
- `objects.list` 首版可以限制返回字段，避免一次返回完整对象图：
  - object id
  - object type
  - CLR type name
  - `TGrid` / `XGrid`
  - `RecordId`
  - `Tag`
  - 常见摘要字段
- 属性完整读取可以后续增加 `objects.get` 或 `objects.inspect`。

Question:

是否在第一版 mutation 设计中同步加入两个只读辅助 tool：`objects.list_supported_types` 和 `objects.list`？

Answer:

同意。第一版 mutation 设计同步加入两个只读辅助 tool：`objects.list_supported_types` 和 `objects.list`。

### 14. Editor state constraints

Status: decided

Problem:

- UI 入口普遍尊重编辑器状态：
  - `DeleteSelection(...)` 在 `IsLocked` 时直接返回。
  - 拖放添加对象时检查 `IsLocked` 和 `IsDesignMode`。
  - 批量刷入依赖当前编辑器交互状态。
- runtime script 作为更底层的自动化入口，可以被脚本作者主动绕过这些 UI 约束。
- object mutation tool 是结构化、安全边界更明确的写入口，如果绕过锁定/预览模式，用户可能在以为编辑器不可编辑时被 MCP 改谱面。

Recommended answer:

- 第一版 `objects.add`、`objects.delete`、`objects.set_properties` 默认尊重编辑器状态。
- 当目标 editor `IsLocked == true` 时拒绝写入，返回 `EDITOR_LOCKED`。
- 当目标 editor 不是 design mode 时拒绝写入，返回 `EDITOR_NOT_IN_DESIGN_MODE`。
- 只读 tool 不受这些限制。
- 第一版不提供 bypass 参数。需要绕过时仍使用更显式、更高风险的 `script.run_*`。

Question:

三个 object mutation 写 tool 是否应像 UI 操作一样尊重编辑器状态：锁定或非 design mode 时拒绝写入，不提供 bypass？

Answer:

同意。三个 object mutation 写 tool 像 UI 操作一样尊重编辑器状态：锁定或非 design mode 时拒绝写入，不提供 bypass。

### 15. Undo transaction naming and grouping

Status: decided

Problem:

- 第 3 条已决定批量操作 all-or-nothing。
- 写操作需要进入 `UndoRedoManager`，否则会绕过编辑器已有撤销模型。
- MCP 客户端通常会发起批量 add/delete/set，一次 tool 调用应当对应用户心智上的一次操作。
- 现有 script tool 已有 `transactionName` 参数。

Recommended answer:

- 每次成功的写 tool 调用生成一个 undo 步骤。
- 数组里有多个对象/属性时，也合并为同一个 undo 步骤。
- 请求参数支持可选 `transactionName`。
- 未提供时使用默认名称：
  - `MCP Add Objects`
  - `MCP Delete Objects`
  - `MCP Set Object Properties`
- `ObjectMutationResult.transactionName` 返回实际使用的名称。
- 失败时不写入撤销栈。
- 第一版不支持一个 tool 调用生成多个 undo 步骤；如需分步撤销，客户端应拆成多次调用。

Question:

每次成功的 object mutation 写 tool 调用是否固定生成一个 undo 步骤，并支持可选 `transactionName` 覆盖默认名称？

Answer:

是。每次成功的 object mutation 写 tool 调用固定生成一个 undo 步骤，并支持可选 `transactionName` 覆盖默认名称。

### 16. First-version `objectType` support set

Status: decided

Problem:

- Toolbox 暴露了较多对象，包括常规音符、lane、beam、soflan、comment、enemy、click se、SVG/editor objects 等。
- Batch mode 的输入对象是更小的常用集合：
  - lane starts: left/center/right/colorful/wall left/wall right
  - tap/hold/flick/lane block/bell
- 某些对象有复杂关联或特殊语义：
  - connectable child，如 lane next/end、beam next/end，需要绑定 start/RecordId/插入位置。
  - `HoldEnd` 需要绑定 `Hold`。
  - bullet palette / bullet 可能涉及 palette 引用。
  - SVG/editor object 和 soflan group 相关对象需要更多 schema。

Recommended answer:

- 第一版 `objects.add` 白名单分两层：
  - Core supported: 默认实现和测试覆盖。
  - Deferred: schema 暂列出但 `add` 返回 `UNSUPPORTED_OBJECT_TYPE`，后续补。
- Core supported 首版建议：
  - `tap`
  - `hold`
  - `flick`
  - `bell`
  - `lane_block_area`
  - `lane_left_start`
  - `lane_center_start`
  - `lane_right_start`
  - `lane_colorful_start`
  - `wall_left_start`
  - `wall_right_start`
  - `bpm_change`
  - `meter_change`
  - `comment`
- 暂不支持 `objects.add` 创建：
  - connectable child (`lane_next` 等)
  - `hold_end`
  - beam 系列
  - bullet / bullet palette
  - soflan / individual soflan area / keyframe/interpolatable soflan
  - SVG prefab / autoplay fader lane / enemy set / click se
- 这些对象仍可通过 `objects.set_properties/delete/list` 在已有对象上工作，前提是它们存在且符合规则。
- `objects.list_supported_types` 要明确每个 type 的 `addSupported`。

Question:

第一版 `objects.add` 是否先支持上述 Core supported 集合，并暂缓 connectable child、hold_end、beam、bullet、soflan、SVG/editor object 等复杂类型的创建？

Answer:

是。第一版 `objects.add` 先支持 Core supported 集合，并暂缓 connectable child、hold_end、beam、bullet、soflan、SVG/editor object 等复杂类型的创建。

### 17. Placement, magnetic docking, and lane references

Status: decided

Problem:

- UI 拖放/刷入对象使用 `InteractiveManager` 和 `OnMoveCanvas(...)`，它依赖 canvas 坐标、编辑器缩放、磁吸设置和当前可见状态。
- MCP mutation 参数使用谱面坐标 (`TGrid` / `XGrid`) 更稳定，不天然有 canvas 坐标。
- `Tap`、`Hold` 等 dockable 对象有 `ReferenceLaneStart` / `ReferenceLaneStrIdManualSet`，UI 移动时会尝试吸附到最近 lane。
- 如果 MCP 默认自动吸附，结果会受用户当前编辑器设置影响，不利于可重复调用。

Recommended answer:

- 第一版 `objects.add` 默认只按请求里的谱面坐标和字段创建对象，不自动模拟 UI 磁吸。
- 对 `ILaneDockableChangable` 对象（首版主要是 `tap`、`hold`）支持显式字段：
  - `referenceLaneRecordId`：指定要绑定的 lane `RecordId`。
  - 可选 `snapXToLane = true`：绑定 lane 后，按该 lane 在对象 `TGrid` 处计算 `XGrid` 并覆盖请求中的 `XGrid`。
- 如果未传 `referenceLaneRecordId`，则 `ReferenceLaneStart = null`，不自动选最近 lane。
- 第一版不提供“按最近 lane 自动选择”的 implicit behavior。后续可加显式 `dockMode = nearest`。
- 对 `hold`，第一版只创建 hold start；`HoldEnd` 创建暂缓。需要完整 hold 时后续设计专门字段或支持 `hold_end`。

Question:

`objects.add` 第一版是否默认不做 UI 磁吸；dockable 对象只有在显式传 `referenceLaneRecordId` 时绑定 lane，并可选 `snapXToLane` 覆盖 XGrid？

Answer:

是。`objects.add` 第一版默认不做 UI 磁吸；dockable 对象只有在显式传 `referenceLaneRecordId` 时绑定 lane，并可选 `snapXToLane` 覆盖 XGrid。

### 18. Hold creation shape

Status: decided

Problem:

- `Hold` 在模型中是 start 对象，`HoldEnd` 通过 `Hold.SetHoldEnd(...)` 关联。
- UI 行为是先创建 hold start，再通过后续操作创建 `HoldEnd`。
- 第 16 条暂缓直接 `objects.add` 创建 `hold_end`，但如果 `hold` 只能创建 start，MCP 创建出来的 hold 往往不是完整 hold。
- `HoldEnd` 的 `XGrid` 如果绑定 lane，可以根据 lane 在 end `TGrid` 处计算。

Recommended answer:

- 第一版 `objects.add` 的 `hold` 支持可选 `endTGrid`。
- 如果传入 `endTGrid`：
  - 同一 add item 内创建 `Hold` 和关联的 `HoldEnd`。
  - `HoldEnd.TGrid = endTGrid`。
  - `HoldEnd.XGrid` 默认等于 hold start 的 `XGrid`。
  - 如果传了 `referenceLaneRecordId` 且 `snapXToLane = true`，则分别按 lane 在 start/end `TGrid` 处计算 start/end 的 `XGrid`。
  - 返回 items 中包含 hold start 的 handle，并在 item 摘要里包含 `holdEndObjectId`。
- 如果未传 `endTGrid`：
  - 只创建 hold start，结果中明确 `hasHoldEnd = false`。
- 校验 `endTGrid >= TGrid`；小于 start 时返回 validation error。
- 暂不开放独立 `hold_end` objectType 创建。

Question:

`objects.add` 的 `hold` 是否在第一版支持可选 `endTGrid`，用于同一事务创建关联 `HoldEnd`，但仍不开放独立 `hold_end` 创建？

Answer:

是。`objects.add` 的 `hold` 在第一版支持可选 `endTGrid`，用于同一事务创建关联 `HoldEnd`，但仍不开放独立 `hold_end` 创建。

### 19. Add-object conflict policy

Status: decided

Problem:

- Batch mode 会用 `GetConflictingObject(...)` 检查同位置冲突，遇到冲突时不添加对象而选中已有对象。
- 普通 toolbox drop action 不显式查冲突。
- MCP 自动化重复调用时，如果没有冲突策略，容易生成重复对象。
- 过于智能的默认合并/复用会让结果不透明。

Recommended answer:

- `objects.add` 支持 `conflictPolicy`：
  - `fail`：默认。发现冲突则整个调用失败，不写入。
  - `allow`：允许重复添加。
  - `return_existing`：不创建冲突项，返回已有对象 handle；但因为第 3 条 all-or-nothing，第一版对批量请求建议暂不支持该策略，或仅当所有 items 都冲突/可返回时才成功。
- 第一版推荐只实现 `fail` 和 `allow`。
- 冲突检测复用 `FumenVisualEditorViewModel.GetConflictingObject(...)` 的规则。
- 成功结果中标明 `conflictPolicy`。

Question:

`objects.add` 第一版是否加入 `conflictPolicy`，默认 `fail`，并仅支持 `fail` / `allow` 两种策略？

Answer:

同意。`objects.add` 第一版加入 `conflictPolicy`，默认 `fail`；第一版仅支持 `fail` / `allow` 两种策略。

### 20. Implementation layering

Status: decided

Problem:

- `Kernel/Mcp` 当前适合放 tool 形状、参数、授权预览和结果包装。
- 实际对象创建、查找、属性转换、撤销事务、Dispatcher、备份等逻辑如果直接写进 MCP tool 类，会让 tool 难测试且后续难复用。
- 现有 skill 文档也建议 `Kernel/Mcp` 保持薄，把 live-editor 逻辑放在 `Kernel/RuntimeAutomation`。

Recommended answer:

- 新增 `Kernel/RuntimeAutomation` 层服务，例如：
  - `IEditorObjectMutationService`
  - `EditorObjectMutationService`
  - `IEditorObjectRegistry`
  - `EditorObjectRegistry`
  - value codec / property accessor helper
- 新增 `Kernel/Mcp/ObjectTools.cs` 只负责：
  - MCP tool method 定义
  - 参数模型绑定
  - 调用授权服务
  - 构造授权预览
  - 调用 runtime service
  - 记录 MCP operation log
- `McpServerHost` 注入并注册 `ObjectTools`。
- `objects.list_supported_types` 可以直接读 registry；`objects.list` 和写操作走 service。

Question:

是否采用这个分层：`Kernel/Mcp/ObjectTools.cs` 保持薄，实际对象查询/变更逻辑放到 `Kernel/RuntimeAutomation` 的专门 service 和 registry？

Answer:

同意。采用这个分层：`Kernel/Mcp/ObjectTools.cs` 保持薄，实际对象查询/变更逻辑放到 `Kernel/RuntimeAutomation` 的专门 service 和 registry。

### 21. `objects.set_properties` request shape

Status: decided

Problem:

- 属性修改需要覆盖：
  - 单个对象改一个属性。
  - 单个对象改多个属性。
  - 多个对象批量改同一属性。
  - 多个对象分别改不同属性。
- 如果设计成“全局 properties + objectIds”，批量改同一属性很方便，但不能自然表达不同对象不同属性。
- 如果设计成低层 operation list，表达能力强，但请求更啰嗦。

Recommended answer:

- 第一版使用 `items` 数组，每个 item 描述一个目标对象和要修改的一组属性：
  - `objectId`
  - `expectedObjectType`
  - `properties`
- `properties` 是 property path 到 value 的 map，例如：
  - `"Tag": "intro"`
  - `"TGrid": { "unit": 12, "grid": 0 }`
  - `"ColorfulLaneBrightness.CurrentValue": 1.5`
- 单对象改单属性就是一个 item + 一个 property。
- 批量改同一属性就是多个 item，各自带相同 property。
- 批量改不同属性也自然支持。
- 同一 item 内禁止重复 property path；如果 JSON parser 已经无法保留重复 key，则文档仍说明不允许。
- 所有 items 在同一个 all-or-nothing transaction 中执行。

Question:

`objects.set_properties` 是否采用 `items[]` 形状：每个 item 包含 `objectId/expectedObjectType/properties`，其中 `properties` 是属性路径到值的 map？

Answer:

同意。`objects.set_properties` 采用 `items[]` 形状：每个 item 包含 `objectId`、`expectedObjectType`、`properties`，其中 `properties` 是属性路径到值的 map。

### 22. `objects.add` request shape

Status: decided

Problem:

- 第 8 条已决定 `objects.add` 按 registry 定义必填字段、可选字段和默认值。
- 第 17 条决定 dockable 绑定 lane 和 `snapXToLane` 是显式字段。
- 第 18 条决定 `hold` 支持可选 `endTGrid`。
- 第 19 条决定 add 支持 `conflictPolicy`，默认 `fail`。
- 请求形状需要能表达每个新增对象自己的类型、字段和策略。

Recommended answer:

- `objects.add` 使用 `items[]`，每个 item 包含：
  - `objectType`
  - `fields`
  - optional `conflictPolicy`
- `fields` 是 registry 允许的 add-time 字段 map，例如：
  - `TGrid`
  - `XGrid`
  - `Tag`
  - `IsCritical`
  - `Direction`
  - `EndTGrid` for `hold`
  - `ReferenceLaneRecordId`
  - `SnapXToLane`
- 顶层参数仍包含：
  - optional `editorId`
  - optional `expectedEditorId`
  - optional `transactionName`
  - optional `requireConfirmation`
  - `requestedBy`
  - `clientId`
- `conflictPolicy` 可以在顶层提供默认值，也可以在 item 覆盖；如果两者都未提供则为 `fail`。
- 所有 items 属于同一个 all-or-nothing transaction。

Question:

`objects.add` 是否也采用 `items[]` 形状：每个 item 包含 `objectType/fields/conflictPolicy`，顶层只放 editor、事务、授权等公共参数？

Answer:

同意。`objects.add` 采用 `items[]` 形状：每个 item 包含 `objectType`、`fields`、`conflictPolicy`，顶层只放 editor、事务、授权等公共参数。

### 23. `objects.delete` request shape

Status: decided

Problem:

- 第 9 条决定删除带 children 的 `ConnectableStartObject` 需要显式 `cascade = true`。
- 删除批量对象时，不同对象可能有不同 cascade 需求。
- 如果只提供顶层 `cascade`，一次误传可能影响所有对象，风险较高。
- 删除也需要支持 `expectedObjectType` 来避免错删。

Recommended answer:

- `objects.delete` 使用 `items[]`，每个 item 包含：
  - `objectId`
  - optional `expectedObjectType`
  - optional `cascade`
- `cascade` 默认 `false`，并且优先作为 item 级字段。
- 顶层不提供全局 `cascade`，避免一次开关影响整批对象。
- 所有 items 属于同一个 all-or-nothing transaction。
- 对同一个 object id 重复出现在 items 中，视为 validation error。

Question:

`objects.delete` 是否采用 `items[]` 形状，并把 `cascade` 设计成每个 item 的显式字段，不提供顶层全局 `cascade`？

Answer:

同意。`objects.delete` 采用 `items[]` 形状，并把 `cascade` 设计成每个 item 的显式字段，不提供顶层全局 `cascade`。

### 24. `objects.list` query shape

Status: decided

Problem:

- 第 13 条决定第一版加入 `objects.list`，用于获取删除/修改所需的 object handles。
- 谱面对象数量可能较多，不能默认返回完整对象图。
- 客户端通常会按类型、时间范围、是否选中等条件查询。
- 查询结果需要包含 `objectId`、`objectType` 和核心定位字段，但完整属性读取可以后续扩展。

Recommended answer:

- `objects.list` 顶层参数：
  - optional `editorId`
  - optional `expectedEditorId`
  - optional `objectTypes: string[]`
  - optional `tGridRange: { start?: TGrid, end?: TGrid }`
  - optional `selectedOnly: bool`
  - optional `includeHiddenDisplayObjects: bool = false`
  - optional `limit`
  - optional `offset`
  - `requestedBy`
  - `clientId`
- 默认返回当前 editor 的 displayable object handles，按 `TGrid`、`objectType`、`objectId` 排序。
- 每个 item 返回：
  - `objectId`
  - `objectType`
  - CLR type name
  - `idShortName`
  - `tGrid` if present
  - `xGrid` if present
  - `recordId` if present
  - `tag`
  - `isSelected`
  - 简短 `summary`
- 第一版不返回所有可写属性值；后续可加 `objects.inspect`。
- 结果包含 `totalCount`、`offset`、`limit`，方便分页。

Question:

`objects.list` 第一版是否只返回对象 handle 和核心摘要字段，并通过 `objectTypes/tGridRange/selectedOnly/limit/offset` 做过滤分页，不返回完整属性图？

Answer:

同意。`objects.list` 第一版只返回对象 handle 和核心摘要字段，并通过 `objectTypes`、`tGridRange`、`selectedOnly`、`limit`、`offset` 做过滤分页，不返回完整属性图。

### 25. `objects.list_supported_types` schema shape

Status: decided

Problem:

- `objects.add` 依赖 object type registry。
- `objects.set_properties` 依赖可写属性和 value codec。
- 客户端需要在调用前知道：
  - 哪些 `objectType` 可创建。
  - 创建时哪些字段必填/可选。
  - 哪些属性可通过 `set_properties` 修改。
  - 每个字段/属性的 value format。
- 如果只在文档里描述，MCP 客户端无法运行时发现能力。

Recommended answer:

- `objects.list_supported_types` 返回 registry schema，包含 `schemaVersion`。
- 每个 type entry 包含：
  - `objectType`
  - CLR type name
  - display name
  - `addSupported`
  - `deleteSupported`
  - `setPropertiesSupported`
  - `addFields`
  - `writableProperties`
  - `nestedWritableProperties`
  - notes / limitations
- field/property schema 包含：
  - `name`
  - `valueKind`，例如 `string`、`bool`、`number`、`enum`、`tGrid`、`xGrid`
  - `required`
  - `nullable`
  - enum values if enum
  - default value if meaningful
  - short description
- 对暂缓 add 的复杂类型也可以列出 `addSupported = false` 和原因，方便客户端理解。

Question:

`objects.list_supported_types` 是否作为运行时 schema 入口，返回每个 `objectType` 的 add 支持、字段 schema、可写属性 schema 和限制说明？

Answer:

同意。`objects.list_supported_types` 作为运行时 schema 入口，返回每个 `objectType` 的 add 支持、字段 schema、可写属性 schema 和限制说明。

### 26. Verification and test scope

Status: decided

Problem:

- 这组能力覆盖 MCP tool、runtime service、对象 registry、value codec、属性可写规则、撤销事务和 UI Dispatcher。
- 如果只手测 MCP 调用，属性转换、all-or-nothing、cascade、undo 等关键行为容易回归。
- 当前仓库没有明显的独立单元测试项目；实现时可能需要选择轻量测试方式。

Recommended answer:

- 第一版至少覆盖 service 层自动化测试或可重复验证脚本，重点不把测试压力放在 ASP.NET MCP transport 上。
- 必测场景：
  - `objects.add` 成功添加核心类型，并返回 object handle。
  - `objects.add` conflictPolicy 默认 `fail` 和显式 `allow`。
  - `hold` 带 `endTGrid` 创建关联 `HoldEnd`。
  - `objects.delete` 删除普通对象、删除 connectable child、start 无 cascade 拒绝、start 有 cascade 成功。
  - `objects.set_properties` 顶层属性修改、`RangeValue.CurrentValue` 修改、只读/隐藏属性拒绝。
  - 批量 all-or-nothing：任一项失败时不改谱面、不进 undo。
  - undo/redo 能恢复 add/delete/set。
  - `objects.list` 和 `objects.list_supported_types` 返回稳定 schema/handles。
- 如果现阶段不方便新增正式测试项目，至少增加 runtime service 级别的调试/验证入口或文档化手测 checklist；但推荐优先补测试项目。

Question:

第一版实现是否必须包含 service 层可重复验证，至少覆盖 add/delete/set 的成功、失败、all-or-nothing 和 undo/redo，而不是只靠手动 MCP 调用验证？

Answer:

同意。第一版实现必须包含 service 层可重复验证，至少覆盖 add/delete/set 的成功、失败、all-or-nothing 和 undo/redo，而不是只靠手动 MCP 调用验证。

### 27. Dirty state, selection, and UI refresh after mutation

Status: decided

Problem:

- `OnFumenObjectModifiedChanged(...)` 会在对象属性变化时设置 `IsDirty = true` 并重算滚动指标。
- `OngekiFumen.AddObject(...)` / `RemoveObject(...)` 本身不明显触发 `ObjectModifiedChanged`，因此 add/delete 可能需要显式标脏。
- 现有 UI 删除会取消被删对象选中并刷新属性浏览器。
- UI 添加通常会选中新建对象，但 MCP 自动化未必应该改变用户当前选择。

Recommended answer:

- 每个成功写操作显式设置 `editor.IsDirty = true`，并执行必要的 `RecalculateScrollMetrics()` / property browser refresh。
- `objects.delete` 删除目标时取消这些对象选中，并刷新属性浏览器。
- `objects.set_properties` 修改当前选中对象时刷新属性浏览器。
- `objects.add` 默认不改变当前选区，避免 MCP 调用打断用户 UI 状态。
- `objects.add` 支持可选顶层参数 `selectCreated: bool = false`：
  - `false`：不改变选区。
  - `true`：清空当前选择并选中新建的可选择对象，然后刷新属性浏览器。
- undo/redo 时也应恢复/刷新到一致状态；第一版可以选择不恢复调用前选区，但必须避免属性浏览器显示已删除对象。

Question:

MCP 写操作是否应显式标记 dirty 并刷新 UI；`objects.add` 默认不改变选区，但提供 `selectCreated = true` 让调用方选中新建对象？

Answer:

同意。MCP 写操作显式标记 dirty 并刷新 UI；`objects.add` 默认不改变选区，但提供 `selectCreated = true` 让调用方选中新建对象。

### 28. API naming and case sensitivity

Status: decided

Problem:

- `objectType` 是外部 API 名称，前面已倾向使用稳定 snake_case。
- `objects.set_properties` 的 property path 需要映射到 CLR public property，例如 `TGrid`、`XGrid`、`Tag`、`IsCritical`。
- 对象属性浏览器有本地化 alias，例如 `RefLaneId`，但这些 alias 面向 UI，不适合作为 API 稳定名称。
- 如果同时接受多种大小写或本地化名称，错误提示、schema 和歧义处理都会复杂。

Recommended answer:

- tool 名称使用 MCP 当前风格：`objects.add`、`objects.delete`、`objects.set_properties`。
- `objectType` 使用 lower snake_case，例如 `lane_left_start`。
- `fields` 和 `properties` 使用 schema 暴露的 canonical API names。
- 第一版 canonical API names 对现有 CLR 属性使用原始 PascalCase，例如：
  - `TGrid`
  - `XGrid`
  - `Tag`
  - `IsCritical`
  - `Direction`
  - `ColorfulLaneBrightness.CurrentValue`
- add-only pseudo fields 也用 PascalCase：
  - `EndTGrid`
  - `ReferenceLaneRecordId`
  - `SnapXToLane`
- 不接受本地化 display name / alias 作为输入。
- 第一版输入名称大小写敏感；名称不匹配返回 `UNSUPPORTED_FIELD` 或 `UNSUPPORTED_PROPERTY`，并在 error 中给出 schema 里的可用名称。

Question:

API 命名是否按这个规则固定：`objectType` 用 lower snake_case，字段/属性路径用 schema 暴露的 PascalCase canonical name，并且输入大小写敏感、不接受本地化 alias？

Answer:

同意。API 命名按这个规则固定：`objectType` 用 lower snake_case，字段/属性路径用 schema 暴露的 PascalCase canonical name，并且输入大小写敏感、不接受本地化 alias。

### 29. Concurrent mutation requests

Status: decided

Problem:

- MCP server 可能同时收到多个客户端请求。
- 即使实际执行切回 WPF Dispatcher，两个请求仍可能出现“请求 A 校验通过、请求 B 修改对象、请求 A 再执行”的竞态。
- 批量 all-or-nothing 需要保证同一次请求的校验和执行之间目标状态稳定。
- 现有 `UndoRedoManager.ExecuteAction(...)` 会进入 render data write lock，但它不能覆盖 mutation service 的整段校验和 action 构造。

Recommended answer:

- `EditorObjectMutationService` 为写操作维护 per-editor async lock。
- 同一 editor 的 `objects.add`、`objects.delete`、`objects.set_properties` 串行执行。
- 不同 editor 可以并行，但最终 UI Dispatcher 会自然调度；实现可先全局串行，后续再优化为 per-editor。
- `objects.list` 可以不拿写锁，但需要在 UI Dispatcher 上读取，或者使用短读锁生成快照。
- 写操作在锁内完成：
  - resolve target editor
  - validate all items
  - construct undo action
  - execute undo action
  - build result
- 如果请求等待时目标 editor 已关闭，返回 `EDITOR_NOT_FOUND`。

Question:

第一版是否要求 object mutation 写操作按目标 editor 串行化，确保同一 editor 上不会并发执行两个 add/delete/set 请求？

Answer:

同意。第一版要求 object mutation 写操作按目标 editor 串行化，确保同一 editor 上不会并发执行两个 add/delete/set 请求。

### 30. Addressable object surface

Status: decided

Problem:

- `OngekiFumen.GetAllDisplayableObjects()` 返回的不只是顶层集合对象，还包括：
  - `HoldEnd`
  - `ConnectableChildObjectBase`
  - `LaneCurvePathControlObject`
  - `LaneBlockAreaEndIndicator`
  - `SoflanEndIndicator`
  - `IndividualSoflanAreaEndIndicator`
- 这些对象很多可选中、可拖动、可删除或可改属性，但并不都适合通过 `objects.add` 独立创建。
- 第 16 条已经限制 `objects.add` 只创建核心类型。
- 第 9 条决定 delete 复用 UI 删除语义，因此 delete 应该能处理一些子显示对象。

Recommended answer:

- 第一版区分：
  - `addSupported`：只能创建 registry 标记支持的类型。
  - `addressableForList/delete/set`：可以通过 `objects.list` 获取并用 object id 删除/修改的 displayable objects。
- `objects.list` 默认列出 `GetAllDisplayableObjects().OfType<OngekiObjectBase>()` 中可寻址对象，但可以通过 `includeAuxiliary = false` 隐藏 end indicators / curve controls 等辅助对象。
- 每个 list item 增加：
  - `isAuxiliary`
  - `ownerObjectId` if applicable
  - `ownerObjectType` if applicable
  - `addSupported`
- `objects.delete` 对可寻址辅助对象按现有 UI 删除语义处理。
- `objects.set_properties` 对可寻址辅助对象仍按第 6/7 条属性规则校验；如果属性浏览器不允许则拒绝。
- `objects.add` 不因某类型 addressable 而自动支持创建。

Question:

第一版是否把 `objects.list/delete/set_properties` 的可寻址范围扩展到 displayable 辅助对象，但 `objects.add` 仍只允许 Core supported 类型？

Answer:

同意。第一版把 `objects.list/delete/set_properties` 的可寻址范围扩展到 displayable 辅助对象，但 `objects.add` 仍只允许 Core supported 类型。

### 31. Backup failure behavior

Status: decided

Problem:

- 第 10 条决定复用执行前 fumen 备份机制。
- 当前 runtime script 的备份逻辑要求：
  - `editor.EditorProjectData.FumenFilePath` 非空。
  - 源 fumen 文件存在。
  - 文件复制成功。
- 新建或未保存工程可能没有有效 fumen 文件路径。
- 如果用户在确认对话框里要求备份，但备份失败后仍继续写入，会违背用户对“先备份再执行”的预期。

Recommended answer:

- 如果用户未要求备份，则 mutation 正常执行。
- 如果用户要求备份，则备份失败时整个 mutation 失败，不修改谱面、不产生 undo action。
- 返回 `FUMEN_BACKUP_FAILED`，并在 `errorMessage/logs` 中说明具体原因，提示用户先保存当前工程/谱面后重试。
- 不做“备份失败但继续执行”的自动降级；只有用户明确要求跳过备份时才允许直接处理。
- 后续可以支持“导出当前内存谱面到临时备份文件”，但第一版先复用现有文件复制语义。

Question:

如果用户要求执行前备份，但当前 editor 没有有效 fumen 文件路径或备份复制失败，object mutation 是否应整体失败并不修改谱面？

Answer:

默认整体失败并提醒用户先保存；只有用户明确要求才可以跳过备份直接处理。

### 32. Explicit backup bypass

Status: decided

Problem:

- 第 31 条决定：用户要求备份但备份失败时，默认提醒先保存并中止。
- 用户也希望“只有明确要求才可以直接处理”。
- 这个“明确要求”需要在 API 和确认流程中可审计，不能由客户端静默绕过。

Recommended answer:

- 增加写 tool 顶层参数 `allowProceedWithoutBackup = false`。
- 仅当同时满足以下条件时允许备份失败后继续执行：
  - 用户在请求中显式传 `allowProceedWithoutBackup = true`。
  - 本次请求仍经过交互确认，或客户端已有 remembered approval。
  - 授权预览明确显示“备份失败时仍将继续修改当前谱面”。
- 如果用户要求备份但备份失败，且 `allowProceedWithoutBackup != true`，返回 `FUMEN_BACKUP_FAILED` 并提示保存。
- 即使允许跳过备份，也要在 result logs 中记录备份失败和继续执行原因。

Question:

是否用顶层参数 `allowProceedWithoutBackup` 表达“用户明确要求备份失败也继续处理”，默认 `false`，并且授权预览必须明确提示这个风险？

Answer:

同意。用顶层参数 `allowProceedWithoutBackup` 表达“用户明确要求备份失败也继续处理”，默认 `false`，并且授权预览必须明确提示这个风险。

### 33. Core add field schema strategy

Status: decided

Problem:

- Core supported 类型也有不同属性：
  - timeline-only: `bpm_change`、`meter_change`、`comment`
  - movable: `tap`、`hold`、`flick`、`bell`
  - lane starts: lane/wall/colorful
  - duration-like: `hold` with optional `EndTGrid`、`lane_block_area` with end indicator
- 如果第一版给每个类型开放太多字段，会增加转换/校验复杂度。
- 如果字段过少，创建对象后需要额外 `set_properties`，但 API 更稳。

Recommended answer:

- Core add 字段采用“最小可用 + 少量高频字段”策略。
- 所有 timeline 对象：
  - required `TGrid`
  - optional `Tag`
- 所有 movable 对象：
  - required `TGrid`
  - required `XGrid`
  - optional `Tag`
- `tap` / `hold` / `flick`：
  - optional `IsCritical`
- `flick`：
  - optional `Direction`
- `tap` / `hold`：
  - optional `ReferenceLaneRecordId`
  - optional `SnapXToLane`
- `hold`：
  - optional `EndTGrid`
- lane starts：
  - required `TGrid`
  - required `XGrid`
  - optional `Tag`
  - optional `IsTransparent`
- `lane_colorful_start`:
  - optional `ColorId`
  - optional `Brightness`
- `bpm_change`:
  - required `TGrid`
  - optional `BPM`
- `meter_change`:
  - required `TGrid`
  - optional `BunShi`
  - optional `Bunbo`
- `comment`:
  - required `TGrid`
  - optional `Content`
  - optional `Tag`
- 其他属性创建后用 `objects.set_properties` 修改。

Question:

Core add 字段是否采用上述“最小可用 + 少量高频字段”策略，而不是第一版就开放每个类型的全部可写属性？

Answer:

同意。Core add 字段采用“最小可用 + 少量高频字段”策略，而不是第一版就开放每个类型的全部可写属性。

### 34. `lane_block_area` creation fields

Status: decided

Problem:

- `lane_block_area` 已纳入 Core supported。
- `LaneBlockArea` 是 timeline object，不是 movable object，但它有：
  - start `TGrid`
  - `EndIndicator.TGrid`
  - `Direction`
- 如果只要求 `TGrid`，创建出来的 lane block 时长为 0 或依赖默认 end indicator 行为，不太可用。

Recommended answer:

- `lane_block_area` add fields:
  - required `TGrid`
  - required `EndTGrid`
  - optional `Direction`
  - optional `Tag`
- 校验 `EndTGrid >= TGrid`。
- `Direction` 使用 enum 字符串名：`Left` / `Right`。
- `EndTGrid` 只作为 add-time pseudo field；创建后如需修改结束点，可通过 `objects.list` 找到 `LaneBlockAreaEndIndicator` 辅助对象，再用 `objects.set_properties` 修改其 `TGrid`。

Question:

`lane_block_area` 创建时是否要求同时传 `TGrid` 和 `EndTGrid`，并只把 `Direction` / `Tag` 作为可选字段？

Answer:

同意。`lane_block_area` 创建时要求同时传 `TGrid` 和 `EndTGrid`，并只把 `Direction` / `Tag` 作为可选字段。

### 35. `ColorId` value format

Status: decided

Problem:

- `lane_colorful_start` 可选字段 `ColorId` 的 CLR 类型是 `ColorId` struct，不是 enum。
- `ColorIdConst.AllColors` 提供稳定的预定义颜色，包含 `Id`、`Name`、`Color`。
- 如果允许完整 `{ id, name, color }` 输入，客户端可能构造出项目不认识的颜色。

Recommended answer:

- 第一版 `ColorId` 输入只允许两种格式：
  - number：匹配 `ColorIdConst.AllColors` 中的 `Id`。
  - string：匹配 `ColorIdConst.AllColors` 中的 `Name`，大小写敏感。
- 不允许客户端传完整自定义 color object。
- 输出 canonical 格式：
  - `{ "id": 0, "name": "Akari" }`
- `objects.list_supported_types` 在 `ColorId` 字段 schema 中列出所有可用 `id/name`。

Question:

`ColorId` 第一版是否只允许输入预定义 id 或 name，不允许自定义颜色对象？

Answer:

同意。`ColorId` 第一版只允许输入预定义 id 或 name，不允许自定义颜色对象。

### 36. `bell` creation fields

Status: decided

Problem:

- `bell` 虽然在 Core supported add 类型里，但代码中 `Bell` 同时支持：
  - 默认 BEL：只保存 `TGrid` / `XGrid` / palette id。
  - custom bell：保存 `ShooterValue` / `PlaceOffset` / `TargetValue` / `Speed` / `SizeValue` / `RandomOffsetRange`。
  - `ReferenceBulletPallete`：引用 fumen 的 `BulletPalleteList`，并且 palette 存在时 projectile 参数变成只读派生值。
- 如果第一版在 `objects.add` 中直接开放 palette/custom projectile 参数，会把 bullet palette 管理也纳入本次 tool 范围。

Recommended answer:

- 第一版 `objects.add` 的 `bell` 支持默认 BEL、palette 引用和 custom projectile 参数：
  - required `TGrid`
  - required `XGrid`
  - optional `Tag`
  - optional `ReferenceBulletPalleteStrId`
  - optional `ShooterValue`
  - optional `PlaceOffset`
  - optional `TargetValue`
  - optional `Speed`
  - optional `SizeValue`
  - optional `RandomOffsetRange`
- `ReferenceBulletPalleteStrId` 引用当前 fumen 的 `BulletPalleteList`，找不到时报校验错误。
- custom projectile 参数使用 JSON number / enum 字符串名，不使用 ogkr 缩写。

Question:

`bell` 第一版创建是否只支持默认 BEL 的 `TGrid` / `XGrid` / `Tag`，暂不开放 palette 引用和 custom projectile 参数？

Answer:

不同意。`bell` 第一版创建要同时支持默认 BEL、palette 引用和 custom projectile 参数。

### 37. `bell` palette/custom conflict rule

Status: decided

Problem:

- `Bell` 设置 `ReferenceBulletPallete` 后，`Speed` / `PlaceOffset` / `TargetValue` / `ShooterValue` / `RandomOffsetRange` 等属性从 palette 派生。
- 属性浏览器里这些字段有 `ProjectilePropertyBrowserReadOnlyForPalleteIsSet` 规则。
- 如果 add request 同时传 `ReferenceBulletPalleteStrId` 和 custom projectile 参数，custom 参数会被 palette 覆盖或变成语义不清。

Recommended answer:

- 第一版禁止同一个 `bell` item 同时传 `ReferenceBulletPalleteStrId` 和 custom projectile 参数。
- 允许三种模式：
  - 默认 BEL：只传 `TGrid` / `XGrid` / `Tag`。
  - palette BEL：传 `ReferenceBulletPalleteStrId`，不传 custom projectile 参数。
  - custom BEL：不传 `ReferenceBulletPalleteStrId`，可传 `ShooterValue` / `PlaceOffset` / `TargetValue` / `Speed` / `SizeValue` / `RandomOffsetRange`。
- 如果同时传，all-or-nothing 校验失败，不修改 fumen。

Question:

`bell` 创建时是否禁止同一个 item 同时传 palette 引用和 custom projectile 参数？

Answer:

同意。`bell` 创建时禁止同一个 item 同时传 palette 引用和 custom projectile 参数；同时传则校验失败且不修改 fumen。

### 38. Bullet palette mutation support

Status: decided

Problem:

- `bell` 创建现在支持 `ReferenceBulletPalleteStrId`。
- palette id 来自当前 fumen 的 `BulletPalleteList`，字段是 `BulletPallete.StrID`。
- `OngekiFumen.AddObject/RemoveObject` 已经支持 `BulletPallete`。
- 如果 palette 只能只读发现，客户端仍无法自动创建 bell 所需的 palette。

Recommended answer:

- 把 `bullet_pallete` 纳入同一套 object mutation registry，而不是新增单独 palette tools。
- `objects.add` 支持 `objectType: "bullet_pallete"`。
- `objects.delete` 支持删除 `bullet_pallete`。
- `objects.set_properties` 支持修改 `bullet_pallete` 的可写属性。
- `objects.list` 支持 `objectTypes: ["bullet_pallete"]`，返回 `StrID`、`EditorName`、`ShooterValue`、`TargetValue`、`Speed`、`SizeValue`、`TypeValue`、`PlaceOffset`、`RandomOffsetRange`。
- `objects.list_supported_types` 标记 `bell.ReferenceBulletPalleteStrId` 为 dynamic reference，并说明可通过 `objects.list` 查询 `bullet_pallete`。
- `objects.add` 的 `ReferenceBulletPalleteStrId` 只接受当前 fumen 已存在的 `StrID`，找不到则校验失败。

Question:

是否通过 `objects.list` 暴露当前 fumen 的 `bullet_pallete` 只读列表，用于支持 `bell.ReferenceBulletPalleteStrId` 选择，而第一版不支持修改 palette 本身？

Answer:

不同意。`bullet_pallete` 也要支持新增、修改和删除，并纳入同一套 `objects.add/delete/set_properties/list`。

### 39. `bullet_pallete` `StrID` conflict policy

Status: decided

Problem:

- `BulletPalleteList.AddPallete` 在 `StrID` 重复时会移除旧 palette 并加入新 palette。
- 这种默认行为对 UI 也许可接受，但对 MCP 批量写入风险很高：新增一个重复 `StrID` 可能隐式替换已有 palette。
- 已有 `bell` / `bullet` 可能引用旧 palette 对象；如果隐式替换，引用关系可能变得不直观。
- 前面已决定 `objects.add` 有 `conflictPolicy`，默认 `fail`，第一版仅 `fail` / `allow`。

Recommended answer:

- `bullet_pallete` 新增不使用 `conflictPolicy` 放宽重复 `StrID`。
- 如果请求显式传入的 `StrID` 已存在，则直接校验失败，不修改 fumen。
- 即使 item 级 `conflictPolicy` 为 `allow`，也不能替换已有 palette。
- 如果 add 时不传 `StrID`，沿用 `BulletPalleteList.AddPallete` 自动生成 `StrID`，不视为冲突。

Question:

`bullet_pallete` 新增遇到重复 `StrID` 时，是否遵守 `conflictPolicy`：默认失败，只有显式 `allow` 才允许替换已有 palette？

Answer:

不同意。添加的 `StrID` 不允许与已存在的 `StrID` 冲突；冲突时直接报错，不允许通过 `conflictPolicy` 替换。

### 40. Referenced `bullet_pallete` delete behavior

Status: decided

Problem:

- UI 删除 palette 时会检查 `Bells` 和 `Bullets` 中所有 `IBulletPalleteReferencable.ReferenceBulletPallete`，如果仍有引用就阻止删除。
- 如果 MCP 直接删除被引用的 palette，已有 `bell` / `bullet` 可能仍持有对象引用，保存、显示和后续编辑语义会变得不清晰。
- 另一种选择是删除 palette 时自动把引用它的 projectile 改成 custom/local 参数，但这是隐式批量修改。

Recommended answer:

- 第一版复用 UI 保护：删除被任何 `bell` / `bullet` 引用的 `bullet_pallete` 时校验失败。
- 不提供自动清引用或自动转 custom。
- 用户如果要删除被引用 palette，必须先显式用 `objects.set_properties` 把引用对象的 `ReferenceBulletPalleteStrId` 改为 `null` 或改到别的 palette，再删除。
- 错误结果返回引用该 palette 的对象 handles，方便客户端二次处理。

Question:

删除 `bullet_pallete` 时，如果仍有 `bell` / `bullet` 引用它，是否直接报错并要求用户先显式处理引用？

Answer:

同意。删除 `bullet_pallete` 时，如果仍有 `bell` / `bullet` 引用它，直接报错并要求用户先显式处理引用。

### 41. `bullet` add support

Status: decided

Problem:

- 前面最初的 Core supported add 类型没有包含 `bullet`，只包含了 `bell`。
- 现在已经决定：
  - `bell` 支持 palette 引用和 custom projectile 参数。
  - `bullet_pallete` 支持新增、修改、删除。
- `Bullet` 的代码结构和 `Bell` 很接近，但比 `Bell` 多：
  - `BulletDamageTypeValue`
  - `TypeValue`
- 如果不支持创建 `bullet`，palette mutation 能力只能服务于已有 bullet 或新建 bell，能力不完整。

Recommended answer:

- 第一版把 `bullet` 加入 `objects.add` 支持白名单。
- `bullet` add fields:
  - required `TGrid`
  - required `XGrid`
  - optional `Tag`
  - optional `BulletDamageTypeValue`
  - optional `ReferenceBulletPalleteStrId`
  - optional `ShooterValue`
  - optional `PlaceOffset`
  - optional `TargetValue`
  - optional `Speed`
  - optional `SizeValue`
  - optional `TypeValue`
  - optional `RandomOffsetRange`
- `ReferenceBulletPalleteStrId` 和 custom projectile 参数同样不能同时传。
- enum 使用 CLR 字符串名，不使用 ogkr 缩写。

Question:

第一版是否把 `bullet` 也加入 `objects.add` 支持，并采用与 `bell` 相同的 palette/custom 冲突规则？

Answer:

同意。第一版把 `bullet` 也加入 `objects.add` 支持，并采用与 `bell` 相同的 palette/custom 冲突规则。

### 42. Palette reference property codec

Status: decided

Problem:

- `Bullet.ReferenceBulletPallete` 和 `Bell.ReferenceBulletPallete` 的 CLR 类型是 `BulletPallete?`，MCP 客户端不能直接构造对象引用。
- UI 的 `BulletPalleteTypeUIViewModel` 允许用 `StrID` 文本选择 palette，并且还能设为 null。
- 之前已经规定字段/属性路径使用 canonical PascalCase，大小写敏感，不接受本地化 alias。
- 如果在 `objects.set_properties` 中暴露 `ReferenceBulletPallete` 原始属性名，value codec 会和 `objects.add` 的 `ReferenceBulletPalleteStrId` 不一致。

Recommended answer:

- 在 MCP schema 中使用 pseudo property path `ReferenceBulletPalleteStrId`，不直接暴露 `ReferenceBulletPallete`。
- 对 `bullet` / `bell`：
  - `ReferenceBulletPalleteStrId: "A1"`：绑定到当前 fumen 中 `StrID` 精确匹配的 palette。
  - `ReferenceBulletPalleteStrId: null`：清除 palette 引用，回到 custom/local 参数模式。
- 查找大小写敏感，找不到则校验失败。
- `objects.list` 输出 `ReferenceBulletPalleteStrId`，而不是序列化整个 palette 对象。
- 对设置了 palette 的 `bullet` / `bell`，custom projectile 参数仍按属性浏览器只读规则不可写；必须先把 `ReferenceBulletPalleteStrId` 设为 `null`，再写 custom 参数。

Question:

`ReferenceBulletPallete` 在 MCP 中是否统一用 pseudo 属性 `ReferenceBulletPalleteStrId` 表达，支持字符串绑定和 `null` 清除，并保持大小写敏感？

Answer:

同意。`ReferenceBulletPallete` 在 MCP 中统一用 pseudo 属性 `ReferenceBulletPalleteStrId` 表达，支持字符串绑定和 `null` 清除，并保持大小写敏感。

### 43. `bullet_pallete` add fields

Status: decided

Problem:

- `bullet_pallete` 已纳入 `objects.add/delete/set_properties/list`。
- `BulletPallete` 的核心 projectile 参数包括 `ShooterValue`、`PlaceOffset`、`RandomOffsetRange`、`TargetValue`、`SizeValue`、`TypeValue`、`Speed`。
- 它还有编辑器元数据 `EditorName` 和 `EditorAxuiliaryLineColor`。
- 如果 add 时只允许 `StrID`，客户端需要额外一次 `set_properties` 才能创建可用 palette。

Recommended answer:

- `bullet_pallete` add fields:
  - optional `StrID`
  - optional `EditorName`
  - optional `EditorAxuiliaryLineColor`
  - optional `ShooterValue`
  - optional `PlaceOffset`
  - optional `RandomOffsetRange`
  - optional `TargetValue`
  - optional `SizeValue`
  - optional `TypeValue`
  - optional `Speed`
- 不要求必填字段；不传 `StrID` 时沿用现有自动生成逻辑。
- `StrID` 为空字符串或空白视为未传。
- enum 使用 CLR 字符串名。
- `IsEnableSoflan` 是派生只读值，不允许 add/set。

Question:

`bullet_pallete` 创建时是否允许一次性传入上述全部可写 palette 字段，并且 `StrID` 可选、不传时自动生成？

Answer:

同意。`bullet_pallete` 创建时允许一次性传入上述全部可写 palette 字段，并且 `StrID` 可选、不传时自动生成。

### 44. `Color` value codec

Status: decided

Problem:

- `BulletPallete.EditorAxuiliaryLineColor` 使用项目内 `OngekiFumenEditor.Base.ValueTypes.Color`，结构是 `byte A, byte R, byte G, byte B`。
- `Color.ToString()` 输出 `#AARRGGBB`，但字符串输入会引入 `#RRGGBB` / `#AARRGGBB` / 颜色名等歧义。
- MCP value codec 已经尽量避免展示字符串。

Recommended answer:

- 第一版 `Color` 输入只允许 JSON object：
  - `{ "a": 255, "r": 189, "g": 183, "b": 107 }`
- 字段名小写固定为 `a/r/g/b`。
- 每个分量必须是 0-255 integer。
- 不接受 `#AARRGGBB` / `#RRGGBB` 字符串，也不接受颜色名。
- 输出 canonical 格式同样是 `{ "a": ..., "r": ..., "g": ..., "b": ... }`，可额外附带只读展示字符串 `hex: "#AARRGGBB"`。

Question:

`Color` 类型第一版是否只接受 `{a,r,g,b}` JSON 对象，不接受 hex 字符串或颜色名？

Answer:

同意。`Color` 类型第一版只接受 `{a,r,g,b}` JSON 对象，不接受 hex 字符串或颜色名。

### 45. `bullet_pallete.StrID` mutation behavior

Status: decided

Problem:

- `BulletPalleteList` 内部用 `ConvertIdToInt(StrID)` 作为 dictionary key。
- `BulletPalleteList.OnPalletePropChanged` 当前为空；直接修改 `BulletPallete.StrID` 不会更新内部索引。
- `objects.set_properties` 如果像普通属性一样 set `StrID`，后续通过 `BulletPalleteList[strId]` 或枚举排序可能出现不一致。
- 但用户已经要求 `bullet_pallete` 支持修改。

Recommended answer:

- `StrID` 修改作为特殊操作处理，不走普通属性 setter。
- 修改 `StrID` 时：
  - 新值不能为空或空白。
  - 新值不能与当前 fumen 已存在的其他 palette `StrID` 冲突。
  - 通过从 `BulletPalleteList` 移除旧 palette、设置新 `StrID`、再添加回列表来维护索引。
  - palette 对象本身保持同一个实例，因此已有 `bell` / `bullet` 对它的引用继续有效。
- `StrID` 修改仍在同一个 undo step 中；undo 时用同样特殊路径改回旧 `StrID`。

Question:

修改 `bullet_pallete.StrID` 时是否作为特殊操作处理，禁止空值和冲突，并通过移除/改值/重加来维护 `BulletPalleteList` 索引？

Answer:

同意。修改 `bullet_pallete.StrID` 时作为特殊操作处理，禁止空值和冲突，并通过移除/改值/重加来维护 `BulletPalleteList` 索引。

### 46. Intra-batch references for `objects.add`

Status: decided

Problem:

- `objects.add` 支持批量创建。
- 现在同一个 batch 可能同时创建：
  - 一个新的 `bullet_pallete`
  - 一个引用该 palette `StrID` 的 `bullet` 或 `bell`
- 前面已决定 add 是 all-or-nothing，因此可以先完整校验整个 batch 再应用。
- 如果只允许引用 batch 开始前已存在的 palette，客户端需要两次 tool call 才能创建 palette 并引用它。

Recommended answer:

- `objects.add` 支持同一请求内后续 item 引用本请求内将创建的 `bullet_pallete.StrID`。
- 校验阶段先收集：
  - 当前 fumen 已存在 palette `StrID`
  - 本次 add 中显式传入的 palette `StrID`
- 本次 add 中不传 `StrID`、依赖自动生成的 palette，不能被同 batch 的其他 item 通过 `ReferenceBulletPalleteStrId` 引用，因为生成值在校验前不可稳定表达。
- 同 batch 内显式 `StrID` 重复，或与现有 `StrID` 冲突，直接校验失败。
- 应用阶段先创建 palette，再创建引用它们的 `bullet` / `bell`，但结果仍作为一个 undo step。

Question:

`objects.add` 是否允许同一批次内的 `bullet` / `bell` 引用本批次显式 `StrID` 创建的 `bullet_pallete`？

Answer:

同意。`objects.add` 允许同一批次内的 `bullet` / `bell` 引用本批次显式 `StrID` 创建的 `bullet_pallete`；自动生成 `StrID` 的 palette 不能被同批次引用。

### 47. Intra-batch references for `objects.set_properties`

Status: decided

Problem:

- `objects.set_properties` 支持批量修改。
- 现在 `bullet_pallete.StrID` 可以被修改，并且 `bullet` / `bell.ReferenceBulletPalleteStrId` 也可以被修改。
- 同一个 set batch 可能同时：
  - 把 palette `A1` 改名为 `B1`
  - 把某个 bullet/bell 绑定到 `B1`
- 如果 reference lookup 只看 batch 开始前的 palette 列表，`B1` 找不到。
- 如果 reference lookup 同时看 batch 里的 rename，需要定义冲突和执行顺序。

Recommended answer:

- `objects.set_properties` 支持同一请求内引用本请求中改名后的 `bullet_pallete.StrID`。
- 校验阶段先计算 palette `StrID` rename plan：
  - 禁止新 `StrID` 为空。
  - 禁止两个 palette 改成同一个 `StrID`。
  - 禁止改成未被同批次释放的既有 `StrID`。
- 再用“应用后的 palette id 集合”校验所有 `ReferenceBulletPalleteStrId`。
- 应用阶段先执行 palette `StrID` 特殊修改，再执行其他属性修改。
- 整个请求仍然 all-or-nothing，一个 undo step。

Question:

`objects.set_properties` 是否允许同一批次内引用本批次刚改名后的 `bullet_pallete.StrID`，并通过先计算 rename plan 再校验引用来实现？

Answer:

同意。`objects.set_properties` 允许同一批次内引用本批次刚改名后的 `bullet_pallete.StrID`，并通过先计算 rename plan 再校验引用来实现。

### 48. Same-item palette clear plus custom projectile writes

Status: decided

Problem:

- 前面已决定：设置了 palette 的 `bullet` / `bell`，custom projectile 参数按属性浏览器只读规则不可写。
- 但同一个 `objects.set_properties` item 可能同时包含：
  - `ReferenceBulletPalleteStrId: null`
  - `Speed` / `TargetValue` / `ShooterValue` 等 custom projectile 参数
- 如果校验只看“修改前状态”，这些 custom 参数会因为当前 palette 非空而被拒绝。
- 如果校验看“应用后状态”，这类从 palette 模式切到 custom 模式的操作可以一次完成。

Recommended answer:

- `objects.set_properties` 的属性可写性校验对同一个对象使用“应用后状态”。
- 如果同一 item 把 `ReferenceBulletPalleteStrId` 设为 `null`，则允许同时写 custom projectile 参数。
- 如果同一 item 把 `ReferenceBulletPalleteStrId` 设为非 null，则禁止同时写 custom projectile 参数。
- 如果对象当前已有 palette，且本 item 没有清除 palette，则 custom projectile 参数仍不可写。
- 应用顺序：先处理 `ReferenceBulletPalleteStrId`，再处理 custom projectile 参数。

Question:

同一个 `objects.set_properties` item 是否允许先清除 `ReferenceBulletPalleteStrId`，再同时写 custom projectile 参数？

Answer:

同意。同一个 `objects.set_properties` item 允许先清除 `ReferenceBulletPalleteStrId`，再同时写 custom projectile 参数。

### 49. Batch delete referenced `bullet_pallete`

Status: decided

Problem:

- 第 40 条已决定：删除仍被 `bell` / `bullet` 引用的 `bullet_pallete` 时直接报错。
- `objects.delete` 支持批量删除。
- 同一个 delete batch 可能同时删除：
  - 某个 `bullet_pallete`
  - 所有引用它的 `bullet` / `bell`
- 如果引用检查只看删除前状态，这个 batch 会被拒绝。
- 如果引用检查看“删除后状态”，只要没有保留下来的对象引用被删 palette，就可以安全执行。

Recommended answer:

- `objects.delete` 删除 `bullet_pallete` 时，引用检查基于“删除后状态”。
- 如果所有引用该 palette 的 `bullet` / `bell` 也在同一个 delete batch 中被删除，则允许删除。
- 如果仍有任何未删除的 `bullet` / `bell` 引用该 palette，则校验失败。
- 错误结果返回未删除但仍引用该 palette 的对象 handles。
- 应用阶段先删除 projectile，再删除 palette，仍作为一个 undo step。

Question:

`objects.delete` 是否允许同一批次删除 palette 及所有引用它的 bullet/bell，只在删除后仍有引用时才报错？

Answer:

同意。`objects.delete` 允许同一批次删除 palette 及所有引用它的 bullet/bell，只在删除后仍有引用时才报错。

### 50. Delete behavior for displayable helper objects

Status: decided

Problem:

- 第 30 条已决定：`objects.list/delete/set_properties` 可寻址 displayable 辅助对象。
- 代码中的辅助对象删除行为并不一致：
  - `HoldEnd`：`OngekiFumen.RemoveObject` 会从 `Hold` 上 detach，undo 可通过 `CacheRecoveryHoldObjectID` 恢复。
  - `ConnectableChildObjectBase`：UI 删除时记录 child 在 `ReferenceStartObject.Children` 中的索引，undo 时插回。
  - `LaneCurvePathControlObject`：UI 删除时记录 control 在 `PathControls` 中的索引，undo 时插回。
  - `LaneBlockAreaEndIndicator`：`OngekiFumen.RemoveObject` 没有删除分支；它是 `LaneBlockArea` 的内置结束点，不应单独删除。
- 如果 MCP 直接对所有 displayable helper 调 `Fumen.RemoveObject`，`LaneBlockAreaEndIndicator` 会变成 no-op，结果语义不可靠。

Recommended answer:

- `objects.delete` 对 displayable helper 使用显式白名单语义：
  - 允许删除 `HoldEnd`，表示移除 hold 的结束点。
  - 允许删除 `ConnectableChildObjectBase`，复用 UI 删除语义并记录原索引用于 undo。
  - 允许删除 `LaneCurvePathControlObject`，复用 UI 删除语义并记录原索引用于 undo。
  - 禁止单独删除 `LaneBlockAreaEndIndicator`；如需调整 lane block 结束时间，用 `objects.set_properties` 修改其 `TGrid`，如需删除则只能连带 `LaneBlockArea` 本体一起删除。
- 删除 `ConnectableStartObject` 仍遵守第 9 条：有 children 时必须 `cascade = true`。
- 禁止删除的 helper 返回 validation error，不做 no-op success。

Question:

`objects.delete` 是否只允许删除上述可安全 detach 的辅助对象，并禁止单独删除 `LaneBlockAreaEndIndicator`？

Answer:

同意，但 `LaneBlockAreaEndIndicator` 不能单独删除，只能连带 `LaneBlockArea` 本体一起删除。

### 51. `LaneBlockAreaEndIndicator.TGrid` validation

Status: decided

Problem:

- `LaneBlockAreaEndIndicator.TGrid` setter 会把小于等于 `LaneBlockArea.TGrid` 的值静默夹到 start `TGrid`。
- MCP 写入如果接受非法 end `TGrid` 并让 setter 静默修正，返回结果可能和客户端请求值不同。
- 前面第 34 条已决定 `lane_block_area` 创建时校验 `EndTGrid >= TGrid`。

Recommended answer:

- `objects.set_properties` 修改 `LaneBlockAreaEndIndicator.TGrid` 时显式校验：
  - 新 `TGrid` 必须 `>= RefLaneBlockArea.TGrid`。
  - 不依赖 setter 静默夹值。
- 如果同一个 set batch 也修改 `LaneBlockArea.TGrid`，则用“应用后 start/end 状态”校验。
- 校验失败时 all-or-nothing，不修改 fumen。

Question:

修改 `LaneBlockAreaEndIndicator.TGrid` 时是否显式校验 end 不早于对应 `LaneBlockArea.TGrid`，而不是依赖 setter 静默夹值？

Answer:

同意。修改 `LaneBlockAreaEndIndicator.TGrid` 时显式校验 end 不早于对应 `LaneBlockArea.TGrid`，不依赖 setter 静默夹值。

### 52. `HoldEnd.TGrid` validation

Status: decided

Problem:

- `Hold.EndTGrid` 直接来自 `HoldEnd.TGrid`，如果 `HoldEnd` 不存在则退回 `Hold.TGrid`。
- `HoldEnd` 没有像 `LaneBlockAreaEndIndicator` 那样的 setter 夹值逻辑。
- 如果 MCP 允许把 `HoldEnd.TGrid` 写到早于 `Hold.TGrid`，会生成反向/非法 hold。
- 如果同一 set batch 同时修改 `Hold.TGrid` 和 `HoldEnd.TGrid`，需要用应用后的两端状态校验。

Recommended answer:

- `objects.set_properties` 修改 `HoldEnd.TGrid` 或 `Hold.TGrid` 时，如果 hold 有 `HoldEnd`，显式校验：
  - `HoldEnd.TGrid >= Hold.TGrid`。
- 如果同一 batch 同时修改 hold start/end，则按应用后状态校验。
- 校验失败时 all-or-nothing，不修改 fumen。
- `objects.add` 创建 `hold` 时仍按第 18 条，`EndTGrid` 可选；如果传入则同样校验 `EndTGrid >= TGrid`。

Question:

修改 `Hold` / `HoldEnd` 时间时是否显式校验 `HoldEnd.TGrid >= Hold.TGrid`，并对同批次修改使用应用后状态？

Answer:

同意。修改 `Hold` / `HoldEnd` 时间时显式校验 `HoldEnd.TGrid >= Hold.TGrid`，并对同批次修改使用应用后状态。

### 53. Recreating `HoldEnd` after deletion

Status: decided

Problem:

- 第 18 条已决定：`objects.add` 创建 `hold` 时可选 `EndTGrid`，同一事务创建 `HoldEnd`；不开放独立 `hold_end` 创建。
- 第 50 条已决定：允许删除 `HoldEnd`，表示移除 hold 的结束点。
- UI 中 `HoldOperationViewModel` 在 hold 没有 `HoldEnd` 时允许通过操作面板创建一个新的 `HoldEnd`。
- 如果 MCP 删除了 `HoldEnd`，但没有恢复方式，用户只能删除并重建整个 hold。

Recommended answer:

- 不开放 `objects.add` 独立创建 `hold_end`，保持第 18 条。
- 在 `objects.set_properties` 中为 `hold` 支持 pseudo property `EndTGrid`：
  - 如果 hold 没有 `HoldEnd` 且传入非 null `EndTGrid`，创建新的 `HoldEnd` 并绑定到该 hold。
  - 如果 hold 已有 `HoldEnd` 且传入非 null `EndTGrid`，修改现有 `HoldEnd.TGrid`。
  - 如果传入 `EndTGrid: null`，等价于删除/detach `HoldEnd`。
- 对 `EndTGrid` 使用第 52 条校验：`EndTGrid >= Hold.TGrid`。
- `objects.list` 对 hold 输出 `EndTGrid` 和 `HoldEndObjectId`，方便客户端选择直接改 `HoldEnd` 或通过 hold pseudo field 修改。

Question:

是否在 `objects.set_properties` 中给 `hold` 支持 pseudo 属性 `EndTGrid`，用于创建、修改或清除 `HoldEnd`，而仍不允许独立 `objects.add hold_end`？

Answer:

同意。在 `objects.set_properties` 中给 `hold` 支持 pseudo 属性 `EndTGrid`，用于创建、修改或清除 `HoldEnd`，而仍不允许独立 `objects.add hold_end`。

### 54. `ConnectableChildObjectBase.TGrid` ordering

Status: decided

Problem:

- lane/beam child 的顺序由 `ConnectableStartObject.Children` 列表和 `PrevObject` / `NextObject` 链维护。
- 直接修改 `ConnectableChildObjectBase.TGrid` 不会自动从列表中移除并按新时间重插。
- 如果 MCP 允许把 child 的 `TGrid` 改到前一个点之前或后一个点之后，会让列表顺序、`PrevObject` 链和时间顺序不一致。
- 自动重排虽然可行，但会隐式改变 lane topology，影响曲线控制点和 docked objects。

Recommended answer:

- 第一版 `objects.set_properties` 修改 `ConnectableChildObjectBase.TGrid` 时不自动重排。
- 不要求 child 的 `TGrid` 保持在当前相邻对象范围内。
- 允许把 child 放到它前一个物件 `TGrid` 之前。
- 这种情况下 connectable path 的 `IsVaildPath` / `IsPathVaild()` 可能变为 `false`，但这是合法编辑器状态，不作为 MCP 校验错误。
- `PrevObject` / `NextObject` 链保持不变；MCP 不隐式改变 lane topology。

Question:

修改 connectable child 的 `TGrid` 时是否禁止跨越前后相邻点，第一版不做自动重排？

Answer:

不同意。connectable child 可以放置到它前一个物件 `TGrid` 之前，即使这样它们的 `IsVaildPath` 是 `false`；MCP 不应因此拒绝。

### 55. Invalid connectable path reporting

Status: decided

Problem:

- 第 54 条已决定：connectable child 可以移动到使 path invalid 的位置。
- 对 MCP 来说，这不是 validation error，但客户端仍可能需要知道该操作让 lane/beam 进入 invalid path 状态。
- 代码里有 `ConnectableChildObjectBase.IsVaildPath` 和 `ConnectableStartObject.IsPathVaild()` 可用于判断。

Recommended answer:

- `objects.set_properties` 不因 connectable path invalid 失败。
- 成功结果中如果本次修改导致相关 `ConnectableStartObject.IsPathVaild()` 为 `false`，返回 non-fatal warning。
- `objects.list` 对 connectable start/child 输出 `IsPathVaild` / `IsVaildPath` 摘要字段，方便客户端主动检查。
- warning 不影响 `affectedCount`，也不触发回滚。

Question:

当 MCP 写入导致 connectable path invalid 时，是否作为成功结果里的 warning 返回，而不是校验失败？

Answer:

同意。当 MCP 写入导致 connectable path invalid 时，作为成功结果里的 warning 返回，而不是校验失败。

### 56. Deleting lanes referenced by dockable objects

Status: decided

Problem:

- `Tap` / `Hold` 通过 `ReferenceLaneStart` 引用 lane start。
- `OngekiFumen.RemoveObject` 删除 `ConnectableStartObject` 时不会自动清理或迁移 `Tap` / `Hold` 的 lane 引用。
- 现有 UI 的 lane 类型转换/替换操作会显式把受影响 dockable objects 迁移到新 lane。
- 删除 lane 后，引用它的 `Tap` / `Hold` 应进入滞空状态，而不是阻止删除。

Recommended answer:

- `objects.delete` 删除 `ConnectableStartObject` 时，仍遵守第 9 条 children/cascade 检查。
- 允许删除仍被 `Tap` / `Hold` 引用的 lane start。
- 删除时把未删除的 `Tap` / `Hold.ReferenceLaneStart` 设置为 `null`，使它们默认滞空。
- 不自动迁移到其他 lane。
- 结果中返回 warning，列出被置为滞空的 `Tap` / `Hold` handles。
- undo 时恢复这些 `Tap` / `Hold` 原本的 `ReferenceLaneStart`。

Question:

删除 lane start 时，如果仍有未删除的 tap/hold 引用它，是否直接报错而不是自动清引用或迁移？

Answer:

不同意。可以直接删除 lane start，引用它的 `Tap` / `Hold` 默认置为滞空。

### 57. Lane delete floating warning

Status: decided

Problem:

- 第 56 条已决定：删除 lane start 时，引用它的 `Tap` / `Hold` 默认置为滞空。
- 这是隐式影响除被删除 lane 以外的对象，客户端需要知道哪些对象被改成滞空。
- 前面第 55 条已经采用 warning 表达 non-fatal 状态变化。

Recommended answer:

- `objects.delete` 删除 lane start 并使 `Tap` / `Hold` 滞空时，操作成功。
- 成功结果返回 non-fatal warning：
  - warning code: `dockable_objects_floated`
  - payload 包含被置为滞空的 `Tap` / `Hold` handles。
- warning 不影响 `affectedCount`，但这些被置空引用的对象应计入 `affectedHandles`，因为它们实际被修改。
- 授权预览也应提示会让 N 个 tap/hold 滞空。

Question:

删除 lane start 导致 Tap/Hold 滞空时，是否在成功结果和授权预览中明确列出这些受影响对象？

Answer:

同意。删除 lane start 导致 Tap/Hold 滞空时，在成功结果和授权预览中明确列出这些受影响对象。

### 58. Lane reference property codec

Status: decided

Problem:

- `Tap` / `Hold` 的 `ReferenceLaneStart` 是 CLR 对象引用，MCP 客户端不能直接构造。
- UI 暴露了 `ReferenceLaneStrIdManualSet`，但这是触发式属性：set 后立即通知并重置，不适合作为稳定 API 字段。
- `ReferenceLaneStrId` 是只读摘要，来自 `ReferenceLaneStart.RecordId`。
- 前面第 17 条已决定 add 时使用 `ReferenceLaneRecordId` 和可选 `SnapXToLane`。

Recommended answer:

- 在 MCP schema 中统一使用 pseudo property `ReferenceLaneRecordId`。
- 对 `tap` / `hold`：
  - `ReferenceLaneRecordId: 123`：绑定到当前 fumen 中 `RecordId` 匹配的 lane start。
  - `ReferenceLaneRecordId: null`：清除 lane 引用，使对象滞空。
- 不直接暴露 `ReferenceLaneStart` 或 `ReferenceLaneStrIdManualSet` 为可写属性。
- `objects.list` 输出 `ReferenceLaneRecordId`。
- 如果设置非 null lane 且同 item 传 `SnapXToLane: true`，则按 lane 在对象 `TGrid` 的位置更新 `XGrid`；否则只改引用，不强制改 `XGrid`。

Question:

Tap/Hold 的 lane 引用在 MCP 中是否统一用 pseudo 属性 `ReferenceLaneRecordId` 表达，支持 number 绑定和 `null` 滞空？

Answer:

同意。Tap/Hold 的 lane 引用在 MCP 中统一用 pseudo 属性 `ReferenceLaneRecordId` 表达，支持 number 绑定和 `null` 滞空。

### 59. Intra-batch lane references for `objects.add`

Status: decided

Problem:

- `objects.add` 支持批量创建 lane start、tap、hold。
- `ConnectableObjectList.Add` 会在 lane start `RecordId < 0` 时自动生成新的 `RecordId`。
- 如果同一 batch 中新建 lane start 后立即创建 tap/hold 引用它，客户端必须能稳定表达目标 lane。
- 自动生成的 `RecordId` 在校验阶段不可由客户端预知。

Recommended answer:

- `objects.add` 允许同一请求内的 tap/hold 通过 `ReferenceLaneRecordId` 引用本请求中将创建的 lane start。
- 仅当该 lane start 在 add fields 中显式提供非负 `RecordId` 时允许同批次引用。
- 同 batch 显式 `RecordId` 不能重复，也不能与当前 fumen 已存在 lane `RecordId` 冲突。
- 未显式提供 `RecordId` 的 lane start 仍可创建，由 `ConnectableObjectList` 自动生成 id，但不能被同 batch 的 tap/hold 引用。
- 应用阶段先创建 lane start，再创建引用它的 tap/hold，仍作为一个 undo step。

Question:

`objects.add` 是否允许同一批次内 tap/hold 引用本批次显式 `RecordId` 创建的 lane start，但不允许引用自动生成 `RecordId` 的 lane？

Answer:

同意。`objects.add` 允许同一批次内 tap/hold 引用本批次显式 `RecordId` 创建的 lane start，但不允许引用自动生成 `RecordId` 的 lane。

### 60. Lane start `RecordId` add field

Status: decided

Problem:

- 第 59 条要求同批次引用新建 lane start 时必须显式 `RecordId`。
- 目前第 33 条 lane starts add fields 只列了 `TGrid` / `XGrid` / `Tag` / `IsTransparent` 等字段，没有明确 `RecordId`。
- `ConnectableObjectList.Add` 会为 `RecordId < 0` 的 start 自动生成 id。
- 如果允许客户端传任意 `RecordId`，必须防止重复和负值语义混乱。

Recommended answer:

- lane start objectTypes 的 add fields 增加 optional `RecordId`：
  - `lane_left_start`
  - `lane_center_start`
  - `lane_right_start`
  - `lane_colorful_start`
  - `wall_left_start`
  - `wall_right_start`
- 如果传 `RecordId`，必须是 `>= 0` integer。
- 显式 `RecordId` 不能与当前 fumen 已存在 lane start 冲突，也不能在同一 add batch 内重复。
- 不传 `RecordId` 时，创建对象保持默认负值，由 `ConnectableObjectList.Add` 自动分配。
- `objects.list_supported_types` 对 lane starts 标记 `RecordId` 为 optional add-only field；是否允许后续 set 另行决策。

Question:

lane start 创建时是否允许可选传入非负 `RecordId`，不传则自动生成，并禁止任何冲突？

Answer:

同意。lane start 创建时允许可选传入非负 `RecordId`，不传则自动生成，并禁止任何冲突。

### 61. Lane start `RecordId` mutation

Status: decided

Problem:

- `ConnectableObjectBase.RecordId` 在属性浏览器中标记为 `[ObjectPropertyBrowserReadOnly]`。
- `RecordId` 是 lane start、children、以及 Tap/Hold lane 引用的关联键。
- 修改已有 lane start `RecordId` 会影响：
  - child `RecordId`
  - `Tap` / `Hold.ReferenceLaneStart`
  - `ReferenceLaneRecordId` 查询结果
- 虽然可以实现特殊 rename plan，但第一版已经有 lane 引用绑定/清除和 add-time `RecordId` 指定能力。

Recommended answer:

- 第一版不允许通过 `objects.set_properties` 修改 lane start `RecordId`。
- `RecordId` 仅作为 lane start `objects.add` 的 optional add-only field。
- 如需改变已有 lane 的 id，第一版要求创建新 lane、迁移/清理引用、删除旧 lane。
- `objects.list_supported_types` 将 `RecordId` 标记为 add-only/read-only-after-create。

Question:

lane start 的 `RecordId` 第一版是否只允许创建时指定，禁止后续通过 `objects.set_properties` 修改？

Answer:

同意。lane start 的 `RecordId` 第一版只允许创建时指定，禁止后续通过 `objects.set_properties` 修改。

### 62. `SnapXToLane` semantics for Tap/Hold

Status: open

Problem:

- 第 17 条和第 58 条都引入了 `SnapXToLane`。
- `Tap` / `Hold` 绑定 `ReferenceLaneRecordId` 时，如果不改变 `XGrid`，对象可能绑定 lane 但视觉上不在 lane 上。
- `Hold.ReferenceLaneStart` setter 会触发 `HoldEnd.RedockXGrid()`，但是否调整 hold 起点 `XGrid` 需要 MCP 明确定义。
- `Tap` 没有 end object；`Hold` 可能有 `HoldEnd`。

Recommended answer:

- `SnapXToLane` 是 add/set 中针对 `tap` / `hold` 的 optional boolean，默认 `false`。
- 当 `ReferenceLaneRecordId` 为非 null 且 `SnapXToLane = true`：
  - 对 `Tap`：用目标 lane 在 `Tap.TGrid` 的 `CalulateXGrid` 更新 `Tap.XGrid`。
  - 对 `Hold`：用目标 lane 在 `Hold.TGrid` 的 `CalulateXGrid` 更新 `Hold.XGrid`；如果存在 `HoldEnd`，也用目标 lane 在 `HoldEnd.TGrid` 的 `CalulateXGrid` 更新 `HoldEnd.XGrid`。
- 当 `ReferenceLaneRecordId = null` 时，忽略/禁止 `SnapXToLane = true`；推荐校验失败，避免“滞空但 snap”的矛盾请求。
- 如果 lane 在目标 `TGrid` 无法计算 `XGrid`，则校验失败，不静默保留旧 `XGrid`。

Question:

`SnapXToLane=true` 时是否同时把 Tap/Hold 起点和 HoldEnd 终点的 `XGrid` 吸附到目标 lane，并在无法计算时校验失败？
