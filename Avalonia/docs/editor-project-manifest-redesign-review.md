# EditorProjectManifest 全新设计持续审阅

> 状态：设计访谈进行中，R1-R17 已确认，Q5a 等待回答  
> 创建日期：2026-08-14  
> 当前范围：从零设计项目清单、项目数据文件、资源绑定和平台加载边界  
> 实现状态：仅完成设计记录，尚未修改生产代码

## 1. 文档目的

本文档记录 `FumenVisualEditor` 项目格式的全新设计。该设计主动抛开现有 `.nyagekiProj` 同时承担项目入口、编辑器状态和资源路径清单的历史结构，重新划分以下职责：

1. `EditorProjectManifest`：可持久化项目清单，说明项目由哪些资源组成。
2. `EditorProjectDataModel`：可持久化编辑器状态，只保存与文件绑定无关的数据。
3. `EditorFileAccessContext`：当前运行会话已经取得的文件与目录能力。
4. 平台 Provider：使用 Avalonia `StorageProvider` 取得权限、解析 Manifest、构造完整上下文。
5. `FumenVisualEditorViewModel`：只消费完整 `EditorContext`，不根据字符串路径重新查找文件。

旧的设计访谈和当前代码仍是迁移依据，但不再限制新模型形状：

- [editor_file_access_context_refactory.md](editor_file_access_context_refactory.md)
- [editor_file_access_context_refactory_live_review_2026-08-12.html](editor_file_access_context_refactory_live_review_2026-08-12.html)
- [fumen-visual-editor-project-folder-io-design.md](fumen-visual-editor-project-folder-io-design.md)

## 2. 已确认决策

### R1. 采用便携单根项目，不支持项目目录外部主文件

- 采用此前方案 A。
- Manifest、ProjectData、Fumen、Audio 必须位于同一个项目目录或其子目录中。
- 用户选择的外部谱面或音频必须先复制或导入项目目录，再写入 Manifest 绑定。
- Desktop 与 Browser 使用同一资源关联语义。
- Manifest 不保存绝对路径、Avalonia bookmark、`LocalPath`、StorageProvider URI 或平台类型。
- 项目整体复制到其他目录、设备或平台后，资源引用仍应有效。

### R2. EditorProjectManifest 使用独立入口后缀

- Manifest 文件后缀固定为 `.nyagekiProjectManifest`。
- `.nyagekiProjectManifest` 是平台 Provider 的项目打开入口。
- Provider 不再把 `.nyagekiProj` 当作 Manifest 文件。
- Manifest 所在目录定义为该项目的逻辑项目目录和所有项目相对资源定位符的基准目录。
- 文件夹扫描只查找 `.nyagekiProjectManifest` 候选；发现多个时要求用户选择一个 Manifest。

### R3. EditorProjectDataModel 重新建立为最小状态模型

- 新的 `EditorProjectDataModel` 继续继承 `EditorProjectDataModelBase`。
- 除基类要求的版本信息外，只声明：
  - `AudioDuration`
  - `RememberLastDisplayTime`
- 不声明以下旧字段或运行时对象：
  - `FumenFilePath`
  - `AudioFilePath`
  - `ProjectFileLocator`
  - `Fumen`
  - `ProjectFile`
  - `FumenFile`
  - `AudioFile`
  - `AudioAwbFile`
  - `ProjectRoot`
  - `RecentRecordId`
- `AudioDuration` 是可重新计算的缓存，不是识别 Audio 文件的依据。
- `RememberLastDisplayTime` 是项目级编辑器状态，加载后应限制在有效音频时间范围内。

确认的核心形状如下：

```csharp
public sealed class EditorProjectDataModel : EditorProjectDataModelBase
{
    public static readonly Version VERSION = new(0, 6, 0);

    public override Version Version => VERSION;

    public TimeSpan AudioDuration { get; set; }

    public TimeSpan RememberLastDisplayTime { get; set; }
}
```

### R4. Manifest 保存项目身份与资源绑定，不保存 EditorProjectDataModel 的状态字段

- `EditorProjectManifest` 不直接声明或复制 `AudioDuration`、`RememberLastDisplayTime`。
- Manifest 声明稳定、非空的 `Guid ProjectId`，作为逻辑项目身份。
- Manifest 通过 `ProjectResourceBinding Project` 指向序列化后的 `EditorProjectDataModel` 文件。
- Manifest 同时通过独立绑定指向 Fumen 和 Audio。
- 三个绑定的属性名直接表达角色，不使用一个需要运行时查询角色字符串的任意集合。

确认的基础形状：

```csharp
public sealed class EditorProjectManifest
{
    public static readonly Version VERSION = new(0, 2, 0);

    public Version Version => VERSION;

    public required Guid ProjectId { get; init; }

    public required ProjectResourceBinding Project { get; init; }

    public required ProjectResourceBinding Fumen { get; init; }

    public required ProjectResourceBinding Audio { get; init; }
}

public sealed record ProjectResourceBinding
{
    public required string Locator { get; init; }
}
```

`ProjectResourceBinding` 首版不需要 `Kind`、绝对路径或 External 分支，因为 R1 已禁止项目目录外部主文件。

### R5. 运行时加载只使用 EditorFileAccessContext

- Manifest 中的 locator 只允许由平台 Provider 在构造上下文时解析。
- Provider 成功后交付直接文件能力，ViewModel 不读取 locator 来执行 I/O。
- `EditorFileAccessContext` 需要明确区分 Manifest 文件和 ProjectData 文件。
- 现有含糊的 `ProjectFile` 建议改名为 `ProjectDataFile`，并新增 `ManifestFile`。

建议的运行时形状：

```csharp
public sealed class EditorFileAccessContext : IDisposable
{
    public required ISimpleDirectory ProjectDirectory { get; init; }

    public required ISimpleFile ManifestFile { get; init; }

    public required ISimpleFile ProjectDataFile { get; init; }

    public required ISimpleFile FumenFile { get; init; }

    public required ISimpleFile AudioFile { get; init; }
}

public sealed class EditorContext : IDisposable
{
    public required EditorProjectManifest Manifest { get; init; }

    public required EditorProjectDataModel ProjectData { get; init; }

    public required EditorFileAccessContext FileAccessContext { get; init; }

    public required EditorOpenMetadata OpenMetadata { get; init; }

    public void Dispose() => FileAccessContext.Dispose();
}

public sealed record EditorOpenMetadata(
    string ProjectName,
    string LocationDescription);
```

### R6. ProjectData 继续使用 `.nyagekiProj` 后缀

