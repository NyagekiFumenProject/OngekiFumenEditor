# 15B 测试流水线状态

更新时间：2026-08-02 02:30 +08:00

## 当前阶段

Research、Plan、Implement 和 Verify 已完成。当前工作树的本项目测试、真实外部语料、上游音频状态机、三端构建、Desktop Native AOT 启动与 Browser AOT 发布均已取得通过证据；只保留明确的平台真实设备/真实浏览器集成边界。

## 已完成

- [x] 读取测试生成流水线、.NET 扩展和示例约束。
- [x] 确认主 solution、三个产品 TFM、CPM 边界和 Native AOT profile。
- [x] 确认产品测试项目为 0；消费既有一次未测试源扫描结果（950/0），未重复运行扫描。
- [x] 盘点 56 个 AXAML、51 个可构造视图/控件和应用资源启动副作用。
- [x] 定位 35 项编辑器键位强类型路由、焦点让出规则和纯键位匹配入口。
- [x] 定位 DataGrid 纯重排算法、两个禁止列排序的顺序编辑表和专业化 handler。
- [x] 定位 Avalonia Skia lease 渲染路径及 Headless 截帧 API。
- [x] 定位共享音频工厂契约、Desktop 硬件耦合和 Browser TFM 限制。
- [x] 盘点本机 ramen 语料 8 个文件，并区分谱面、项目、脚本、音频和图片。
- [x] 发现 ramen 谱面含 `SvgPrefab: 1`；按 11C 记录为决策排除命令，避免把静默忽略误报为完整解析成功。
- [x] 写出精确实施阶段、建议测试名、失败分类和验证命令。
- [x] 新建独立 `net10.0` Headless xUnit 测试项目和测试侧 CPM 文件，并由主流程注册到 solution。
- [x] 配置产品 `App.axaml` 资源加载、Headless 非软件绘图和 Avalonia Skia renderer。
- [x] 逐项枚举并核对 51/51 个 AXAML 视图/控件类型，实施构造、视觉树挂载和非零布局断言。
- [x] 验证应用转换器、编辑器主题和 Fluent 主题关键资源，并验证两个重排 DataGrid 的禁止排序及强类型行为挂载。
- [x] 实施键位格式/解析、多键序列、35 项编辑器动作映射、焦点让出、冲突、重复挂载、解绑和窗口关闭解绑测试。
- [x] 实施 5/6 项数据集的多选前后移动、边界修正和无效输入纯重排测试。
- [x] 实施产品 `DefaultSkiaDrawingManagerImpl` 到 Avalonia Skia lease 的 96x64 洋红帧像素测试。
- [x] 实施 `INAudioWavePlayerFactory.CreateDefaultWavePlayer()` 精确 `Task<IWavePlayer>` 公共契约测试。
- [x] 为内部键位路由测试增加最小 `InternalsVisibleTo` 与 3 个只读观察点。
- [x] 对测试项目 XML、51 项类型集合差异和所有新增文本 UTF-8 无 BOM 做静态检查。

## 实施与验证清单

- [x] 新建 Headless xUnit 测试项目并注册 solution。
- [x] 实施 AXAML/resource/view 构造测试。
- [x] 实施键位和输入路由测试。
- [x] 实施 DataGrid 纯顺序测试；专业化 ViewModel/undo 暂不在范围内。
- [x] 实施并运行 Skia 非空白像素测试，验证产品 Avalonia Skia lease 路径。
- [x] 实施内置 fixture 和外部 ramen 语料测试。
- [x] 实施共享音频接口测试；平台工厂由平台构建、上游 91 项状态机测试和 AOT 提供证据。
- [x] 运行 narrow tests、外部语料、上游音频测试、三平台构建和两端 Native AOT publish。
- [x] 运行 solution 级发现/测试。
- [x] 运行 test-gap-analysis 和 assertion-quality，并记录修复。

## 当前阻塞与风险

