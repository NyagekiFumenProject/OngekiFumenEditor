# WPF → Avalonia 迁移状态报告

- **检查日期**：2026-07-31
- **文档更新日期**：2026-07-31（第二次更新）
- **检查基线提交**：`32909b6b`（分支 `avalonia`）
- **验证命令**：`dotnet build OngekiFumenEditor.Avalonia.sln --no-restore -t:Rebuild -m:1 -v:minimal`
- **构建结果**：失败。C# 编译已 **0 错误**；完整重建在 Avalonia XAML 编译阶段暴露 `2190` 个唯一 AVLN 错误、`67` 个警告（此前被 C# 错误掩盖，属 XAML/资源加载链阶段）
- **检查范围**：当前工作区中的旧 WPF 项目、Avalonia 解决方案、应用源码、XAML、构建结果和测试资产
- **检查性质**：在只读审查基础上进行了定向迁移清理，移除了两个暂不纳入 Avalonia 的模块引用，将渲染路径固定为 Avalonia.Skia；本次又集中清理了核心项目的非 shim 类编译错误

> 本报告刻意区分“源码已搬运”与“能构建、能启动、功能等价”。文件层面的高覆盖率不代表功能完成度；当前工作树包含大量未提交或未跟踪的迁移文件，此快照尚不能由分支稳定复现。
>
> **重要更正**：上一版记录的“5 个错误、45 个警告”是增量构建跳过了核心项目完整编译造成的假象。核心项目当时的真实唯一错误数约 361 个（编译输出 722 行）。本轮已将其全部消减到 0（含 WPF 兼容 shim 批次）。C# 清零后，完整重建进入 Avalonia XAML 编译阶段，暴露出一直存在但被 C# 错误掩盖的 XAML 迁移错误（见“当前编译阻塞”）。

## 结论

当前迁移处于“大规模源码搬运后的整合阶段”，尚未达到可构建、可启动或可验证核心编辑流程的状态。

文件层面的覆盖率已经较高，但大量文件仍是机械搬运、未接入、未编译、空实现或 WPF 语义残留。因此，文件覆盖率不能作为功能完成率使用。按可交付口径，当前版本应视为迁移中的 pre-alpha 快照。

## 状态总览

| 领域 | 状态 | 当前结果 |
| --- | --- | --- |
| 项目与应用外壳 | 部分完成 | 已建立 Core、Desktop、Browser 项目，以及 Gekimini Shell 和 DI 启动结构 |
| C# 文件搬运 | 较高 | 旧项目 969 个 C# 文件中有 888 个同路径对应，覆盖率约 91.6% |
| XAML 文件搬运 | 较高 | 旧项目 65 个 WPF XAML 中有 58 个对应 AXAML，覆盖率约 89.2% |
| Debug 构建 | 阻断 | 全解决方案构建失败：C# 0 错误，XAML 编译阶段 2190 个唯一 AVLN 错误、67 个警告，集中在核心项目的 40+ 个 .axaml 文件 |
| Avalonia XAML | 阻断 | 存在旧 CLR namespace、WPF Trigger、Storyboard 和 pack URI；绝大多数视图未加载 XAML |
| 谱面渲染 | 已接入、待运行验证 | 已固定使用 Avalonia.Skia 的 `SKCanvas` lease；D3D、OpenGL 和独立 CPU Skia backend 不再参与编译 |
| 音频 | 不可用 | NAudio 后端被排除编译，保留实现明确标记为未迁移 |
| 功能模块 | 不完整 | 3 个完整模块尚未迁移，另有少量模块文件缺失 |
| 自动化验证 | 未开始 | 应用源码中没有测试项目或测试文件 |
| 仓库可复现性 | 高风险 | 应用目录有 832 个未跟踪文件，其中包括 754 个 C# 和 58 个 AXAML |

## 检查基准

### 项目版本

