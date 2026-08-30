# OngekiFumenEditor.Avalonia 核心项目 System.IO File/Directory 实际使用审计

> 检查日期：2026-08-30
> 检查对象：`src/OngekiFumenEditor.Avalonia` 核心项目
> 检查方式：静态源码扫描、MSBuild 编译项核对、预处理常量核对和调用点追踪
> 限制：未进行 ETW、Process Monitor 或其他运行时 I/O 插桩

## 1. 结论摘要

当前核心项目仍然存在真实的本地文件系统依赖：

- 当前编译分支中共有 `23` 处直接 `File.*` 调用，分布于 `11` 个源文件；
- 共有 `2` 处直接 `Directory.*` 调用；
- 另有 `7` 处直接创建 `FileStream`，分布于 `5` 个源文件；
- 有 `1` 处 `Path.GetTempFileName()`。该 API 会创建真实文件，因此虽然属于 `Path.*`，仍计为文件系统 I/O；
- 不是所有已编译调用都有当前生产调用者。本文区分“当前可达”“已编译但未见调用”和“不参与当前编译”三种状态。

核心项目在 [`OngekiFumenEditor.Avalonia.csproj`](../src/OngekiFumenEditor.Avalonia/OngekiFumenEditor.Avalonia.csproj) 中启用了 `<ImplicitUsings>enable</ImplicitUsings>`，生成的全局 using 已包含 `System.IO`。因此，源文件没有显式 `using System.IO` 并不代表没有使用相关 API。

## 2. 统计口径

计入本次直接文件系统使用的内容：

- `File.*` 和 `Directory.*`；
- 直接创建的 `FileStream`；
- `FileInfo`、`DirectoryInfo` 等直接表达本地文件或目录语义的对象；
- `Path.GetTempFileName()`，因为它会创建一个零字节临时文件。

以下内容不计入直接文件系统 I/O 数量：

- 普通 `Path.GetFullPath`、`Path.Combine`、`Path.GetFileName` 等纯字符串处理；
- 调用方传入的普通 `Stream`、`MemoryStream`；
- Avalonia `IStorageFile`、`IStorageFolder` 自身的流操作；
- 注释、字符串内容和属性链中名称恰好为 `File` 的误报；
- 未启用的预处理分支和被 MSBuild `Compile Remove` 排除的源文件。

## 3. 当前可达的实际使用

### 3.1 键位配置持久化

[`DefaultKeyBindingManager.cs`](../src/OngekiFumenEditor.Avalonia/Kernel/KeyBinding/DefaultKeyBindingManager.cs) 直接读写工作目录下的 `keybind.json`：

- `File.WriteAllText` 保存配置（第 43 行）；
- `File.Exists` 判断配置是否存在（第 50 行）；
- `File.ReadAllText` 加载配置（第 54 行）；
- `Path.GetFullPath("./keybind.json")` 将相对路径转换为本地绝对路径（第 30 行）。

该类型作为键位管理服务注册并被应用使用，属于当前明确可达的本地持久化。

### 3.2 谱面转换

[`DefaultFumenConvertService.cs`](../src/OngekiFumenEditor.Avalonia/Modules/FumenConverter/Kernel/DefaultFumenConvertService.cs) 包含完整的路径输入和原子输出流程：

- `File.OpenRead` 打开路径形式的输入谱面（第 47 行）；
- `File.WriteAllBytesAsync` 写入目标文件旁的临时文件（第 115 行）；
- `File.Move(..., overwrite: true)` 提交结果（第 117 行）；
- `File.Exists` 和 `File.Delete` 清理失败时残留的临时文件（第 121-122 行）。

当 `InputFumenFile` 没有提供时会进入路径分支。Desktop 命令行转换处理器目前会使用该路径 API，因此这不是仅保留的兼容代码。

### 3.3 SVG 预览输出及临时文件泄漏

[`DefaultPreviewSvgGenerator.cs`](../src/OngekiFumenEditor.Avalonia/Modules/PreviewSvgGenerator/Kernel/DefaultPreviewSvgGenerator.cs) 在提供 `OutputFilePath` 时，通过 `File.WriteAllBytesAsync` 写出 SVG（第 70 行）。

