# OngekiFumenEditor.Avalonia 磁盘 I/O 审计

> 检查日期：2026-08-04
> 检查对象：当前工作树中的 `src/OngekiFumenEditor.Avalonia` Core 项目，以及跨平台临时文件夹服务的 Desktop/Browser 后端
> 检查方式：静态调用扫描与调用链核对，未进行 ETW、Process Monitor 或运行时插桩
> 结论状态：已完成静态审计

## 1. 范围与口径

本次检查主体覆盖 `src/OngekiFumenEditor.Avalonia`，即核心 UI、编辑器、解析器及公共服务。为记录 Core 临时存储调用的实际落点，另对 Desktop 和 Browser 的 `ITemporaryFolderProvider` 后端作定向核对；这不扩展到两个平台项目的其他 I/O。

以下内容明确排除：

- `src/OngekiFumenEditor.Avalonia.Desktop` 中除临时文件夹后端之外的代码；
- `src/OngekiFumenEditor.Avalonia.Browser` 中除临时文件夹后端及其 OPFS JS 模块之外的代码；
- `src/OngekiFumenEditor.Avalonia.CommandLine`；
- Core 项目内除临时文件夹公共契约之外的 `src/OngekiFumenEditor.Avalonia/Platforms` 平台适配代码；
- `tests` 和 `Dependencies` 内部实现。

Core 通过 `ISettingManager`、`INAudioFileReaderFactory` 等接口发起的平台相关操作只记录调用边界，不沿调用链进入 Desktop 或 Browser，也不把宿主实现计入 Core 磁盘 I/O。

本报告采用以下口径：

- `File.*`、`Directory.*`、`FileStream`、`FileInfo.Open` 等计为直接文件系统 I/O；
- Core 中 NAudio、Skia 等接收文件路径并可能在库内部打开文件的调用计为间接 I/O；平台项目中的具体实现不展开；
- `File.Exists`、`Directory.Exists`、目录枚举和独占打开探测计为文件系统元数据 I/O；
- Avalonia 文件选择器只负责授权或选择文件，不等同于读取、写入文件内容，因此单独列出；
- `Path.*` 只处理路径字符串，不计为磁盘 I/O；
- `MemoryStream` 及仅针对调用方传入 `Stream` 的解析和序列化不单独计为磁盘 I/O；
- Avalonia `AssetLoader`、程序集嵌入资源属于应用资源访问，与任意本地文件访问分开记录；
- MSBuild、编译器和发布流程读取或复制资源属于构建期 I/O，不计入运行时统计。

## 2. 扫描摘要

| 范围 | 显式 `File.*` / `Directory.*` 调用行 | 涉及源文件 |
| --- | ---: | ---: |
| Core 源码（排除平台目录并剔除字符串误报） | 63 | 25 |
| 其中参与当前 Core 编译的源码 | 61 | 24 |

源码口径包含已被项目文件排除编译的旧 OpenGL 字体实现，其中有 2 行、1 个文件；当前编译口径已扣除这部分。两种数字均不包含 `FileInfo.Open`、原生 Win32 API、NAudio、Skia 等间接 I/O，因此应视为显式调用下限，而不是完整的运行时系统调用次数。部分代码当前没有调用者，详见第 7 节。

## 3. I/O 意图分类

### 3.1 分类规则

| 类别 | 主意图 | 判定规则 |
| --- | --- | --- |
| A. 临时文件与缓存 | 暂存、缓存、恢复或中间产物 | 文件可由程序重新生成，或者只服务于一次事务、一次转换、恢复及性能缓存；通常位于 `%TEMP%` 或目标文件旁的临时路径 |
| B. 用户数据 | 用户拥有、选择、编辑或导出的内容 | 包括工程、谱面、音频、图片、SVG 以及用户指定的输入输出；自动保存最终覆盖的工程和谱面也属于此类 |
| C. 程序资源 | 应用运行所需的只读资源 | 包括 `Resources` 美术资源、默认音效、Avalonia 资源、语言文件及 Core 构建输入 |
| D. 其他或特殊 | 程序内部状态、诊断、平台边界与构建行为 | 包括快捷键、日志、崩溃转储、外部进程、文件选择器、构建期 I/O 及当前未启用的特殊实现 |

分类以“读写产物的主意图”为准，而不是只看物理路径。例如 Core 日志虽然位于 `%TEMP%`，主意图仍是诊断，所以归入 D。

一个完整工作流可以跨多个类别。此时按阶段拆分，例如工程保存先创建 A 类临时文件，再覆盖 B 类用户工程。以下清单覆盖本报告确认的全部 I/O 意图。

### 3.2 A 类：临时文件、缓存与恢复数据

