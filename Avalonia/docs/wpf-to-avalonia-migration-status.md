# WPF → Avalonia 迁移状态报告

- **检查日期**：2026-07-31
- **文档更新日期**：2026-07-31
- **检查基线提交**：`32909b6b`（分支 `avalonia`）
- **验证命令**：`dotnet build OngekiFumenEditor.Avalonia.sln --no-restore -m:1 -v:minimal`
- **构建结果**：失败，`8` 个错误、`20` 个警告（整套解决方案当前复验）
- **检查范围**：当前工作区中的旧 WPF 项目、Avalonia 解决方案、应用源码、XAML、构建结果和测试资产
- **检查性质**：在只读审查基础上进行了定向迁移清理，移除了两个暂不纳入 Avalonia 的模块引用

> 本报告刻意区分“源码已搬运”与“能构建、能启动、功能等价”。文件层面的高覆盖率不代表功能完成度；当前工作树包含大量未提交或未跟踪的迁移文件，此快照尚不能由分支稳定复现。

## 结论

当前迁移处于“大规模源码搬运后的整合阶段”，尚未达到可构建、可启动或可验证核心编辑流程的状态。

文件层面的覆盖率已经较高，但大量文件仍是机械搬运、未接入、未编译、空实现或 WPF 语义残留。因此，文件覆盖率不能作为功能完成率使用。按可交付口径，当前版本应视为迁移中的 pre-alpha 快照。

## 状态总览

