# Gekimini.Avalonia VisualStudio 主题迁移、兼容与验收计划

> 建立时间：2026-08-02（Asia/Shanghai）
> 工作目录：`F:\Source\OngekiFumenEditor\Avalonia`
> 文档状态：规划完成，等待 G0 实施；本文件是后续实施、验证和交接的唯一主记录
> 当前阶段：只读调查与方案设计已完成，尚未修改主题实现、尚未运行构建或测试

## 文档维护协议

1. 后续每个实现批次开始前，先在“实时进度与操作日志”登记目标、范围和预期门禁。
2. 批次结束后在同一条记录补充实际改动、验证命令、结果、遗留问题和回滚点。
3. 所有设计变更必须写入“决策记录”；不能只存在于聊天、提交信息或个人记忆中。
4. 每个控件的迁移状态、WPF 来源、Avalonia 目标文件和验收证据必须登记到逐控件清单。
5. 新建或重写文本文件使用 UTF-8；移植旧代码时保留原注释、来源说明和许可证信息。
6. 本文档与实现代码同批提交。若实现与本文档冲突，以尚未验收处理，并先修正文档或代码。

## 已确认需求

- 在 `Gekimini.Avalonia` 中新增一个 `IControlTheme`，显示名为 `VisualStudio`。
- `VisualStudio` 的完整控件外观以 WPF 默认风格为兜底，WPF 源码位于 `F:\Source\wpf`。
- VisualStudio 主题提供 Light、Dark、Blue 三个 `IColorTheme`。
- 旧 Gemini/Ongeki VS2013 风格作为 VisualStudio 覆盖层的主要视觉依据。
- 迁移必须覆盖实现、向后兼容、自动化验证、人工视觉验收、跨平台边界、许可证和交接。

## 当前源代码基线

| 来源 | 基线 | 当前确认 |
|---|---|---|
| WPF | `F:\Source\wpf`，提交 `114fbee660df4e981e851cc04a8a557dc7328898` | 无本地改动；MIT；未启用新 Fluent 时默认使用 Aero2 |
| 旧 Gemini | `F:\Source\OngekiFumenEditor\Dependences\gemini`，提交 `1147123f3506e531e71f940f1765d28825f28ae5` | VS2013 Light/Dark/Blue 色板和 WPF 模板来源 |
| Gekimini.Avalonia | `Dependencies\Gekimini.Avalonia`，提交 `b059066d07ea94f0c6fd12edfb810f256ec2202b` | 当前仅有 Fluent `IControlTheme`；颜色实现仍直接替换 `FluentTheme` |
| Dock | Gekimini 子模块内提交 `de3d9270f4be5c5407a6c27c683f131c5b936fbc` | 当前使用 `Dock.Avalonia.Themes.Fluent` 作为结构主题 |
| WindowManager | Gekimini 子模块内提交 `e95c85eadc23a467e1d3f0424e8848f311e8e29c` | 当前 ManagedWindow 结构主题来源 |
| Ongeki Avalonia | 根提交 `e3f3c33e4e8eff6c505b6dab605a28cf19de8e87` 的当前工作树 | 已有其他未提交改动；本任务只新增本文件，不覆盖无关改动 |

工具链快照：.NET SDK `10.0.302`、Avalonia `11.3.10`、Avalonia.Controls.ToolBar `11.3.6`、StatusBar.Avalonia `0.0.2`。实施期间版本变化必须新增决策记录并重跑全部主题门禁。

## WPF 默认兜底基线说明

`F:\Source\wpf` 同时包含 Classic、Aero、Aero2、AeroLite、Luna、Royale 和新 Fluent。WPF 文档明确说明：未启用实验性 Fluent `ThemeMode` 时使用默认 Aero2。因此本计划暂定以下可复现定义：

- “WPF 默认风格”指上述固定提交中的 `Aero2.NormalColor`，不是会随操作系统变化的模糊概念。
- 源码证据是 `Application.ThemeMode` 默认 `None`，Win8+ 将系统 aero 映射到 Aero2；高对比模式则由 WPF 回退 Classic。
- 以 `src\Microsoft.DotNet.Wpf\src\Themes\XAML\*.xaml` 中标记为 `[[Aero2.NormalColor]]` 的分段作为逐控件可维护来源。
- 以生成产物 `src\Microsoft.DotNet.Wpf\src\Themes\PresentationFramework.Aero2\Themes\Aero2.NormalColor.xaml` 做最终比对。
- 不在 Avalonia 运行时加载 WPF XAML；所有兜底模板都按 Avalonia 控件契约重新实现。
- WPF Fluent 可作为以后独立主题候选，但不属于本次 VisualStudio 兜底基线。

## 已发现的架构阻断

- 当前 `FluentColorTheme2<T>` 会查找、移除并重建 `FluentTheme`，颜色主题与控件主题实际没有解耦。
- `MainMenuSettingsViewModel.ApplyChanges()` 先切颜色、后切控件；初始化则先控件、后颜色，主题组合不是原子操作。
- 当前 Light/Dark 名称已被 Fluent 颜色主题占用；VisualStudio 的 Light/Dark 需要稳定 ID 和兼容性规则，不能继续仅按显示名持久化。
- 派生应用的 XAML 不继承 `Gekimini.Avalonia.App` 的资源；Dock、StatusBar、ToolBar、WindowManager 和 DataGrid 主题目前由应用静态加载。
- Dock 与其他第三方主题不受 `IControlTheme` 生命周期管理，完整切换时会出现混合主题。

## 实时进度与操作日志

| 时间 | 批次 | 状态 | 记录 |
|---|---|---|---|
| 2026-08-02 | 建立规划文档 | 完成 | 登记用户需求、维护协议、四个源代码基线和当前主题架构阻断 |
| 2026-08-02 | Avalonia 主题迁移规则核对 | 完成 | 确认采用稳定语义资源键、`DynamicResource`、编译 XAML、明确样式顺序，以及 WPF Trigger 到选择器/伪类的映射 |
| 2026-08-02 | WPF 默认主题确认 | 完成 | 固定 Aero2 来源、48 个生成输入、关键状态映射、不可机械迁移项和许可证要求 |
| 2026-08-02 | Gekimini 挂载点与兼容调查 | 完成 | 确认颜色重建 Fluent、冷/热应用顺序不一致、派生 App 不继承资源和 Name 冲突；确定托管插槽与原子事务方案 |
| 2026-08-02 | 验收能力调查 | 完成 | 确认现有 Headless+Skia/51 视图基础及完整 Shell 缺口；建立 G0-G7、状态、平台、DPI、AOT 和证据矩阵 |
| 2026-08-02 04:35:26 +08:00 | 完整路线收口 | 完成 | 写入目标架构、资源契约、兼容设置、逐控件清单、第三方适配、阶段门禁、验证命令、风险、许可证、回滚和交接规则 |

## 目标、范围和完成定义

### 目标

1. `VisualStudio` 在不依赖 WPF 运行时的情况下，为 Gekimini 提供完整、可切换的 Avalonia 控件主题。
2. 未被 VS2013 覆盖的 Avalonia 核心控件使用重写后的 WPF Aero2 默认结构和状态兜底，不能混入 Fluent 控件模板。
3. Light、Dark、Blue 只负责颜色和主题敏感资源，不创建、删除或替换控件主题。
4. Gekimini、Dock、ToolBar、StatusBar、WindowManager 与 Ongeki 应用资源在运行时只出现一套有效主题；第三方可复用其 Fluent 命名的结构兜底，但验收可见面不得泄漏 Fluent 颜色、密度或重复主题层。
5. 旧设置、旧 Gemini 资源键和现有 Fluent 主题继续可用；任何失败切换都能回滚到上一个已知可用组合。

### 首轮明确不做

- 不恢复 WPF、MahApps 或 AvalonDock 运行时依赖。
- 不在首轮恢复 Dock 浮动行为；当前 `ShellDockFactory` 的 `CanFloat=false` 属于功能策略，不由主题修改。
- 不在首轮实现跨平台自绘主窗口标题栏；Windows 主窗口 chrome 作为独立可选阶段。
- 不追求 WPF 与 Avalonia 逐像素相同；要求控件结构、状态、密度、颜色角色和交互反馈一致。
- 不同时进行 Avalonia 12 或 Dock 大版本升级。主题迁移先固定现有依赖图，升级另开路线。

### 完成定义

只有同时满足以下条件才能把迁移标记为完成：

- VisualStudio + Light/Dark/Blue 三个组合均可冷启动、热切换和持久化恢复。
- 当前产品使用的所有控件均有明确主题所有者，不再依赖偶然的 Fluent 隐式模板。
- WPF 兜底清单、旧 Gemini 187 个颜色资源兼容清单和第三方适配清单全部有状态与证据。
- Gekimini 独立测试、Ongeki 派生 App 集成测试、Windows Desktop、Browser、NativeAOT 和人工视觉门禁全部通过。
- 无未登记的缺失资源、无不可读文字、无键盘焦点丢失、无切换后重复主题对象。
- 许可证、NOTICE、来源提交和保留注释检查完成。

## 决策记录

