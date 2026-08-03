# OngekiFumenEditor.Avalonia FileSystem 抽象设计访谈

> 状态：访谈进行中
> 创建日期：2026-08-03
> 目标命名空间：`OngekiFumenEditor.Avalonia.Platforms.Services.FileSystem`
> 参考实现：ReOsuStoryboardPlayer.Avalonia `SimpleFileSystem`

## 1. 目标

为 `OngekiFumenEditor.Avalonia` 设计并最终实现基于 Avalonia Storage API 的文件系统抽象：

- `IFileSystem`：对标 ReOsuStoryboardPlayer.Avalonia 的 `SimpleIO` 与 `AvaloniaStorageProviderFileSystemBuilder`；
- `IFile`：对标 `ISimpleFile`；
- `IDirectory`：对标 `ISimpleDirectory`；
- 实现范围暂不包含 `Platforms/Services/FileSystem/Providers` 下的 Provider；
- 通过逐题设计访谈消除语义歧义，问答和最终决策持续记录在本文档中。

当前阶段只确定设计，不在决策完成前填充接口或实现类。

## 2. 已确认的仓库事实

### 2.1 当前草案

以下用户新增文件已经存在，但类型尚无成员：

- `Platforms/Services/FileSystem/IFileSystem.cs`；
- `Platforms/Services/FileSystem/IFile.cs`；
- `Platforms/Services/FileSystem/IDirectory.cs`；
- `Platforms/Services/FileSystem/IOOperationCanceledByUserException.cs`；
- `Platforms/Services/FileSystem/Providers/ITemporaryFolderProvider.cs`。

这些文件属于未提交工作树，设计及后续实现必须保留并增量修改，不得覆盖用户的并行变更。

`IResourceFolderProvider` 已废除。Core 项目的 `Resources/**` 全部作为 `AvaloniaResource`
内嵌；Desktop 可用 EXE 所在目录下的 `Resources` 按相对路径逐文件覆盖，缺失文件回退到内嵌资源，
Browser 始终只读取内嵌资源。资源覆盖不属于 Storage API 文件系统抽象。

### 2.2 技术基线

- Core 项目目标框架为 `net10.0`，语言版本为 `preview`；
- 当前 Avalonia 版本为 `11.3.18`；
- Avalonia `IStorageFile` 提供 `OpenReadAsync()` 和 `OpenWriteAsync()`；
- Avalonia `IStorageFolder` 提供异步枚举、按名称获取、创建文件和创建目录；
- Avalonia `IStorageItem` 提供元数据、书签、父目录、删除和移动能力，并要求明确管理 `Dispose()` 生命周期；
- 本设计应优先保留 Storage API 的流和存储项语义，不应以 `TryGetLocalPath()` 作为基础能力。

### 2.3 当前调用边界

现有 `FileDialogHelper` 直接取得 `Application.Current.TopLevel.StorageProvider`，打开文件、保存文件或选择目录后立即调用 `TryGetLocalPath()`，返回 `string`。

Core 当前有 12 个 `FileDialogHelper` 调用点，覆盖：

- 谱面和音频文件选择；
- 谱面转换输入、输出；
- WAV 调整输入、输出；
- SVG 文件选择；
- 音效、日志和崩溃转储目录选择。

完整磁盘 I/O 清单及 A/B/C/D 分类见 [disk-io-audit.md](./disk-io-audit.md)。该审计表明 Core 不只有读取需求，还存在保存、覆盖、创建目录、复制、移动、删除、临时提交和缓存等行为。是否全部纳入首版 FileSystem 抽象仍待访谈决定。

## 3. 参考设计分析

