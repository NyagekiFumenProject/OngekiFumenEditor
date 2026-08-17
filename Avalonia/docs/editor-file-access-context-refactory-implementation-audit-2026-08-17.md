# EditorFileAccessContext 重构实现审计（2026-08-17）

## 1. 审计范围与判定规则

本报告核对以下一手证据：

- `docs/editor_file_access_context_refactory_live_review_2026-08-12.html` 中 Q1-Q24 的最终决策；
- 当前 `src/` 生产代码与 `tests/` 测试代码；
- 相关提交 `859b6a85`、`8086ab48`、`68fb7b74`、`0667b2d9`、`966b6e2a`、`8eef9d3e`。

规范解释优先级如下：

1. 每题最后的“最终决策 Dn”；
2. 后续问题对早期结论的明确修订或否决；
3. 文档中的“实施状态”；
4. 推荐方案、备选方案、示例代码和 2026-08-12 历史基线只用于解释问题，不自动视为要求。

HTML 在第 288 行明确说明“未确认内容不会被写成最终结论”，第 311-315 行又明确说明历史基线不代表当前实现。本报告因此不会把讨论阶段的 `WorkspaceKind`、`AuthorizationRoot`、平台 recipe 等备选方案误判为缺失功能。

状态定义：

- **已实现**：最终行为和职责边界基本一致；
- **部分实现/有差异**：已有对应代码，但缺少最终决策中的关键行为，或采用了不同架构；
- **本阶段缺失**：D21 要求本阶段完成，但当前没有达到最终形态；
- **明确延期**：D21 已把该功能排到下一阶段，当前缺失不是本阶段回归，但仍是文档确认的待实现要求。

## 2. 结论摘要

### 2.1 已实现

- **D14**：5 字段 `EditorFileAccessContextSnapshot` 已作为最近记录数据载体，工程文件书签可空；无法生成完整书签时跳过最近记录写入。
- **D22**：运行时与恢复契约已删除 `ProjectFileLocator`，Core 加载入口只接受完整 `EditorFileAccessContext`。
- **D23**：最新项目格式为 `0.5.5`，不再持久化 `FumenFilePath`/`AudioFilePath`；`0.5.2`/`0.5.4` 作为冻结的旧读取契约保留并迁移到最新模型。
- **D24（现有“打开项目文件夹”入口）**：即使谱面和音频各只有一个候选，也必须由用户显式选择；外部文件可以通过 Storage Provider 补选；需要外置 AWB 时会补充绑定。
- **D20 的主要职责迁移**：最近记录恢复、快照读取、上下文构造和向 ViewModel 转交已经移到 Provider。
- **D1/D12 的核心部分**：Core 加载器直接消费角色文件能力；持久化模型已经成为纯数据对象；文档关闭会释放当前 `EditorContext` 及其文件能力。

### 2.2 文档已明确要求，但本阶段缺失或偏离

- **D13、D17、D18 未按最终形态实现**：没有 Desktop/Browser 两个宿主 Provider，没有不注册的抽象 `FumenVisualEditorProviderBase`，共享具体 Provider 仍在 Core 中自动注册两个接口。
- **D19 未实现并采用了相反职责分配**：`EditorContext` 没有 `ProjectName`/`LocationDescription`；最近记录写入位于 Provider，而不是文档确认的 ViewModel。
- **D12 所有权细节不完整**：根集合不拒绝重复/祖先后代重叠；角色属性替换会直接释放旧文件；ViewModel 替换整个 `EditorContext` 时不会释放旧实例。
- **D15 主体已实现，D16 有提示差异**：`CheckIsValid` 只验证书签能否恢复，正是 D14/D15 确认的书签有效性检查；外置 AWB 最近恢复会按 D16 干净失败并释放资源，但当前只显示通用错误，没有明确提示用户改用“打开项目文件夹”。

### 2.3 明确延期及其他待实现流程

- **D2-D7**：New/Setup 两分支、项目名/谱面名/格式、冲突检查、有效初始内容、独立 `#region` 回滚均未实现。
- **D9-D11**：复制/保留/取消、谱面/音频/AWB 复制与原子切换均未实现；这些项目被 D21 明确排到下一阶段。
- **D8**：Fast Open 首次完整保存、工程目标选择和原地转正未实现。D21 没有像 D2-D7、D9-D11 那样明确写出延期 D8，但完整流程依赖后续复制策略，因此应单独保留为已确认的待实现要求。
- 当前 `CanCreateNew == false` 与 D21 的阶段门控一致；D2-D7、D9-D11 应记录为“明确延期”，D8 应记录为“已确认但未明确分期的待实现要求”，都不能误判为已否决。