| ID | 状态 | 决策 | 理由与约束 |
|---|---|---|---|
| D001 | 已确定 | WPF 默认兜底固定为提交 `114fbee...` 的 Aero2 | WPF 在未启用 Fluent `ThemeMode` 时默认使用 Aero2；固定提交可复现 |
| D002 | 已确定 | 只迁移 WPF 结构、状态、密度和资源角色 | Aero2 颜色与模板混排且只适合浅色/系统色，无法直接支持 VS Dark/Blue |
| D003 | 已确定 | `VisualStudio` 是完整 `IControlTheme`，内部为 WpfDefault 基线 + VS 覆盖 + 第三方适配 | 保证未覆盖控件仍有非 Fluent 兜底 |
| D004 | 已确定 | Light/Dark/Blue 是 VisualStudio 主题族的三个 `IColorTheme` | 颜色主题不得操作 `Application.Styles` 中的控件主题 |
| D005 | 推荐实施 | 增加命名空间化稳定 ID，`Name` 只作显示 | `Light`/`Dark` 与现有 Fluent 重名，按 Name 恢复存在歧义 |
| D006 | 推荐实施 | 不破坏现有公开接口，使用可选能力接口承载 ID、兼容族和资源资产 | 允许已有或外部 `IControlTheme`/`IColorTheme` 继续按旧方式工作 |
| D007 | 已确定 | 控件主题和颜色主题必须由 ThemeManager 原子应用 | 当前初始化和设置页的应用顺序不同，会产生混合主题 |
| D008 | 推荐实施 | VisualStudio 首先作为可选主题发布，验收完成后再决定是否设为默认 | 保留现有用户启动和回滚路径 |
| D009 | 已确定 | Blue 使用浅色基础变体和独立蓝色资源包 | 旧 VS2013 Blue 的内容面和输入控件是浅色，不应继承 Dark |
| D010 | 已确定 | 平台高对比是系统保护层，不增加第四个用户可选颜色主题 | 可访问性优先于 VS2013 像素还原 |
| D011 | 推荐实施 | 不修改 Dock 子模块即可实现的适配全部留在 Gekimini | 避免把主题迁移扩散为三层子模块维护；仅在缺少必要状态契约时修改 Dock |
| D012 | 已确定 | 迁移旧源码时保留原注释和许可证头 | 符合仓库迁移约定及 .NET Foundation/Gemini/Wide 来源要求 |

## 目标架构

```text
设置页/冷启动
    |
    v
IThemeManager.ApplyThemeSelection(controlThemeId, colorThemeId)
    |
    +-- 解析稳定 ID、旧 Name 和主题族兼容性
    +-- 离树创建并验证主题资产
    +-- 在 UI 线程原子替换固定主题槽
    +-- 成功后持久化；失败则恢复快照
    |
    v
Application.Styles / Application.Resources
    1. ActiveControlThemeHost
       1.1 WpfDefault Aero2 结构兜底或 Fluent 基础
       1.2 第三方结构主题
       1.3 VisualStudio/Fluent 控件集成覆盖
    2. ActiveColorThemeHost
       2.1 主题族 primitive colors
       2.2 Gekimini 语义资源
       2.3 旧 Gemini、Fluent System、Dock 等兼容别名
    3. Ongeki 应用语义别名和局部控件覆盖
```

### 身份和兼容能力

保留现有 `IControlTheme.Name`、`IColorTheme.Name`、`Apply*()` 和 `Revert*()`，新增非破坏性的可选能力接口。名称可在实现时调整，但能力不能缺失：

```csharp
public interface IThemeIdentity
{
    string Id { get; }
    string FamilyId { get; }
}

public interface IColorThemeCompatibility
{
    IReadOnlySet<string> CompatibleControlThemeIds { get; }
    string PreferredControlThemeId { get; }
    ThemeVariant BaseThemeVariant { get; }
}

public interface IManagedControlThemeAssets
{
    IStyle CreateFoundationStyles();
    IStyle? CreateIntegrationStyles();
}

public interface IManagedColorThemeAssets
{
    ResourceDictionary CreateColorResources();
}
```

稳定 ID 约定：

| 对象 | ID | 显示名 |
|---|---|---|
| Fluent 控件主题 | `gekimini.control.fluent` | `Fluent` |
| VisualStudio 控件主题 | `gekimini.control.visualstudio` | `VisualStudio` |
| VS Light | `gekimini.color.visualstudio.light` | `Light` |
| VS Dark | `gekimini.color.visualstudio.dark` | `Dark` |
| VS Blue | `gekimini.color.visualstudio.blue` | `Blue` |
| 现有 Fluent 颜色 | `gekimini.color.fluent.*` | 保持当前显示名 |

对未实现新能力接口的第三方主题，ThemeManager 使用旧 `Apply/Revert` 兼容路径，但要记录警告，并禁止与声明了专属主题族的颜色主题组合。

### 原子切换算法

1. 根据 ID 查找控件和颜色主题；ID 不存在时尝试旧 Name 迁移。
2. 校验组合兼容性；不兼容时按映射选择该控件主题的默认颜色，不静默保留错误组合。
3. 在修改 `Application` 前创建新样式、新资源字典并执行资源清单验证。
4. 保存旧控件主题、颜色主题、`RequestedThemeVariant`、样式槽和资源槽快照。
5. 在 `Dispatcher.UIThread` 的一次事务中先替换颜色资源槽和基础变体，再替换控件主题槽；同一渲染帧内完成。
6. 验证固定资源键和主题对象唯一性；成功后更新 `Current*Theme` 并持久化 ID/兼容 Name。
7. 任一步异常时移除不完整的新资产，完整恢复快照，记录结构化错误并保留旧设置。
8. `Revert*()` 只能移除自身持有的确切实例，禁止 `FirstOrDefault<FluentTheme>()` 或按类型删除任意主题。

### 样式和资源优先级

顺序从基础到最终覆盖：

1. 控件主题基础：VisualStudio 下为 WpfDefault；Fluent 下为 `FluentTheme`。
2. 第三方结构兜底：DialogHost、Dock、ToolBar、StatusBar、WindowManager；DataGrid 由产品应用适配。
3. 控件主题集成覆盖：VisualStudio 或 Fluent 对第三方控件的颜色、模板和尺寸适配。
4. 活动颜色资源：当前颜色主题的 primitive、semantic 和 compatibility 资源。
5. 产品应用资源：Ongeki 语义别名、自定义 GroupBox/TabControl/CheckComboBox 等。
6. 视图局部样式：只处理业务视图特有布局，不得重新定义全局主题角色。

资源查找顺序必须用测试锁定。任何新增重复键都要在资源清单中声明所有者和预期覆盖方。

### 主题所有权

| 所有者 | 负责内容 | 不负责内容 |
|---|---|---|
| Gekimini | ThemeManager、身份/兼容协议、WpfDefault、VisualStudio 核心控件、Dock/ToolBar/StatusBar/WindowManager/DialogHost 适配、主题示例 | Ongeki 业务控件和编辑器颜色 |
| Ongeki Avalonia | 派生 App 接线、DataGrid、自定义 GroupBox/TabControl/CheckComboBox/RangeValue、编辑器语义别名、集成截图 | 重新实现通用 Button/Menu/Dock 主题 |
| Dock 子模块 | 仅在现有公开控件没有所需伪类、模板部件或可覆盖资源时增加最小契约 | VisualStudio 颜色和产品特有模板 |
| WPF/旧 Gemini | 只读参考与来源基线 | 任何运行时依赖或构建期绝对路径引用 |

### 派生 App 契约

`OngekiFumenEditor.Avalonia.App` 的 XAML 不继承 `Gekimini.Avalonia.App` 资源。实施后必须满足：

- ThemeManager 运行时装载完整 Gekimini 主题包，派生 App 不再重复静态装载同一套 Dock/ToolBar/StatusBar/WindowManager 主题。
- 派生 App 仍显式装载自身转换器、业务资源和 Ongeki 适配包。
- DataGrid 的结构主题与 VisualStudio 覆盖由 Ongeki 适配包负责，并随控件主题切换。
- `Container*`、`ManagedWindow_*` 等资源只能有一个所有者；应用别名必须指向 Gekimini 语义键，不能硬编码颜色。

## 推荐目录和文件职责

```text
Dependencies/Gekimini.Avalonia/
  src/Gekimini.Avalonia/Framework/Themes/
    ThemeIdentity.cs
    ThemeSelection.cs
    IThemeCompatibility.cs
    DefaultImpl/
      DefaultThemeManager.cs
      ThemeAssetHost.cs
      ThemeResourceManifest.cs
      Fluent/
        FluentControlTheme.cs
        FluentColorTheme.cs
      VisualStudio/
        VisualStudioControlTheme.cs
        VisualStudioTheme.axaml
        Palettes/
          VisualStudioLightColorTheme.cs
          VisualStudioDarkColorTheme.cs
          VisualStudioBlueColorTheme.cs
          Light.axaml
          Dark.axaml
          Blue.axaml
        Resources/
          SemanticResources.axaml
          GeminiCompatibility.axaml
          FluentCompatibility.axaml
          Metrics.axaml
          Geometries.axaml
        WpfDefault/Aero2/
          WpfDefaultTheme.axaml
          Controls/*.axaml
        Overrides/
          Controls/*.axaml
        Integrations/
          DialogHost.axaml
          Dock.axaml
          StatusBar.axaml
          ToolBar.axaml
          WindowManager.axaml
  examples/Gekimini.Avalonia.Example/
    Themes/ThemeGalleryView.axaml
  tests/Gekimini.Avalonia.Tests/
    Themes/*.cs

src/OngekiFumenEditor.Avalonia/UI/Themes/
  OngekiThemeAdapter.axaml
  DataGrid.axaml
  GroupBox.axaml
  TabControl.axaml
  EditorThemeResources.axaml

tests/OngekiFumenEditor.Avalonia.Tests/UI/Themes/
  ThemeApplicationTests.cs
  ThemeResourceContractTests.cs
  ThemeRenderTests.cs
```

