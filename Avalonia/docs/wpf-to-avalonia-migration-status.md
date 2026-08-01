# WPF → Avalonia 迁移状态报告

- **检查日期**：2026-08-01
- **文档更新日期**：2026-08-01（第三次更新）
- **检查基线**：工作树未提交快照（分支 `avalonia`，含 XAML 清零批次①~⑦全部改动）
- **验证命令**：`dotnet build OngekiFumenEditor.Avalonia.sln --no-restore -t:Rebuild -m:1 -v:minimal`
- **构建结果**：**成功**。全解决方案（核心 + Desktop + Browser）完整重建 **0 错误**、87 个警告
- **检查范围**：当前工作区中的旧 WPF 项目、Avalonia 解决方案、应用源码、XAML、构建结果和测试资产
- **检查性质**：在只读审查基础上进行了定向迁移清理；本轮完成了 Avalonia XAML 编译清零（批次①~⑦）

> 本报告刻意区分“源码已搬运”“能构建”与“能启动、功能等价”。**编译清零只代表通过了 Roslyn 和 Avalonia XAML IL 两道静态关卡**；应用尚未做过一次启动冒烟，大量视图存在运行时风险（见“编译清零后的已知问题”）。
>
> **历史更正回顾**：第二版记录的“5 个错误”是增量构建假象。本轮同样注意：增量构建可能跳过 XAML 编译显示“0 错误”，验收一律以 `-t:Rebuild` 全量重建为准；且 CoreCompile 失败时后续 XAML pass 不执行，AVLN 数必须先确认 CS 为 0 才可采信。

## 结论

当前迁移处于“**编译清零完成、运行验证未开始**”的阶段。

- C# 编译 0 错误（此前轮次完成）；
- Avalonia XAML 编译 0 错误（本轮完成，从 2190 个唯一 AVLN 错误清零）；
- Desktop / Browser 入口项目也首次通过完整重建。

下一步的阻塞从“编译”转为“运行时”：视图 XAML 加载（`InitializeComponent` 缺失）、资源键运行时缺失、`pack://` 图片 URI、快捷键宿主、音频后端等问题只会在启动和操作时才暴露。当前版本应视为**能构建但不能保证能启动**的 pre-alpha 快照。

## 状态总览

| 领域 | 状态 | 当前结果 |
| --- | --- | --- |
| 项目与应用外壳 | 部分完成 | 已建立 Core、Desktop、Browser 项目，以及 Gekimini Shell 和 DI 启动结构 |
| C# 文件搬运 | 较高 | 旧项目 969 个 C# 文件中有 888 个同路径对应，覆盖率约 91.6% |
| XAML 文件搬运 | 较高 | 旧项目 65 个 WPF XAML 中有 58 个对应 AXAML，覆盖率约 89.2% |
| Debug 构建 | **通过** | 全解决方案 `-t:Rebuild` 0 错误、87 警告（首次） |
| Avalonia XAML | **编译通过、运行未验** | 0 AVLN 错误；Trigger/Storyboard/pack URI 残留清零；58 个 code-behind 已全部接入 `InitializeComponent` |
| 谱面渲染 | 已接入、待运行验证 | 已固定使用 Avalonia.Skia 的 `SKCanvas` lease；D3D、OpenGL 和独立 CPU Skia backend 不再参与编译 |
| 音频 | 不可用 | NAudio 后端被排除编译，保留实现明确标记为未迁移 |
| 功能模块 | 不完整 | 3 个完整模块尚未迁移，另有少量模块文件缺失 |
| 自动化验证 | 未开始 | 应用源码中没有测试项目或测试文件 |
| 仓库可复现性 | 高风险 | XAML 清零批次①~⑦的全部改动（数百个文件）尚未提交 |

## 检查基准

### 项目版本