## 3. 关键发现与延期项

### 高：F1. D13/D17/D18 的平台 Provider 拆分没有落地，D21 要求的本阶段工作未完成

**文档位置与原意**

- D13：HTML 2474-2476、2518-2522，要求共享程序集不再注册具体 Provider；Desktop/Browser 各注册唯一平台 Provider；`IEditorProvider` 与 `IFumenVisualEditorProvider` 显式映射到同一实例。
- D17：HTML 2998-3004，用户明确否决“以后再拆”，要求本次就拆分并把平台交互下沉到平台 Provider。
- D18：HTML 3088-3094，要求 Core Provider 变成不注册的抽象 `FumenVisualEditorProviderBase`，平台 Provider 继承并覆盖交互。
- D21：HTML 3319-3324，只把 New/Setup 和 autoImport 延期；D17-D20 的拆分属于本阶段范围。

**当前代码证据**

- `src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/FumenVisualEditorProvider.cs:13-15`：共享程序集中的具体 `FumenVisualEditorProvider` 仍同时使用两个 `[RegisterSingleton<...>]` 自动注册，并且不是抽象基类。
- `src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/FumenVisualEditorProvider.ProjectIO.cs:19-25`：文件夹选择和 Storage Provider 交互仍属于同一个共享 Provider。
- `src/OngekiFumenEditor.Avalonia.Desktop/OngekiFumenEditorDesktopApp.cs:34-38` 与 `src/OngekiFumenEditor.Avalonia.Browser/OngekiFumenEditorBrowserApp.cs:30-38`：宿主只注册各自通用服务，没有注册 Desktop/Browser Fumen Provider。
- `tests/OngekiFumenEditor.Avalonia.Tests/Modules/FumenVisualEditor/FumenVisualEditorProviderTests.cs:13-24`：测试仍直接实例化共享 Provider，并断言 `TryNew` 回调 ViewModel；没有 D13 要求的两个宿主组合根测试，也没有双接口 `ReferenceEquals` 断言。
- `8086ab48` 的提交说明只声称“把打开编排移入 FumenVisualEditorProvider”，并明确保留 `IPersistedDocumentViewModel` forwarding adapters；Git 历史中从未出现 `FumenVisualEditorProviderBase`、`DefaultDesktopEditorProvider` 或 `DefaultBrowserEditorProvider`。

**判断**

**本阶段缺失，且与最终架构有实质差异。** 已实现的是“ViewModel -> 共享 Provider”的职责迁移，不是“共享抽象基类 + 两个平台 Provider”的拆分。

两个注册注解也只是两个服务描述，没有文档要求的显式别名工厂；当前仓库没有测试证明两个接口解析到同一对象。

文档自身在 D18 的实现描述（HTML 3088-3093）写了平台子类使用两个 `[RegisterSingleton<...>]`，这与 D13（HTML 2498-2500、2518-2522）要求“先注册具体单例，再把两个接口通过工厂映射到该实例，并禁止两个独立实现描述符”存在冲突。实施时应以 D13 的对象身份不变量为准，而不是照抄 D18 的双注解示例。

**风险**

- Desktop 的直接 `.nyagekiProj` 打开、Fast Open、本地路径能力与 Browser 的目录授权/导入能力仍无法在宿主边界独立演进。
- 实现 D2-D11 时很容易重新把平台分支塞回共享 Core Provider，继续扩大耦合。
- 双接口实例身份没有保障，未来 Provider 一旦持有状态，可能产生同一功能的两个服务级单例和不一致状态。

### 高：F2. D12 的上下文替换与根所有权规则未完整实现

**文档位置与原意**

- HTML 2308-2319：`EditorFileAccessContext` 是唯一运行时所有者；目录内角色文件只是借用别名，独立文件才单独拥有。
- HTML 2331-2336：上下文替换必须“先建后换”，成功后释放旧上下文；根集合必须拒绝重复和祖先/后代重叠。
- 最终 D12 位于 HTML 2340-2344。

**当前代码证据**