[`GenerateSvgCommandHandler.cs`](../src/OngekiFumenEditor.Avalonia/Modules/PreviewSvgGenerator/Commands/GenerateSvg/GenerateSvgCommandHandler.cs) 使用以下表达式生成输出路径（第 39 行）：

```csharp
Path.GetTempFileName() + ".svg"
```

`Path.GetTempFileName()` 已经创建了一个真实的零字节临时文件。随后追加 `.svg` 会让生成器写入另一个路径，而原始临时文件没有被删除。因此，每次执行该命令都可能遗留一个无扩展名的空临时文件。

### 3.4 WAV 音频偏移

[`DefaultWavAudioOffsetService.cs`](../src/OngekiFumenEditor.Avalonia/Kernel/Audio/DefaultCommonImpl/Wave/DefaultWavAudioOffsetService.cs) 的字符串路径重载执行以下本地操作：

- `Directory.CreateDirectory` 创建输出目录（第 106 行）；
- 直接创建输入和临时输出 `FileStream`（第 260、281 行附近）；
- `File.Exists` 处理临时文件名碰撞（第 291 行）；
- `File.Move(..., overwrite: true)` 原子提交结果（第 302 行）；
- `File.Delete` 清理失败时的临时文件（第 309 行）。

当前音频调整 UI 的主要流程使用 `ISimpleFile` 重载，但字符串路径重载仍在公开接口及 `IAudioAdjustWindow.OffsetAudioFile` 路径入口中保留，并有测试覆盖。因此它仍是可达的本地路径能力，只是不是当前 UI 的首选路径。

### 3.5 SimpleFileSystem 本地后端

核心项目虽然已经引入 `ISimpleFile` 和 `ISimpleDirectory`，但当 `ISimpleFile.LocalPath` 可用时，Desktop 本地后端仍会落到 `System.IO`：

- [`SimpleFileWriteTransaction.cs`](../src/OngekiFumenEditor.Avalonia/Utils/SimpleFileSystem/SimpleFileWriteTransaction.cs)：创建临时 `FileStream`，并通过 `File.Move`、`File.Exists`、`File.Delete` 完成本地原子写入和清理；
- [`LocalSimpleFile.cs`](../src/OngekiFumenEditor.Avalonia/Utils/SimpleFileSystem/Impl/LocalFileSystem/LocalSimpleFile.cs)：使用 `File.Exists`、`FileInfo.Length`、`File.ReadAllBytesAsync` 以及读写 `FileStream`；Desktop 快速打开流程目前会构造和使用该类型；
- [`AvaloniaStorageProviderSimpleFile.cs`](../src/OngekiFumenEditor.Avalonia/Utils/SimpleFileSystem/Impl/AvaloniaStorageProvider/AvaloniaStorageProviderSimpleFile.cs)：本地原子移动发生访问拒绝时，重新以 `FileStream` 打开临时文件，再通过 Avalonia 存储提供程序提交；
- [`AvaloniaStorageProviderFileSystemBuilder.cs`](../src/OngekiFumenEditor.Avalonia/Utils/SimpleFileSystem/Impl/AvaloniaStorageProvider/AvaloniaStorageProviderFileSystemBuilder.cs)：当存储项能够取得本地路径时，使用 `File.GetAttributes` 检测 `FileAttributes.ReparsePoint`，从工程目录树中跳过符号链接、junction 和挂载点。

Browser 环境通常没有 `LocalPath`，会继续使用 Avalonia StorageProvider/浏览器存储流，不进入这些本地路径分支。

### 3.6 资源管理器打开前的路径检查

[`ProcessUtils.cs`](../src/OngekiFumenEditor.Avalonia/Utils/ProcessUtils.cs) 在调用 Explorer 前通过 `File.Exists` 和 `Directory.Exists` 判断参数是文件还是目录（第 34、36 行）。核心项目内没有直接调用者，但 Desktop 异常终止窗口会调用该方法，因此从整个应用调用图看属于实际使用。

### 3.7 目录启动对象

