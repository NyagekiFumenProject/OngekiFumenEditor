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