所有跨程序集加载的 XAML 类型必须是公开、编译 XAML 可达的类型，URI 使用 `avares://`；不允许运行时解析本地文件路径。

## 颜色与资源契约

### 分层原则

1. Primitive 层只存 `Color`、数值、厚度、圆角、字体和几何，不引用具体控件。
2. Semantic 层表达窗口、工具窗口、控件、输入、选择、焦点、菜单、工具栏、状态栏和 Dock 的视觉角色。
3. Control 层只通过 `DynamicResource` 使用 semantic 资源，禁止写主题敏感的十六进制颜色。
4. Compatibility 层提供旧 Gemini、Avalonia Fluent System、Dock、WindowManager 和 Ongeki 旧键映射。
5. Light/Dark/Blue 必须实现完全相同的资源清单；缺一项即不得注册该颜色主题。

### Gekimini 最小公共语义契约

以下键是应用和第三方适配可依赖的稳定公共入口。实现时可增加更细角色，但不得删除或改变类型。

| 分类 | 必需资源键 |
|---|---|
| 原色 | `Gekimini.Theme.Color.Accent`、`Gekimini.Theme.Color.Error` |
| 窗口 | `Gekimini.Theme.Brush.WindowBackground`、`WindowForeground`、`WindowBorder` |
| 工具窗口 | `Gekimini.Theme.Brush.ToolWindowBackground`、`ToolWindowForeground`、`ToolWindowDisabledForeground` |
| 普通控件 | `Gekimini.Theme.Brush.ControlBackground`、`ControlForeground`、`ControlBorder` |
| 控件状态 | `ControlHoverBackground`、`ControlHoverBorder`、`ControlPressedBackground`、`ControlPressedForeground`、`ControlDisabledBackground`、`ControlDisabledForeground` |
| 输入 | `Gekimini.Theme.Brush.InputBackground`、`InputForeground`、`InputBorder`、`InputFocusBorder`、`Caret` |
| 弹出层 | `Gekimini.Theme.Brush.PopupBackground`、`PopupForeground`、`PopupBorder`、`PopupShadow` |
| 选择和焦点 | `Gekimini.Theme.Brush.SelectionActive`、`SelectionActiveForeground`、`SelectionInactive`、`SelectionInactiveForeground`、`Focus` |
| Shell | `Gekimini.Theme.Brush.MenuBackground`、`ToolbarBackground`、`StatusBackground`、`StatusForeground` |
| Dock | `Gekimini.Theme.Brush.DockDocumentActive`、`DockDocumentInactive`、`DockToolActive`、`DockBorder` |
| 指标 | `Gekimini.Theme.Metric.FontSize`、`SmallFontSize`、`ControlMinHeight`、`BorderThickness`、`CornerRadius` |
| 字体 | `Gekimini.Theme.FontFamily`、`Gekimini.Theme.SymbolFontFamily` |

### VisualStudio 内部角色

旧 Gemini 的三个色板各含 187 个同名画刷。迁移时建立一个版本化 `VisualStudioResourceManifest`：

- Menu、Menu Popup、Top Level Header。
- Toolbar、Toolbar Button、Overflow。
- Main Window、Caption、Caption Button。
- ToolTip、Environment、History、Expander、StatusBar。
- TreeView、TreeViewItem、Button、CheckBox、TextBox、Slider。
- ColorCanvas、ColorPicker、ScrollBar、ComboBox、Label、Focus。

所有 187 个键在 Light/Dark/Blue 中必须一一存在。新模板可以使用带 `VisualStudio.*` 前缀的内部角色，但 Compatibility 字典必须继续公开旧键，例如 `EnvironmentWindowBackground`、`MenuPopupHoveredItemBackground` 和 `Button.Static.Background`。

### 现有兼容键

| 兼容族 | 首轮必须覆盖 |
|---|---|
| 旧 Gemini | 三个旧色板的全部 187 个键；精确键名和资源类型不变 |
| 当前 Fluent System | `SystemAccentColor`、`SystemAltHighColor`、`SystemBaseHighColor`、`SystemBaseMediumLowColor`、`SystemBaseMediumLowColorBrush`、`SystemChromeGrayColor`、`SystemRegionColor`、`SystemControlBackgroundBaseLowBrush`、`SystemControlErrorTextForegroundBrush`、`SystemControlForegroundBaseHighBrush` |
| Dock | `RegionColor` 及现有 `DockApplication*`、`DockTheme*`、`DockFontSizeNormal` 资源 |
| WindowManager | 所有当前 `ManagedWindow_*` 画刷 |
| Ongeki | `EditorWindowBackgroundBrush`、`EditorToolWindowForegroundBrush`、`EditorInteractionAccentBrush` 以及仍需保留的旧 Gemini 别名 |

兼容资源只能指向公共语义键或 VisualStudio 内部角色，不允许再复制一份颜色。`RegionColor` 与 `SystemRegionColor` 必须同时存在，解决 Dock 当前使用裸 `RegionColor` 的差异。

### 三套颜色主题

| 主题 | 基础变体 | 色板来源 | 特殊要求 |
|---|---|---|---|
| Light | `ThemeVariant.Light` | 旧 Gemini `LightTheme.xaml` | 浅色 WpfDefault 与 VS Light 覆盖均不得出现 Fluent 圆角/颜色泄漏 |
| Dark | `ThemeVariant.Dark` | 旧 Gemini `DarkTheme.xaml` | 所有文本、弹出层、禁用和非活动选择必须重新验证对比度 |
| Blue | `ThemeVariant.Light` | 旧 Gemini `BlueTheme.xaml` | 蓝色主要用于 Shell，内容区仍按浅色语义；不得简单对 Light 做色相滤镜 |

字体和密度三套配色共用，不在色板重复。Windows 优先使用接近 WPF MessageFont 的 `Segoe UI`，其他平台回退现有 Avalonia 字体；最终选择必须经可覆盖字体资源，而不是控件硬编码。

平台高对比激活时，应用应临时用系统高对比语义资源覆盖当前颜色槽；用户设置仍保留原 Light/Dark/Blue，退出高对比后恢复。具体平台 API 在实现前以 Avalonia 11.3 实际能力验证，不增加第四个可选 `IColorTheme`。

## WPF 到 Avalonia 映射规则

### 来源处理

- 手工迁移输入：`F:\Source\wpf\src\Microsoft.DotNet.Wpf\src\Themes\XAML\*.xaml`。
- 生成规则：`ThemeGenerator.proj` 中 48 个控件源，按 `[[Aero2.NormalColor]]` 标签抽取。
- 只读比对产物：`PresentationFramework.Aero2\Themes\Aero2.NormalColor.xaml`，约 9592 行、136 个 Style、140 个 ControlTemplate、345 个 Trigger 类节点、48 个 VisualState、217 个 DynamicResource。
- 禁止以生成产物中的压缩字符键作为新代码名称；每个新文件记录 WPF 源文件、提交和迁移说明。

`ThemeGenerator.proj` 当前登记的 48 个输入文件如下，G0 逐控件清单不得漏项：

```text
BrowserWindow, Button, Calendar, CheckBox, CollectionViewGroup, ComboBox,
ComboBoxItem, ContentControl, ContextMenu, DocumentViewer, DataGrid,
DatePicker, Expander, FocusVisual, Frame, GridSplitter, GroupBox, GroupItem,
HeaderedContentControl, Hyperlink, ItemsControl, Label, ListBox, ListBoxItem,
ListView, ListViewItem, Menu, MenuItem, NavigationWindow, Page, ProgressBar,
RadioButton, ResizeGrip, ScrollBar, ScrollViewer, Separator, Slider,
StatusBar, TabControl, TabItem, TextBox, Thumb, ToolBar, ToolTip, TreeView,
TreeViewItem, UserControl, Window
```

“不迁移”也必须在清单中写明理由和替代关系，不能从清单直接删除。

### 状态映射

| WPF | Avalonia 目标 | 验收重点 |
|---|---|---|
| `IsMouseOver` | `:pointerover` | 鼠标进入/离开无布局跳动 |
| `IsPressed` | 控件实际提供的 `:pressed` | 按下、释放、移出后恢复 |
| `IsEnabled=False` | `:disabled` | 前景、边框、图标均可读 |
| `IsKeyboardFocused` | `:focus-visible`，输入控件必要时配合 `:focus` | 只用键盘时焦点始终可见 |
| `IsKeyboardFocusWithin` | `:focus-within` 或经测试确认的等价类 | Popup/复合控件焦点不丢失 |
| `IsChecked`/null | `:checked`/`:indeterminate` | 三态 CheckBox 与 ToggleButton |
| `IsSelected` | `:selected` | 活动与非活动选择必须区分 |
| `IsExpanded` | `:expanded` | TreeView/Expander 箭头和内容 |
| `IsDropDownOpen`/`IsSubmenuOpen` | 控件真实伪类或显式 class | Popup 生命周期、轻点关闭、焦点返回 |
| MultiTrigger | 组合选择器或显式状态 class | 禁止在 code-behind 重建通用视觉状态机 |
| DataTrigger | 绑定到 `Classes.*` | 类名表达业务/状态语义 |
| Orientation/TabStripPlacement | 属性选择器或分离 ControlTheme | 四个方向都要布局验证 |
| Storyboard/VSM | 伪类 + Transition/Animation | 非关键装饰动画可延期，但状态不能丢失 |