| 项目 | 状态 | 下一步 |
| --- | --- | --- |
| Desktop 工厂真实设备/ASIO | JIT 与 Native AOT 构建、原生 EXE 8 秒启动通过；自动测试不打开用户音频设备 | 后续在有 ASIO 驱动的机器做人工设备切换/播放；不为测试引入反射 seam |
| Browser 真实 AudioWorklet | NAudio 3 fake bridge 91/91 与 Browser AOT 通过；不执行真实 AudioContext | 后续使用真实浏览器自动化验证授权、首次启动和音频中断恢复 |
| Skia Headless lease | 产品路径像素测试已通过 | 无当前阻塞；真实 GPU 后端差异仍由平台冒烟承担 |
| 外部语料可移植性 | 本机目录 5/5 通过，内置 fixture 始终可运行 | CI 显式设置 `ONGEKI_FUMEN_TEST_CORPUS_ROOT` 并要求 0 skip |
| SvgPrefab 往返 | 按 11C 已删除，ramen 中有 1 条 | 继续作为决策排除边界，不宣称保真 |

## 验证记录

以下“未运行 dotnet/未修改 solution”只描述早期并行测试编写子任务，已经被后续主流程的动态验证取代，不能再视为当前状态。

2026-08-02 01:28 观察到并行协作者生成的 Debug 测试程序集，其中已包含 AXAML、输入、重排、Skia、音频与 Corpus 测试类型，说明当时源码曾通过编译；但没有取得该命令和退出摘要，而且 `TestApplication.cs` 此后又为 Headless DI 初始化发生修改，所以该产物只作线索，不能替代当前工作树的重新构建与测试。

2026-08-02 01:45 当前工作树执行 `dotnet build .\tests\OngekiFumenEditor.Avalonia.Tests\OngekiFumenEditor.Avalonia.Tests.csproj -c Debug --no-restore -v:minimal`，退出码 0、0 error、23 个核心既有警告加 SkiaSharp `NU1903`。随后执行 `dotnet test .\tests\OngekiFumenEditor.Avalonia.Tests\OngekiFumenEditor.Avalonia.Tests.csproj -c Debug --no-build --filter "Category!=ExternalCorpus" -v:minimal`，退出码 0，92/92 通过、0 失败、0 跳过。首轮动态测试促成产品修复：非 GUI App 不挂 DeveloperTools、Localize 多值格式迁移、对象池 DI、拖拽语义强调色、Nyageki Soflan 哨兵去重。

2026-08-02 02:22 最终验证：测试工程非增量 Debug 构建 0 error；solution 级发现列出 101 个展开用例，设置真实语料环境变量后 101/101 通过、0 失败/0 跳过（快速集 96/96，外部 ramen 5/5）。NAudio 3 主仓兼容覆盖配置下，上游 Microsoft Testing Platform/NUnit 状态机测试 91/91 通过。Core、Desktop、Browser 的 Debug/Release 均 0 error；Desktop `win-x64-aot` 发布成功，43,757,056 字节 EXE 存活 8 秒；Browser Release AOT 约 402 秒发布成功，当前清单引用 48,173,323 字节原生 WASM，并携带 AudioWorklet 主脚本与 processor 的原始/br/gz 资源。警告仍包括依赖漏洞、第三方裁剪/AOT、旧 JSON 反射序列化和 WASM P/Invoke 收集，未宣称零警告。

2026-08-02 02:30 Browser AOT 干净目录复核：`src/OngekiFumenEditor.Avalonia.Browser/bin/Release/net10.0-browser/publish-clean-20260802-0225` 中只有当前运行时与 NAudio 两份未压缩哈希 WASM，无旧哈希副本；两份 WASM 和 AudioWorklet 主脚本/processor 的原始、br、gz 版本均齐全。标准增量目录可能保留旧构建生成物，交付验收以该干净目录为准。

## 质量复核

- test-gap-analysis：对本轮高风险逻辑实证注入 3 类变异。DataGrid `After` 边界和 Nyageki Soflan 哨兵变异被原测试杀死；键位冲突阈值 `> 1`→`> 2` 首次存活，已增加 Error 日志分支断言，复注入后被杀死。最终抽样 3/3 被杀死，所有临时变异均已恢复且全套测试再次为绿。
- assertion-quality：当前 42 个测试方法展开为 101 例、约 234 个源码 `Assert.*` 调用点；零断言、仅平凡断言和自引用/恒真断言均为 0。覆盖 12 类断言中的 11 类，仅缺当前没有必要强求的 Approximate。新增两个单类别本地化输出测试属于精确转换器行为断言，不视为浅层烟测。
- 剩余测试边界：Desktop 真实音频设备/ASIO 和 Browser 真实 AudioContext/AudioWorklet 需要平台集成环境；当前自动化不伪装覆盖这两项。