- `src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/Models/EditorFileAccessContext.cs:51-74`：最终释放会去重顶层根，并避免再次释放可由目录祖先拥有的角色文件，这部分符合目标。
- 同文件 `:81-91`：重复或重叠根只在 `Dispose()` 时被过滤，没有在构造或赋值时拒绝；`AdditionDirectories` 在 `:22` 仍是可任意替换的公开 `List`。
- 同文件 `:94-102`：任一角色属性被替换时都会直接 `Dispose()` 旧对象，没有判断旧对象是否只是目录树中的借用别名，也没有判断它是否仍被另一个角色引用。
- `src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/Models/EditorContext.cs:34-49`：替换 `FileAccessContext` 会释放旧文件上下文，这一层实现正确。
- `src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.cs:100-118`：替换整个 `EditorContext` 时只解除事件订阅并更新 UI，没有释放 `oldValue`。
- 同文件 `:206-224`：`LoadProjectAsync` 可直接把新上下文赋给已有 ViewModel；如果 ViewModel 已持有上下文，旧实例不会被释放。
- 同文件 `:438-449`：文档最终关闭时只释放当前上下文，因此已经被替换掉的旧上下文无法在关闭时补偿释放。
- `tests/OngekiFumenEditor.Avalonia.Tests/Modules/FumenVisualEditor/EditorProjectLoadOwnershipTests.cs:14-35` 只覆盖加载失败时根和子文件被释放；`tests/OngekiFumenEditor.Avalonia.Tests/Modules/FumenVisualEditor/EditorFileAccessContextSnapshotTests.cs:80-116` 只覆盖失败时独立文件释放。没有覆盖重叠根拒绝、角色替换借用语义或 ViewModel 上下文替换释放。

**判断**

**部分实现，存在三个与 D12 不同的所有权行为：**

1. 根集合没有被验证，只在销毁时容错过滤；
2. 角色 setter 把“借用引用”当成“独立所有权”立即释放；
3. ViewModel 替换整个 `EditorContext` 时泄漏旧上下文。

**风险**

- 未来 Fast Open 转正、另存为或资源导入若复用同一 ViewModel 并切换上下文，会泄漏目录/文件句柄及旧谱面的 SVG 资源。
- 替换一个来自项目目录树的角色文件可能提前释放目录仍持有的子对象；同一文件被多个角色引用时，替换其中一个角色可能使其他角色指向已释放对象。
- 重叠根会进入书签快照和运行逻辑，直到 Dispose 时才被静默归并，错误构造不会及早暴露。

### 低：F3. D15 的二态判定已实现，D16 缺少明确的重开引导

**文档位置与原意**

- D15：HTML 2828-2836，只把书签不可恢复（包括权限撤销后返回 `null`）定义为永久失效并持久化置灰；其他平台异常为临时失败。
- D16：HTML 2910-2917，首版不增加 AWB 书签；外置 AWB 工程从最近记录恢复应干净失败、释放资源，并提示改用“打开项目文件夹”。
- D14 实施归纳 HTML 2743-2748 明确写明 `CheckIsValid` 通过 `ToContextAsync` 恢复临时上下文后立即释放，因此它不是完整工程内容校验器。

**当前代码证据**

- `src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/Models/EditorFileAccessContextSnapshot.cs:92-99`：快照恢复不会设置 `AudioAwbFile`，符合 5 字段约束。
- `src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/FumenVisualEditorProvider.ProjectIO.cs:138-149`：永久失效 catch 只包围 `snapshot.ToContextAsync()`。
- 同文件 `:189-209`：`CheckIsValid` 只恢复并立即释放上下文，符合 D14 记录的实现形态；书签恢复的 `IOException`/`InvalidDataException` 会置灰，其他异常不置灰。
- `src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/Base/EditorProjectDataUtils.cs:52-77、119-140`：外置 AWB 未绑定时会在完整打开阶段报错，失败路径由加载器释放上下文。
- `src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/FumenVisualEditorProvider.ProjectIO.cs:167-173`：用户看到的是通用 “Unable to open the recent project” 错误，没有 D16 要求的“改用打开项目文件夹”明确引导。
- 当前测试没有覆盖 Provider 的书签失效分类或外置 AWB 最近恢复及提示。

**判断**

**D15 已实现；D16 的核心限制和资源释放已实现，用户提示有差异。** 文档本来就接受外置 AWB 最近恢复失败，也没有要求 `CheckIsValid` 预先完整解析工程。真正未对齐的是失败消息没有明确告诉用户通过“打开项目文件夹”恢复使用。

**风险**

- 外置 AWB 用户只能从通用异常文本推断原因，不容易知道文件夹打开仍然可用。
- 缺少集成测试时，未来可能破坏已经确认的失败释放或二态分类。