- `ProjectResourceBinding Project` 指向的独立 ProjectData 文件继续使用 `.nyagekiProj`。
- `.nyagekiProj` 的新语义仅是内部编辑器状态文件，不再是项目入口或资源清单。
- 平台 Provider、文件选择器和文件夹扫描只把 `.nyagekiProjectManifest` 作为新项目入口。
- 用户直接选择 `.nyagekiProj` 时，正常打开流程必须拒绝把它当作新项目；旧项目是否提供显式导入由后续迁移问题决定。
- 由于后缀无法区分旧式完整项目与新式 ProjectData，文件内容版本必须承担格式识别边界，不能让两种不同结构继续共享同一个版本号。

### R7. Manifest 使用 `0.2.0`，ProjectData 使用 `0.6.0`

- `EditorProjectManifest.VERSION` 为 `0.2.0`。
- 新的 `EditorProjectDataModel.VERSION` 为 `0.6.0`。
- Manifest 与 ProjectData 使用相互独立的版本序列，不要求主版本号保持一致。
- `.nyagekiProj` 的 `0.6.0` 明确表示新的最小 ProjectData 语义；旧程序只理解 `0.5.2/0.5.4` 时必须拒绝该版本。
- Manifest `0.2.0` 是首个投入实现的新清单版本；不为尚未落地的 `0.1.0` 提供兼容承诺。

### R8. 有效 Manifest 可以绑定并内存迁移旧 ProjectData

- `0.2.0` Manifest 的 `Project` binding 可以指向 `0.5.2`、`0.5.4` 或 `0.6.0` 的 `.nyagekiProj`。
- 旧 ProjectData 只有在 Manifest 已成功解析、locator 已验证且其余资源 binding 已明确时，才允许迁移到内存中的 `0.6.0` 模型。
- 迁移只保留 `AudioDuration` 与 `RememberLastDisplayTime`；旧资源路径、旧 `Id`、`EditorSetting` 和 bullet palette 数据不进入新模型。
- 仅加载不改写文件。第一次成功执行正常保存时，ProjectData 才写为 `0.6.0`。
- 没有 Manifest 的旧 `.nyagekiProj` 仍不得进入正常打开流程。

### R9. Manifest 使用稳定、非空的 `Guid ProjectId`

- `ProjectId` 位于 Manifest，不位于新的 ProjectData。
- 新建项目时生成随机非空 GUID；打开 Manifest 时拒绝 `Guid.Empty`。
- `ProjectId` 表示逻辑项目身份，普通保存和资源 binding 变化不会修改它。
- 最近记录快照保存 `ProjectId`；`RecentRecordInfo.RecordId` 仍只表示本机最近记录列表中的一行。
- 书签只负责恢复平台文件能力，不再承担项目身份语义。

### R10. 同一项目操作保留 ID，应用内创建副本时生成新 ID

- 普通保存、资源 binding 变化、文件或目录重命名、项目整体移动均保留 `ProjectId`。
- 应用内“另存为”、复制项目、从模板创建项目及其他显式创建独立副本的操作生成新的随机非空 `ProjectId`。
- 操作系统文件管理器直接复制目录时，应用无法介入，复制出的 Manifest 会暂时保留原 `ProjectId`。
- 应用不得因为观察到目录位置变化而自动改写 `ProjectId`。

### R11. 最近记录按 Manifest 文件位置判定，不按 `ProjectId` 合并

- 最近记录的项目判重依据是 Manifest 对应 `ISimpleFile` 的存储位置。
- 相同 Manifest 文件位置更新同一条最近记录；不同位置始终保留为不同记录，即使 Manifest 中的 `ProjectId` 相同。
- 项目移动或重命名导致 Manifest 位置变化时，新位置创建新记录；旧位置记录由正常有效性检查标记失效或由用户清理。
- 外部复制目录天然得到不同 Manifest 位置，因此不会发生同 ID 最近记录互相覆盖。
- `ProjectId` 继续表示 Manifest 中的逻辑项目身份，可用于格式验证、诊断和显式副本操作，但不作为最近记录判重键。

### R12. ManifestFile 的 `FullPath` 是最近记录的唯一判重键

- 最近记录只使用 `EditorFileAccessContext.ManifestFile.FullPath` 判重。
- 不使用 `LocalPath`、额外 `StorageLocation`、`ProjectId` 或 bookmark 作为判重回退。
- 平台 Provider 必须保证 ManifestFile 的 `FullPath` 表示底层 Manifest 文件的完整存储位置。
- 从项目目录扫描发现 Manifest 时，不能直接把目录树中仅含相对路径的文件包装交给上下文；应保留或重新取得对应 `IStorageFile.Path`，使 ManifestFile 与独立打开文件具有相同的完整 `FullPath` 语义。
- `FullPath` 为空或仍是项目相对路径时，不创建最近记录。

### R13. Manifest `FullPath` 使用原始字符串 `Ordinal` 精确比较

- 最近记录判重直接调用 `string.Equals(storedFullPath, currentFullPath, StringComparison.Ordinal)`。
- 不进行 URI 规范化、本地路径规范化、大小写折叠、分隔符转换、转义统一或符号链接解析。
- 同一物理文件若由 Provider 返回不同 `FullPath` 字符串，可以产生多条最近记录；该结果按当前设计接受。
- 最近记录保存 Provider 当次交付的原始 ManifestFile `FullPath`。

### R14. 旧项目导入继承有效旧 `Id`

- 显式导入没有 Manifest 的 `0.5.2/0.5.4` 项目时，旧 ProjectData `Id` 非空则写入新 Manifest `ProjectId`。
- 旧 `Id == Guid.Empty` 时生成新的随机非空 GUID。
- 如果项目已经有有效 Manifest，则 Manifest `ProjectId` 权威，绑定的旧 ProjectData `Id` 被忽略。
- 导入后的 `0.6.0` ProjectData 不再保存 `Id`。

### R15. Manifest locator 采用严格格式，但允许 `.` 路径段

