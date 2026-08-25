# 15B 测试研究

更新时间：2026-08-02 00:50 +08:00

## 范围与验收清单

本轮仅研究 XAML 迁移及相关新实现的可验证入口，不恢复已经删除或暂缓的功能。后续测试必须逐项覆盖：

- [ ] AXAML、应用资源和视图构造可以在 Avalonia Headless 中完成。
- [ ] 键位定义、组合键匹配以及编辑器顶层输入路由行为可验证。
- [ ] DataGrid 行顺序算法及禁止列排序的 UI 约束可验证。
- [ ] `INAudioWavePlayerFactory` 的共享契约，以及 Desktop/Browser 平台实现的注册和选择规则有证据。
- [ ] Avalonia 托管 Skia 画布能够产生非空白像素。
- [ ] Desktop `win-x64` Native AOT 发布成功，并有独立的启动烟测记录。
- [ ] 使用 `C:\Users\mikir\Desktop\音寄谱\拉面` 做外部谱面语料解析、格式化、再解析验证。
- [ ] 明确区分“语料目录缺失”“不支持的附件类型”“谱面解析失败”和“已决策排除的命令”。

## 仓库基线

| 项目 | 结果 |
| --- | --- |
| 主 solution | `OngekiFumenEditor.Avalonia.sln` |
| 共享项目 | `src/OngekiFumenEditor.Avalonia`, `net10.0` |
| Desktop | `src/OngekiFumenEditor.Avalonia.Desktop`；常规构建为 `net10.0-windows10.0.19041.0`，Native AOT profile 条件切换为 `net10.0`/`win-x64` |
| Browser | `src/OngekiFumenEditor.Avalonia.Browser`, `net10.0-browser` |
| SDK | .NET SDK `10.0.302`, MSBuild `18.6.11` |
| Avalonia | `11.3.10` |
| 产品测试项目 | 0 |
| 静态未测试扫描 | 已执行一次：950 个源文件、0 个产品测试文件 |
| AXAML | 56 个；`x:CompileBindings="False"` 0 个；`ReflectionBinding` 0 个 |
| Native AOT profile | `src/OngekiFumenEditor.Avalonia.Desktop/Properties/PublishProfiles/win-x64-aot.pubxml` |

`Dependencies` 内的上游测试不能当作产品测试计数。其中
`NAudio.BrowserAudioWorklet.Tests` 使用 NUnit 4.5.1，现有 5 个测试文件、69 个 `[Test]`，可作为浏览器播放器状态机的补充证据，但它没有覆盖本项目的平台工厂和 DI 注册。

## 测试框架建议

仓库产品层没有既有测试约定。依赖树中的 Avalonia Headless 示例已经采用 xUnit，因此建议产品测试沿用以下已验证组合，并保持 Headless 包与 Avalonia 完全同版本：

| 包 | 建议版本 | 原因 |
| --- | --- | --- |
| `Avalonia.Headless.XUnit` | `11.3.10` | 必须与产品 Avalonia 对齐 |
| `xunit` | `2.9.3` | 本仓库 Dock 依赖使用的版本 |
| `xunit.runner.visualstudio` | `3.1.5` | 本仓库 Dock 依赖使用的版本 |
| `Microsoft.NET.Test.Sdk` | `18.0.1` | 与当前 .NET 10/MSBuild 组合一致 |

不默认引入覆盖率包；不需要 Moq，关键依赖用小型显式 fake 即可。建议在 `tests/Directory.Packages.props` 中导入根 `Directory.Packages.props` 并启用 CPM，避免改变产品项目当前由 `src/Directory.Packages.props` 管理 NAudio 的边界。

## 可测试入口

### AXAML 与 Headless

- 56 个 AXAML 中有 52 个代码后置文件：1 个应用类和 51 个视图/控件。
- 51 个视图/控件都有无参构造入口；绝大多数构造函数只调用 `InitializeComponent()`，适合参数化构造烟测。
- 应用资源包含 DataGrid Fluent 样式、Dock、DialogHost、WindowManager，以及 4 个主题资源字典。应先加载测试应用资源，再构造视图并调用模板/布局。
- 产品 `OngekiFumenEditorApp.OnFrameworkInitializationCompleted()` 会初始化完整 DI、挂载键位路由并投递启动页。Headless 测试应用应复用产品 `App.axaml`，但覆写框架完成阶段，避免启动真实窗口工作流。
- UI 测试必须禁用并行执行，因为 Avalonia `Application.Current` 和 Dispatcher 是进程级状态。

### 键位与输入路由

- `KeyBindingDefinition` 的格式化/解析是纯逻辑，可覆盖空绑定、四类修饰键、非法表达式和往返。
- `KeyTrigger`、`MultiKeyGesture` 可用 `KeyEventArgs` 直接覆盖修饰键、序列中间状态、超时复位和错误键复位。
- `DefaultEditorKeyBindingRouter` 有 35 项强类型映射；构造函数自身会校验映射总数。
- 路由按 Normal/Batch/Global 层筛选；冲突时不执行；焦点位于 `WindowViewBase`、`TextBox`、`NumericUpDown`、`ComboBox`、`DataGrid` 或 `DataGridCell` 时必须让出输入。
- 路由类型和 `IKeyBindingManager` 是 internal，建议给测试程序集增加 `InternalsVisibleTo`，不要依赖反射调用私有事件处理器。Headless 的 `TopLevel.KeyPress(...)` 可验证真实冒泡路径。

### DataGrid 顺序

- `DataGridRowReorderOperations.Reorder<T>` 是公开、纯函数入口，覆盖 before/after、跨区间移动、多选保持源顺序、inside/no-target/no-moving 等边界。
- RenderControl 与 SoflanGroup 两个可拖动 DataGrid 都显式设置 `CanUserSortColumns="False"`；这是防止 UI 排序视图与持久顺序冲突的必要约束，应在视图烟测中断言。
- `FumenEditorRenderControlViewerViewModel.Reorder` 会重写连续的 `CurrentRenderOrder` 并接入撤销。
- Soflan 分组重排允许同组、跨组和拖入分组，且必须保持 Parent；其 ViewModel 构造依赖全局 IoC，宜在 Headless 测试应用 DI 初始化后做集成级测试。

