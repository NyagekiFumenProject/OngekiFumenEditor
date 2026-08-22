# Desktop FastOpen 设计方案

## 1. 文档目的

本文记录将原 WPF 项目的 FastOpen 功能恢复到 Avalonia Desktop 的方案。

本方案的核心边界是：

- FastOpen 的文件选择、本地路径处理、音频自动发现和 Desktop UI 入口属于 Desktop；
- Core 不解析 Desktop 路径、不扫描本地目录，也不依赖 Desktop 程序集；
- Core 只消费 Desktop 已经构造并验证好的 `EditorContext` 与 `EditorFileAccessContext`；
- 本文中的“AccessFileContext”统一指当前代码中的 `EditorFileAccessContext`。

本文是实现设计文档，不表示 FastOpen 已经恢复。

## 最终决策（2026-08-22）

1. Core 提供 `SplashScreenViewModelBase`、公共 Splash 功能和可复用公共视图，不提供平台具体 Splash 窗口。
2. Desktop 和 Browser 分别实现自己的 Splash View/ViewModel 子类，并在各自组合根注册为 `ISplashScreenWindow`。
3. Desktop 具体 Splash ViewModel 拥有 FastOpen 按钮，并直接调用 Desktop FastOpen 服务；Browser 不包含 FastOpen，而是提供自己的平台功能按钮。
4. Core 不实现 `Music.xml`、`musicsource`、本地路径扫描、StorageProvider 文件绑定或 FastOpen 上下文构造。
5. `DocumentOpenHelper` 和本地启动参数打开逻辑从 Core 移到 Desktop；Core 只保留通用编辑器上下文和 Provider 能力。

## 2. 当前状态

### 2.1 原 WPF 行为

原项目的 FastOpen 流程位于：

- `OngekiFumenEditor/Modules/FumenVisualEditor/Commands/OgkrImpl/FastOpenFumen/FastOpenFumenCommandHandler.cs`
- `OngekiFumenEditor/Utils/DocumentOpenHelper.cs`

行为如下：

1. 通过文件选择器选择一个 `.ogkr` 或 `.nyageki` 文件。
2. 读取谱面文件同目录的 `Music.xml`。
3. 优先从 `MusicSourceName/id` 取得歌曲 ID。
4. 如果 XML 没有有效 ID，则从谱面文件名匹配 `(\d+)_\d+`。
5. 根据歌曲 ID 查找 `musicsourceNNNN/musicNNNN.*` 音频。
6. 自动发现失败时，让用户手动选择音频文件。
7. 解析谱面，构造内存工程数据，不创建 `.nyagekiProj`。
8. 以 `[FastOpen]` 前缀显示文档，并打开编辑器。

FastOpen 初始状态没有正式工程文件，谱面和音频是运行时绑定的文件能力。

### 2.2 Avalonia 当前代码

当前 Core 仍保留迁移中的 FastOpen 代码，但没有形成可用入口：

- `Modules/FumenVisualEditor/Commands/OgkrImpl/FastOpenFumen/FastOpenFumenCommandHandler.cs` 的 Injectio 注册已注释；
- `FastOpenFumenCommandDefinition.cs` 的注册已注释；
- `Commands/OgkrImpl/MenuDefinitions.cs` 的菜单项已注释；
- `Modules/SplashScreen/Views/SplashScreenView.axaml` 的 FastOpen 卡片已注释；
- `Utils/DocumentOpenHelper.cs` 的 FastOpen 分支受未定义的 `ENABLE_CROSS_PLATFORM_FAST_OPEN` 保护，正常构建不会进入；
- `FumenVisualEditorProviderBase.TryOpen(document, EditorContext)` 已经可以把完整上下文交给编辑器；
- `EditorFileAccessContext.ProjectFile` 当前允许为空，但 `FumenFile` 和 `AudioFile` 仍是编辑器加载所需的必需能力。

因此，这次工作不是简单解开几处注释，而是把 Desktop 逻辑从共享 Core 中抽出，并接入现有上下文加载边界。