所有伪类必须通过 Avalonia 11.3 控件实际行为或源码确认，不能只因名称相似而假设存在。

### 必须重设计的 WPF 机制

- `SystemColors`、`SystemFonts`、`SystemParameters` 改为 Gekimini 语义资源和平台字体/指标适配。
- `ComponentResourceKey` 改为稳定字符串键或类型键。
- `SystemDropShadowChrome`、`DataGridHeaderBorder`、`ListBoxChrome` 改为 Avalonia Border/Path/BoxShadow。
- `AdornerDecorator` 改为 Avalonia AdornerLayer 或局部覆盖层；无实际需求时不移植。
- `FindAncestor` 与 WPF PART 名称按 Avalonia 控件契约重写。
- GroupBox 的 `BorderGapMaskConverter`/MultiBinding 改为简单、可测的头部与边框布局。
- Popup、ContextMenu、Window chrome 和虚拟化只采用 Avalonia 原生生命周期，不仿造 WPF 内部实现。

## 迁移清单和优先级

### WpfDefault 核心兜底

| 优先级 | 控件族 | WPF 来源 | 目标与状态要求 |
|---|---|---|---|
| P0 | Button、RepeatButton、ToggleButton | `Button.xaml` | normal/hover/pressed/disabled/focus/default/cancel，1px 边框，内容驱动尺寸 |
| P0 | CheckBox、RadioButton | `CheckBox.xaml`、`RadioButton.xaml` | checked/unchecked/indeterminate、hover/pressed/disabled/focus |
| P0 | TextBox、PasswordBox | `TextBox.xaml` 及共享输入结构 | hover/focus/error/readonly/disabled、选择、Caret、滚动 |
| P0 | ComboBox、ComboBoxItem | `ComboBox*.xaml` | editable/non-editable、Popup、选中、方向键、禁用 |
| P0 | Label、TextBlock | `Label.xaml` 与 WPF 元数据语义 | AccessKey、Target、disabled、文本不裁切 |
| P0 | Menu、MenuItem、ContextMenu | `Menu*.xaml`、`ContextMenu.xaml` | 顶级/子菜单、图标、勾选、手势、分隔、禁用、Popup |
| P0 | ScrollViewer、ScrollBar | `ScrollViewer.xaml`、`ScrollBar.xaml` | 两个方向、Thumb/RepeatButton、拖动、滚轮、隐藏策略 |
| P0 | Focus、Popup 基础 | `FocusVisual.xaml` 及相关文件 | `:focus-visible`、弹出层阴影/边框/关闭 |
| P1 | ItemsControl、ListBox/Item、ListView/Item | 对应 WPF 文件 | 虚拟化不退化、活动/非活动选择、键盘多选 |
| P1 | TreeView/Item | `TreeView*.xaml` | 展开、层级缩进、虚拟化、活动/非活动选择、键盘 |
| P1 | TabControl/TabItem | `TabControl.xaml`、`TabItem.xaml` | 四个 TabStripPlacement、选中、禁用、内容不重建 |
| P1 | Expander、GroupBox、GridSplitter | 对应 WPF 文件 | 四方向、焦点、拖动；GroupBox 重设计边框缺口 |
| P1 | Slider、Thumb、Separator | 对应 WPF 文件 | 横纵、Tick、拖动、键盘、禁用 |
| P1 | ProgressBar、ResizeGrip | 对应 WPF 文件 | determinate/indeterminate、缩放和命中区域 |
| P2 | Calendar、DatePicker | 对应 WPF 文件 | 低频，先保留结构兜底，再做完整状态 |
| 跳过 | BrowserWindow、NavigationWindow、Frame/Page、DocumentViewer/FlowDocument | WPF 专有工作流 | 当前产品不用或无直接等价，不进入 VisualStudio 完成门禁 |

当前静态使用频次支持上述顺序：Button 84、CheckBox 75、Label 67、TextBox 59、GroupBox 30、MenuItem 24、ItemsControl 20、ComboBox 16、ScrollViewer 14；后续以生成的控件使用清单为准。

### VisualStudio 覆盖层

旧 Gemini `Controls` 有 17 个 XAML 文件，其中 `Merged.xaml` 负责汇总。迁移分组如下：

```text
Button, CheckBox, ColorCanvas, ComboBox, Focus, Label, Menu, Merged,
ScrollBar, Slider, TextBlock, TextBox, Toolbar, Tooltip, TreeView, Window,
WindowCommands
```

| 批次 | 覆盖 |
|---|---|
| VS0 | Focus、Label、TextBlock、Button、TextBox、CheckBox |
| VS1 | ComboBox、ScrollBar、Slider、ToolTip、TreeView |
| VS2 | Menu、Toolbar、StatusBar 资源、普通 TabControl |
| VS3 | Dock 文档/工具页签、工具标题、关闭/固定按钮、活动/非活动状态 |
| VS4 | ManagedWindow、可选 Windows 主窗口 chrome |
| 延期/重设计 | ColorCanvas、ColorPicker、WindowCommands；只有当前功能真实使用时才实现 |

旧 Gemini 的 `Window.xaml` 依赖 MahApps，不作为 Avalonia Window 模板输入；只保留标题色、边框、窗口按钮状态等视觉角色。

### 第三方适配

| 组件 | 结构兜底 | VisualStudio 适配策略 | 是否允许改依赖子模块 |
|---|---|---|---|
| Dock | 现有 `DockFluentTheme` | 先映射语义资源，再覆盖 DocumentTab、ToolTab、ToolChrome、DockTarget 等高价值 ControlTheme | 默认否；缺伪类/PART 时单独审批 |
| ToolBar | `Avalonia.Controls.ToolBar` 自带主题 | 覆盖 Tray、Grip、Separator、Button、ToggleButton、Overflow | 默认否 |
| StatusBar | `StatusBar.Avalonia` | 结构沿用，背景、前景、高度和状态项间距使用语义资源 | 默认否 |
| WindowManager | 现有 `WindowManagerTheme` | 映射 `ManagedWindow_*`，必要时覆盖标题按钮和边框 | 默认否 |
| DialogHost | 现有结构主题 | 只适配 Surface、Overlay、焦点和按钮 | 默认否 |
| DataGrid | Ongeki 当前 Fluent DataGrid 主题 | 由应用适配 Row/Header/Cell/Selection/Edit/Error，不增加 Gekimini 不需要的依赖 | 否 |

### Ongeki 应用适配

- 清理 `MediumPurple`、`WhiteSmoke`、硬编码 White 等主题泄漏，区分功能色与主题色。
- `EditorThemeResources.axaml` 改为公共 Gekimini 语义键的别名，Blue 不依赖 Avalonia Light/Dark ThemeDictionary 推断。
- 恢复普通 TabControl/TabItem 的 VS 外观，但以 Avalonia/WpfDefault 控件契约重写旧 Style Snooper 模板。
- GroupBox、CheckComboBox、RangeValue、DataGrid 和 DialogButton 使用产品适配层，不反向耦合 Gekimini 核心主题。
- 移除派生 App 中与活动主题包重复的静态主题装载；保留产品业务资源。

## 兼容和设置迁移

### 允许的主题组合

| 控件主题 | 允许的颜色主题 | 默认颜色 | 说明 |
|---|---|---|---|
| Fluent | 现有 `Default`、`Light`、`Dark`、`LavenderLight`、`LavenderDark` | 保持现有默认 | 由重构后的稳定 Fluent 实例消费 palette，不再由颜色主题替换实例 |
| VisualStudio | `VisualStudio.Light`、`VisualStudio.Dark`、`VisualStudio.Blue` | `VisualStudio.Light` | 设置页显示为 Light/Dark/Blue，通过主题族过滤避免重名 |
| 未声明兼容能力的外部主题 | 仅允许未声明专属主题族的旧颜色主题 | 外部主题自己的首选项或显式回退 | 记录兼容警告，不允许与 VisualStudio 专属颜色混用 |

设置页必须根据待选 `IControlTheme` 显示兼容颜色列表。用户切换控件主题时：

- 优先恢复该控件主题上次使用的颜色 ID。
- 没有历史值时按明暗语义映射；Fluent Dark/LavenderDark -> VS Dark，其他 Fluent 颜色 -> VS Light。
- VS Blue 切回 Fluent 时使用 Fluent Light；不得把 Blue 的显示名误当成未知并依赖 DI 注册顺序。
- 点击应用时只调用一次 `ApplyThemeSelection`，不能分别设置两个属性。

### 设置模型迁移

建议在 `GekiminiSetting` 增加：

- `ControlThemeId`
- `ColorThemeId`
- `Dictionary<string, string> LastColorThemeIdByControlThemeId`

现有 `ControlThemeName` 和 `ColorThemeName` 至少保留一个兼容版本。读取顺序：