### Skia 非空白输出

- `DefaultSkiaDrawingManagerImpl.CreateRenderControl()` 返回 Avalonia 托管的 `AvaloniaSkiaRenderControl`。
- `DefaultSkiaRenderContext.OnRender` 在 `ISkiaSharpApiLeaseFeature` 租约有效期间触发；测试可在回调中用 `CleanRender` 清为固定非透明颜色。
- Headless 提供 `CaptureRenderedFrame()`。建议将控件放入固定尺寸 Window，启动渲染、捕获帧、编码后用 Skia 解码，断言目标颜色像素数大于 0，而不只断言位图非 null。
- 首个实现步骤应验证 Headless renderer 暴露 Skia lease。如果得到 `NotSupportedException`，这属于 Headless 后端能力阻塞，需改用桌面离屏集成测试；不能退化为只测试纯 `SKSurface`，因为那不会覆盖产品租约路径。

### 音频工厂

- 共享契约是公开的 `Task<IWavePlayer> INAudioWavePlayerFactory.CreateDefaultWavePlayer()`。
- Desktop 工厂为 internal；配置 `Asio` 时创建 `AsioOut`，`Wasapi` 及旧 `WaveOut` 值走 20 ms、shared/event-sync/low-latency WASAPI。Native AOT 下 ASIO 回退 WASAPI。
- Desktop 方法会真实访问默认音频设备，普通 CI 不应直接调用。要做确定性的选择行为单测，需要把 WASAPI/ASIO 构造委托注入工厂；否则只能验证接口签名、DI 唯一注册和 AOT/常规构建。
- Browser 工厂为 internal，返回 `BrowserAudioWorkletPlayer(BrowserAudioLatencyProfile.Interactive)`。该项目仅目标 `net10.0-browser`，且真实构造在非浏览器环境会抛 `PlatformNotSupportedException`，不能伪装成普通 `net10.0` 单测。
- 浏览器播放器本体已有 fake bridge 测试；产品层仍需 Browser build/publish 和工厂注册证据。真实 AudioWorklet 初始化属于浏览器集成测试，不应在 Headless 单测中冒充完成。

### 谱面解析与格式化

- 公共入口是 `IFumenParserManager.GetDeserializer/GetSerializer` 及其默认异步文件方法。
- `.nyageki` 使用 `DefaultNyagekiFumenParser`；格式化器由 `IFumenSerializable` 注册。解析器会静默忽略未知命令，因此“未抛异常”不是充分断言。
- 往返测试应比较语义指纹：MetaInfo、BPM、节拍、Soflan、Lane/Beam、Tap/Hold、Bullet/Bell、Flick、LaneBlock、Comment 和 BulletPalette 的计数及关键有序字段；不比较字节文本。

## 外部语料盘点

默认目录：`C:\Users\mikir\Desktop\音寄谱\拉面`

建议环境变量：`ONGEKI_FUMEN_TEST_CORPUS_ROOT`。解析顺序为环境变量优先，其次使用上述本机默认目录。

| 类型 | 数量 | 处理方式 |
| --- | ---: | --- |
| `.nyageki` | 1 | 谱面解析、格式化、再解析 |
| `.nyagekiProj` | 1 | 单独验证相对引用和项目版本；不是谱面输入 |
| `.nyagekiScript` | 4 | 旧 C# 脚本，引用原项目/Caliburn；不得传给谱面解析器 |
| `.wav` | 1 | 项目附件，只验证引用存在，不在单测打开设备 |
| `.png` | 1 | 项目附件，只验证引用存在 |

`ramen.nyagekiProj` 版本为 `0.5.4`，相对引用 `ramen.nyageki` 和 `track.wav` 均存在。

`ramen.nyageki` 的关键语义计数包括 Tap 532、Hold 148、Bell 109、Bullet 88、Flick 74、LaneBlock 12、Soflan 11、MeterChange 12、CurveControlPoint 191。另有 **1 条 `SvgPrefab`**。根据本轮 11C 决策，SvgPrefab 解析/格式化链已删除；该命令会被当前解析器静默忽略。外部语料测试必须：

1. 扫描所有命令前缀；
2. 将 `SvgPrefab: 1` 记录为明确的决策排除项；
3. 对任何不在解析器集合且不在排除清单中的新命令失败；
4. 仅对受支持对象比较语义往返，不宣称 SvgPrefab 已保真。

## 失败分类

| 分类 | 建议标识 | 判定 |
| --- | --- | --- |
| 目录缺失 | `CORPUS_MISSING` | 根目录不存在，或显式执行外部语料组时无受支持谱面 |
| 不支持附件 | `CORPUS_IGNORED_ATTACHMENT` | script/wav/png 等已分类附件；不进入 parser |
| 决策排除命令 | `CORPUS_EXCLUDED_COMMAND` | 当前仅允许 `SvgPrefab: 1` |
| 解析失败 | `CORPUS_PARSE_FAILED:<relative-path>` | 已找到受支持谱面，但反序列化抛异常 |
| 往返不一致 | `CORPUS_ROUNDTRIP_MISMATCH:<relative-path>` | 受支持语义指纹不同 |

CI 必须始终运行仓库内最小 fixture。外部语料测试单独标记 `Category=ExternalCorpus`；只有显式执行该类别时目录缺失才失败，从而既保留本机完整回归，也不让个人目录成为 CI 唯一输入。

## 精确命令基线