- 目标框架：`.NET 10.0`
- 本次使用的 SDK：`.NET SDK 10.0.302`
- Avalonia：`11.3.10`（含 `Avalonia.Controls.DataGrid 11.3.10`、`Xaml.Behaviors 11.3.9`，本轮新增引用）
- 主项目：[`OngekiFumenEditor.Avalonia.csproj`](../src/OngekiFumenEditor.Avalonia/OngekiFumenEditor.Avalonia.csproj)
- Desktop 入口：[`OngekiFumenEditor.Avalonia.Desktop.csproj`](../src/OngekiFumenEditor.Avalonia.Desktop/OngekiFumenEditor.Avalonia.Desktop.csproj)
- Browser 入口：[`OngekiFumenEditor.Avalonia.Browser.csproj`](../src/OngekiFumenEditor.Avalonia.Browser/OngekiFumenEditor.Avalonia.Browser.csproj)

仓库没有 `global.json`，因此实际 SDK 版本取决于开发机环境。

### 构建命令

```powershell
dotnet build .\OngekiFumenEditor.Avalonia.sln --no-restore -t:Rebuild -m:1 -v:minimal
```

当前完整重建结果：**构建成功，0 错误、87 警告**。警告构成（去重前）：CS8632 nullable 注解上下文（76）、CS0436 类型冲突（18）、CS8509 开关表达式不全（14）、CS0168 未用变量（12）、CS8603/CS8625/CS8618 nullable（18）、AVLN3001 XAML 资源提示（8）、MVVMTK0034（4）、NU1903 包漏洞（4，Gekimini 的 SkiaSharp 2.88.3 与 Desktop 的 Tmds.DBus.Protocol 0.21.2）。

历史错误数变化轨迹：126 → 83 →（虚报 5，实际约 361）→ 77 → 17 → C# 0（XAML 阶段暴露 2190 AVLN）→ 122 → 6 → 2 → **0（CS + AVLN 全部清零）**。

## XAML 清零批次明细（本轮）

第二版记录的 2190 个唯一 AVLN 错误已全部消除。处理按批次进行：