## 3. 目标架构

### 3.1 程序集职责

```text
Core
  EditorContext
  EditorFileAccessContext
  FumenVisualEditorProviderBase.TryOpen(document, context)
  SplashScreenViewModelBase
  SplashScreenCommonView.axaml

Desktop
  DesktopSplashScreenViewModel
  DesktopSplashScreenView.axaml
  DesktopFastOpenService
  FastOpenCommandDefinition
  FastOpenCommandHandler
  FastOpen 菜单与 Ctrl+F 快捷键
  Music.xml / musicsource / 本地路径解析
  Desktop StorageProvider 文件选择

Browser
  BrowserSplashScreenViewModel
  BrowserSplashScreenView.axaml
  Browser 专用启动按钮和服务
```

Core 只保留 Splash 的公共功能和公共布局，不应保留 Desktop FastOpen 的实现细节。具体禁止以下内容进入 Core：

- `File.Exists`、`Directory.Exists`、`Directory.GetFiles`、`Directory.GetDirectories`；
- 根据谱面文件路径推导音频目录；
- 读取 `Music.xml` 作为 Desktop 文件发现协议；
- 直接引用 `FastOpenFumenCommandDefinition`；
- 通过字符串路径构造 FastOpen 的 `EditorFileAccessContext`；
- Desktop FastOpen loader、command handler、菜单定义和本地路径服务。

### 3.2 Core 基类与平台子类边界

Core 的 `SplashScreenViewModelBase` 只实现所有平台共有的功能：

- 语言切换和重启提示；
- 最近文件列表和刷新；
- 通用的新建项目、打开项目、教程入口；
- `ISplashScreenWindow.WindowViewModel` 所需的窗口生命周期。

Desktop 子类额外实现 FastOpen 操作：

```csharp
public sealed partial class DesktopSplashScreenViewModel : SplashScreenViewModelBase
{
    private readonly DesktopFastOpenService fastOpenService;

    [RelayCommand]
    private Task FastOpenAsync() => fastOpenService.OpenAsync();
}
```

`DesktopFastOpenService` 负责命令、文件选择、错误提示、谱面加载及 Shell 打开。Browser 子类可以注入 OPFS 或其他 Browser 服务，并添加自己的命令。

Core 不需要知道这些平台服务：

- Core 基类不声明 FastOpen 属性、命令或服务接口；
- Desktop View 绑定 Desktop 子类的 `FastOpenCommand`；
- Browser View 只绑定 Browser 子类提供的功能；
- `EditorContext` 仍是通用编辑器运行时契约，FastOpen 上下文构造仍由 Desktop 完成。

启动参数处理仍应由 Desktop 实现，并调用 Desktop 文档打开服务。Core 不保留 `DefaultArgProcessManager` 的本地路径打开实现。

## 4. Desktop FastOpen 流程

### 4.1 用户触发路径

```text
Desktop 菜单 FastOpen / Ctrl+F / Desktop Splash FastOpen
  -> DesktopFastOpenService.OpenAsync()
  -> TopLevel.StorageProvider.OpenFilePickerAsync()
  -> 取得 .ogkr 或 .nyageki
  -> 发现或选择 AudioFile
  -> 解析 FumenFile
  -> 构造 EditorContext
  -> IFumenVisualEditorProvider.Create()
  -> IFumenVisualEditorProvider.TryOpen(editor, context)
  -> IShell.OpenDocumentAsync(editor)
```

文件选择必须从当前 `Window` 或 `TopLevel` 取得 `StorageProvider`，不要在服务中缓存静态的 StorageProvider。现有 `FileDialogHelper` 可以继续作为薄的 Avalonia 文件选择适配层使用，但 FastOpen 的发现算法应属于 Desktop 服务。

### 4.2 文件类型过滤

FastOpen 文件选择器只允许：

- `*.ogkr`
- `*.nyageki`

音频手动选择器复用 `IAudioManager.SupportAudioFileExtensionList`。Desktop 允许 ACB；是否能使用外置 AWB 由后续音频绑定校验决定。