| 编号 | I/O 意图 | 具体操作与位置 | 生命周期结论 |
| --- | --- | --- | --- |
| A1 | 跨平台临时存储 | Core 通过 [`ITemporaryFolderProvider`](../src/OngekiFumenEditor.Avalonia/Platforms/Services/FileSystem/Providers/ITemporaryFolderProvider.cs) 分配安全的相对路径句柄；Desktop 根为 `%TEMP%/NagekiFumenEditorTempFolder`，Browser 根为当前 origin 的 OPFS `temp` | 内容跨启动保留，只由显式 `ClearAsync` / 删除 API 清理 |
| A2 | 工程、谱面保存暂存 | Desktop 将工程和谱面事务式写入临时句柄，再复制覆盖 B 类目标；Browser 救援写入可直接使用流式句柄；见 [`EditorProjectDataUtils.cs`](../src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/Base/EditorProjectDataUtils.cs) | 复制完成后保留中间文件，除非显式清理 |
| A3 | 谱面转换原子暂存 | 在目标文件同目录写入 `.<name>.<guid>.tmp`，成功后移动为 B 类输出，失败时删除；见 [`DefaultFumenConvertService.cs`](../src/OngekiFumenEditor.Avalonia/Modules/FumenConverter/Kernel/DefaultFumenConvertService.cs) | 有完整提交和清理逻辑 |
| A4 | WAV 调整原子暂存 | 在目标 WAV 同目录创建随机 `.tmp`，强制刷新后移动为 B 类输出，失败时删除；见 [`DefaultWavAudioOffsetService.cs`](../src/OngekiFumenEditor.Avalonia/Kernel/Audio/DefaultCommonImpl/Wave/DefaultWavAudioOffsetService.cs) | 有完整提交和失败清理逻辑 |
| A5 | 网络图片缓存 | 将 HTTP 图片写入临时根下的 `images/*.img.cache`，后续优先读取；见 [`ImageLoader.cs`](../src/OngekiFumenEditor.Avalonia/Utils/ImageLoader.cs) | 无过期或容量限制；显式清理临时根时删除 |
| A6 | ACB 播放解码缓存 | Desktop 将 B 类 `.acb` / `.awb` 解码为临时根下的 `decodeAcbFiles/*.wav`；见 [`AcbConverter.cs`](../src/OngekiFumenEditor.Avalonia/Kernel/Audio/AcbConverter.cs) | 成功缓存长期复用，失败输出会删除；Browser 因没有本地路径而明确失败 |
| A7 | 崩溃时工程救援副本 | 在临时根的 `Rescue/` 下通过句柄保存当前工程和谱面副本；见 [`FumenRescue.cs`](../src/OngekiFumenEditor.Avalonia/Utils/DeadHandler/FumenRescue.cs) | 当前没有外部调用者；Desktop 与 OPFS 均设计为保留供人工恢复 |

### 3.3 B 类：用户数据