- locator 非空，不以 `/` 开头或结尾，只使用 `/`，且不包含 `//` 或 `\`。
- 禁止绝对路径、盘符、URI scheme 和 `..` 路径段。
- 允许 `.` 路径段；加载时将其消除后再解析。
- locator 仅由 `.` 组成、消除后为空或不能识别文件名时仍是格式错误。
- 创建、修改和保存 Manifest 时始终写不含 `.` 的规范 locator；读取时的 `.` 兼容不会原样写回。

### R16. Manifest locator 统一采用大小写无关匹配

- 所有平台和 Provider 都使用 `StringComparison.OrdinalIgnoreCase` 逐段匹配 locator，不跟随宿主文件系统的大小写规则。
- 成功解析后保留目录枚举得到的实际文件名大小写；后续保存 Manifest 时使用实际大小写。
- 同一目录内存在多个大小写无关匹配候选时，解析直接失败并报告名称冲突，不任意选择其中一个。
- 不使用当前文化或用户区域设置参与比较。
- 该规则只适用于 Manifest binding locator；最近记录的 ManifestFile `FullPath` 仍按 R13 使用原始字符串 `Ordinal` 精确比较。

### R17. Manifest locator 不做 Unicode 规范化

- locator 和 Provider 返回的目录名、文件名都保留原始 Unicode 码点序列。
- 逐段解析只执行 R16 的 `OrdinalIgnoreCase`，不调用 NFC、NFD、NFKC 或 NFKD 规范化。
- 视觉上相同但码点序列不同的名称视为不同名称；如果 Manifest 与 Provider 返回的组合形式不同，locator 可以解析失败。
- 创建或更新 binding 时，保存 Provider 返回的实际名称，不改写其 Unicode 组合形式。
- 只在原始字符串按 `OrdinalIgnoreCase` 同时匹配多个候选时报告名称冲突；不检测 Unicode 规范等价冲突。

## 3. 文件角色和目录不变量

一个完整项目包含四个明确文件角色：

| 角色 | 文件 | 用途 | 正常保存行为 |
| --- | --- | --- | --- |
| Manifest | `*.nyagekiProjectManifest` | 项目入口和三个资源绑定 | 只有绑定或 Manifest 元数据变化时写入 |
| ProjectData | `*.nyagekiProj` | 保存 `AudioDuration`、`RememberLastDisplayTime` | 普通项目保存时写入 |
| Fumen | `*.ogkr`、`*.nyageki` 等受支持格式 | 保存实际谱面内容 | 普通项目保存时写入 |
| Audio | 平台支持的音频格式 | 提供音频数据 | 默认只读，不随普通保存重写 |

Manifest 文件的父目录是项目目录。所有 binding locator 必须满足：

1. 使用 `/` 作为持久化分隔符。
2. 是非空相对定位符。
3. 禁止绝对路径和 URI。
4. 规范化后不能越过 Manifest 所在目录。
5. 不允许定位到目录，只能定位到普通文件。
6. `Project`、`Fumen`、`Audio` 最终不能指向同一个文件。
7. 角色文件必须满足对应格式和读写能力要求。
8. locator 每一段统一使用 `OrdinalIgnoreCase` 匹配，并在存在多个大小写无关候选时失败。
9. Unicode 码点序列不做规范化；只按原始字符串 `OrdinalIgnoreCase` 判断匹配和冲突。

示例目录结构：

```text
  SongA/
  SongA.nyagekiProjectManifest
  SongA.nyagekiProj
  charts/
    master.ogkr
  audio/
    bgm.wav
```

示例 Manifest：

```json
{
  "version": "0.2.0",
  "projectId": "ecb2b9a5-d6cb-4b31-a4dd-c577e54429cf",
  "project": {
    "locator": "SongA.nyagekiProj"
  },
  "fumen": {
    "locator": "charts/master.ogkr"
  },
  "audio": {
    "locator": "audio/bgm.wav"
  }
}
```

## 4. Provider 加载流程

```text
用户选择 .nyagekiProjectManifest 或包含它的目录
  -> Provider 从当前 TopLevel 取得 StorageProvider
  -> 取得 ManifestFile 和其父目录能力
  -> 反序列化并验证 EditorProjectManifest
  -> 相对于 Manifest 父目录解析 Project/Fumen/Audio locator
  -> 验证所有定位符未越界且角色文件互不混淆
  -> 反序列化 ProjectDataFile 得到 EditorProjectDataModel
  -> 验证 Fumen 格式并读取谱面
  -> 验证 Audio 格式并加载音频
  -> 必要时重新计算 AudioDuration
  -> 构造完整 EditorFileAccessContext
  -> 构造 EditorContext
  -> 调用 FumenVisualEditorViewModel.Load(EditorContext)
