# Editor Project Setup 实施计划与决策访谈（2026-08-17）

> 状态：已按 S1-S9 完成首版实现（2026-08-18）；Desktop 已开放人工测试，Browser 保持门控关闭
> 实施范围：菜单“新建项目”到 Setup、文件创建事务、编辑器接管、最近记录及回归测试
> 当前工程格式：`EditorProjectDataModel.VERSION = 0.5.5`
> 关键约束：`EditorProjectDataModelBase` 不增加任何字段，继续只声明 `Version`

## 1. 文档目的

本文把已经确认的 D2-D7、D12-D13、D17-D21，以及当前代码审计结论，整理为可以直接实施和验收的 Editor Project Setup 方案。

本文需要同时解决四个问题：

1. 明确“新建项目”从菜单触发到编辑器成功打开的完整调用链。
2. 明确项目目录、谱面、音频、AWB、工程描述文件的所有权和释放顺序。
3. 明确创建失败时只删除本轮新建内容，绝不覆盖或删除用户原文件。
4. 把仍需产品确认的事项按依赖顺序列出，并逐题确认，避免实现中临时猜测。

本文不是对旧 `EditorProjectSetupDialogViewModel` 的简单接线说明。旧实现只选择音频、可选谱面和 BPM，不能创建 `.nyagekiProj` 或谱面文件，也没有冲突检查、事务回滚和明确的所有权转移，因此应重构为新的 Setup 会话，而不是直接把 `CanCreateNew` 改为 `true`。

## 2. 设计和审计依据

- [Editor File Access Context Live Review](editor_file_access_context_refactory_live_review_2026-08-12.html)
- [Editor File Access Context 实现审计](editor-file-access-context-refactory-implementation-audit-2026-08-17.md)
- [Fumen Visual Editor Project Folder I/O 设计](fumen-visual-editor-project-folder-io-design.md)
- [Editor Project Manifest Redesign Review](editor-project-manifest-redesign-review.md)

本文继承以下已经确认的决定：

| 决策 | 本计划采用的约束 |
|---|---|
| D2 | Setup 有“使用现有谱面”和“创建新谱面”两条互斥路径，两条路径在交付编辑器前都必须得到明确且可写的 `FumenFile`。 |
| D3 | 创建新谱面时，目标固定为 `ProjectDirectory` 根级，不再选择谱面子目录。 |
| D4 | 用户填写不带扩展名的谱面名称主体，保存格式是扩展名的唯一来源，UI 实时显示最终文件名。 |
| D5 | 最终谱面名称冲突时拒绝创建，保留表单内容，不覆盖、不自动编号、不自动切换到现有谱面模式。 |
| D6 | 新谱面在交付编辑器前必须包含可解析的有效内容；失败时按反向顺序最佳努力删除本轮新建文件。 |
| D7 | Setup 显著提供独立且必填的项目名称，Provider 固定追加 `.nyagekiProj`，冲突规则与谱面一致。 |
| D12 | `EditorFileAccessContext` 是运行时文件能力的唯一所有者；角色字段是借用别名；上下文替换后释放旧上下文。 |
| D13 | Desktop 和 Browser 各自只注册一个平台 Provider，`IEditorProvider` 与 `IFumenVisualEditorProvider` 必须解析为同一对象。 |
| D17-D18 | 平台交互下沉到平台 Provider；共享 Provider 改为不注册的抽象基类。 |
| D19-D20 | Provider 恢复或构造上下文；`EditorContext` 携带显示元数据；ViewModel 只消费完整上下文。 |
| D21 | Setup 完成并验证以前保持 `CanCreateNew == false`。 |
| D22-D24 | Core 不使用 locator 或持久化路径猜测主文件；Provider 在交付前完成显式文件绑定。 |

## 3. 不可破坏的版本契约

### 3.1 `EditorProjectDataModelBase`

`EditorProjectDataModelBase` 继续只承担“带版本号的持久化契约基类”职责：

~~~csharp
public abstract class EditorProjectDataModelBase : ObservableObject
{
    [JsonInclude]
    public abstract Version Version { get; }
}
~~~

Setup 实现不得向该类加入以下内容：

- 项目名称、目录名称或显示名称。
- `ProjectDirectory`、`ProjectFile`、`FumenFile`、`AudioFile` 或 `AudioAwbFile`。
- 本地路径、虚拟路径、locator、书签或平台标识。
- Setup 模式、用户选择、临时文件或回滚状态。
- 最近记录 ID、窗口状态或其他运行时数据。

### 3.2 最新版项目模型

当前最新版 `EditorProjectDataModel` 为 0.5.5。Setup 创建工程描述文件时使用最新版 serializer 写出 0.5.5，不修改 0.5.2/0.5.4 兼容 DTO。

本阶段建议只把以下已经存在的持久化信息写入最新版模型：

- 新生成的项目 `Id`。
- 选中音频的 `AudioDuration`。
- 默认编辑器设置。
- 默认 `RememberLastDisplayTime`。
- 默认的谱面调色板编辑数据集合。

项目名称当前首先作为：

- `.nyagekiProj` 文件名的名称主体；
- `EditorContext.ProjectName`；
- 最近记录的显示名称。

如果以后需要把“项目显示名称”独立持久化到工程文件，应只修改最新的 `EditorProjectDataModel` 并升级版本，不能把字段放到 `EditorProjectDataModelBase`。

## 4. 当前实现基线

| 区域 | 当前实现 | 对 Setup 的影响 |
|---|---|---|
| Provider | Core 中的 `FumenVisualEditorProvider` 同时注册两个接口。 | 不符合 D13/D18，必须先拆平台 Provider。 |
| 新建能力 | `CanCreateNew == false`，`FumenVisualEditorViewModel.New()` 固定返回 `false`。 | 当前菜单不显示该 Provider，符合阶段门控。 |
| 旧 Setup | 只选择音频、可选现有谱面和 BPM；`CreateAsync` 只检查音频。 | 不能作为最终创建流程，仅可复用少量展示或解析代码。 |
| Setup 调用点 | 生产代码没有调用旧 Setup ViewModel/View。 | 可以安全重构其契约，但需要补 UI 和集成测试。 |
| 工程加载 | `EditorProjectDataUtils.TryLoadFromContextAsync` 成功时消费上下文，失败时自行 Dispose。 | 创建失败后文件 wrapper 已被释放，妨碍事务删除，必须拆出非消费式加载。 |
| 文件写入 | `ISimpleFile.WriteAsync` 提供替换式写入，本地文件使用同目录临时文件原子提交。 | 可用于单文件提交，但多个文件仍需显式回滚。 |
| 文件删除 | `ISimpleFile.DeleteAsync` 已存在。 | 可按 D6 删除本轮创建文件，但必须在 Dispose wrapper 前调用。 |
| 上下文所有权 | Dispose 时会过滤重叠目录根；角色 setter 会 Dispose 旧引用。 | 不符合“角色是借用别名”，存在误释放风险。 |
| ViewModel 上下文替换 | `OnEditorContextChanged` 只解绑事件，不 Dispose `oldValue`。 | 打开第二个上下文时可能泄漏旧上下文。 |
| 菜单“打开” | `OpenFileCommandListHandler` 错误使用 `CanCreateNew` 过滤 Provider。 | 即使支持打开，`CanCreateNew == false` 时也不会显示；应独立修复。 |

## 5. 目标用户流程

~~~text
用户点击“新建”
    |
    v
当前宿主的 Platform Fumen Provider.TryNew(document)
    |
    +-- 选择或取得 ProjectDirectory
    |       |
    |       +-- 取消：关闭尚未初始化的 document，释放目录能力，返回 false
    |
    v
创建 EditorProjectSetupSession
    |
    v
显示 EditorProjectSetupDialog
    |
    +-- 选择音频
    +-- 选择“使用现有谱面”或“创建新谱面”
    +-- 输入项目名称
    +-- 新谱面模式下输入谱面名称、格式、基础 BPM
    +-- 实时校验最终文件名和目录冲突
    |
    +-- 取消：释放所有候选能力，不创建文件，返回 false
    |
    v
取得 EditorProjectSetupSelection
    |
    v
EditorProjectCreationTransaction.PrepareAsync
    |
    +-- 最终校验和并发冲突复查
    +-- 预先序列化新谱面和 0.5.5 工程到内存
    +-- 创建/复制需要的目标文件
    +-- 写入有效内容
    +-- 绑定 ACB 所需 AWB
    +-- 构造完整 EditorFileAccessContext
    +-- 从实际落盘文件重新解析校验
    |
    v
创建候选 EditorContext
    |
    v
FumenVisualEditorViewModel.TryAttachProjectAsync
    |
    +-- 失败：编辑器状态不变，事务反向删除本轮创建文件
    |
    +-- 成功：编辑器接管 EditorContext，事务 Commit
    |
    v
创建/刷新最近记录
    |
    +-- 快照或最近记录写入失败：记录警告，不撤销已成功打开的工程
    |
    v
返回 true
~~~

## 6. 分层职责

### 6.1 `FumenVisualEditorProviderBase`

共享程序集中的抽象基类，不注册为服务，负责：

- `SupportFileTypes`、`FileTypes` 和编辑器 ViewModel 创建。
- 最近记录快照读取、有效性分类和恢复后的通用处理。
- Provider 到 ViewModel 的上下文交付辅助逻辑。
- 与平台无关的错误到用户消息映射。
- 可以复用的 Setup 编排，但不能直接调用 Avalonia Storage Picker。

基类不负责：

- 选择项目目录、谱面、音频或 AWB。
- 从平台 picker 取得文件 capability；新建工程的“项目外资源复制进项目根”规则由共享创建事务统一执行。
- 从本地路径猜测资源。
- 自动注册 `IEditorProvider`。

### 6.2 Desktop Provider

建议类型名：

`DefaultDesktopFumenVisualEditorProvider : FumenVisualEditorProviderBase`

负责：

- Desktop 文件夹和文件选择器。
- 把 `IStorageFolder` / `IStorageFile` 转成 `ISimpleDirectory` / `ISimpleFile`。
- Desktop 允许的外部文件能力和多目录上下文。
- 本地 ACB/AWB 解析和必要的 AWB 显式选择。
- Desktop 的 `TryNew`、文件夹打开和未来 Fast Open。

### 6.3 Browser Provider

建议类型名：

`DefaultBrowserFumenVisualEditorProvider : FumenVisualEditorProviderBase`

负责：

- Browser/OPFS 项目目录选择或授权。
- Browser 不支持的 ACB 情况提前阻止，而不是创建后在 Core 才失败。
- 必须导入到项目根的资源复制。
- Browser 的 `TryNew` 和打开流程。

### 6.4 Setup Session

`EditorProjectSetupSession` 是 Provider 与 ViewModel 之间的会话对象，负责：

- 持有 Provider 已取得的 `ProjectDirectory`。
- 通过注入 delegate 或平台服务发起音频、现有谱面、AWB 选择。
- 持有尚未转交的 `ISimpleFile` 能力。
- 关闭对话框时按是否 `TakeSelection()` 决定释放候选文件。
- 提供目录快照和平台文件名验证结果。

Setup ViewModel 不应直接调用静态 `FileDialogHelper` 或 `IoC.Get`。这样可以：

- 对 Desktop/Browser 使用同一表单。
- 在测试中注入确定性的 picker 结果。
- 精确验证取消和替换选择时的 Dispose 次数。
- 避免 ViewModel 重新承担 D17 已划给 Provider 的平台职责。

### 6.5 创建事务

`EditorProjectCreationTransaction` 负责从 Selection 到编辑器成功接管之前的全部临时状态：

- 记录本轮创建的文件，顺序与创建顺序一致。
- 持有尚未转交的目录、源文件和候选 `EditorContext`。
- 在 `Commit()` 前发生任何异常或取消时执行反向回滚。
- `Commit()` 后只释放不再需要的源能力，不删除目标文件。

创建事务比单纯的 `EditorProjectCreationService.CreateAsync(): Task<EditorContext>` 更合适，因为“编辑器是否成功接管”也是事务提交条件。若 service 在返回 `EditorContext` 时就提交，随后音频加载或 ViewModel 初始化失败，将无法按照 D6 删除刚创建的工程和谱面。

## 7. 建议的数据契约

### 7.1 谱面模式

~~~csharp
public enum SetupFumenMode
{
    Existing,
    CreateNew
}
~~~

### 7.2 可写格式选项

~~~csharp
public sealed record FumenFormatOption(
    string Description,
    string Extension);
~~~

格式列表必须来自：

~~~csharp
IFumenParserManager.GetSerializerDescriptions()
~~~

每个 serializer 声明多个扩展名时，应展开为多个可选择的 `FumenFormatOption`。扩展名保留 serializer 声明的完整值，例如未来的 `.chart.json` 不能被截成 `.json`。

### 7.3 Setup Selection

建议把旧 Setup 返回的 `EditorContext` 改成只描述用户选择的 Selection：

~~~csharp
public sealed class EditorProjectSetupSelection : IDisposable
{
    public required ISimpleDirectory ProjectDirectory { get; init; }
    public required string ProjectName { get; init; }
    public required SetupFumenMode FumenMode { get; init; }
    public required ISimpleFile AudioFile { get; init; }

    public ISimpleFile? ExistingFumenFile { get; init; }
    public string? NewFumenStem { get; init; }
    public string? NewFumenExtension { get; init; }
    public double BaseBpm { get; init; }

    public EditorProjectSetupSelection Take();
    public void Dispose();
}
~~~

根据 S2，最终实现需要增加以下事务输入字段，但不能放入持久化数据基类：

- `AudioAwbFile`：已明确选择的外置 AWB。
- `TargetFumenFileName`、`TargetAudioFileName`、`TargetAudioAwbFileName` 和 `TargetProjectFileName`：由校验器确定的最终名称快照。
- `FumenRequiresImport`、`AudioRequiresImport` 和 `AudioAwbRequiresImport`：根据 capability 是否属于 `ProjectDirectory` 计算的只读事务事实。

Setup 不向用户提供“保留外部资源”开关。项目目录外资源一律复制，项目目录内资源一律直接绑定。

### 7.4 EditorContext 显示元数据

为了对齐 D19，建议在 `EditorContext` 中加入运行时属性：

~~~csharp
public string ProjectName { get; init; } = string.Empty;
public string LocationDescription { get; init; } = string.Empty;
~~~

这两个字段：

- 不序列化到 `EditorProjectDataModelBase`。
- 由 Provider 在新建、文件夹打开或最近恢复时填写。
- 用于窗口标题和最近记录显示。
- 最近恢复时 `LocationDescription` 沿用原 `RecentRecordInfo`。

## 8. 所有权状态机

### 8.1 目录和文件所有权

~~~text
Platform Picker
    |
    | 返回 capability
    v
Provider / Setup Session
    |
    | TakeSelection()
    v
Creation Transaction
    |
    | 构造候选 EditorFileAccessContext
    v
Candidate EditorContext
    |
    | TryAttachProjectAsync 成功
    v
FumenVisualEditorViewModel
    |
    | 文档关闭或上下文替换
    v
Dispose EditorContext -> Dispose EditorFileAccessContext
~~~

每一次转交必须满足：

- 转交前，发送方仍负责 Dispose。
- 转交动作成功后，发送方立即清空自己的引用。
- 接收方失败时不能留下半接管状态。
- 每个 capability 最终恰好 Dispose 一次；允许 Dispose 幂等，但测试仍应断言不存在重复所有权。

### 8.2 `EditorFileAccessContext` 前置修复

Setup 接线前先完成 D12：

1. 根集合在构造或 builder 完成时校验，而不是仅在 Dispose 时过滤。
2. 拒绝同一目录引用重复出现。
3. 拒绝一个拥有根是另一个拥有根的祖先或后代。
4. `ProjectFile`、`FumenFile`、`AudioFile`、`AudioAwbFile` 只是角色别名，修改角色映射时不能直接 Dispose 旧引用。
5. 属于任一拥有目录树的角色文件由目录根释放。
6. 不属于拥有目录树的独立角色文件由上下文单独拥有并释放。
7. 已 Dispose 的上下文拒绝后续修改。

建议使用一次性构造或 builder，减少任意 setter 造成的中间非法状态：

~~~csharp
var fileAccessContext = EditorFileAccessContext.Create(
    projectDirectory,
    additionDirectories,
    projectFile,
    fumenFile,
    audioFile,
    audioAwbFile);
~~~

### 8.3 ViewModel 上下文替换

`FumenVisualEditorViewModel` 必须采用“先完整准备新状态，再一次替换”的方式：

1. 在局部变量中完成音频加载和所有可能失败的初始化。
2. 失败时释放局部音频播放器，旧 `AudioPlayer` 和旧 `EditorContext` 保持不变。
3. 成功时交换 `AudioPlayer` 和 `EditorContext`。
4. 解绑旧上下文事件后 Dispose 旧 `EditorContext`。
5. 交换完成后只执行不会使方法进入失败分支的通知；如仍可能抛出，必须有恢复旧状态的方案。

建议把当前 `LoadProjectAsync` 的所有权契约明确为：

~~~csharp
// true: ViewModel 已接管 context。
// false/exception: ViewModel 状态不变，调用方仍拥有 context。
internal Task<bool> TryAttachProjectAsync(
    EditorContext context,
    CancellationToken cancellationToken);
~~~

## 9. 非消费式加载和校验

当前 `TryLoadFromContextAsync` 在失败时 Dispose context，适合普通“打开”流程，但不适合创建事务。创建事务需要在解析失败后先调用 `DeleteAsync`，再 Dispose 文件能力。

建议拆为两层：

~~~csharp
public sealed class LoadedEditorProjectData : IDisposable
{
    public required EditorProjectDataModel ProjectData { get; init; }
    public required OngekiFumen Fumen { get; init; }

    public (EditorProjectDataModel ProjectData, OngekiFumen Fumen) Take();
    public void Dispose();
}

public static Task<LoadedEditorProjectData> LoadDataAsync(
    EditorFileAccessContext context,
    CancellationToken cancellationToken = default);
~~~

`LoadDataAsync`：

- 读取但不 Dispose `EditorFileAccessContext`。
- 加载 0.5.5 或迁移旧工程数据。
- 校验音频文件存在及 ACB/AWB 依赖。
- 按 `FumenFile.FileName` 取得 deserializer 并解析谱面。
- 返回的 `LoadedEditorProjectData` 暂时拥有解析谱面中的可释放 prefab。

原来的入口保留为兼容 wrapper：

~~~csharp
public static async Task<EditorContext> TryLoadFromContextAsync(
    EditorFileAccessContext context,
    CancellationToken cancellationToken = default)
{
    // 保留现有“成功转交、失败消费并释放”的公开语义。
}
~~~

创建事务调用 `LoadDataAsync`，只有实际文件重新读取和解析成功后才构造候选 `EditorContext`。

## 10. Setup ViewModel 详细设计

### 10.1 建议属性

目录和项目：

- `ProjectDirectoryDisplayName`：只读。
- `ProjectName`：必填名称主体，不含 `.nyagekiProj`。
- `ProjectFileNamePreview`：`ProjectName + ".nyagekiProj"`。

谱面模式：

- `FumenMode`：`Existing` 或 `CreateNew`。
- `IsExistingFumenMode`。
- `IsCreateNewFumenMode`。

现有谱面：

- `ExistingFumenDisplayName`：只读。
- `SelectExistingFumenCommand`。
- `ClearExistingFumenCommand`。

新谱面：

- `NewFumenStem`：不含扩展名。
- `FumenFormatOptions`。
- `SelectedFumenFormat`。
- `FumenFileNamePreview`。
- `BaseBpm`。

音频：

- `AudioFileDisplayName`：只读。
- `AudioDuration`。
- `SelectAudioFileCommand`。
- `ClearAudioFileCommand`。
- `AudioAwbDisplayName`：只在外置 AWB 需要时显示。

表单状态：

- `IsBusy`。
- `ValidationMessage`：当前最高优先级错误。
- `HasValidationErrors`。
- `CanCreate`。
- `CreateCommand`。
- `CancelCommand`。

### 10.2 模式切换

切换模式不能立即 Dispose 另一模式已选文件，建议在 Setup 会话结束前保留用户输入，便于用户切回：

- 从 Existing 切到 CreateNew 时，隐藏现有谱面区，但暂时保留已选 capability。
- 从 CreateNew 切回 Existing 时，恢复此前的现有谱面选择。
- 最终 `TakeSelection()` 只转交当前模式使用的能力。
- 对话框关闭时，Session Dispose 未转交的另一分支 capability。

如果希望模式切换立即释放并清空另一分支，也可以实现，但会降低试填体验。该行为属于较低优先级产品决策，待目录和资源拓扑确定后再确认。

### 10.3 Picker 注入

ViewModel 依赖的接口建议为：

~~~csharp
public interface IEditorProjectSetupFilePicker
{
    Task<ISimpleFile?> PickAudioAsync(CancellationToken cancellationToken);
    Task<ISimpleFile?> PickExistingFumenAsync(CancellationToken cancellationToken);
    Task<ISimpleFile?> PickExternalAwbAsync(
        string expectedFileName,
        CancellationToken cancellationToken);
}
~~~

Desktop/Browser Provider 创建平台实现或传入 delegate。ViewModel 不读取 `LocalPath` 来决定业务逻辑，`LocalPath` 只用于可用时的显示或平台解码能力判断。

### 10.4 UI 布局

建议 Setup 窗口按从目标到内容的顺序组织：

1. 顶部显示项目目录，使用文件夹图标按钮重新选择。
2. 项目名称输入框，紧邻只读的工程文件名预览。
3. 使用分段模式或单选按钮切换“现有谱面/新建谱面”。
4. Existing 模式显示谱面选择行。
5. CreateNew 模式显示谱面名称、格式、最终预览和必填的初始 BPM。
6. 音频选择行和音频时长。
7. 需要外置 AWB 时显示 AWB 选择状态。
8. 底部显示当前验证错误，以及取消/创建命令。

创建过程中：

- `IsBusy == true` 时禁用目录、名称、模式、文件选择和创建按钮。
- 允许取消后台事务时，取消按钮改为触发 `CancellationTokenSource.Cancel()`。
- 不支持安全取消的不可分割提交区间内，取消按钮暂时禁用。
- 错误文本允许换行，不能覆盖按钮或预览。

## 11. 校验规则

### 11.1 通用校验顺序

`CanCreate` 按以下顺序计算，并显示第一条可操作错误：