| 编号 | I/O 意图 | 具体操作 | 主要位置 |
| --- | --- | --- | --- |
| B1 | 用户工程和谱面加载 | 读取工程文件、工程引用的 `.ogkr` 及其他受支持谱面格式 | [`EditorProjectDataUtils.cs`](../src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/Base/EditorProjectDataUtils.cs)、[`EditorProjectFileManager.cs`](../src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/Kernel/EditorProjectFile/EditorProjectFileManager.cs)、[`IFumenParserManager.cs`](../src/OngekiFumenEditor.Avalonia/Parser/IFumenParserManager.cs) |
| B2 | 用户工程和谱面保存 | 手动保存最终覆盖用户指定工程和谱面；`FileHelper` 对目标执行占用探测 | [`EditorProjectDataUtils.cs`](../src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/Base/EditorProjectDataUtils.cs)、[`FileHelper.cs`](../src/OngekiFumenEditor.Avalonia/Utils/FileHelper.cs) |
| B3 | 自动保存用户文档 | 定时保存最终覆盖当前工程及其谱面；中间暂存属于 A2，但最终产物仍是 B 类 | [`DefaultEditorDocumentManager.cs`](../src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/Kernel/DefaultImpl/DefaultEditorDocumentManager.cs) |
| B4 | 打开谱面并发现关联数据 | 读取 `.ogkr` / `.nyageki`、同目录 `Music.xml`，递归查找 `musicsourceXXXX` 和 `musicXXXX.*` | [`DocumentOpenHelper.cs`](../src/OngekiFumenEditor.Avalonia/Utils/DocumentOpenHelper.cs) |
| B5 | 谱面格式转换 | 读取用户输入谱面并写入用户指定输出；提交前暂存属于 A3 | [`DefaultFumenConvertService.cs`](../src/OngekiFumenEditor.Avalonia/Modules/FumenConverter/Kernel/DefaultFumenConvertService.cs) |
| B6 | 用户 WAV 调整 | 读取输入 WAV，并写入用户指定的新 WAV；提交前暂存属于 A4 | [`DefaultWavAudioOffsetService.cs`](../src/OngekiFumenEditor.Avalonia/Kernel/Audio/DefaultCommonImpl/Wave/DefaultWavAudioOffsetService.cs) |
| B7 | 用户音乐和音效素材读取 | Core 把用户工程引用或手动选择的音频路径交给 `INAudioFileReaderFactory`；具体平台解码和文件实现不在范围内 | [`NAudioManager.cs`](../src/OngekiFumenEditor.Avalonia/Kernel/Audio/NAudioImpl/NAudioManager.cs)、[`DefaultMusicPlayer.cs`](../src/OngekiFumenEditor.Avalonia/Kernel/Audio/NAudioImpl/Music/DefaultMusicPlayer.cs) |
| B8 | 用户 ACB/AWB 读取 | 读取用户选择或工程发现的 `.acb` 和外部 `.awb`；解码缓存属于 A6 | [`AcbConverter.cs`](../src/OngekiFumenEditor.Avalonia/Kernel/Audio/AcbConverter.cs) |
| B9 | 用户图片和外部 SVG 读取 | 读取本地图片路径及 SVG prefab 文件；HTTP 图片下载后的缓存属于 A5 | [`ImageLoader.cs`](../src/OngekiFumenEditor.Avalonia/Utils/ImageLoader.cs)、[`SvgImageFilePrefab.cs`](../src/OngekiFumenEditor.Avalonia/Base/EditorObjects/Svg/SvgImageFilePrefab.cs) |
| B10 | SVG 导出 | 将谱面预览写入用户指定 SVG 路径 | [`DefaultPreviewSvgGenerator.cs`](../src/OngekiFumenEditor.Avalonia/Modules/PreviewSvgGenerator/Kernel/DefaultPreviewSvgGenerator.cs) |
| B11 | 用户文件选择和校验 | 文件选择器获取工程、谱面、音频、SVG 和导出路径；启动参数、快速打开和对话框执行存在性检查或验证性读取 | [`FileDialogHelper.cs`](../src/OngekiFumenEditor.Avalonia/Utils/FileDialogHelper.cs)、[`DefaultArgProcessManager.cs`](../src/OngekiFumenEditor.Avalonia/Kernel/ArgProcesser/DefaultArgProcessManager.cs)、[`EditorProjectSetupDialogViewModel.cs`](../src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/ViewModels/Dialogs/EditorProjectSetupDialogViewModel.cs) |

### 3.4 C 类：程序资源

| 编号 | I/O 意图 | 具体操作与来源 | 主要位置 |
| --- | --- | --- | --- |
| C1 | Avalonia 打包资源 | 使用 `AssetLoader.Open` 读取 `avares://` 图片等应用资源 | [`ResourceUtils.cs`](../src/OngekiFumenEditor.Avalonia/Utils/ResourceUtils.cs) |
| C2 | 编辑器美术纹理 | 从当前工作目录 `./Resources/editor/*.png` 读取 Tap、Bell、Flick、Beam、Lane 等渲染纹理 | [`ResourceUtils.cs`](../src/OngekiFumenEditor.Avalonia/Utils/ResourceUtils.cs) 及各 DrawingTarget |
| C3 | 纹理尺寸与锚点配置 | 在 `ResourceUtils` 静态初始化时读取 `./Resources/editor/textureSizeAnchor.ini` | [`ResourceUtils.cs`](../src/OngekiFumenEditor.Avalonia/Utils/ResourceUtils.cs) |
| C4 | 默认音效资源 | 从 `AppContext.BaseDirectory/Sound/*.wav` 读取 17 个编辑器播放音效 | [`DefaultFumenSoundPlayer.cs`](../src/OngekiFumenEditor.Avalonia/Kernel/Audio/DefaultCommonImpl/Sound/DefaultFumenSoundPlayer.cs) |
| C5 | Core 编译和打包输入资源 | 构建时读取语言 JSON、AXAML、PNG、ICO、外部项目引用和 `HintPath` DLL | Core [项目文件](../src/OngekiFumenEditor.Avalonia/OngekiFumenEditor.Avalonia.csproj) |

### 3.5 D 类：其他或特殊 I/O

