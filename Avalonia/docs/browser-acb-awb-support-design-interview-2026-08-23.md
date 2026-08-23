# Browser ACB/AWB 支持设计访谈与实施计划

- **文档日期**：2026-08-23
- **状态**：设计访谈阶段；MVP 范围和产品行为已基本确定，文件抽象 API 边界仍待最终确认
- **目标平台**：Avalonia Browser / WebAssembly（标准 Browser AOT，兼顾 LLVM Browser）
- **相关能力**：ACB、内嵌 AWB、外部 AWB、HCA 解码、OPFS/StorageProvider、项目文件夹导入

## 1. 文档目的

本文记录 Browser 支持 ACB/AWB 的完整设计访谈过程、已确认的产品决策、代码现状、约束、风险和分阶段实施计划。

本文不是“已经完成”的实现说明。目前 Browser 仍有以下能力开关未解除：

- `DefaultBrowserFumenVisualEditorProvider` 仍使用 `supportsAcb: false`；
- `BrowserNAudioFileReaderFactory` 的普通音频扩展列表目前只有 `.wav`、`.aif`、`.aiff`；
- `AcbPackageInspector` 仍会在 Browser 环境返回 `BrowserUnsupported`；
- `EditorProjectDataUtils` 仍会拒绝 Browser 上的 ACB 项目；
- Browser 项目打开流程尚未把用户选择的外部 AWB 导入项目目录。

## 2. 背景与已知代码基础

### 2.1 之前的程序集问题

Browser 发布后打开项目时曾出现：

```text
Unable to open the selected project: Could not load file or assembly
'DereTore.Exchange.Archive.ACB, Version=0.8.1.176, Culture=neutral,
PublicKeyToken=null' or one of its dependencies.
```

已经完成的发布侧修复是：

- 在两个 Browser 工程中显式声明 DereTore DLL 引用；
- 将以下程序集加入 `TrimmerRootAssembly`：
  - `DereTore.Common`
  - `DereTore.Exchange.Archive.ACB`
  - `DereTore.Exchange.Audio.HCA`
  - `DereTore.Interop.OS`
- 全新 Browser Release AOT 发布目录已确认包含这些程序集对应的带指纹 WASM、Brotli 和 GZip 资源。

这只解决了 linker/发布产物缺少程序集的问题，**不等于 Browser 已经支持 ACB 播放**。

### 2.2 已存在的 ACB/AWB 能力

共享核心已经有流式 ACB 转换基础：

- `AcbConverter.ConvertAcbFileToWavAsync(Stream, Stream, ISimpleFile, ...)`；
- 支持 ACB 内嵌 AWB；
- 支持传入外部 AWB 流；
- 使用 `Afs2Archive` 查找 AFS 条目；
- 找到第一个可识别的 HCA 后，转换为临时 WAV；
- `IAudioManager.LoadAudioAsync(Stream acbStream, Stream externalAwbStream)` 已存在；
- Browser 已有 `BrowserTemporaryFolderProvider`，可用于临时 WAV 和 staging 文件。

### 2.3 已存在的外部 AWB 项目模型

项目文件访问上下文已经支持外部 AWB：

- `EditorFileAccessContext.AudioAwbFile`；
- `EditorFileAccessContextSnapshot.AudioAwbFileBookmark`；
- 项目文件夹打开时会优先查找 ACB 声明的同名 sibling AWB；
- 找不到时可以调用 `PickExternalAwbAsync`；
- recent project 可以保存和恢复外部 AWB bookmark；
- `EditorProjectCreationTransaction` 在创建新项目时已经有复制外部 AWB 的基础。

本轮 Browser 需求主要补齐的是：**打开已有项目文件夹时的 ACB/AWB 导入、验证和绑定**。

## 3. 最终产品范围：MVP

### 3.1 选定范围

采用 MVP（原方案 A）：

Browser 需要支持：