检查基于 ReOsuStoryboardPlayer.Avalonia `master` 的提交 [`aba10495`](https://github.com/MikiraSora/ReOsuStoryboardPlayer.Avalonia/tree/aba10495ffd726f484f1c1c900a4091ea11bf99a)。

### 3.1 数据流

```text
已有 IStorageFolder -> 递归枚举 -> Avalonia Storage 目录树适配器 --+
                                                               |
ZIP byte[] -> ZipArchive -> ZIP 目录树适配器 -------------------+-> ISimpleDirectory
                                                                      |
                                                                      v
                                                                 SimpleIO
                                                                      |
                                                                      v
                                                     Storyboard、谱面、图片、音频解析
```

### 3.2 类型职责

| ReOsu 类型 | 职责 | 本项目对应目标 |
| --- | --- | --- |
| `ISimpleFile` | 文件名、虚拟路径、长度、读取全部字节/文本、打开只读流 | `IFile` |
| `ISimpleDirectory` | 父目录、子目录、子文件、虚拟路径、直接子项查询 | `IDirectory` |
| `SimpleIO` | 相对路径拆分、目录/文件查找、存在性检查、通配符查询、打开读取流 | `IFileSystem` 的路径操作部分 |
| `AvaloniaStorageProviderFileSystemBuilder` | 把已有 `IStorageFolder` 递归投影为虚拟目录树 | `IFileSystem` 的 Storage API 适配部分 |

### 3.3 值得保留的原则

- 业务解析器依赖文件、目录和 `Stream`，不依赖本地磁盘路径；
- 用户选择、ZIP、网络或其他来源可以在业务层之前适配成相同模型；
- 文件内容延迟读取，目录结构与内容访问分离；
- 根目录拥有子存储项的生命周期，业务对象最终释放根目录。

### 3.4 不应原样照搬的行为

- ReOsu 接口完全只读，不能覆盖本项目的保存和临时提交需求；
- 目录树会在构建时完整递归枚举，对大型目录成本较高；
- `SeekableStream` 使用无界内存缓存，并遗漏归还 `ArrayPool<byte>` 缓冲区；
- ZIP 后端没有确定性释放 `ZipArchive` 和底层流；
- API 没有 `CancellationToken`；
- `SimpleIO` 是静态类，Builder 也是静态入口，没有依赖注入边界；
- `FullPath` 实际是虚拟路径，却容易被误解为可供 `System.IO` 使用的本地路径；
- 文件选择器由调用方直接使用，不属于 ReOsu 的 `SimpleFileSystem`。

## 4. 待解析的设计树

问题按依赖顺序逐一解决，后续问题可能根据前一答案调整：

1. `IFileSystem` 是否拥有文件选择器等用户交互；
2. 首版是只读、完整读写，还是能力拆分；
3. `IFileSystem` 是全局无状态服务、一个挂载根实例，还是二者拆分；
4. `IFile` / `IDirectory` 是否直接暴露底层 `IStorageItem`；
5. 目录枚举采用实时异步访问还是预构建快照树；
6. 路径模型、大小写规则、分隔符、`.` / `..` 与越界行为；
7. 创建、覆盖、删除、移动及原子提交的语义；
8. 用户取消、未找到、权限不足、能力不支持和 I/O 失败的错误模型；
9. 生命周期、句柄所有权、缓存和并发访问；
10. `CancellationToken`、进度报告和大文件策略；
11. `Providers` 与通用 FileSystem 的边界；
12. 旧 `string` 路径 API 的迁移和兼容策略；
13. 单元测试、内存测试后端和平台集成测试边界。

## 5. 问答与决策记录

### Q1. `IFileSystem` 是否负责弹出文件或目录选择器？

**状态：** 等待回答。

**问题背景：**

当前空的 `IoOperationCanceledByUserException` 暗示文件系统层可能计划处理用户取消；但参考项目的实际分层是：ViewModel 使用 `TopLevel.StorageProvider` 完成选择，`SimpleFileSystem` 只包装已经取得的 `IStorageFolder` 或 ZIP 数据。文件选择器依赖当前 `TopLevel`、窗口生命周期和用户交互，而普通文件查找及读取不应依赖 UI。

**建议答案：不负责。**

建议让 `IFileSystem` 只接收并包装已经获得的 `IStorageFile` / `IStorageFolder`，提供存储项适配、相对路径导航和后续确定的文件操作。打开、保存和目录选择应保留在单独的 Picker 服务或 UI 协调层中；Picker 再把选中的 Avalonia 存储项交给 `IFileSystem`。

这样做的直接结果：

- `IFileSystem` 不需要取得或持有 `TopLevel`；
- 文件操作可以在后台服务、测试和无 UI 场景复用；
- “用户取消”属于 Picker 结果，而不是文件系统异常；
- `IoOperationCanceledByUserException` 应删除、移到 Picker 边界，或等待 Picker 设计时再决定；
- 与 ReOsu 的实际职责分离保持一致，同时避免复制其静态 Builder 形式。

**备选答案：负责。**

若 `IFileSystem` 同时负责 Picker，它会成为应用级平台存储门面，必须定义 `TopLevel` 的获取方式、能力检查、用户取消语义、UI 线程约束和无窗口行为。这样调用方便，但会把 UI 交互、存储项适配和文件操作耦合在同一个接口中。

**用户回答：** 待补充。

**最终决策：** 待补充。