```powershell
# 仅构建后续产品测试项目
dotnet build .\tests\OngekiFumenEditor.Avalonia.Tests\OngekiFumenEditor.Avalonia.Tests.csproj -c Debug -v:minimal

# 快速测试（排除机器外部语料）
dotnet test .\tests\OngekiFumenEditor.Avalonia.Tests\OngekiFumenEditor.Avalonia.Tests.csproj -c Debug --no-build --filter 'Category!=ExternalCorpus' -v:minimal

# 本机外部语料
$env:ONGEKI_FUMEN_TEST_CORPUS_ROOT = 'C:\Users\mikir\Desktop\音寄谱\拉面'
dotnet test .\tests\OngekiFumenEditor.Avalonia.Tests\OngekiFumenEditor.Avalonia.Tests.csproj -c Debug --filter 'Category=ExternalCorpus' -v:minimal

# BrowserAudioWorklet 上游状态机测试
dotnet test .\Dependencies\NAudio.BrowserAudioWorklet\tests\NAudio.BrowserAudioWorklet.Tests\NAudio.BrowserAudioWorklet.Tests.csproj -c Release -v:minimal

# Native AOT 发布证据
dotnet publish .\src\OngekiFumenEditor.Avalonia.Desktop\OngekiFumenEditor.Avalonia.Desktop.csproj -p:PublishProfile=win-x64-aot -v:minimal

# 最终工作区非增量构建和 solution 级发现
dotnet build .\OngekiFumenEditor.Avalonia.sln -c Release --no-incremental -v:minimal
dotnet test .\OngekiFumenEditor.Avalonia.sln -c Release --list-tests --no-build -v:minimal
dotnet test .\OngekiFumenEditor.Avalonia.sln -c Release --no-build -v:minimal
```

Native AOT 可执行文件启动烟测必须与 publish 分开记录：启动后若在限定时间内非零退出则失败；若仍存活则终止该次测试进程并视为“未立即崩溃”。不要让自动化测试无限等待 GUI。

## 验证后研究结论（2026-08-02 02:22 +08:00）

- Headless Avalonia + Skia 确实提供产品所需的 `ISkiaSharpApiLeaseFeature`；96x64 产品渲染路径像素测试通过，不需要退化到独立 `SKSurface`。
- Nyageki 首个显式 Soflan 必须替换 `OngekiFumen` 构造器哨兵；内置 fixture 的 round-trip 可杀死该逻辑变异。BPM 集合的 `Count` 属性带哨兵语义，测试语义数量应使用实际枚举项。
- 同一 TGrid 对象在解析/格式化后可能改变相对输出顺序；采用排序后的完整行集合和语义指纹双重比较，可忽略该无语义顺序而继续捕获字段变化、丢失与重复。
- `NAudio.BrowserAudioWorklet` 的原始 91 项测试在 NAudio 2.3 下通过并不足以证明主仓覆盖层。测试项目和生产项目统一 NAudio 3、加入主仓可追踪的 Span/数组测试兼容层后，91/91 在实际配置下通过，且子模块仍为干净固定提交。
- Browser AOT 产物包含托管程序集 AOT 分片、约 48.17 MB 原生 runtime WASM，以及 AudioWorklet 主脚本/processor。fake bridge 状态机与真实浏览器 AudioContext 是不同层级，后者仍需浏览器集成测试。

## 第七轮增量研究：18、19A、20B、21（2026-08-02 03:31 +08:00）

本轮复用既有 broad 源码扫描结果（950 个产品源文件、当时 0 个产品测试）和现有 101 项 solution 测试，不重复运行未测试源扫描。Release 非增量基线为 0 error、117 个既有 warning。

| 范围 | 已知实现缺口 | 需要的动态证据 |
| --- | --- | --- |
| Skia 波形 | 波形设置与 150px 宿主仍可见，但旧 `WaveformDrawing` partial 被编译排除且依赖旧渲染上下文 | 产品 Skia lease 输出非空像素；峰值映射、缩放和空音频边界；卸载/停止后取消工作 |
| WAV 偏移 | 零偏移仅复制，非零偏移固定失败 | PCM/IEEE-float 多声道帧边界；正补静音、负裁剪、过量裁剪；错误格式和目标原子性 |
| 20B 能力矩阵 | AOT 对 ASIO 静默回退；Browser 仍显示 Windows 后端和无效变速 | AOT/JIT/Browser 三组纯能力断言；旧配置归一化及显式回退；两个 publish profile 的依赖图和构建 |
| Svg* | 11C 删除了领域、解析写出、编辑 UI 与绘制链；真实语料含 1 条 `SvgPrefab` | 字符串/文件 prefab 强类型往返；Skia 绘制非空像素；相关视图构造；ramen 不再报告排除命令 |

现有测试项目 `tests/OngekiFumenEditor.Avalonia.Tests` 使用 xUnit、目标 `net10.0`，已通过 `ProjectReference` 引用核心项目，并具备 Avalonia Headless、Avalonia.Skia、SkiaSharp 与 NAudio.Core 依赖。平台入口实现若不能被核心测试直接引用，优先把能力选择提取为核心纯模型，再用项目构建和发布图验证平台接线；不通过反射绕过可见性或 AOT 约束。

## CommandLine 迁移增量研究（2026-08-02）

### 用户验收要求（原话）

- `1同意，但需要参考原项目的形式,是否能直接使用System.CommandLine进行迁移`
- `2同意，但大概架构需要和DefaultCommandExecutor相似，避免写死命令逻辑过程，也便于后面添加其他命令和代码`
- `3同意`
- `4同意`
- `5暂时不考虑`
- `ICommandModule改名成ICommandLineDefinition，对应Handler就是ICommandLineHandler`
- `ConvertCommandLineDefinition怎么获取它对应的Handler?`
- 对“Definition 通过构造函数注入 `ICommandLineHandler<FumenConvertOption>`”的结论：`同意，开始实现`

### 仓库与测试基线