- 打开包含 ACB 音频的项目；
- 支持 ACB 内嵌 AWB；
- 支持 ACB 引用的外部 AWB；
- 外部 AWB 可从项目目录自动发现，也可由用户选择；
- 将项目外选择的 AWB 复制进项目目录；
- 解码第一个可播放的 HCA；
- 转换为临时 WAV 并播放；
- 项目打开成功后，绑定项目目录内的 AWB。

### 3.2 非目标

本轮不实现：

- ACB cue 列表选择；
- 多个音轨选择；
- 循环参数编辑或展示；
- ACB 元数据浏览器；
- 任意嵌套目录的外部 AWB 引用；
- 外部 AWB 复制失败后的临时 fallback；
- recent project 恢复时自动把外部 AWB 再导入项目。

## 4. 设计访谈决策记录

### Q1：Browser 的 ACB 支持范围

**问题**：选择实用 MVP、完整 ACB 支持，还是只允许打开不播放？

**最终决定：MVP。**

只支持内嵌/外部 AWB 和第一个可播放 HCA，不做 cue 和多轨高级功能。

---

### Q2：外部 AWB 的匹配规则

**问题**：是否沿用严格绑定规则？

**最终决定：是。**

规则：

- ACB 声明 `audio.awb` 时，优先查找项目目录中的同名 `audio.awb`；
- 找不到时弹出 AWB 文件选择器；
- 选择器只允许 `.awb`；
- 仍要求 AWB 文件名与 ACB 声明的目标名一致；
- 不支持任意改名；
- 不支持 ACB 引用嵌套子目录中的 AWB；
- 外部选择的 AWB 最终必须复制到项目目录。

---

### Q3：项目内已经存在同名 AWB 时的处理

**问题**：如果项目内已有同名 AWB，而用户又选择了一个项目外的同名 AWB，如何处理？

**最终决定：比较后决定。**

- 内容相同：直接复用项目内 AWB；
- 内容不同：弹出替换确认；
- 用户取消：不修改项目并取消本次打开；
- 用户确认替换：使用 staging + 原子提交；
- 不允许静默覆盖已有 AWB。

---

### Q4：AWB 何时正式写入项目目录

**问题**：复制操作是立即执行，还是事务式提交？

**最终决定：事务式提交。**

打开项目文件夹时按以下顺序执行：

1. 读取并检查 ACB；
2. 找到或选择外部 AWB；
3. 将外部 AWB 流式复制到临时 staging 文件；
4. 对 staging AWB 做长度、抽样和完整比较；
5. 使用 ACB + staging AWB 尝试解码第一个 HCA；
6. 解码成功后，才将 AWB 提交到项目目录；
7. 成功提交后，编辑器上下文绑定项目内 AWB；
8. 任一步失败，都不修改项目中已有的 AWB。

---

### Q5：项目目录无法写入或复制失败时的行为

**问题**：浏览器没有写权限、OPFS 写入失败、空间不足或复制中断时，是否允许继续使用外部 AWB？

**最终决定：不允许 fallback。**

复制失败时：

- 取消打开；
- 显示需要项目目录写权限的错误；
- 不绑定外部 AWB 继续运行；
- 不留下半截项目 AWB；
- 不修改已有项目 AWB。

这样保证“Browser 项目打开成功”意味着 AWB 已经按要求导入或已复用项目内版本。

---

### Q6：recent project 恢复时是否自动导入外部 AWB

**问题**：recent project snapshot 中保存的外部 AWB bookmark 是否也要自动复制进项目？

**最终决定：不自动导入（方案 B）。**

recent project 维持现有行为：

- 从 snapshot 恢复外部 AWB bookmark；
- bookmark 有效时继续使用外部 AWB；
- 不自动复制到项目目录；
- bookmark 失效时提示用户重新打开项目文件夹并重新绑定 AWB；
- recent project 不执行本轮新增的导入事务。

这是有意接受的产品权衡：

- “从项目文件夹打开”会尽量使项目自包含；
- “从最近项目恢复”仍可能依赖历史外部 bookmark。

---