### 规划：F4. D2-D7 的 New/Setup 完全未实现，但这是 D21 明确延期而非本阶段回归

**文档位置与原意**

- D2-D4：HTML 893-940、1013-1017、1102-1141，要求现有谱面/空白新建两条互斥路径，交付前绑定可写 `FumenFile`，空白谱面创建在项目根，并由动态序列化器列表决定扩展名。
- D5-D7：HTML 1217-1256、1340-1401、1516-1573，要求不区分大小写的冲突拒绝、有效初始内容、反向最佳努力回滚及独立 `#region`、显著且必填的项目名称。
- D21：HTML 3319-3324，明确把 Setup、autoImport 和 `.nyagekiProj` 创建放到下一阶段，并要求完成前 `CanCreateNew == false`。

**当前代码证据**

- `src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/FumenVisualEditorProvider.cs:32-39`：`CanCreateNew` 为 `false`，`TryNew` 仍转发 ViewModel。
- `src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.cs:194-197`：`New()` 固定返回 `false`。
- `src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/ViewModels/Dialogs/EditorProjectSetupDialogViewModel.cs:107-118`：旧 Setup 只要求音频存在，没有项目目录、项目名称、谱面名称、保存格式、冲突检查或文件创建。
- `src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/Views/Dialogs/EditorProjectSetupDialogView.axaml:31-100`：界面仍是音频选择、BPM 和“可选谱面文件”，不是 D2-D7 的互斥表单。
- 仓库中该 Setup ViewModel 没有生产调用点，只有 AXAML smoke test 引用。
- `tests/OngekiFumenEditor.Avalonia.Tests/Modules/FumenVisualEditor/FumenVisualEditorInitializationTests.cs:170-179` 明确断言 Save As 不可用；`tests/OngekiFumenEditor.Avalonia.Tests/Modules/FumenVisualEditor/FumenVisualEditorProviderTests.cs:13-24` 仍测试 Provider 将 New 转发到 ViewModel。
- FumenVisualEditor 相关代码不存在 D6 要求的回滚 `#region`，也没有调用 `GetSerializerDescriptions()` 构建新建格式选项。

**判断**

**明确延期，尚未实现。** 当前 `CanCreateNew == false` 正确执行了 D21 的阶段门控；旧 Setup 文件的存在不代表 D2-D7 已落地。

**风险**

- 若只把 `CanCreateNew` 改为 `true`，会暴露一个与最终设计严重不符、且不能创建有效工程文件的旧对话框。
- 后续实现必须建立在完成 D13/D17/D18 平台拆分之后，否则会把新建平台逻辑继续放入共享 Provider/ViewModel。

### 规划：F5. D8 的 Fast Open 首次保存未实现；D9-D11 被 D21 明确延期

**文档位置与原意**

- D8：HTML 1678-1741，允许 `ProjectFile == null`，但用户触发保存时必须选择 `.nyagekiProj` 目标并在完整成功后原地转正。
- D9-D11：HTML 1838-1906、1992-2056、2146-2184，要求复制/保留/取消三选一，只复制谱面、主音频和必要 AWB；谱面副本在项目根，冲突要求明确改名；全部成功后才切换绑定。
- D21 3319-3324 明确将 D9-D11 的 autoImport 复制与 New/Setup 放到下一阶段；它没有逐项声明延期 D8。

**当前代码证据**

- `src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/Models/EditorFileAccessContext.cs:24-28` 与 `src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.cs:65`：运行时状态能够表达 `ProjectFile == null` 和 `IsNew == true`，这是已实现的基础。
- `src/OngekiFumenEditor.Avalonia/Utils/DocumentOpenHelper.cs:198-214`、`:258-273`：条件编译路径能构造只有谱面和音频的 `EditorContext`。
- `src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/Kernel/DefaultImpl/DefaultEditorDocumentManager.cs:121-123`：后台恢复快照在缺 ProjectFile 时会跳过，符合 D8 的“后台不得弹 Picker”。
- `src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.cs:234-237`：用户执行标准保存时若没有 ProjectFile，只返回 `false`，没有目标选择或转正流程。
- 同文件 `:263-266`：`SaveAs()` 固定返回 `false`。
- `src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/Commands/OgkrImpl/FastOpenFumen/FastOpenFumenCommandHandler.cs:9-10`：Fast Open handler 的注册被注释。
- `src/OngekiFumenEditor.Avalonia/Utils/DocumentOpenHelper.cs:60-90`、`:97-133`、`:141-164`：Fast Open 主逻辑受 `ENABLE_CROSS_PLATFORM_FAST_OPEN` 保护；Desktop/Browser 工程文件均未定义该常量（`src/OngekiFumenEditor.Avalonia.Desktop/OngekiFumenEditor.Avalonia.Desktop.csproj:28-31` 只定义 `NATIVE_AOT`；`src/OngekiFumenEditor.Avalonia.Browser/OngekiFumenEditor.Avalonia.Browser.csproj:14-20` 只定义 `AVALONIA_BROWSER`）。
- 当前 FumenVisualEditor 代码没有 D9-D11 的复制策略对话框、音频/AWB 导入、谱面改名或一次性绑定切换。