- 旧项目直接引用 `System.CommandLine 2.0.0`，其 `DefaultCommandExecutor` 创建根命令、五个子命令和全局 `--verbose/-v`。
- Avalonia CLI 当前只是固定返回 1 的占位程序；现有全量 xUnit/VSTest 基线为 144/144。
- 当前 SDK 为 .NET 10.0.302；测试项目使用 `Microsoft.NET.Test.Sdk`、xUnit 和 Visual Studio runner，按 VSTest 执行。
- 已按测试流程执行一次静态未测试源扫描：2108 个源文件、166 个测试文件、1797 个未测试源、311 个配对源；该结果是命名/路径启发式，不是覆盖率数据。
- 本轮属于 broad、多文件改动，测试至少覆盖命令聚合、参数绑定、Definition/Handler 配对、业务退出码、真实转换和 JIT/AOT EXE。

### 实现约束

- `ConvertCommandLineDefinition` 仅持有 `System.CommandLine` 的命令/选项定义，并通过构造函数接收闭合泛型处理器；Handler 不接触 `ParseResult`。
- CLI 通过 `AddOngekiFumenEditorAvalonia()` 复用 Injectio 生成的核心注册，并提供自己的编译期注册扩展，不做程序集反射扫描。
- `FumenConverterWrapper`、`DefaultFumenConverter`、OGKR `CommandArgs` 和 `StandardizeFormat` 的现有路径包含静态 `IoC`；真实 headless convert 必须给这些路径增加显式依赖入口。
- GUI 兼容入口和已有注释保留；CLI 不创建 `Avalonia.Application`，不启动窗口或 Dispatcher。
- CI 工作流本轮不修改。

## CommandLine 迁移到 Desktop 增量研究（2026-08-02）

### 本轮边界与仓库快照

本轮测试范围是把 CommandLine 框架和平台命令迁入 Desktop 后的完整行为，不恢复 `acb`，也不改变 updater 的旧版高风险覆盖模型。研究期间主代理已开始并行迁移，因此以下内容区分两类事实：

- 迁移前基线：CommandLine 项目拥有框架和 `convert`，现有 Core 测试项目直接引用 CommandLine。
- 迁移目标：CommandLine 只保留调用 `DesktopCommandLineHost.Run(args)` 的薄入口；命令类型、DI 和平台实现由 Desktop 拥有。

当前测试基础如下：

| 项目 | 研究结果 |
| --- | --- |
| 测试框架 | xUnit `2.9.3`、`xunit.runner.visualstudio 3.1.5`、`Microsoft.NET.Test.Sdk 18.0.1` |
| Core 测试项目 | `tests/OngekiFumenEditor.Avalonia.Tests`，目标 `net10.0`，当前同时引用 Core 和 CommandLine |
| Avalonia 测试 | `Avalonia.Headless.XUnit 11.3.18`，程序集级禁用并行，Headless 使用 Skia |
| 断言/替身习惯 | 直接使用 xUnit `Assert`；不使用 Moq/FluentAssertions；用小型显式 fake、record 结果和独立临时目录 |
| 命名 | `Method_Condition_ExpectedResult`；测试类 `public sealed class ...Tests`；同步/异步按真实 API 选择 |
| 文件测试 | 使用 `Path.GetTempPath()` + GUID，`IDisposable` 清理；断言内容、状态和副作用，不只断言文件存在 |
| 当前 Avalonia | `11.3.18`；Desktop 普通 TFM 为 `net10.0-windows10.0.19041.0`，AOT 条件为 `net10.0` |

研究阶段按 broad-scope 要求执行了一次静态未测试源扫描。仓库没有根级 `global.json`，本机最高 SDK 为 .NET 10，无法满足 Roslyn 文件应用脚本的 .NET 11 前提，因此使用技能允许的 polyglot C# 分析器并启用 paired 输出。结果为 2,100 个源文件、161 个测试文件、425 个静态配对源、1,675 个未配对源、21 个 orphan 测试。该数字包含 `Dependencies`，仅是语法符号/名称启发式，不是行或分支覆盖率。

CommandLine 的 `DefaultCommandExecutor`、四个接口、convert Definition/Handler 被现有 CommandLine 测试静态配对。`CommandLineLogOutput`、`ConsoleCommandLineOutput`、注册扩展和 `OngekiFumenEditorDesktopApp` 未配对；扩展方法以实例语法调用会造成静态扫描假阴性。迁移后的实际测试位置必须遵循用户指定的新 Desktop 测试项目，而不是 polyglot 分析器给出的源码旁置 fallback 路径。

### 现有 18 项 CommandLine 基线

现有代码共有 15 个测试方法；三个 `[Theory]` 各有两个数据行，因此 VSTest 展开为 18 项。这 18 项必须迁入 Desktop 测试项目并保持或加强行为断言。

| 文件/方法 | 展开数 | 当前行为证据 |
| --- | ---: | --- |
| `DefaultCommandExecutorTests.RootHelp_ListsEveryRegisteredCommand` | 1 | 根帮助列出注册命令且 stderr 为空 |
| `DefaultCommandExecutorTests.Constructor_DuplicateCommandNamesIgnoringCase_Throws` | 1 | 命令名大小写不敏感去重 |
| `DefaultCommandExecutorTests.ExecuteAsync_VerbosityAliasAfterSubcommand_InvokesCommand` | 2 | `--verbose`、`-v` 均可递归解析并调用命令 |
| `DefaultCommandExecutorTests.UnknownCommand_DoesNotInvokeAnyRegisteredCommand` | 1 | 未知命令非零且不进入 Handler |
| `ConvertCommandLineDefinitionTests.Invoke_AllOptions_BindsStronglyTypedOptionsAndCallsInjectedHandler` | 1 | 路径和 `--standardize` 强类型绑定，Handler 返回码穿透 |
| `ConvertCommandLineDefinitionTests.Invoke_MissingRequiredOption_DoesNotCallHandler` | 2 | 分别缺少 input/output 时解析层拒绝 |
| `ConvertCommandLineDefinitionTests.Invoke_UnknownOption_DoesNotCallHandler` | 1 | 未知参数不进入 Handler |
| `ConvertCommandLineHandlerTests.HandleAsync_RelativePath_ReturnsLegacyPathExitCodeWithoutCallingService` | 1 | 相对路径 `-3`，业务服务零调用 |
| `ConvertCommandLineHandlerTests.HandleAsync_ServiceFailure_ReturnsLegacyConversionExitCodeAndWritesError` | 1 | 失败结果 `-4` 且 stderr 包含服务消息 |
| `ConvertCommandLineHandlerTests.HandleAsync_ServiceThrows_ReturnsLegacyConversionExitCodeAndWritesExceptionMessage` | 1 | 异常映射 `-4` 且输出异常消息 |
| `ConvertCommandLineHandlerTests.HandleAsync_ServiceSuccess_ReturnsZeroWithoutWritingError` | 1 | 成功为 0 且无错误输出 |
| `ConvertCommandIntegrationTests.AddOngekiFumenEditorCommandLine_RegistersDefinitionHandlerAndExecutorAsSingletons` | 1 | Injectio/DI 注册与 singleton 生命周期 |
| `ConvertCommandIntegrationTests.ConvertFixture_ToOgkr_ProducesReparseableChartWithPreservedContent` | 2 | 标准化开/关的真实 Nyageki -> OGKR 语义往返 |
| `ConvertCommandIntegrationTests.ConvertFixture_UnsupportedOutputFormat_ReturnsFailureWithoutCreatingOutput` | 1 | 不支持格式不创建输出或临时文件 |
| `ConvertCommandIntegrationTests.GenerateAsync_CancellationAfterConversion_PreservesExistingTargetAndRemovesTemporaryFile` | 1 | 取消保留旧目标并清理临时文件 |
| **合计** | **18** | **迁移后不得减少** |