| 编号 | I/O 意图 | 具体操作与位置 | 说明 |
| --- | --- | --- | --- |
| D1 | 快捷键配置 | 读取和同步覆盖当前工作目录 `keybind.json`；见 [`DefaultKeyBindingManager.cs`](../src/OngekiFumenEditor.Avalonia/Kernel/KeyBinding/DefaultKeyBindingManager.cs) | 程序内部用户偏好 |
| D2 | Core 诊断日志 | 通过平台日志存储在 `logs/*.log` 创建单会话文件，写入 WPF 兼容起始标记并顺序追加 UTF-8 日志；见 [`FileLogOutput.cs`](../src/OngekiFumenEditor.Avalonia/Utils/Logs/DefaultImpls/FileLogOutput.cs) 和 [`ILogFileStorage.cs`](../src/OngekiFumenEditor.Avalonia/Platforms/Services/Logging/ILogFileStorage.cs) | Desktop 写入 exe 所在目录的 `logs`，Browser 写入 OPFS 根的 `logs`；主意图仍是诊断 |
| D3 | 崩溃转储 | 创建 `ProgramSetting.DumpFileDirPath/*.dmp` 并调用原生 `MiniDumpWriteDump`；见 [`DumpFileHelper.cs`](../src/OngekiFumenEditor.Avalonia/Utils/DeadHandler/DumpFileHelper.cs) | 当前未初始化、无活动调用者；工程救援副本另归 A7 |
| D4 | 外部打开与资源管理器 | 检查文件或目录后启动 Explorer、默认程序或 URL；见 [`ProcessUtils.cs`](../src/OngekiFumenEditor.Avalonia/Utils/ProcessUtils.cs) | 本进程只做元数据检查和进程启动 |
| D5 | 文件选择平台机制 | Core 中的 Avalonia StorageProvider 调用打开文件、保存文件和目录选择器 | 只记录 Core 调用；具体平台 Provider 不在范围内，选择目标按用途归入 B、C 或 D |
| D6 | Core 构建过程 | MSBuild 读取 Core 项目引用、资源和 DLL | 资源本体属于 C，构建动作属于特殊工具链 I/O |
| D7 | 未启用的 INI 持久化 | `IniFile` 可通过 Win32 API 读写 INI | 已编译但没有实例化调用 |
| D8 | 未调用的图片比较读取 | `SKPixelComparer` 路径重载可通过 Skia 读取图片 | 已编译但没有外部调用者 |
| D9 | 已排除的命名共享内存 | `IPCHelper` 使用命名 `MemoryMappedFile` | 已被 `Compile Remove`，且不等同于普通用户磁盘文件 |
| D10 | 已排除的系统字体读取 | 旧 OpenGL 实现枚举系统字体目录并读取字体字节 | 整个旧 OpenGL 目录已排除编译 |
| D11 | 控制台句柄流 | `ConsoleWindowHelper` 用 `FileStream` 包装 stdin/stdout/stderr | 属于控制台 I/O，不是磁盘文件读写 |

### 3.6 跨分类工作流

| 工作流 | 分类组合 | 分类依据 |
| --- | --- | --- |
| 手动保存工程或谱面 | A2 -> B2 | 临时序列化和中间复制属于 A，最终用户文档属于 B |
| 自动保存 | A2 -> B3 | 当前实现不是把自动保存永久放在临时目录，而是经过临时文件后覆盖当前用户工程和谱面 |
| 谱面转换 | B5 -> A3 -> B5 | 读取用户输入，写临时文件，原子提交为用户输出 |
| WAV 偏移 | B6 -> A4 -> B6 | 读取用户 WAV，写同目录临时 WAV，再提交为用户输出 |
| ACB 播放 | B8 -> A6 | 读取用户或游戏 ACB/AWB，生成可复用的临时 WAV 缓存 |
| 网络图片加载 | 网络 -> A5 | 网络响应不是磁盘 I/O；落地缓存属于 A |
| 崩溃处理 | D3 + A7 | `.dmp` 是诊断数据，工程救援副本是自动恢复数据 |
| 文件选择器 | D5 -> B/C/D | Core 选择器调用不读写内容；被选择对象的实际用途决定最终类别 |

## 4. Core 运行时 I/O

### 4.1 工程与谱面文件

| 场景 | 读取 | 写入、移动与删除 | 主要位置 |
| --- | --- | --- | --- |
| 工程加载 | `ReadAllBytesAsync` 读取工程文件；`OpenRead` 读取工程引用的谱面 | 无 | [`EditorProjectDataUtils.cs`](../src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/Base/EditorProjectDataUtils.cs)、[`EditorProjectFileManager.cs`](../src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/Kernel/EditorProjectFile/EditorProjectFileManager.cs) |
| 工程保存 | 通过独占打开探测目标是否可写 | 将工程序列化到临时文件，再用 `File.Copy(..., overwrite: true)` 覆盖目标 | [`EditorProjectDataUtils.cs`](../src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/Base/EditorProjectDataUtils.cs)、[`FileHelper.cs`](../src/OngekiFumenEditor.Avalonia/Utils/FileHelper.cs) |
| 谱面保存 | 无 | 将序列化结果写入临时文件，再复制覆盖目标 `.ogkr` 或其他受支持格式 | [`EditorProjectDataUtils.cs`](../src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/Base/EditorProjectDataUtils.cs) |
| 通用谱面 API | `OpenRead` 后交给对应反序列化器 | `WriteAllBytesAsync` 写入对应序列化结果 | [`IFumenParserManager.cs`](../src/OngekiFumenEditor.Avalonia/Parser/IFumenParserManager.cs) |
| 自动保存 | 读取当前工程状态 | 定时调用完整工程及谱面覆盖流程 | [`DefaultEditorDocumentManager.cs`](../src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/Kernel/DefaultImpl/DefaultEditorDocumentManager.cs) |