1. **DataGrid**：引入 `Avalonia.Controls.DataGrid 11.3.10`（`Directory.Packages.props` + 核心项目引用 + App.axaml 引入 Fluent 主题），7 个 AXAML 的 `ListView`/`GridView`/`GridViewColumn` 改为 `DataGrid`/`DataGridTextColumn`/`DataGridTemplateColumn`。
2. **GroupBox**：自写 [`UI/Controls/GroupBox.cs`](../src/OngekiFumenEditor.Avalonia/UI/Controls/GroupBox.cs)（继承 `HeaderedContentControl`）+ [`UI/Themes/GroupBox.axaml`](../src/OngekiFumenEditor.Avalonia/UI/Themes/GroupBox.axaml) ControlTheme，14 个文件替换。
3. **CheckComboBox/CheckListBox**：自写 [`UI/Controls/CheckListBox.cs`](../src/OngekiFumenEditor.Avalonia/UI/Controls/CheckListBox.cs)（`SelectedMemberPath`/`DisplayMemberPath`/全选）和 [`UI/Controls/CheckComboBox.cs`](../src/OngekiFumenEditor.Avalonia/UI/Controls/CheckComboBox.cs)（DropDownButton + Flyout），替代 Xceed 控件。
4. **cal:Message.Attach**：自写 [`UI/Behaviors/EventMethodBehavior.cs`](../src/OngekiFumenEditor.Avalonia/UI/Behaviors/EventMethodBehavior.cs)（基于 `Xaml.Behaviors`，支持事件名/方法名/PassMode/按键手势/DragEnter·Drop 附加事件/属性观察兜底），34 个文件 114 处替换；事件映射如 `MouseLeftButtonDown→PointerPressed`、`MouseDoubleClick→DoubleTapped`、`MouseWheel→PointerWheelChanged`；方法反射缓存按 ViewModel 类型和方法名共同索引，避免同一视图的多个行为错误复用首个方法。
5. **散件批**：`Visibility→IsVisible`（31）、`ToolTip→ToolTip.Tip`（28）、删 `SnapsToDevicePixels`、多行 GridLength 折叠（171）、WPF DataTrigger/ChangePropertyAction → `Xaml.Behaviors` 等价物、新增 `WideModeToVisibilityConverter`/`BoolToNotShowTextConverter`、`ListViewMultiSelectionBehavior` 重写为 DataGrid 双向同步、`BubbleScrollWheelEventBehavior` 删除（Avalonia 滚轮天然冒泡）、`SliderEx→Slider`、`ExpanderEx→Expander`、`BooleanToVisibilityConverter` 统一为全局 `BoolToVisibilityConverter`、`TranslateExtension` 补 `Path`/`StringFormat`、Toast 删 Storyboard、TabControl 自定义主题清空回退默认、CommonColorPicker 的 Xceed 色板改占位、FumenVisualEditorView 快捷键块删除。
6. **mah:MetroWindow**：13 个窗口根改 `Window`（`ResizeMode=NoResize→CanResize=False`，删除 MahApps 专有属性），多个 code-behind 基类 `UserControl→Window`。
7. **收尾批**：纯 Style 的 `.Resources` 改 `.Styles`（10 处）、空 `<ColumnDefinition>` 折叠（10 处，空白内容会被 Avalonia 当 GridLength 解析）、`App.axaml` 合并字典改 `ResourceInclude` + `avares://`、TextBlock 主题改 `Styles` 根 + `StyleInclude`、`ComboBox.ItemContainerStyle` 改 `ComboBox.Styles`、隐式 `DataTemplate` 从 `Resources` 移入 `UserControl.DataTemplates`（Avalonia 的 Resources 不支持无键模板）、`DataType="{x:Type ...}"` 改 `DataType="ns:Type"`、`App.Initialize()` 显式 `AvaloniaXamlLoader.Load(this)`（消除 AVLN3000；Gekimini 基类本就有同样调用，重写保持一致避免重复加载）、`JsonSourceGenerateContext` 跨程序集同名冲突重命名为 `OngekiJsonSourceGenerateContext`、`Startup` 改 public 并修正入口项目引用、入口项目补 using。
8. **运行时资源键预修**：`ProgramSettingView.axaml` 的 `{StaticResource MahApps.Brushes.Highlight}`（编译期不报错、运行期必炸）改为 `{DynamicResource AccentTextFillColorPrimaryBrush}`。

## 编译清零后的已知问题（功能损失与运行时风险）

以下问题不影响编译，但会在运行或功能层面暴露，按优先级排序。

**P2 运行时接线已于 2026-08-01 完成**：58 个 code-behind 全部接入 `InitializeComponent`；8 处 `pack://` 图片 URI 改 `avares://`（旧 WPF 项目 `Resources/Icons` 32 个文件复制进核心项目并登记 `AvaloniaResource`）；窗口接线按 Gekimini `IWindowManager` 语义修正——`WindowViewModelBase` 经 `ShowWindowAsync`/`ShowDialogAsync` 展示的视图必须是 `WindowViewBase` 子类（SplashScreen、ShowNewVersionDialog、AudioAdjustWindow、FumenConverter、BrushTGridRangeDialog、BulletPalleteSelectDialog、EditorProjectSetupDialog 共 7 个视图已换基类）；`FumenConverter` 菜单命令恢复为 `IWindowManager.ShowWindowAsync`，并移植缺失的转换包装器及文件选择/当前编辑器输入/执行转换逻辑；`AudioAdjustWindow` 菜单命令原本静默空转，已改为 `IWindowManager.ShowWindowAsync`；`ProgramSettingViewModel` 补上 `ProgramUpdater` 属性和 `OpenShowNewVersionDialog` 方法（XAML 行为引用的方法此前不存在）。