## Requirement | Evidence

| Requirement | Evidence |
| --- | --- |
| AXAML/resource/view construction | 51/51 可构造视图逐一构造、挂载、布局；应用主题、转换器和拖拽资源检查；活动编译绑定豁免静态计数为 0 |
| key commands/input routes | 35 项动作映射、焦点让出、冲突、重复 Attach/Detach、窗口关闭解绑；冲突阈值存活变异已修复并杀死 |
| DataGrid sorting/reordering | 两表禁止列排序并挂强类型行为；6 个纯重排用例；After 边界变异被杀死 |
| audio factory platform contract | 精确 `Task<IWavePlayer>` 契约；Desktop/Browser 构建；NAudio 3 上游 91/91；两端 AOT 成功 |
| Skia nonblank | 产品 Avalonia Skia lease 输出 96x64 洋红帧并做像素数量断言 |
| Desktop Native AOT smoke | `win-x64-aot` 发布成功；43,757,056 字节 EXE 存活 8 秒 |
| Browser Native AOT/assets | 402 秒发布成功；干净发布目录只有当前运行时与 NAudio 两份哈希 WASM、无旧哈希副本；AudioWorklet JS/processor 原始及 br/gz 资源齐全 |
| corpus discovery/parse/format | 内置 fixture + 本机 ramen；真实语料 5/5，完整 solution 101/101；`SvgPrefab: 1` 显式排除 |

## 第七轮状态（2026-08-02 03:31 +08:00）

当前阶段重新进入 Implement。第六轮“`SvgPrefab: 1` 显式排除”结论已由用户第 21 项覆盖，不能再作为最终通过证据；本轮完成后必须替换为解析、格式化、绘制及真实语料保真证据。

- [x] 登记 18 Skia 完整波形、19A、20B、21 Svg* 完整迁移。
- [x] 读取 .NET 测试扩展，确认现有 xUnit 项目已引用核心程序集及所需 Headless/Skia/NAudio 包。
- [x] 完成 Release 非增量基线：0 error、117 个既有 warning。
- [x] 完成波形产品实现与测试（Release 窄测 11/11）。
- [x] 完成 WAV 偏移服务、ViewModel 接线与测试（Release 窄测 12/12）。
- [x] 完成音频能力矩阵、编译绑定 UI 和双 Windows publish profile。
- [x] 完成 Svg* 领域/格式/UI/Skia 功能链与真实语料测试。
- [x] 完成 solution 测试、三端构建、AOT/JIT 发布和质量复核。

2026-08-02 04:00 合并态阶段验证：18、19A、20B 产品代码及其测试源码均已落盘；共享核心执行 Release 非增量构建为 0 error、110 个 warning。该结果只证明当前接口、DI 生成和 AXAML 编译绑定可共同编译，尚未替代新增测试执行、Desktop 两种发布、Browser 构建及第 21 项 Svg* 验收。

2026-08-02 04:10 动态验证：波形首轮 27 项合并窄测中 26 项通过，非空像素用例捕获到回调执行但产品截帧全黑；修正为 lease 内 Skia 直接绘制后波形组 11/11 通过。WAV 偏移/事务组随后 12/12 通过，包含新增 DI 单例解析。该失败未通过放宽断言规避。

2026-08-02 04:15 平台阶段验证：`win-x64-jit` 首轮因平台入口缺少显式 `System` using 失败，修复后发布成功，产物含 ASIO/WinMM/WASAPI/SoundTouch；Browser Release 非增量构建 0 error、117 warning。20B 仍待最终 AOT/WASAPI 发布、AOT 产物排除 ASIO、两个 Windows 包启动及 Svg* 合并后重验，因此清单暂不标完成。

