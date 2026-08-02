# Avalonia CommandLine 迁移跟踪

> 建立时间：2026-08-02
> 最后更新：2026-08-03
> 状态：Desktop 迁移完成，`convert`、`svg`、`jacket`、`updater` 已实现；`acb` 延期

## 1. 最终范围

- `OngekiFumenEditor.Avalonia.CommandLine` 是薄启动器，只引用 Desktop 并调用
  `DesktopCommandLineHost.Run(args)`。
- 命令框架、Definition、Handler、平台服务、`System.CommandLine` 和 Injectio 注册均由 Desktop
  拥有。
- Core 保留 `IFumenConvertService`、`IPreviewSvgGenerator`、`IFumenParserManager` 等可复用领域服务。
- 本轮注册 `convert`、`svg`、`jacket`、`updater`；根命令不注册 `acb`。
- 本轮不修改 Desktop/CommandLine 合包流程和 CI workflow。
- Updater 按已确认决策保留旧版高风险覆盖模型，不增加目录边界或事务增强。

## 2. 最终架构

### 2.1 应用生命周期

Gekimini App 增加可重写的 `ShouldCreateMainView`。Core App 用 `IsGUIMode` 实现该开关：

- GUI 模式创建 `IMainView`、主窗口和状态栏，恢复并在退出前保存窗口状态。
- 命令行模式仍执行 Avalonia/XAML 初始化、主题和语言初始化、Core/Desktop DI 与日志初始化。
- 命令行模式不解析 `IMainView`，不创建主窗口或状态栏，不恢复或保存窗口状态。
- Core App 在命令行模式跳过编辑器快捷键路由和 Splash。
- Desktop App 在命令行模式跳过 XamlMcp 与 GUI 启动参数处理。

`DesktopCommandLineHost.Run(string[] args)` 使用完整 Classic Desktop 生命周期和
`ShutdownMode.OnExplicitShutdown`。命令在 UI Dispatcher 初始化后执行，完成时调用
`desktop.Shutdown(exitCode)`，因此 Handler 的有符号退出码会穿透应用生命周期。

### 2.2 命令框架

```text
CommandLine Program.Main(args)
    -> DesktopCommandLineHost.Run(args)
        -> OngekiFumenEditorDesktopApp(isGUIMode: false)
            -> ICommandExecutor / DefaultCommandExecutor
                -> IEnumerable<ICommandLineDefinition>
                    -> Definition
                        -> ICommandLineHandler<TOptions>
                            -> 领域或平台服务
```

- `DefaultCommandExecutor` 只聚合 Definition、检查重复命令名并执行 `RootCommand`。
- `--verbose/-v` 是递归全局选项。
- Definition 显式创建 `Option<T>`，构造函数注入对应的闭合泛型 Handler。
- Handler 只接收强类型选项，不接触 `ParseResult`。
- Desktop DI 中恰好发现四个 Definition、四个闭合泛型 Handler 和一个
  `DefaultFumenParserManager`。
- CommandLine 项目没有 `System.CommandLine`、Injectio 或独立服务容器。
- CommandLine 的普通/AOT TFM 条件与 Desktop 完全一致。

## 3. 命令契约

### 3.1 `convert`

| 项目 | 契约 |
| --- | --- |
| 必填 | `--inputFile`、`--outputFile` |
| 可选 | `--standardize`，默认 `false` |
| 路径 | 输入和输出均必须为完全限定路径 |
| 成功 | 返回 `0`；真实 Nyageki 到 OGKR 可重新解析并保留关键语义 |
| 失败 | 路径错误 `-3`；转换失败、格式不支持或异常 `-4` |

### 3.2 `svg`

| 项目 | 契约 |
| --- | --- |
| 必填 | `--inputFile`、`--outputFile`、`--audioFile`，三者均必须为完全限定路径 |
| 默认值 | `maxXGrid=40`、`viewWidth=800`、`verticalScale=1`、`soflanMode=Soflan`、`png=false` |
| 时长 | 音频存在时使用音频时长；否则使用谱面末尾 TGrid 加 5 格 |
| SVG | 输出可解析 XML，根元素为 `svg`，声明正数宽高 |
| PNG | 按 SVG 声明尺寸栅格化；PNG 在 IEND 结束，无尾随 SVG 数据，可由 ImageSharp 解码 |
| 失败 | 路径错误 `-1`；解析、生成或栅格化失败 `-2` |

### 3.3 `jacket`