**判断**

**基础状态部分实现，完整用户工作流尚未实现。** D9-D11 是明确延期项；D8 是已经确认、但 D21 未明确归入延期清单的待实现要求。正常构建中 Fast Open 本身不可用；即使通过条件常量启用，首次保存仍只会失败。

**风险**

- 用户无法把 Fast Open 文档转成正式工程。
- 条件代码位于共享 `DocumentOpenHelper`，并使用本地路径猜测音频；若直接启用，会绕过 D1/D17 的平台 Provider 构造边界。
- 在实现原子切换前，不应只补一个文件选择器，否则容易产生 ProjectFile 已绑定但谱面/音频复制半完成的状态。

### 中：F6. D19 未实现，最近记录显示元数据和写入职责采用了文档相反的方案

**文档位置与原意**

- HTML 3168-3173：`EditorContext = FileAccessContext + ProjectName + LocationDescription`，Provider 构造显示元数据，ViewModel 保持最近记录写入。
- D20 3244-3250：恢复由 Provider 负责，但记录刷新应经 `StoreRecentProject` 去重；ViewModel 只消费 `EditorContext`。

**当前代码证据**

- `src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/Models/EditorContext.cs:19-28`：上下文包含 `ProjectData`、`FilePath`、`FileName` 和 `RecentRecordId`，没有 `ProjectName` 或 `LocationDescription`。
- `src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/FumenVisualEditorProvider.ProjectIO.cs:98-101`、`:232-245`：Provider 以局部参数保存最近记录。
- 同文件 `:255-268`：从最近记录打开后直接按 `RecordId` 调用 `UpdateRecent`，没有通过 `StoreRecentProject` 的身份去重路径。
- `src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.cs:200-224`：ViewModel 的框架 `Load` 方法只是回调 Provider；实际消费入口名为内部 `LoadProjectAsync(EditorContext, sourcePath)`。

**判断**

**D20 的恢复编排已实现；D19 的上下文形状和最近记录写入归属未实现。** 当前选择的是“Provider 同时恢复和写入最近记录，显示元数据留在调用局部变量”的替代架构。

保留 `IPersistedDocumentViewModel.Load()/Load(recordInfo)` 作为无业务逻辑 forwarding adapter 是受框架接口 `Dependencies/Gekimini.Avalonia/src/Gekimini.Avalonia/Framework/Documents/IPersistedDocumentViewModel.cs:11-15` 约束的合理差异，不应单独判为职责回退。

**风险**

- 未来平台 Provider 通过公共 `TryOpen(document, EditorContext)` 打开上下文时，`EditorContext` 本身不足以携带最近记录显示信息，容易漏写最近记录或重复增加额外参数。
- 文档与代码对最近记录写入所有者的描述相反，后续修改者可能在 ViewModel 和 Provider 两边各实现一份。
- 直接按旧 `RecordId` 更新目前合理，但不等于 D20 记录的“统一走身份去重”，应更新设计或实现其中一方。

### 中：F7. D1 的 Core 消费边界已实现，但“只由平台 Provider 构造”尚未实现

**文档位置与原意**

- HTML 720-723 明确否决把 `WorkspaceKind`/`AuthorizationRoot` 暴露给 Core。
- D1 位于 764-768：平台构造能力对象，Core 只按角色读写，平台差异在 `New/Load(EditorContext)` 前消化。

**当前代码证据**