- 目标框架：`.NET 10.0`
- 本次使用的 SDK：`.NET SDK 10.0.302`
- Avalonia：`11.3.10`
- 主项目：[`OngekiFumenEditor.Avalonia.csproj`](../src/OngekiFumenEditor.Avalonia/OngekiFumenEditor.Avalonia.csproj)
- Desktop 入口：[`OngekiFumenEditor.Avalonia.Desktop.csproj`](../src/OngekiFumenEditor.Avalonia.Desktop/OngekiFumenEditor.Avalonia.Desktop.csproj)
- Browser 入口：[`OngekiFumenEditor.Avalonia.Browser.csproj`](../src/OngekiFumenEditor.Avalonia.Browser/OngekiFumenEditor.Avalonia.Browser.csproj)

仓库没有 `global.json`，因此实际 SDK 版本取决于开发机环境。

### 构建命令

```powershell
dotnet build .\OngekiFumenEditor.Avalonia.sln --no-restore -t:Rebuild -m:1 -v:minimal
```

当前完整重建结果：构建失败。C# 编译（含核心 `OngekiFumenEditor.Avalonia`）已 0 错误；失败发生在核心项目的 Avalonia XAML 编译（CompileAvaloniaXaml）阶段，共 2190 个唯一 AVLN 错误和 67 个警告。Desktop 和 Browser 入口尚未进入有效验收阶段；仅重跑增量构建时，XAML 编译可能被跳过而显示“生成成功”，需以 `-t:Rebuild` 全量重建为准。

此前只读审查快照记录的是 83 个错误和 53 个警告；上一版本节记录的“5 个错误”经本轮验证为增量构建假象，核心项目当时真实唯一错误约 361 个。本轮集中修复后 C# 降至 0（含 WPF 兼容 shim 批次：交互系统、`MessageBox`、拖放、`Visibility`），剩余构建错误全部属于 XAML 迁移范畴。其中最后 8 个非 shim 错误为：`LambdaUndoAction`/`EndCombineAction` 的 `LocalizedString` 参数改用 `ToLocalizedStringByRawText()`/`ToFormatLocalizedString()`（6 处）、`IoC.Get<IShell>()` 补 `Gekimini.Avalonia.Modules.Shell` using（1 处）、`CommandRouterHelper` 补单参数 `ExecuteCommand(Command)` 重载（1 处）。

历史文件 [`migration_gap_report.txt`](../../migration_gap_report.txt) 在 2026-03-15 记录了 126 个错误。错误数变化轨迹：126 → 83 → （虚报 5，实际约 361）→ 77 → 17 → C# 0（但 XAML 编译阶段暴露 2190 个 AVLN 错误）。构建门槛仍未通过。

## 当前编译阻塞

本轮已修复上一版记录的 5 个“错误”（`KeyBindingDefinition` 源生成重复、`Screen` 缺失、重复 `ScrollTo`、无效 `OnViewLoaded` override），并额外清理了约 350 个非 shim 类错误，主要包括：

- `IoC` 等全局命名解析：补充 `GlobalUsings.Compat.cs` 全局 using（`OngekiFumenEditor.Avalonia.Avalonia`、`OngekiFumenEditor.Avalonia.Utils`、`Gekimini.Avalonia.Utils.MethodExtensions`），一次性消除约 250 个错误；
- 缺失类型移植/补齐：`ColorId`、`BezierCurve`（De Casteljau，替代 OpenTK 版本）、`InterpolateAllWithXGridLimitCommandDefinition`、`ISplashScreenWindow.WindowViewModel`、`IObjectPropertyAccessProxy : INotifyPropertyChanged`；
- Caliburn → CommunityToolkit 通知迁移：`OnPropertyChanged(() => X)` 全面改为 `nameof`，新增 `NotifyOfPropertyChange` 扩展；
- `Dock.Model.Core.DockMode` 被 `Dock` 属性遮蔽的 9 处改为 `global::` 限定；
- `EditorProjectSetupDialogViewModel` 按 `FileDialogHelper.OpenFileAsync` + `IDialogManager` 重写为 Avalonia 文件对话框流程；
- SkiaSharp 3.x API 适配：`SKMatrix44.SetConcat`、`SKPoint` 显式构造、`SKFont.MeasureText` 的 UTF-16 span 重载；
- `string` → `LocalizedString` 转换（`ToLocalizedStringByRawText`）、`DropShadowEffect`、Avalonia 只读 `Point` 赋值等散点修复。