```

关键边界：

- 文件选择器和目录选择器必须从当前窗口或视图对应的 `TopLevel.StorageProvider` 获取。
- Provider 检查 `CanOpen`、`CanPickFolder`、`CanSave` 后再执行相应操作。
- Provider 和上下文使用 `IStorageFile`/`ISimpleFile` 流，不把 `TryGetLocalPath()` 当作正常加载前提。
- 任一步失败或用户取消时，Provider 释放本轮取得的所有未转交文件能力。
- 只有 ViewModel 成功接管 `EditorContext` 后，文件能力所有权才发生一次性转移。

## 5. 保存职责

普通保存建议采用以下顺序：

1. 把当前播放位置写入 `ProjectData.RememberLastDisplayTime`。
2. 把已验证的音频时长写入 `ProjectData.AudioDuration`。
3. 序列化 Fumen 到 `FileAccessContext.FumenFile`。
4. 序列化 ProjectData 到 `FileAccessContext.ProjectDataFile`。
5. Manifest 绑定未变化时不重写 ManifestFile。
6. Fumen 或 ProjectData 任一写入失败时，执行双文件回滚或事务替换。

绑定变更是独立操作：

- 先在项目目录中创建或导入新文件。
- 完成格式、读写和内容验证。
- 构造新的候选 `EditorFileAccessContext`。
- 原子更新 Manifest binding。
- 成功后替换当前上下文并释放旧能力。
- 任一步失败时保留原 Manifest 和原上下文。

## 6. 最近记录和书签定位

- Manifest 是项目的可移植事实来源。
- 最近记录和 Avalonia bookmark 只是加速重新取得文件能力的本机缓存，不是项目绑定的唯一来源。
- 最近记录至少应能恢复 ManifestFile；恢复后仍以 Manifest 的三个 locator 为权威绑定。
- 最近记录可以保存 Manifest 的 `ProjectId` 用于验证和诊断，但判重键是 Manifest 文件位置；平台书签只用于重新取得 ManifestFile 或项目目录能力。
- 如果平台支持分别为四个文件保存 bookmark，可以作为快速路径，但恢复结果必须与当前 Manifest 绑定一致。
- 书签失效时退回普通 Manifest 打开流程，不从书签正文、本地路径或显示名称猜测绑定。
- 书签不得写入 `.nyagekiProjectManifest`，也不得进入日志或诊断数据。

## 7. 与上一轮方案的主要变化

| 上一轮概念 | 新设计 |
| --- | --- |
| `.nyagekiProj` 同时是入口和项目数据 | `.nyagekiProjectManifest` 是入口，ProjectData 是独立资源 |
| `EditorProjectDataModel` 保存大量业务与路径字段 | 只保存 `AudioDuration`、`RememberLastDisplayTime` |
| `ProjectFileLocator` | 删除 |
| `FumenFilePath`、`AudioFilePath` | 删除，改由 Manifest 的结构化 binding 表达关联 |
| Context 只有含糊的 `ProjectFile` | 明确区分 `ManifestFile` 与 `ProjectDataFile` |
| 最近记录快照可能成为主要恢复配方 | Manifest 是权威来源，书签只是平台缓存 |
| Desktop 可保留外部主文件 | 所有主文件必须导入项目目录 |

## 8. 尚未确认的问题顺序

后续按依赖顺序逐题确认：

1. 最近记录恢复需要保存哪些必需书签，以及是否额外缓存 ManifestFile 书签。
2. Manifest、ProjectData、Fumen 的保存事务边界。
3. 新建项目时四个文件的默认名称、创建顺序与失败回滚。
4. 绑定修改和“另存为”如何生成新的项目目录。
5. 旧 `.nyagekiProj` 是否完全拒绝、只提供一次性导入，还是存在迁移工具。

## 9. Q1（已确认）：Project 绑定指向的 ProjectData 文件应使用什么后缀？

### 9.1 为什么必须先决定

用户已确认 Manifest 不保存 `AudioDuration` 和 `RememberLastDisplayTime`，而是通过 `ProjectResourceBinding Project` 指向单独的 `EditorProjectDataModel` 文件。因此项目至少包含两个自定义格式文件：

1. `*.nyagekiProjectManifest`：稳定入口和资源清单。
2. ProjectData 文件：频繁保存的最小编辑器状态。

如果 ProjectData 继续使用 `.nyagekiProj`，旧程序和文件关联可能把它误认为旧式完整项目文件；如果改用新后缀，则格式职责更清楚，但会同时引入两个新后缀并需要更新文件类型注册、选择器和测试。

### 9.2 方案 A：ProjectData 继续使用 `.nyagekiProj`（已采用）

示例：

```text
SongA.nyagekiProjectManifest
SongA.nyagekiProj
```

含义调整为：`.nyagekiProj` 不再是项目入口，只是 `EditorProjectDataModel` 的数据文件。

优点：

- 可继续复用现有 `EditorProjectFileManager`、MigratableSerializer 和部分测试基础。
- `EditorProjectDataModelBase` 与文件后缀的现有关系变化较小。
- 文件名较短，用户可能已经熟悉该后缀。

风险和代价：

- 同一个 `.nyagekiProj` 后缀在旧版本中表示“完整项目入口”，在新版本中只表示“内部状态资源”。
- 操作系统文件关联、旧程序和用户可能直接打开 ProjectData 文件，得到错误入口或不明确错误。
- 即使内容版本升级，后缀语义仍发生不兼容变化，不符合“抛开历史设计包袱”的目标。
- Provider 必须显式拒绝用户把 `.nyagekiProj` 当成新项目入口。

### 9.3 方案 B：ProjectData 使用新的 `.nyagekiProjectData`（未采用）

示例：

```text
SongA.nyagekiProjectManifest
SongA.nyagekiProjectData
```

优点：

- Manifest 与 ProjectData 的职责从文件名即可区分。
- 不会被旧程序误当作旧 `.nyagekiProj` 完整项目。
- 可以为两个格式独立制定版本号、序列化器和迁移策略。
- 文件类型注册可以只把 `.nyagekiProjectManifest` 暴露为可打开项目；ProjectData 保持内部资源，不出现在普通“打开项目”过滤器中。
- 与全新设计目标一致，后续删除旧 `.nyagekiProj` 入口没有语义冲突。

风险和代价：

- 需要新增 ProjectData 文件类型、序列化测试和迁移代码。
- 项目目录中会出现两个较长的新后缀。
- 现有 `EditorProjectFileManager` 需要重新命名或明确只管理 ProjectData。

### 9.4 方案 C：取消独立 ProjectData 文件，把两个状态字段嵌入 Manifest

示例：

```json
{
  "rememberLastDisplayTime": "00:00:12.500",
  "audioDuration": "00:02:36.100",
  "fumen": { "locator": "charts/master.ogkr" },
  "audio": { "locator": "audio/bgm.wav" }
}
```

优点：

- 项目只需要一个自定义元数据文件。
- 不需要 `ProjectResourceBinding Project`。

风险和代价：

- 直接违反当前已确认要求：Manifest 不引用这两个属性，而是通过 `Project` binding 指向独立 ProjectData。
- 每次保存播放位置都会重写项目入口 Manifest，把稳定结构与高频编辑器状态再次混在一起。
- Manifest 损坏同时丢失资源清单和编辑器状态，故障隔离更差。

### 9.5 最终决定

采用方案 A：ProjectData 继续使用 `.nyagekiProj`。

这项决定只复用文件后缀，不保留旧入口语义。新项目必须从 `.nyagekiProjectManifest` 打开，`.nyagekiProj` 仅由 Manifest 的 `Project` binding 定位和加载。

### 9.6 确认后的强制约束

- 新项目打开过滤器不得继续列出 `.nyagekiProj`。
- 新格式必须使用不同于旧 `0.5.2`、`0.5.4` 的内容版本，避免旧程序按旧结构静默接受文件。
- 旧 `.nyagekiProj` 的兼容处理必须是显式拒绝或显式导入，不能由新项目 Provider 猜测其缺失的 Manifest 绑定。

## 10. Q2（已确认）：Manifest 与新 ProjectData 应采用什么版本和迁移边界？

### 10.1 现有版本机制

当前 `EditorProjectFileManager` 按 JSON `Version` 精确选择序列化器，只登记 `0.5.2` 和 `0.5.4`，再通过 `MigratableSerializer` 把旧模型迁移到当前模型。旧 `.nyagekiProj` 同时包含编辑器状态和资源路径，而 R3 已确认的新 ProjectData 只包含 `AudioDuration` 与 `RememberLastDisplayTime`。

因此，新旧文件虽然共享 `.nyagekiProj` 后缀，却不是一次普通字段增删：旧文件缺少 Manifest，资源路径的权威来源也已经从 ProjectData 转移到 Manifest。仅把新模型继续标记为 `0.5.4` 会让旧 WPF 和现有解析器把两种不同语义视为同一格式。

### 10.2 已确认的版本

- Manifest 使用 `0.2.0`。
- ProjectData 使用 `0.6.0`。
- 两者版本序列相互独立。
- ProjectData 延续 `.nyagekiProj` 格式家族的版本序列，但 `0.6.0` 的模型只保留 `AudioDuration` 与 `RememberLastDisplayTime`。
- 旧 WPF 遇到 `0.6.0` 时应因未知版本明确拒绝，而不是按旧字段语义误读。

### 10.3 Q2b 方案 A：允许 Manifest 内的旧 ProjectData 自动内存迁移（已采用）

- 只有先成功解析并验证 `0.2.0` Manifest 后，才允许其 `Project` binding 指向 `0.5.2` 或 `0.5.4` 的 `.nyagekiProj`。
- `EditorProjectFileManager` 将旧 ProjectData 在内存中迁移为 `0.6.0`，只保留 `AudioDuration` 与 `RememberLastDisplayTime`。
- 旧 `AudioFilePath`、`FumenFilePath`、`Id`、`EditorSetting` 和 bullet palette 数据不进入新 ProjectData；资源文件只服从 Manifest binding。
- 仅打开项目不改写文件。迁移后的 ProjectData 标记为待保存，第一次成功执行正常保存时写成 `0.6.0`。
- 用户直接选择一个没有 Manifest 的旧 `.nyagekiProj` 时，正常打开流程仍拒绝；是否提供旧项目导入器由后续问题决定。

该方案能复用现有精确版本解析和迁移框架，同时由 Manifest 提供旧文件本身无法提供的新资源绑定边界。

### 10.4 Q2b 方案 B：Manifest 只能绑定 `0.6.0` ProjectData（未采用）

- 正常 Manifest 加载链拒绝 `0.5.2/0.5.4` ProjectData。
- 旧格式解析器只存在于显式旧项目导入器中；导入器生成 Manifest 和新的 `0.6.0` ProjectData 后，项目才可正常打开。
- 加载规则最简单，但即使 Manifest 已经给出完整、可信的资源 binding，也不能直接复用旧文件中的两个兼容状态字段。

### 10.5 最终决定

采用 Q2b 方案 A：允许有效 `0.2.0` Manifest 绑定的旧 ProjectData 自动在内存迁移到 `0.6.0`，但没有 Manifest 的旧 `.nyagekiProj` 仍不得进入正常打开流程。

### 10.6 确认后的加载规则

- `0.5.2/0.5.4` 的识别和迁移发生在 ProjectData 解析层，但必须由已验证的 Manifest 加载流程调用。
- 迁移器不得使用旧 `AudioFilePath`、`FumenFilePath` 查找资源。
- 加载结果应携带“ProjectData 待升级保存”状态；不能通过比较内存模型版本猜测文件是否已升级。
- 首次保存升级失败时保留原 `0.5.2/0.5.4` 文件，并继续把当前编辑器视为未保存状态。

## 11. Q3a（已确认）：Manifest 是否需要稳定的 `ProjectId`？

### 11.1 当前代码事实

- 旧 `EditorProjectDataModel` 的 `Guid Id` 目前没有生产业务引用，主要出现在序列化和测试中。
- `RecentRecordInfo.RecordId` 是最近记录管理器为每个本机列表项生成的随机 GUID，只标识该条设置记录，不标识项目本身。
- 当前项目最近记录通过目录书签和项目文件书签字符串完全相等来尝试去重。书签是平台不透明数据，重新授权、移动目录或平台改变后可能变化。
- R3 已确认新的 ProjectData 只包含两个时间字段，因此项目身份若继续存在，应由 Manifest 承担。

### 11.2 方案 A：Manifest 声明稳定 `Guid ProjectId`（已采用）

```csharp
public sealed class EditorProjectManifest
{
    public static readonly Version VERSION = new(0, 2, 0);