| 项目 | 契约 |
| --- | --- |
| 必填 | `--musicId`、`--outputFolder`、`--inputFile` |
| 默认值 | 大图 `520x520`，小图 `220x220`，`updateAssetBytesFile=true` |
| 绑定 | `--outputWidthSmall` 绑定小图宽，`--outputHeightSmall` 绑定小图高 |
| 输出 | 生成 `ui_jacket_NNNN` 和 `ui_jacket_NNNN_s` 两份 AssetBundle |
| 清单 | 更新 `assets.bytes` 时保留既有记录，并只追加缺失的两条记录 |
| 失败 | 路径错误 `-5`；图片、模板、编码或 AssetBundle 生成失败 `-6` |

Desktop 管理并复制以下 Jacket 资源：

- `ui_jacket_0666`
- `TexturePlugin.dll`
- `TexToolWrap.dll`
- `PVRTexLib.dll`
- `ispc_texcomp.dll`
- `crnlib.dll`

计划最初列出四个 DLL；实际检查 `TexToolWrap.dll` 导入表后确认还依赖 `crnlib.dll`，因此必须随包。
JIT 使用 ReadyToRun 处理 `TexturePlugin.dll`；Native AOT 额外以 `CopyToPublishDirectory=Always`
保留原 DLL，最终 AOT 文件与源文件 SHA-256 一致。

### 3.4 `updater`

必填内部参数：`--sourceFolder`、`--targetFolder`、`--sourceVersion`。

保留的旧版行为：

1. 递归枚举源目录文件，大小写不敏感地排除 `.log`、`.xml`、`.dmp`。
2. 按进程名 `OngekiFumenEditor.Avalonia.Desktop` 终止除当前 PID 外的实例。
3. 将既有目标移动为随机 `.bak_*` 备份。
4. 以不覆盖方式复制新文件。
5. 备份或复制失败时执行旧版回滚；复制失败可能保留新目标和 `.bak_*`，这是被测试锁定的旧行为。
6. 成功后删除备份；删除失败只记录日志，仍返回成功。
7. 启动 `OngekiFumenEditor.Avalonia.Desktop.exe`，参数固定为
   `--wait --notifySucess --sourceVersion <version>`。

退出码：终止进程失败 `-1`、备份失败 `-2`、复制失败 `-3`、成功 `0`。

风险边界：Updater 没有源/目标目录隔离、目标根保护或完整事务。调用者传入错误目录时可能覆盖任意
可写文件；这是本轮明确保留的旧版模型，不应在文档或 UI 中描述为安全更新器。

## 4. `acb` 延期

本轮没有 `AcbCommandLineDefinition`，根帮助明确不出现 `acb`。旧版契约为：

- 必填 `--musicId`、`--inputFile`、`--outputFolder`。
- `--previewBegin` 默认 `60000` 毫秒，`--previewEnd` 默认 `80000` 毫秒。
- 路径错误 `-7`，生成失败 `-8`。
- 输出 ACB/AWB 和由嵌入 `MusicSource.xml` 改写的音乐源 XML。

延期原因：旧实现直接依赖没有当前源码项目的 `AcbGeneratorFuck.dll`（1,622,528 字节）、
`VGAudio.Cli.Options`，以及五个 DereTore 二进制 DLL：

- `DereTore.Common.dll`
- `DereTore.Common.StarlightStage.dll`
- `DereTore.Exchange.Archive.ACB.dll`
- `DereTore.Exchange.Audio.HCA.dll`
- `DereTore.Interop.OS.dll`

这些二进制的 .NET 10、裁剪、Native AOT 和再分发状态尚未取得证据。恢复 `acb` 的条件：

1. 获得可维护源码或受支持的 .NET 10 包，并确认许可证/再分发边界。
2. 将生成逻辑封装为可注入服务，不恢复旧版反射选项绑定或全局 IoC。
3. JIT 和 Native AOT 均完成发布，并运行真实 WAV 到 ACB/AWB/XML 的成功与失败测试。
4. 校验 ACB/AWB 可被现有读取链重新打开，预览区间和音乐 ID 正确。
5. 满足以上条件后才注册 Definition；不能只复制旧 DLL 并把命令加入根帮助。

## 5. 自动化测试

新增 Windows TFM 的 `OngekiFumenEditor.Avalonia.Desktop.Tests`，Core 测试项目只引用 Core。
Updater 使用独立、无害的 Desktop Stub；Stub 已加入 solution，保证 Release 干净构建可复现。

| 范围 | 展开测试数 | 主要证据 |
| --- | ---: | --- |
| 框架与 `convert` | 18 | 帮助、重复命令、未知参数、verbose、Definition/Handler、真实 round-trip |
| `svg` | 15 | 必填/默认值、两种时长、SVG XML、PNG chunks/IEND、ImageSharp 解码 |
| `jacket` | 13 | 默认值/绑定、退出码、真实模板双 Bundle、纹理尺寸、`assets.bytes` |
| `updater` | 15 | 成功、`-1/-2/-3`、旧回滚、过滤、参数、真实 EXE+Stub |
| 注册、结构、生命周期 | 13 | 四命令/无 `acb`、DI 映射、TFM/引用、无窗口、退出码、主视图开关 |
| **Desktop 合计** | **74** | **0 失败、0 跳过** |