- `src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/Base/EditorProjectDataUtils.cs:40-57`：Core 加载器只读取 `ProjectFile`、`FumenFile`、`AudioFile`，不解析平台拓扑或持久化路径。
- 同文件 `:79-95`：解析成功后把完整上下文转交给 `EditorContext`，符合能力消费方向。
- `src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/Models/EditorFileAccessContext.cs:16-46`：主文件角色有显式属性，`AdditionDirectories` 不承担主文件猜测。
- 但 `src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/FumenVisualEditorProvider.ProjectIO.cs:64-70` 仍在 Core 共享程序集构造上下文；`src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/ViewModels/Dialogs/EditorProjectSetupDialogViewModel.cs:63-64、91-92` 和条件 Fast Open `src/OngekiFumenEditor.Avalonia/Utils/DocumentOpenHelper.cs:198-211、258-270` 也在共享层直接构造上下文。
- D14 明确接受 Core 可见快照类型，因此 `EditorFileAccessContextSnapshot.ToContextAsync` 位于 Models 层是已知设计偏离，不能再按早期推荐 recipe 要求判错。

**判断**

**Core 的消费算法已实现，构造所有权和程序集边界部分实现。** 主要原因仍是 F1 的宿主 Provider 拆分缺失。

**风险**

- 平台文件选择、路径猜测和能力验证仍可从共享工具/ViewModel 绕过 Provider，导致不同入口形成不同的校验和所有权规则。

### 中：F8. D24 的显式绑定已实现，但“可写性验证”与端到端测试仍不足

**文档位置与原意**

- D24：HTML 3639-3649，冷打开必须显式确认谱面和音频，即使候选唯一也不得自动关联；完整验证项目、谱面、音频及必要 AWB 后才交给 Core。
- HTML 754-756 进一步要求平台在交付前尽可能验证预期读写能力。

**当前代码证据**

- `src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/FumenVisualEditorProvider.ProjectIO.cs:42-70`：选定工程文件后总会进入 `SelectProjectFilesAsync` 并构造显式角色上下文。
- 同文件 `:286-303`：绑定对话框始终显示，不因唯一候选而自动选择。
- `src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/ViewModels/Dialogs/ProjectFileBindingDialogViewModel.cs:94-95`：确认条件只检查两个选项非空。
- 同文件 `:153-183`：外部浏览文件会正确转移或释放所有权。
- `src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/FumenVisualEditorProvider.ProjectIO.cs:305-347`：ACB 外置 AWB 会先找明确同级文件，否则要求用户补选。
- `tests/OngekiFumenEditor.Avalonia.Tests/Modules/FumenVisualEditor/ProjectFileBindingDialogViewModelTests.cs:9-31` 覆盖“唯一候选仍需显式选择”；`:34-73` 覆盖外部浏览文件的释放/转移。
- 工程和谱面在 `src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/Base/EditorProjectDataUtils.cs:52-85` 中被实际读取，音频在 `src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.cs:211-219` 中解码；但没有交付前写入探测，也没有 D24 完整 UI -> Provider -> Core 的端到端测试。

**判断**

**主要行为已实现，验证强度低于文档。** 当前验证了可读、可解析和可解码；没有证据表明在交付前验证了 Project/Fumen 的可写能力。

**风险**

- 只读或失去写权限的文件可以成功打开，直到用户保存时才失败。
- 现有单元测试不能防止未来有人恢复“唯一候选自动绑定”或在 Provider 转交前遗漏某个角色验证。

### 低：F9. D14 快照主体已实现，但恢复测试只覆盖序列化和失败释放

**文档位置与原意**

- HTML 2740-2750：采用 5 字段运行时上下文快照，不采用 HostKind/RootId recipe；无法生成完整快照时不新增最近记录。

**当前代码证据**

- `src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/Models/EditorFileAccessContextSnapshot.cs:11-21`：字段为项目目录、附加目录列表、可空工程文件、谱面文件、音频文件书签，共五个字段。
- 同文件 `:50-117`：恢复失败会反向释放已取得的文件和目录。
- 同文件 `:119-150`：生成快照要求 ProjectDirectory/Fumen/Audio，ProjectFile 可空。
- `src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/FumenVisualEditorProvider.ProjectIO.cs:241-252`：书签或最近记录持久化失败只记录 warning，不阻断打开。
- `tests/OngekiFumenEditor.Avalonia.Tests/Modules/FumenVisualEditor/EditorFileAccessContextSnapshotTests.cs:11-76` 覆盖 JSON 往返、可空 ProjectFile、缺少必需书签和损坏数据；`:80-116` 覆盖 Core 加载失败释放。

**判断**