    public required Guid ProjectId { get; init; }

    public required ProjectResourceBinding Project { get; init; }
    public required ProjectResourceBinding Fumen { get; init; }
    public required ProjectResourceBinding Audio { get; init; }
}
```

- 新建项目时生成非空随机 GUID，普通保存和资源换绑保持不变。
- 最近记录快照保存 `ProjectId`，书签只负责恢复平台文件能力，不再承担项目身份语义。
- 打开 Manifest 时拒绝 `Guid.Empty`；不根据目录名、文件名或内容哈希补造身份。
- 项目目录移动或重新授权后，最近记录仍可识别为同一逻辑项目。
- 文件系统复制、显式“另存为”和导入时是否保留该 ID，需要在确认存在 `ProjectId` 后继续细分。

### 11.3 方案 B：Manifest 不保存稳定身份（未采用）

- 最近记录继续以书签组合或位置描述作为近似身份。
- 实现字段更少，但同一项目重新授权后可能产生重复最近项，位置移动也无法可靠关联旧记录。
- 书签等值只能证明两个缓存令牌文本相同，不能成为跨平台、跨授权周期的项目标识。

### 11.4 最终决定

采用方案 A：在 Manifest 中增加必填、非空的 `Guid ProjectId`，并明确它是项目逻辑身份；`RecentRecordInfo.RecordId` 继续只作为本机最近记录行的主键。

### 11.5 确认后的约束

- `ProjectId` 必须参与 Manifest 序列化和验证。
- `Guid.Empty` 是格式错误，不能在加载时静默补造。
- 最近记录 data 必须保存该 ID，但不能用本机 `RecentRecordInfo.RecordId` 回写 Manifest。
- 新 ProjectData 不重新引入旧 `Id` 字段。

## 12. Q3b（已确认）：哪些项目操作应保留或重新生成 `ProjectId`？

### 12.1 当前实现边界

当前编辑器只实现普通 `Save()`；`SaveAs()` 明确返回不可用，项目级复制和导入也尚未形成生产流程。因此这里定义的是新格式的未来契约。

### 12.2 方案 A：按“同一项目”与“新项目”区分（已采用）

保留 `ProjectId` 的操作：

- 普通保存。
- 修改 Manifest 中的 Project/Fumen/Audio binding。
- 重命名 Manifest、资源文件或项目目录。
- 把整个项目目录移动到其他位置、设备或平台。

生成新 `ProjectId` 的应用内操作：

- “另存为”到新的项目目录。
- “复制项目”或“从模板创建项目”。
- 显式创建一个可独立编辑、独立出现在最近记录中的项目副本。

操作系统文件管理器直接复制整个目录时，应用无法在复制发生时改写 Manifest，因此副本会暂时保留原 `ProjectId`。最近记录按 Manifest 位置分别保存这两个副本；重复 `ProjectId` 只作为逻辑身份冲突供诊断或显式分叉操作使用。

### 12.3 方案 B：所有复制和移动都保留 `ProjectId`（未采用）

- 把 `ProjectId` 解释为项目谱系而非独立项目实例。
- “另存为”产生的两个可独立编辑目录会共享逻辑身份；即使最近记录按位置分开，诊断、同步或未来项目级关联仍无法明确区分它们。

### 12.4 方案 C：位置变化就生成新 `ProjectId`（未采用）

- 可以避免复制冲突，但目录移动、重命名或重新授权会丢失稳定身份的主要价值。
- Browser 与 Desktop 对“位置变化”的可观察能力不同，跨平台语义不一致。

### 12.5 最终决定

采用方案 A：同一逻辑项目的移动、重命名、普通保存和资源换绑保留 ID；应用内“另存为/复制项目”生成新 ID。

### 12.6 确认后的约束

- 普通加载和保存不得修改 Manifest 的 `ProjectId`。
- “另存为/复制项目”的事务必须先生成新 ID，再写目标 Manifest；失败时不能污染源项目 ID。
- 外部复制产生的重复 ID 不在打开时自动修复，避免一次只读打开意外改写用户文件。

## 13. Q3c（方向已确认）：最近记录使用什么项目判定依据？

### 13.1 已确认方向

最近记录根据 Manifest 对应 `ISimpleFile` 的文件位置判定，不根据 `ProjectId` 合并：

- 相同 Manifest 位置更新原记录。
- 不同 Manifest 位置创建独立记录。
- 项目移动到新位置后创建新记录，旧位置记录按普通失效规则处理。
- 文件管理器复制出的项目因 Manifest 位置不同，自然保留为两个最近项，即使两份 Manifest 的 `ProjectId` 相同。

### 13.2 当前 `ISimpleFile` 位置字段的限制

`ISimpleFile` 当前暴露：

- `LocalPath`：底层 Provider 提供本地文件系统路径时才有值；Browser、`content:` URI 和其他非本地 Provider 可以为 `null`。
- `FullPath`：定义为 SimpleFileSystem 内的虚拟路径，不保证是本地绝对路径或全局唯一位置。

当前 Avalonia StorageProvider 包装还有两种不同表现：

- 直接从 `IStorageFile` 构建独立 `ISimpleFile` 时，`FullPath` 来自 `IStorageFile.Path.ToString()`。
- 从选中的项目目录递归构建时，根目录 `FullPath` 为空，Manifest 的 `FullPath` 只是类似 `SongA.nyagekiProjectManifest` 的项目内相对路径；两个不同项目目录可能得到完全相同的值。

因此不能直接使用 `LocalPath ?? FullPath` 作为跨平台最近记录键。

### 13.3 Q3c1 最终决定：只采用 `FullPath`

- 不新增 `StorageLocation`。
- 不使用 `LocalPath ?? FullPath` 组合；判重输入始终是 ManifestFile 的 `FullPath`。
- 不使用 `ProjectId`、bookmark、显示名称或目录名称作为回退判重依据。
- 最近记录 data 保存 ManifestFile 的 `FullPath`，查找已有记录时比较该字段。

### 13.4 ManifestFile 构造不变量

为了使上述决定可用，ManifestFile 必须与普通项目资源文件采用不同的构造要求：

- 用户直接选择 Manifest 文件时，`FullPath` 使用底层 `IStorageFile.Path.ToString()`。
- 用户选择项目目录并由 Provider 扫描 Manifest 时，Provider 必须保留发现到的 Manifest `IStorageFile.Path`，或重新取得该文件的独立 `IStorageFile`，再构造 ManifestFile。
- 不允许把 `FullPath == "SongA.nyagekiProjectManifest"` 这类仅项目内相对路径的目录树子项直接用作 ManifestFile。
- 其他 ProjectData/Fumen/Audio 文件仍可保留项目内虚拟 `FullPath`；该特殊不变量只针对作为项目入口和最近记录依据的 ManifestFile。
- 如果 ManifestFile 的 `FullPath` 为空或不是完整位置，项目仍可打开，但本次不创建或更新最近记录。

### 13.5 Q3c2（已确认）：`FullPath` 应如何比较？

#### 方案 A：规范化后按位置语义比较（未采用）

- 绝对 `file:` URI 转换为规范化本地绝对路径；Windows 使用 `OrdinalIgnoreCase`，区分大小写的平台使用 `Ordinal`。
- 其他绝对 URI 规范化 scheme、host 和转义形式后使用 `Ordinal` 比较其规范字符串。
- 不是绝对路径或绝对 URI 的值不参与最近记录判重。
- 最近记录仍只保存和使用 `FullPath`；规范化只是比较过程，不引入第二个身份字段。

#### 方案 B：原始字符串 `Ordinal` 精确比较（已采用）

- 实现最直接。
- 同一本地文件若因盘符大小写、分隔符、URI 转义或 Provider 表达差异产生不同字符串，会出现重复最近记录。

### 13.6 最终决定

采用 Q3c2 方案 B：直接按 `StringComparison.Ordinal` 比较原始 ManifestFile `FullPath`。

### 13.7 确认后的约束

- 最近记录 data 不额外保存规范化键。
- Provider 返回值发生任何字符变化时均视为不同位置。
- 判重代码不得根据当前操作系统切换比较器。

## 14. Q3d（已确认）：旧项目导入时如何生成 Manifest `ProjectId`？

### 14.1 现有旧字段

`0.5.2/0.5.4` ProjectData 都继承旧模型中的 `Guid Id`。该字段默认初始化为随机 GUID，现有迁移测试也验证它能够跨序列化保留；但旧格式没有强制校验，文件仍可能显式保存 `Guid.Empty`。

### 14.2 方案 A：继承有效旧 `Id`，空值时生成新 ID（已采用）

- 显式导入一个没有 Manifest 的旧项目时，若旧 ProjectData `Id != Guid.Empty`，将其写入新 Manifest 的 `ProjectId`。
- 若旧 `Id == Guid.Empty`，生成新的随机非空 GUID。
- 导入完成后的 `0.6.0` ProjectData 不再保存 `Id`；身份只存在于 Manifest。
- 如果项目已经有有效 Manifest，则 Manifest `ProjectId` 始终权威，加载绑定的旧 ProjectData 时忽略其旧 `Id`。

这把旧项目导入视为同一逻辑项目的格式升级，并保留旧文件已经携带的稳定标识。

### 14.3 方案 B：导入时始终生成新 `ProjectId`（未采用）

- 不信任旧 `Id`，规则最简单。
- 同一个旧项目在不同机器或不同时间重复导入会得到不同逻辑身份，无法表达它们来自同一旧项目。

### 14.4 最终决定

采用方案 A：有效旧 `Id` 迁移到 Manifest；只有空 ID 才生成新值。

### 14.5 确认后的约束

- 导入流程必须先完整解析旧 ProjectData，再决定新 Manifest 的 `ProjectId`。
- 不允许把 `Guid.Empty` 写入 Manifest。
- 旧 `Id` 只迁移到 Manifest，不迁移到 `0.6.0` ProjectData。

## 15. Q4a（已确认）：Manifest binding locator 是否必须已经是规范形式？

### 15.1 现有兼容行为

当前 `EditorProjectPathResolver` 面向旧 `.nyagekiProj`：

- 同时接受 `/` 与 `\`。
- 消除 `.` 路径段。
- 允许 `..` 在不越过所选项目根时回退目录。
- 忽略重复分隔符产生的空路径段。
- 在内存中统一输出 `/`。

新设计中 Manifest 的父目录就是项目根，三个 binding 都必须位于该目录或子目录中，且 Manifest 由新应用生成，不需要继承上述宽松输入语义。

### 15.2 方案 A：Manifest 只接受完全规范化 locator（未采用）

一个合法 binding locator 必须同时满足：

- 非空且不以 `/` 开头或结尾。
- 只使用 `/`，不包含 `\`。
- 不包含空路径段，因此禁止 `//`。
- 不包含 `.` 或 `..` 路径段。
- 不是绝对路径，不含盘符或 URI scheme。
- 解析后至少包含一个文件名段。