### Q7/Q8：如何比较两个 AWB 是否相同

**问题**：使用完整比较、哈希，还是随机抽样？

用户选择了“随机抽取 offset + length”，随后确认采用“抽样预检后完整比较”。

**最终决定：确定性抽样预检 + 完整流式比较。**

流程：

1. 先比较文件长度；
2. 对小文件直接完整流式比较；
3. 对大文件使用固定、可复现的伪随机算法生成多个 `offset + length` 片段；
4. 任意抽样片段不同，立即判定不同；
5. 所有抽样片段一致后，再从头到尾做完整流式逐字节比较；
6. 只有完整比较一致，才允许复用项目内 AWB。

注意：

- 不使用真正随机数，避免同一对文件每次结果不同；
- 抽样只是快速预检，不能作为最终一致性证明；
- 不依赖修改时间、文件名或 `GetHashCode()`；
- 不将整个 AWB 一次性读入内存。

---

### Q9：内容不同时的确认窗口

**问题**：同名 AWB 内容不同的时候，确认窗口提供哪些按钮？

用户曾短暂选择过其他分支，最终确认采用 A。

**最终决定：只提供“替换 / 取消”。**

窗口应显示：

- 项目内 AWB 路径；
- 外部 AWB 路径；
- 两个文件的大小；
- 替换警告；
- 默认焦点放在“取消”。

按钮：

```text
替换
取消
```

不提供“使用现有”按钮，因为内容已经确认不同，继续使用旧 AWB 容易造成用户误用错误音频。

---

### Q10：Browser 替换已有 AWB 的策略

**问题**：直接覆盖、使用已有 `WriteAsync`，还是新增 Browser 专用 API？

**最终决定：采用显式 staged/atomic replacement。**

但实现必须使用平台无关的文件抽象：

- staging 使用 `ITemporaryFolderProvider` / `ITemporaryFile`；
- 项目文件使用 `ISimpleFile` / `ISimpleDirectory`；
- 业务层只通过流复制；
- Browser/OPFS 的原子提交由底层 StorageProvider 实现；
- 失败时旧 AWB 必须保持不变；
- 如果某个平台不能提供安全替换，则拒绝替换，不静默覆盖。

---

### Q11：不使用 System.IO 直接文件操作

**约束：**

业务层禁止使用以下直接文件系统操作：

```csharp
File.*
Directory.*
FileStream
File.Copy
Directory.CreateDirectory
```

也不允许通过 `LocalPath` 绕过 Browser/OPFS 抽象。

允许使用现有抽象提供的流：

- `ISimpleFile.OpenRead()`；
- `ISimpleFile.WriteAsync()`；
- `ITemporaryFile.OpenReadAsync()`；
- `ITemporaryFile.WriteAsync()`；
- `Stream.CopyToAsync()`。

`System.IO` 相关的底层实现只应存在于对应平台 Provider 内部，不能进入 ACB/AWB 业务服务。

---

### Q12：`ITemporaryFile` 是否直接转换为 `ISimpleFile`

**问题**：是否让 `ITemporaryFile` 继承 `ISimpleFile`，然后实现 `ISimpleFile.ReplaceTo`？

**当前建议：不直接继承，待最终确认。**

原因：

- `ITemporaryFile.GetLengthAsync()` 是异步，`ISimpleFile.FileLength` 是同步；
- `ITemporaryFile` 没有普通 `OpenWrite()`；
- 临时文件的生命周期和普通文件不同；
- 临时文件和项目文件可能属于不同的底层存储 Provider；
- 跨 Provider 通常不能做物理 rename；
- 直接继承会暴露临时文件实际不支持的能力。

建议改为：

1. 增加一个只读内容源抽象，例如 `IFileContentSource`；
2. `ITemporaryFile` 和 `ISimpleFile` 都实现该抽象；
3. 在目标 `ISimpleFile` 上提供 `ReplaceFromAsync`；
4. 如需保留用户希望的调用形式，再提供 `ReplaceToAsync` 扩展方法。