| 领域 | 状态 | 当前结果 |
| --- | --- | --- |
| 项目与应用外壳 | 部分完成 | 已建立 Core、Desktop、Browser 项目，以及 Gekimini Shell 和 DI 启动结构 |
| C# 文件搬运 | 较高 | 旧项目 969 个 C# 文件中有 888 个同路径对应，覆盖率约 91.6% |
| XAML 文件搬运 | 较高 | 旧项目 65 个 WPF XAML 中有 58 个对应 AXAML，覆盖率约 89.2% |
| Debug 构建 | 阻断 | 全解决方案构建失败，当前 8 个错误、20 个警告（此前快照为 83/53） |
| Avalonia XAML | 阻断 | 存在旧 CLR namespace、WPF Trigger、Storyboard 和 pack URI；绝大多数视图未加载 XAML |
| 谱面渲染 | 不可用 | RenderControl、OpenGL 和部分 Skia 绘制仍为 `Panel`、`Noop*` 或空方法 |
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
dotnet build .\OngekiFumenEditor.Avalonia.sln --no-restore -m:1 -v:minimal
```

当前复验结果：构建失败，共 8 个错误和 20 个警告。依赖项目已完成编译，错误仍发生在核心 `OngekiFumenEditor.Avalonia` 项目，因此 Desktop 和 Browser 入口尚未进入有效验收阶段。

此前只读审查快照记录的是 83 个错误和 53 个警告；本次清理后，`EditorScriptExecutor` 和 `OptionGeneratorTools` 相关错误已不再出现。

历史文件 [`migration_gap_report.txt`](../../migration_gap_report.txt) 在 2026-03-15 记录了 126 个错误。此前审查快照已降至 83 个错误，本次定向清理后当前为 8 个错误，但构建门槛仍未通过。

## 当前编译阻塞

当前 8 个唯一错误的主要构成如下：

| 根因 | 数量 | 说明 |
| --- | ---: | --- |
| Skia D3D 类型缺失 | 3 | `GRD3DBackendContext`、`GRD3DTextureResourceInfo` 等 API 与实际依赖图不匹配 |
| 源生成属性重复 | 2 | `KeyBindingDefinition` 同时声明 `[ObservableProperty]` 字段和同名显式属性 |
| `Screen` 类型缺失 | 1 | `EditorProjectSetupDialogViewModel` 仍使用未映射的屏幕类型 |
| 重复 `ScrollTo` | 1 | `FumenVisualEditorViewModel` 的分部类中存在相同签名 |
| 无效 `OnViewLoaded` override | 1 | 当前基类没有可重写的同名生命周期方法 |

此前审查中的 `ActionExecutionContext`、`ToolboxItem` 和两个暂不纳入模块引用错误，本次构建输出中均已不再出现。

重点文件：

- [`KeyBindingDefinition.cs`](../src/OngekiFumenEditor.Avalonia/Kernel/KeyBinding/KeyBindingDefinition.cs)
- [`GRVorticeD3DBackendContext.cs`](../src/OngekiFumenEditor.Avalonia/Kernel/Graphics/Skia/D3dContexts/GRVorticeD3DBackendContext.cs)
- [`GRVorticeD3DTextureResourceInfo.cs`](../src/OngekiFumenEditor.Avalonia/Kernel/Graphics/Skia/D3dContexts/GRVorticeD3DTextureResourceInfo.cs)
- [`VorticeDirect3DContext.cs`](../src/OngekiFumenEditor.Avalonia/Kernel/Graphics/Skia/D3dContexts/VorticeDirect3DContext.cs)
- [`EditorProjectSetupDialogViewModel.cs`](../src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/ViewModels/Dialogs/EditorProjectSetupDialogViewModel.cs)
- [`FumenVisualEditorViewModel.Drawing.cs`](../src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.Drawing.cs)
- [`FumenVisualEditorViewModel.ScrollViewer.cs`](../src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.ScrollViewer.cs)

历史审查中的 `ActionExecutionContext` 风险仍记录在迁移背景中，但不计入本次 8 个编译错误。若后续重新启用相关未编译文件，不应通过复制 WPF/Caliburn 内部模型来恢复；应按实际交互迁移为 Avalonia 的 `PointerEventArgs`、`PointerPressedEventArgs`、命令参数、显式 `KeyBinding` 或视图事件适配层。

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

渲染是当前最重要的架构阻塞之一。

### Skia

[`DefaultSkiaDrawingManagerImpl.cs`](../src/OngekiFumenEditor.Avalonia/Kernel/Graphics/Skia/DefaultSkiaDrawingManagerImpl.cs) 当前：

- `CreateRenderControl()` 返回普通 `Panel`；
- `SimpleLineDrawing`、`StaticVBODrawing` 和 `SvgDrawing` 使用 `Noop*`；
- Beam 和 Circle 绘制类保留空方法；
- RenderContext 的 `BeforeRender`、`AfterRender` 和 `CleanRender` 为空；
- 渲染循环运行于 `Task.Run`，尚未与 Avalonia 渲染线程或 compositor 生命周期形成明确契约。

### OpenGL

[`DefaultOpenGLRenderManagerImpl.cs`](../src/OngekiFumenEditor.Avalonia/Kernel/Graphics/OpenGL/DefaultOpenGLRenderManagerImpl.cs) 的全部绘制接口均使用 `Noop*`，`CreateRenderControl()` 同样返回普通 `Panel`。

### 被排除的旧后端

主项目通过 `Compile Remove` 排除了旧 Skia RenderControls。当前工作区中这些文件仍包含 WPF `FrameworkElement`、Win32 和 DirectX 假设，不能直接重新纳入编译。

迁移时应优先确定一个可工作的 Avalonia 渲染路径，例如基于 `Control.Render`、`DrawingContext.Custom`、`ICustomDrawOperation` 或 Avalonia/Skia lease 的实现。完成一条路径并验证画面后，再决定是否保留多后端抽象。

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

[`OngekiFumenEditor.Avalonia.csproj`](../src/OngekiFumenEditor.Avalonia/OngekiFumenEditor.Avalonia.csproj) 当前通过 `Compile Remove` 排除了至少 39 个 C# 文件：

| 功能区域 | 文件数 |
| --- | ---: |
| NAudio 后端 | 19 |
| Skia RenderControls | 5 |
| SVG 属性编辑 ViewModel | 4 |
| 音频波形绘制 | 1 |
| UI Behaviors | 2 |
| ListView 拖放 | 3 |
| 特定 WPF 控件 | 5 |

这些排除项应逐项标记为以下三类之一：

1. 已由 Avalonia 实现替代，可以删除旧文件；
2. 尚未迁移，需要进入路线图；
3. 产品决定取消，需要记录功能差异。

目前项目文件只表达了“不要编译”，没有表达替代关系或产品决定。

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

构建报告实际依赖图解析到 `SkiaSharp 2.88.3`，并产生 `NU1903` 高严重性漏洞警告。中央包配置中声明的 `SkiaSharp 3.119.1` 没有自动改写该传递依赖。处理该问题时需要同时核对 Avalonia、SkiaSharp 和当前渲染代码的 API 兼容性。

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
dotnet build '.\OngekiFumenEditor.Avalonia.sln' --no-restore -m:1 -v:minimal
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

1. 按事件类型替换 `ActionExecutionContext`。
2. 将 Toolbox 注册迁移到当前 Gekimini 模型。
3. 修复 KeyBinding 源生成重复定义。
4. 统一 SkiaSharp 版本和 D3D API。
5. 清理已删除模块的编译期引用（本次已完成）。
6. 删除迁移过程中保留的重复空壳方法。

验收条件：核心、Desktop 和 Browser 项目 Debug 构建均为 0 错误。

### P2：重建资源和视图加载链

1. 修复 `App.axaml` 类型、主题和资源入口。
2. 统一全部 AXAML CLR namespace。
3. 为所有 code-behind 接入 `InitializeComponent` 或明确的 loader。
4. 将 pack URI 转换为 `avares://`。
5. 用 Avalonia selector、pseudo-class、class 和 transition 替代 WPF Trigger/Storyboard。

验收条件：应用 AXAML 编译为 0 个 AVLN 错误，主 Shell 和主要工具视图可以显示。

### P3：打通核心编辑闭环

1. 实现一个真实可工作的 Avalonia 渲染后端。
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