### 4.3 音频自动发现

建议将旧 `GetAudioFilePath` 改造成 Desktop 内部的纯发现服务，例如 `DesktopFastOpenAudioResolver`。它接收已经选中的谱面文件能力，返回候选音频能力和发现结果，不直接修改编辑器状态。

发现顺序保持 WPF 兼容：

1. 读取谱面所在目录的 `Music.xml`。
2. 读取 `MusicSourceName/id`，并解析为整数歌曲 ID。
3. 如果 XML 没有有效 ID，从谱面文件名匹配 `(\d+)_\d+`。
4. 将 ID 格式化为四位数字，例如 `1` 变成 `0001`。
5. 首先检查谱面目录上两级的 `musicsource/musicsourceNNNN`。
6. 如果谱面路径位于 `package` 树内且第一处不存在，允许在受约束的 package 根内递归查找 `musicsourceNNNN`。
7. 在目标目录中查找 `musicNNNN.*`，只接受音频管理器声明的扩展名。
8. 找不到唯一有效音频时，打开手动音频选择器。

实现时必须：

- 使用 `Path.GetFullPath` 规范化本地路径；
- 只在明确的 package 根内递归，不能从任意用户路径向整个磁盘扩散；
- 比较扩展名时不区分大小写；
- 对 XML 缺失、ID 非法、目录不存在和多个候选分别记录可诊断日志；
- 不因为发现失败而提前释放仍由调用方持有的谱面文件；
- 手动选择取消时返回取消结果，而不是把取消当作“找不到音频”的异常。

### 4.4 显示名称

显示名生成也属于 Desktop FastOpen 服务：

1. 默认使用谱面文件名。
2. 如果同目录存在 `Music.xml`，读取 `Name/str` 的第一个值作为友好名称。
3. 最终名称为 `[{Lang.FastOpen}] {name}`。

该名称只用于文档显示，不参与文件恢复、最近记录身份或路径解析。

## 5. `EditorContext` 构造和所有权

### 5.1 FastOpen 上下文形态

FastOpen 不应虚构临时 `.nyagekiProj`。Desktop 应构造以下运行时状态：

```text
EditorContext.ProjectData = new EditorProjectDataModel
EditorContext.Fumen = 已反序列化的 OngekiFumen
EditorContext.FileAccessContext =
  ProjectDirectory = 可选
  ProjectFile = null
  FumenFile = 已选择的谱面文件
  AudioFile = 自动发现或手动选择的音频文件
  AudioAwbFile = ACB 外置 AWB 存在时绑定
```

`ProjectData.AudioDuration` 使用实际加载的音频时长填充。FastOpen 不应调用要求 `ProjectFile` 非空的 `EditorProjectDataUtils.TryLoadFromContextAsync`，因为该方法是完整 `.nyagekiProj` 项目加载器。

Desktop FastOpen 加载器可以直接调用 Core 已有的 `IFumenParserManager`、`IAudioManager` 和 `EditorContext` 类型，但不应为 FastOpen 在 Core 新增专用解析方法。Core 只提供通用解析器和运行时模型。

### 5.2 推荐转交顺序

```text
1. 选择谱面文件能力
2. 发现或选择音频文件能力
3. 解析谱面并创建 EditorProjectDataModel
4. 创建 EditorFileAccessContext
5. 创建 EditorContext
6. 调用 editor.LoadProjectAsync(context, sourcePath)
7. 成功后由编辑器接管 context
8. 失败或取消时释放 context
```

在第 7 步之前，Desktop 服务拥有所有文件能力。`TryOpen` 返回 `false`、解析异常、音频加载异常和用户取消都必须释放尚未转交的资源。

### 5.3 文件对象选择

- 用户从 Avalonia StorageProvider 选择的文件，使用现有 `AvaloniaStorageProviderSimpleFile` 包装，并由上下文负责释放。
- 自动发现的 Desktop 本地文件，可以使用 `LocalSimpleFile`。
- 不要为同一个角色同时保留两个独立文件句柄；如果替换包装对象，必须明确旧对象的所有权。
- 如果使用 ACB，Desktop 必须保证 `AudioFile.LocalPath` 可用，并在存在外置 AWB 时将 `AudioAwbFile` 一并绑定；Browser 不应暴露这一入口。