示意：

```csharp
public interface IFileContentSource
{
    string FileName { get; }

    Task<long> GetLengthAsync(
        CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(
        CancellationToken cancellationToken = default);
}

public interface ISimpleFile : IDisposable, IFileContentSource
{
    Task ReplaceFromAsync(
        IFileContentSource source,
        CancellationToken cancellationToken = default);
}

public static class FileContentSourceExtensions
{
    public static Task ReplaceToAsync(
        this IFileContentSource source,
        ISimpleFile target,
        CancellationToken cancellationToken = default) =>
        target.ReplaceFromAsync(source, cancellationToken);
}
```

关键语义是：

```text
目标项目 AWB.ReplaceFromAsync(临时 AWB)
```

而不是让源文件负责了解目标 Provider 的原子替换机制。

这是当前尚未最终确认的 API 设计点。

## 5. 目标实现流程

### 5.1 项目文件夹打开：内嵌 AWB

```text
选择项目目录
    ↓
找到项目描述文件、谱面文件和 ACB
    ↓
解析 ACB
    ↓
检测到内嵌 AWB
    ↓
直接用 ACB 内嵌 AWB 解码
    ↓
创建编辑器上下文
```

不创建外部 AWB，不修改项目目录。

### 5.2 项目文件夹打开：外部 AWB 已存在且内容相同

```text
解析 ACB，得到 expected.awb
    ↓
查找项目目录中的 expected.awb
    ↓
长度 + 确定性抽样 + 完整比较
    ↓
内容一致
    ↓
复用项目内 AWB
    ↓
用项目内 AWB 解码并打开
```

### 5.3 项目文件夹打开：外部 AWB 不存在

```text
解析 ACB，得到 expected.awb
    ↓
项目目录没有 expected.awb
    ↓
弹出 .awb 选择器
    ↓
要求所选文件名符合 ACB 声明
    ↓
流式复制到 ITemporaryFile
    ↓
用临时 AWB 解码验证
    ↓
验证成功
    ↓
创建项目内 expected.awb
    ↓
通过 ReplaceFromAsync / ReplaceToAsync 提交
    ↓
绑定项目内 AWB
```

### 5.4 项目文件夹打开：同名但内容不同

```text
项目内 expected.awb
外部选择的 expected.awb
        ↓
长度/抽样/完整比较
        ↓
内容不同
        ↓
显示“替换 / 取消”
```

选择“取消”：

- 不写项目文件；
- 不删除或修改旧 AWB；
- 不使用外部 AWB fallback；
- 取消本次打开。

选择“替换”：

- 先使用临时 AWB 完成解码验证；
- 验证成功后调用目标文件的原子替换操作；
- 提交成功后刷新/重新取得项目内 AWB capability；
- 绑定项目内 AWB。

## 6. 建议的代码分层

### 6.1 共享 Core 层

建议新增或整理：

- `IFileContentSource`（如果最终采用该方案）；
- `ReplaceFromAsync` 的共享契约；
- AWB 比较器：长度、确定性抽样、完整流式比较；
- 外部 AWB 导入服务；
- ACB/AWB 导入结果和失败原因模型。

Core 业务服务不依赖：

- `System.IO.File`；
- `System.IO.Directory`；
- `FileStream`；
- Browser JavaScript；
- Avalonia Browser 专用类型。

### 6.2 Browser 层

需要：

- 将 `supportsAcb: false` 改为 `true`；
- 将 `.acb` 加入 Browser 的音频能力列表；
- 让 `AcbPackageInspector` 在 Browser 通过流检查 ACB，而不是要求 `LocalPath`；
- 移除 Browser 环境下无条件的 `BrowserUnsupported` 分支；
- 让 `EditorProjectDataUtils` 使用 ACB/AWB 流验证，而不是直接拒绝 Browser；
- 实现/验证 Browser StorageProvider 对目标文件的原子写入语义；
- 对无法写入的项目目录返回可理解的错误。