[`StandardizeFormatOutputService.cs`](../src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/Commands/OgkrImpl/StandardizeFormat/StandardizeFormatOutputService.cs) 创建 `DirectoryInfo`，并将其传给 Avalonia `Launcher.LaunchDirectoryInfoAsync` 打开标准化输出目录（第 56 行）。

这里没有直接调用 `Directory.*`，但明确依赖 `System.IO.DirectoryInfo` 表达本地目录。

## 4. 已编译但当前未发现生产调用者

### 4.1 IFumenParserManager 路径默认实现

[`IFumenParserManager.cs`](../src/OngekiFumenEditor.Avalonia/Parser/IFumenParserManager.cs) 的默认接口方法包含：

- `File.WriteAllBytesAsync` 路径序列化（第 15 行）；
- `File.OpenRead` 路径反序列化（第 20 行）。

当前调用点使用 `GetSerializer`、`GetDeserializer` 和 `ISimpleFile`/流接口，未找到这两个路径默认方法的调用者。它们属于已编译的旧式路径 API。

### 4.2 EditorProjectFileManager 字符串路径重载

[`EditorProjectFileManager.cs`](../src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/Kernel/EditorProjectFile/EditorProjectFileManager.cs) 的路径重载使用：

- `File.ReadAllBytesAsync` 加载工程文件（第 40 行）；
- `FileStream` 创建并保存工程文件（第 83 行）。

当前工程加载和保存流程使用 `Stream` 或 `ISimpleFile` 重载，未发现生产代码调用字符串路径重载。

### 4.3 ImageLoader 本地路径分支

[`ImageLoader.cs`](../src/OngekiFumenEditor.Avalonia/Utils/ImageLoader.cs) 对非 HTTP 路径使用 `File.ReadAllBytesAsync`（第 195 行）。服务仍有注册，但当前解决方案内未找到 `LoadImage` 的调用点，因此该本地路径分支目前没有确认的调用者。

### 4.4 IniFile

[`IniFile.cs`](../src/OngekiFumenEditor.Avalonia/Utils/IniFile.cs) 使用 `FileInfo.FullName` 规范化路径，并通过 Win32 INI API 读写文件。当前解决方案内未发现该类型的实例化位置，属于已编译但未启用的持久化能力。

## 5. 不属于当前实际使用的命中

### 5.1 未启用的 SVG prefab 分支

[`SvgImageFilePrefab.cs`](../src/OngekiFumenEditor.Avalonia/Base/EditorObjects/Svg/SvgImageFilePrefab.cs) 第 128 行存在 `File.Exists`，但它位于 `#if ENABLE_SVG_PREFAB_OBJECTS` 中。当前 Debug 和 Release 的 `DefineConstants` 均不包含该常量，因此该调用不参与当前编译。

该文件当前活动分支中的 `Path.IsPathFullyQualified` 和 `Path.GetFileName` 只是路径字符串处理。

### 5.2 已排除的旧 OpenGL 字体实现

[`DefaultStringDrawing.cs`](../src/OngekiFumenEditor.Avalonia/Kernel/Graphics/OpenGL/Drawing/StringDrawing/DefaultStringDrawing.cs) 包含 `Directory.GetFiles` 和 `File.ReadAllBytes`，但整个 `Kernel/Graphics/OpenGL/**/*.cs` 已在核心项目文件中通过 `Compile Remove` 排除，因此不计入当前编译结果。

### 5.3 注释中的 File 调用

[`FileHelper.cs`](../src/OngekiFumenEditor.Avalonia/Utils/FileHelper.cs) 中的 `File.Open` 和 `File.Exists` 仅存在于注释代码里，当前活动实现只使用 `Path.GetInvalidFileNameChars`。

### 5.4 已排除的 MemoryMappedFile

[`IPCHelper.cs`](../src/OngekiFumenEditor.Avalonia/Utils/IPCHelper.cs) 已被项目文件排除编译。其 `MemoryMappedFile` 也不是本次所检查的普通 `File`/`Directory` 磁盘访问。

## 6. 直接调用分布

下表只统计当前编译分支中的直接调用；“状态”来自当前解决方案的静态调用点追踪，不等同于运行时采样。