**C# 编译已 0 错误**（WPF 兼容 shim 批次全部完成：交互系统改 `PointerEventArgs` 事件流 + VM 缓存输入状态，`GetView()` 改 View 回注，`Message.SetAttach` 改显式事件订阅，`MessageBox` 改 `IDialogManager`，拖放改 Gekimini `IDragDropManager`，`Visibility` converter 改 bool）。此前记录的 shim 修复方案明细已全部落地，不再赘述。

当前完整重建的 2190 个唯一错误全部是 **Avalonia XAML 编译错误**，遍布核心项目 40+ 个 .axaml 文件，构成：

- AVLN2000（约 2143 个）：无法解析类型。主要是 WPF 残留命名空间（`clr-namespace:OngekiFumenEditor.*` 未改为 `.Avalonia`）、WPF 扩展控件命名空间（`schemas.timjones.io/gemini` 的 `SliderEx`、`schemas.xceed.com` 的 `CheckComboBox`）、`OngekiFumenEditor.UI.Markup` 的 `Translate` 标记扩展未迁移、根节点旧 `x:Class`（如 `OngekiFumenEditor.App`）；单个根类型解析失败会级联出全文件错误（多个文件各报约 200 条即为此形态）。
- AVLN2200（约 32 个）：`Setter` 目标类型无法确定（上级 `Style`/`ControlTheme` 目标类型解析失败的级联）。
- AVLN2005（约 18 个）：值解析失败，如 WPF 写法 `GridLength` 字符串带空格。
- AVLN3000（约 12 个）：属性赋值器不匹配，如 WPF 的 `Click="handler"` 字符串事件写法。

这些错误在 C# 编译失败期间被掩盖（Avalonia XAML IL 编译在 C# 编译成功后才执行），属于 XAML/资源/视图加载链阶段（见下文 P2），不是本轮 C# 修复引入的回归。

此前审查中的 Skia D3D、`ActionExecutionContext`、`ToolboxItem` 和两个暂不纳入模块引用错误，本次构建输出中均已不再出现。Skia D3D 错误是通过明确取消该 backend 的编译支持消除的，不代表相关 Vortice 代码已迁移。

重点文件：

- [`FumenVisualEditorViewModel.UserInteractionActions.cs`](../src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.UserInteractionActions.cs)
- [`ConnectableObjectOperationViewModel.cs`](../src/OngekiFumenEditor.Avalonia/Modules/FumenObjectPropertyBrowser/ViewModels/ConnectableObjectOperationViewModel.cs)
- [`FumenVisualEditorViewModel.Drawing.cs`](../src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.Drawing.cs)

历史审查中的 `ActionExecutionContext` 风险仍记录在迁移背景中，但不计入当前 17 个编译错误。若后续重新启用相关未编译文件，不应通过复制 WPF/Caliburn 内部模型来恢复；应按实际交互迁移为 Avalonia 的 `PointerEventArgs`、`PointerPressedEventArgs`、命令参数、显式 `KeyBinding` 或视图事件适配层。

## XAML、资源和绑定

### MSBuild 接入

主项目当前评估到 61 个 `AvaloniaXaml` 项，说明 AXAML 已进入 Avalonia 构建项。由于 C# 中间程序集尚未生成成功，应用 AXAML 还没有完成一次有效的 XAML IL 编译验收。

### 旧命名空间残留

55 个 AXAML 文件中存在 157 行旧的 `clr-namespace:OngekiFumenEditor.*` 引用，而当前 Avalonia C# 源码中没有对应的旧 namespace 声明。示例：