1. ProjectDirectory 可用且未 Dispose。
2. 项目名称有效。
3. 最终工程文件名有效且无冲突。
4. 音频已选择且能解码或通过平台预检。
5. 谱面模式有效。
6. Existing 模式下谱面已选择、可读、可写且同时有 parser/serializer。
7. CreateNew 模式下谱面名称、格式和 BPM 有效。
8. 最终谱面文件名有效且无冲突。
9. ACB 需要外置 AWB 时，AWB 已明确绑定且平台支持。
10. 当前没有进行中的提交。

### 11.2 项目名称

通用层至少拒绝：

- 空字符串或仅空白。
- `.` 和 `..`。
- `/` 与 `\` 路径分隔符。
- 控制字符。
- 用户输入已经包含 `.nyagekiProj`，避免双扩展。

平台层还需校验目标 provider 的实际文件名限制。为保证 Desktop 与 Browser 互通，建议最终采用一套跨平台可移植名称子集；具体限制列入后续决策主题。

最终工程文件名：

~~~text
ProjectFileName = ProjectNameStem + ".nyagekiProj"
~~~

### 11.3 新谱面名称和格式

名称主体至少拒绝：

- 空字符串或仅空白。
- `.` 和 `..`。
- 路径分隔符。
- 当前所选扩展名已出现在末尾。
- 任何已注册 serializer 扩展名已出现在末尾，避免用户绕过“格式唯一决定扩展名”。

最终谱面文件名：

~~~text
FumenFileName = NewFumenStem + SelectedFumenFormat.Extension
~~~

在创建前再次执行：

~~~csharp
parserManager.GetSerializer(FumenFileName)
~~~

返回 `null` 时即使下拉框中曾出现该格式，也必须阻止创建。

### 11.4 BPM

CreateNew 模式只显示一个“初始 BPM”输入项。它初始为空，用户必须手动填写；不另外显示 First、Common、Minimum、Maximum、`BpmList.FirstBpm` 或 `ProgJudgeBpm`。

初始 BPM 必须：

- 是有限数字，不是 `NaN` 或正负无穷。
- 大于 0。
- 首版不增加 0.001、9999、固定小数位数等产品级边界；所选格式若无法无损写入并重新解析该值，应报告格式兼容错误，而不是静默修正输入。

创建空白谱面时，把该输入值同步设置到：

- `MetaInfo.BpmDefinition.First`。
- `Common`、`Minimum`、`Maximum`。
- `Fumen.BpmList.FirstBpm`。

`MetaInfo.ProgJudgeBpm` 不从用户初始 BPM 派生，也不在 Setup 中显示或覆盖；它继续使用 `OngekiFumen`/`FumenMetaInfo` 的模型默认值。这样 UI 只有一个明确的用户输入，而五个代表初始谱面速度的内部字段保持一致。

### 11.5 冲突

项目目录根级的文件和目录统一使用 `StringComparison.OrdinalIgnoreCase` 比较。

以下都视为冲突：

- 已有同名文件。
- 已有同名目录。
- 只在大小写上不同的名称。
- Setup 表单打开后由外部程序新建的同名条目。

冲突检查必须执行两次：

1. 表单实时预检，用于禁用创建按钮和显示错误。
2. 事务开始后的最终复查，用于处理并发变化。

不得通过 `OpenWrite()` 探测可写性，因为该操作可能截断已有内容。创建时依赖 `CreateFileAsync` 和 `WriteAsync` 的实际结果，并映射为可恢复错误。

## 12. 创建事务详细步骤

建议 API：

~~~csharp
public sealed class EditorProjectCreationService
{
    public Task<EditorProjectCreationTransaction> PrepareAsync(
        EditorProjectSetupSelection selection,
        CancellationToken cancellationToken);
}
~~~

### 12.1 事务外操作

以下操作不应持有全局 `EditorProjectIoGate`：

- 打开文件夹 picker。
- 打开文件 picker。
- 用户填写 Setup 表单。
- 等待用户处理普通验证错误。

否则一个停留在 Setup 窗口的用户会阻塞其他文档 I/O。

### 12.2 取得 I/O gate

用户确认创建后：

1. 将 Selection 从 Setup Session 转交给 Provider。
2. Provider 调用 `PrepareAsync`。
3. `PrepareAsync` 取得 `EditorProjectIoGate`。
4. 在 gate 内刷新目录快照并做最终冲突校验。
5. 所有创建、复制、实际文件复读校验都在同一 gate 生命周期内完成。

如果 gate 只保证应用内串行，仍需捕获外部进程造成的 `IOException`。

### 12.3 内存预计算

尽量在创建目标文件前完成可能失败的纯计算：

1. 验证最终项目和谱面文件名。
2. Existing 模式先解析现有谱面，确认内容有效。
3. CreateNew 模式创建 `OngekiFumen` 并设置完整初始 BPM。
4. 调用目标 serializer，把新谱面序列化为 `byte[]`。
5. 使用 `EditorProjectFileManager.Create()` 创建最新版项目数据。
6. 写入 `AudioDuration`。
7. 把 0.5.5 工程序列化到 `MemoryStream`。

这样 serializer 或工程 serializer 失败时还没有创建任何目标文件，不需要回滚磁盘。

### 12.4 目标创建顺序

在没有资源复制的最小流程中，建议顺序为：

1. CreateNew 模式：创建最终谱面文件。
2. CreateNew 模式：用 `WriteAsync` 写入已生成的有效谱面内容。
3. 创建最终 `.nyagekiProj` 文件。
4. 用 `EditorProjectFileManager.Save(Stream, EditorProjectDataModel)` 经 `WriteAsync` 写入 0.5.5 内容。
5. 构造完整 `EditorFileAccessContext`。
6. 调用非消费式 `LoadDataAsync`，从刚落盘的真实文件重新解析。
7. 使用解析结果构造候选 `EditorContext`。

根据 S2，Existing 谱面、音频或必要 AWB 位于 `ProjectDirectory` 外时必须创建项目内副本。所有副本都属于本轮创建文件，必须纳入同一 created-files stack。建议先创建资源副本，再创建 `.nyagekiProj`，回滚时首先删除工程描述文件。

### 12.5 写入要求

- 所有新建文件都使用 `ISimpleDirectory.CreateFileAsync` 获取 capability。
- 文件内容通过 `ISimpleFile.WriteAsync` 写入。
- 不直接使用本地 `FileStream`，不依赖 `LocalPath`。
- 每个 `CreateFileAsync` 成功后立即把文件压入回滚栈。
- `WriteAsync` 失败时该文件仍在回滚栈中。
- CreateNew 模式不能留下零字节“占位谱面”作为成功结果。
- 工程文件必须由 `EditorProjectFileManager` 写出，不能手拼 JSON。

### 12.6 ACB/AWB

选择音频后尽早检查：

1. 非 `.acb` 音频不需要 AWB。
2. ACB 含 internal AWB 时直接通过。
3. ACB 声明 external AWB 时，先在明确的父目录能力中按声明文件名查找。
4. 找不到时由平台 Provider 要求用户显式选择。
5. 找到多个同名候选时拒绝猜测。
6. Browser 或无本地路径 provider 不支持当前 ACB 解码时，应在 Setup 中阻止创建。

最终 `EditorFileAccessContext` 必须在提交前带上 `AudioAwbFile`，不能在编辑器打开后再补。

## 13. 回滚设计

### 13.1 只记录本轮创建文件

事务维护：

~~~csharp
private readonly List<ISimpleFile> createdFiles = [];
~~~

不得加入：

- 用户选择的原音频。
- 用户选择的原谱面。
- 用户选择的原 AWB。
- 创建前已经存在的任何项目文件。
- 仅被借用的目录内文件。

### 13.2 回滚顺序

失败、取消或编辑器拒绝接管时：

~~~text
反向遍历 createdFiles
    |
    +-- DeleteAsync(CancellationToken.None)
    +-- 记录删除异常
    +-- 继续删除下一个

删除尝试完成
    |
    +-- Dispose 候选 EditorContext / FileAccessContext
    +-- Dispose 未转交的源 capability
~~~

必须先 Delete，后 Dispose。否则 wrapper 可能已不能执行 `DeleteAsync`。

### 13.3 独立区域

按 D6，生产代码中保留清晰的独立区域：

~~~csharp
#region Setup rollback
// Reverse-order best-effort deletion of files created by this transaction.
#endregion
~~~

注释只说明关键不变量，不逐行复述代码。

### 13.4 异常保留

- 主创建异常是最终显示给用户和上报日志的主要异常。
- 回滚异常逐项记录，但不替换主异常。
- 不建议把所有异常重新包装成只显示 rollback 的 `AggregateException`。
- 日志应包含未能删除的虚拟路径或文件名，但避免依赖本地路径。
- 若删除失败，错误对话框应告知用户可能留下了哪些本轮创建文件，以便手动处理。

## 14. 编辑器接管与事务提交

### 14.1 候选上下文

候选 `EditorFileAccessContext` 至少包含：

- `ProjectDirectory`。
- `ProjectFile`。
- `FumenFile`。
- `AudioFile`。
- ACB 需要时的 `AudioAwbFile`。
- Desktop 外部能力需要时的非重叠 `AdditionDirectories`。

候选 `EditorContext` 至少包含：

- 已加载的 `ProjectData`。
- 已解析的 `Fumen`。
- 完整 `FileAccessContext`。
- `ProjectName`。
- `LocationDescription`。
- `FileName` / `FilePath` 兼容显示字段，后续可统一清理。

### 14.2 接管契约

Provider 调用：

~~~csharp
var attached = await editor.TryAttachProjectAsync(
    transaction.CandidateContext,
    cancellationToken);
~~~

结果语义：

- `true`：ViewModel 已接管上下文，Provider 立即调用 `transaction.Commit()`。
- `false`：ViewModel 未接管，事务执行回滚。
- exception：ViewModel 未接管且旧状态未变，事务执行回滚后向用户显示错误。

为了让这个语义可验证，测试应在 `false` 和异常路径断言：

- `editor.EditorContext` 仍是调用前对象。
- `editor.AudioPlayer` 仍是调用前对象。
- 候选上下文仍可由事务访问和删除其文件。

### 14.3 提交点

事务的唯一提交点是：

~~~text
ViewModel 已成功接管完整 EditorContext
~~~

提交后：

- 清空 created-files rollback stack，但不 Delete 文件。
- 清空事务对候选上下文的所有权。
- 释放未进入最终上下文的源文件能力。
- 最近记录写入失败不触发回滚。

## 15. 最近记录

新建工程成功后生成 `EditorFileAccessContextSnapshot`，并使用：

- `ProjectName` 作为显示名称。
- `LocationDescription` 作为位置说明。
- 当前五字段 capability snapshot 作为 data。

若快照或最近记录持久化失败：

- 记录警告。
- `RecentRecordId` 保持空。
- 不关闭编辑器。
- 不删除已创建工程。
- 用户仍可正常保存和继续编辑。

根据 D19，最终职责应由 `ViewModel.Load/TryAttachProjectAsync` 在接管后触发。当前实现由 Provider 写最近记录，与文档不同。实施前应选择以下一种方式并保持统一：

- 按 D19 调整为 ViewModel 写入，Provider 只提供 `ProjectName` 和 `LocationDescription`。
- 若正式保留当前 Provider 写入方案，应先修订 live review 中 D19，而不是让代码和文档继续分叉。

本计划默认按 D19 对齐。

## 16. Provider 注册与菜单

### 16.1 双接口同实例

每个宿主组合根执行：

1. 注册具体平台 Provider 为 singleton。
2. `IEditorProvider` 通过 factory 返回该具体 singleton。
3. `IFumenVisualEditorProvider` 通过 factory 返回同一具体 singleton。
4. Core 的 `FumenVisualEditorProviderBase` 不注册。

必须有测试断言：

~~~csharp
Assert.Same(
    services.GetRequiredService<IEditorProvider>(),
    services.GetRequiredService<IFumenVisualEditorProvider>());
~~~

同时断言 `IEnumerable<IEditorProvider>` 中该文件类型只出现一次。

### 16.2 “打开”菜单 bug

`OpenFileCommandListHandler` 当前错误地执行：

~~~csharp
if (!editorProvider.CanCreateNew)
    continue;
~~~

“是否能新建”不能决定“是否能打开”。该过滤应从 Open handler 删除，并增加回归测试：

- `CanCreateNew == false` 且有 `FileTypes` 的 Provider 仍出现在“打开”菜单。
- 同一 Provider 不出现在“新建”菜单。

此修复可独立于 Setup 提前提交。

### 16.3 启用时机

`CanCreateNew` 保持 `false`，直到以下条件全部满足：

- Desktop/Browser Provider 注册正确。
- Setup 取消路径不泄漏 capability。
- 新谱面和现有谱面两条路径均能创建有效工程。
- 失败回滚测试通过。
- ViewModel 接管契约测试通过。
- Desktop 完成端到端手工测试。

根据 S3，Desktop 通过上述门槛后先返回 `true`。Browser Provider 即使已经完成架构接线，也保持 `false`，直到 25.5 的真实浏览器验收全部通过；不应为了接口一致而暴露未经验证的菜单。

## 17. 分阶段接入 UI 供测试

### 17.1 第一阶段：纯 ViewModel 和 XAML

- 重构 Setup ViewModel 为 Selection 表单。
- 使用 fake picker 和内存目录测试。
- 更新 XAML smoke test。
- 不接主菜单。

### 17.2 第二阶段：正式入口启用前的自动化验证

不增加 DEBUG Setup 命令或菜单。通过以下方式验证尚未公开的功能：

- 直接构造 Setup Session 和 ViewModel。
- 使用 fake picker、内存目录和 fault injection 测试 Selection、CreationPlan、冲突和回滚。
- 使用 Headless Avalonia/UI smoke 验证 XAML、绑定、状态切换和进度显示。
- Provider 集成测试直接调用 `TryNew`，不依赖菜单枚举。
- `CanCreateNew` 保持 `false`，Splash 也不得绕过该门控。

只有正式 Desktop 纵向链路完整后，才通过正式 File > New 做人工 UI 测试。

### 17.3 第三阶段：平台 Provider 端到端

- Desktop `TryNew` 接入真实创建事务。
- 修正 Splash，使其在 UI 和命令层都遵守 `CanCreateNew`。
- Desktop 端到端稳定后将 Desktop `CanCreateNew` 改为 `true`。
- Debug 和 Release 通过同一正式入口执行，不增加条件编译业务分支。
- Browser 按 S3 的独立真实运行时门槛启用。

## 18. 测试计划

### 18.1 Setup ViewModel

至少覆盖：

- 默认模式和默认格式。
- serializer 列表动态生成和多扩展展开。
- 项目名称改变时工程预览刷新。
- 谱面名称或格式改变时谱面预览刷新。
- 用户输入扩展名时的错误。
- 空名称、`.`、`..`、分隔符和控制字符。
- BPM 为 0、负数、`NaN`、正负无穷和边界值。
- Existing 模式缺少谱面。
- Existing 谱面只有 deserializer、没有 serializer。
- 音频缺失或解码失败。
- ACB 需要外置 AWB。
- 文件与目录的大小写不敏感冲突。
- 模式切换后候选 capability 的保留、转交和释放。
- `IsBusy` 时命令禁用。

### 18.2 创建事务

成功路径：

- Existing 谱面 + 普通音频。
- CreateNew `.nyageki`。
- CreateNew `.ogkr`。
- 内置 AWB 的 ACB。
- 外置 AWB 的 ACB。

失败注入点：

- 新谱面 serializer 失败。
- 工程 serializer 失败。
- 创建谱面文件失败。
- 写谱面失败。
- 创建工程文件失败。
- 写工程失败。
- 实际落盘文件复读失败。
- 谱面反序列化失败。
- ACB/AWB 最终校验失败。
- ViewModel 音频加载失败。
- ViewModel 拒绝接管。
- ViewModel 接管前抛异常。
- 用户取消。
- 第一个 Delete 失败但后续 Delete 继续。
- 并发产生同名文件。

每个失败测试都断言：

- 用户原文件字节不变。
- 只删除本轮创建文件。
- 删除顺序与创建顺序相反。
- 主异常未被 rollback 异常替换。
- 所有 capability 最终释放。

### 18.3 所有权

使用带 Dispose 计数的 fake：

- Setup 替换音频时旧候选释放一次。
- Setup 取消时全部候选释放一次。
- `TakeSelection` 后 Session 不释放已转交对象。
- 创建失败时事务删除后释放。
- 创建成功时事务不释放已转交 `EditorContext`。
- ViewModel 替换上下文时旧上下文释放。
- 目录根内角色文件不被角色字段重复释放。
- 独立外部文件由上下文释放。
- 重复或祖先/后代重叠根在构造时被拒绝。

### 18.4 Provider 和 DI

- Desktop 组合根只注册一个 Fumen Provider。
- Browser 组合根只注册一个 Fumen Provider。
- 两个接口解析为同一实例。
- Provider 枚举不包含共享基类。
- 目录 picker 取消返回 `false` 且不显示 Setup。
- Setup 取消返回 `false` 且不创建文件。
- 创建成功后文档被接管。
- 最近快照失败不影响成功返回。
- 外置 AWB 取消时完整清理。

### 18.5 菜单

- 不支持新建但支持打开的 Provider 出现在 Open。
- `CanCreateNew == false` 时不出现在 New。
- `CanCreateNew == true` 后只出现一次。
- Desktop/Browser 不会同时枚举两个相同 Fumen 文件类型 Provider。

### 18.6 端到端

建议至少执行：

1. 在空项目目录创建新谱面工程。
2. 保存、关闭、从最近记录重新打开。
3. 使用现有谱面创建工程。
4. 对非空目录触发工程名和谱面名冲突。
5. 创建中注入失败，确认目录只保留原内容。
6. 选择损坏音频或谱面，确认 Setup 保留输入并可修正。
7. 外置 AWB 正常绑定和取消。
8. Desktop 与 Browser 各自验证权限恢复行为。

## 19. 推荐实施批次

### 批次 1：菜单打开修复

- 删除 Open handler 对 `CanCreateNew` 的过滤。
- 添加菜单回归测试。

完成条件：当前 `CanCreateNew == false` 的 Fumen Provider 能出现在 Open，但仍不出现在 New。

### 批次 2：D12 和加载契约

- 修复 `EditorFileAccessContext` 根和角色所有权。
- 修复 ViewModel 替换旧上下文的释放。
- 将 `LoadProjectAsync` 改为原子接管契约。
- 拆出非消费式 `LoadDataAsync`。
- 添加 fault/dispose 测试。

完成条件：任何加载失败都能明确判断上下文仍由调用方拥有还是已由文档接管。

### 批次 3：平台 Provider 拆分

- 创建不注册的 `FumenVisualEditorProviderBase`。
- 创建 Desktop/Browser Provider。
- 在宿主组合根做具体 singleton + 双接口别名。
- 迁移现有打开和最近恢复逻辑。
- 对齐或正式修订 D19。

完成条件：每个宿主只有一个 Provider，现有打开链路完全回归。

### 批次 4：Setup 表单

- 新建 Session、Selection、picker abstraction 和 format option。
- 重构 ViewModel。
- 重写 XAML。
- 完成纯表单测试。
- 增加 Headless UI/XAML smoke、内存目录和 Provider 直接调用测试，不增加 DEBUG 菜单入口。

完成条件：两种谱面模式、全部预览和校验均由自动化测试覆盖；正式 Desktop 纵向链路完成前，Debug 和 Release 的 New 都不启用。

### 批次 5：创建事务

- 实现内存预序列化。
- 实现目标创建、写入、复读校验。
- 实现独立 `#region Setup rollback`。
- 实现 fault injection 测试。

完成条件：任一提交点前失败均不会覆盖用户文件，也不会遗留可正常删除的本轮文件。

### 批次 6：平台接线

- Desktop `TryNew` 接入目录选择、Setup 和事务。
- 按已确认资源策略处理现有谱面、音频和 AWB。
- Browser Provider 完成平台服务接线、编译、AOT/trimming 和可自动化测试，但保留 `CanCreateNew == false`。
- 新建成功后接最近记录。

完成条件：Desktop 端到端创建、保存、关闭、重开通过；Browser 架构接线和构建验证通过。

### 批次 7：启用菜单和收尾

- Desktop `CanCreateNew = true`；Browser 继续按独立验收门控保持 `false`。
- 验证 New/Open 菜单各显示一次。
- 更新 live review 实施状态和本计划决策记录。
- 执行完整测试和必要的平台条件构建。

## 20. 完成定义

Setup 功能只有同时满足以下条件才算实现：

- 用户从“新建”菜单可以完成项目创建。
- 两种谱面模式都产生非空、可写、可解析的 `FumenFile`。
- 创建的 `.nyagekiProj` 是 0.5.5，并可正常保存和重新打开。
- `EditorProjectDataModelBase` 没有新增字段。
- 不保存谱面或音频路径到最新版工程模型。
- 没有同名覆盖或自动编号。
- 失败和取消不会删除用户原文件。
- 回滚删除顺序和异常处理有测试。
- ViewModel 接管前后的所有权边界可测试。
- Desktop/Browser Provider 注册符合 D13。
- Open 菜单不再错误依赖 `CanCreateNew`。
- 最近记录失败不影响已成功创建的工程。

### 20.1 首版实现状态（2026-08-18）

以上完成定义已经落到当前代码，关键入口和验证结果如下：

- Desktop 注册一个 `DefaultDesktopFumenVisualEditorProvider`，同时作为 `IEditorProvider` 和 `IFumenVisualEditorProvider`，`CanCreateNew == true`。
- Browser 注册独立的 `DefaultBrowserFumenVisualEditorProvider`，共享同一套 Setup/事务核心，但 `CanCreateNew == false`，因此不会在 Browser 菜单暴露未经真实运行时验收的新建入口。
- Setup 在确认时冻结“项目目录、源能力、最终文件名、需要复制的角色”快照；目标冲突检查先于写入，回滚只删除本轮 `CreateFileAsync` 返回的文件，源文件和用户原文件不会被删除或覆盖。
- 新谱面只接受用户填写的正数有限初始 BPM，并验证 `First`、`Common`、`Minimum`、`Maximum` 和 `BpmList.FirstBpm`；`ProgJudgeBpm` 保持格式默认值且不在 UI 暴露。
- 外置 AWB 的目标叶名由 ACB 声明固定，源文件可以不同名；最近记录快照对 AWB 使用向后兼容的可选书签。AWB 书签失效时不会把整个最近项目永久标记为损坏，而是提示从项目目录重新绑定。
- `EditorProjectDataModelBase` 未新增字段；运行时项目名称、位置和文件能力继续由 `EditorContext` 持有。