1. 新稳定 ID 存在且组合有效时直接使用。
2. 只有旧 Name 时，先依据 `ControlThemeName` 确定主题族，再在该族内解析 `ColorThemeName`。
3. `Default`、`LavenderLight` 映射到相应族的 Light；`LavenderDark` 映射到 Dark。
4. 未知控件主题回退 Fluent；未知颜色回退所选控件主题的显式默认颜色。
5. 成功应用后回写稳定 ID，同时保留兼容 Name；失败时不覆盖旧设置。

旧的 `Avaliable*` 和 `Initalize()` 拼写若要修正，应新增正确成员并保留 `[Obsolete]` 转发，不能在主题迁移中顺带制造源兼容破坏。

### 运行时失败处理

- 冷启动主题加载失败：记录失败 ID 和异常，回退 `Fluent + Light`，主应用继续启动并提示可诊断信息。
- 热切换失败：恢复上一主题对，不更新设置，不保留半应用的样式或资源。
- 资源清单缺失：主题注册阶段直接拒绝，不等某个页面打开后才暴露。
- 重复 ID 或同主题族重复显示名：DI 构造后初始化前即失败，并输出冲突实现类型。
- 提供 Desktop 安全启动入口，例如启动参数或环境设置强制 `Fluent + Light`；具体入口在 G1 实现并记录。

## 分阶段实施和验收门

每个阶段必须独立提交、独立验证并更新实时日志。除紧急阻断修复外，不跨阶段混合大范围业务改动。

### G0：冻结基线和迁移清单

任务：

- [ ] 记录根仓、Gekimini、Dock、WindowManager、WPF、Gemini 的提交、dirty 状态、SDK 和 Avalonia 版本。
- [ ] 固定 WPF 48 个源文件清单，字段包含源文件、TargetType、PART、Trigger/VSM、Avalonia 目标、差异、测试、截图、状态。
- [ ] 固定旧 Gemini 17 个控件 XAML、三个 187 键色板及旧截图清单。
- [ ] 生成当前 AXAML 控件使用清单和所有主题敏感硬编码颜色清单。
- [ ] 捕获当前 Fluent 的 Shell、51 个视图 smoke、Desktop 和 Browser 基线，作为回滚证据。
- [ ] 建立许可证/NOTICE 计划，确认 Gekimini 自身许可证状态。

退出条件：所有输入可由固定提交复现；清单中没有“来源未知”的控件或资源；基线命令和 artifact 可重跑。

### G1：主题身份、托管插槽和原子切换

任务：

- [ ] 增加稳定 ID、主题族和兼容能力接口，不破坏旧公开接口。
- [ ] 新增 `IApplicationThemeLayerHost`/等价托管器，固定 Foundation、Integration、Color 插槽。
- [ ] 重写 `DefaultThemeManager` 为显式原子事务，保证 UI 线程、幂等和失败回滚。
- [ ] 停用 `FluentColorTheme2<T>` 重建 `FluentTheme` 的行为；Fluent 控件主题只拥有一个稳定实例。
- [ ] 新增 `ApplyThemeSelection`，统一冷启动、属性兼容 setter 和设置页路径。
- [ ] 增加设置 ID、旧 Name 迁移、兼容颜色过滤和每主题族上次选择。
- [ ] 合并 Gekimini/派生 App 的基础设施装载契约，消除重复清单漂移。
- [ ] 新建 Gekimini 主题测试工程并加入 Gekimini solution。

退出条件：现有 Fluent 视觉无计划外变化；所有合法转换循环 100 次后托管层数量和对象唯一性稳定；非法组合、异常注入和旧设置迁移测试通过。

### G2：WpfDefault Aero2 完整兜底

任务：

- [ ] 创建编译 XAML 的 WpfDefault Foundation、指标、字体、几何和基础语义资源。
- [ ] 首个垂直切片实现 Button、CheckBox、TextBox、ComboBox、Menu 和焦点，验证迁移模式。
- [ ] 完成 P0 控件族，再完成 P1；P2 根据实际产品使用补齐。
- [ ] 每个控件登记来源、保留注释、PART 差异、伪类和已知偏差。
- [ ] 建立明确 unsupported 清单，禁止未迁移控件静默回退 Fluent。
- [ ] 在完全不加载 `FluentTheme` 的测试 App 中实例化、应用模板、测量和交互。
- [ ] 验证虚拟化、Popup、键盘导航和输入行为没有因模板重写退化。

退出条件：当前使用的 Avalonia 核心控件均可在 WpfDefault 下独立运行；没有 WPF 程序集、本地绝对路径或未解析资源；P0/P1 状态测试通过。

### G3：VisualStudio 核心覆盖与三套色板

任务：

- [ ] 从旧 Gemini 三个色板提取同构 187 键清单，建立 canonical/compatibility 映射。
- [ ] 实现 Light、Dark、Blue primitive + semantic 资源包；Blue 基础变体为 Light。
- [ ] 实现 Fluent System、Dock、WindowManager、Gekimini 和旧 Gemini 兼容字典。
- [ ] 实现 VS0、VS1、VS2 核心控件覆盖，所有颜色通过 `DynamicResource`。
- [ ] 清除 `Button.Static.Background` 等悬空键，同时保留旧键兼容入口。
- [ ] 验证在已打开控件、Popup 和 ContextMenu 存在时热切换三套颜色。
- [ ] 增加平台高对比保护路径，不添加第四个用户颜色选项。

退出条件：三套 palette 的键和类型完全一致；0 个缺失资源；现有视觉树实时更新；Dark/Blue 无 Aero2 浅色泄漏。

### G4：Shell 与第三方控件适配

任务：

- [ ] 建立统一 Infrastructure 主题入口，按活动控件主题装载结构主题和集成覆盖。
- [ ] 适配 Menu/ContextMenu、ToolBar/Overflow、StatusBar 和 DialogHost。
- [ ] 先用资源映射适配 Dock，再覆盖文档页签、工具页签、ToolChrome、DockTarget 和 HostWindow。
- [ ] 适配 ManagedWindow 的活动/非活动、Modal、窗口状态和标题按钮。
- [ ] 验证第三方模板的选择器确实命中，特别是 ManagedWindow 的 PART/层级和 Dock 组合伪类。
- [ ] 只有测试证明现有 Dock 缺少必需状态契约时，才创建最小 Dock 子模块变更。

退出条件：完整 Gekimini Shell 在 Light/Dark/Blue 下无混合 Fluent 表面；菜单、溢出、Dock 命令、拖放属性和窗口命令保持可用。

### G5：Ongeki 派生应用集成

任务：

- [ ] 修改派生 `App.axaml`，移除与活动主题包重复的静态主题加载。
- [ ] 将 Editor、Container、ManagedWindow 资源改为 Gekimini 公共语义别名。
- [ ] 清理 `MediumPurple`、`WhiteSmoke` 和非功能性硬编码前景/背景。
- [ ] 实现 Ongeki DataGrid、GroupBox、普通 TabControl、CheckComboBox、RangeValue 和 DialogButton 适配。
- [ ] 对 51 个现有视图分别在 VS Light/Dark/Blue 下构造、挂载、布局和资源检查。
- [ ] 构造真实 Shell 夹具，覆盖 Menu、Toolbar、Dock、StatusBar 和 Welcome ManagedWindow。
- [ ] 通过设置页保存主题，重建应用/ThemeManager 并验证同一组合恢复。

退出条件：Ongeki 51 视图 × 3 配色全部通过；完整 Shell 截图批准；派生 App 不再依赖重复主题列表或 Fluent 特有键。

### G6：视觉、平台、可访问性和性能硬化

任务：

- [ ] 建立固定字体/Skia/CI 镜像下的三配色批准基线。
- [ ] Windows 验证 100%、125%、150%、200% DPI；Browser 验证宽屏和移动窄屏 viewport。
- [ ] 检查 normal/hover/pressed/disabled/checked/selected/open/focus/error 全状态。
- [ ] 验证 en-US、zh-Hans 的菜单、页签、按钮和设置页无裁切或重叠。
- [ ] 验证仅键盘操作、AccessKey、Tab 顺序、Popup 焦点返回和 `:focus-visible`。
- [ ] 检查正文、禁用、选择和焦点对比度；可访问性冲突时优先修正，不机械保留旧色值。
- [ ] 对比主题初始化、热切换、布局和虚拟化基线；循环切换后样式层和可回收对象不增长。
- [ ] 增加真实 Browser 自动化或把缺失项保留为发布阻断，publish 成功不能替代渲染验收。

退出条件：固定环境视觉差异门通过；所有目标 DPI/viewport 无布局问题；键盘和高对比无阻断；性能无未批准回归。

### G7：发布、文档和切换策略

任务：

- [ ] 执行 Gekimini Desktop/Browser 示例、Ongeki Desktop JIT/AOT 和 Browser Release 全矩阵。
- [ ] 验证 NativeAOT 冷启动、主题切换、重启恢复、进程退出码和 fatal 日志。
- [ ] 完成许可证、NOTICE、逐文件来源注释和 WPF/Gemini 提交记录。
- [ ] 关闭或接受所有风险/兼容偏差，链接最终验证 artifact。
- [ ] 先以 opt-in 方式发布 VisualStudio，保留 Fluent 安全回退。
- [ ] 另行记录是否把 VisualStudio Light 设为新安装默认的产品决定。
- [ ] 分别提交 Gekimini 子模块改动和父仓 gitlink，文档记录两个提交。