- [`FumenVisualEditorView.axaml`](../src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/Views/FumenVisualEditorView.axaml)
- [`SplashScreenView.axaml`](../src/OngekiFumenEditor.Avalonia/Modules/SplashScreen/Views/SplashScreenView.axaml)

这些引用需要统一迁移到 `OngekiFumenEditor.Avalonia.*`，并核对必要的 `assembly=` 限定。

### WPF XAML 子系统残留

当前 AXAML 中仍包含：

- 12 组 `Style.Triggers`
- 14 个 `DataTrigger`
- 32 个 `MultiDataTrigger`
- 11 个 WPF `Trigger`
- 1 个 WPF `Storyboard`
- 9 个 `pack://application` URI

典型文件包括：

- [`TabControl.axaml`](../src/OngekiFumenEditor.Avalonia/UI/Themes/TabControl.axaml)
- [`CheckComboBox.axaml`](../src/OngekiFumenEditor.Avalonia/UI/Themes/CheckComboBox.axaml)
- [`Toast.axaml`](../src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/Views/UI/Toast.axaml)

Avalonia 不直接保留 WPF Trigger、MultiDataTrigger 和 Storyboard 子系统。迁移时应使用：

- selector 和 pseudo-class 表达控件状态；
- view model 状态或附加 class 表达数据驱动状态；
- `Transitions`、动画和 compositor API 代替 WPF Storyboard；
- `avares://` URI 和 Avalonia 资源项代替 WPF pack URI。

### 应用资源入口

[`App.axaml`](../src/OngekiFumenEditor.Avalonia/App.axaml) 仍存在以下问题：

- `x:Class="OngekiFumenEditor.App"` 与当前 CLR 类型不匹配；
- 仍引用旧 Gemini namespace；
- 仍引用 MahApps WPF 主题；
- 合并字典使用 `.xaml` 路径和 WPF pack URI；
- Ongeki 派生应用没有明确加载该资源字典，当前初始化主要来自 Gekimini 基类。

应先确定应用级资源的唯一入口，再迁移主题、转换器和合并字典，避免逐个视图修复后仍因资源不可见而失败。

### 视图初始化

58 个 XAML code-behind 文件中，仅 4 个调用了 `InitializeComponent` 或 `AvaloniaXamlLoader.Load`，其余 54 个没有加载对应 XAML。主编辑器视图就是空构造器：

- [`FumenVisualEditorView.xaml.cs`](../src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/Views/FumenVisualEditorView.xaml.cs)

Gekimini [`ViewLocator.cs`](../Dependencies/Gekimini.Avalonia/src/Gekimini.Avalonia/Views/ViewLocator.cs) 只负责创建控件和设置 `DataContext`，不会代替视图调用 XAML loader。因此这些视图即使成功实例化，也不能形成预期视觉树。

### 编译绑定和 Release/AOT

AXAML 中约有 503 个普通 `{Binding ...}`，但只有 4 处 `x:DataType`，没有 `{CompiledBinding ...}`。主项目在 Release 配置中启用了：

- `AvaloniaUseCompiledBindingsByDefault=true`
- `IsAotCompatible=true`

在完成 `x:DataType` 和绑定路径校验前，Release/AOT 不能视为可用。应在 Debug XAML 编译通过后，单独建立 Release 和裁剪/AOT 验收。

## 应用生命周期和窗口

以下结构已经建立：

- Desktop 使用 `StartWithClassicDesktopLifetime`；
- Browser 使用单视图生命周期；
- Ongeki 应用通过 Gekimini 基类注册服务、创建主视图并接入 Shell；
- Desktop 和 Browser 都提供了平台服务注册入口。

相关文件：