**已实现。** 测试缺少真实/模拟 `IStorageProvider` 书签恢复、部分恢复失败顺序、Provider 最近记录写入/去重和 D15 失效分类，因此 F3 的问题没有被捕获。

**风险**

- 快照类本身较稳定，但 Provider 集成回归可能在现有测试全绿时发生。

## 4. D1-D24 全量状态矩阵

| 决策 | 状态 | 结论 |
|---|---|---|
| D1 | 部分实现/有差异 | Core 已只消费角色文件能力；平台 Provider 构造边界因宿主拆分缺失而未完成。 |
| D2 | 明确延期 | Setup 两个互斥谱面分支未实现；旧对话框不是最终设计。 |
| D3 | 明确延期 | 空白谱面固定创建在 ProjectDirectory 根级未实现。 |
| D4 | 明确延期 | 名称主体 + 动态格式扩展名 + 最终名预览未实现。 |
| D5 | 明确延期 | OrdinalIgnoreCase 冲突拒绝、保留表单状态未实现。 |
| D6 | 明确延期 | 有效初始内容、反向回滚和独立 `#region` 未实现。 |
| D7 | 明确延期 | 显著独立项目名、`.nyagekiProj` 预览和冲突校验未实现。 |
| D8 | 部分实现/待实现 | 可表达 ProjectFile 为空，后台自动保存会跳过；用户保存选择目标和原地转正未实现。D21 未明确逐项延期 D8。 |
| D9 | 明确延期 | 复制/保留/取消布局选择未实现。 |
| D10 | 明确延期 | 谱面、主音频、必要 AWB 的最小复制集合未实现。 |
| D11 | 明确延期 | 谱面根级副本、沿用原名和冲突改名未实现。 |
| D12 | 部分实现/有差异 | 单一所有者和关闭释放已实现；重叠根拒绝、借用角色替换和旧 EditorContext 释放未实现。 |
| D13 | 本阶段缺失 | 没有按宿主唯一注册，也没有双接口显式别名及身份测试。 |
| D14 | 已实现 | 5 字段快照、双向转换和非致命最近记录写入已落地。 |
| D15 | 已实现 | 书签恢复的 IOException/InvalidDataException 会永久置灰；其他平台异常按临时失败处理。 |
| D16 | 主要实现/提示有差异 | 维持 5 字段、外置 AWB 最近恢复干净失败并释放资源；错误消息未明确引导用户改用文件夹打开。 |
| D17 | 部分实现/本阶段偏离 | 编排已移到 Provider，但未拆成 Desktop/Browser Provider。 |
| D18 | 本阶段缺失 | 没有 `FumenVisualEditorProviderBase` 或平台子类。 |
| D19 | 本阶段缺失/替代实现 | 上下文无 ProjectName/LocationDescription；最近记录写入归 Provider。 |
| D20 | 主要行为已实现/有差异 | Provider 负责恢复与构造；ViewModel 只留接口适配。刷新直接 UpdateRecent，不走 StoreRecentProject dedup。 |
| D21 | 阶段门控已实现 | `CanCreateNew=false`；D2-D7、D9-D11 的延期有效，D8 未明确分期；D13/D17-D20 仍未全部完成。 |
| D22 | 已实现 | locator 从运行时、恢复和 Core 加载入口删除。 |
| D23 | 已实现 | 0.5.5 无路径；0.5.2/0.5.4 冻结兼容并迁移。 |
| D24 | 已实现（文件夹入口）/验证有差异 | 显式绑定与 AWB 补选已实现；写能力验证和端到端测试不足。Desktop 单文件入口尚未实现，但文档只要求其未来复用该规则。 |

## 5. 已被后续修订或否决的旧方案

以下内容不是当前待实现要求：

- Q1 早期的 `WorkspaceKind`、每文件 `AuthorizationRoot` 和 Core 可见拓扑模型：HTML 720-723 已明确认定初步建议错误。
- “单一项目根是全部 I/O 的全局不变量”：HTML 759-760 已收窄为 Browser/便携项目规则；Desktop 可由 Provider 封装多来源能力。
- Q3 的“谱面文件夹”：HTML 1010-1017 已更正为 `ProjectDirectory`；Setup 在任意子目录新建谱面的旧设计也被收窄。
- Q9 推荐的“永远保留原位置”：用户改为复制/保留/取消三选一，见 1838-1906。
- Q14 推荐的平台版本化 recipe、`HostKind`、`RootId`：用户明确改选 5 字段共享快照，见 2740-2750。
- Q17 推荐的“暂缓 Provider 拆分”：用户明确否决，选择本次拆分，见 2998-3004。
- Q22 早期允许 locator 作为运行时/模型字段的方案：被 D22 全面删除。
- Q23 推荐保留 `FumenFilePath`/`AudioFilePath` 作为持久化引用：被 D23 明确否决。
- Q24 的唯一候选自动绑定、文件名/目录约定、GUID/哈希匹配：均被最终方案 A 否决。