### 运行时必炸或必失效

1. **音频后端**：`IAudioManager` 无可用注册，涉及音频的流程必然失败（见“音频状态”）。
2. **文档新建/打开/保存未迁移**：`FumenVisualEditorViewModel` 没有 `DoNew`/`DoOpen`/`DoSave`，`EditorProjectSetupDialogViewModel` 因此没有调用方；谱面项目的创建和加载流程整体缺失（属 P3 范围）。
3. **主题资源键悬空**：各视图引用的 `EnvironmentWindowBackground`/`EnvironmentToolWindowText` 等资源键来自旧 Gemini 主题，Gekimini 源码中不存在定义（11 处），运行时按 Avalonia 缺资源行为回退（不保证美观，部分场景可能影响可读性）。

### 功能放弃或降级（需要产品决策或后续恢复）

4. **FumenVisualEditorView 编辑器快捷键**：`kb:ActionMessageKeyBinding` 块整体删除，约 20 个编辑器快捷键失效，需要写宿主层（KeyBinding/热键管理）恢复。
5. **FumenEditorSelectingObjectViewerView 列头排序**：DataGrid 列头 `SortColumn` 三处放弃。
6. **拖拽高亮**：jas 拖拽触发器全删，拖入时的高亮反馈消失（拖拽本身走 Gekimini `IDragDropManager`）。
7. **Toast 入场动画**：Storyboard 删除，现为定时器直接显隐。
8. **TabControl 自定义主题**：清空为最小字典，回退 Fluent 默认外观。
9. **CheckComboBox**：`ValueMemberPath` 语义未迁移；SelectionFilterView 显示文本为占位实现。
10. **ToolBar**：FumenEditorRenderControlViewerView 的 ToolBar 降级为 `StackPanel`；SoflanGroupListViewerView 改用 `Avalonia.Controls.ToolBar` 包前缀。
11. **CommonColorPicker**：Xceed `StandardColorPicker` 改为占位色板，标准色选择功能降级。
12. **ExceptionTermWindow**：MahApps 隐藏关闭按钮的语义在迁移中丢失。
13. **语言切换刷新**：FumenCheckerListViewer 的 `NotShow` 后缀文案在语言切换后不自动刷新。

### 需要单独验收

14. **编译绑定**：AXAML 中约 503 个普通 `{Binding}`，仅 4 处 `x:DataType`。主项目 Release 启用 `AvaloniaUseCompiledBindingsByDefault=true` 和 `IsAotCompatible=true`，在补齐 `x:DataType` 前 Release/AOT 不可用。
15. **NuGet 漏洞**：Gekimini 的 SkiaSharp 2.88.3（NU1903 高危）与 Desktop 的 Tmds.DBus.Protocol 0.21.2 需统一升级。

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

编译虽已清零，但仍缺少桌面启动后的人工渲染冒烟验证，暂不能把画面显示标记为已验收。

## 音频状态

主项目排除了整个 `Kernel/Audio/NAudioImpl` 编译目录，共涉及 19 个 C# 文件。保留的 [`NAudioManager.cs`](../src/OngekiFumenEditor.Avalonia/Kernel/Audio/NAudioImpl/NAudioManager.cs) 和 `DefaultMusicPlayer` 明确标记为未迁移，并在加载或播放时抛出异常或返回空状态。

当前业务代码仍通过 `IAudioManager` 执行：

- 音频文件选择；
- 谱面项目加载；
- 波形提取；
- 播放、暂停、定位和变速；
- 谱面音效播放。

但源码中没有可用的替代 `IAudioManager` 注册。因此，即使应用能启动，涉及音频的编辑流程仍会失败。

[`AudioAdjustWindowViewModel.cs`](../src/OngekiFumenEditor.Avalonia/Modules/AudioAdjustWindow/ViewModels/AudioAdjustWindowViewModel.cs) 也只支持零偏移文件复制，非零偏移明确返回“未实现”。