2026-08-02 06:05 第七轮收尾（kimi-code 接管 codex 会话 019fbc61 完成最终验证）：变异恢复后三项定向测试 3/3 回绿，含 ramen 语料全量 143/143；solution Release 非增量复建 0 error。`win-x64-aot` 最终冒烟暴露真实启动回归：`ToolViewModelTypeCollectedActivator` 因源生成器对跨文件 partial 的 `AudioPlayerToolViewerViewModel` 重复收集而在静态构造抛出重复键异常；此前全量测试未覆盖该路径（`TestApplication` 直接继承 `App`，不经过 `OngekiFumenEditorApp.RegisterServices`）。修复生成器按 `FullClassName` 去重，并新增 `ToolViewModelTypeCollectedActivatorTests`（修复前红、修复后绿）。最终证据：全量 144/144、0 跳过；`win-x64-aot` EXE 53,153,792 字节 10 秒存活（完整启动）；`win-x64-jit` 产物含 ASIO/WinMM/WASAPI/SoundTouch 且 EXE 10 秒存活；Browser AOT 发布成功，`dotnet.js` 仅引用本次 56,496,934 字节原生 WASM，AudioWorklet 主脚本/processor 原始及 br/gz 齐全。第七轮四项决策均有行为测试与构建/发布证据，清单标记完成；真实音频设备/ASIO 与真实浏览器 AudioContext 仍维持平台人工验收边界。

## CommandLine 第一阶段状态（2026-08-02）

Research、Plan、Implement 和 Verify 已完成。实现采用 `System.CommandLine 2.0.0`、
Injectio 编译期集合注册和强类型 Definition/Handler 配对；`Program.Main` 不包含具体命令逻辑。

- [x] 实现 `ICommandExecutor`、`DefaultCommandExecutor`、`ICommandLineDefinition`、
  `ICommandLineHandler` 与 `ICommandLineHandler<TOptions>`。
- [x] `ConvertCommandLineDefinition` 构造函数注入
  `ICommandLineHandler<FumenConvertOption>`，显式完成选项绑定。
- [x] 提取可注入的 `IFumenConvertService`，保留 GUI 静态兼容入口。
- [x] 移除 OGKR 解析、日志和对象池在 CLI 路径上对 Avalonia 全局 IoC 的依赖。
- [x] 转换输出采用同目录临时文件和原子替换；失败、取消均清理临时文件。
- [x] 新增 18 项 CommandLine 单元/集成测试；Release 全量 162/162 通过，0 失败、0 跳过。
- [x] JIT self-contained 与 Native AOT 发布成功。
- [x] 两种 EXE 各执行 10 组真实参数，共 20/20 组结果符合预期。
- [x] 本轮未修改 CI 工作流。

真实 EXE 冒烟覆盖：根帮助、版本、`convert --help`、未知命令、缺失必填参数、相对路径、
Nyageki→OGKR、OGKR→Nyageki、`--standardize` 和不支持输出格式。JIT/AOT 分别生成一致的
635 字节 OGKR、762 字节 Nyageki 与 609 字节规范化 OGKR；无 `.tmp` 残留，失败用例不创建目标文件。

| Requirement（用户原话） | Evidence |
|---|---|
| `需要参考原项目的形式,是否能直接使用System.CommandLine进行迁移` | 使用旧项目相同的 `System.CommandLine 2.0.0`，保留 `convert` 参数、根 verbose 与旧版业务退出码；JIT/AOT 均通过 |
| `架构需要和DefaultCommandExecutor相似，避免写死命令逻辑过程` | 根执行器只聚合 `IEnumerable<ICommandLineDefinition>`；`Program.Main` 不出现具体命令名；重复命令名有测试 |
| `也便于后面添加其他命令和代码` | 新命令只需增加 Definition、闭合泛型 Handler 与业务服务，Injectio 自动加入集合；无需修改入口和执行器 |
| `ICommandModule改名成ICommandLineDefinition` | 已使用 `ICommandLineDefinition`，仓库没有新增 `ICommandModule` |
| `对应Handler就是ICommandLineHandler` | 已实现非泛型标记接口与 `ICommandLineHandler<TOptions>`，Handler 不依赖 `ParseResult` |
| `ConvertCommandLineDefinition怎么获取它对应的Handler?` | 构造函数直接注入 `ICommandLineHandler<FumenConvertOption>`；编译期类型映射和 DI 单例解析均有测试 |
| `测试和检查commandline.exe和它的命令功能是否全部正常, 允许冒烟测试` | Release 全量 162/162；JIT/AOT 发布成功；两种真实 EXE 共 20/20 次冒烟通过 |
| `5暂时不考虑` | `.github/workflows/BuildProgram.yml` 未改动 |