DI 测试迁入 Desktop 后不能继续 `Assert.Single(definitions)`，因为完成范围应恰好发现 `convert`、`svg`、`jacket`、`updater` 四个 Definition。它应改为断言精确命令集合、四个闭合泛型 Handler 映射、单一 `DefaultFumenParserManager`，并明确断言没有 `acb`。

### 验收清单

以下清单直接来自交接计划；每项在 `plan.md` 中必须映射到具体测试或非行为验证证据。

- [ ] `CLD-01`：`OngekiFumenEditor.Avalonia.CommandLine` 保留为薄启动器，只引用 Desktop 并转发参数。
- [ ] `CLD-02`：命令框架、Definition、Handler 及平台命令实现全部迁移到 Desktop；Core 继续保留可复用领域服务。
- [ ] `CLD-03`：Gekimini App 增加可重写的 `ShouldCreateMainView`；命令行模式初始化 Avalonia、XAML、语言、主题、Core/Desktop DI 和日志，但不创建 `IMainView`、窗口、状态栏，不恢复/保存窗口状态。
- [ ] `CLD-04`：Core App 和 Desktop App 根据 `IsGUIMode` 跳过快捷键、Splash、XamlMcp、GUI 启动参数等逻辑。
- [ ] `CLD-05`：`DesktopCommandLineHost.Run(string[] args)` 使用完整 Classic Desktop 生命周期和 `ShutdownMode.OnExplicitShutdown`，命令完成后以命令退出码关闭。
- [ ] `CLD-06`：Desktop 接管 `System.CommandLine`、Injectio 注册和测试可见性；CommandLine 移除相关包和生成器，普通/AOT TFM 条件与 Desktop 保持一致。
- [ ] `CLD-07`：移除旧 Nyageki parser manager 的重复 Injectio 注册，统一解析到 `DefaultFumenParserManager`；`SvgGenerateOption.Duration` 可由 Desktop Handler 设置。
- [ ] `CLD-08`：`convert` 保持参数和实现；绝对路径错误 `-3`，转换失败 `-4`，并执行真实谱面 round-trip。
- [ ] `CLD-09`：`svg` 的 `--inputFile`、`--outputFile`、`--audioFile` 均为必填绝对路径；音频存在时用音频时长，否则按谱面末尾加 5 格计算。
- [ ] `CLD-10`：`svg` 默认值为 `40/800/1/Soflan/false`；`--png` 按 SVG 声明尺寸生成无尾随 SVG 数据、可由 ImageSharp 解码的 PNG；路径错误 `-1`，生成失败 `-2`。
- [ ] `CLD-11`：`jacket` 默认尺寸 `520x520` 和 `220x220`；`--outputWidthSmall`/`--outputHeightSmall` 正确绑定；路径错误 `-5`，生成失败 `-6`。
- [ ] `CLD-12`：jacket 使用真实模板生成大小两份 AssetBundle，更新并保留 `assets.bytes` 既有记录；模板和四个 DLL 由 Desktop 复制到 build/publish 输出。
- [ ] `CLD-13`：updater 使用可替换文件/进程环境，递归复制并排除 `.log/.xml/.dmp`，按 Desktop 进程名终止实例，使用 `.bak_*`，覆盖成功、`-1/-2/-3` 和旧版回滚行为。
- [ ] `CLD-14`：updater 成功后启动 `OngekiFumenEditor.Avalonia.Desktop.exe`，保留 `--notifySucess` 与 `--sourceVersion`，实际 EXE 冒烟只操作临时目录和无害 Desktop stub。
- [ ] `CLD-15`：本轮不注册 `acb`；旧 DLL、AOT 风险和恢复条件只记录在迁移文档。
- [ ] `CLD-16`：新增 Windows TFM Desktop 测试项目并迁移现有 18 项；原 Core 测试项目恢复为只引用 Core。
- [ ] `CLD-17`：覆盖命令发现、重复命令、帮助、未知参数、`--verbose`、必填参数、默认值、Handler 映射和退出码。
- [ ] `CLD-18`：分别构建并冒烟 CommandLine JIT/AOT、Desktop JIT/AOT，确认命令行模式不创建窗口且退出码穿透生命周期。
- [ ] `CLD-19`：执行全量测试与 `git diff --check`；CI 仍检查占位输出是已知失败，不能记录为通过。

### 可测试入口与所需 seam