工程保存所使用的临时文件由 `ITemporaryFolderProvider` 分配。当前基于本地 `System.IO` 路径的原子保存只在 `LocalPath` 可用的 Desktop 后端执行；缺少本地路径时会明确失败，不会把 OPFS 相对路径伪装为系统路径。保存流程不会自动删除中间文件。

### 4.2 打开谱面与关联音频发现

[`DocumentOpenHelper.cs`](../src/OngekiFumenEditor.Avalonia/Utils/DocumentOpenHelper.cs) 涉及以下文件系统操作：

- 检查并读取用户指定的 `.ogkr` 或 `.nyageki`；
- 检查同目录的 `Music.xml`，通过 `XDocument.LoadAsync` 读取歌曲名称和 MusicSource ID；
- 根据歌曲 ID 推导 `musicsourceXXXX` 目录；
- 必要时使用 `Directory.GetDirectories(..., SearchOption.AllDirectories)` 递归查找目录；
- 使用 `Directory.GetFiles` 查找 `musicXXXX.*` 音频；
- 检查找到的音频是否存在，并通过音频管理器读取音频时长；
- 自动发现失败时调用文件选择器，让用户手动选择音频。

启动参数、快速打开命令、编辑器加载和工程设置对话框还会分别执行文件存在性检查或打开谱面验证：

- [`DefaultArgProcessManager.cs`](../src/OngekiFumenEditor.Avalonia/Kernel/ArgProcesser/DefaultArgProcessManager.cs)
- [`FastOpenFumenCommandHandler.cs`](../src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/Commands/OgkrImpl/FastOpenFumen/FastOpenFumenCommandHandler.cs)
- [`FumenVisualEditorViewModel.cs`](../src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.cs)
- [`EditorProjectSetupDialogViewModel.cs`](../src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/ViewModels/Dialogs/EditorProjectSetupDialogViewModel.cs)

### 4.3 谱面转换与预览导出

[`DefaultFumenConvertService.cs`](../src/OngekiFumenEditor.Avalonia/Modules/FumenConverter/Kernel/DefaultFumenConvertService.cs) 的转换流程为：

1. 使用 `File.OpenRead` 读取输入谱面；
2. 在目标文件同目录创建隐藏 `.tmp` 文件；
3. 使用 `File.WriteAllBytesAsync` 写入转换结果；
4. 使用 `File.Move(..., overwrite: true)` 原子替换目标；
5. 无论成功与否，检查并删除残留临时文件。

[`DefaultPreviewSvgGenerator.cs`](../src/OngekiFumenEditor.Avalonia/Modules/PreviewSvgGenerator/Kernel/DefaultPreviewSvgGenerator.cs) 在指定 `OutputFilePath` 时使用 `File.WriteAllBytesAsync` 写出 SVG。

### 4.4 WAV 偏移处理

[`DefaultWavAudioOffsetService.cs`](../src/OngekiFumenEditor.Avalonia/Kernel/Audio/DefaultCommonImpl/Wave/DefaultWavAudioOffsetService.cs) 执行以下操作：

- 以异步、顺序扫描模式打开输入 WAV；
- 读取 RIFF/WAVE 头、格式块、数据块及音频数据；
- 创建目标目录；
- 在目标文件同目录创建随机 `.tmp` 文件；
- 写入调整后的 WAV，并调用 `Flush(flushToDisk: true)` 强制刷新；
- 使用同卷 `File.Move` 原子替换目标文件；
- 失败时尝试删除临时文件。

[`AudioAdjustWindowViewModel.cs`](../src/OngekiFumenEditor.Avalonia/Modules/AudioAdjustWindow/ViewModels/AudioAdjustWindowViewModel.cs) 在调用该服务前检查输入音频和用户选择文件是否存在。

### 4.5 音频读取与 ACB 解码

[`NAudioManager.cs`](../src/OngekiFumenEditor.Avalonia/Kernel/Audio/NAudioImpl/NAudioManager.cs) 和 [`DefaultMusicPlayer.cs`](../src/OngekiFumenEditor.Avalonia/Kernel/Audio/NAudioImpl/Music/DefaultMusicPlayer.cs) 将音频文件路径交给 `INAudioFileReaderFactory`。Core 表达了读取用户音频的意图，但具体 Reader 和平台文件实现位于范围外，本报告不继续展开。

[`AcbConverter.cs`](../src/OngekiFumenEditor.Avalonia/Kernel/Audio/AcbConverter.cs) 还会：

- 通过 `AcbFile.FromFile` 间接读取 `.acb`；
- 必要时使用 `File.OpenRead` 读取外部 `.awb`；
- 在 Desktop 临时根的 `decodeAcbFiles/` 中缓存解码后的 WAV；
- 写入 HCA 解码结果；
- 解码失败时删除不完整输出；
- 后续加载优先复用已经存在的缓存 WAV；
- Browser 后端没有 `LocalPath`，因此 ACB 本地路径解码会返回明确失败，不会构造虚假的 OPFS 路径。