剩余风险：CommandLine 宿主不启动 Avalonia UI，但当前复用完整的
`AddOngekiFumenEditorAvalonia()` 注册，扩大了 Native AOT 可达面并保留共享核心既有裁剪警告。
后续用 Injectio tags 或独立编译期注册模块缩小服务集合；下一业务阶段为 `svg`。

## CommandLine 迁移到 Desktop 最终状态（2026-08-03）

本节覆盖上方“第一阶段/下一阶段为 svg”的旧状态。Research、Plan、Implement、Verify 已完成；
CommandLine 现在通过完整 Avalonia Classic Desktop 生命周期运行，但 `ShouldCreateMainView=false`，
因此仍初始化 XAML、主题、语言、Core/Desktop DI 与日志，同时不创建 GUI。

- [x] CommandLine 变为只引用 Desktop 并转发参数的薄启动器。
- [x] Desktop 接管命令框架、Injectio、`System.CommandLine`、测试可见性和四个命令。
- [x] 完成 `convert`、`svg`、`jacket`、`updater`；根命令明确不注册 `acb`。
- [x] Core 测试恢复为只引用 Core；新增 Windows TFM Desktop 测试和 Updater Stub。
- [x] Stub 加入 solution，Release solution 构建不依赖旧的 Release 产物。
- [x] CommandLine/Desktop 的 JIT 与 Native AOT 全部发布并冒烟。
- [x] CI workflow 未修改；旧占位检查明确记录为已知失败，未误记为通过。

### 最终动态验证

- Desktop CommandLine 测试：74/74，0 失败、0 跳过。
- Core 测试：144/144，0 失败、0 跳过。
- Release solution：218/218，0 失败、0 跳过；Stub 明确构建到 Release。
- CommandLine JIT/AOT：各 7 组最终冒烟，共 14/14；帮助、`-3`、convert、SVG、PNG、
  Jacket、Updater 均符合预期，全部未创建独立 GUI 窗口。
- Desktop JIT/AOT：分别存活 8 秒并创建主窗口，随后只终止本轮启动的精确 PID。
- 四种 publish 均包含 Jacket 模板和依赖；AOT `TexturePlugin.dll` 与源 DLL SHA-256 一致。

### test-gap-analysis

对迁移范围实证注入 10 个高风险变异。首轮 8 个被杀死，2 个存活：

1. Updater 过滤测试创建了排除文件，但 fake 枚举未返回这些文件。
2. Desktop 进程名断言与生产常量自引用。

修复 fixture 和字面量契约断言后重新注入，最终 10/10 全部被杀死。每个变异均立即恢复；
最终全量测试为绿，工作树没有残留变异。

### assertion-quality

- 18 个测试文件，53 个源测试方法，74 个展开用例。
- 277 个 `Assert.*` 调用，平均 5.23 个/源测试方法。
- 零断言、仅平凡断言、自引用/恒真断言均为 0。
- 28 个方法含负向断言，2 个含异常断言，28 个含结构/深层断言。
- 使用 12 类断言中的 11 类；仅缺不适用于当前精确整数尺寸/退出码契约的 Approximate。

### 最终剩余边界

- Updater 的目录覆盖和旧版部分回滚状态是用户明确要求保留的兼容风险。
- `acb` 等待旧二进制的源码/许可、.NET 10、裁剪和 Native AOT 证据。
- `NU1903`、`NU1507` 和共享 Core/Avalonia 的裁剪/AOT 告警仍存在，不宣称零警告。
- 普通/AOT TFM 共用 `obj/project.assets.json`；切换后使用 `--no-restore` 可能触发 NETSDK1005，
  需恢复对应 TFM。本轮最终 solution 已重新 restore 普通 TFM。

## Desktop acb 命令测试状态（2026-08-03 04:20 +08:00）

本节覆盖上方“`acb` 等待旧二进制”的旧边界。Acb 命令测试的 Research、Plan、Implement 和 Verify
均已完成；本轮只修改 `.testagent` 与 `tests/OngekiFumenEditor.Avalonia.Desktop.Tests/CommandLine/Acb/`。

### 实施内容

- 6 个 Acb 测试文件，15 个源测试方法、21 个展开用例；另有既有根注册测试共同覆盖五命令集合。
- Definition 覆盖帮助、三项必填参数、默认 `60000/80000`、显式预览值和 Handler 返回码穿透。
- Handler 覆盖输入/输出相对路径分别返回 `-7` 且 service 零调用，业务失败和异常均返回 `-8`，
  成功映射同一 options/token，并透传已取消的 `OperationCanceledException`。