Manifest 加载器只验证，不自动修正。创建项目、修改 binding 和旧项目导入流程必须在写 Manifest 前生成规范形式。

优点是同一资源只有一种持久化 locator 表达，签名、差异比较、事务判断和跨平台测试都更确定。代价是手工编辑或第三方生成的宽松路径会被直接拒绝。

### 15.3 方案 B：加载时兼容所有旧式写法（未采用）

- 继续接受反斜杠、`.`、安全范围内的 `..` 和重复分隔符。
- 内存中规范化，下一次保存再写回标准形式。
- 容错更强，但原始 Manifest 文本与实际解析目标可能不同，也增加路径边界和诊断分支。

### 15.4 方案 C：严格格式，仅兼容 `.` 路径段（已采用）

- 采用方案 A 的全部严格规则，但允许 `.` 路径段。
- 加载时消除 `.`；保存时只写消除后的 locator。
- `..`、反斜杠、重复分隔符和绝对形式仍直接拒绝。

### 15.5 最终决定

采用方案 C：Manifest locator 采用严格格式，只对 `.` 路径段提供读取兼容。

### 15.6 确认后的约束

- `./audio/bgm.wav` 与 `audio/./bgm.wav` 都解析为 `audio/bgm.wav`。
- `.`、`./.` 等消除后没有文件名的 locator 无效。
- Manifest 序列化器不得输出 `.` 路径段。