[`DefaultFumenSoundPlayer.cs`](../src/OngekiFumenEditor.Avalonia/Kernel/Audio/DefaultCommonImpl/Sound/DefaultFumenSoundPlayer.cs) 检查 `AppContext.BaseDirectory/Sound`，随后通过音频工厂读取 17 个固定名称的 WAV 音效。

### 4.6 快捷键和设置边界

[`DefaultKeyBindingManager.cs`](../src/OngekiFumenEditor.Avalonia/Kernel/KeyBinding/DefaultKeyBindingManager.cs) 在构造时读取当前工作目录的 `keybind.json`，保存时同步覆盖该文件。

Core 设置模型通过 [`SettingModelBase.cs`](../src/OngekiFumenEditor.Avalonia/Models/Settings/SettingModelBase.cs) 调用 `ISettingManager`，但接口没有指定文件、目录或其他持久化介质。由于具体实现属于平台项目，本报告不把应用设置计为 Core 磁盘 I/O。

### 4.7 日志

Core [`FileLogOutput.cs`](../src/OngekiFumenEditor.Avalonia/Utils/Logs/DefaultImpls/FileLogOutput.cs) 通过 [`ILogFileStorage`](../src/OngekiFumenEditor.Avalonia/Platforms/Services/Logging/ILogFileStorage.cs) 创建每次启动一份时间戳 `.log` 文件，并在文件开头写入原 WPF 的 `----------BEGIN FILE LOG OUTPUT----------` 标记，再按调用顺序串行追加 UTF-8 日志。文件创建失败只记录诊断信息，不让日志故障反向终止应用。

Desktop 的 [`DesktopLogFileStorage`](../src/OngekiFumenEditor.Avalonia.Desktop/Platforms/Services/Logging/DesktopLogFileStorage.cs) 将目录固定为 `AppContext.BaseDirectory/logs`（即 exe 所在目录的 `logs` 子目录），并使用不覆盖已有文件的时间戳名称。原 Desktop `ILogger` provider 现在转发到同一 `FileLogOutputWrapper`，不再额外创建工作目录下的 `Logs/current.log`。

Browser 的 [`BrowserLogFileStorage`](../src/OngekiFumenEditor.Avalonia.Browser/Platforms/Services/Logging/BrowserLogFileStorage.cs) 通过 `LogFileSystemInterop` 使用 OPFS 根目录的 `logs` 子目录；临时文件仍位于 OPFS `temp`，`ClearAsync` 不会清理日志。`BrowserFileLoggerProvider` 将 `Microsoft.Extensions.Logging` 记录转发到同一文件。OPFS 不可用时，文件 sink 明确报告不可用且不返回伪造路径，控制台日志仍保留。

旧 `LogSetting.LogFileDirPath` 属性仅为读取旧设置文件而保留；日志设置页改为展示平台实际目录并设为只读，不再提供不会生效的目录选择命令。

### 4.8 图片、纹理和 SVG 文件

[`ImageLoader.cs`](../src/OngekiFumenEditor.Avalonia/Utils/ImageLoader.cs) 的行为如下：

- 非 HTTP 路径通过 `File.ReadAllBytesAsync` 读取本地图片；
- HTTP 图片先查询磁盘缓存，未命中后从网络下载；
- 下载结果写入临时根的 `images/*.img.cache`；
- 缓存没有过期或容量限制，只在显式删除文件、目录或调用 `ClearAsync` 时清理。

[`ResourceUtils.cs`](../src/OngekiFumenEditor.Avalonia/Utils/ResourceUtils.cs) 会：

- 使用 `AssetLoader.Open` 读取 `avares://` 应用资源；
- 对普通相对路径执行 `File.Exists` 和 `File.OpenRead`；
- 直接读取 `./Resources/editor/*.png` 纹理；
- 在静态构造阶段读取 `./Resources/editor/textureSizeAnchor.ini`。

[`SvgImageFilePrefab.cs`](../src/OngekiFumenEditor.Avalonia/Base/EditorObjects/Svg/SvgImageFilePrefab.cs) 使用 `FileInfo.Exists` 和 `FileInfo.Open` 读取用户指定的 SVG 文件。

### 4.9 临时目录和元数据探测

旧 `TempFileHelper` 已删除。公共句柄实现位于 [`Platforms/Services/FileSystem/Providers`](../src/OngekiFumenEditor.Avalonia/Platforms/Services/FileSystem/Providers/ITemporaryFolderProvider.cs)，统一执行单路径段校验、固定名称复用、唯一文件实际占位、递归删除及显式清理。写入回调只有成功结束才提交新内容；回调失败或取消时保留原内容，进入提交阶段后不再响应取消。