| 范围 | 首选入口 | 强断言/替身 |
| --- | --- | --- |
| 生命周期 | `ShouldCreateMainView`、命令行 App、`DesktopCommandLineHost.Run` | 注册“被解析即失败”的 `IMainView`/状态栏/窗口设置 fake；记录语言、主题、Core/Desktop 服务和日志已初始化；子进程验证退出码 |
| 命令框架 | `DefaultCommandExecutor.RootCommand`、Desktop DI 扩展 | 捕获 stdout/stderr；精确比较四命令集合；每个闭合泛型 Handler 单独解析 |
| convert | `ConvertCommandLineDefinition/Handler`、`IFumenConvertService` | 迁移既有 fake 和 fixture；保留 18 项现有证据 |
| svg | SVG Definition/Handler、`IFumenParserManager`、音频时长 seam、`IPreviewSvgGenerator`、Desktop rasterizer | 捕获传入的 `Duration`；构造有明确尾部 TGrid 的谱面；解析 SVG XML；检查 PNG chunks、IEND 结束位置和 ImageSharp 尺寸 |
| jacket | Jacket Definition/Handler、`IJacketGenerateService` | 使用 ImageSharp 创建非退化临时图；用不同 small width/height 杀死反向绑定；读取两份 bundle 的纹理尺寸和 `assets.bytes` 二进制记录 |
| updater | Updater Definition/Handler、`IProgramUpdateService`、文件/进程环境接口 | fake 进程与可按操作序号抛错的文件环境；精确断言 kill/move/copy/delete/start 调用序列及遗留文件状态 |
| 发布/EXE | JIT/AOT publish 目录中的两个 EXE | 每个子进程设置超时，捕获 stdout/stderr/有符号退出码；枚举窗口；所有输入输出位于独立临时根 |

### 风险与实施约束

- Avalonia `Application.Current`、Dispatcher 和 Classic Desktop lifetime 是进程级状态；生命周期测试必须串行，真实 `DesktopCommandLineHost.Run` 最稳妥的自动化边界是子进程，不能在同一 xUnit 进程反复启动。
- updater 的复制失败回滚必须锁定旧行为：已复制且原先有目标的文件会阻止无覆盖 `File.Move(backup, target)`，因此可能留下新目标与 `.bak_*`。用户明确不授权事务增强，测试不能把理想化原子回滚写成预期。
- jacket 依赖 Windows x64 原生 DLL。真实集成测试和 AOT 冒烟需在 `win-x64` 执行；非 x64 环境应由测试项目 TFM/RID 约束，而不是静默 Skip。
- PNG 解码成功不足以排除旧版“PNG 后追加 SVG”缺陷；必须解析 PNG chunk 长度并断言 `IEND` 结束偏移等于文件长度。
- `Process.ExitCode` 应按有符号 `Int32` 比较 `-1..-6`；Shell 的 `$LASTEXITCODE`/CI 展示可能把负值映射成无符号值，记录时必须说明采集方式。
- CommandLine 薄项目的包/生成器移除、TFM 条件一致、资源复制和 CI 不改属于结构/发布证据，不应伪装成行为单元测试。

## Desktop acb 命令增量研究（2026-08-03）

### 有界目标与当前状态

本轮只为 Desktop `acb` 命令生成测试，允许写入 `.testagent` 和
`tests/OngekiFumenEditor.Avalonia.Desktop.Tests/CommandLine/Acb/`。生产代码、测试 csproj 和既有
CommandLine 测试均不在写入范围。

研究时工作树干净，Desktop 已有 Convert/Svg/Jacket/Updater 的 xUnit 测试约定，但新的 acb 生产 API
尚未出现。按 broad-scope 流程执行一次 polyglot C# 静态配对扫描：2,124 个源文件、178 个测试文件、
452 个静态配对源、1,672 个未配对源、24 个 orphan 测试。唯一命中 acb 的产品源是
`src/OngekiFumenEditor.Avalonia/Kernel/Audio/AcbConverter.cs`，当前未配对。该结果是包含 Dependencies 的
静态标识符启发式，不是行或分支覆盖率。

旧项目提供以下契约基线：

- 命令名 `acb`。
- 必填选项 `--musicId`、`--inputFile`、`--outputFolder`。
- `--previewBegin` 默认 60000，`--previewEnd` 默认 80000，单位毫秒。
- 相对输入/输出路径返回 `-7`；生成失败或异常返回 `-8`。
- music id 以四位补零，生成 `musicNNNN.acb`、`musicNNNN.awb` 和 `MusicSource.xml`；XML 中
  `Name/id`、`Name/str`、`dataName`、`acbFile/path`、`awbFile/path` 必须同步。
- `DereTore.Exchange.Archive.ACB.AcbFile` 可重新解析 ACB 并定位外置 AWB；`Afs2Archive` 可独立验证 AWB
  的 AFS2 结构和文件记录。

根据现有 Desktop 命名模式，预期生产类型位于
`OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Acb`：
`AcbGenerateOption`、`AcbCommandLineDefinition`、`AcbCommandLineHandler`、`IAcbGenerateService`、
`DefaultAcbGenerateService` 和结果模型。若最终名称或签名不同，测试只做机械适配，不降低行为断言。

### acb 验收清单（用户原话）

- [x] `ACB-01`：`命令注册/帮助`。
- [x] `ACB-02`：`必填参数与默认 previewBegin=60000/previewEnd=80000`。
- [x] `ACB-03`：`路径错误 -7`。
- [x] `ACB-04`：`生成失败 -8`。
- [x] `ACB-05`：`Handler 到可注入 IAcbGenerateService 映射`。
- [x] `ACB-06`：`真实临时 48k PCM WAV 生成 ACB/AWB/MusicSource.xml，并验证 XML musicId/文件名及 ACB/AWB 能由可用解析链重新打开`。
- [x] `ACB-07`：`Desktop引用项目https://github.com/NyagekiFumenProject/AcbGeneratorFuck, 不需要dll依赖`。

### 测试约定与强断言

