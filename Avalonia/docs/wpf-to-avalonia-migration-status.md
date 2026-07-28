# WPF → Avalonia 迁移状态报告

- **检查日期**：2026-07-29
- **检查基线提交**：`32909b6b`（分支 `avalonia`）
- **验证命令**：`dotnet build OngekiFumenEditor.Avalonia.sln --no-restore -m:1 -v:minimal`
- **构建结果**：**失败**，`83` 个错误、`53` 个警告
- **检查性质**：只读审查，未修改任何源代码

> **重要提示**：本报告刻意区分「源码已搬运」与「能构建、能启动、功能等价」。文件层面的高覆盖率**不代表**功能完成度。当前工作树包含大量未提交/未跟踪的迁移文件，此快照尚不能由分支稳定复现（详见「可复现性风险」）。

## 结论摘要

项目当前处于**大规模源码搬运后的整合阶段**，尚未达到可构建、可启动或可验证核心编辑流程的状态。启动外壳（Desktop / Browser / Gekimini Shell / DI）已搭起，但核心 C# 仍有 83 个编译错误、绝大多数视图未加载 XAML、渲染与音频后端仍是占位实现，并有 3 个完整功能模块尚未迁移。

## 当前状态总表

| 领域 | 状态 | 结果 |
|---|---|---|
| 文件搬运 | 较高 | C# 同路径覆盖 `888/969`（91.6%），XAML→AXAML 覆盖 `58/65`（89.2%） |
| Debug 构建 | 阻断 | 最新全解决方案构建：`83` 个错误、`53` 个警告 |
| 应用外壳 | 部分完成 | Desktop、Browser、Gekimini Shell 和 DI 结构已建立 |
| Avalonia UI | 未闭环 | 大量 WPF XAML 语义残留，绝大多数视图没有加载 XAML |
| 谱面渲染 | 不可用 | RenderControl、OpenGL、部分 Skia 绘制仍为 `Panel`、`Noop*` 或空方法 |
| 音频 | 不可用 | NAudio 后端被排除编译，现有实现明确标记为未迁移 |
| 功能模块 | 不完整 | 3 个完整模块缺失 |
| 自动化验证 | 无 | 应用代码中没有测试项目或测试文件 |

## 主要阻塞项

### 1. 核心项目仍有 83 个编译错误

历史报告为 126 个（`migration_gap_report.txt`，2026-03-15），说明有所推进，但当前仍不能生成 Desktop/Browser 应用。错误主要集中在把 WPF/Caliburn 事件上下文直接搬到 Avalonia 后未完成重构：

- **`ActionExecutionContext`：56 个** — 仍沿用 Caliburn/WPF 事件上下文，例如 [ConnectableObjectOperationViewModel.cs:77](../src/OngekiFumenEditor.Avalonia/Modules/FumenObjectPropertyBrowser/ViewModels/ConnectableObjectOperationViewModel.cs)
- **`ToolboxItem` / `ToolboxItemAttribute`：16 个**
- **Skia D3D API 类型：3 个**
- 其余：KeyBinding 源生成属性重复、重复 `ScrollTo`、无效 `override`、缺失模块引用等

### 2. AXAML 仍基本是机械迁移内容

Avalonia 不提供以下 WPF 子系统的直接兼容，这批视图需按选择器、伪类、状态和 Transitions 重写，不能靠把扩展名改成 `.axaml` 即完成迁移：

- 55 个文件中存在 **157 行**旧 `OngekiFumenEditor.*` CLR namespace，而当前 C# 中已无对应旧命名空间
- **12 组** `Style.Triggers`、**14 个** `DataTrigger`、**32 个** `MultiDataTrigger`、**11 个** WPF `Trigger`
- **9 个** WPF `pack://application` URI
- 示例：[SplashScreenView.axaml:27](../src/OngekiFumenEditor.Avalonia/Modules/SplashScreen/Views/SplashScreenView.axaml)、[TabControl.axaml:213](../src/OngekiFumenEditor.Avalonia/UI/Themes/TabControl.axaml)
- `App.axaml` 的 `x:Class`、Gemini/MahApps 资源和 `.xaml` 路径仍不匹配当前项目：[App.axaml:2](../src/OngekiFumenEditor.Avalonia/App.axaml)

### 3. 绝大多数视图未加载 XAML

58 个 XAML code-behind 中，**54 个**没有调用 `InitializeComponent` 或 `AvaloniaXamlLoader.Load`。主编辑器就是空构造器：[FumenVisualEditorView.xaml.cs:7](../src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/Views/FumenVisualEditorView.xaml.cs)。Gekimini 的 [ViewLocator.cs:104](../Dependencies/Gekimini.Avalonia/src/Gekimini.Avalonia/Views/ViewLocator.cs) 只负责实例化控件并设置 `DataContext`，**不会**代为加载 XAML，因此这 54 个视图是实际运行缺口。

### 4. 核心后端仍是占位实现