| 文件 | `File.*` | `Directory.*` | 直接 `FileStream` | 状态 |
| --- | ---: | ---: | ---: | --- |
| `DefaultKeyBindingManager.cs` | 3 | 0 | 0 | 当前可达 |
| `DefaultFumenConvertService.cs` | 5 | 0 | 0 | 当前可达，Desktop CLI 使用路径分支 |
| `DefaultPreviewSvgGenerator.cs` | 1 | 0 | 0 | 当前可达 |
| `DefaultWavAudioOffsetService.cs` | 3 | 1 | 2 | 路径 API 可达，当前 UI 主要使用句柄重载 |
| `SimpleFileWriteTransaction.cs` | 3 | 0 | 1 | `LocalPath` 可用时可达 |
| `LocalSimpleFile.cs` | 2 | 0 | 2 | Desktop 当前可达 |
| `AvaloniaStorageProviderFileSystemBuilder.cs` | 1 | 0 | 0 | 本地存储项检查时可达 |
| `AvaloniaStorageProviderSimpleFile.cs` | 0 | 0 | 1 | 本地提交失败回退时可达 |
| `ProcessUtils.cs` | 1 | 1 | 0 | Desktop 跨项目调用可达 |
| `IFumenParserManager.cs` | 2 | 0 | 0 | 已编译，未见调用 |
| `EditorProjectFileManager.cs` | 1 | 0 | 1 | 已编译，字符串重载未见调用 |
| `ImageLoader.cs` | 1 | 0 | 0 | 已编译，未见调用 |
| **合计** | **23** | **2** | **7** | |

表外还有以下 `System.IO` 相关对象或操作：

- `GenerateSvgCommandHandler.cs`：`Path.GetTempFileName()`，会创建实际文件；
- `StandardizeFormatOutputService.cs`：`new DirectoryInfo(...)`；
- `LocalSimpleFile.cs`：`new FileInfo(...).Length`；
- `IniFile.cs`：`new FileInfo(...).FullName`，但未见调用。

## 7. Path API 的边界

核心项目还广泛使用以下 `Path.*` 方法：

- `GetFullPath`、`GetRelativePath`、`IsPathFullyQualified`：路径边界、安全检查和同一文件判断；
- `GetExtension`、`GetFileName`：格式识别和显示名称；
- `Combine`、`GetDirectoryName`：构造转换、WAV 和本地事务的目标路径；
- `IsPathRooted`：输入路径校验；
- `GetInvalidFileNameChars`：文件名校验。

这些方法通常只处理字符串，本身不访问磁盘。唯一需要单独处理的是 `Path.GetTempFileName()`，因为它会创建文件。

## 8. 架构判断与后续关注点

工程文件和谱面编辑主流程已较多迁移到 `ISimpleFile`、`ISimpleDirectory` 和 Avalonia StorageProvider。剩余本地文件系统依赖主要集中在：

1. 键位配置的核心层直接持久化；
2. 谱面转换器和 WAV 服务保留的字符串路径 API；
3. Desktop `LocalPath` 下有意保留的本地原子写入事务；
4. 本地文件树的 reparse point 检查；
5. SVG 预览命令的临时文件创建和当前泄漏；
6. 已编译但没有当前调用者的旧式路径重载。

如果后续目标是进一步提高 Browser 兼容性或收紧核心项目的平台边界，优先级最高的检查点是：

- 修复 `Path.GetTempFileName() + ".svg"` 产生的孤立临时文件；
- 明确字符串路径重载是兼容 API、Desktop 专属能力，还是应迁移到 `ISimpleFile`；
- 决定 `keybind.json` 是否应继续由 Core 直接通过 `File.*` 管理；
- 删除或标记没有调用者的路径 API，避免未来业务代码重新绕过文件系统抽象。

## 9. 与旧审计文档的关系

仓库中的 [`disk-io-audit.md`](./disk-io-audit.md) 检查日期为 2026-08-04，记录的是当时更广泛的磁盘 I/O 状态。此后工程文件访问和 StorageProvider 抽象已有变化，其中部分旧调用和结论不再对应当前代码。

本文不覆盖或改写旧文档，而是作为 2026-08-30 当前工作树中 `System.IO.File`、`Directory`、`FileStream` 及紧密相关类型的聚焦快照。