验证结果：Core `493/493`、Desktop `112/112` 测试通过；Core、Desktop、Browser Debug 构建均为 `0` 个错误。现有依赖漏洞、旧 nullable/分析器和 WASM 诊断仍作为非阻塞警告保留。

## 21. 决策访谈规则

剩余产品决策按依赖关系逐题确认。每次只确认一个问题：

1. 先说明它影响哪些接口、事务和 UI。
2. 给出推荐方案和原因。
3. 记录用户回答。
4. 更新本文的“最终决定”和受影响步骤。
5. 再进入下一个依赖问题。

可以从代码直接确定的事项不会拿来提问。

## 22. 已确认决策主题

以下决策访谈已完成，S1-S9 均已确认：

1. `ProjectDirectory` 是用户直接选择的最终目录，还是由应用在父目录下创建子目录。
2. 新建工程选择的现有谱面、音频和 AWB，在 Desktop/Browser 上复制到项目目录还是保留外部能力。
3. 第一版是 Desktop 先行，还是 Desktop 与 Browser 同批启用。
4. 项目名称、目录名称和默认值之间的关系。
5. 跨平台可移植文件名限制。
6. 新谱面的默认格式、必填初始 BPM 和其他初始元数据。
7. ACB/AWB 导入时的目标命名和冲突交互。
8. 创建过程是否向用户提供可取消进度。
9. 不实现独立 DEBUG Setup 预览入口，正式 Desktop New 作为唯一人工入口。

## 23. 决策 S1：ProjectDirectory 如何取得

### 23.1 为什么这是第一个问题

D3 已经规定新谱面写入 `ProjectDirectory` 根级，但没有规定该目录本身如何产生。这个选择会直接改变：

- Provider 的第一个 picker 是“选择最终项目目录”还是“选择父目录”。
- 项目名称改变时是否也改变目标目录。
- 是否需要创建和回滚目录。
- 是否需要给 `ISimpleDirectory` 增加删除目录能力。
- 非空目录是否是正常场景。
- Browser 对父目录写权限和子目录 capability 的处理。
- Setup 顶部显示的是固定目录还是动态预览目录。

### 23.2 推荐方向：直接选择最终 ProjectDirectory

推荐用户先选择一个已经存在、并明确授权给应用的最终项目目录。操作系统或应用内 picker 若支持“新建文件夹”，用户可以在 picker 中先创建再选中；应用自身不根据项目名称自动创建子目录。

推荐后的具体规则：

- Provider 选择到的目录就是 `ProjectDirectory`。
- Setup 中目录只读显示，可通过文件夹按钮重新选择。
- `ProjectName` 只决定 `ProjectName.nyagekiProj` 和显示名称，不隐式重命名目录。
- 默认项目名称可以取 Provider 在包装前保存的 picker 文件夹显示名，但用户可独立修改。
- 用户取消时只 Dispose 目录 capability，不删除目录。
- 允许选择非空目录，但项目名、谱面名和将来资源副本都必须通过冲突检查。
- 创建事务只回滚文件，不需要回滚目录。

推荐原因：

- 与当前 `ISimpleDirectory` 能力模型一致，已有 `CreateFileAsync` 和 `GetOrCreateDirectoryAsync`，但没有安全的目录删除事务。
- 与 D3 “在已经选定的 ProjectDirectory 根级创建”最直接一致。
- 避免项目名称编辑过程中目标目录随输入变化。
- Desktop 和 Browser 都可以围绕一个明确授权目录构造 capability。
- 取消和失败不会误删用户原有目录。

### 23.3 另一方向：选择父目录，由应用创建项目子目录

该方向通常会把 `ProjectName` 同时作为子目录名，并在父目录下创建：

~~~text
SelectedParent/
    ProjectName/
        ProjectName.nyagekiProj
        chart.nyageki
        audio...
~~~

优点：

- 默认得到结构整洁的独立目录。
- 用户只需要选择一次父位置并输入项目名。

代价和新增要求：

- 必须明确“项目显示名称”和“目录名”是否始终相同。
- 项目名修改会改变尚未创建的目标目录预览。
- 必须处理同名目录冲突。
- 必须为目录创建失败、部分文件失败和取消定义目录回滚。
- 当前 `ISimpleDirectory` 没有 `DeleteDirectoryAsync`，需要扩展所有 provider 和测试 fake。
- 不能因为目录由本轮创建就无条件删除；只有确认目录仍为空且仍是本轮创建对象时才可删除。
- Browser/OPFS 需要验证父 capability 创建子目录和恢复书签后的语义。

### 23.4 同时支持两种方式

也可以让用户选择“使用当前目录”或“创建项目子目录”，但第一版会增加一套模式、预览、冲突规则和测试矩阵。除非实际用户工作流明确同时需要两种方式，否则不建议在 Setup 首版加入。

### 23.5 当前建议结论

当前建议采用“用户直接选择最终 `ProjectDirectory`，应用只在其中创建文件，不自动创建或删除项目目录”。如果确认，批次 4 和批次 6 将按此契约实施；如果希望应用创建子目录，需要先补目录事务和跨 provider 的目录删除/回滚设计。

### 23.6 用户确认

已确认，2026-08-17。

最终规则：

- 用户通过平台 picker 直接选择最终 `ProjectDirectory`。
- 应用不根据 `ProjectName` 自动创建项目子目录。
- `ProjectName` 与目录名称相互独立。
- Setup 可用 Provider 在包装前保存的 picker 文件夹显示名作为项目名称默认值，但用户可以修改。
- 用户取消或创建失败时只释放目录 capability，不删除目录。
- 创建事务只回滚本轮创建的文件。

## 24. 决策 S2：现有谱面、音频和 AWB 是否复制进项目目录

### 24.1 为什么必须现在确认

这个决定会改变 Setup Selection、创建事务、最近记录和平台 Provider 的基本形状：

- 现有谱面是直接编辑用户原文件，还是编辑项目内副本。
- 大音频是否在新建阶段复制，以及是否需要进度和取消。
- Desktop 是否允许新项目从一开始就是多根工作区。
- Browser 最近恢复时是否依赖多个外部文件授权。
- 外置 AWB 是否必须和 ACB 一起迁移。
- 回滚栈中需要记录哪些副本。
- 项目目录是否能作为新工程全部主文件的单一授权根。

代码已经能确定以下事实：

1. `EditorFileAccessContextSnapshot` 可以记录多个直接文件 capability，但外部文件移动、书签失效或权限撤销都会影响最近恢复。
2. `EditorProjectDataModel` 0.5.5 不保存谱面和音频路径，无法依靠工程文件重新猜测外部资源。
3. Browser 当前在 `EditorProjectDataUtils` 中明确拒绝 ACB 解码，因此复制 ACB 也不会让 Browser 立即支持 ACB。
4. `ISimpleFile` 没有现成 Copy API，但可以使用 `OpenRead()` 和目标 `WriteAsync()` 实现跨 provider 流式复制。
5. 音频可能很大，复制实现不能调用 `ReadAllBytes()` 把整个文件放入内存。
6. live review 已建议 Browser 新建使用单根工作区，Desktop 新建与 Browser 共用导入流程；Desktop 多根主要用于旧项目和 Fast Open 兼容。

### 24.2 推荐方向：新建工程统一采用单根工作区

建议 Desktop 和 Browser 的“新建项目”都遵守以下规则：

- 已选资源本来就在 `ProjectDirectory` 树内时，直接绑定该文件，不复制。
- 已选现有谱面位于项目目录外时，复制到 `ProjectDirectory` 根级，并把副本作为最终 `FumenFile`。
- 已选音频位于项目目录外时，复制到 `ProjectDirectory` 根级，并把副本作为最终 `AudioFile`。
- ACB 需要外置 AWB 且平台支持时，AWB 与 ACB 一起复制到项目目录，并保持 ACB 声明所要求的文件名关系。
- `EditorFileAccessContext` 最终只拥有 `ProjectDirectory` 这个目录根；主文件角色都指向该根内文件。
- 源谱面、源音频和源 AWB 只在复制期间借用，复制和校验完成后释放源 capability。
- 后续保存只修改项目内谱面副本，绝不修改项目目录外的源谱面。

最终目录形态大致为：

~~~text
ProjectDirectory/
    ProjectName.nyagekiProj
    chart.nyageki
    audio.wav
    audio.acb
    audio.awb
~~~

实际只包含用户选择格式所需的文件，不会同时产生上例中的所有音频文件。

### 24.3 推荐原因

#### 不修改外部源谱面

如果 Desktop 直接绑定项目外的现有谱面，用户第一次保存工程就会修改原谱面。Setup 中“使用现有谱面”容易被理解为“导入到新工程”，不一定意味着“把原文件变成当前工程的长期写入目标”。

复制后：

- 源谱面字节保持不变。
- 项目内副本成为明确的独占写入目标。
- 创建失败时只删除副本。
- 用户可以比较或恢复原谱面。

#### 最近记录更稳定

单根工作区只需要项目目录及其子文件能力。外部文件方案需要分别恢复谱面、音频和 AWB；任意一个外部书签失效都会使项目无法完整恢复。

#### Desktop 和 Browser 行为一致

统一策略可以让：

- Setup ViewModel 使用同一资源处置语义。
- 创建事务使用同一回滚规则。
- Desktop 创建的工程更容易复制到 Browser 可访问位置。
- 测试不需要为每种资源组合出“Desktop 外部、Browser 内部”两套长期行为。

#### 所有权更简单

最终上下文只拥有 `ProjectDirectory`：

- 角色文件由目录根释放。
- 不需要为新工程构造多个 `AdditionDirectories`。
- 不需要处理外部文件根与项目根重叠。
- D12 的借用别名规则更容易验证。

### 24.4 复制的具体实现建议

建议新增与平台无关的流式复制 helper：

~~~csharp
public static async Task CopyToAsync(
    ISimpleFile source,
    ISimpleFile target,
    CancellationToken cancellationToken)
{
    await using var sourceStream = await source.OpenRead();
    await target.WriteAsync(
        (targetStream, writerCancellationToken) =>
            sourceStream.CopyToAsync(
                targetStream,
                81_920,
                writerCancellationToken),
        cancellationToken);
}
~~~

实现时还应：

- 在创建目标前完成源文件可读性和目标名称校验。
- 目标 `CreateFileAsync` 成功后立即压入回滚栈。
- 使用 cancellation token 复制，但回滚删除使用 `CancellationToken.None`。
- 复制完成后检查目标长度；格式可解析的资源还要执行语义校验。
- 不根据 `FullPath` 字符串判断源是否属于项目目录，应沿 `ParentDictionary` capability 链按对象身份判断。
- 源已经属于 `ProjectDirectory` 时不创建副本，也不把源加入 created-files stack。
- 目标冲突时拒绝覆盖并回到 Setup，让用户明确修改目标名称。

### 24.5 现有谱面的处理

项目外现有谱面建议：

1. 先确认其扩展名同时存在 deserializer 和 serializer。
2. 解析源谱面，确保内容有效。
3. 默认沿用源文件名作为项目内目标名称。
4. 在项目根创建副本并流式复制原始字节，不在导入时强制重新序列化。
5. 从副本重新解析，确认复制结果有效。
6. 后续保存使用对应 serializer 写入副本。

导入时复制原始字节的原因是尽量保留格式中的注释、排序或 serializer 尚未表达的细节。用户第一次主动保存后，文件才按当前 serializer 的标准输出重写。

项目目录内现有谱面建议直接绑定：

- 用户已经明确选择该文件。
- 文件本身属于项目工作区。
- 不需要制造第二个同名副本。
- 该文件不是本轮创建对象，失败时不得删除。

### 24.6 音频的处理

项目外音频建议默认复制，原因是音频是重新打开编辑器的强依赖。

需要接受的代价：

- 大文件会增加创建时间和磁盘占用。
- Setup 需要显示复制进度。
- 用户取消时需要中止复制并删除不完整目标。
- Browser 还受 OPFS 或授权目录存储配额限制。

为了控制内存：

- 复制必须流式执行。
- 进度以 `source.FileLength` 和累计复制字节计算。
- provider 不可靠地报告长度时，显示不确定进度。
- 不为复制计算完整文件哈希作为首版必需条件；长度和实际解码验证已经能覆盖主要失败。

项目目录内音频直接绑定，不复制，也不删除。

### 24.7 ACB/AWB 的平台事实

Desktop：

- 可以检查 ACB 是 internal AWB 还是 external AWB。
- external AWB 必须在提交前明确绑定。
- 若复制 ACB，必要 AWB 也必须作为同一事务的一部分复制。
- 只复制主 ACB 和它明确要求的 AWB，不复制邻接的 `Music.xml`、封面或其他文件。

Browser：

- 当前实现明确不支持 ACB 解码。
- Setup 的 Browser 音频过滤器应暂时隐藏或拒绝 `.acb`。
- 不能通过“先复制到项目目录”绕过该限制。
- 未来 Browser 音频栈支持 ACB 后，再复用同一 ACB/AWB 复制事务。

这些属于当前代码能力事实，不需要额外产品确认。

### 24.8 保留外部文件的替代方向

Desktop 可以技术上保留项目外 capability，并建立多根上下文：

~~~text
ProjectDirectory/
    ProjectName.nyagekiProj

ExternalFumenDirectory/
    chart.nyageki

ExternalAudioDirectory/
    audio.wav
~~~

它的优点是：

- 不复制大音频。
- 用户可以继续编辑原位置谱面。
- 与某些既有本地目录工作流兼容。

但它会引入：

- 保存时修改原谱面的风险。
- 多个授权根和更复杂的 Dispose 规则。
- 最近记录依赖多个书签。
- 工程目录单独复制后不能独立打开。
- Desktop 和 Browser 行为不一致。
- 用户需要理解“保留原位置”不是只读引用。

该能力仍适合 D9 已确认的 Desktop Fast Open 转正兼容流程，但不建议作为新建工程的默认形态。

### 24.9 折中方向：只保留外部音频

另一种折中是：

- 谱面总是复制进项目根，确保不修改源谱面。
- Desktop 允许音频和 AWB 留在原位置，避免复制大文件。
- Browser 仍复制普通音频。

这个方案比全部外部安全，但项目仍然不是可独立移动的单根工程，最近恢复也仍依赖外部音频授权。若用户的音频通常很大、项目目录空间有限，它可能更符合 Desktop 实际工作流。

### 24.10 当前建议结论

当前建议是：

> 新建项目统一采用单根工作区。项目目录外的现有谱面、普通音频和必要 AWB 全部复制到最终 `ProjectDirectory`；项目目录内的资源直接绑定。源文件从不覆盖、从不删除，最终 `EditorContext` 只绑定项目内文件。Browser 暂不接受 ACB。

这会让 Setup 首版的行为可预测、安全且跨平台一致。代价是复制大音频需要进度、取消和存储空间检查，这些应进入批次 5/6，而不能通过整文件读入内存规避。

### 24.11 用户确认

已确认，2026-08-17。

最终规则：

- 新建工程统一采用单根工作区。
- 项目目录外的现有谱面、普通音频和必要 AWB 必须复制到 `ProjectDirectory` 根级。
- 项目目录内资源直接绑定，不重复复制。
- 源文件不覆盖、不删除，也不作为后续谱面保存目标。
- 复制使用流式 I/O、进度、取消和反向回滚。
- 最终 `EditorFileAccessContext` 只拥有 `ProjectDirectory` 目录根。
- Browser 当前不接受 ACB。

## 25. 决策 S3：首版是 Desktop 先行还是 Desktop 与 Browser 同批启用

### 25.1 代码现状

Desktop 和 Browser 都有可继续实现 Setup 的基础，但成熟度不同。

Desktop 当前具备：

- 完整的 Desktop 宿主项目和服务注册入口。
- 本地或 Avalonia Storage Provider 文件夹、文件 capability。
- Desktop NAudio reader 和输出设备实现。
- `.mp3`、`.wav`、`.aif`、`.aiff`、`.acb` 等平台音频能力。
- 独立 Desktop 测试项目，可覆盖平台服务和命令行路径。
- 可直接检查本地 ACB 及 external AWB。

Browser 当前具备：

- 独立 Browser 宿主项目。
- Browser AudioWorklet 输出实现。
- `.wav`、`.aif`、`.aiff` reader。
- Avalonia Storage Provider wrapper 的通用创建、写入、删除和书签接口。
- OPFS 基础存储、浏览和下载模块。
- Browser Release 的 WebAssembly AOT 和 trimming 构建配置。

但 Browser 仍缺少：

- `DefaultBrowserFumenVisualEditorProvider`。
- Browser Setup 项目目录选择和资源导入的端到端实现。
- 把 OPFS 目录直接适配为 `ISimpleDirectory` 项目根的正式入口；当前 OPFS 浏览模块主要面向浏览、预览和下载。
- 在真实浏览器中验证文件夹 capability 是否支持创建、替换、删除和恢复。
- 浏览器存储配额不足时的 Setup 错误和清理验证。
- 大音频复制时的真实内存、吞吐和取消验证。
- 用户激活约束下启动 AudioWorklet 和加载项目音频的端到端验证。
- Browser Setup 创建、保存、关闭、重开的自动化或稳定手工验收基线。

当前 Browser 相关单元测试主要使用 fake、链接源码或静态 XAML/模块契约检查，不等价于真实浏览器运行。

### 25.2 推荐方向：架构同时完成，功能分阶段启用

建议不要把 Browser Provider 的架构留到以后，而是：

1. 批次 3 同时建立 Desktop 和 Browser 平台 Provider，完成 D13 的宿主注册边界。
2. 共享 Setup、Selection、创建事务和回滚逻辑同时支持两个平台。
3. Browser 项目始终参与编译、AOT/trimming 检查和平台单元测试。
4. 第一个用户测试里程碑只让 Desktop Provider 返回 `CanCreateNew == true`。
5. Browser Provider 暂时保持 `CanCreateNew == false`，但不是空壳；它应能完成平台服务组合并具备待验收实现。
6. Browser 通过真实运行时验收后，只修改平台能力门控并启用菜单，不再修改 Core 契约。

这可以表述为：

~~~text
共同开发完成
    |
    +-- Core contracts / transaction / rollback
    +-- Desktop Provider
    +-- Browser Provider
    +-- 两端 DI identity tests
    |
    v
Desktop acceptance gate
    |
    +-- 通过 -> Desktop CanCreateNew = true
    |
    v
Browser runtime acceptance gate
    |
    +-- 通过 -> Browser CanCreateNew = true
~~~

### 25.3 推荐原因

#### 不让平台架构再次延期

D13/D17/D18 已经要求本次拆成两个宿主 Provider。如果只实现 Desktop 类并继续让 Browser 依赖共享具体 Provider，会再次制造过渡架构。Browser Provider、DI 别名和编译边界应与 Desktop 同批完成。

#### 不让 Browser 运行时风险阻塞 Desktop 反馈

Browser 的主要风险不是 C# 业务逻辑，而是：

- 浏览器文件系统实现和权限生命周期。
- 存储配额。
- 大文件复制。
- AudioWorklet 的用户激活。
- AOT/trimming。
- 浏览器刷新或页面关闭后的恢复。

这些问题需要真实浏览器验证。把“两个平台必须同一天打开菜单”设为第一里程碑，会延迟 Desktop 用户对 Setup 表单、名称规则和事务行为的反馈。

#### 避免临时分叉

分阶段启用不代表实现两套 Setup：

- ViewModel、Selection、事务和 serializer 都共享。
- S1/S2 的目录和单根规则一致。
- 差异只在 Provider picker、支持音频格式和 `CanCreateNew` 门控。
- Browser 通过后不应复制 Desktop 代码或修改持久化格式。

### 25.4 Desktop 首次启用门槛

Desktop `CanCreateNew` 可以改为 `true` 前，至少需要：

1. 新谱面和现有谱面模式都通过。
2. 普通音频复制、进度、取消和回滚通过。
3. ACB internal AWB 和 external AWB 通过。
4. 创建后保存、关闭、文件夹打开和最近打开通过。
5. 文件名冲突和并发冲突通过。
6. 失败注入和 capability Dispose 测试通过。
7. Desktop 实际 UI 手工验收通过。

### 25.5 Browser 启用门槛

Browser `CanCreateNew` 可以改为 `true` 前，至少需要：

1. 在目标支持浏览器中取得可写的最终 `ProjectDirectory`。
2. 在真实浏览器中验证 `CreateFileAsync`、`WriteAsync` 和 `DeleteAsync`。
3. 普通 `.wav` 或 `.aif/.aiff` 导入、复制和播放通过。
4. 大音频复制不造成不可接受的内存峰值。
5. 复制取消和页面内回滚通过。
6. 存储配额不足能显示明确错误并清理本轮文件。
7. Setup 成功后保存、关闭、重新打开通过。
8. 书签不可用时最近记录非致命降级符合预期。
9. WebAssembly Release AOT/trimming 构建通过。
10. Browser 不显示或拒绝 `.acb`。

目标浏览器范围和自动化方案可以在确认 S3 后作为 Browser 验收子任务细化，不需要改变 Setup 核心设计。

### 25.6 同批启用的替代方向

Desktop 和 Browser 同批打开 `CanCreateNew` 的优点是：

- 首次发布即保持平台功能一致。
- 不会出现一段时间内 Browser 菜单缺少“新建”。
- 可以更早发现共享抽象只适合 Desktop 的问题。

代价是：

- Browser 的真实权限、配额和音频问题会成为 Desktop 发布阻塞项。
- 在没有浏览器端端到端基线时，容易为了赶同批发布而降低验收标准。
- 调试跨越 Desktop、本地文件系统、WebAssembly、JavaScript interop 和浏览器安全模型，定位周期更长。

如果产品要求严格同批启用，则必须把 25.5 的全部 Browser 门槛视为首个发布里程碑的硬阻塞条件，不能只依靠编译通过。

### 25.7 当前建议结论

当前建议是：

> Desktop 和 Browser Provider、共享事务及 DI 架构同批实现；首个可供用户测试的 `CanCreateNew` 只在 Desktop 启用。Browser 保持菜单关闭，直到真实浏览器中的目录写入、资源复制、音频播放、回滚、重开及 AOT/trimming 验收全部通过，再独立启用。