- [`Program.cs`](../src/OngekiFumenEditor.Avalonia.Desktop/Program.cs)
- [`ExampleDesktopApp.cs`](../src/OngekiFumenEditor.Avalonia.Desktop/ExampleDesktopApp.cs)
- [`ExampleBrowserApp.cs`](../src/OngekiFumenEditor.Avalonia.Browser/ExampleBrowserApp.cs)
- [`OngekiFumenEditorApp.cs`](../src/OngekiFumenEditor.Avalonia/OngekiFumenEditorApp.cs)

该部分属于“结构已建立、运行未验证”。核心项目无法编译，应用资源和主视图也未接通，因此不能据此判断窗口恢复、布局持久化、退出流程或 Browser 生命周期已经可用。

## 渲染状态

渲染后端已按产品决策收敛为单一路径：Avalonia 负责控件和渲染表面，编辑器在 Avalonia 提供的 Skia lease 生命周期内使用 `SkiaSharp.SKCanvas` 绘制。

### Skia

[`DefaultSkiaDrawingManagerImpl.cs`](../src/OngekiFumenEditor.Avalonia/Kernel/Graphics/Skia/DefaultSkiaDrawingManagerImpl.cs) 与 [`AvaloniaSkiaRenderControl.cs`](../src/OngekiFumenEditor.Avalonia/Kernel/Graphics/Skia/AvaloniaSkiaRenderControl.cs) 当前：