- 默认 service 额外覆盖缺失输入、负 musicId、空输出目录及预取消，不生成部分 ACB/AWB/XML。
- 集成测试手工写入 48 kHz、16-bit、双声道、非零正弦 PCM WAV，真实生成 `music0427.acb`、
  `music0427.awb`、`MusicSource.xml`；精确验证 XML 关联字段，并通过运行时携带的 DereTore
  `AcbFile`/`Afs2Archive` 解析链重新打开两个容器和 HCA 数据流。
- 项目图测试验证官方 `AcbGeneratorFuck` 子模块 URL、Desktop 无条件 `<ProjectReference>`、
  不存在预编译 ACB `<Reference>`/`<Content>`，并确认 service 不再包含原生 Interop 分支。

### 动态验证

- `dotnet test ...Desktop.Tests.csproj -c Debug --filter "FullyQualifiedName~Acb" -v:minimal`：22/22，
  其中 Acb 目录 21 项，既有根注册用例 1 项。
- `dotnet test ...Desktop.Tests.csproj -c Debug -v:minimal`：96/96。
- `dotnet test .\OngekiFumenEditor.Avalonia.sln -c Debug -v:minimal`：Core 144/144、Desktop 96/96，
  合计 240/240，0 失败、0 跳过。
- CommandLine JIT 与 NativeAOT EXE 均使用真实 48 kHz WAV 和中文输出目录执行 `acb`，分别生成
  ACB/AWB/XML 并返回 0；两条路径都直接调用子模块源码中的 `Generator.Generate`。
- CommandLine 与 Desktop 的 win-x64 NativeAOT 发布均成功，生成代码直接链接进主 EXE，发布目录
  没有 `AcbGeneratorFuck*.dll`。JIT 发布中的托管程序集由同一子模块项目现场构建。
- 仍有既有 `NU1903`（SkiaSharp）与 `NU1507`（多包源）告警；本轮未把它们误记为通过或新增问题。

### test-gap-analysis

用户明确禁止修改生产代码，因此没有临时注入生产变异，也不报告伪造的实证 mutation score。
静态伪变异复核促成三项补强：service 三条前置校验的精确结果/无产物断言、Handler 取消透传、
以及官方子模块 URL/无条件项目引用/无 DLL binding 的精确断言。验收关键点（必填、默认值、
`-7/-8` 字面量、service 零/一次调用、options/token 映射、XML 字段和容器可解析性）均有直接断言。

Debug xUnit 验证项目图和托管生成链；主流程另用真实 NativeAOT EXE 完成发布与命令冒烟，证明
项目引用的可达生成闭包可以直接 AOT 链接。剩余静态边界是硬编码旧生成器的“返回 false/生成不足
三文件/复制中途失败”内部支路不可替换；Handler 的统一 `-8` 映射已由可注入 fake 覆盖。

### assertion-quality

- 104 个源码 `Assert.*` 调用点，平均 6.93 个/源测试方法；辅助断言按源码调用点计一次。
- 零断言、仅平凡断言、自引用/恒真断言均为 0；取消路径有 2 个显式异常断言。
- 使用 12 类断言中的 11 类：Equality、Boolean、Null、Exception、Type、String、Collection、
  Comparison、Negative、State/Side-effect、Structural/Deep；仅缺不适用于精确整数/文件格式契约的 Approximate。
- 真实集成用例同时约束成功标志、文件存在/长度、XML 深层字段、ACB/AWB 结构、HCA 数据流长度和文件范围，
  不是只检查“文件存在”的浅层烟测。

### Requirement | Evidence