注意：`.acb` 不应直接交给 `BrowserNAudioFileReaderFactory` 的 WAV/AIFF reader。ACB 应继续由 `NAudioManager` 的 ACB 专用分支转换为临时 WAV，再交给普通 WAV reader。

### 6.3 Desktop 层

- 保持现有 Desktop ACB 行为；
- 让 Desktop 的 `ISimpleFile` 实现满足新的替换契约；
- 保留现有本地文件原子写入实现；
- 不引入 Browser 专用逻辑。

### 6.4 UI 层

新增一个跨平台的 AWB 替换确认窗口/ViewModel：

- 显示源文件和目标文件；
- 显示大小；
- “替换”/“取消”；
- 默认“取消”；
- 由项目打开流程调用，而不是由底层文件 Provider 弹窗。

现有 `IDialogManager` 只有消息对话框能力，确认窗口更适合通过现有 `IWindowManager.ShowDialogAsync(...)` 实现。

## 7. 测试计划

### 7.1 比较器测试

- 长度不同立即返回不同；
- 小文件完整一致；
- 小文件单字节差异；
- 大文件抽样命中差异；
- 大文件抽样未命中差异，但完整比较最终识别不同；
- 完全一致时返回相同；
- 固定种子/确定性抽样在多次执行中结果一致；
- 取消令牌在读取中生效；
- 不使用 `ReadAllBytes` 一次性加载大 AWB。

### 7.2 导入事务测试

- 内嵌 AWB 不创建外部文件；
- 项目内 AWB 相同：复用，不替换；
- 项目内不存在 AWB：成功复制到声明的目标名；
- 项目内 AWB 不同 + 用户取消：项目不变；
- 项目内 AWB 不同 + 用户确认：完成原子替换；
- ACB 解码失败：项目不变；
- 临时 staging 失败：项目不变；
- 项目目标创建失败：项目不变；
- 原子替换失败：旧 AWB 内容不变；
- 复制失败不使用外部 AWB fallback；
- 复制成功后上下文绑定的是项目内 AWB，不是外部 capability。

### 7.3 recent project 测试

- snapshot 中有效外部 AWB bookmark 仍按旧行为恢复；
- recent 恢复不自动复制 AWB；
- bookmark 失效时显示重新绑定提示；
- recent 行为不触发本轮导入确认窗口。

### 7.4 Browser 合同测试

- Browser 工程 `supportsAcb` 为 true；
- Browser 音频扩展列表包含 `.acb`；
- Browser 不再无条件返回 `ACB audio is not supported by this platform.`；
- ACB/AWB 程序集显式引用和 trimming root 仍存在；
- 普通 Browser 和 LLVM Browser 工程都保留 ACB 依赖；
- Browser OPFS/StorageProvider 的目标替换失败保持旧内容；
- 真实浏览器中测试内嵌 AWB、外部 sibling AWB 和用户选择外部 AWB。

## 8. 发布与运行验收

### 8.1 构建验收

- Core Debug/Release 构建通过；
- Browser Debug 构建通过；
- Browser Release AOT 发布通过；
- LLVM Browser 构建/发布按当前工具链能力单独记录；
- 产物包含：
  - `DereTore.Exchange.Archive.ACB`；
  - `DereTore.Common`；
  - `DereTore.Exchange.Audio.HCA`；
  - `DereTore.Interop.OS`。

### 8.2 浏览器验收

至少验证：

1. ACB 内嵌 AWB 项目可以打开并播放；
2. 项目目录已有同名外部 AWB 时可以自动复用；
3. 项目目录没有 AWB 时可以选择并导入；
4. 外部 AWB 导入后刷新页面/重新打开项目，项目目录内仍存在 AWB；
5. 同名不同内容时取消不会修改旧 AWB；
6. 同名不同内容时替换成功后播放新 AWB；
7. 目录只读或写入失败时打开失败且不留下半成品；
8. recent project 行为保持“外部 bookmark 不自动导入”的既定策略；
9. 浏览器控制台没有程序集加载错误、未处理的 JS interop 错误或 ACB 解码异常。