- `CreateRenderControl()` 返回专用 `Control`，通过 `DrawingContext.Custom(ICustomDrawOperation)` 接入 Avalonia 渲染树；
- `ICustomDrawOperation.Render(ImmediateDrawingContext)` 使用 `TryGetFeature(typeof(ISkiaSharpApiLeaseFeature))` 获取 `ISkiaSharpApiLeaseFeature`，调用 `Lease()` 后只在 lease 有效期间访问 `lease.SkCanvas`；
- 帧循环不再使用 `Task.Run`，由 `InvalidateVisual()` 驱动 Avalonia 渲染调度，帧间隔使用 `Stopwatch` 计算；
- `BeforeRender`/`AfterRender` 对画布执行 `Save`/`Restore`，`CleanRender` 使用 Skia 清屏；
- Circle、Beam、Line、Texture、Highlight、Polygon、String 及 CPU-side 缓存形式的静态线条句柄均通过该画布路径绘制（不是独立 CPU Skia backend）；
- 独立的 `ISvgDrawing` 已明确抛出“不支持”；当前 SVG 编辑器绘制目标实际使用缓存线条与纹理路径，后续仍需单独验收 SVG 显示一致性；
- custom draw operation 边界按 `RenderScaling` 转换为物理像素；编辑器布局与输入继续使用 Avalonia 逻辑像素，绘制时保留 lease 画布已有的控件偏移、裁剪和 DPI 变换，再叠加编辑器投影矩阵，避免高 DPI 下重复缩放或控件位置丢失；
- 该实现参考了 [`ReOsuStoryboardPlayer.Avalonia` 的 Skia lease 示例](https://github.com/MikiraSora/ReOsuStoryboardPlayer.Avalonia/blob/master/ReOsuStoryboardPlayer.Avalonia/UI/Controls/StoryboardPlayer.axaml.cs#L379-L421)。

### 不支持的 backend

- `Kernel/Graphics/OpenGL`、Skia D3D/GL context、旧 Skia RenderControls、CPU/OpenGL/DirectX backend 枚举均通过主项目 `Compile Remove` 排除；
- 主项目不再引用 Vortice Direct3D/DXGI 包，程序设置页也不再暴露 render manager 或 Skia backend 选择；
- 旧 backend 源码保留在工作区以保留迁移历史和注释，但不属于当前 Avalonia 构建产物，也不应作为运行时 fallback；
- 当前核心项目的依赖图为 `Avalonia.Skia 11.3.10` + `SkiaSharp 2.88.9`。Gekimini 依赖项目仍解析到 `SkiaSharp 2.88.3` 并产生独立漏洞警告，后续需要单独统一依赖版本。

当前仍缺少桌面启动后的人工渲染冒烟验证；C# 编译虽已清零，但核心项目 XAML 编译阶段尚有 2190 个唯一 AVLN 错误，且缺少桌面启动后的人工渲染冒烟验证，暂不能把画面显示标记为已验收。

## 音频状态

主项目排除了整个 `Kernel/Audio/NAudioImpl` 编译目录，共涉及 19 个 C# 文件。保留的 [`NAudioManager.cs`](../src/OngekiFumenEditor.Avalonia/Kernel/Audio/NAudioImpl/NAudioManager.cs) 和 `DefaultMusicPlayer` 明确标记为未迁移，并在加载或播放时抛出异常或返回空状态。

当前业务代码仍通过 `IAudioManager` 执行：

- 音频文件选择；
- 谱面项目加载；
- 波形提取；
- 播放、暂停、定位和变速；
- 谱面音效播放。

但源码中没有可用的替代 `IAudioManager` 注册。因此，即使解决编译和 XAML 问题，涉及音频的编辑流程仍会失败。

[`AudioAdjustWindowViewModel.cs`](../src/OngekiFumenEditor.Avalonia/Modules/AudioAdjustWindow/ViewModels/AudioAdjustWindowViewModel.cs) 也只支持零偏移文件复制，非零偏移明确返回“未实现”。

## 显式排除的源码

[`OngekiFumenEditor.Avalonia.csproj`](../src/OngekiFumenEditor.Avalonia/OngekiFumenEditor.Avalonia.csproj) 当前通过 `Compile Remove` 排除了 82 个 C# 文件：

| 功能区域 | 文件数 |
| --- | ---: |
| NAudio 后端 | 19 |
| OpenGL 后端 | 29 |
| Skia D3D context | 3 |
| Skia GL context | 10 |
| Skia RenderControls | 5 |
| Skia backend 枚举 | 1 |
| SVG 属性编辑 ViewModel | 4 |
| 音频波形绘制 | 1 |
| UI Behaviors | 2 |
| ListView 拖放 | 3 |
| 特定 WPF 控件 | 5 |

这些排除项应逐项标记为以下三类之一；本次新增的 OpenGL、D3D、GL、CPU/DirectX 路径属于产品决定取消，不是待自动 fallback 的实现：

1. 已由 Avalonia 实现替代，可以删除旧文件；
2. 尚未迁移，需要进入路线图；
3. 产品决定取消，需要记录功能差异。

渲染相关排除项已经由项目注释和本报告记录为“由 Avalonia.Skia 单一路径替代”；其他排除项仍需继续补充替代关系或取消原因。

## 缺失模块（按当前决策不纳入 Avalonia 编译）

以下完整模块在旧 WPF 项目中存在，但 Avalonia 应用目录中不存在：

本次已移除它们在 Avalonia 源码、设置重置和 JSON 源生成中的编译期引用。下表仍用于记录旧 WPF 功能覆盖差异，不代表这些模块当前应参与编译。

| 模块 | 缺失 C# | 缺失 XAML | 功能范围 |
| --- | ---: | ---: | --- |
| `OptionGeneratorTools` | 39 | 5 | ACB、封面和 Music XML 等生成工具 |
| `EditorScriptExecutor` | 18 | 1 | 编辑器脚本执行与文档 |
| `OgkiFumenListBrowser` | 9 | 1 | 谱面列表浏览 |

此外，`FumenVisualEditor` 仍缺少 3 个同路径 C# 文件，`FumenConverter` 缺少 1 个。

## 测试、诊断和依赖风险

### 自动化测试

应用 `src` 目录中：

- 测试项目数量：0；
- 可识别测试文件数量：0。

依赖仓库中的测试不能替代本应用的迁移测试。至少需要覆盖：

- 应用启动和 Shell 创建；
- ViewLocator 能加载主要视图；
- OGKR/项目文件打开、保存和往返一致性；
- 谱面渲染非空；
- 音频加载、播放、暂停和定位；
- 选择、拖放、撤销/重做和剪贴板；
- 设置、语言和布局持久化。

### NuGet 风险

核心项目通过直接引用 `Avalonia.Skia 11.3.10` 解析到 `SkiaSharp 2.88.9`。但依赖项目 Gekimini 仍单独解析到 `SkiaSharp 2.88.3` 并产生 `NU1903` 高严重性漏洞警告；Desktop 项目另有 `Tmds.DBus.Protocol 0.21.2` 的 `NU1903`。后续需要在依赖项目层统一版本并重新验证 Avalonia/SkiaSharp API 兼容性。

## 仓库状态

当前应用相关目录统计：

| 类型 | 已跟踪 | 未跟踪 |
| --- | ---: | ---: |
| 全部应用文件 | 215 | 832 |
| C# | 190 | 754 |
| AXAML | 3 | 58 |
| JSON | 4 | 3 |
| 项目文件 | 4 | 0 |

采集时工作树领先远端 2 个提交，并存在大量未提交修改。在形成可复现检查点前，其他开发机或 CI 无法仅通过当前分支还原本报告所检查的源码状态。

## 可复现的检查方法

以下命令均在仓库的 `Avalonia` 目录中通过 PowerShell 执行。

### 构建解决方案

```powershell
dotnet build '.\OngekiFumenEditor.Avalonia.sln' --no-restore -t:Rebuild -m:1 -v:minimal
```

### 按错误码归并核心项目编译错误

```powershell
$output = & dotnet build `
    '.\src\OngekiFumenEditor.Avalonia\OngekiFumenEditor.Avalonia.csproj' `
    --no-restore -m:1 -v:minimal 2>&1

$output |
    Select-String 'error \w+:' |
    ForEach-Object { $_.Line -replace '^.*error (?<code>\w+):.*$', '${code}' } |
    Group-Object |
    Sort-Object Count -Descending
```

编译器通常会在最终摘要中重复输出错误；需要统计唯一错误时，应按文件、行号、错误码和消息去重。

### 统计未加载 XAML 的 code-behind

```powershell
$root = '.\src\OngekiFumenEditor.Avalonia'
$codeBehind = @(
    Get-ChildItem -LiteralPath $root -Recurse -File |
        Where-Object {
            $_.Name -match '\.(a?xaml)\.cs$' -and
            $_.FullName -notmatch '\\(bin|obj)\\'
        }
)

$withLoader = @(
    $codeBehind | Where-Object {
        $text = Get-Content -Raw -Encoding UTF8 -LiteralPath $_.FullName
        $text -match 'InitializeComponent\s*\(' -or
        $text -match 'AvaloniaXamlLoader\.Load\s*\('
    }
)

"CODE_BEHIND=$($codeBehind.Count)"
"WITH_XAML_LOAD=$($withLoader.Count)"
"WITHOUT_XAML_LOAD=$($codeBehind.Count - $withLoader.Count)"
```

### 统计 AXAML 中的 WPF 语义残留

```powershell
$root = '.\src\OngekiFumenEditor.Avalonia'
$patterns = [ordered]@{
    StyleTriggers    = '<Style\.Triggers>'
    DataTrigger      = '<DataTrigger\b'
    MultiDataTrigger = '<MultiDataTrigger\b'
    Trigger          = '<Trigger\b'
    Storyboard       = '<Storyboard\b'
    PackUri          = 'pack://application'
}

foreach ($entry in $patterns.GetEnumerator()) {
    $matches = @(
        rg -n --glob '!**/bin/**' --glob '!**/obj/**' `
            --glob '*.axaml' $entry.Value $root 2>$null
    )
    "{0}={1}" -f $entry.Key, $matches.Count
}
```

### 检查旧 CLR namespace 和仓库状态

```powershell
rg -n --pcre2 --glob '!**/bin/**' --glob '!**/obj/**' `
    --glob '*.cs' '^namespace OngekiFumenEditor\.(?!Avalonia(?:\.|;))' `
    '.\src\OngekiFumenEditor.Avalonia'