Release solution 最终结果：Core 144/144，Desktop 74/74，共 218/218，0 失败、0 跳过。

### 5.1 测试质量

- 静态未测试源扫描：2,120 个源文件、170 个测试文件、326 个名称配对源、1,794 个未配对源。
  该结果包含 Dependencies，仅是启发式名称配对，不是覆盖率。
- 对迁移高风险逻辑实证注入 10 个伪变异：路径 OR/AND、SVG 音频分支、尾部 `+5`、Jacket
  小图宽高、Updater 过滤/大小写/当前 PID/退出码/进程名和 Definition 注册。
- 首轮 8 个被杀死，2 个存活：过滤测试没有把排除文件交给 fake 枚举；进程名断言与生产常量
  自引用。修复后复注入，最终 10/10 全部被杀死，生产代码已恢复并全量回绿。
- 18 个文件包含 53 个源测试方法、74 个展开用例、277 个 `Assert.*` 调用，平均 5.23 个；
  零断言、仅平凡断言和自引用断言均为 0。使用 12 类中的 11 类断言，仅没有当前不需要的
  Approximate。

## 6. 发布与冒烟

所有发布均为 `win-x64`、Release、self-contained。

| 产物 | 模式 | EXE 字节数 | 结果 |
| --- | --- | ---: | --- |
| CommandLine | JIT + ReadyToRun | 162,816 | 发布成功 |
| CommandLine | Native AOT | 57,269,248 | 发布成功，保留既有裁剪/AOT 警告 |
| Desktop | JIT + ReadyToRun | 163,328 | 发布成功，启动 8 秒并创建主窗口 |
| Desktop | Native AOT | 57,268,224 | 发布成功，启动 8 秒并创建主窗口 |

CommandLine JIT/AOT 各执行 7 组最终冒烟，共 14/14：

- 根帮助：只列出 `convert/svg/jacket/updater`，返回 0。
- 相对路径 convert：返回有符号 `-3`，不创建输出。
- convert：真实 fixture 生成 635 字节 OGKR。
- svg：生成可解析 XML。
- svg PNG：签名 `89-50-4E-47-0D-0A-1A-0A`。
- jacket：生成 5,261/4,685 字节双 Bundle；最终 AOT 资源修正后再次运行返回 0。
- updater：只操作隔离目录和无害 Stub，重启参数完全匹配旧拼写。

全部 14 次命令行调用均未创建独立 GUI 窗口。四个 publish 目录均包含 Jacket 模板及所需 DLL。

构建顺序注意：普通 TFM 与 AOT TFM 共用项目 `obj/project.assets.json`。AOT 发布后直接对普通 TFM
使用 `--no-restore` 会出现 NETSDK1005；重新 restore 对应 TFM 即可。本轮最终 solution 测试已执行
普通 TFM restore，不存在残留失败状态。

## 7. 提交记录

| 批次 | 提交 | 内容 |
| --- | --- | --- |
| Gekimini 子模块 | `47338df` | 增加 service-only 启动与 `ShouldCreateMainView` |
| 1 | `bfbe4c72` | 生命周期、薄启动器、框架迁移与 `convert` |
| 2 | `853b85ea` | `svg`、音频时长与真实 PNG 栅格化 |
| 3 | `79451f96` | `jacket`、真实模板和原生资源 |
| 4 | 当前提交 `add updater command line and finalize migration` | `updater`、跨命令测试、发布修正和最终文档 |

## 8. CI 已知失败

本轮按边界没有修改 `../.github/workflows/BuildProgram.yml`。该 workflow 的 Native AOT 检查仍要求：

```text
exit code == 1
output matches "no commands are available yet"
```

当前 CommandLine 已有四个命令，空参数会显示根帮助而不是占位文本，因此该检查现在是确定的已知
失败项。本轮没有运行或记录该检查为通过；后续 CI 修改应把它替换为根帮助命令集合和至少一条真实
命令冒烟。

## 9. 剩余风险

- Updater 的高风险目录覆盖模型是有意保留的兼容行为。
- `acb` 尚未迁移，旧二进制兼容性和再分发状态未确认。
- Native AOT 仍报告共享 Core/Avalonia/Dock/SVG/DereTore 的既有裁剪和动态代码警告。
- Gekimini 引用的 SkiaSharp 2.88.3 仍报告 `NU1903` 高严重性漏洞告警。
- Browser restore 仍报告双包源 `NU1507`；与本轮 CommandLine 行为无关。
- Desktop/CommandLine 合包逻辑和 CI workflow 仍需单独更新。