`ProjectDirectory` 不是 FastOpen 的必需字段。只有在实现了明确的目录能力取得、所有权和恢复语义后，才应把目录树放入上下文；不能为了满足普通项目快照而虚构一个工程目录。

## 6. UI 入口恢复

### 6.1 Desktop 主菜单和快捷键

FastOpen 命令定义、处理器和菜单定义放入 Desktop 项目，例如：

```text
src/OngekiFumenEditor.Avalonia.Desktop/
  Modules/FumenVisualEditor/FastOpen/
    DesktopFastOpenService.cs
    FastOpenFumenCommandDefinition.cs
    FastOpenFumenCommandHandler.cs
    MenuDefinitions.cs
```

Desktop 注册：

- `ICommandHandler`；
- `CommandDefinitionBase`；
- `MenuItemDefinition`；
- `Ctrl+F` 快捷键。

菜单项仍挂在 `Gekimini.Avalonia.Modules.MainMenu.MenuDefinitions.FileNewOpenMenuGroup`，排序位置沿用 WPF 的 FastOpen 位置。

Core 的 `Commands/OgkrImpl/MenuDefinitions.cs` 不再注册 FastOpen 菜单，也不保留指向 Desktop 命令类型的引用。

### 6.2 平台 Splash View/ViewModel

Core 提供公共基类和公共视图片段，平台提供最终窗口：

```text
Core SplashScreenViewModelBase
  + Core SplashScreenCommonView.axaml
            |
            +-- DesktopSplashScreenViewModel
            |     +-- DesktopSplashScreenView.axaml
            |     +-- FastOpen card
            |
            +-- BrowserSplashScreenViewModel
                  +-- BrowserSplashScreenView.axaml
                  +-- Browser-specific cards
```

平台组合根显式绑定 `ISplashScreenWindow`，不要依赖多个 `[RegisterSingleton<ISplashScreenWindow>]` 的注册顺序：

```csharp
services.AddSingleton<DesktopSplashScreenViewModel>();
services.AddSingleton<ISplashScreenWindow>(sp =>
    sp.GetRequiredService<DesktopSplashScreenViewModel>());
```

Browser 使用自己的 ViewModel 替换类型。`OngekiFumenEditorApp` 和 `ShowSplashScreenCommandHandler` 继续只依赖 `ISplashScreenWindow`，不需要知道具体平台类型。

公共视图应拆成可复用的 `UserControl` 或模板，平台 View 只负责组合公共区域和差异化区域，避免 Desktop/Browser 复制整套最近文件和语言布局。

### 6.3 错误和取消

服务至少区分以下结果：

| 情况 | 行为 |
| --- | --- |
| 用户取消谱面选择 | 静默返回，不创建文档 |
| 用户取消音频选择 | 静默返回，不创建文档 |
| 扩展名不支持 | 显示 FastOpen 错误 |
| 谱面解析失败 | 记录异常并显示本地化错误 |
| 音频加载或 AWB 校验失败 | 记录异常并显示本地化错误 |
| 编辑器拒绝上下文 | 释放上下文并显示 FastOpen 错误 |

不能在失败路径留下空编辑器文档，也不能让谱面文件或音频文件的句柄泄漏。

## 7. 首次保存和最近记录边界

### 7.1 第一期不实现 FastOpen 转正式工程

当前 `FumenVisualEditorViewModel.Save()` 在 `ProjectFile == null` 时直接失败，`SaveAs()` 仍未实现。因此第一期只恢复“选择并打开”，不应把 FastOpen 命令伪装成完整可保存工程。

编辑器仍可以继续编辑内存中的谱面，但用户触发完整保存时必须得到明确的“暂不支持/后续实现”结果，不能静默清除 `IsDirty`。

### 7.2 后续正式保存设计