## 16. Q4b（已确认）：binding locator 的大小写匹配采用什么规则？

### 16.1 当前代码行为

现有 `EditorProjectPathResolver` 在所有平台使用 `StringComparison.OrdinalIgnoreCase` 逐段查找，并把目录枚举得到的实际大小写写入解析结果。如果同一层存在两个仅大小写不同且都匹配的候选，则报告冲突，不任意选择。

### 16.2 方案 A：统一 `OrdinalIgnoreCase`，冲突时报错（推荐）

- Desktop、Browser 和其他 Provider 使用相同的大小写无关逻辑语义。
- locator `charts/map.ogkr` 可以匹配实际文件 `Charts/Map.ogkr`。
- 成功解析后保留实际文件名大小写，后续保存 Manifest 时可写回 `Charts/Map.ogkr`。
- 如果同时存在 `Assets/logo.svg` 与 `assets/Logo.svg`，任何大小写无关匹配到二者的 locator 都失败并报告重名冲突。

该方案不依赖底层文件系统是否区分大小写，项目复制到其他平台时行为稳定。

### 16.3 方案 B：统一 `Ordinal` 严格区分大小写

- locator 必须与每个目录和文件名的实际大小写完全一致。
- 规则最明确，也允许区分大小写的平台合法引用两个仅大小写不同的文件。
- 在 Windows 等常见不区分大小写环境中，手工大小写差异会导致项目无法打开，跨平台搬运更脆弱。

### 16.4 方案 C：跟随底层 Provider

- 本地表现最符合宿主文件系统。
- 同一个项目在 Windows、Linux、Browser 或不同 Provider 中可能解析到不同结果，不符合统一便携格式目标。

### 16.5 最终决定

采用方案 A：统一 `OrdinalIgnoreCase` 查找，保留实际大小写；出现多个大小写无关候选时明确失败。

### 16.6 确认后的约束

- 比较器固定为 `StringComparison.OrdinalIgnoreCase`，不能根据 Windows、Linux、Browser 或当前 Provider 切换。
- 逐段查找目录和最终文件时应用相同规则。
- 实际名称大小写只用于生成规范 locator，不改变匹配语义。
- 错误信息必须指出发生冲突的 locator 段，不能把冲突伪装成“文件不存在”。

## 17. Q4c（已确认）：binding locator 的 Unicode 规范化采用什么规则？

### 17.1 当前代码行为和跨平台风险

现有 `EditorProjectPathResolver` 直接对原始 .NET 字符串执行 `OrdinalIgnoreCase`，没有调用 `string.Normalize(...)`。因此视觉上相同的名称仍可能不相等，例如预组合字符 `é` 与 `e` 加组合重音符使用不同的码点序列。

不同文件系统和 Provider 可能保留或返回不同的 Unicode 组合形式。如果 Manifest 保存 NFC 名称，而目标平台枚举得到 NFD 名称，单纯采用 R16 的 `OrdinalIgnoreCase` 仍可能找不到文件。同一目录还可能存在多个在 Unicode 规范化后等价的名称，必须定义冲突行为。

### 17.2 方案 A：统一转换为 NFC 后再执行 `OrdinalIgnoreCase`（未采用）

- 加载 locator 时把每个普通路径段转换为 Unicode NFC。
- 比较时也把 Provider 枚举得到的目录名和文件名转换为 NFC，再使用 `OrdinalIgnoreCase`。
- 创建或保存 Manifest 时，持久化实际名称的 NFC 形式，因此新写入 locator 只有一种规范表达。
- 读取已有 Manifest 时可以接受 NFD 等规范等价写法，在内存中转为 NFC；下次因绑定变化而保存 Manifest 时写回 NFC。
- 如果同一层有多个名称在 NFC 加 `OrdinalIgnoreCase` 后相等，解析失败并报告 Unicode/大小写规范化名称冲突。
- 不使用 NFKC；兼容性规范化会把全角字符、圈号和其他本应可区分的字符折叠在一起。

该方案与 R16 共同建立跨平台逻辑名称语义，同时仍允许中文、日文和其他 Unicode 文件名。

### 17.3 方案 B：不做 Unicode 规范化（已采用）

- 继续只使用原始字符串 `OrdinalIgnoreCase`。
- 实现最简单，也完整保留 Provider 返回的码点序列。
- 项目复制到采用不同 Unicode 文件名表示方式的平台后，视觉相同的 locator 可能无法解析，便携性依赖底层 Provider。

### 17.4 方案 C：locator 只允许 ASCII（未采用）

- 可以避开 Unicode 规范化差异，规则和测试最简单。
- 会拒绝中文、日文及其他非 ASCII 目录名和文件名，与现有项目和目标用户场景不符。

### 17.5 最终决定

采用方案 B：不做任何 Unicode 规范化，直接对原始字符串执行 `OrdinalIgnoreCase`。

### 17.6 确认后的约束

- locator 验证器和解析器不得调用 `string.Normalize(...)` 或 `IsNormalized(...)` 改变匹配结果。
- 保存 binding 时保留 Provider 返回的实际 Unicode 码点序列，只统一路径分隔符并消除允许读取的 `.` 段。
- NFC 与 NFD 等规范等价形式不会互相匹配；由此导致的跨平台解析失败是当前方案接受的结果。
- 不引入仅用于 Unicode 规范化的隐藏比较键或备用 locator。

## 18. Q5a：最近记录是否需要为 ProjectData、Fumen 和 Audio 分别保存必需书签？

### 18.1 当前实现与新设计的冲突

当前未完成实现中的 `EditorFileAccessContextSnapshot` 保存项目目录、ProjectFile、Fumen 和 Audio 的独立书签，其中 Fumen 和 Audio 书签属于反序列化必需字段。恢复时会直接打开这些文件书签并构造上下文；任一必需书签缺失或失效，整条最近记录都会被判定无效。