## 显式排除的源码

[`OngekiFumenEditor.Avalonia.csproj`](../src/OngekiFumenEditor.Avalonia/OngekiFumenEditor.Avalonia.csproj) 当前通过 `Compile Remove` 排除了一批 C# 文件（渲染/音频 backend、SVG 属性编辑、音频波形绘制、特定 WPF 控件与拖放等；批次⑤后 UI Behaviors、ListView 拖放相关排除已随替代实现落地逐步取消）。

这些排除项应逐项标记为以下三类之一；OpenGL、D3D、GL、CPU/DirectX 路径属于产品决定取消，不是待自动 fallback 的实现：

1. 已由 Avalonia 实现替代，可以删除旧文件；
2. 尚未迁移，需要进入路线图；
3. 产品决定取消，需要记录功能差异。

渲染相关排除项已经由项目注释和本报告记录为“由 Avalonia.Skia 单一路径替代”；其他排除项仍需继续补充替代关系或取消原因。

## 缺失模块（按当前决策不纳入 Avalonia 编译）

以下完整模块在旧 WPF 项目中存在，但 Avalonia 应用目录中不存在：

此前已移除它们在 Avalonia 源码、设置重置和 JSON 源生成中的编译期引用。下表仍用于记录旧 WPF 功能覆盖差异，不代表这些模块当前应参与编译。

| 模块 | 缺失 C# | 缺失 XAML | 功能范围 |
| --- | ---: | ---: | --- |
| `OptionGeneratorTools` | 39 | 5 | ACB、封面和 Music XML 等生成工具 |
| `EditorScriptExecutor` | 18 | 1 | 编辑器脚本执行与文档 |
| `OgkiFumenListBrowser` | 9 | 1 | 谱面列表浏览 |

此外，`FumenVisualEditor` 仍缺少 3 个同路径 C# 文件；`FumenConverter` 原缺失的转换包装器已补齐。

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

### 构建配置差异

- Debug 全解决方案 Rebuild：0 错误 / 87 警告。
- 默认 Release 全解决方案构建：374 个 AVLN 错误；核心项目在 Release 开启了 `AvaloniaUseCompiledBindingsByDefault`，但大量迁移 AXAML 尚未声明 `x:DataType`。本轮 `FumenConverterView` 已使用显式 `x:DataType` 和 `CompiledBinding`，不在该错误列表中。
- Release 核心项目在临时覆盖 `AvaloniaUseCompiledBindingsByDefault=false` 后：0 错误 / 50 警告；同一覆盖下继续构建全解决方案时，Desktop 项目还会因 `Program.cs` 缺少 `System.Threading.Tasks` 而无法解析 `TaskScheduler`。这两项属于全局 Release 基线问题。

### NuGet 风险

核心项目通过直接引用 `Avalonia.Skia 11.3.10` 解析到 `SkiaSharp 2.88.9`。但依赖项目 Gekimini 仍单独解析到 `SkiaSharp 2.88.3` 并产生 `NU1903` 高严重性漏洞警告；Desktop 项目另有 `Tmds.DBus.Protocol 0.21.2` 的 `NU1903`。后续需要在依赖项目层统一版本并重新验证 Avalonia/SkiaSharp API 兼容性。

## 仓库状态

XAML 清零批次①~⑦及 P2 运行时接线已按功能分组签入 `avalonia` 分支，报告所检查的源码状态可从分支提交历史还原。工作区中的迁移审计 `.txt` 文件不属于应用构建输入。

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

当前读数：`CODE_BEHIND=58`、`WITH_XAML_LOAD=58`、`WITHOUT_XAML_LOAD=0`。

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

当前读数：Trigger/Storyboard/PackUri 全部 0。

### 检查旧 CLR namespace 和仓库状态