| Requirement（用户原话） | Evidence |
| --- | --- |
| `命令注册/帮助` | `DesktopRegistration_RootAndCommandHelpDiscoverAcbWithExpectedOptions`; 根五命令注册测试 |
| `必填参数与默认 previewBegin=60000/previewEnd=80000` | `Invoke_MissingEachRequiredAcbOption_DoesNotCallHandler`; `Invoke_DefaultOptions_UsesLegacyPreviewRange` |
| `路径错误 -7` | `HandleAsync_AnyRelativeAcbPath_ReturnsMinusSevenWithoutCallingService` 两组路径 |
| `生成失败 -8` | `HandleAsync_ServiceFailureOrException_ReturnsMinusEightAndWritesReason` 两组失败形态 |
| `Handler 到可注入 IAcbGenerateService 映射` | 成功 options/token 映射、失败/异常、取消透传及 DI singleton 测试 |
| `真实临时 48k PCM WAV 生成 ACB/AWB/MusicSource.xml，并验证 XML musicId/文件名及 ACB/AWB 能由可用解析链重新打开` | `Generate_48KhzPcmWave_CreatesAcbAwbAndMusicSourceThatReopen` |
| `Desktop引用项目https://github.com/NyagekiFumenProject/AcbGeneratorFuck, 不需要dll依赖` | `DesktopProject_ReferencesAcbGeneratorProjectWithoutDllBindings`; `AcbGeneratorSubmodule_UsesOfficialRepository`; `AcbGenerateService_UsesManagedProjectWithoutNativeInterop` |

## CommandLine CI 验证状态（2026-08-03）

- `.github/workflows/BuildProgram.yml` 已运行 Avalonia solution 测试，不再保留“没有可用命令”的
  占位断言。
- CommandLine 新增独立 `win-x64-jit` profile；workflow 不再把 AOT CommandLine 复制进 JIT 包。
- `.github/scripts/Test-AvaloniaPackage.ps1` 对两种最终包检查必需/禁止文件，并运行根帮助、
  `convert --help`、`acb --help`。
- Release solution 本地复现为 Core 144/144、Desktop 96/96；CommandLine/Desktop JIT 与 NativeAOT
  四次发布成功，AOT/JIT 合包验证均通过。
- 验证脚本负向复测确认空包会失败；workflow YAML 已由 YamlDotNet 成功解析。

| Requirement（用户原话） | Evidence |
| --- | --- |
| `处理CI` | `BuildProgram.yml` 的全量测试、独立 JIT/AOT CommandLine 发布与两次 `Test-AvaloniaPackage.ps1`；本地 Release 240/240、AOT/JIT 最终包验证通过 |

## FileDialog/SimpleFileSystem 用户读写迁移状态（2026-08-03）

本轮实现完成，范围按用户补充要求限定为文件/目录 picker 产生的用户读写。其他 CLI、项目 JSON、
日志/缓存、自动扫描及只接受本地路径的第三方 I/O 未做全仓迁移。

### 动态验证

- Core 聚焦测试：42/42，通过 SimpleFileSystem、WAV、事务和 SVG 往返用例。
- Core 全量测试：174/174；首次全量运行有一个未改动的路径版 WAV `File.Move` 瞬时
  `UnauthorizedAccessException`，单测立即复现通过，随后全量重跑 174/174。
- Desktop 全量测试：97/97；非本地谱面转换事务写入并重新解析用例单独 1/1。
- Browser Debug 构建：0 error；保留既有 `NU1507`、`NU1903`、nullable/WASM 警告。
- `dotnet build OngekiFumenEditor.Avalonia.sln -c Debug --no-incremental --no-restore`：0 error，
  83 个既有警告。
- `git diff --check`：通过。

### test-gap-analysis

对 5 个高风险点逐一注入真实变异并运行最窄覆盖测试：standalone provider 身份退化、删除本地原子
提交、转换器退回 `OpenWrite`、WAV 仅按同名判同文件、Nyageki 丢弃非本地 SVG 内容。5/5 均被测试
杀死；每个变异均立即恢复，最终全量测试和 diff 检查为绿色。未对范围外 I/O 注入或修改。

### assertion-quality

- 新增 17 个测试方法，69 个直接 `Assert.*` 调用，平均 4.06 个/测试方法。
- 零断言、仅平凡断言、自引用/恒真断言均为 0。
- 11/12 类断言得到使用；仅 Approximate 不适用于本轮精确字节、路径和文件格式契约。
- 用例同时验证输出内容、重新解析、缓存/长度、异常/取消后原文件、临时文件清理、流关闭时序和所有权，
  不是仅检查“调用成功”的浅层烟测。

### Requirement | Evidence