退出条件：G0-G7 证据齐全、无阻断风险、交接清单可由未参与实现的人复跑；主题默认值变更必须有单独决策。

## 自动化和人工验收设计

### 当前可复用测试基础

- 产品测试已使用 `Avalonia.Headless.XUnit`、`Avalonia.Skia`，并设置 `UseHeadlessDrawing=false`，可做真实 Skia 截帧。
- `AxamlSmokeTests` 已覆盖 51 个产品视图的构造、挂载和布局，并已有应用资源断言入口。
- `SkiaRenderSmokeTests` 已示范 `CaptureRenderedFrame()` 和像素断言。
- Desktop 已有 JIT/AOT publish profile；Browser Release 已启用裁剪/AOT相关配置。
- 当前产品测试宿主绕过完整 Shell 和 ThemeManager 冷启动，不能代替主题集成验收。
- Gekimini solution 当前没有测试项目，必须新增主题包独立测试，防止产品 App 的资源掩盖库内缺失。

### 新增测试分层

| 层 | 项目 | 负责证明 |
|---|---|---|
| T1 | `Gekimini.Avalonia.Themes.Tests` | 主题注册、身份、兼容、原子事务、资源清单、WpfDefault 控件、第三方适配、主题 Gallery |
| T2 | 现有 `OngekiFumenEditor.Avalonia.Tests` | 派生 App 资源、51 视图 × 3 配色、产品自定义控件、真实 Shell 夹具 |
| T3 | Gekimini Desktop/Browser 示例 | 库单独使用时的启动、主题设置和跨平台资源打包 |
| T4 | Ongeki Desktop/Browser | 产品冷启动、真实 Popup/Window、设置持久化、JIT/AOT/Browser |
| T5 | 人工视觉与可访问性检查 | 旧 VS2013 视觉意图、中文排版、DPI、键盘、高对比和难以稳定自动化的窗口行为 |

Gekimini 测试项目使用 `net10.0`、`Avalonia.Headless.XUnit`、`Avalonia.Skia`、`UseHeadlessDrawing=false`，并禁用程序集并行，避免全局 `Application.Current` 和样式集合互相污染。

### 核心主题事务测试

至少实现以下测试；名称可按仓库约定调整，语义不得删除：

- `VisualStudioControlTheme_Registration_IsUnique`
- `VisualStudioColorThemes_HaveUniquePersistentIds`
- `ThemeManager_ColdStart_AppliesPersistedPair`
- `ThemeManager_LegacyNames_MigrateWithinSelectedFamily`
- `ThemeManager_InvalidPair_FallsBackDeterministically`
- `ThemeManager_AllTransitions_KeepOneControlAndOneColorLayer`
- `ThemeManager_OneHundredCycles_DoesNotLeakStyleLayers`
- `ThemeManager_ApplyFailure_RestoresPreviousSnapshot`
- `ThemeManager_RepeatedApply_IsIdempotent`
- `ThemeManager_OnlyMutatesOnUiThread`
- `ThemeSwitch_UpdatesExistingVisualTree`
- `ThemeSwitch_OpenPopupAndContextMenu_RemainUsable`

切换矩阵必须覆盖所有合法有向转换，而不只是 Light -> Dark：

```text
Fluent 各现有颜色 <-> VisualStudio Light/Dark/Blue
VisualStudio Light <-> Dark
VisualStudio Light <-> Blue
VisualStudio Dark <-> Blue
每条转换重复执行，并包含相同组合重复 Apply
```

每次切换断言：托管槽数量、确切对象引用、`RequestedThemeVariant`、当前 ID、资源值、设置值、日志中资源错误均符合预期。

### 资源契约测试

- 三个 VisualStudio palette 的键集合和资源类型完全相同。
- 旧 Gemini 187 键在三个 palette 下均可解析。
- 公共 Gekimini semantic、当前 Fluent System、Dock、ManagedWindow、Ongeki 兼容键均可解析。
- 扫描编译后的主题 XAML 中所有 `DynamicResource`，在每个合法组合下逐一解析。
- 检查颜色主题没有增加或删除 `FluentTheme`、VisualStudio Foundation 或第三方结构主题。
- 检查 ControlTheme 文件没有主题敏感的直接十六进制颜色；功能色、透明色和几何常量必须列白名单并注明原因。
- 检查运行产物和项目文件不存在 `F:\Source\wpf`、WPF 程序集或 `pack://application` 依赖。

### WpfDefault 控件状态矩阵

| 控件族 | 必测状态/行为 |
|---|---|
| Button/RepeatButton/ToggleButton | normal、pointerover、pressed、disabled、checked、focus-visible、default/cancel |
| CheckBox/RadioButton | unchecked、checked、indeterminate、pointerover、pressed、disabled、focus-visible |
| TextBox/PasswordBox | empty/content、focus、selection、caret、readonly、disabled、validation error、滚动 |
| ComboBox | editable/non-editable、closed/open、selected、pointerover、disabled、键盘选择、Popup 焦点返回 |
| ListBox/ListView | normal、pointerover、selected-active、selected-inactive、disabled、键盘/多选、虚拟化 |
| TreeView | collapsed/expanded、层级缩进、selected-active/inactive、pointerover、disabled、键盘 |
| TabControl/TabItem | top/bottom/left/right、normal、selected、pointerover、disabled、焦点、动态增删页签 |
| Menu/ContextMenu | 顶级、子菜单、图标、gesture、checked/radio、separator、disabled、键盘、轻点关闭 |
| Slider/ScrollBar | 横向/纵向、min/mid/max、drag、pointerover、pressed、disabled、键盘 |
| ProgressBar | min/mid/max、indeterminate、disabled |
| ToolTip/Popup | placement、边界翻转、阴影/边框、长文本、关闭和主题中途切换 |
| GroupBox/Expander/GridSplitter | 长标题、disabled、四方向展开、拖动、窄宽度 |
| Window/ResizeGrip | active/inactive、可调整/不可调整、最小尺寸；原生 chrome 不纳入首轮像素门 |

所有状态同时在 Light、Dark、Blue 下运行。行为断言和视觉断言分开，避免截图通过却交互失效。

### Shell 和第三方状态矩阵

Dock：

- Document tab：普通、选中但非活动、活动选中、hover、修改标记、关闭按钮 hover、ContextMenu。
- Tool tab：普通、选中、hover、disabled。
- ToolChrome：`:active`、`:floating`、`:pinned`、`:maximized` 及实际可达组合。
- HostWindow：document/tool、dragging、active/inactive；当前产品禁用浮动时用独立 Dock 夹具覆盖。
- DockTarget：Top/Bottom/Left/Right/Fill、selector、indicator、不同 DPI 和资源图片清晰度。
- Splitter、上下文命令、拖放附加属性在换主题后不得失效。

ToolBar：

- Button/ToggleButton 的 normal、hover、pressed、disabled、checked。
- `IsOverflowOpen`、`HasOverflowItems`、`OverflowMode` 和溢出 Popup。
- ToolBarTray 的 Orientation、Band/BandIndex、IsLocked、Grip 和 Separator。

StatusBar：

- 背景、前景、item hover/pressed/disabled/hidden。
- left/center/right 对齐、临时消息、tooltip、icon、自定义前景/背景。
- 当前强制子 Border 使用 accent 的局部样式必须改为语义资源，并由测试证明三个色板可读。

ManagedWindow：

- active/inactive、normal/minimized/maximized、fixedsize、modal、noborder、notitle。
- 系统菜单以及最小化、最大化、恢复、关闭按钮。
- 自动测试必须证明模板选择器真实命中，不能只断言 `ManagedWindow_*` 资源存在。

### Ongeki 集成矩阵

- 51 个参数无参视图分别在 VisualStudio Light/Dark/Blue 下构造、显示、完成布局。
- 每个视图断言非零尺寸、无资源异常、主要文字前景/背景非同色。
- 单独覆盖 DataGrid 编辑/排序/列调整、GroupBox、TabControl、CheckComboBox、RangeValue、DialogButton。
- 完整 Shell 覆盖菜单、工具栏、Dock 文档与工具窗、状态栏、Welcome ManagedWindow。
- 打开 Popup、ContextMenu、ManagedWindow 后切换主题，既有可视树立即更新。
- 通过设置页选择主题、保存、销毁服务、重新创建应用后恢复同一稳定 ID。
- 编辑器画布和谱面功能色不被主题色误覆盖；功能色需有白名单和可读文字检查。

### 平台和发布矩阵

| 环境 | Light | Dark | Blue | 门禁重点 |
|---|---:|---:|---:|---|
| Gekimini Headless/Skia | 必须 | 必须 | 必须 | 事务、资源、控件状态、第三方截图 |
| Ongeki Headless/Skia | 必须 | 必须 | 必须 | 51 视图、Shell、应用兼容键 |
| Windows Desktop JIT | 必须 | 必须 | 必须 | 真实 Popup、焦点、窗口、Dock、设置恢复 |
| Windows NativeAOT | 必须 | 必须 | 必须 | 冷启动、切换、重启、fatal 日志、退出码 |
| Browser Release/WASM | 必须 | 必须 | 必须 | 宽/窄 viewport、Popup、字体、Canvas 非空、console |
| Linux/macOS Gekimini 示例 | 条件门 | 条件门 | 条件门 | 字体、Popup、原生窗口差异；当前 Ongeki 不宣称发布支持 |
| Android | 观察项 | 观察项 | 观察项 | 当前不作为首发完成门，不允许引入新的编译阻断 |