新设计已经确认：

- 所有主文件都位于 Manifest 项目目录内。
- Manifest 的三个 binding 是文件关联的唯一权威来源。
- binding 可以在项目正常使用期间变化。
- 最近记录和书签只是重新取得平台权限的本机缓存，不能覆盖 Manifest。

因此，分别要求 ProjectData、Fumen 和 Audio 书签会复制 Manifest 已保存的绑定状态。旧子文件书签可能在 binding 更新后仍指向旧文件，也可能独立过期，使项目目录和 Manifest 明明有效时，最近记录仍被错误置灰。

### 18.2 方案 A：只有项目目录书签是必需权限书签（推荐）

最近记录保存：

- 必需的 `ProjectDirectoryBookmark`。
- 精确的 Manifest 项目内 locator；按当前目录不变量通常就是 Manifest 文件名。
- 用于 R12 判重的原始 `ManifestFullPath`。
- 可用于校验和诊断的 `ProjectId`。

恢复时先打开项目目录书签，再定位精确 Manifest、读取当前三个 binding，并重新构造全部文件能力。不保存 ProjectData、Fumen 或 Audio 的独立书签。

优点：

- 权限状态最少，Manifest 始终是唯一绑定权威。
- binding 变化不会留下必须同步更新的子文件书签。
- 子文件被替换但 locator 仍有效时，最近记录可以正常恢复。
- 失效判断只围绕项目目录权限和精确 Manifest 是否存在。

代价是恢复时必须重新枚举项目目录并解析 Manifest；现有快照和目录构建器需要重构，不能继续直接依赖三个文件书签。

是否额外保存一个可选的 ManifestFile 书签，将在 Q5b 单独确认，不包含在本题中。

### 18.3 方案 B：子文件书签作为可选且必须验证的快速缓存

- 项目目录书签仍是唯一必需权限书签。
- 可以额外保存 ProjectData、Fumen、Audio 书签，但任何一个失效时都退回 Manifest locator 解析，不能判整条记录失效。
- 每次使用缓存前必须确认它仍对应当前 binding，绑定改变时更新或清除缓存。

该方案可能减少部分目录查找，但引入缓存一致性、敏感书签存储和独立过期处理，首版收益有限。

### 18.4 方案 C：继续要求所有角色文件都有独立书签

- 恢复可以直接取得各角色文件句柄。
- 任一文件书签失效都会使最近记录不可用，即使项目目录和 Manifest 仍然有效。
- binding 改变时必须事务性刷新最近记录中的全部相关书签，否则快照会恢复旧绑定。

该方案使最近记录快照变成第二份绑定配方，与 Manifest 权威模型冲突。

### 18.5 推荐答案

采用方案 A：最近记录只把项目目录书签作为必需权限能力，不保存 ProjectData、Fumen、Audio 的独立书签。

### 18.6 请确认的唯一问题

是否确认采用方案 A，把 ProjectData、Fumen、Audio 书签从新快照契约中删除？

## 19. 变更记录

### 2026-08-16

- 实施前置重构（非 Manifest 新格式本体）：`EditorProjectDataModel` 改造为纯数据类。
  - 移除 `EditorProjectDataModel` 上的 `FileAccessContext`、`AudioFile`、`AudioAwbFile`、`FumenFile`、`ProjectFile`、`ProjectRoot`、`ProjectFileLocator`、`RecentRecordId`、`DisposeRuntimeFiles()` 等运行时成员。
  - 移除 `EditorProjectDataModel_V0_5_2` 上的运行时成员 `Fumen` 与 `BaseBPM`；`CanEditBaseBpm`（由持久化的 `FumenFilePath` 派生）保留。
  - 新增运行时上下文 `EditorContext`（`Models/EditorContext.cs`）：持有 `ProjectData`、`Fumen`、`FileAccessContext`、`ProjectFileLocator`、`RecentRecordId` 与 `BaseBPM`，`Dispose()` 释放谱面 SVG prefab 与文件能力。
  - 加载（`TryLoadFromFileAsync` / `TryLoadFromContextAsync`）、保存（`TrySaveEditorAsync` / `TrySaveProjFileAsync` / `TrySaveFumenFileAsync`）、`FumenVisualEditorViewModel`（新增 `EditorContext` 属性，`IsNew`/`Save`/`Dispose` 改走上下文）、Setup 对话框、`DocumentOpenHelper`、`FumenRescue`、自动保存与最近记录逻辑全部迁移到 `EditorContext`。
  - 持久化契约冻结：`.nyagekiProj` 序列化字段与 `0.5.4` 版本号不变，被移除成员均为 `[JsonIgnore]` 运行时成员；相关测试（440 项）全部通过。

### 2026-08-14

- 建立本持续审阅文档。
- 确认 R1：所有主文件必须位于 Manifest 项目目录中。
- 确认 R2：项目入口后缀为 `.nyagekiProjectManifest`。
- 确认 R3：新的 `EditorProjectDataModel` 只声明 `AudioDuration`、`RememberLastDisplayTime`。
- 确认 R4：Manifest 保存项目身份与三个资源 binding，不复制 ProjectData 状态。
- 确认 R5：运行时只使用 `EditorFileAccessContext` 的直接文件能力。
- 确认 R6 / Q1：ProjectData 继续使用 `.nyagekiProj`，但不再作为项目入口。
- 确认 R7 / Q2a：Manifest 使用 `0.2.0`，ProjectData 使用 `0.6.0`。
- 确认 R8 / Q2b：有效 Manifest 绑定的旧 ProjectData 允许自动内存迁移，首次保存才写为 `0.6.0`。
- 确认 R9 / Q3a：Manifest 声明稳定、非空的 `Guid ProjectId`。
- 确认 R10 / Q3b：同一项目操作保留 ID，应用内创建独立副本时生成新 ID。
- 确认 R11 / Q3c：最近记录按 Manifest 文件位置判定，不按 `ProjectId` 合并。
- 确认 R12 / Q3c1：ManifestFile 的 `FullPath` 是最近记录唯一判重键，其他字段不回退。
- 确认 R13 / Q3c2：ManifestFile `FullPath` 使用原始字符串 `Ordinal` 精确比较。
- 确认 R14 / Q3d：旧项目导入继承非空旧 `Id`，空值时生成新 `ProjectId`。
- 确认 R15 / Q4a：Manifest locator 采用严格格式，但读取时允许并消除 `.` 路径段。
- 确认 R16 / Q4b：binding locator 统一使用 `OrdinalIgnoreCase` 匹配，大小写冲突时失败。
- 确认 R17 / Q4c：binding locator 不做 Unicode 规范化，直接比较原始字符串。
- 新增 Q5a：确认最近记录的必需权限书签范围。