- Skia 的 SimpleLine、VBO、SVG 使用 `Noop*`：[DefaultSkiaDrawingManagerImpl.cs:32](../src/OngekiFumenEditor.Avalonia/Kernel/Graphics/Skia/DefaultSkiaDrawingManagerImpl.cs)
- OpenGL 的全部绘制接口使用 `Noop*`：[DefaultOpenGLRenderManagerImpl.cs:15](../src/OngekiFumenEditor.Avalonia/Kernel/Graphics/OpenGL/DefaultOpenGLRenderManagerImpl.cs)
- NAudio 加载直接抛出未迁移异常 `NotSupportedException("NAudio backend is not migrated in Avalonia build.")`：[NAudioManager.cs:33](../src/OngekiFumenEditor.Avalonia/Kernel/Audio/NAudioImpl/NAudioManager.cs)
- 主项目通过 **11 条 `<Compile Remove>`** 显式排除了音频、Skia RenderControls、SVG 属性编辑器、拖放和多组 UI 行为/控件源码：[OngekiFumenEditor.Avalonia.csproj:33](../src/OngekiFumenEditor.Avalonia/OngekiFumenEditor.Avalonia.csproj)

### 5. 尚未迁移的完整模块

| 模块 | 旧项目文件数 | 新项目文件数 |
|---|---|---|
| `OptionGeneratorTools` | 44（39 C# + 5 XAML） | 0 |
| `EditorScriptExecutor` | 19（18 C# + 1 XAML） | 0 |
| `OgkiFumenListBrowser` | 10（9 C# + 1 XAML） | 0 |

## 其他风险

### 可复现性风险

当前应用目录有 `832` 个未跟踪文件（其中 `754` 个 C#、`58` 个 AXAML），且工作树领先远端 2 个提交并存在大量未提交修改。这意味着**当前迁移快照尚不能由分支稳定复现**，应尽快形成一个可复现的迁移检查点提交，再继续后续修复。

### 依赖漏洞

构建实际解析到的 `SkiaSharp 2.88.3` 存在 `NU1903` 高严重性漏洞，需升级到无已知漏洞的版本。

## 可复现的检查方法

以下命令均在 `F:\Source\OngekiFumenEditor\Avalonia`（PowerShell）下执行：

```powershell
# 1. 全解决方案构建（验证错误/警告数）
dotnet build 'OngekiFumenEditor.Avalonia.sln' --no-restore -m:1 -v:minimal

# 2. 归并核心项目编译错误按错误码分组
$out = & dotnet build 'src\OngekiFumenEditor.Avalonia\OngekiFumenEditor.Avalonia.csproj' --no-restore -m:1 -v:minimal 2>&1
$out | Select-String 'error \w+:' | Group-Object { ($_ -replace '.*error (\w+):.*','$1') } | Sort-Object Count -Descending

# 3. 统计未加载 XAML 的 code-behind
$root='src\OngekiFumenEditor.Avalonia'
$cb=@(Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object {$_.Name -match '\.(a?xaml)\.cs$' -and $_.FullName -notmatch '\\(bin|obj)\\'})
$with=@($cb | Where-Object { (Get-Content -Raw -Encoding UTF8 $_.FullName) -match 'InitializeComponent\s*\(|AvaloniaXamlLoader\.Load\s*\(' })
"CODE_BEHIND=$($cb.Count) WITH_XAML_LOAD=$($with.Count) WITHOUT=$($cb.Count-$with.Count)"

# 4. 统计 AXAML 中残留的 WPF 语义
$r='src\OngekiFumenEditor.Avalonia'
'Style.Triggers','<DataTrigger','<MultiDataTrigger','<Trigger','<Storyboard','pack://application' | ForEach-Object {
  "{0} = {1}" -f $_, @(rg -n --glob '*.axaml' ([regex]::Escape($_)) $r 2>$null).Count
}

# 5. 旧命名空间残留
rg -n --pcre2 --glob '*.cs' '^namespace OngekiFumenEditor\.(?!Avalonia(?:\.|;))' 'src\OngekiFumenEditor.Avalonia'
```

## 建议修复顺序

1. **形成可复现的迁移检查点**：提交/整理当前未跟踪文件，让迁移快照可由分支复现。
2. **集中消除 83 个 C# 编译错误**：优先处理 `ActionExecutionContext`（56 个）与 `ToolboxItem`（16 个）这两类批量根因。
3. **统一修复 XAML**：清理旧 CLR namespace、`pack://application` 资源加载、视图初始化（`InitializeComponent`），并将 Trigger/Storyboard 重写为 Avalonia 的选择器 / 伪类 / 状态 / Transitions。
4. **实现可用的渲染后端与音频后端**：替换 Skia/OpenGL 的 `Noop*` 占位与 NAudio 未迁移实现。
5. **补齐缺失模块**：`OptionGeneratorTools`、`EditorScriptExecutor`、`OgkiFumenListBrowser`。
6. **补充关键工作流的自动化测试**：当前没有任何测试项目。

## 分阶段验收标准

| 阶段 | 验收标准 |
|---|---|
| A. 可复现基线 | 迁移快照可由分支复现，无游离未跟踪源码 |
| B. 编译闭环 | 全解决方案 Debug 构建 0 错误，可产出 Desktop/Browser 应用 |
| C. UI 可显示 | 视图加载 XAML，主界面能启动并显示，无残留 WPF 语义报错 |
| D. 核心可用 | 谱面渲染与音频后端替换占位实现，核心编辑工作流可用 |
| E. 功能齐备 | 3 个缺失模块补齐，关键工作流具备自动化测试 |