Windows DPI：100%、125%、150%、200%。Browser viewport 至少 `1280x720`、`1920x1080` 和 `390x844`，设备像素比至少 1 和 2。所有固定格式控件在这些尺寸下不能出现文本覆盖、页签高度跳动或图标裁切。

### 视觉基线政策

- WPF/Gemini 参考用于比较结构、视觉角色、状态和密度，不要求不同渲染器逐像素一致。
- Avalonia 回归基线只在固定 OS、字体、Skia、SDK 和 DPI 的 CI 镜像设置硬门。
- 每张图片固定控件尺寸、状态、主题 ID、文化和缩放；文件名包含 surface/state/variant/platform/scale。
- 自动门至少检查尺寸、非透明/非空像素占比、关键颜色采样和视觉差异比率。
- 建议初始阈值：单通道差异不超过 8 视为相同，超阈像素不超过总像素 0.5%；G0 实测后可调整，但调整必须写决策记录。
- 字体或渲染器不同的平台只上传 artifact 供人工复核，不使用同一像素阈值。
- 更新批准基线必须附 before/after、原因、关联任务和审阅者，禁止无说明覆盖图片。

### 性能与泄漏门

- G0 记录当前 Fluent 冷启动和主题初始化基线；G1 退出前冻结 VisualStudio 允许预算。
- 暂定热切换在参考机器 UI 线程内完成且无可见多帧混合；p95 目标不超过 100ms，若基线表明不合理再记录调整。
- 100 次全组合循环后 `Application.Styles`、MergedDictionaries 和托管主题层数量与首次稳定状态相同。
- 使用 WeakReference 证明已卸载的主题资产可回收；不以不稳定的进程总内存单值替代对象所有权测试。
- 大列表、TreeView、DataGrid 的虚拟化和滚动帧率不得因模板层级显著退化；超过 G0 基线 20% 必须分析并批准。

## 验证命令

以下是实施后的标准命令，均从 `F:\Source\OngekiFumenEditor\Avalonia` 执行。每条实际运行结果必须写入实时日志；本规划阶段尚未执行。

```powershell
# Gekimini 独立构建与主题测试
dotnet build .\Dependencies\Gekimini.Avalonia\Gekimini.Avalonia.sln -c Debug -t:Rebuild -m:1 -v:minimal
dotnet build .\Dependencies\Gekimini.Avalonia\Gekimini.Avalonia.sln -c Release -t:Rebuild -m:1 -v:minimal
dotnet test .\Dependencies\Gekimini.Avalonia\tests\Gekimini.Avalonia.Themes.Tests\Gekimini.Avalonia.Themes.Tests.csproj -c Release -v:minimal

# Ongeki 主题定向测试和全解决方案重建
dotnet test .\tests\OngekiFumenEditor.Avalonia.Tests\OngekiFumenEditor.Avalonia.Tests.csproj -c Release --filter "FullyQualifiedName~UI" -v:minimal
dotnet build .\OngekiFumenEditor.Avalonia.sln -c Release -t:Rebuild -m:1 -v:minimal

# Gekimini 示例发布
dotnet publish .\Dependencies\Gekimini.Avalonia\examples\Gekimini.Avalonia.Example.Desktop\Gekimini.Avalonia.Example.Desktop.csproj -c Release -v:minimal
dotnet publish .\Dependencies\Gekimini.Avalonia\examples\Gekimini.Avalonia.Example.Browser\Gekimini.Avalonia.Example.Browser.csproj -c Release -v:minimal

# 产品 Desktop JIT、NativeAOT 与 Browser
dotnet publish .\src\OngekiFumenEditor.Avalonia.Desktop\OngekiFumenEditor.Avalonia.Desktop.csproj -p:PublishProfile=win-x64-jit -v:minimal
dotnet publish .\src\OngekiFumenEditor.Avalonia.Desktop\OngekiFumenEditor.Avalonia.Desktop.csproj -p:PublishProfile=win-x64-aot -v:minimal
dotnet publish .\src\OngekiFumenEditor.Avalonia.Browser\OngekiFumenEditor.Avalonia.Browser.csproj -c Release -v:minimal
```

Browser 当前没有完整 Playwright/Selenium 基础。G6 必须新增真实浏览器渲染和 console 检查，或把它明确保留为发布阻断；不能以 `dotnet publish` 成功代替 Browser 验收。

## 验收证据规范

临时运行产物放入忽略目录：

```text
.artifacts/visualstudio-theme/<yyyyMMdd-HHmmss>-<root-short-sha>/
  manifest.json
  build-*.log
  test-*.trx
  warnings.json
  screenshots/<surface>.<state>.<variant>.<platform>.<scale>.png
  diffs/*.png
  native-aot-smoke.json
  browser-console.log
  hashes.sha256
```

`manifest.json` 至少记录各仓提交和 dirty 状态、SDK/Avalonia/OS、主题 ID、文化、字体、DPI、viewport、命令和退出码。源控中的批准基线放在测试项目 `Baselines/VisualStudio/{Light,Dark,Blue}`；临时日志和未批准截图不提交。

每个验收证据分配稳定编号 `V-xxx`，并在实时日志中链接。失败证据保留，后续成功记录注明取代了哪条失败记录，不能重写历史。

## 风险登记

| ID | 严重度 | 风险 | 预防/缓解 | 关闭条件 |
|---|---|---|---|---|
| R-001 | 阻断 | Gekimini 仓库没有 LICENSE/NOTICE，但 README 明确源自 Gemini | 在分发前确认许可证，补 NOTICE，保留来源和修改说明，必要时法律复核 | G7 许可证审计通过 |
| R-002 | 阻断 | Fluent 与 VisualStudio 的 Light/Dark 显示名冲突 | 稳定 ID、主题族过滤、旧 Name 上下文迁移、唯一性测试 | G1 全部身份测试通过 |
| R-003 | 阻断 | 颜色主题残留 `FluentTheme`，与 VisualStudio 同时生效 | 托管插槽、确切实例所有权、原子事务、100 次循环测试 | G1 无重复/泄漏 |
| R-004 | 高 | 派生 App 不继承 Gekimini XAML，静态第三方主题产生混合层 | 统一 Infrastructure 入口，并分别测试基础 App 和派生 App | G4/G5 Shell 通过 |
| R-005 | 高 | DynamicResource 缺失只在特定页面、Popup 或 AOT 启动暴露 | 三色板清单同构、全资源扫描、51 视图和 NativeAOT smoke | G3/G5/G7 0 缺失 |
| R-006 | 高 | WPF Trigger、Chrome、PART 和系统资源被机械复制 | 按控件契约重写、逐文件差异登记、行为测试 | G2 无 WPF 运行时依赖 |
| R-007 | 高 | Aero2 浅色常量泄漏到 Dark/Blue | Foundation 禁止主题色常量、资源扫描、三主题截图 | G3 0 泄漏 |
| R-008 | 高 | Dock/Toolbar/WindowManager 模板升级或本地 fork 差异破坏覆盖 | 优先资源适配、最小模板覆盖、固定依赖提交、状态测试 | G4 第三方矩阵通过 |
| R-009 | 高 | 旧主题关闭焦点反馈或颜色对比不足 | 保留 `:focus-visible`、键盘矩阵、高对比保护、对比度审查 | G6 无可访问性阻断 |
| R-010 | 中 | Blue 的基础 ThemeVariant 与第三方 Light/Dark 回退不一致 | 固定 Blue -> Light；所有第三方 Blue 颜色显式适配，不隐藏混合表面 | G4/G5 Blue Shell 通过 |
| R-011 | 中 | Segoe UI/系统指标跨平台不可用，引发裁切和密度漂移 | 字体/指标令牌、平台回退、中文和多 DPI 验收 | G6 排版矩阵通过 |
| R-012 | 中 | 应用硬编码 White/Transparent/主题色造成局部不可读 | 生成硬编码清单，功能色白名单，其余改语义资源 | G5 清单归零或批准 |
| R-013 | 中 | 深层模板破坏虚拟化、滚动和主题切换性能 | 控制视觉树深度，建立 G0 基线和大数据测试 | G6 性能门通过 |
| R-014 | 中 | Gekimini、Dock、父仓 gitlink 未同步，接手者拿到不完整实现 | 分仓提交和日志双 SHA，CI 检查 dirty/指针 | G7 两层提交一致 |
| R-015 | 中 | 将浮动 Dock 或自绘窗口误认为主题范围，导致无限扩面 | 非目标写入完成定义；功能恢复另开设计与验收 | 无未登记扩面 |
| R-016 | 高 | Headless 产品测试绕过完整 Shell/ThemeManager，产生假阳性 | 新增 Gekimini 独立测试、真实 Shell 夹具、Desktop/Browser 启动 | G5/G7 集成门通过 |

风险不能直接删除。关闭时保留原记录，补充关闭日期、证据编号和批准人；被接受的残余风险必须转入兼容偏差登记。

## 已知兼容偏差