### 25.8 用户确认

已确认，2026-08-17。

最终规则：

- Desktop 和 Browser Provider、共享事务及 DI 架构同批实现。
- 首个用户测试里程碑只启用 Desktop `CanCreateNew`。
- Browser Provider 参与编译、AOT/trimming 和平台测试，但菜单保持关闭。
- Browser 通过真实目录写入、资源复制、普通音频、回滚、重开及 Release 构建验收后再启用。
- Browser 启用不改变 Core 契约或项目格式。

## 26. 决策 S4：项目名称的默认值和目录切换行为

### 26.1 已由 S1 确定的边界

以下内容已经确定，不再重复提问：

- `ProjectName` 与项目目录名称相互独立。
- 修改 `ProjectName` 不创建、重命名或移动目录。
- `ProjectName` 只决定 `ProjectName.nyagekiProj`、`EditorContext.ProjectName` 和最近记录显示名称。
- 最终工程文件名冲突时不覆盖、不自动编号。

S4 只需要决定：

- 初次选择目录后如何产生默认 `ProjectName`。
- 用户修改项目名称后，再次选择目录是否覆盖该输入。
- 自动默认值无效或冲突时是否自动修正。

### 26.2 当前代码事实：不能从根 DirectoryName 取得显示名

`AvaloniaStorageProviderFileSystemBuilder.LoadFromAvaloniaStorageFolder` 在构造完整目录树后会执行：

~~~csharp
root.DirectoryName = string.Empty;
~~~

`LoadRootFromAvaloniaStorageFolder` 同样用空字符串创建根 wrapper。因此：

- `ProjectDirectory.DirectoryName` 不能作为所选文件夹显示名。
- Setup Session 必须单独保存 picker 返回的 `IStorageFolder.Name` 或平台等价显示名。
- 当前打开流程已经在包装前保存 `folderDisplayName = selectedFolder.Name`，新建流程应沿用这一模式。

建议 Session 构造参数包含：

~~~csharp
public EditorProjectSetupSession(
    ISimpleDirectory projectDirectory,
    string projectDirectoryDisplayName,
    IEditorProjectSetupFilePicker filePicker);
~~~

### 26.3 推荐的首次默认值

建议打开 Setup 时：

1. 取得 picker 返回的文件夹显示名。
2. 显示名非空时，原样作为 `ProjectName` 初始建议。
3. 显示名为空时，使用稳定的 ASCII 后备名称 `project`。
4. 立即生成 `ProjectFileNamePreview`。
5. 立即运行名称合法性和冲突校验。

不建议在默认阶段静默执行：

- 替换非法字符。
- 删除尾部字符。
- 自动追加 `_1`、`_2`。
- 因冲突自动生成另一个名称。
- 把本地化文本作为文件名后备值。

原因是静默修正会让输入框内容、最终预览和用户对目录名称的理解不一致。若文件夹显示名不适合作为工程文件名，应保留该建议并显示明确校验错误，要求用户主动修改。

### 26.4 用户编辑状态

建议 ViewModel 记录项目名称来源：

~~~csharp
private enum ProjectNameOrigin
{
    SuggestedFromDirectory,
    UserEdited
}
~~~

行为规则：

- Setup 第一次打开时，名称来源为 `SuggestedFromDirectory`。
- 用户对项目名称进行任何实际编辑后，来源变为 `UserEdited`。
- 用户主动清空输入也算编辑，不应立刻被自动填回。
- 程序更新预览或验证状态不改变名称来源。
- 不需要把该枚举暴露到持久化模型。

### 26.5 重新选择 ProjectDirectory

用户点击目录按钮并选择另一个最终目录时，建议：

- 若名称仍是自动建议状态，则用新目录显示名重新建议。
- 若名称已经由用户编辑，则保留用户输入，不覆盖。
- 无论名称是否变化，都重新计算工程文件名预览和冲突。
- 重新判断已选谱面、音频和 AWB 相对于新目录是“目录内直接绑定”还是“目录外需要复制”。
- 旧 `ProjectDirectory` capability 在新目录成功接管后释放。
- 用户取消第二次目录 picker 时，目录、名称和资源状态全部保持不变。

“用户编辑后不覆盖”可以避免以下场景：

~~~text
选择目录 A
  -> 默认项目名 A
用户改为 MySong
  -> 发现目录选错，改选目录 B
  -> 项目名仍保持 MySong
~~~

### 26.6 默认名称发生冲突

如果目录 `MySong` 中已经存在 `MySong.nyagekiProj`：

- Setup 仍显示 `ProjectName = MySong`。
- 预览显示 `MySong.nyagekiProj`。
- 创建按钮禁用并显示冲突。
- 用户必须主动修改项目名称。
- 不自动变成 `MySong_1`。

这与 D5/D7 的“不自动编号、保留输入”一致。

### 26.7 显示名称和最终名称

建议：

- 输入框显示用户当前文本。
- 预览显示验证后将实际使用的 `ProjectName.nyagekiProj`。
- `EditorContext.ProjectName` 使用最终确认的项目名称主体。
- 最近记录显示 `EditorContext.ProjectName`，不显示目录名代替它。
- `LocationDescription` 使用目录的显示名或平台提供的位置说明。

首尾空白、Unicode、保留名称和跨平台字符集规则由 S5 统一决定；S4 不提前静默 trim 或规范化。

### 26.8 为什么不始终跟随目录名

如果每次目录变化都强制覆盖项目名：

- 用户的明确输入会丢失。
- 项目名无法独立表达歌曲或工程名称。
- 同一个目录中创建多个工程时无法工作。
- S1 已确认的“目录名与项目名独立”会被 UI 行为暗中破坏。

如果从不提供默认值：

- 大多数空目录新建场景会增加一次没有必要的输入。
- 用户仍可能直接输入与目录相同的名称。

“首次建议，编辑后锁定”在效率和可预测性之间更平衡。

### 26.9 当前建议结论

当前建议是：

> Provider 在包装目录前保存 picker 文件夹显示名。Setup 首次用该显示名建议 `ProjectName`，为空时使用 `project`；只要用户尚未编辑，重新选择目录就更新建议。一旦用户编辑过项目名称，后续目录切换永不覆盖。默认值非法或冲突时只显示错误，不清洗、不编号。

### 26.10 用户确认

已确认，2026-08-17。

最终规则：

- Provider 在把 picker 文件夹包装为 `ISimpleDirectory` 前，单独保存文件夹显示名。
- Setup 首次使用文件夹显示名建议 `ProjectName`；显示名为空时使用稳定后备值 `project`。
- 用户尚未编辑项目名称时，重新选择目录会用新目录显示名更新建议。
- 用户一旦实际编辑项目名称，包括主动清空，后续目录切换都不得覆盖输入。
- 默认值无效或发生冲突时只显示校验错误，不静默清洗、不自动编号。
- 目录切换后重新计算项目文件名预览、名称冲突和所有资源的目录内/目录外状态。

## 27. 决策 S5：跨平台可移植文件名限制

### 27.1 为什么现在必须确定

Setup 会在用户授权的项目目录中创建或复制多个根级条目：

- `ProjectName.nyagekiProj`。
- 新建谱面的 `NewFumenStem + SelectedFumenFormat.Extension`。
- Existing 模式复制进项目目录的谱面。
- 项目外普通音频的副本。
- ACB 使用外置 AWB 时的 ACB/AWB 文件包。

如果仅依赖当前宿主接受某个名称，会出现以下问题：

- Browser 创建成功，项目复制到 Windows 后无法落盘。
- Linux 上允许的 `bad:name`、`CON.txt` 或尾点名称在 Windows 上失败。
- Windows 的字符长度检查通过，但 UTF-8 文件系统的单段 255 字节限制失败。
- UI 预检通过，直到事务中 `CreateFileAsync` 才以平台异常失败。
- 不同 Provider 各自清洗名称，导致输入框、预览、Manifest locator 和实际文件名不一致。

因此 S5 需要同时决定：

1. Setup 是否采用一套比单个平台更严格的共同名称规则。
2. 长度按什么单位计算。
3. Unicode 是否允许、是否规范化。
4. 首尾空白、尾点、保留设备名和扩展名如何处理。
5. 非法源文件名复制进项目时是否自动改名。
6. 校验器放在何处，如何让 Setup、临时目录和未来导入流程复用。

### 27.2 当前仓库已有的名称安全基线

仓库并非完全没有通用规则。`TemporaryEntryName` 已用于临时文件和临时目录 Provider，当前会拒绝：