## 9. 风险与取舍

### 9.1 recent project 不自动导入的取舍

已确认采用方案 B，因此项目在 recent 恢复路径下仍可能依赖项目外 AWB bookmark。这是有意的行为，不应在实现中偷偷改变为自动导入。

### 9.2 跨 Provider 的“原子”含义

临时 AWB 通常位于临时 Provider，项目 AWB 位于用户选择的项目 Provider。两者不一定属于同一个物理存储，不能承诺跨 Provider 的单次 rename。

本方案中的原子性定义为：

- staging 文件完整写入后才开始提交；
- 目标 Provider 只有在新内容完整写入后才提交替换；
- 提交失败时目标旧内容保持不变；
- 不要求临时文件和项目文件共享同一个底层文件系统事务。

### 9.3 ACB parser 的旧式路径参数

部分 DereTore API 仍需要一个路径/文件名参数。Browser 不应依赖 `LocalPath`，应传入稳定的虚拟路径或文件名，并通过流读取实际内容。需要用真实 Browser ACB 样本验证该参数不会触发本地文件访问。

### 9.4 内存与大文件

- AWB 比较必须流式进行；
- staging 复制必须流式进行；
- HCA 条目解码继续使用已有的 `ArrayPool<byte>`；
- Browser OPFS 读取流已经是分块读取，不能为了方便改成全文件 `ReadAllBytes`；
- 临时 WAV 需要继续使用 `ITemporaryFolderProvider`，不能改回本地临时路径。

## 10. 当前待确认项

当前唯一尚未最终确认的架构细节是：

> 是否采用 `IFileContentSource` + 目标端 `ISimpleFile.ReplaceFromAsync`，并提供源端 `ReplaceToAsync` 扩展方法，而不是让 `ITemporaryFile` 直接继承 `ISimpleFile`。

推荐采用该方案，原因是：

- 不伪造临时文件的写入/父目录/生命周期能力；
- 目标文件拥有原子提交语义；
- 能处理临时 Provider 与项目 Provider 不同的情况；
- 业务层保持平台无关；
- 可以通过扩展方法保留直观的 `temporaryAwb.ReplaceToAsync(projectAwb)` 调用形式。

## 11. 实施顺序

1. 确认文件内容源和原子替换 API；
2. 为 Core 和各 Provider 增加/强化替换契约；
3. 实现确定性抽样 + 完整流式 AWB 比较器；
4. 实现平台无关的外部 AWB staging/import 服务；
5. 增加替换确认窗口；
6. 修改 Browser ACB 能力开关和流式验证逻辑；
7. 将导入事务接入 `OpenFromFolderAsync`，保持 recent 路径不自动导入；
8. 增加 Core、Browser 合同和 Provider 失败回滚测试；
9. 执行 Browser Debug/Release/AOT 构建；
10. 使用真实浏览器验证内嵌 AWB、外部 AWB、替换、取消和写入失败场景；
11. 再决定是否提交程序集发布修复和 ACB/AWB 功能实现为一个 commit，或拆成两个逻辑 commit。

## 12. 当前结论

本轮设计已经确定 Browser ACB/AWB 支持的产品行为：

- Browser 支持 ACB；
- 支持内嵌和外部 AWB；
- 外部 AWB 必须严格匹配 ACB 声明并导入项目；
- 同内容复用，不同内容询问替换；
- 替换失败不破坏旧文件；
- 复制/写入失败不允许外部 fallback；
- recent project 暂不自动导入外部 AWB；
- 比较使用确定性抽样预检后完整流式比较；
- 业务层不使用 `System.IO` 直接文件系统 API；
- 原子提交由平台无关文件抽象及各 Provider 实现；
- 当前尚待最终确认的是 `ITemporaryFile` 与 `ISimpleFile` 的抽象关系，以及 `ReplaceFromAsync`/`ReplaceToAsync` 的最终 API 形态。