Desktop [`DesktopTemporaryFolderProvider`](../src/OngekiFumenEditor.Avalonia.Desktop/Platforms/Services/FileSystem/Providers/DesktopTemporaryFolderProvider.cs) 使用 `%TEMP%/NagekiFumenEditorTempFolder`，通过同目录事务文件和原子替换提交，并对所有 `LocalPath` 做根目录包含性校验。Browser [`BrowserTemporaryFolderProvider`](../src/OngekiFumenEditor.Avalonia.Browser/Platforms/Services/FileSystem/Providers/BrowserTemporaryFolderProvider.cs) 使用当前 origin 的 OPFS `temp` 目录，数据受 origin 隔离、浏览器配额和站点数据清理影响。

两端均不在启动或退出时自动清空，也不做过期和容量淘汰。只有调用文件/目录删除 API 或 `ITemporaryFolderProvider.ClearAsync` 才会删除内容。Browser 如果在启动时因缺少 OPFS、安全上下文或权限而无法初始化，会切换到 discard 后端：`IsAvailable=false`，写入回调仍执行但数据被丢弃，查找始终未命中，直接读取按文件不存在处理；初始化成功后的配额耗尽等运行错误仍向调用者传播。

[`FileHelper.cs`](../src/OngekiFumenEditor.Avalonia/Utils/FileHelper.cs) 通过 `FileShare.None` 独占打开文件来判断目标是否可写或被占用。

[`ProcessUtils.cs`](../src/OngekiFumenEditor.Avalonia/Utils/ProcessUtils.cs) 在调用资源管理器前检查目标是文件还是目录。实际打开文件、目录或 URL 由外部进程完成，不属于本进程的数据读写。

## 5. Avalonia 文件选择器

[`FileDialogHelper.cs`](../src/OngekiFumenEditor.Avalonia/Utils/FileDialogHelper.cs) 封装了三种平台服务：

- `StorageProvider.OpenFilePickerAsync`；
- `StorageProvider.SaveFilePickerAsync`；
- `StorageProvider.OpenFolderPickerAsync`。

当前共有 12 个调用入口，覆盖：

- 谱面转换输入和输出；
- WAV 偏移输入和输出；
- 新工程的谱面和音频选择；
- 快速打开谱面时的手动音频选择；
- SVG 外部文件选择；
- 音效目录、日志目录和崩溃转储目录设置。

文件选择器本身只取得 `IStorageFile` 或 `IStorageFolder`。当前封装立即调用 `TryGetLocalPath()` 并把结果传给 `System.IO`，没有通过 `IStorageFile.OpenReadAsync()` / `OpenWriteAsync()` 使用平台存储流。

具体 StorageProvider 如何取得文件不在本报告范围内；Core 取得结果后立即调用 `TryGetLocalPath()`，并把路径交给本报告所列的 `System.IO` 工作流。

## 6. 构建期 I/O

Core [项目文件](../src/OngekiFumenEditor.Avalonia/OngekiFumenEditor.Avalonia.csproj) 使 MSBuild、编译器和 Avalonia 工具链读取：

- `Assets/Languages/*.json`；
- 全部 `*.axaml`；
- `Resources/**/*.png` 和 `Resources/**/*.ico`；
- 外部项目引用和 `HintPath` DLL。

这些操作发生在构建或发布阶段，不是应用自身的运行时磁盘 I/O。

## 7. 当前未生效或未调用的 I/O 代码

以下代码存在磁盘或持久化访问能力，但当前不属于已确认的活动调用链：

- [`IniFile.cs`](../src/OngekiFumenEditor.Avalonia/Utils/IniFile.cs) 通过 `GetPrivateProfileString` / `WritePrivateProfileString` 读写 INI，但全仓库没有实例化调用；
- [`SKPixelComparer.cs`](../src/OngekiFumenEditor.Avalonia/Kernel/Graphics/Skia/Utils/SKPixelComparer.cs) 的路径重载通过 Skia 读取图片，但没有外部调用者；
- [`DumpFileHelper.cs`](../src/OngekiFumenEditor.Avalonia/Utils/DeadHandler/DumpFileHelper.cs) 和 [`FumenRescue.cs`](../src/OngekiFumenEditor.Avalonia/Utils/DeadHandler/FumenRescue.cs) 已编译，但当前没有初始化或外部调用点；
- `Utils/IPCHelper.cs` 使用命名 `MemoryMappedFile`，但已被 Core 项目文件 `Compile Remove`；
- 旧 OpenGL 字体实现会枚举系统字体目录并读取字体文件，但整个 `Kernel/Graphics/OpenGL/**/*.cs` 已被排除编译；
- `ConsoleWindowHelper` 创建的 `FileStream` 包装标准输入、输出和错误句柄，属于控制台 I/O，不是磁盘文件 I/O；
- 谱面解析器内部的 `StreamReader` 以及格式化器内部的 `StreamWriter` 只操作调用方流或 `MemoryStream`，磁盘归属由上层打开文件的位置决定。