git status --porcelain=v1 -uall -- .
```

## 建议实施顺序

### P0：建立可复现基线

1. 确认 832 个未跟踪应用文件的来源和归属。
2. 将迁移快照拆分为可审查的提交或专用 checkpoint 分支。
3. 固定 .NET SDK，并记录标准 restore/build 命令。
4. 将本报告中的检查项转为脚本或 CI 输出。

验收条件：干净检出后可以还原相同项目图、源码数量和构建错误基线。

### P1：恢复 Debug 编译

1. ~~修复 KeyBinding 源生成重复定义。~~（已完成）
2. ~~将 `EditorProjectSetupDialogViewModel` 的 `Screen` 迁移到 Avalonia 窗口/屏幕模型。~~（已完成：改用 `WindowViewModelBase` + `FileDialogHelper` + `IDialogManager`）
3. ~~合并重复的 `ScrollTo` 实现。~~（已完成）
4. ~~将无效的 `OnViewLoaded` override 接入当前 Gekimini 生命周期。~~（已完成）
5. 统一 Gekimini 与核心项目的 SkiaSharp 版本。（未完成）
6. ~~清理已删除模块的编译期引用~~（已完成）。
7. ~~完成 WPF 兼容 shim 批次~~（已完成：交互系统改 `PointerEventArgs` 事件流 + VM 缓存输入状态，`GetView()` 改 View 回注，`Message.SetAttach` 改显式事件订阅，`MessageBox` 改 `IDialogManager`，拖放改 Gekimini `IDragDropManager`，`Visibility` converter 改 bool）。

验收条件：核心、Desktop 和 Browser 项目 Debug 构建均为 0 错误（当前 C# 0 错误已达成；完整重建在 XAML 编译阶段 2190 错误 / 67 警告，转入 P2）。

### P2：重建资源和视图加载链

1. 修复 `App.axaml` 类型、主题和资源入口。
2. 统一全部 AXAML CLR namespace。
3. 为所有 code-behind 接入 `InitializeComponent` 或明确的 loader。
4. 将 pack URI 转换为 `avares://`。
5. 用 Avalonia selector、pseudo-class、class 和 transition 替代 WPF Trigger/Storyboard。