- 沿用 xUnit、`public sealed class ...Tests`、`Method_Condition_ExpectedResult` 和显式 fake；不引入 mock 包。
- Definition 测试用 `RootCommand` + `InvocationConfiguration` 捕获 stdout/stderr，并验证 Handler 返回码穿透。
- Handler fake 必须记录收到的 option 对象、`CancellationToken` 和调用次数；路径失败断言 service 零调用。
- 真实 WAV 手工写 RIFF/WAVE、PCM 16-bit、双声道、48000 Hz 和非零正弦样本，避免只给空音频头。
- 真实生成断言三份文件存在且非空，并解析 XML 的全部关联字段；不能只断言文件名。
- ACB 用 `AcbFile.FromStream` 解析并断言 format/cue/external AWB/data stream；AWB 另用
  `Afs2Archive.Initialize()` 解析并断言至少一个有效 file record。
- JIT/AOT 依赖测试解析 Desktop csproj：两种模式都必须无条件引用官方子模块中的
  `src/AcbGeneratorFuck/AcbGeneratorFuck.csproj`，并明确不存在预编译 ACB `<Reference>`/`<Content>`
  或 Native Interop 源码。另检查 `.gitmodules` 的官方 URL，避免退回本机绝对路径或二进制文件。

### 风险

- 官方子模块当前固定到 `d00e636c`；测试需同时核对 gitlink 项目存在、URL 正确、项目引用无条件，
  以及旧 `AcbGeneratorFuck.aot.dll`/Interop 文件已从工作区移除。
- 旧生成器把 preview 时间直接写入模板，测试音频无需达到 80 秒；使用短而非退化的 48 kHz WAV 控制测试耗时。
- `AcbFile.FromFile` 的旧 API 对底层流所有权不直观；测试使用显式 `FileStream` + `FromStream`，确保临时目录可清理。
- 真实 HCA 编码可能较慢，集成测试只生成一段短音频并设置合理测试命令超时，不依赖真实音频设备或网络。

## FileDialog/SimpleFileSystem 迁移增量研究（2026-08-03）

### 验收清单

- [ ] `OpenFileAsync` 和 `SaveFileAsync` 返回 `Task<ISimpleFile>`；`OpenDirectoryAsync` 返回 `Task<ISimpleDirectory>`。
- [ ] Picker 取消继续返回空结果，选中的 `IStorageItem` 所有权可确定释放。
- [ ] Save 结果支持覆盖写，写后读取缓存和文件长度正确刷新。
- [ ] 12 个直接 Picker 调用点改用 `ISimpleFile`/`ISimpleDirectory`；Picker 文件内容不再经 `File.*` 重开。
- [ ] 命令行、项目 JSON、Win32 dump 和只接受本地路径的第三方 API 保留显式兼容边界。
- [ ] Core、Desktop、Browser 均可编译，相关 xUnit 和 solution 回归通过。

### 有界目标

- SimpleFileSystem 增加单文件 StorageProvider 包装和写流。
- 迁移谱面转换、WAV 调整、项目设置/快速打开、SVG 选择和三个目录设置。
- 字符串路径只保留为显示、持久化或明确的本地平台能力，不作为 Picker 内容 I/O 入口。

### FileDialog/SimpleFileSystem 最终边界与结论（2026-08-03）

- 实际直接 picker 调用点为 13 个。所有 picker 文件内容均通过 `ISimpleFile` 的
  `OpenRead`、`ReadAllBytes` 或事务式 `WriteAsync` 访问，没有把虚拟 `FullPath` 重新传入 `File.*`。
- 用户明确将范围限定为用户读写。CLI 路径原子写、项目 JSON、日志/崩溃转储、相邻音频自动发现、
  MP3/ACB 等只接受本地路径的第三方边界保持原实现；目录设置只接受可用的 `LocalPath`。
- standalone storage file 的 `FullPath` 使用 provider URI，避免不同目录同名文件被误判为同一文件；
  `FileName` 仅用于格式/扩展名选择。
- `ISimpleFile.WriteAsync` 在本地路径上同目录暂存，writer 成功且流关闭后才替换目标；writer 异常或取消
  会删除临时文件并保留目标。非本地 provider 无法由通用接口保证原子提交，但失败后会失效旧缓存并
  尽力刷新长度，避免继续返回与 provider 实际状态不一致的旧内容。
- runtime picker 文件由 ViewModel、项目模型或文档管理器明确持有；替换、取消、打开失败、窗口关闭和
  文档销毁路径均释放或转交所有权。

新增测试共 17 个方法、69 个直接 `Assert.*` 调用。断言覆盖 Equality、Boolean、Null、Exception、
Type、String、Collection、Comparison、Negative、State/Side-effect、Structural/Deep；Approximate 对
字节精确和格式契约不适用。零断言、仅平凡断言和自引用/恒真断言均为 0。

## 跨平台临时文件夹服务研究（2026-08-04）

### 有界目标

- Core：`Platforms/Services/FileSystem/Providers` 下的四个公共契约、名称校验、共享句柄与唯一命名、discard 后端。
- Desktop：`%TEMP%/NagekiFumenEditorTempFolder` 后端、根目录包含性校验、同目录事务写和 Injectio 单例注册。
- Browser：OPFS `temp` 根、启动前 JS 初始化、`Task<JSObject>` 字节解包、串行写及不可用时 discard 组合。
- 消费者：图片缓存、文件日志、救援文件、工程保存、ACB 解码，以及 Desktop ACB/Jacket 命令行服务。
- 文档与验证：更新 `docs/disk-io-audit.md`，删除 `TempFileHelper.cs`，完成 Core/Desktop 测试、Desktop AOT 与两种 Browser 发布/烟测。

### 仓库与测试约定