对应编译排除项见 Core [项目文件](../src/OngekiFumenEditor.Avalonia/OngekiFumenEditor.Avalonia.csproj)。

## 8. 关键结论

### 8.1 Core 仍高度依赖本地路径

工程、谱面、音频、图片、纹理、快捷键及导出流程仍有直接 `System.IO` 路径依赖。临时文件、图片缓存、日志与救援写入已改为平台无关句柄；工程原子覆盖和 ACB 解码等依赖第三方路径 API 的流程会显式要求 Desktop `LocalPath`。Core 尚未整体转换为存储流或文件系统抽象边界。

### 8.2 平台接口的具体 I/O 不属于本报告结论

`ISettingManager`、`INAudioFileReaderFactory` 和 Avalonia StorageProvider 的具体实现位于平台项目。本报告只确认 Core 发起设置持久化、音频路径读取和文件选择请求，不对其实际存储介质、解码器或平台行为作结论。`ITemporaryFolderProvider` 是本报告的定向例外，其 Desktop、OPFS 和 discard 行为已在 4.9 节核对。

### 8.3 Core 项目的运行时资源输入不完整

当前 `src/OngekiFumenEditor.Avalonia/Resources` 只有图标资源，没有代码直接读取的 `Resources/editor` 和 `textureSizeAnchor.ini`。Core 项目文件也没有把这些文件包含为资源或内容。

此外，`DefaultFumenSoundPlayer` 默认读取 `AppContext.BaseDirectory/Sound`，而设置模型默认路径为 `./Resources/sounds/`，当前未发现二者之间的接线。

因此，纹理配置、编辑器纹理和默认音效读取在当前部署结构下存在确定的路径缺失或路径不一致问题。

### 8.4 日志目录由平台能力固定提供

日志位置已经由 `ILogFileStorage` 统一抽象：Desktop 是 exe 所在目录的 `logs`，Browser 是 OPFS 根的 `logs`。旧 `LogSetting.LogFileDirPath` 不再作为活动写入配置，设置页只展示实际能力，因此不存在“保存成功但日志仍写到另一目录”的断链。对应迁移审计条目 B-036 已完成并有核心、Desktop 和 Browser 契约测试证据。

### 8.5 临时存储只支持显式生命周期管理

跨平台临时存储已经统一事务写入、删除和根清理 API，但产品约束是不自动清空、不过期且不做容量淘汰。工程保存、远程图片缓存、ACB 解码、日志和崩溃救援会跨启动保留，直到显式删除、清理，或由操作系统/浏览器站点数据机制移除。

## 9. 默认路径汇总

| 数据 | 分类 | 默认路径或来源 |
| --- | --- | --- |
| 快捷键 | D1 | 当前工作目录 `keybind.json` |
| Core 日志 | D2 | Desktop：`AppContext.BaseDirectory/logs/*.log`；Browser：OPFS 根 `logs/*.log` |
| 通用临时文件 | A1 | Desktop：`%TEMP%/NagekiFumenEditorTempFolder/<subfolder>/`；Browser：OPFS `temp/<subfolder>/` |
| 网络图片缓存 | A5 | Desktop：`%TEMP%/NagekiFumenEditorTempFolder/images/*.img.cache`；Browser：OPFS `temp/images/*.img.cache` |
| ACB 解码 WAV 缓存 | A6 | Desktop：`%TEMP%/NagekiFumenEditorTempFolder/decodeAcbFiles/`；Browser 无本地路径，不执行该转换 |
| 崩溃救援工程和谱面 | A7 | Desktop 临时根或 Browser OPFS `temp/Rescue/` 下的自动恢复副本 |
| 崩溃转储 | D3 | `ProgramSetting.DumpFileDirPath`，默认 `./Dumps` |
| 编辑器纹理 | C2 | 当前工作目录 `./Resources/editor/*.png` |
| 纹理尺寸配置 | C3 | 当前工作目录 `./Resources/editor/textureSizeAnchor.ini` |
| 默认音效 | C4 | `AppContext.BaseDirectory/Sound/*.wav` |
| 谱面、工程及导出文件 | B | 用户选择或传入的路径 |

## 10. 审计限制

- 本报告基于当前未提交工作树，而不是某个 Git 提交；后续改动可能改变结果。
- 未实际运行 Core 工作流，也未捕获系统调用。
- Desktop、Browser、CommandLine、Core 的 `Platforms` 目录、测试和依赖项目均明确排除；平台服务的具体文件行为需要另行审计。
- 第三方托管库和原生 DLL 可能在内部执行额外的配置、缓存、临时文件或动态库加载操作；本报告只记录能从 Core 调用参数和公开 API 明确判断的部分。
- 扫描统计按源码调用行计数，不等同于一次应用运行中的实际 I/O 次数、字节数或性能成本。