后续实现首次保存时应遵守已有 D8-D11 决策：

1. 仅在用户触发的保存或关闭保存流程中选择 `.nyagekiProj` 目标。
2. 选择取消时保持 `ProjectFile == null`、FastOpen 显示状态和脏状态不变。
3. 选择复制时，只复制运行时明确绑定的谱面、主音频和必要外置 AWB；不扫描并复制 `Music.xml`、封面或其他邻接文件。
4. 选择保留原位置时，允许 Desktop 多根上下文，但必须有明确的恢复方案和写入能力验证。
5. 所有文件复制、工程序列化和绑定切换成功后，才把新工程文件交给当前编辑器。
6. 失败时保持原 FastOpen 文件绑定，清理本次创建的临时或半成品文件。

### 7.3 最近记录

第一期不应直接把 FastOpen 写入现有普通项目最近记录：

- 当前 `EditorFileAccessContextSnapshot` 要求工程目录、谱面和音频书签；
- FastOpen 允许 `ProjectFile == null`，且谱面和音频可能来自不同目录；
- 用空工程文件或伪目录填充快照会产生无法恢复的记录。

等首次保存和 FastOpen 专用恢复配方明确后，再决定是否加入最近记录。无法生成完整恢复数据时，应跳过记录写入，而不是写入无效记录。

## 8. 实施步骤

### 阶段 1：抽取 Core 边界

1. 将 Core `SplashScreenViewModel` 重构为 `SplashScreenViewModelBase`，保留公共状态和命令。
2. 将 Core Splash AXAML 拆出公共视图片段，不在 Core 中保留平台专用卡片。
3. 从 Core 删除 `DocumentOpenHelper`、FastOpen command handler、FastOpen 菜单定义和 Desktop 路径发现逻辑。
4. 从 Core 删除 `DefaultArgProcessManager` 对本地路径文档打开的实现；启动参数处理器改由 Desktop 注册。
5. 保留 Core 的通用 `EditorContext`/`EditorFileAccessContext`、解析器和编辑器 Provider 接口。

### 阶段 2：实现 Desktop FastOpen

1. 新增 `DesktopFastOpenService`。
2. 接入当前 `TopLevel.StorageProvider` 文件选择。
3. 实现 `Music.xml`、文件名 ID、`musicsource` 和 `musicNNNN.*` 发现规则。
4. 实现手动音频选择、音频时长计算和 ACB/AWB 校验。
5. 按所有权规则构造 `EditorFileAccessContext` 与 `EditorContext`。
6. 调用 `IFumenVisualEditorProvider.TryOpen(editor, context)`，成功后打开 Shell 文档。

### 阶段 3：恢复 Desktop UI

1. 在 Desktop 注册 FastOpen command handler 和 command definition。
2. 在 Desktop 注册 File 菜单项。
3. 恢复 `Ctrl+F` 快捷键。
4. 新增 `DesktopSplashScreenViewModel` 和 `DesktopSplashScreenView.axaml`，接入公共 Splash 视图。
5. 在 Desktop 组合根显式注册 `ISplashScreenWindow` 到 Desktop Splash 子类。
6. Browser 新增自己的 Splash View/ViewModel，并注册 Browser 专用启动按钮。

### 阶段 4：统一启动参数路径

1. 在 Desktop 注册启动参数处理器，对单个 `.ogkr`/`.nyageki` 调用 Desktop 文档打开服务。
2. `.nyagekiProj` 等项目文件路径也由 Desktop 文档打开服务分发，不回到 Core helper。
3. 启动参数打开成功后再决定是否显示 Splash，避免 Splash 覆盖正在加载的文档。

### 阶段 5：后续保存闭环

1. 实现 `ProjectFile == null` 时的用户保存目标选择。
2. 实现复制/保留布局选择和原子绑定切换。
3. 实现 FastOpen 转正式工程后的最近记录恢复数据。
4. 增加关闭脏文档时的保存、取消和失败语义测试。

## 9. 测试与验收标准

### 9.1 Resolver 单元测试