| ID | 状态 | 偏差 | 首轮处理 |
|---|---|---|---|
| C-001 | 接受 | 当前文档和工具 Dockable 禁止浮动，与旧 AvalonDock 行为不同 | 主题仅验证独立 Dock 夹具；产品浮动恢复另立项目 |
| C-002 | 接受 | Avalonia 主窗口默认使用平台原生 chrome，不等同 MahApps MetroWindow | 首轮只迁移内容区和 ManagedWindow；Windows chrome 可选后续阶段 |
| C-003 | 接受 | WPF 默认字体来自系统 MessageFont，跨平台无法完全相同 | 使用可覆盖字体令牌和平台回退，以不裁切/可读为门 |
| C-004 | 接受 | NavigationWindow、Frame/Page、DocumentViewer/FlowDocument 不纳入主题 | 当前产品不用；未来引入时新增控件清单和门禁 |
| C-005 | 待确认使用面 | ColorCanvas/ColorPicker/WindowCommands 不能按旧依赖直接迁移 | 仅在产品功能恢复并有实际调用方后重设计 |
| C-006 | 已确定 | WPF/Gemini 与 Avalonia 不做逐像素跨框架比较 | 验收结构、状态、角色、密度；Avalonia 自身使用稳定像素基线 |

## 许可证、注释和来源要求

### 已知来源

- WPF：.NET Foundation MIT，固定提交 `114fbee660df4e981e851cc04a8a557dc7328898`。
- Gemini：Apache-2.0/Ms-PL 双许可，固定提交 `1147123f3506e531e71f940f1765d28825f28ae5`。
- 旧 Gemini Menu/WindowCommands 等文件包含 Wide Framework 的 MIT 归属说明。
- 当前 Dock 和 WindowManager fork 为 MIT；第三方包版本和许可证需进入 NOTICE。
- 旧 Ongeki TabControl 有 Style Snooper 来源注释，建议按视觉行为重写，不机械复制来源不清的完整模板。
- Gekimini 当前没有 LICENSE/NOTICE，这是正式分发阻断项，父仓 MIT 不自动替代独立子模块授权说明。

### 实施要求

1. 实质迁移 WPF 模板、几何或注释的文件保留 .NET Foundation MIT 头部和原解释性注释。
2. 每个派生文件头记录源仓、固定提交、源路径、迁移日期和主要改写点。
3. Gemini/Wide 来源文件保留相应归属；对仅参考视觉重新实现的文件也在 NOTICE 记录参考来源。
4. 不删除旧注释；失效的 WPF 机制注释改为保留原意并补 Avalonia 等价说明。
5. 不复制生成后的压缩 Aero2 XAML；使用有完整键名和注释的生成器输入文件。
6. G7 运行来源扫描，确保没有遗漏绝对路径、WPF pack URI、WPF 程序集或未说明的大段复制。

建议在 Gekimini 根目录新增 `THIRD-PARTY-NOTICES.md`，同时确认并补齐仓库主许可证。此项属于工程合规计划，不替代法律意见。

## 回滚和发布保护

- G0-G7 每阶段使用独立提交；架构、控件模板、第三方适配和产品接线不压成一个不可拆分提交。
- VisualStudio 在 G7 前保持 opt-in；Fluent + Light 始终作为安全主题组合保留。
- ThemeManager 在新主题冷启动失败时自动回退安全组合，并保留原失败 ID 供诊断。
- 设置迁移只有在新主题成功应用后才回写 ID；旧 Name 至少保留一个兼容版本。
- 每个主题资产按确切对象引用增删，回滚使用事务前快照，不通过重新扫描 `Application.Styles` 猜测旧状态。
- 若某个控件批次失败，可移除该 VisualStudio 覆盖，让它回到 WpfDefault Foundation；不得回到 Fluent 隐式模板而不记录。
- Gekimini 子模块提交和父仓 gitlink 分开；回滚父仓指针前先确认不会丢失子模块中其他人的提交。
- 已批准的视觉基线不可随回滚删除；新增一条证据说明回到哪个版本。

## 关键证据索引

| 主题 | 位置 |
|---|---|
| 当前主题接口 | [`ControlTheme.cs`](../Dependencies/Gekimini.Avalonia/src/Gekimini.Avalonia/Framework/Themes/ControlTheme.cs)、[`ColorTheme.cs`](../Dependencies/Gekimini.Avalonia/src/Gekimini.Avalonia/Framework/Themes/ColorTheme.cs) |
| 当前 ThemeManager | [`DefaultThemeManager.cs`](../Dependencies/Gekimini.Avalonia/src/Gekimini.Avalonia/Framework/Themes/DefaultImpl/DefaultThemeManager.cs) |
| Fluent 颜色替换行为 | [`FluentColorTheme2.cs`](../Dependencies/Gekimini.Avalonia/src/Gekimini.Avalonia/Framework/Themes/DefaultImpl/Fluent/FluentColorTheme2.cs) |
| 当前设置持久化 | [`GekiminiSetting.cs`](../Dependencies/Gekimini.Avalonia/src/Gekimini.Avalonia/Models/Settings/GekiminiSetting.cs)、[`MainMenuSettingsViewModel.cs`](../Dependencies/Gekimini.Avalonia/src/Gekimini.Avalonia/Modules/MainMenu/ViewModels/MainMenuSettingsViewModel.cs) |
| 派生 App 主题入口 | [`App.axaml`](../src/OngekiFumenEditor.Avalonia/App.axaml) |
| 当前应用语义资源 | [`EditorThemeResources.axaml`](../src/OngekiFumenEditor.Avalonia/UI/Themes/EditorThemeResources.axaml) |
| 产品 Headless 基础 | [`TestAppBuilder.cs`](../tests/OngekiFumenEditor.Avalonia.Tests/TestAppBuilder.cs)、[`AxamlSmokeTests.cs`](../tests/OngekiFumenEditor.Avalonia.Tests/UI/AxamlSmokeTests.cs) |
| WPF 默认选择证据 | `F:\Source\wpf\src\Microsoft.DotNet.Wpf\src\PresentationFramework\System\Windows\Application.cs`、`MS\Win32\UxThemeWrapper.cs` |
| WPF 主题输入/生成器 | `F:\Source\wpf\src\Microsoft.DotNet.Wpf\src\Themes\XAML`、`Themes\Generator\ThemeGenerator.proj` |
| 旧 VS2013 色板与控件 | `F:\Source\OngekiFumenEditor\Dependences\gemini\src\Gemini\Themes\VS2013` |

## 实时执行日志格式

每次实施必须在现有“实时进度与操作日志”追加记录，不覆盖旧行。建议详细记录使用以下模板：

```markdown
### YYYY-MM-DD HH:mm:ss +08:00 / B-xxx / 标题

- 状态：进行中 | 完成 | 阻断 | 被 V-xxx 取代
- 负责人：
- 对应阶段/门禁：Gx
- 目标与范围：
- 预期修改文件：
- 明确不修改：
- 前置工作树/子模块状态：
- 执行命令、工作目录和退出码：
- 实际改动：
- 测试数、警告增量和视觉证据：
- 新增风险/兼容偏差：
- 回滚点：
- 下一动作和阻塞条件：
```

日志时间统一为 `yyyy-MM-dd HH:mm:ss +08:00`。每条“进行中”记录必须最终追加完成、阻断或取代结果；失败历史不能删除。

## 交接检查表

接手者开始前：

- [ ] 阅读“已确认需求”“决策记录”“实时进度与操作日志”和当前阶段。
- [ ] 执行 `git status --short`，分别检查根仓、Gekimini、必要时 Dock/WindowManager 子模块。
- [ ] 核对文档基线 SHA 与实际 SHA；不一致时先登记新基线或停止实施。
- [ ] 确认当前批次没有与工作树中其他未提交改动重叠。
- [ ] 找到该控件的 WPF 源、Gemini 覆盖、资源角色和现有 Avalonia 控件契约。
- [ ] 在写代码前新增“进行中”日志和预期门禁。

接手者提交前：

- [ ] 更新逐控件状态、资源清单、风险、偏差和验证证据。
- [ ] 运行该阶段定向测试和 `git diff --check`；构建成功不能替代状态/视觉验收。
- [ ] 核对 Light/Dark/Blue，不能只验 Light。
- [ ] 核对旧注释、文件编码、许可证头和 NOTICE。
- [ ] 记录 Gekimini 提交、父仓 gitlink 和任何嵌套子模块提交。
- [ ] 把实时日志从“进行中”更新为实际结果，并注明下一动作。

## 当前状态与下一动作

当前状态：**规划完成，等待 G0 实施**。本轮只新增此文档，没有修改 Gekimini、Dock、WindowManager、WPF、Gemini 或产品主题实现，也没有运行构建/测试。

建议下一批严格按以下顺序开始：

1. 执行 G0，生成固定清单、当前 Fluent 基线和许可证决策，不写 VisualStudio 模板。
2. 执行 G1，只解决主题身份、托管插槽、设置迁移和 Fluent 回归；在这一步通过前禁止添加 VisualStudio 控件 XAML。
3. 以 Button + CheckBox + TextBox + ComboBox + Menu + Focus 做 G2/G3 首个 Light 垂直切片，验证 WpfDefault + VS Overlay + Palette 的完整链路。
4. 垂直切片通过后再扩到完整控件清单、Shell、Dark/Blue 和 Ongeki 集成。
