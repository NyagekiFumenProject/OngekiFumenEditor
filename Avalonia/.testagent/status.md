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