- `Music.xml` 的有效 ID 能找到正确 `musicNNNN.*`；
- XML 缺失或 ID 非法时能从文件名回退；
- ID 格式化覆盖 `1`、`999`、`1000` 等边界；
- `package` 目录内递归发现只在限定根范围内执行；
- 多种大小写扩展名均能匹配；
- 不支持的音频扩展名被拒绝；
- 找不到音频时返回“需要手动选择”，不抛出误导性文件不存在异常。

### 9.2 上下文和所有权测试

- 手动选择音频后取消，谱面文件和音频文件均释放；
- 谱面解析失败时释放所有已取得能力；
- 音频加载失败时释放所有已取得能力；
- `TryOpen` 返回 `false` 时上下文释放；
- `TryOpen` 成功后上下文由编辑器接管，服务不再重复释放；
- FastOpen 上下文的 `ProjectFile` 为空，`FumenFile` 和 `AudioFile` 非空；
- ACB 外置 AWB 缺失或不可读时不会创建半初始化文档。

### 9.3 宿主与 UI 测试

- Desktop DI 能解析 `DesktopFastOpenService`、`DesktopSplashScreenViewModel`、command handler 和 command definition；
- Browser DI 能解析 `BrowserSplashScreenViewModel` 和 Browser 专用启动按钮；
- Desktop 的 `IEditorProvider` 与 `IFumenVisualEditorProvider` 仍解析到同一 Provider 实例；
- Desktop 主菜单包含 FastOpen，快捷键为 `Ctrl+F`；
- Browser 不注册 FastOpen 菜单，Browser Splash 不显示 FastOpen；
- Desktop Splash 与 Desktop 菜单入口调用同一个 Desktop FastOpen 服务；
- 用户取消任一文件选择不会显示错误对话框，也不会打开空文档。

### 9.4 构建验收

- Core 构建不引用 Desktop 程序集；
- Core 只包含 Splash 基类和公共视图片段，不包含 Desktop FastOpen 实现；
- Desktop 构建通过并包含 FastOpen 注册；
- Browser 构建通过且不包含 FastOpen UI/命令注册；
- 不再依赖 `ENABLE_CROSS_PLATFORM_FAST_OPEN` 才能编译 Desktop FastOpen；
- 启动参数打开 `.ogkr` 与 UI FastOpen 使用同一套 Desktop 发现和上下文构造逻辑。

## 10. 相关文件

当前实现和设计依据：

- [`src/OngekiFumenEditor.Avalonia/Utils/DocumentOpenHelper.cs`](../src/OngekiFumenEditor.Avalonia/Utils/DocumentOpenHelper.cs)
- [`src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/Models/EditorContext.cs`](../src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/Models/EditorContext.cs)
- [`src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/Models/EditorFileAccessContext.cs`](../src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/Models/EditorFileAccessContext.cs)
- [`src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/FumenVisualEditorProvider.cs`](../src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/FumenVisualEditorProvider.cs)
- [`src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/FumenVisualEditorProvider.ProjectIO.cs`](../src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/FumenVisualEditorProvider.ProjectIO.cs)
- [`src/OngekiFumenEditor.Avalonia/Utils/FileDialogHelper.cs`](../src/OngekiFumenEditor.Avalonia/Utils/FileDialogHelper.cs)
- [`src/OngekiFumenEditor.Avalonia.Desktop/Modules/FumenVisualEditor/DefaultDesktopFumenVisualEditorProvider.cs`](../src/OngekiFumenEditor.Avalonia.Desktop/Modules/FumenVisualEditor/DefaultDesktopFumenVisualEditorProvider.cs)
- [`docs/editor_file_access_context_refactory.md`](editor_file_access_context_refactory.md)
- [`docs/editor-file-access-context-refactory-implementation-audit-2026-08-17.md`](editor-file-access-context-refactory-implementation-audit-2026-08-17.md)
- [`docs/wpf-avalonia-full-migration-audit-2026-08-07.html`](wpf-avalonia-full-migration-audit-2026-08-07.html)