验收条件：应用 AXAML 编译为 0 个 AVLN 错误，主 Shell 和主要工具视图可以显示。

### P3：打通核心编辑闭环

1. 对已接入的 Avalonia.Skia 渲染路径执行桌面人工冒烟、DPI、缩放和资源释放验证。
2. 实现并注册可用的音频后端。
3. 验证谱面打开、渲染、编辑、撤销和保存。
4. 验证选中、拖放、滚动、缩放、键盘命令和剪贴板。

验收条件：能够完成“打开项目 -> 显示谱面 -> 编辑对象 -> 播放定位 -> 保存项目”的人工冒烟流程。

### P4：补齐功能和发布质量

1. 迁移或明确取消 3 个缺失模块。
2. 补齐设置、更新、对话框、SVG、波形和音频偏移功能。
3. 添加 Headless/UI/业务逻辑测试。
4. 修复依赖漏洞和警告。
5. 验证 Release compiled bindings、裁剪和 AOT。
6. 分别验证 Desktop 与 Browser 的平台能力边界。

验收条件：功能差异清单全部关闭或有明确产品决策，Release 构建和关键自动化测试通过。

## 后续更新规则

每次更新本报告时应至少重新采集：

1. `dotnet build` 错误和警告数量；
2. 缺失 C#/XAML 文件数量；
3. WPF namespace、Trigger、Storyboard 和 pack URI 残留数量；
4. 没有加载 XAML 的 code-behind 数量；
5. `Noop*`、空实现和 `NotImplementedException` 数量；
6. 被 `Compile Remove` 排除的文件数量；
7. 测试项目、测试数量和关键工作流结果；
8. 未跟踪应用文件数量。

只有通过对应验收条件后，才应将某一迁移领域标记为完成。