- 空名称和纯空白名称。
- 超过 255 个 .NET `char` 的名称。
- 首尾空白。
- `.`、`..` 和 rooted path。
- 尾点。
- 控制字符。
- `<`、`>`、`:`、`"`、`/`、`\`、`|`、`?`、`*`。
- Windows 设备保留名 `CON`、`PRN`、`AUX`、`NUL`、`COM1` 至 `COM9`、`LPT1` 至 `LPT9`，包括 `COM1.txt` 这类带扩展名形式。

对应的 `TemporaryFolderProviderContractTests` 已覆盖 `C:\escape`、嵌套路径、非法字符、尾点、保留名、首尾空白和控制字符。

但它还不能直接作为 Setup 的最终实现：

- 类型是临时目录专用的 `internal static class TemporaryEntryName`，错误通过 `ArgumentException` 英文文本表达。
- 长度只检查 UTF-16 code unit 数量，没有检查 UTF-8 字节数。
- Setup 需要结构化错误码，以便内联显示本地化且可操作的错误。
- Setup 还需要“用户输入的是名称主体，扩展名由系统追加”的领域规则。
- Browser OPFS JavaScript 当前只拒绝空名称、`.`、`..` 和路径分隔符，约束比临时目录基线弱。
- `LocalSimpleDirectory` 和 `AvaloniaStorageProviderSimpleDirectory` 主要依赖底层 Provider 报错，没有形成相同的预检语义。

推荐不要复制 `TemporaryEntryName` 的判断代码，而是抽取一个共享的便携叶级名称校验器，再让临时目录和 Setup 分别调用它。

### 27.3 推荐方向：采用统一的严格共同子集

建议 Desktop、Browser 和测试 Provider 都采用同一套应用级便携规则。平台可以比该规则更宽松，但 Setup 不因此放宽；平台也可能有额外的动态限制，最终创建仍需捕获实际 I/O 错误。

推荐将校验分成两层：

1. **便携叶级名称校验**：验证一个最终文件名或目录名是否可以作为单个项目根级条目。
2. **Setup 名称主体校验**：验证用户是否误填了系统管理的扩展名，然后拼出最终文件名，再运行第一层校验。

这样不会把 `.nyagekiProj`、`.ogkr` 等产品规则塞进通用文件系统工具，也不会让每个字段重复实现 Windows/UTF-8 限制。

### 27.4 推荐的便携叶级名称规则

对每一个准备在 `ProjectDirectory` 中创建的最终叶级名称，建议按以下顺序验证。

#### 27.4.1 必须是一个非空单段名称

拒绝：

- `null`。
- 空字符串。
- 只包含空白的字符串。
- `.` 和 `..`。
- 绝对路径、URI 或 rooted path。
- 任何包含 `/` 或 `\` 的字符串。

Setup 只接受叶级名称，不接受 `charts/master.ogkr` 这类相对路径。首版所有正式角色均位于项目根级；未来如果引入子目录，应由目录模型逐段创建并对每一段独立校验，不能放宽本规则允许路径字符串。

#### 27.4.2 不静默处理首尾空白和尾点

拒绝：

- 任意 Unicode 首部空白。
- 任意 Unicode 尾部空白。
- 以 `.` 结尾的名称。

内部普通空格和内部点号允许，例如：

- `Song 01.nyagekiProj`。
- `Song.v2.ogkr`。

校验器不得先调用 `Trim()` 再接受，也不得删除尾点。输入框、最终预览和实际创建参数必须保持同一字符串。错误信息应要求用户主动修改。

#### 27.4.3 禁止控制字符和 Windows 非法字符

拒绝所有 `char.IsControl(character) == true` 的字符，以及：

~~~text
< > : " / \ | ? *
~~~

这套规则即使在 Linux 或 Browser 上也不放宽。冒号、问号等字符在某些宿主可创建，但会破坏 Windows 可移植性或与路径/流语义冲突。

还建议拒绝格式错误的 UTF-16 序列，例如未配对的高/低代理项。正常 UI 输入不会有意产生这种字符串，但剪贴板、测试或外部调用仍可能传入；明确拒绝比让编码器替换成 U+FFFD 更可预测。

#### 27.4.4 禁止 Windows 设备保留名

按 `StringComparer.OrdinalIgnoreCase` 检查第一个点号之前的名称主体，拒绝：

~~~text
CON  PRN  AUX  NUL
COM1 ... COM9
LPT1 ... LPT9
~~~

因此以下都无效：

- `CON`。
- `con.nyagekiProj`。
- `COM1.txt`。
- `Lpt9.ogkr`。

建议实现时同时补查并覆盖 Windows 仍识别的控制台设备别名和遗留数字写法；这部分应进入参数化测试，而不是依赖当前运行平台的 `Path.GetInvalidFileNameChars()`。名称规则必须在 Browser 测试中也得到相同结果。

#### 27.4.5 同时限制 UTF-16 长度和 UTF-8 字节数

只采用当前 `name.Length <= 255` 不足以保证 UTF-8 文件系统可创建；只采用 UTF-8 字节数也不能覆盖 Windows 的 UTF-16 单段限制。

推荐最终完整文件名同时满足：

~~~text
name.Length <= 255
Encoding.UTF8.GetByteCount(name) <= 255
~~~

这里的 `name` 是已经追加扩展名的最终文件名，而不是只检查输入主体。例如项目名主体的可用上限会自动扣除 `.nyagekiProj` 所占长度。

该规则会使极长中文、日文和 emoji 名称比纯 ASCII 更早到达 UTF-8 上限，但可避免在 UTF-8 单段 255 字节的常见文件系统上创建失败。UI 错误需要显示“最终文件名过长”，并显示最终预览；不建议把字节预算暴露为用户必须手算的规则。

### 27.5 Unicode 规则

建议继续遵循已确认的 Manifest locator 规则 R17：

- 允许中文、日文、韩文、重音字符、emoji 和其他合法 Unicode。
- 保留用户输入和 Provider 返回的原始 Unicode 码点序列。
- 不调用 NFC、NFD、NFKC 或 NFKD 自动规范化。
- 不使用本地文化进行大小写或合法性判断。

例如预组合 `é` 与 `e` 加组合重音符都可以分别成为合法名称，但二者不会因为视觉相似而被自动改写为同一种形式。冲突检查继续使用已确定的 `OrdinalIgnoreCase`；Unicode 规范等价但原始序列不同的名称不视为同名。

原因是：

- 项目 locator 已明确保存实际码点序列，Setup 不应另建不同语义。
- 自动规范化可能使预览名称与底层 Provider 最终返回名称不同。
- ASCII-only 会直接排除项目的主要中文和日文使用场景。

### 27.6 Setup 名称主体规则

#### 27.6.1 ProjectName

用户输入的是项目名称主体，不输入 `.nyagekiProj`。

建议验证流程：

1. 对原始 `ProjectName` 运行叶级名称校验，使首尾空白、尾点和保留名立即报错。
2. 按 `OrdinalIgnoreCase` 检查输入是否已经以 `.nyagekiProj` 结尾；若是，显示“只填写项目名称，不要填写扩展名”。
3. 拼接 `ProjectFileName = ProjectName + ".nyagekiProj"`。
4. 对 `ProjectFileName` 再运行叶级名称校验，尤其检查追加扩展名后的双长度上限。
5. 对项目目录根级执行 `OrdinalIgnoreCase` 文件和目录冲突检查。

允许项目名称主体包含内部点号，例如 `Song.v2`；最终文件名为 `Song.v2.nyagekiProj`。

#### 27.6.2 NewFumenStem

用户输入的是谱面名称主体，扩展名只由当前格式下拉框决定。

建议验证流程：

1. 对原始 `NewFumenStem` 运行叶级名称校验。
2. 收集当前已注册且可写的全部谱面 serializer 扩展名。
3. 若输入按 `OrdinalIgnoreCase` 已以任何受管理谱面扩展名结尾，显示“只填写谱面名称，格式由下拉框决定”。
4. 拼接 `FumenFileName = NewFumenStem + SelectedFumenFormat.Extension`。
5. 对最终名称再次运行叶级名称和目录冲突校验。
6. 在事务权威复查时再次确认该最终文件名仍能取得 serializer。

这可阻止 `master.ogkr` 再选择 OGKR 后生成 `master.ogkr.ogkr`，也避免用户通过手填另一个扩展名绕过格式下拉框。

### 27.7 Existing 谱面、音频和 AWB 的目标名称

S2 已确认项目外资源需要复制进 `ProjectDirectory`，因此源文件名也必须经过目标端规则：

- 源文件已经在项目目录内并保持当前绑定时，不因 Setup 新规则重命名现有文件；打开/绑定阶段只验证其可用性。
- 源文件在项目目录外、准备创建副本时，先以原文件名作为目标建议。
- 建议名称满足便携规则且无冲突时，原样复制。
- 建议名称非法、过长或冲突时，不自动替换字符、不截断、不编号；Setup 要求用户明确填写新的目标名称主体。
- 用户改名只影响项目内副本，绝不重命名、覆盖或删除外部源文件。

ACB 与外置 AWB 的配对命名还涉及解码器要求，应在 S7 单独确认。S5 只确定两个最终叶级名称都必须通过同一便携校验，不能因为它们是导入文件就绕过规则。

### 27.8 建议的共享校验器形状

建议在共享 Avalonia 项目中新增不依赖 UI 和本地化资源的纯校验器，例如：

~~~csharp
internal enum PortableEntryNameError
{
    None,
    Empty,
    InvalidUnicode,
    TooLongUtf16,
    TooLongUtf8,
    LeadingOrTrailingWhitespace,
    DotSegment,
    RootedOrMultiSegment,
    TrailingPeriod,
    InvalidCharacter,
    ReservedDeviceName
}

internal readonly record struct PortableEntryNameValidationResult(
    PortableEntryNameError Error,
    char? InvalidCharacter = null);

internal static class PortableEntryNameValidator
{
    public static PortableEntryNameValidationResult Validate(string? name);
    public static void ThrowIfInvalid(string? name, string parameterName);
}
~~~

职责边界：

- `Validate` 返回稳定错误码，不返回本地化文本。
- Setup ViewModel 把错误码映射为中文、英文和日文表单文案。
- `ThrowIfInvalid` 给临时目录和内部防御式检查复用。
- `TemporaryEntryName` 可保留为薄包装，避免一次无关重命名扩大改动，也可以在独立重构中迁移为直接调用共享校验器。
- `.nyagekiProj` 和谱面扩展名判断放在 Setup 领域验证器，不放进便携名称校验器。
- 冲突枚举和 `OrdinalIgnoreCase` 比较仍由目录级验证服务负责，不属于纯字符串校验器。

不建议让公共校验器抛出带最终用户文案的 `ArgumentException`，否则 UI 只能显示英文内部异常，也难以精确聚焦对应输入框。

### 27.9 UI 错误和提交时行为

建议区分三类反馈：

1. **纯名称错误**：输入时内联显示，创建按钮禁用；焦点保持在对应名称输入框。
2. **目录快照冲突**：内联显示实际冲突条目名；用户点击创建时若并发产生新冲突，再弹错误对话框并保留全部输入。
3. **实际 Provider/I/O 错误**：即使便携预检通过，底层仍可能因权限、配额、文件系统状态或平台额外限制失败；事务回滚本轮产物，显示归类后的错误并记录诊断日志。

文案应指出具体修正方向，例如：

- “项目名称不能以空格开头或结尾。”
- “最终文件名包含 Windows 不支持的字符 `:`。”
- “`COM1` 是保留设备名，请使用其他名称。”
- “最终文件名过长，请缩短名称。”
- “不要在谱面名称中填写 `.ogkr`；请在格式列表中选择格式。”

不提供“自动修复”按钮。若未来确实需要批量导入自动清洗，应设计为独立、可预览并由用户确认的映射流程，不能改变 Setup 的明确命名契约。

### 27.10 代表性用例

| 输入或最终名称 | 预期 | 原因 |
|---|---|---|
| `初音ミク` | 允许作为主体 | 合法 Unicode，系统随后追加扩展名。 |
| `Song 01` | 允许作为主体 | 内部空格允许。 |
| `Song.v2` | 允许作为主体 | 内部点号允许。 |
| `.draft` | 允许作为主体 | 不是 `.`/`..`，也不以点结尾；在类 Unix 平台可能呈现为隐藏文件。 |
| ` Song` | 拒绝 | 首部空白，不静默 Trim。 |
| `Song ` | 拒绝 | 尾部空白。 |
| `Song.` | 拒绝 | 尾点。 |
| `parent/song` | 拒绝 | 不是单段名称。 |
| `bad:name` | 拒绝 | 包含共同禁用字符 `:`。 |
| `CON` | 拒绝 | Windows 设备保留名。 |
| `COM1.mix` | 拒绝 | 保留名即使带扩展名也无效。 |
| `master.ogkr` 作为 NewFumenStem | 拒绝 | 用户不得手填受管理谱面扩展名。 |
| `song.nyagekiProj` 作为 ProjectName | 拒绝 | 用户不得手填工程扩展名。 |
| 最终名 UTF-8 编码为 256 字节 | 拒绝 | 超过推荐的便携单段字节上限。 |
| 预组合 `é` 与分解形式 `e + ◌́` | 分别允许且不互相规范化 | 与 R17 的原始码点规则一致。 |

### 27.11 测试要求

共享校验器至少覆盖：

- 空、纯空白、`.`、`..`、rooted path 和两种路径分隔符。
- 每一个共同禁用字符和多种控制字符。
- ASCII、中文、日文、emoji、组合字符和未配对代理项。
- 首尾 ASCII 空格、制表符、换行和其他 Unicode 空白。
- 尾点、内部点、前导点和多个内部点。
- 全部保留设备名的大小写变体和带扩展名形式。
- UTF-16 254/255/256 边界。
- UTF-8 254/255/256 字节边界。
- 同一个用例在 InMemory、Desktop 和 Browser 相关契约测试中得到相同结果。

Setup 领域测试至少覆盖：

- 项目扩展名手填的大小写变体。
- 当前 serializer 扩展名和其他已注册 serializer 扩展名的手填。
- 追加扩展名前合法、追加后超过长度上限。
- S4 自动建议得到非法名称时保留文本并显示错误。
- 目录切换后重新校验名称，但不覆盖用户编辑。
- 外部源文件名非法时要求目标改名，源文件不变。
- 提交前最后复查仍使用同一规则。

迁移 `TemporaryEntryName` 后应保留现有契约测试，并新增 UTF-8 字节上限和结构化错误结果测试，防止临时目录与 Setup 再次分叉。

### 27.12 其他方案及其代价

#### 方案 B：仅按当前平台校验

Desktop 调用 `Path.GetInvalidFileNameChars()`，Browser/OPFS 只禁止路径分隔符，Linux 依赖本地文件系统。

优点是对当前平台限制最少；代价是同一个项目在另一平台可能无法复制、恢复或保存，且测试结果依赖运行机器。该方向与 S3 的共享架构和项目可搬运目标不一致。

#### 方案 C：自动清洗并生成可用名称

例如把非法字符替换为 `_`、Trim、删除尾点、截断过长名称，并对冲突自动追加编号。

优点是用户更少遇到阻塞；代价是实际文件名不再由用户明确决定，也会破坏 D5/D7 已确认的“不覆盖、不自动编号”和 S4 已确认的“默认值非法时不清洗”。不建议采用。

#### 方案 D：只允许 ASCII

规则最容易跨平台，但会拒绝中文、日文及大量现有歌曲名，不符合实际用户场景，也与 R17 已确认允许 Unicode 的方向冲突。

### 27.13 当前建议结论

当前建议是：

> Setup 采用统一的跨平台便携叶级名称规则，并从现有 `TemporaryEntryName` 抽取共享结构化校验器。允许合法 Unicode、内部空格和内部点号；保留原始码点，不做 NFC/NFD。拒绝首尾空白、尾点、控制字符、路径/Windows 非法字符、`.`/`..`、Windows 设备保留名和格式错误 Unicode；最终完整文件名同时限制为最多 255 个 UTF-16 code unit 和最多 255 个 UTF-8 字节。用户只填写名称主体，系统追加扩展名；非法、过长或冲突时只提示并要求明确修改，不 Trim、不替换、不截断、不自动编号。

### 27.14 用户确认

已确认，2026-08-17。

最终规则：

- Setup、Desktop、Browser 和临时目录采用同一套便携叶级名称基线。
- 允许合法 Unicode、内部空格、内部点号和前导点；保留原始码点，不做 NFC/NFD 等规范化。
- 拒绝首尾空白、尾点、控制字符、路径/Windows 非法字符、`.`/`..`、Windows 设备保留名和格式错误 Unicode。
- 最终完整文件名同时不得超过 255 个 UTF-16 code unit 和 255 个 UTF-8 字节。
- 用户只填写名称主体，工程或谱面扩展名由系统唯一追加。
- 非法、过长或冲突时要求用户明确修改，不 Trim、不替换、不截断、不自动编号。
- 从现有 `TemporaryEntryName` 抽取共享结构化校验器，Setup 负责把错误码映射为本地化文案。

## 28. 决策 S6：新谱面的默认格式、必填初始 BPM 和初始内容

### 28.1 为什么这些初始规则必须作为一个整体确定

CreateNew 分支不是只在目录中写一个空文件。它必须生成一个能被编辑器立即加载、保存并在重启后重新打开的完整空白谱面。因此以下内容相互依赖：

- `NewFumenStem` 的初始建议。
- 默认选择哪个 serializer 扩展名。
- 格式列表是否只要求可写，还是还必须可重新读取。
- `BaseBpm` 是否预填，以及允许范围。
- `FumenMetaInfo.BpmDefinition` 的四个字段如何同步。
- `ProgJudgeBpm` 是否跟随输入、`BpmList.FirstBpm` 和零时刻 BPM 对象如何初始化。
- 4/4 拍号、默认变速和其他模型默认值是否由 Setup 重复创建。

如果只修改 `MetaInfo.BpmDefinition.First`，空白谱面会同时保存用户输入 BPM 和仍为 240 的其他 BPM 元数据。若又手工添加一个 BPM 或 Meter 对象，还可能与 `OngekiFumen` 构造器自带的哨兵重复。

### 28.2 当前代码事实

#### 28.2.1 当前 New 入口仍未接线

`FumenVisualEditorProvider.CanCreateNew` 当前为 `false`，`FumenVisualEditorViewModel.New()` 只记录“不支持新建”并返回 `false`。

仓库中虽然存在 `EditorProjectSetupDialogViewModel` 和 `EditorProjectSetupDialogView.axaml`，但它们是旧表单壳：

- 只选择音频和可选已有谱面。
- 没有项目目录、项目名称、谱面名称或格式选择。
- BPM 使用普通 `TextBox`，没有完整数值校验。
- `Create` 只检查音频存在并关闭对话框。
- 生产 Provider 没有实例化该 ViewModel。
- 没有创建工程文件、空白谱面、资源副本或事务回滚。

因此 S6 应指导重构现有 Setup 类型或用新的 Session/ViewModel 替换其行为，不能把旧 UI 当前显示什么视为已实现契约。

#### 28.2.2 当前可写谱面格式

共享 Core 当前注册两个内置 serializer：

| 格式 | 扩展名 | serializer | deserializer |
|---|---|---|---|
| Ongeki Fumen File | `.ogkr` | 有 | 有 |
| Nyageki Fumen File | `.nyageki` | 有 | 有 |

`IFumenParserManager.GetSerializerDescriptions()` 返回 serializer 注册枚举，但当前没有声明稳定排序。`GetSerializer(fileName)` 和 `GetDeserializer(fileName)` 采用 `FirstOrDefault`，因此 Setup 不得用“注册列表第一项”隐式决定默认格式。

#### 28.2.3 当前模型默认 BPM 是 240

以下位置都把 240 作为默认：

- `FumenMetaInfo.BpmDef.First`。
- `Common`、`Minimum`、`Maximum`。
- `FumenMetaInfo.ProgJudgeBpm`。
- `BpmList.DefaultFirstBpm`。
- `BPMChange` 的初始值。
- 旧 Setup 新建的默认 `EditorContext.Fumen`。

`OngekiFumen` 构造时还会：

- 在 TGrid 0 建立首个 BPM 对象。
- 建立首个 4/4 Meter。
- 建立默认 Soflan group，并在 TGrid 0 放置速度 1 的 KeyframeSoflan。
- 建立默认 Soflan group 的显示包装项。

这些都是空白谱面正常工作的模型哨兵，不应由 Setup 再添加一份。

仓库还保留 `Resources/empty_ogkr_template.ogkr`，其历史提交说明用途是生成 `Music.xml` 时提供空谱面对比；当前源码没有引用该文件，而且其中 `BPM_DEF` 为 1、`PROGJUDGE_BPM` 为 240，本身不是一致的新建工程模板。Setup 不得读取或复制该资源作为新谱面内容，应从当前 `OngekiFumen` 模型和所选 serializer 生成。

#### 28.2.4 当前 OGKR BPM_DEF 存在字段顺序差异

当前 OGKR formatter 写出：

~~~text
BPM_DEF First Common Maximum Minimum
~~~

但 `BpmDefinitionCommandParser` 读取时赋值为：

~~~text
First  = data[1]
Common = data[2]
Minimum = data[3]
Maximum = data[4]
~~~

这会在 `Minimum != Maximum` 时把两者交换。空白谱面把 First/Common/Minimum/Maximum 全部设置为同一个 BPM，因此普通空白 round-trip 无法暴露该问题。

S6 实现前应先统一 formatter/parser 的正式字段顺序，并用 Minimum 与 Maximum 不相等的独立回归用例固定。该修复属于谱面格式层，不应通过在 Setup 中反向赋值来补偿，否则 Nyageki、内存模型和后续格式修复会再次错位。

### 28.3 推荐的谱面名称初始值

建议 CreateNew 分支首次启用时：

1. 使用当前 `ProjectName` 作为 `NewFumenStem` 建议值。
2. 若 `ProjectName` 尚处于 S4 自动建议状态，谱面名称也标记为自动建议。
3. 用户尚未编辑谱面名称时，项目名称变化同步更新 `NewFumenStem`。
4. 用户一旦实际编辑谱面名称，包括清空，后续项目名称或目录变化都不覆盖它。
5. Existing/CreateNew 模式切换时保留各自输入，切回 CreateNew 后恢复原状态。

建议使用独立来源状态，不能复用项目名称状态：

~~~csharp
private enum FumenNameOrigin
{
    SuggestedFromProjectName,
    UserEdited
}
~~~

示例：

~~~text
项目名称自动建议：MySong
谱面名称自动建议：MySong

用户将谱面名称改为：master
用户随后将项目名称改为：MySongRemake

最终：
  工程文件 MySongRemake.nyagekiProj
  谱面文件 master.ogkr
~~~

这延续 S4 的“先提供有效建议，用户编辑后不覆盖”，同时允许工程名和谱面名承担不同含义。

### 28.4 推荐的格式选项生成规则

#### 28.4.1 一个具体扩展名对应一个格式选项

serializer 可能声明多个扩展名，未来也可能出现 `.chart.json` 这类复合扩展。建议格式选项模型明确保存最终扩展名：

~~~csharp
internal sealed record FumenFormatOption(
    string DisplayName,
    string Extension);
~~~

生成时：

1. 读取 `GetSerializerDescriptions()`。
2. 将每个 serializer 的每个扩展名展开为独立选项。
3. 统一要求扩展名以 `.` 开头，但不改变其余字符。
4. 按 `OrdinalIgnoreCase` 去重并检测冲突。
5. 用 `GetSerializer("probe" + extension)` 再次确认可写。
6. 用 `GetDeserializer("probe" + extension)` 确认新建后可以重新打开。
7. 只把同时可写和可读的扩展名列为“项目工作格式”。

仅 serializer 可用、没有 deserializer 的格式应属于“导出格式”，不能出现在新项目的主谱面格式列表中，否则本次创建成功但下次打开必然失败。

#### 28.4.2 显式优先 `.ogkr`

推荐首版默认选择 `.ogkr`，理由是：

- 当前项目文档、Fast Open 和标准化输出大量以 `.ogkr` 作为主工作示例。
- `.ogkr` 是当前 Ongeki 谱面交换和标准化流程的直接格式。
- 当前 OGKR serializer/deserializer 已覆盖空白谱面需要的 BPM、Meter、Soflan 和编辑器扩展对象。
- Nyageki 仍作为用户可见的明确选择保留。
- 显式按扩展名选择不会受 DI 注册顺序变化影响。

推荐顺序：

1. `.ogkr` 存在且读写完整时，默认选择 `.ogkr`。
2. `.ogkr` 不可用时，按扩展名 `OrdinalIgnoreCase` 稳定排序后选择第一项。
3. 没有任何同时可读写的格式时，CreateNew 模式显示阻塞错误并禁用创建。

不要把“上次使用的格式”作为首版默认。记忆格式需要新增设置持久化、无效旧扩展回退和多平台同步规则，也会让同一操作在不同机器上产生不同默认结果。后续可作为独立体验增强。

### 28.5 已确认的初始 BPM 行为

首版不为 `BaseBpm` 提供任何自动默认值：

- 首次进入 CreateNew 时，输入值为 `null`/空。
- 用户必须手动填写初始 BPM，填写前 `CanCreate == false`。
- 不读取模型当前的 240 作为表单预填值。
- 不记住上一次创建项目时的 BPM。
- 不根据音频长度、文件名或波形猜测 BPM。

这里需要区分“表单没有默认值”和“领域模型仍有内部默认值”。`new OngekiFumen()` 的 `ProgJudgeBpm` 仍由模型初始化为当前默认值 240，但该值不显示、不编辑，也不复制到 `BaseBpm` 输入框。用户填写的初始 BPM 只负责初始化实际谱面速度相关的五个字段。

### 28.6 推荐的 BPM 输入规则

建议使用支持空值和内联验证的数值输入控件。首版业务规则只有：

~~~text
BaseBpm 已填写
double.IsFinite(BaseBpm.Value)
BaseBpm.Value > 0
~~~

说明：

- 空值、0、负数、`NaN` 和正负无穷一律无效。
- 正的小数 BPM 有效，不限制为整数，也不固定最多 3 位小数。
- 首版不设置 0.001 下限或 9999 上限；不要把控件的技术取值范围描述成产品 BPM 规则。
- 若使用 `NumericUpDown`，其 `Value` 必须允许为空；步进值只影响按钮操作，不改变允许输入的精度。
- 若该控件基于 `decimal?`，转换成 `double` 后仍须执行有限值和 `> 0` 复查。若控件本身会拒绝业务上本应允许的正有限值，应改用带可靠 double 解析和内联验证的数值文本输入，而不是偷偷增加产品边界。
- Existing 分支不允许在 Setup 中修改已有谱面的 BPM，也不显示 First/Common/Minimum/Maximum、`BpmList.FirstBpm` 或 `ProgJudgeBpm` 输入项。
- 所选 serializer/deserializer 无法 round-trip 某个正有限值时，创建前自检必须显示“该谱面格式无法保存此 BPM”并阻止提交；不得钳制、四舍五入后继续或回退到 240。

Selection 和创建服务必须重新解析并验证最终 `double`，不能只依赖 UI 控件阻止非法调用。

### 28.7 空白谱面的权威初始化步骤

建议用一个纯方法集中创建空白谱面：

~~~csharp
internal static OngekiFumen CreateBlankFumen(double baseBpm)
{
    ValidateBaseBpm(baseBpm);

    var fumen = new OngekiFumen();
    fumen.MetaInfo.BpmDefinition.First = baseBpm;
    fumen.MetaInfo.BpmDefinition.Common = baseBpm;
    fumen.MetaInfo.BpmDefinition.Minimum = baseBpm;
    fumen.MetaInfo.BpmDefinition.Maximum = baseBpm;
    fumen.BpmList.FirstBpm = baseBpm;
    return fumen;
}
~~~

必须同步设置：

- `MetaInfo.BpmDefinition.First`。
- `Common`。
- `Minimum`。
- `Maximum`。
- `BpmList.FirstBpm`。

`Minimum` 和 `Maximum` 在只有一个 BPM 的空白谱面中都等于 `baseBpm`。不能把 Minimum/Maximum 留作 240，也不能交换二者；OGKR header 的字段顺序和解析必须通过 round-trip 测试确认。

`MetaInfo.ProgJudgeBpm` 特意不出现在赋值代码中。它保持 `new OngekiFumen()` 创建时的模型默认值，当前为 240；Setup 既不显示这个字段，也不把它同步成用户初始 BPM。为了避免未来模型默认值调整后 Setup 仍硬编码旧值，创建服务不得再次写入字面量 240。

### 28.8 不应由 Setup 重复设置的模型默认值

以下值建议继续由 `OngekiFumen`/`FumenMetaInfo` 构造器拥有，Setup 不提供首版输入项：

- Meter：4/4。
- `TRESOLUTION = 1920`。
- `XRESOLUTION = 4096`。
- `ClickDefinition = 1920`。
- `ProgJudgeBpm`：沿用模型默认值，当前为 240。
- 默认 Soflan group 0。
- TGrid 0、速度 1 的默认 KeyframeSoflan。
- `Tutorial = false`。
- Bullet/Beam damage 默认值。
- 空 Creator。
- 空的轨道、音符、子弹、铃铛、注释和 SVG 集合。

特别注意：

- 不再调用 `fumen.AddObject(new BPMChange(...))` 创建第二个首 BPM。
- 不再手工 `SetFirstMeter` 创建第二个 4/4 Meter。
- 不再向默认 Soflan list 添加第二个速度 1 哨兵。
- 不读取操作系统用户名填充 Creator，避免隐式写入个人信息。
- 格式版本由 serializer 的格式契约负责；Setup 不根据下拉框手工猜写 Header Version。

如果未来希望在 Setup 暴露拍号、Creator 或难度元数据，应作为独立需求加入，不应扩大首版表单。

### 28.9 创建前的格式自检

用户点击创建后，在创建任何目标文件之前执行：

1. 根据选项重新组合最终 `FumenFileName`。
2. 再次调用 `GetSerializer(FumenFileName)`。
3. 再次调用 `GetDeserializer(FumenFileName)`。
4. 创建内存中的空白 `OngekiFumen`。
5. serializer 输出 `byte[]`。
6. 使用同格式 deserializer 从该内存字节重新解析。
7. 验证关键初始化语义后再进入目标文件创建事务。

建议至少验证：

- First/Common/Minimum/Maximum 都等于用户 BPM。
- `BpmList.FirstBpm` 等于用户 BPM。
- `ProgJudgeBpm` 等于创建空白模型时捕获的默认值，当前为 240，而不是用户 BPM。
- 首个 Meter 为 4/4 且位于 TGrid 0。
- 默认 Soflan 速度为 1。

这一步不会写磁盘，能在事务开始前发现 serializer 注册错误、格式不支持或初始模型无法 round-trip。对于内置格式可保持快速；若未来某插件格式代价较高，再单独定义 capability，不应先跳过正确性验证。

### 28.10 UI 行为

CreateNew 分支建议显示：

- 谱面名称主体输入框。
- 格式下拉菜单，条目显示 `Ongeki Fumen File (.ogkr)` 等完整信息。
- 最终文件名只读预览。
- 必填的“初始 BPM”数值输入；首次显示为空。

行为规则：

- 修改谱面名称或格式后立即刷新预览并运行 S5 校验。
- 格式下拉框不能显示 serializer 注册顺序造成的随机默认项。
- 只有一个格式时仍显示只读格式值，让用户知道最终扩展名来源。
- CreateNew 不显示 First、Common、Minimum、Maximum、`BpmList.FirstBpm` 和 `ProgJudgeBpm` 的独立字段；这些值按本节规则内部初始化。
- Existing 分支隐藏新建名称、格式和初始 BPM 输入，只显示已选谱面的基本文件信息。
- 模式切换不销毁另一分支输入；最终 Selection 只转交当前分支。
- 初始 BPM 为空或无效时内联显示错误并禁用创建，不在用户每次输入时弹对话框。

### 28.11 建议的数据模型

Setup Session/ViewModel 可以新增：

~~~csharp
public IReadOnlyList<FumenFormatOption> FumenFormatOptions { get; }
public FumenFormatOption? SelectedFumenFormat { get; set; }
public string NewFumenStem { get; set; }
public string FumenFileNamePreview { get; }
public double? BaseBpm { get; set; }
public bool IsFumenNameUserEdited { get; }
~~~

`EditorProjectSetupSelection` 在 CreateNew 模式携带不可变快照：

~~~csharp
public sealed record CreateNewFumenSelection(
    string Stem,
    string Extension,
    string FinalFileName,
    double BaseBpm);
~~~

创建服务不得在提交时重新读取可变 ViewModel 属性；它只使用用户确认时生成且再次验证的 Selection。

### 28.12 测试要求

格式选项测试：

- serializer 注册顺序变化时默认仍是 `.ogkr`。
- `.ogkr` 缺失时采用按扩展名稳定排序的第一项。
- serializer-only 格式不进入项目工作格式列表。
- deserializer-only 格式不进入列表。
- 多扩展 serializer 展开为多个明确选项。
- 复合扩展名完整保留。
- 大小写重复扩展或歧义注册被明确拒绝，不任意选择。
- 无可读写格式时 CreateNew 被禁用。

默认值和联动测试：

- 首次进入 CreateNew 时 `NewFumenStem == ProjectName`。
- 项目名变化在谱面名未编辑时同步。
- 谱面名编辑后不再被项目名覆盖。
- Existing/CreateNew 往返保留各自状态。
- 初始格式为 `.ogkr`，初始 BPM 为空，且填写前不能创建。

BPM 边界测试：

- 多个正整数和正小数有效，包括小于 0.001 和大于 9999 的可 round-trip 样例。
- 0、负数、`NaN`、正无穷和负无穷无效。
- 不因小数位数超过 3 位而单独拒绝；格式不能保留该值时以格式自检错误阻止创建。
- 追加格式扩展名后名称仍执行 S5 长度和字符校验。

空白谱面测试：

- 内存对象的 First/Common/Minimum/Maximum 和 `BpmList.FirstBpm` 五处值都等于用户输入。
- `ProgJudgeBpm` 保持模型默认值 240；使用非 240 的初始 BPM 测试，确保它没有被误同步。
- 只有一个首 BPM 哨兵。
- 只有一个首 Meter，值为 4/4。
- 默认 Soflan group 只有构造器要求的初始速度 1 哨兵。
- OGKR serialize/deserialize 后关键语义一致。
- Nyageki 被用户选择时同样能 round-trip。
- serializer 或 deserializer 失败时没有创建任何目标文件。
- 使用不同的 Minimum/Maximum 构造独立 OGKR `BPM_DEF` 回归，确认 formatter/parser 不再交换字段；不能只依赖五处值相同的空白谱面测试。

### 28.13 其他方案及其代价

#### 方案 B：默认 `.nyageki`

Nyageki 是当前受支持的完整可读写格式，也可以作为用户明确选择。把它设为默认会改变文档和既有 `.ogkr` 工作流的惯例，但仓库没有设置或历史行为要求这一变化。除非产品希望新项目优先使用 Nyageki，否则不建议首版切换。

#### 方案 C：使用注册列表第一项

实现最少，但默认结果依赖 DI 生成和注册顺序；添加插件或调整源码顺序后可能静默改变。该行为不可作为用户数据格式决策。

#### 方案 D：记住上次格式和 BPM

对重复创建相似项目的用户更方便，但需要定义设置作用域、跨平台同步、已卸载格式回退、无效 BPM 迁移和“恢复默认”入口。建议在首版稳定后单独实现。

#### 方案 E：自动预填任意 BPM

无论预填 120、240 还是上一次使用值，都会让用户在未确认歌曲速度时直接创建，并容易把表单默认误当成已测得 BPM。本次决定要求用户主动填写，所以首版不预填任何 BPM。模型内部的 `ProgJudgeBpm = 240` 是未暴露的领域默认值，不构成表单默认。

### 28.14 当前建议结论

当前建议是：

> CreateNew 首次用 `ProjectName` 建议谱面名称，用户编辑后停止跟随。格式选项只包含同时有 serializer 和 deserializer 的具体扩展名；显式以 `.ogkr` 为默认，不依赖注册顺序，`.nyageki` 保持可选。初始 BPM 不预填，用户必须手动输入正有限值；首版不另设 0.001、9999 或 3 位小数限制。界面不显示 First/Common/Minimum/Maximum、`BpmList.FirstBpm` 或 `ProgJudgeBpm`。创建空白 `OngekiFumen` 后，把用户值同步到前五个速度字段；`ProgJudgeBpm` 保持模型默认值，当前为 240。保留构造器的 4/4 Meter、速度 1 Soflan 和其他默认值，不重复添加哨兵。写文件前在内存中完成同格式 serialize/deserialize 自检。

### 28.15 用户确认

已确认，2026-08-17。

最终规则：

- 用户必须手动填写初始 BPM，初始输入为空。
- 初始 BPM 必须是有限数字且大于 0；不增加固定上下限或小数位数限制。
- First、Common、Minimum、Maximum 和 `BpmList.FirstBpm` 不在 UI 中分别显示，创建时全部使用用户输入值。
- `ProgJudgeBpm` 不显示、不从用户输入派生，保持模型默认值，当前为 240。
- S6 的谱面名称联动、`.ogkr` 默认格式、可读写格式过滤、模型哨兵复用和创建前 round-trip 自检按本节其余推荐实施。

## 29. 决策 S7：ACB/AWB 导入时的目标命名和冲突交互

### 29.1 为什么普通音频规则不能直接套到外置 AWB

S2 已确认项目外音频要复制到 `ProjectDirectory` 根级，S5 已确认非法或冲突的普通源文件名不能自动清洗、截断或编号。但 ACB 还有一层包关系：

- ACB 可能内置 AWB，此时只有一个文件。
- ACB 也可能声明一个外置 AWB 文件名，此时两个文件共同构成可播放资源。
- ACB 自身的文件名通常不被 ACB 内部引用，可以为项目副本改名。
- 外置 AWB 的目标文件名受 ACB 声明约束，随意改名会使按文件夹重新打开时无法自动配对。
- 两个文件可能一个已在项目目录内、另一个仍在目录外。
- 同名目标可能被已有文件或目录占用，而且 Setup 不能覆盖或猜测复用。

因此 S7 需要同时决定普通音频、internal-AWB ACB 和 external-AWB ACB 的目标名称可编辑性、冲突处理和事务原子性。

### 29.2 当前代码能够确定的事实

#### 29.2.1 当前 Provider 的自动绑定流程

`FumenVisualEditorProvider.TryBindExternalAwbAsync` 当前会：

1. 只对 `.acb` 执行包检查。
2. 使用 `AcbFile` 解析 ACB。
3. 有 `InternalAwb` 时直接通过。
4. 否则读取 `acb.ExternalAwb.FileName`。
5. 在 ACB 的父目录中按 `OrdinalIgnoreCase` 查找同名文件。
6. 找不到时打开 AWB picker；多个同名候选时拒绝猜测。

但当前手动 picker 返回后直接写入 `context.AudioAwbFile`，没有验证用户选择的文件名是否等于声明名，也没有在绑定阶段证明这组 ACB/AWB 能成功解码。`EditorProjectDataUtils` 当前也主要检查 AWB 已绑定且提供 `LocalPath`，并不建立可移植的项目内目标名称契约。

#### 29.2.2 解码和重新打开使用了不同层次的关系

`NAudioManager.LoadProjectAudioAsync` 会把已验证的 ACB 路径和 AWB 路径显式传给 `AcbConverter.ConvertAcbFileToWavFile`。因此：

- 当前解码器不要求 ACB 自身必须保留原文件名。
- 已明确绑定 AWB 时，单次解码可以使用与 ACB 声明名不同的源文件路径。
- 但文件夹打开、工程重新绑定和丢失书签后的恢复仍需要根据 ACB 声明在项目目录中找到 AWB。

所以“这次能通过显式路径解码”不等于“项目复制后能够稳定重开”。项目内副本仍必须恢复 ACB 声明的同级 AWB 名称关系。

#### 29.2.3 `ExternalAwb.FileName` 不一定天然是叶级名称

现有集成测试表明，使用 `FileStream` 解析时，`AcbFile.ExternalAwb.FileName` 可能是解析后的完整路径，而不只是 `music0001.awb`。因此实现不能直接：

~~~csharp
targetAwbFileName = acb.ExternalAwb.FileName;
~~~

也不能无条件调用 `Path.GetFileName` 后接受，因为这会把 ACB 真正声明的嵌套路径静默压平成根级文件名。需要一个明确的 ACB 包检查器区分：

- ACB 声明的是同目录叶级 AWB。
- 解析库把同目录叶级声明解析成了绝对路径。
- ACB 真正要求嵌套目录、其他目录或无法判定的路径关系。

#### 29.2.4 平台边界

- Desktop 当前有本地路径和 ACB 解码能力，可以启用本节流程。
- Browser 当前明确不支持 ACB；picker 过滤器应隐藏 `.acb`，绕过过滤器传入时仍要返回可操作错误。
- 本节先定义共享数据模型和事务语义，但 Browser 在具备真实 ACB 解码能力前不显示 ACB/AWB UI。

### 29.3 推荐的三类音频导入模型

建议在选择音频后立即分类，不要等到用户点击创建才发现包类型：

~~~csharp
internal enum SetupAudioPackageKind
{
    OrdinaryAudio,
    AcbWithInternalAwb,
    AcbWithExternalAwb
}
~~~

三类规则：

| 类型 | 项目内正式文件 | 可否修改主音频副本名 | AWB 目标名 |
|---|---|---|---|
| 普通音频 | 一个音频文件 | 可以，用户显式修改名称主体 | 无 |
| internal-AWB ACB | 一个 ACB 文件 | 可以，用户显式修改名称主体 | 无独立 AWB |
| external-AWB ACB | 一个 ACB + 一个 AWB | ACB 可以独立改名 | 必须使用 ACB 声明的同级叶级名，只读 |

“可以改名”只表示项目外源文件需要复制时，可以修改项目内副本名。项目目录内已经绑定的正式文件不因 Setup 新规则被重命名。

### 29.4 外置 AWB 声明的安全解析规则

建议新增一个无 UI 的 Desktop ACB 包检查服务，输出不可变结果：

~~~csharp
internal sealed record AcbPackageInspection(
    SetupAudioPackageKind Kind,
    string? RequiredExternalAwbLeafName,
    AcbExternalAwbReferenceError Error);
~~~

对于 external AWB，按以下顺序提取目标叶级名：

1. 要求源 ACB 具有可用 `LocalPath`，并取得其规范化父目录。
2. 读取解析库返回的 external AWB 文件名或路径。
3. 返回值本身是无 `/`、无 `\`、非 rooted 的单段名称时，直接作为声明叶级名。
4. 返回值是完整路径时，规范化后要求其父目录与源 ACB 的父目录表示同一目录；这覆盖解析库把“同级 AWB”展开成完整路径的情况，然后提取叶级名。
5. 返回值是包含目录段的相对路径、指向其他父目录的完整路径、URI、空值或无法稳定比较的路径时，首版标记为不支持。
6. 提取出的叶级名必须以 `.awb` 结尾，并在需要创建项目内副本时通过 S5 便携叶级名称校验。

比较源本地目录时使用宿主文件系统适用的路径比较规则；比较项目目录中的叶级条目继续使用已确认的 `OrdinalIgnoreCase`。不能把本地完整路径保存进 Selection、工程数据模型或最近记录。

首版只支持 ACB 与外置 AWB 为同目录兄弟文件，原因是 S2 已确定所有正式角色位于 `ProjectDirectory` 根级，当前自动绑定也以同级查找为基础。遇到真实的嵌套 AWB 声明时，应明确提示“首版只支持与 ACB 同目录的外置 AWB”，不能静默压平路径。

### 29.5 普通音频和 internal-AWB ACB 的目标命名

源音频位于项目目录外、需要复制时：

1. 默认目标文件名使用源 `FileName` 原样建议。
2. UI 显示“项目内音频文件名”，用户编辑名称主体，扩展名由已选源格式固定。
3. 不允许用户仅通过改扩展名把一种编码伪装成另一种编码。
4. 默认名满足 S5 且无冲突时可以直接使用；用户也可以主动填写另一个合法名称主体。
5. 默认名非法或冲突时，内联指出原因并要求用户明确修改；不自动替换字符、不截断、不追加 `(1)` 或数字。
6. internal-AWB ACB 按单文件音频处理，扩展名固定为 `.acb`。

ACB 自身目标名可以修改，因为 ACB 内部外置引用指向 AWB，不引用 ACB 自身；仍需在最终复制后用实际项目内路径完成解码验证。

### 29.6 external-AWB ACB 的目标命名

建议把 external-AWB ACB 作为一个不可分割的双文件包显示和提交，但两个目标名的可编辑性不同。

#### 29.6.1 ACB 主文件名

- 项目外 ACB 的默认目标名使用源 ACB 文件名。
- 用户可以显式修改 ACB 名称主体，扩展名固定为 `.acb`。
- ACB 名称非法或冲突时，处理方式与普通音频相同。
- 修改 ACB 目标名不联动修改 AWB 名，因为两者不一定共享相同 stem，且 ACB 内部引用的权威值是 AWB 声明名。

#### 29.6.2 AWB 文件名

- 项目内目标名固定为 `RequiredExternalAwbLeafName`，在 UI 中只读显示。
- Setup 不提供 AWB 目标名编辑框，不自动编号，也不跟随 ACB stem 改名。
- 用户手动选择的源 AWB 可以位于其他目录，源叶级名也可以不同；复制时始终使用 ACB 声明的目标叶级名。
- 当源 AWB 名与声明名不同时，UI 必须明确显示“选中的源文件”和“项目内将保存为”，并在写文件前对这对 ACB/AWB 做真实解码预检。
- 如果声明的 AWB 叶级名违反 S5，首版阻止导入。用户需要选择其他 ACB 包或先在外部工具中正确重建包；Setup 不修改 ACB 二进制元数据。

允许源 AWB 名不同的原因是显式选择和真实解码可以证明用户选中了可用数据，项目内复制又会恢复声明要求的正式名称。仅比较源文件名不能证明内容正确，也会不必要地拒绝已经被用户改名但仍有效的 AWB。

### 29.7 项目目录内外的组合规则

最终目标是 ACB 和必要 AWB 都成为 `ProjectDirectory` 根级角色，但不要求两个源文件一开始处于同一位置：

| ACB 位置 | AWB 位置 | 处理 |
|---|---|---|
| 两者都在项目目录内 | 两者名称关系正确且解码通过时直接绑定，不复制 |
| 两者都在项目目录外 | 分别复制为最终 ACB 名和声明 AWB 名，同一事务提交 |
| ACB 在内、AWB 在外 | 保留 ACB，复制 AWB 到声明名 |
| ACB 在外、AWB 在内 | AWB 必须正好占用声明名且是用户选中的该文件；只复制 ACB |

补充约束：

- 项目目录内 AWB 若文件名与声明名仅大小写不同，可按 `OrdinalIgnoreCase` 视为名称匹配并直接绑定实际 capability。
- 项目目录内 AWB 若名称不同，不能继续依赖显式路径绑定，因为按文件夹重开仍会丢失关系；首版阻止创建。
- 目录内已有正式文件按 S5 的已确认规则直接绑定，不因新规则强制重命名；但 ACB/AWB 必须能配对并解码。
- “双文件包原子提交”指最终成功或失败必须覆盖整个包，不代表已经位于项目目录内的成员还要再复制一份。

### 29.8 冲突判定和禁止的静默行为

对每一个需要由 Setup 创建的目标条目，在实时预检和事务权威复查中都执行 `OrdinalIgnoreCase` 文件/目录冲突检查。

目标名只有以下三种结果：

1. 目录中不存在同名文件或目录：允许创建。
2. 同名条目就是已经位于项目目录内、当前 Selection 明确持有的正式角色 capability：允许直接绑定，不创建。
3. 其他任何同名条目：冲突并阻止创建。

第三类包括：

- 同名已有文件，但不是本次选择的角色。
- 同名已有目录。
- 只有大小写不同的文件或目录。
- 表单打开后由其他程序新建的同名条目。
- ACB 目标名、AWB 目标名、谱面目标名和工程目标名彼此冲突。

禁止以下行为：

- 覆盖已有文件。
- 看到同名 AWB 就不验证内容而静默复用。
- 通过文件长度相同、扩展名相同或名称相同推断它就是正确 AWB。
- 自动生成 `music (1).awb`、`music_2.awb` 等名称。
- 为解决冲突而只修改 AWB 目标名。
- 把 AWB 写进子目录来绕过根级冲突。

普通音频或 ACB 主文件冲突时，用户可以修改主文件目标 stem。声明 AWB 名冲突时，由于 Setup 不重写 ACB 元数据，用户只能选择其他最终项目目录、移除/改名冲突条目后重新预检，或选择/准备另一套 ACB 包。

### 29.9 自动查找、手动选择和内容验证

external AWB 的绑定建议按以下状态机执行：

1. 解析 ACB 并得到声明 AWB 叶级名。
2. 在源 ACB 明确的父目录 capability 中按该叶级名 `OrdinalIgnoreCase` 自动查找。
3. 恰好一个候选时暂时绑定为源 AWB。
4. 没有候选时提示用户手动选择 AWB。
5. 多个候选时不猜测，提示用户明确选择其中一个；如果 picker 无法区分同名 capability，则直接阻止并说明原因。
6. 对最终选择的源 ACB + 源 AWB 执行真实解码预检，至少确认能产生非空可读音频。
7. 复制完成后，再使用项目内最终 ACB + AWB 路径执行一次最终解码验证。

手动选择 AWB 后不能只因扩展名为 `.awb` 就进入 Ready 状态。预检失败时保留 ACB 选择，清除或标记 AWB 选择为无效，并允许用户重新选择；错误信息应说明是包不匹配或无法解码，不能留下半绑定状态。

当前 `TryBindExternalAwbAsync`、Setup 和 `EditorProjectDataUtils` 应复用同一个 ACB 包检查/绑定组件，避免三处分别解释 `ExternalAwb.FileName`。至少要修复当前“手动选择任意 AWB 即直接绑定”的宽松行为。

### 29.10 建议的事务顺序

在 external-AWB ACB 场景中，创建事务建议按以下顺序执行：

1. 从 ViewModel 生成不可变 Selection，冻结 ACB、AWB、目标名和 import 标志。
2. 重新解析源 ACB，确认包类型和声明 AWB 叶级名没有变化。
3. 重新确认源 AWB capability 仍可用。
4. 对源 ACB/AWB 做解码预检。
5. 对项目目录全部目标名做一次权威冲突复查。
6. 若 AWB 需要导入，先流式创建并复制 AWB，成功创建后立即压入回滚栈。
7. 若 ACB 需要导入，再流式创建并复制 ACB，同样立即压入回滚栈。
8. 使用最终项目内 ACB/AWB capability 做解码验证。
9. 再继续创建/写入谱面和 `.nyagekiProj`，工程描述文件仍最后创建。
10. 候选 `EditorContext` 成功加载并被编辑器接管后，才提交 created-files stack。

任一步失败或取消都反向删除本轮新建文件。已位于项目目录内的 ACB/AWB、项目外源 ACB/AWB 和任何创建前已有冲突条目都不进入回滚栈，也绝不删除。

先复制 AWB 再复制 ACB 的目的是避免在正常观察窗口中先出现一个缺少依赖的 ACB；真正的安全性仍来自同一事务回滚和工程文件最后创建，而不是依赖复制顺序假装文件系统支持原子双文件写入。

### 29.11 UI 交互建议

普通音频或 internal-AWB ACB 位于项目目录外时显示：

- 已选源文件。
- 音频类型和可读时长。
- “项目内文件名”输入框，默认使用源名称，扩展名固定显示。
- 名称合法性和冲突错误。

external-AWB ACB 额外显示一个紧凑的“外置 AWB”区域：

- ACB 源文件和可编辑的项目内 ACB 名称。
- ACB 声明的 AWB 目标名，只读。
- 当前选中的 AWB 源文件。
- 自动找到、等待选择、正在验证、验证成功、名称冲突或解码失败状态。
- 一个用于选择/更换 AWB 的文件夹图标按钮，并提供工具提示。

不要提供名为“重命名包”的单一输入框，因为它会暗示 ACB/AWB 可以同时换成同一 stem。错误文本应指出具体成员，例如：

~~~text
项目目录已存在“music0001.awb”，但它不是当前选择的外置 AWB。
ACB 要求项目内 AWB 名称保持为“music0001.awb”，Setup 不会覆盖或自动改名。
~~~

Browser 不显示 external-AWB 区域；选择 `.acb` 被拒绝时只显示平台能力错误，不让用户继续填写一个注定无法提交的 AWB 表单。

### 29.12 建议的 Selection 和实现边界

Setup-only 数据可以使用类似快照：

~~~csharp
internal sealed record AudioImportSelection(
    SetupAudioPackageKind PackageKind,
    ISimpleFile SourceAudioFile,
    ISimpleFile? SourceExternalAwbFile,
    string TargetAudioFileName,
    string? TargetExternalAwbFileName,
    bool AudioRequiresImport,
    bool ExternalAwbRequiresImport);
~~~

约束：

- `TargetExternalAwbFileName` 在 external-AWB 场景只能来自权威包检查结果，不能来自自由文本框。
- `AudioRequiresImport` 和 `ExternalAwbRequiresImport` 根据冻结时的 capability 归属计算，提交时再次验证。
- Session Dispose 所有未转交的源 capability；成功后最终 `EditorFileAccessContext` 只持有项目内正式角色。
- 这些字段只属于 Setup/创建事务和运行时文件访问上下文，不得添加到 `EditorProjectDataModelBase`。
- 工程格式继续由最新 `EditorProjectDataModel` 和 Manifest/角色定位规则表达，不通过修改旧版本基类制造破坏性更新。

### 29.13 测试要求

包识别测试：

- 普通音频分类为 `OrdinaryAudio`。
- internal-AWB ACB 不要求独立 AWB。
- external-AWB ACB 正确得到声明叶级名。
- 解析库返回同级完整路径时能安全还原叶级名。
- 相对嵌套路径、其他目录完整路径、URI、空声明和非 `.awb` 声明被明确拒绝。
- 损坏 ACB 在写任何目标文件前失败。

绑定测试：

- 同级唯一 AWB 自动绑定。
- 无同级 AWB 时进入显式选择状态。
- 多个候选不任意选择。
- 手动选择源文件名不同但内容正确的 AWB，预解码通过后可复制为声明名。
- 手动选择错误 AWB 时不能进入 Ready，且可以重新选择。
- 当前 Provider、Setup 和加载校验对 external AWB 的解释一致。

命名和冲突测试：

- 普通音频和 ACB 主文件可以显式修改目标 stem，扩展名不可伪装修改。
- AWB 目标名只读且保持 ACB 声明的原始叶级码点和大小写。
- ACB 改名、AWB 保持声明名后仍能解码并重新打开。
- AWB 声明名非法时阻止导入，不自动清洗。
- ACB 主文件冲突时可由用户改名解决。
- AWB 文件冲突、目录冲突和仅大小写不同冲突都阻止创建。
- 不静默复用未选择的同名已有 AWB。
- ACB、AWB、谱面和工程四类目标互相进行冲突检查。

目录组合测试：

- ACB/AWB 都在目录内时零复制直接绑定。
- 两者都在目录外时复制两个文件。
- ACB 在内、AWB 在外时只复制 AWB。
- ACB 在外、AWB 在内且名称匹配时只复制 ACB。
- 项目内 AWB 名称与声明名不同，即使显式路径能解码也阻止创建。

事务测试：

- AWB 复制成功、ACB 复制失败时删除新 AWB。
- 两者复制成功、最终解码失败时删除两个副本。
- 谱面或工程文件后续失败时 ACB/AWB 同样回滚。
- 取消发生在大 AWB 复制中、两文件之间和最终验证阶段时都只删除本轮新建文件。
- 项目目录内原文件和目录外源文件在所有失败路径中保持不变。
- 成功后按文件夹重新打开能自动绑定同级 AWB，不再次弹 picker。

平台测试：

- Desktop `.acb` 过滤器和 Setup 能进入上述流程。
- Browser picker 不提供 `.acb`，绕过 UI 传入 `.acb` 仍被拒绝。
- Browser 拒绝不会留下已创建文件或未释放 capability。

### 29.14 其他方案及其代价

#### 方案 B：ACB 和 AWB 始终改成同一目标 stem

UI 看似简单，但除非可靠重写 ACB 内部 external AWB 引用，否则改 AWB 名会破坏自动配对。当前仓库没有经过验证的 ACB 元数据重写与回读流程，因此首版不能采用。

#### 方案 C：AWB 冲突时自动编号

`music0001.awb` 自动改成 `music0001 (1).awb` 会立即偏离 ACB 声明。即使本次显式路径解码成功，按文件夹重开仍可能失败，不可采用。

#### 方案 D：把每个 ACB 包放进独立子目录

可以减少根级冲突，但会改变 S2 已确认的根级单根布局、Manifest locator、父目录自动查找和最近恢复规则。若未来需要正式的 `audio/` 或包子目录，应作为完整目录版本迁移设计，而不是在 S7 中局部引入。

#### 方案 E：同名已有 AWB 自动复用

名称相同不代表内容就是该 ACB 所需的数据。静默复用可能直到播放特定 cue 才暴露错误，也会违反“已有条目不覆盖、不猜测”的安全原则。

#### 方案 F：无条件把任何 external AWB 路径压平成叶级名

能接受更多来源，但会悄悄改变真正的嵌套相对关系，并让项目重开规则依赖未记录的路径转换。首版应明确拒绝不属于同级兄弟关系的包。

### 29.15 当前推荐结论

当前推荐是：

> 普通音频和 internal-AWB ACB 作为单文件导入，项目外副本默认保留源名，用户可显式修改目标 stem，扩展名固定。external-AWB ACB 作为不可分割双文件包：ACB 目标名可独立修改；AWB 目标名固定为 ACB 声明的同级叶级名，只读且不可自动编号。手动选择的源 AWB 可以名称不同，但必须在写入前真实解码，并复制成声明名。AWB 声明非法、要求嵌套路径或目标冲突时阻止创建，不重写 ACB 元数据、不覆盖、不猜测复用。项目内外混合来源只复制缺少的成员，整个包仍纳入同一回滚事务；Browser 继续拒绝 ACB。

### 29.16 用户确认

已确认，日期为 2026-08-17。最终规则为：

- 普通音频和 internal-AWB ACB 的项目内主文件名默认沿用源名称，允许用户显式修改 stem，扩展名固定。
- external-AWB ACB 的项目内 ACB 主文件名同样可以独立修改，不要求与 AWB 使用相同 stem。
- external AWB 的项目内目标名固定为 ACB 声明的同级叶级名，不显示可编辑输入框，不自动清洗、编号或改名。
- 用户手动选择的源 AWB 文件名可以与声明名不同，但必须经过真实解码验证；复制到项目目录时使用 ACB 声明名。
- AWB 声明非法、要求嵌套路径、目标已冲突或最终解码失败时阻止创建；首版不重写 ACB 二进制元数据。
- ACB/AWB 混合位于项目目录内外时只复制缺少的成员，但整个包仍使用同一事务和反向回滚。
- Browser 首版继续拒绝 ACB，不显示一个无法提交的 external-AWB 表单。

## 30. 决策 S8：创建过程的进度、取消和关闭行为

### 30.1 为什么需要单独确认

S2 和 S7 已经确认项目外谱面、普通音频以及必要的 ACB/AWB 成员要复制到 `ProjectDirectory`。音频和 AWB 可能很大，Browser 的授权目录写入还可能受配额、浏览器调度和存储实现影响。如果点击“创建”后只冻结窗口而没有阶段提示，用户无法判断应用是在等待 I/O gate、解析 ACB、复制大文件、回读校验，还是已经失去响应。

另一方面，当前基础设施并不是每一个异步 API 都能被 `CancellationToken` 立即中断。S8 必须定义的是一套诚实、可实现的取消契约，而不是向用户承诺“任何时刻点击取消都能立刻停止”。

本决策直接影响：

- Setup 是在创建期间留在原窗口，还是先关闭再显示另一个进度窗口。
- `EditorProjectCreationService` 的进度接口和阶段模型。
- 流式复制 helper 是否报告字节进度。
- 用户点击取消后，当前不可中断调用如何收尾。
- 编辑器接管前的最后一个可取消点在哪里。
- 回滚是否允许再次取消。
- 用户点击窗口关闭按钮或按 Escape 时的行为。
- 用户确认创建时如何冻结“需要复制”和“无需复制”的文件清单。
- 取消或失败时哪些文件允许删除、哪些文件绝对不能删除。
- 创建完成、取消或失败后 Setup 如何结束本次流程。

### 30.2 当前代码能够提供的取消粒度

基于现有实现，操作可以分为三类：

| 操作 | 当前取消能力 | S8 中必须采用的语义 |
|---|---|---|
| 等待 `EditorProjectIoGate.EnterAsync(token)` | 能直接响应 token | 立即停止等待，不创建文件 |
| 自定义流式复制循环 | 每次 `ReadAsync`、`WriteAsync` 和循环检查都能响应 token | 作为主要的精确进度和及时取消区间 |
| `ISimpleDirectory.CreateFileAsync` | 接口接收 token，但部分 StorageProvider 调用只在进入前检查 | 接受取消请求；底层调用返回后不再开始下一步 |
| `ISimpleFile.OpenRead/OpenWrite` | 接口本身没有 token | 不能承诺打开动作立即中断，只能在调用前后检查 |
| `ISimpleFile.WriteAsync` 的 writer | writer 可以收到 token | writer 阶段可取消；writer 成功后的临时文件提交可能故意不可取消 |
| 谱面 serializer/deserializer | 当前接口没有 token | 当前调用完成后检查取消，不再进入下一阶段 |
| `AcbConverter.ConvertAcbFileToWavFile` | 当前没有 token，内部等待也未接收用户 token | 解码期间只能登记取消请求，解码返回后停止并回滚 |
| 音频加载和部分编辑器接管调用 | 调用链中存在不接收 token 的操作 | 在进入最终接管前设置明确的最后取消检查 |
| 反向删除本轮创建文件 | 技术上可传 token | 必须使用 `CancellationToken.None`，不允许用户中止清理 |

因此，UI 中的“取消”应解释为：

> 请求停止创建。可中断的复制和等待会尽快停止；当前不可中断调用会先返回，随后不再开始新的正向步骤，并进入完整回滚。

不能通过把不可中断调用放进 `Task.Run` 并丢弃任务来伪造取消。这样只会让后台任务继续使用文件和 capability，并与回滚并发，可能删除仍在写入或读取的目标。

### 30.3 推荐的总体用户体验

建议创建过程继续显示在同一个 Setup 窗口内，不在用户点击“创建”后立即隐藏窗口，也不额外叠加第二个进度对话框。

同一个窗口分成两种稳定状态：

1. **编辑状态**：显示完整表单、验证信息、取消和创建按钮。
2. **创建状态**：表单仍保留在窗口中但整体禁用，底部区域切换为当前阶段、当前文件、进度条和取消创建按钮。

这样做的目的不是让 ViewModel 自己承担 Provider 的全部职责，而是让 Provider/协调器把创建状态和进度投影回 Setup ViewModel。用户能看见刚刚提交的配置，并能在取消后的文件清理完成前确认应用仍在工作。

用户一旦点击“创建”，本次表单就被消费为不可变创建计划，不再恢复到可编辑状态。成功时关闭并返回 `true`；用户取消时完成回滚后关闭并返回 `false`；失败时显示错误，用户确认后关闭并返回 `false`。需要重试时由用户重新进入“新建”流程。这一取舍避免为了保留表单引入可返还的 capability 所有权状态机。

布局要求：

- 不使用新的嵌套卡片或单独的装饰面板；进度作为 Setup 底部的全宽状态区域。
- 表单进入创建状态后尺寸保持稳定，不能因状态文本或百分比出现而推动按钮跳动。
- 当前阶段文本允许换行，但不能覆盖进度条和取消按钮。
- 创建按钮在运行期间不可再次触发。
- 文件、目录、模式、名称和 BPM 控件全部禁用，避免 Selection 与正在执行的事务发生分叉。
- 只保留一个可操作命令：“取消创建”；请求发出后该按钮立即禁用并显示“正在取消…”。

### 30.4 创建运行状态机

建议把窗口级运行状态与底层创建阶段分开。窗口级状态可以是：

~~~csharp
internal enum EditorProjectSetupRunState
{
    Editing,
    Running,
    CancellationRequested,
    Finalizing,
    RollingBack,
    Completed
}
~~~

语义如下：

| 状态 | 表单可编辑 | 可请求取消 | 可直接关闭窗口 |
|---|---:|---:|---:|
| `Editing` | 是 | 不适用 | 是 |
| `Running` | 否 | 是 | 否；关闭动作转为取消请求 |
| `CancellationRequested` | 否 | 否，已有请求 | 否；等待当前调用结束和回滚 |
| `Finalizing` | 否 | 否 | 否；正在执行短暂的最终接管 |
| `RollingBack` | 否 | 否 | 否；必须完成清理 |
| `Completed` | 否 | 否 | 由程序按成功、取消或失败结果关闭 |

底层阶段用于显示和测试，建议至少包括：

~~~csharp
internal enum EditorProjectCreationPhase
{
    WaitingForIoGate,
    RefreshingDirectory,
    ValidatingSelection,
    ParsingSourceFumen,
    InspectingAudioPackage,
    DecodingSourceAudio,
    PreparingNewFumen,
    PreparingProjectData,
    CopyingExternalAwb,
    CopyingAudio,
    CopyingFumen,
    WritingNewFumen,
    WritingProjectFile,
    VerifyingCreatedFiles,
    LoadingCandidateContext,
    AttachingEditor,
    RollingBack
}
~~~

阶段枚举只表达业务事实，不直接携带本地化文本。Setup ViewModel 使用 `Lang` 把阶段映射为用户可见文本，避免服务层返回中文字符串，也便于单元测试只断言稳定枚举值。

建议进度数据契约：

~~~csharp
internal sealed record EditorProjectCreationProgress(
    EditorProjectCreationPhase Phase,
    string? CurrentFileName,
    long BytesCompleted,
    long? BytesTotal,
    bool IsIndeterminate);
~~~

服务 API 相应扩展为：

~~~csharp
public Task<EditorProjectCreationTransaction> PrepareAsync(
    EditorProjectSetupSelection selection,
    IProgress<EditorProjectCreationProgress>? progress,
    CancellationToken cancellationToken);
~~~

`CanRequestCancellation`、窗口关闭策略和“正在取消”不应由底层服务通过进度对象决定，而应由唯一的创建协调器根据 `EditorProjectSetupRunState` 决定。

### 30.5 进度条表达什么

首版不建议显示一个从 0% 到 100% 的“总创建百分比”。原因是：

- ACB 解码、StorageProvider 建立文件、serializer 和编辑器接管都没有可靠的工作量单位。
- Existing/CreateNew、普通音频/internal ACB/external ACB 具有不同的动态步骤数。
- 把每个步骤平均分配百分比会产生长时间卡在某个数字、随后突然跳跃的假精度。

推荐只显示“当前阶段进度”：

- 等待 gate、解析、解码、写项目数据、回读校验和编辑器接管使用不确定进度条。
- 复制单个已知长度文件时显示确定进度条。
- 同时显示安全处理后的 `CurrentFileName`，例如“正在复制 music0001.awb”。
- 已知总长度时显示“已复制大小 / 总大小”，不需要显示估算剩余时间。
- `FileLength <= 0`、长度读取失败或 provider 报告不可信时使用不确定进度。
- 实际累计字节超过先前报告的 `FileLength` 时立即切回不确定进度，不能显示超过 100%。
- 一个文件复制完成后，下一阶段可以重新变为不确定进度；这不是总进度回退。
- ACB 和 AWB 是两个文件时分别显示当前文件进度，不把解码时间伪装成文件复制百分比。

复制 helper 需要从 `Stream.CopyToAsync` 改为显式循环，以便报告字节数：

~~~csharp
public static async Task CopyToAsync(
    ISimpleFile source,
    ISimpleFile target,
    IProgress<long>? progress,
    CancellationToken cancellationToken)
{
    await using var sourceStream = await source.OpenRead();

    await target.WriteAsync(
        async (targetStream, writerCancellationToken) =>
        {
            var buffer = ArrayPool<byte>.Shared.Rent(81_920);
            try
            {
                long copied = 0;
                while (true)
                {
                    writerCancellationToken.ThrowIfCancellationRequested();

                    var read = await sourceStream.ReadAsync(
                        buffer.AsMemory(0, buffer.Length),
                        writerCancellationToken);
                    if (read == 0)
                        break;

                    await targetStream.WriteAsync(
                        buffer.AsMemory(0, read),
                        writerCancellationToken);

                    copied += read;
                    progress?.Report(copied);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        },
        cancellationToken);
}
~~~

生产实现还应：

- 在开始读取前报告 0。
- 用 `long` 保存累计字节。
- 每次复制循环都使用 writer 提供的 token，不能改用 `CancellationToken.None`。
- 限制 UI 通知频率，例如最多约每 50 毫秒一次；不要每写入一个 buffer 就强制调度 UI。
- 无论是否节流，都在复制结束时报告最终字节数。
- 使用本次运行 id 或等效方式忽略流程结束后到达的迟到进度，防止已关闭窗口继续被更新。
- `IProgress<T>` 若在 UI 线程构造，可以利用其同步上下文；若实现改用普通回调，必须显式调度到 Avalonia UI 线程。

### 30.6 取消请求的精确语义

用户第一次点击“取消创建”时，协调器必须原子地完成：

1. 把 `RunState` 从 `Running` 改为 `CancellationRequested`。
2. 禁用取消按钮，避免重复命令。
3. 把可见文本改为“正在取消…”。
4. 调用本次尝试专属的 `CancellationTokenSource.Cancel()`。
5. 保持 Setup 窗口打开。

随后按当前操作处理：

- 正在等待 gate：`WaitAsync(token)` 抛出取消，直接结束，不产生回滚文件。
- 正在流式复制：下一次读、写或显式检查停止；已创建的部分目标进入回滚。
- 正在执行不接收 token 的 parser、serializer、ACB 解码或 provider 调用：登记的取消请求保持有效，等待当前调用自然返回；返回后立即检查 token，不启动下一正向阶段。
- 正好位于两个步骤之间：在创建下一个目标文件前检查 token。
- 已经进入 `Finalizing` 或 `RollingBack`：不再接受新取消请求。

所有正向阶段都需要采用一致的检查规则：

~~~text
进入阶段前检查取消
    |
    v
执行当前阶段
    |
    v
阶段返回后再次检查取消
    |
    +-- 已取消：不启动下一阶段，进入回滚
    |
    +-- 未取消：进入下一阶段
~~~

异常分类要求：

- 只有在本次用户 token 已请求取消时捕获到的 `OperationCanceledException` 才显示为“用户取消”。
- token 未请求时出现的 `OperationCanceledException` 按普通失败处理并记录，不能静默伪装成用户操作。
- 取消是幂等操作；重复点击、窗口关闭和 Escape 同时到达时只调用一次状态转换。
- 取消请求不清空原始失败信息。如果先发生真实 I/O 失败、随后用户点击取消，应保留最先决定结果的真实失败。

### 30.7 最后可取消点和编辑器接管

唯一明确的最后可取消点应位于调用 `TryAttachProjectAsync` 之前。

推荐顺序：

~~~text
候选文件已全部创建并回读验证
    |
    v
最后一次检查用户 cancellation token
    |
    v
原子切换到 Finalizing
    |
    +-- 禁用取消
    +-- 拦截关闭和 Escape
    +-- 显示“正在打开新项目…”
    |
    v
TryAttachProjectAsync
    |
    +-- true：Commit，成功关闭 Setup
    |
    +-- false/exception：进入不可取消回滚
~~~

不能允许用户取消请求与编辑器接管成功同时生效。否则会出现两种相互冲突的结果：

- 编辑器已经持有并开始使用新 `EditorContext`。
- 创建事务又因为取消而删除该上下文正在使用的工程文件。

因此：

- `Running -> Finalizing` 的切换和取消请求必须由同一个协调器串行化。
- 切换前再检查一次 token。
- 切换成功后，用户取消 token 不再传给最终接管内部作为可撤销信号；应用生命周期终止是另一套退出机制。
- `TryAttachProjectAsync` 返回 `true` 后立即提交事务，不能再因为一个稍晚到达的取消点击回滚。
- `TryAttachProjectAsync` 返回 `false` 或抛出异常时，契约仍必须保证旧编辑器状态不变，候选上下文仍由事务持有，随后执行回滚。

`Finalizing` 应保持很短，只包含候选上下文的最终所有权转移和编辑器接管。ACB 解码、大文件复制、项目写入和回读都不能提前塞进这个不可取消阶段。

### 30.8 回滚不能再取消

一旦决定取消或失败，`RunState` 进入 `RollingBack`。回滚使用独立于用户 token 的清理路径：

- 反向遍历 `createdFiles`。
- 每个删除调用使用 `CancellationToken.None`。
- 单个删除失败时记录错误并继续处理其他文件。
- 删除全部尝试完成后再 Dispose wrapper 和候选上下文。
- 回滚期间取消按钮保持禁用。
- 回滚期间窗口关闭按钮和 Escape 被拦截。
- 不显示会暗示可以中断清理的第二个“取消”按钮。

原因是回滚的目标正是恢复“未创建项目”的一致状态。允许再次取消回滚会主动把半个项目留在用户选择的目录中，并破坏此前所有关于原子创建的承诺。

如果回滚完全成功：

- 普通用户取消不显示错误对话框。
- Setup 结束本次新建流程并返回 `false`。
- 用户需要重试时重新从“新建”菜单进入 Setup。
- 因真实创建失败而回滚时，先显示可操作错误，再由用户关闭本次 Setup。

如果一个或多个回滚删除失败：

- 仍完成所有其他删除尝试。
- 显示明确警告和未删除的本轮文件名。
- 不自动覆盖或复用残留文件。
- 用户确认警告后关闭本次 Setup；再次进入新建流程时，目录权威冲突检查会把残留文件识别为已有条目。

### 30.9 关闭按钮和 Escape

窗口关闭行为必须由状态决定：

| 当前状态 | 点击标题栏关闭按钮 / Alt+F4 / Escape |
|---|---|
| `Editing` | 按普通 Setup 取消处理，释放尚未转交的 capability，并返回 `false` |
| `Running` | 拦截窗口关闭，等价于一次“取消创建”请求；窗口保持可见 |
| `CancellationRequested` | 拦截并忽略重复请求，继续显示“正在取消…” |
| `Finalizing` | 拦截关闭，显示“正在完成项目创建，暂时无法关闭” |
| `RollingBack` | 拦截关闭，显示“正在清理未完成的项目文件，暂时无法关闭” |
| `Completed` | 成功返回 `true`；取消或失败返回 `false` |

不建议在用户请求取消时再弹“是否确定取消”确认框。创建尚未提交，取消的设计目标就是回滚到无副作用状态；额外确认只会拖延大文件复制和增加窗口堆叠。

本规则只约束应用仍在正常运行时的窗口行为。进程被操作系统强制终止、浏览器标签被强制关闭或机器断电无法由应用内回滚完全保证，不能在 UI 文案中暗示对此也有事务保证。

### 30.10 确认时冻结创建计划和回滚边界

用户点击“创建”时，Setup 先把当前选择转换为不可变创建计划。计划只回答三个问题：

1. 哪些已选文件位于 `ProjectDirectory` 外，需要复制到项目目录。
2. 哪些已选文件已经位于 `ProjectDirectory` 内，只需要直接绑定。
3. 本轮还要生成哪些新文件，例如新谱面和 `.nyagekiProj`。

建议的数据结构：

~~~csharp
internal sealed record EditorProjectCreationPlan(
    EditorProjectSetupSelection Selection,
    IReadOnlyList<EditorProjectFileCopyPlan> FilesToCopy,
    IReadOnlyList<EditorProjectExistingFileBinding> ExistingBindings,
    IReadOnlySet<string> PlannedTargetFileNames);

internal sealed record EditorProjectFileCopyPlan(
    EditorProjectFileRole Role,
    ISimpleFile SourceFile,
    string TargetFileName);

internal sealed record EditorProjectExistingFileBinding(
    EditorProjectFileRole Role,
    ISimpleFile ProjectFile);
~~~

其中：

- `FilesToCopy` 只包含项目目录外的现有谱面、普通音频、ACB 或必要 AWB。
- `ExistingBindings` 只包含已经位于项目目录内、无需复制的谱面、音频、ACB 或 AWB。
- `PlannedTargetFileNames` 包含全部副本目标名、新谱面名和工程文件名。
- external-AWB ACB 的 ACB 和 AWB 分别分类，允许出现一个在目录内、另一个需要复制的组合。
- 分类按目录 capability 的真实归属关系计算，不根据 `FullPath` 字符串前缀猜测。
- 计划生成后不再因 UI 字段变化而改变；此时表单已冻结。

### 30.10.1 写入前统一检查目标冲突

取得 `EditorProjectIoGate` 并刷新目录快照后，事务必须在创建任何文件之前，对 `PlannedTargetFileNames` 做一次权威检查：

- 每个需要复制的目标名只要已经存在同名文件或目录，就立即报错。
- 冲突比较继续使用 S5 已确认的便携名称和大小写规则。
- 即使已有文件与源文件长度相同、内容看起来相同，也不自动复用、不覆盖。
- 已位于项目目录内的 `ExistingBindings` 不是待创建目标，不会与自身构成冲突。
- 但一个 ExistingBinding 的名称若与另一个计划副本、新谱面或工程目标冲突，仍要阻止创建。
- ACB/AWB、谱面和工程文件的所有目标名必须在计划内部互不冲突。
- 冲突发生在写入前，因此该路径不需要磁盘回滚，只需显示错误并结束本次 Setup。

这一规则对应用户要求的“需要复制的文件遇到项目内同名条目就报错”，不增加自动改名、覆盖或内容猜测。

### 30.10.2 `createdFiles` 只记录真正新建成功的目标

不可变创建计划不是删除清单。事务仍维护独立的运行时 `createdFiles`：

~~~csharp
private readonly List<ISimpleFile> createdFiles = [];
~~~

只有满足以下条件的文件才加入：

- 事务调用 `CreateFileAsync` 创建了副本目标，并且调用成功返回 capability。
- CreateNew 模式创建了新谱面文件。
- 事务创建了新的 `.nyagekiProj` 文件。

加入时点必须紧跟在 `CreateFileAsync` 成功之后，不能等到文件内容写完。这样复制中途取消时，不完整目标已经位于回滚栈中。

以下对象永远不能加入 `createdFiles`：

- `FilesToCopy` 中的源文件。
- `ExistingBindings` 中原本就在项目目录内的文件。
- 用户选择的项目目录。
- 创建前已经存在的任何文件或目录。
- 仅用于查找文件的父目录 capability。

取消或失败时：

~~~text
反向删除 createdFiles
    |
    +-- 删除本轮复制或生成的目标
    +-- 不删除项目外源文件
    +-- 不删除项目内原有绑定文件
    +-- 不删除用户选择的项目目录
~~~

例如：

| 场景 | 取消时处理 |
|---|---|
| 音频已在项目目录内 | 不复制、不删除 |
| 音频在目录外，尚未创建目标 | 不删除任何物理文件 |
| 音频在目录外，目标已创建且复制一半 | 删除本轮创建的不完整目标，保留源文件 |
| Existing 谱面已在项目目录内 | 不删除 |
| 新谱面已创建，工程文件尚未创建 | 删除本轮新谱面 |
| AWB 已复制，ACB 复制失败 | 删除本轮 ACB 目标和 AWB 副本，保留源包 |
| 同名冲突在预检阶段发现 | 不创建、不删除任何文件 |

这里需要区分 `Dispose` 和物理删除：

- `Dispose` capability wrapper 只是释放本次流程持有的访问对象。
- 只有明确调用 `DeleteAsync` 才表示删除物理文件。
- 取消后可以 Dispose 源文件和 ExistingBinding 的 wrapper，但绝不能对它们调用 `DeleteAsync`。

### 30.10.3 所有权采用一次性转交，不再支持返还表单

用户确认创建后，`TakeSelection()` 可以保持单向语义：

~~~text
Setup Session
    |
    | TakeCreationPlan()
    v
Creation Coordinator / Transaction
    |
    +-- 成功：正式项目上下文接管项目目录和角色
    |
    +-- 取消/失败：回滚 createdFiles，Dispose capability，关闭 Setup
~~~

要求：

- Session 在 `TakeCreationPlan()` 后不再拥有或 Dispose 已转交 capability。
- 事务执行期间不存在可编辑表单，也没有“把 capability 还给 Session”的路径。
- 成功时最终 `EditorFileAccessContext` 接管项目目录；项目内角色继续遵守借用别名规则。
- 项目外源文件只用于复制，复制和最终验证结束后 Dispose，不进入最终上下文。
- 取消或失败时先回滚 `createdFiles`，再 Dispose 计划持有的 wrapper。
- `TryAttachProjectAsync` 返回 `false` 或异常时，候选上下文仍由事务持有，能够完成回滚和 Dispose。
- 不引入 `CreationAttempt`、`ReturnToSession` 或共享 disposable capability。
- 创建计划、复制分类和回滚状态都属于运行时事务，不写入 `EditorProjectDataModelBase`。

### 30.11 Provider、协调器和 ViewModel 的职责

建议新增一个单次创建协调器，避免把文件事务和编辑器接管全部塞进 Setup ViewModel：

~~~csharp
internal interface IEditorProjectCreationCoordinator
{
    Task<EditorProjectCreationOutcome> RunAsync(
        EditorProjectCreationPlan plan,
        IProgress<EditorProjectCreationProgress> progress,
        CancellationToken cancellationToken);
}
~~~

职责划分：

- Setup ViewModel：收集输入、展示阶段和进度、维护 `RunState`、发出取消请求。
- Setup Session：拥有编辑状态下的选择，在用户确认时一次性生成并转交创建计划。
- Coordinator：串联 `PrepareAsync`、最后取消点、`TryAttachProjectAsync`、Commit 和 rollback。
- Creation service/transaction：权威冲突检查、文件创建、复制、回读、`createdFiles` 和候选上下文。
- Platform Provider：创建目录/file picker 适配、创建 Session/Coordinator，并在成功后完成最近记录。
- Editor ViewModel：只负责原子接管完整候选上下文，不负责 Setup UI。

协调器返回结构化结果，不通过解析异常消息决定 UI：

~~~csharp
internal abstract record EditorProjectCreationOutcome
{
    public sealed record Succeeded : EditorProjectCreationOutcome;
    public sealed record Canceled : EditorProjectCreationOutcome;
    public sealed record Failed(
        EditorProjectCreationFailureKind Kind,
        Exception Exception,
        IReadOnlyList<string> RollbackFailures)
        : EditorProjectCreationOutcome;
}
~~~

Setup ViewModel 的结果处理：

- `Succeeded`：关闭对话框并返回 `true`。
- `Canceled`：回滚完成后直接关闭对话框并返回 `false`。
- `Failed` 且回滚成功：显示可操作错误，用户确认后关闭并返回 `false`。
- `Failed` 且回滚不完整：显示主错误和残留文件列表，用户确认后关闭并返回 `false`。

### 30.12 建议的阶段文案

阶段文本应描述当前动作，不解释实现细节。建议映射：

| 阶段 | 用户可见文本 |
|---|---|
| `WaitingForIoGate` | 正在等待其他工程操作完成… |
| `RefreshingDirectory` | 正在检查项目目录… |
| `ValidatingSelection` | 正在验证项目设置… |
| `ParsingSourceFumen` | 正在读取谱面… |
| `InspectingAudioPackage` | 正在检查音频包… |
| `DecodingSourceAudio` | 正在验证音频… |
| `PreparingNewFumen` | 正在生成初始谱面… |
| `PreparingProjectData` | 正在生成工程数据… |
| `CopyingExternalAwb` | 正在复制外置 AWB… |
| `CopyingAudio` | 正在复制音频… |
| `CopyingFumen` | 正在复制谱面… |
| `WritingNewFumen` | 正在写入新谱面… |
| `WritingProjectFile` | 正在写入工程文件… |
| `VerifyingCreatedFiles` | 正在检查创建结果… |
| `LoadingCandidateContext` | 正在加载新工程… |
| `AttachingEditor` | 正在打开新工程… |
| `RollingBack` | 正在清理未完成的项目文件… |

不得向普通用户显示 `SemaphoreSlim`、`CancellationToken`、`StorageProvider`、rollback stack 或 capability 等内部术语。详细异常进入日志；用户错误文本只保留可操作信息。

### 30.13 测试要求

状态机单元测试：

- `Editing -> Running -> Completed` 成功路径只执行一次。
- 运行期间第二次 Create 被拒绝。
- 第一次取消把状态改为 `CancellationRequested`，重复取消不重复调用 CTS。
- 取消成功并完成回滚后进入 `Completed`，关闭并返回 `false`。
- `Finalizing` 和 `RollingBack` 不接受用户取消。
- 流程结束后的迟到进度不能更新已关闭窗口。

进度测试：

- 已知长度复制从 0 开始，单调增加并以实际字节数结束。
- 未知或非正长度显示不确定进度。
- 实际复制字节超过报告长度时不显示超过 100%。
- ACB/AWB 分别显示正确当前文件名。
- 非复制阶段不制造虚假百分比。
- 高频进度被节流，但最终通知不会丢失。

取消时点测试：

- 等待 `EditorProjectIoGate` 时取消，不创建文件。
- 复制普通音频中取消，删除部分目标。
- external-AWB 场景在 AWB 复制中、AWB 与 ACB 之间、ACB 复制中取消，按逆序清理。
- 在不支持 token 的 ACB 解码期间请求取消，解码返回后不创建任何目标。
- `CreateFileAsync` 返回后发现已取消，新文件立即进入回滚，不开始写下一个文件。
- serializer/deserializer 返回后发现已取消，不进入后续阶段。
- 最后取消检查之前的请求导致回滚，不调用编辑器接管。
- 成功进入 `Finalizing` 后到达的取消请求不能删除已经接管的文件。
- token 未请求时的 `OperationCanceledException` 按失败显示。

关闭交互测试：

- 编辑状态 Escape 正常关闭并 Dispose Session。
- 运行状态 Escape、Alt+F4 和标题栏关闭都只请求一次取消，窗口不立即消失。
- `CancellationRequested`、`Finalizing` 和 `RollingBack` 的关闭被拦截。
- 用户取消且回滚完成后窗口自动关闭并返回 `false`。
- 成功只由程序关闭一次，不产生取消结果。

创建计划、所有权和回滚测试：

- 用户确认时能稳定区分 `FilesToCopy` 和 `ExistingBindings`。
- 项目外文件的目标已存在时在创建任何文件前报错。
- 项目内 ExistingBinding 不与自身产生冲突，但不能与其他计划目标重名。
- `CreateFileAsync` 成功后目标立即加入 `createdFiles`。
- 源文件、ExistingBinding、项目目录和创建前已有条目永不加入 `createdFiles`。
- 取消后不向 Session 返还 capability；计划持有的 wrapper 各 Dispose 一次。
- 成功后最终上下文只接管一次正式 capability。
- 失败接管不会改变旧 `EditorContext`，也不会消费候选所有权。
- 回滚全部使用 `CancellationToken.None`。
- 一个删除失败不阻止删除其余本轮文件。
- 残留文件名进入错误结果；下一次 Setup 的权威预检会把它识别为冲突。

平台验收：

- Desktop 使用一个足够大的普通音频，能看到确定进度并在中途取消。
- Desktop external-AWB 大文件取消后，源 ACB/AWB 不变，项目目录没有本轮完整或部分副本。
- Browser 使用真实授权目录验证进度、取消和页面内回滚。
- Browser 标签关闭属于不可保证的进程外终止场景，不把它作为应用内回滚成功断言。
- 所有状态文本、进度条和取消按钮在窄窗口及高 DPI 下不重叠。
- 进度条和状态文本具有可访问名称；运行期间焦点能到达取消按钮，进入不可取消状态后不会停留在已禁用控件。

### 30.14 推荐实施顺序

1. 先增加纯数据的阶段枚举、进度 record、Outcome 和窗口运行状态，不改文件行为。
2. 增加不可变 `EditorProjectCreationPlan`，在用户确认时分类 `FilesToCopy`、`ExistingBindings` 和全部目标名。
3. 在 I/O gate 内实现写入前的统一权威冲突检查。
4. 约束 `createdFiles` 只接收本轮 `CreateFileAsync` 成功返回的目标，并补禁止删除源文件/既有文件的测试。
5. 给流式复制 helper 增加字节报告和节流，并补已知/未知长度及取消测试。
6. 实现 Coordinator，把 Prepare、最后取消点、Attach、Commit 和 rollback 收口到一个地方。
7. 在 Setup ViewModel 增加本次运行 CTS、状态转换和进度投影。
8. 在 Setup View 中加入稳定尺寸的进度区域、运行态按钮和可访问属性。
9. 在 Window closing 事件中按 `RunState` 拦截或转换关闭请求。
10. 补全部时点测试，再接 Desktop Provider，并用大普通音频和 external-AWB 包做人工取消验证。
11. Browser 架构同批落地，但仍按 S3 的启用门槛完成真实浏览器验收后才开放菜单。

### 30.15 其他方案及其代价

#### 方案 B：点击创建后关闭 Setup，再弹独立进度窗口

所有权边界同样简单，但会多一层模态窗口。首版没有必要维护两套窗口和焦点/关闭逻辑，建议仍在原 Setup 窗口显示创建进度，结束后关闭该窗口。

#### 方案 C：取消后恢复原表单

需要把已经由 `TakeSelection()` 转交的 capability 再返还给 Session，或者允许 Session、事务和候选上下文共享 disposable wrapper。前者增加可逆所有权状态机，后者容易产生双重 Dispose 和泄漏。按当前决定不采用；用户取消后结束本次新建流程，需要重试时重新进入 Setup。

#### 方案 D：所有阶段都显示总百分比

只能通过人为给未知阶段分配权重实现，数值没有稳定含义。它会比不确定进度更容易让用户误判卡死，不建议采用。

#### 方案 E：关闭窗口立即隐藏，后台继续回滚

用户看不到清理是否完成，可能立刻对同一目录再次创建；后台回滚还可能与下一次事务竞争同名文件。不可采用。

#### 方案 F：回滚也接受取消

会把未完成的项目文件主动留在目录中，与原子创建和“不破坏已有内容”的目标冲突。不可采用。

### 30.16 当前推荐结论

当前推荐是：

> 用户确认创建时生成不可变创建计划，预先区分需要复制的外部源文件、无需复制的项目内既有文件和本轮生成文件。全部复制/生成目标在写入前统一检查同名冲突，存在即报错。`createdFiles` 只记录本轮真正创建成功的目标；取消或失败时只反向删除这些目标，绝不删除项目外源文件、项目内既有文件或项目目录。创建过程留在同一个 Setup 窗口显示真实阶段和文件复制进度；编辑器接管前设置最后取消点，回滚不可取消。取消完成后关闭本次 Setup，不恢复表单，也不引入可返还的 `CreationAttempt`。

### 30.17 用户确认

已确认，日期为 2026-08-17。S8 最终规则为：

- 确认创建时冻结需要复制、无需复制和需要生成的文件清单。
- 需要复制或生成的目标只要已存在同名条目，就在写入前报错。
- 无需复制的项目内既有文件只绑定，不加入回滚，也永不删除。
- 项目外源文件只读取，不加入回滚，也永不删除。
- 只有本轮 `CreateFileAsync` 成功创建的目标进入 `createdFiles`，取消或失败时反向删除。
- 不引入可返还的 `CreationAttempt`。
- 创建期间在当前 Setup 窗口显示进度；取消后等待本轮目标清理完成，再关闭 Setup 并返回 `false`；重试时重新进入“新建”流程。

## 31. 决策 S9：不实现独立 DEBUG Setup 入口

### 31.1 用户决定

已确认，日期为 2026-08-17。

不增加 DEBUG Setup 命令、菜单、Preview service 或第二套测试入口。Desktop 的正式 New 路径是唯一人工操作入口；表单和事务在正式入口启用前通过 ViewModel、创建计划、Coordinator 和 UI 自动化测试验证。

### 31.2 决策依据

原方案提出 DEBUG 入口，是为了在 CanCreateNew 仍为 false、完整创建事务尚未完成时提前打开 Setup 检查布局。该入口不是工程创建本身所需的能力。

当前代码事实是：

- FumenVisualEditorProvider.CanCreateNew 仍为 false。
- FumenVisualEditorViewModel.New() 只记录不支持创建并返回 false。
- EditorProjectSetupDialogViewModel 和对应 XAML 没有生产调用方。
- Splash 的“新建工程”直接调用 TryNew()，目前会绕过菜单对 CanCreateNew 的过滤。
- 仓库不存在需要兼容或迁移的 DEBUG Setup 命令。

因此，不创建临时入口比创建后再删除更直接，也避免出现正式 Provider 与 DEBUG handler 两条行为不同的新建链路。

### 31.3 唯一入口模型

最终入口关系为：

~~~text
自动化测试
    |
    +-- Setup ViewModel / Session
    +-- EditorProjectCreationPlan
    +-- Creation Coordinator / Transaction
    +-- Headless XAML / UI smoke

Desktop 用户
    |
    +-- 正式 File > New / Splash New
            |
            +-- Desktop FumenVisualEditorProvider.TryNew
                    |
                    +-- 正式 Setup
                    +-- 正式创建事务

Browser 用户
    |
    +-- 验收前保持 New 不可用
    +-- 验收通过后使用 Browser Provider 的正式 TryNew
~~~

明确禁止：

- 新增“DEBUG：工程 Setup 预览”菜单。
- 新增只在 Debug 构建注册的 Setup command/handler。
- 为预览复制一套独立校验、Selection 或 CreationPlan。
- 通过 Splash、启动参数或隐藏快捷键绕过 CanCreateNew。
- 为调试入口增加 EditorProjectDataModelBase 字段或工程格式分支。

Debug 和 Release 可以有不同的诊断工具，但工程创建的业务路径必须相同。

### 31.4 正式入口启用前如何验证

CanCreateNew 保持 false 期间，按层验证而不增加可见入口：

1. 直接构造 Setup Session 和 ViewModel，测试默认值、名称、BPM、模式切换、文件分类和 CanCreate。
2. 使用 fake picker 与内存 ISimpleDirectory 测试 Selection 和 EditorProjectCreationPlan。
3. 使用 fault-injection 文件实现测试同名冲突、复制失败、取消和 createdFiles 回滚。
4. 使用 Headless Avalonia 或现有 UI smoke 基础设施加载 Setup XAML，检查绑定、可见性、禁用状态和进度状态。
5. 在 Provider 集成测试中直接调用 TryNew，不依赖菜单枚举；此时测试可以注入 fake 窗口、目录和 coordinator。
6. 等正式 Desktop 纵向链路完整后，再在 Debug Desktop 应用中通过正式 File > New 做人工测试。

这里“人工测试必须等纵向链路完整”是刻意的门槛。它保证用户实际点击的入口从第一次出现起就能创建或安全取消一个真实工程，而不是先暴露一个只会生成 Selection 的半成品。

### 31.5 Desktop 正式 New 的启用门槛

Desktop FumenVisualEditorProvider.CanCreateNew 只有在以下条件全部满足后才能改为 true：

- Desktop Provider 已从共享 Provider 正确拆分和注册。
- 正式 TryNew 能取得 ProjectDirectory 并打开正式 Setup。
- Existing 和 CreateNew 两种谱面模式都能生成不可变创建计划。
- 普通音频、ACB/AWB 和现有谱面的目录内外分类符合 S2、S7、S8。
- 全部复制和生成目标在写入前完成权威同名冲突检查。
- createdFiles 只记录本轮真正创建的文件。
- 取消、失败和编辑器拒绝接管都能完成反向回滚。
- TryAttachProjectAsync 的成功/失败所有权契约通过测试。
- Setup 进度、取消和关闭行为符合 S8。
- 正式 File > New 菜单集成测试通过。
- Splash 的新建按钮遵守同一能力门控。
- Desktop Debug 人工端到端测试通过。

满足后：

1. Desktop CanCreateNew 改为 true。
2. 正式 File > New 列出 Desktop Provider。
3. Splash 新建按钮启用。
4. Debug 和 Release 都调用同一 Desktop TryNew，不增加条件编译业务分支。

Browser 仍按 S3 保持独立门槛。Desktop 启用不自动使 Browser CanCreateNew 返回 true。

### 31.6 Splash 必须修正的门控

当前 SplashScreenViewModel.CreateNewProjectAsync() 直接执行：

~~~csharp
var editor = editorProvider.Create();
if (await editorProvider.TryNew(editor))
    await shell.OpenDocumentAsync(editor);
~~~

它没有先检查 CanCreateNew。正式实现需要保证 Splash 不成为旁路：

- CanCreateNew=false 时，新建按钮隐藏或禁用。
- 即使命令被程序化调用，handler 也要在创建 editor 前检查 CanCreateNew 并直接返回。
- CanCreateNew=true 后，Splash 与 File > New 调用同一个正式 Provider。
- 不增加 DEBUG 条件让 Splash 在开发构建中绕过门控。
- Provider 返回 false 时不打开空文档，并释放由调用方拥有的未接管对象。

是否隐藏还是禁用属于 Splash 的呈现细节；安全要求是 false 时绝不能调用 TryNew。

### 31.7 实施步骤

1. 保持当前代码中不存在 DEBUG Setup 入口的状态。
2. 完成 Setup Session、ViewModel、XAML 和 EditorProjectCreationPlan。
3. 增加纯 ViewModel、Headless UI 和内存文件系统测试。
4. 完成创建 Coordinator、事务、进度、取消和 createdFiles 回滚。
5. 完成 Desktop Provider.TryNew 的正式纵向接线。
6. 修正 Splash 对 CanCreateNew 的可见性/启用状态和命令内二次门控。
7. 补 File > New、Splash New 和直接 Provider 调用的集成测试。
8. 达到 31.5 的门槛后启用 Desktop CanCreateNew。
9. 通过正式入口做 Desktop Debug 人工验收。
10. Browser 继续完成真实运行时验收，再独立启用正式入口。

### 31.8 测试要求

未启用阶段：

- CanCreateNew=false 时 File > New 不显示 Provider。
- Splash 的新建按钮不可用，命令被直接调用也不执行 Create/TryNew。
- 测试可以直接调用 Setup/Coordinator，不需要菜单入口。
- Setup XAML 在 Headless/UI smoke 中加载成功。
- Debug 和 Release 源码中都不存在 DEBUG Setup command、handler 或菜单定义。

启用阶段：

- CanCreateNew=true 时 File > New 只出现一个正式 Provider 项。
- Splash 和 File > New 都进入同一个 Desktop TryNew。
- 两个入口成功时只打开一个编辑器文档。
- 取消时不打开文档，只删除本轮 createdFiles。
- 同名冲突时不创建文件、不打开文档。
- 失败时不留下未接管的空文档或 capability。
- Debug/Release 的创建计划、文件写入和回滚行为一致。

Browser：

- Desktop 启用后 Browser CanCreateNew 仍可保持 false。
- Browser 的 Splash 和菜单同样遵守 Browser Provider 自身的 CanCreateNew。
- Browser 通过 S3 的真实运行时验收后才出现正式 New。

### 31.9 被否决的方案

#### 方案 B：临时 DEBUG 菜单

可以提前人工查看 UI，但需要新增命令、handler、菜单、条件注册和专用测试，完成后还要删除。用户已决定不采用。

#### 方案 C：复用 Splash 作为提前预览入口

会绕过 CanCreateNew，把未完成事务暴露为正式用户行为，并掩盖 Splash 当前缺少能力检查的问题，不采用。

#### 方案 D：提前把 CanCreateNew 改为 true

菜单会出现，但旧 New() 仍返回 false，或者只打开不能创建工程的表单。它会制造可见但不可用的功能，不采用。

#### 方案 E：长期保留开发者新建入口

会形成与正式 Provider 平行的第二套操作路径，容易在权限、校验、取消和回滚上发生漂移，不采用。

### 31.10 最终结论

> 不实现独立 DEBUG Setup 入口。正式 Desktop FumenVisualEditorProvider.TryNew 是唯一人工测试和用户入口；启用前使用 ViewModel、Headless UI、内存文件系统、Coordinator 和 Provider 集成测试完成验证。达到完整纵向门槛后再把 Desktop CanCreateNew 改为 true，并通过正式 File > New 和 Splash 做人工验收。Splash 必须在 UI 和命令层共同遵守 CanCreateNew，不能作为开发旁路。Browser 继续按独立验收门槛启用。

## 32. 决策记录

| 编号 | 状态 | 决定 | 日期 |
|---|---|---|---|
| S1 | 已确认 | 用户直接选择最终 ProjectDirectory；应用不自动创建或删除项目目录。 | 2026-08-17 |
| S2 | 已确认 | 新建工程统一单根；项目外谱面、普通音频和必要 AWB 复制进 ProjectDirectory。 | 2026-08-17 |
| S3 | 已确认 | 两端架构同批实现；Desktop 先启用，Browser 通过真实运行时验收后启用。 | 2026-08-17 |
| S4 | 已确认 | 目录显示名只作初始建议；用户编辑后目录切换不覆盖；非法或冲突不自动修正。 | 2026-08-17 |
| S5 | 已确认 | 采用统一便携叶级名称子集、双长度上限及原始 Unicode 码点语义。 | 2026-08-17 |
| S6 | 已确认 | 初始 BPM 必须手填正有限值；五个速度字段使用该值，ProgJudgeBpm 保持模型默认；其余 S6 推荐同意。 | 2026-08-17 |
| S7 | 已确认 | ACB 主文件可独立改名；external AWB 目标名固定为声明叶级名；真实解码、严格冲突检查并按包原子回滚；Browser 拒绝 ACB。 | 2026-08-17 |
| S8 | 已确认 | 确认时冻结复制/绑定/生成计划；同名目标写入前报错；只回滚本轮 createdFiles；取消清理完成后关闭 Setup。 | 2026-08-17 |
| S9 | 已确认 | 不实现独立 DEBUG Setup 入口；正式 Desktop New 是唯一人工入口，启用前使用自动化和 Provider 集成测试；Splash 必须遵守 CanCreateNew。 | 2026-08-17 |