| Requirement（用户原话） | Evidence |
| --- | --- |
| `FileDialogHelper的OpenFileAsync返回对象改成Task<ISimpleFile>, 同理其他接口也是` | 三个公开签名、取消语义、storage item 所有权和 12 个 SimpleFileSystem 测试 |
| `然后那些使用这些方法的内容也全都改造成SimpleFileSystem相关API操作。` | 13 个 picker 调用点；谱面/WAV/SVG/目录设置使用 `ISimpleFile`/`ISimpleDirectory`；Core 174/174、Desktop 97/97 |
| `对于改造仅限于用户读写相关范畴，其他读写操作不要改动` | CLI、项目 JSON、日志/缓存、自动关联扫描及第三方本地路径边界保留；静态审计未发现 picker `FullPath` 回流到 `File.*` |

## 跨平台临时文件夹服务状态（2026-08-04）

### 动态验证

- Core 全量：219/219；Desktop 全量：105/105；合计 324/324，0 失败、0 跳过。
- Desktop 第一次 `--no-restore` 尝试因先前发布切换了共享 assets TFM 而触发 `NETSDK1005`；按 Desktop 测试项目重新还原后 105/105 通过，不属于测试用例失败。
- Desktop win-x64 Native AOT 最终发布成功，EXE 69,832,704 字节；启动后精确进程存活 8 秒，再由测试清理。
- 标准 Browser Release AOT 最终发布成功；独立 `localhost:13048` origin 的 OPFS 覆盖、读取、追加、长度、删除、递归清理全部通过。
- Browser 实际启动日志写入 `temp/logs/runtime/20260804_140725...log`，烟测大小 6447 字节，包含应用设置/主题/Shell 日志；应用 origin 控制台无错误或警告。
- Node ESM 初始化错误夹具通过：安全上下文 `SecurityError` 选择 discard，配额 `QuotaExceededError` 不被吞掉并继续上抛。
- LLVM Browser 最终发布成功，OPFS JS 模块可读写并清理；当前 preview.2 LLVM 工具链缺少 Avalonia JSExport wasm 导出，.NET 应用无法启动，作为既有工具链残余风险保留。

### 覆盖与质量

- 公共契约测试覆盖唯一占位、固定复用、嵌套目录、全部读取/只读流、事务覆盖、追加、失败与取消回滚、提交后忽略取消、删除、清理及名称逃逸。
- discard 测试覆盖生产回调执行后丢弃、查找为空、直接读取不存在、删除和清理为空操作。
- Desktop 集成覆盖默认根、隔离根注入、`LocalPath`、跨实例保留、并发唯一、根包含性及清理不越界。
- 消费者覆盖图片缓存命中/未命中、日志追加、救援工程序列化、ACB/Jacket 临时本地路径以及缺少 `LocalPath` 时的明确失败。
- 保留既有 `NU1903`、`NU1507`、裁剪/AOT 分析告警；未把它们误记为本轮零警告。

### Requirement | Evidence

| Requirement（用户原话） | Evidence |
| --- | --- |
| `Desktop 使用 %TEMP%\NagekiFumenEditorTempFolder；Browser 使用 OPFS 根目录下的 temp。` | Desktop 后端集成测试；标准 Browser AOT 独立 origin OPFS 烟测 |
| `内容跨启动保留，仅在显式调用清理 API 时删除。` | 跨 provider 实例保留测试、显式 `ClearAsync`/删除测试及审计文档 |
| `删除 TempFileHelper，一次性迁移全部生产调用。` | 文件删除；`src/tests` C# 生产扫描无引用；图片、日志、救援、工程、ACB/Jacket 消费者测试 |
| `写入回调失败或取消时保留原内容，回调成功后完成提交阶段不再响应取消。` | `TemporaryFolderProviderContractTests` 的失败、预取消、回调取消和提交阶段取消用例 |
| `OPFS 缺失或初始化因安全上下文/权限失败时，切换到 discard 后端。` | `DiscardTemporaryFolderProviderTests` 与 Browser 组合提供程序实现 |
| `发布并短启动 Desktop Native AOT。` | 最终 win-x64 AOT EXE 短启动存活 8 秒 |
| `发布标准 Browser Release AOT 和 LLVM Browser` | 两种最终发布均成功；标准 AOT 全部运行烟测通过，LLVM 运行受明确记录的 preview 工具链 JSExport 缺陷阻断 |