```powershell
rg -n --pcre2 --glob '!**/bin/**' --glob '!**/obj/**' `
    --glob '*.cs' '^namespace OngekiFumenEditor\.(?!Avalonia(?:\.|;))' `
    '.\src\OngekiFumenEditor.Avalonia'

git status --porcelain=v1 -uall -- .
```

## 建议实施顺序

### P0：建立可复现基线

1. 将 XAML 清零批次①~⑦的未提交快照拆分为可审查的提交并推送。
2. 固定 .NET SDK，并记录标准 restore/build 命令。
3. 将本报告中的检查项转为脚本或 CI 输出。

验收条件：干净检出后可以还原相同项目图、源码数量和构建结果（0 错误）。

### P1：恢复 Debug 编译（已完成）

1. ~~修复 KeyBinding 源生成重复定义、`Screen`、重复 `ScrollTo`、无效 `OnViewLoaded` override。~~
2. ~~统一 Gekimini 与核心项目的 SkiaSharp 版本。~~（转入 P4 漏洞修复）
3. ~~清理已删除模块的编译期引用。~~
4. ~~完成 WPF 兼容 shim 批次。~~
5. ~~完成 XAML 清零批次①~⑦（本轮）。~~

验收条件：核心、Desktop 和 Browser 项目 Debug 构建均为 0 错误。**已达成（0 错误 / 87 警告）。**

### P2：重建资源和视图加载链（已完成，运行效果待冒烟）

1. ~~修复 `App.axaml` 类型、主题和资源入口。~~
2. ~~统一全部 AXAML CLR namespace。~~
3. ~~为剩余 49 个 code-behind 接入 `InitializeComponent` 或明确的 loader~~（已完成，58/58）。
4. ~~将 8 处 pack URI 转换为 `avares://`~~（已完成，含 `Resources/Icons` 资源复制与 `AvaloniaResource` 登记）。
5. ~~用 Avalonia selector、pseudo-class、class 和 transition 替代 WPF Trigger/Storyboard~~（编译层面完成；部分功能以放弃处理，见已知问题清单）。
6. ~~核对窗口类视图的实例化与 `Show`/`ShowDialog` 接线~~（已完成：7 个窗口视图换 `WindowViewBase` 基类，`FumenConverter` 与 `AudioAdjustWindow` 命令改为 `IWindowManager` 展示，`FumenConverter` 补齐转换包装器和 ViewModel 操作，`ProgramSettingViewModel` 补 `OpenShowNewVersionDialog`；`EditorProjectSetupDialog` 的调用方随文档新建流程一并属 P3）。

验收条件：应用 AXAML 编译为 0 个 AVLN 错误（**已达成**）；主 Shell 和主要工具视图可以显示（**接线已就绪，待启动冒烟确认**）。

### P3：打通核心编辑闭环

1. 对已接入的 Avalonia.Skia 渲染路径执行桌面人工冒烟、DPI、缩放和资源释放验证。
2. 实现并注册可用的音频后端。
3. 验证谱面打开、渲染、编辑、撤销和保存。
4. 验证选中、拖放、滚动、缩放、键盘命令和剪贴板。
5. 恢复 FumenVisualEditorView 编辑器快捷键宿主。

验收条件：能够完成“打开项目 -> 显示谱面 -> 编辑对象 -> 播放定位 -> 保存项目”的人工冒烟流程。

### P4：补齐功能和发布质量

1. 迁移或明确取消 3 个缺失模块。
2. 补齐设置、更新、对话框、SVG、波形和音频偏移功能；处理已知问题清单中的降级项（列头排序、拖拽高亮、Toast 动画、TabControl 主题、ColorPicker 等）。
3. 添加 Headless/UI/业务逻辑测试。
4. 修复依赖漏洞（SkiaSharp 2.88.3、Tmds.DBus.Protocol 0.21.2）和 87 个编译警告。
5. 验证 Release compiled bindings、裁剪和 AOT（先补 `x:DataType`）。
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