早期段落 1003-1005、1866-1868、2019-2020、2322 中仍出现项目定位符或路径字段，是当时讨论上下文，已经被 D22-D23 推翻，不能据此要求恢复路径字段。

## 6. Git 历史与测试证据

- `859b6a85`：引入纯数据 `EditorProjectDataModel`、`EditorContext`、`EditorFileAccessContext` 和 5 字段快照；当时仍含后来被 D22 否决的 locator。
- `8086ab48`：把文件夹打开和最近记录编排从 ViewModel 移到共享 Provider；这是 D17/D20 的职责迁移部分，不是 D13/D18 的宿主拆分。
- `68fb7b74`：调用方统一迁移到 `EditorContext`，减少 ViewModel 代理属性。
- `0667b2d9`：删除 locator 和最新项目格式路径字段，加入显式绑定对话框，对应 D22-D24。
- `966b6e2a`：把 D22-D24 实施状态写回文档。
- `8eef9d3e`：冻结版本契约，`EditorProjectDataModelBase` 只保留 `Version`，旧字段回到旧 DTO；这强化了 D23 的兼容边界。

当前测试直接证明：

- 唯一候选仍需显式选择，并覆盖外部浏览文件的所有权转移；
- 快照 JSON、可空 ProjectFile、缺少必需书签及损坏载荷处理；
- Core 加载失败会释放根/文件；
- 0.5.2/0.5.4 可迁移，0.5.5 不含路径字段，基类只声明 Version。

当前测试没有证明：

- Desktop/Browser 组合根各只有一个 Provider，且两个接口为同一实例；
- `EditorContext` 替换会释放旧实例；
- 重叠根被拒绝、角色借用引用替换不会误释放；
- Provider 最近记录的完整恢复、失效分类、外置 AWB 行为；
- D2-D11 的 UI、冲突、回滚、复制和提交事务。

## 7. 建议对策顺序

1. **先补齐 D12 生命周期契约**：定义不可变或受控的根集合；构造时拒绝重复/重叠根；角色引用不直接释放目录树借用对象；ViewModel 替换 `EditorContext` 时释放旧实例，并增加对应测试。这是当前最直接的资源泄漏和误释放风险。
2. **完成本阶段欠账 D13/D17-D20**：建立不注册的共享基类和两个宿主 Provider；按 D13 使用具体单例加接口别名工厂，不采用两个独立实现描述符；用宿主组合根测试锁定“唯一 Provider + 双接口同实例”。同时明确采纳 D19 原设计，或正式修订文档接受“Provider 负责最近记录写入”的现状。
3. **补齐 D16 用户引导和集成测试**：外置 AWB 最近恢复失败时明确提示从文件夹重开，并覆盖失败释放及 D15 书签二态分类；不要把 `CheckIsValid` 擅自扩大成完整工程加载，除非先修订文档。
4. **再进入后续流程实现**：在平台 Provider 内实现 D2-D7 New/Setup、D8 Fast Open 转正和 D9-D11 复制事务；不要激活旧 Setup 或条件 Fast Open 作为临时终态。
5. **补 D24 集成覆盖**：至少覆盖唯一候选不自动绑定、只读目标、外置 AWB、取消/失败释放，以及 Folder Open 到 ViewModel 接管的完整链路。

## 8. 最终判断

当前仓库已经完成“纯数据模型 + 运行时上下文 + 5 字段最近快照 + 无路径 0.5.5 + 冷打开显式绑定”的主体改造，D22-D24 的文档实施声明基本可信。

但这还不是 live review 确认的完整本阶段终态：D13/D17/D18 的宿主 Provider 拆分没有发生，D19 采用了未写回文档的替代架构，D12 仍有可观察的生命周期缺口，D16 的错误提示也未完全对齐。D2-D7 与 D9-D11 属于 D21 明确延期的下一阶段需求；D8 则是未明确分期但仍未完成的已确认要求，三者都应继续保留在实施清单中，且不能与本阶段 Provider 拆分欠账混为一谈。