- 生产项目为 `net10.0`，Browser 为 `net10.0-browser`；现有 interop 已使用 `JSImport`、`Task<JSObject>`、`JSObject.GetPropertyAs*` 和同步 `MemoryView` 拷贝。
- Core 与 Desktop 测试均使用 xUnit 2.9.3、`public sealed class ...Tests`、显式 fake/临时目录和行为式方法名，不引入 mock 框架。
- Desktop 项目已通过 `InternalsVisibleTo` 暴露内部类型，可为后端注入隔离根目录；Browser 当前无测试项目，因此 JS 行为由发布后的 localhost 烟测覆盖。
- 2026-08-04 已按 `find-untested-sources` Roslyn 流程静态扫描一次：2935 个生产源、201 个测试源、412 个静态配对源。该结果仅是符号配对启发式，不代表行/分支覆盖。

### 验收清单

- [x] 唯一文件返回前已占位；固定名称复用；嵌套目录、读取、只读流、事务覆盖、追加、删除与清理行为正确。
- [x] writer 失败/取消保留旧内容，writer 成功后提交阶段忽略取消；所有名称逃逸与非法路径段被拒绝。
- [x] discard 写回调会执行但数据丢弃，查找永远为空，读取按不存在处理，删除和清理为空操作。
- [x] Desktop 默认根、`LocalPath`、跨实例保留、并发唯一命名、根包含性和清理不越界均有集成断言。
- [x] 图片缓存命中/未命中、日志追加、救援/工程序列化，以及 Desktop ACB/Jacket 的本地临时路径迁移有消费者证据。
- [x] Browser OPFS 创建、写入、读取、追加、删除及 `temp/logs/runtime` 日志通过独立 localhost origin 烟测。
- [x] `rg TempFileHelper` 无生产结果，Core/Desktop 全量 xUnit 与目标发布均成功。

### 风险与边界

- OPFS 没有通用本地路径；第三方路径 API 必须通过 `LocalPath` 显式拒绝 Browser，不能把相对路径伪装成磁盘路径。
- Browser 首版整文件缓冲，事务提交依赖 `FileSystemWritableFileStream.close()`；运行时配额和 I/O 错误必须继续上抛，只有初始化不可用才选择 discard。
- OPFS 数据和 Desktop 临时根跨启动保留，不做自动清空、过期或容量淘汰；测试必须只清理由自身注入的隔离根。
- `.testagent` 已含此前任务记录，本轮只追加独立章节，不覆盖旧内容。

### 最终验证发现

- Browser 的 OPFS 模块在 .NET 启动前初始化成功；标准 Release AOT 中覆盖、读取、追加、长度、删除和递归清理均通过独立 origin 烟测。
- 既有应用启动信息使用 `Microsoft.Extensions.Logging`，而原 `FileLogOutputWrapper` 只实现旧 `ILogOutput`。Browser 增加日志提供程序适配器后，真实启动日志会写入 OPFS `temp/logs/runtime`。
- 标准 Browser AOT 控制台没有应用 origin 的 JS 互操作错误；最终运行日志文件为 6447 字节，并包含设置、主题和 Shell 启动记录。
- OPFS 初始化错误分类用 Node ESM 夹具验证：`SecurityError` 返回不可用并进入 discard，`QuotaExceededError` 保持上抛。
- LLVM Browser 使用的 `Microsoft.DotNet.ILCompiler.LLVM 10.0.0-preview.2` 与当前 .NET 10/Avalonia JSExport 生成物不兼容。发布成功、OPFS JS 模块可独立读写，但应用启动在缺少 Avalonia JSExport wasm 导出时失败；该残余风险不归因于临时存储实现。

## EditorFileAccessContextSnapshot（2026-08-13）

### 有界目标

- 新增 `EditorFileAccessContext`，统一保存项目目录、附加目录及 Project/Fumen/Audio 文件角色。
- 新增用户指定结构的 `EditorFileAccessContextSnapshot`，通过异步书签保存与恢复和运行时上下文相互转换。
- Avalonia Storage Provider 的简单文件/目录包装器提供窄书签能力，普通 `ISimpleFile`/`ISimpleDirectory` 不承担平台 API。
- 最近记录的二进制 data 改为序列化快照；校验和打开从快照恢复上下文，不再读取旧 `FolderBookmark + ProjectFileLocator` 载荷。
- 保持 Fast Open 的 `ProjectFile` 可空语义；必需的 ProjectDirectory、FumenFile、AudioFile 书签为空时拒绝快照。

### 仓库与测试约定

- 生产与测试均为 SDK-style `net10.0`；测试使用 xUnit 与 `AvaloniaFact`。
- 书签 API 为异步 `SaveBookmarkAsync` / `OpenFileBookmarkAsync` / `OpenFolderBookmarkAsync`，因此转换 API必须异步。
- 当前 `EditorProjectDataModel` 拥有并释放运行时文件；本轮需把资源所有权迁移到上下文，兼容属性只转发角色，避免上下文与模型双重释放。

### 验收清单

- [ ] 上下文到快照保存全部目录和 Project/Fumen/Audio 文件书签，Fast Open 可省略 ProjectFileBookmark。
- [ ] 快照序列化为 UTF-8 JSON，可从 RecentRecordInfo data 精确反序列化。
- [ ] 快照恢复上下文时逐项恢复书签；任一必需项失败会释放此前取得的全部资源。
- [ ] 最近记录校验、打开、去重和更新均使用快照，不再依赖旧工程定位符载荷。
- [ ] 运行时所有权只有上下文一处；模型兼容属性与关闭链不造成双重释放。
- [ ] 聚焦测试和 Core 全量测试通过，`git diff --check` 无错误。

## OgkiFumenListBrowser Focused Inventory

- Production targets: simple-file scanner, relative locator guard, Unity AssetBundle jacket conversion, weak/temporary cover caching, window lifecycle, storage bookmarks, editor-context ownership, and compiled XAML view.
- Existing conventions: SDK-style net10.0 xUnit tests with `Avalonia.Headless.XUnit`; storage fixtures are wrapped through `AvaloniaStorageProviderFileSystemBuilder`.
- Acceptance evidence is recorded in the Ogki-specific test classes under `tests/OngekiFumenEditor.Avalonia.Tests/Modules/OgkiFumenListBrowser`.
