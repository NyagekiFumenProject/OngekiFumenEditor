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
