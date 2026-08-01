# XAML 迁移整改决策与执行日志

> 建立时间：2026-08-01 17:37:06 +08:00  
> 工作目录：`F:\Source\OngekiFumenEditor\Avalonia`  
> 用途：记录本轮 XAML 迁移复查的问题、候选方案、用户决策、实施进度、验证结果与后续交接信息。本文档在每个实施批次完成后同步更新。

## 当前结论

本轮只处理用户明确选择的范围：`1B + 2B + 3C + 4A→4C`。其余复查问题暂不处理，除非它们直接阻止所选范围的编译或验证。

| 项目 | 用户决策 | 执行含义 | 状态 |
|---|---|---|---|
| 1. Release 编译绑定 | **1B** | 保留 Release 全局编译绑定；未完成视图局部设置 `x:CompileBindings="False"`，成熟模块逐步补 `x:DataType`、模板类型和编译绑定 | 完成 |
| 2. 缺失 ViewModel 动作 | **2B** | 复用现有领域模型与服务重建动作；按钮/菜单使用命令；仅指针、拖拽等视图输入保留薄事件处理 | 完成（选定范围） |
| 3. 反射事件行为 | **3C** | 普通 `Click`、菜单动作、双击等交互迁移到 `ICommand`；模板数据项通过 `CommandParameter` 传递；原始输入事件才保留行为 | 完成（普通交互清零） |
| 4. 旧主题资源键 | **4A→4C** | 先提供旧键兼容别名解除运行时悬空，再建立应用语义主题资源并把现有引用迁移到语义键 | 完成 |

## 复查基线

以下数字来自本轮重新检查时的工作树快照；后续如代码发生变化，以“批次日志”和最终复查数字为准。

- Release 核心项目干净重建失败，共 **374** 个 Avalonia XAML 错误，涉及 **48** 个 AXAML 文件。
- 错误分布：`AVLN2100` 319 个、`AVLN2000` 51 个、`AVLN2101` 4 个。
- 约 53 个 AXAML 文件包含绑定，其中 49 个根节点没有 `x:DataType`。
- 共有 47 个 `DataTemplate`，仅 10 个声明类型，37 个未声明类型。
- `EventMethodBehavior` 有 122 处引用、92 个不同方法名；其中 38 处引用对应的 34 个方法在 Avalonia 应用源码中不存在，但旧 WPF 源码中存在。
- 20 处事件行为位于 `DataTemplate` 或行样式中，反射目标会落到数据项 `DataContext`，而不是父级 ViewModel。`FumenCheckerListViewer` 行双击是已确认实例。
- 多个迁移后的 ViewModel 只保留骨架，典型文件行数相对旧 WPF 版本大幅缩减：Splash 11/155、音频播放器 29/289、子弹调色板 17/249、Soflan 19/274、TGrid 17/175、程序设置 65/265。
- 旧资源键 `EnvironmentToolWindowText` 有 82 处引用，`EnvironmentWindowBackground` 有 11 处引用，共 93 处、36 个文件；应用与 Gekimini 中均没有定义。
- Release 构建必须用 `-t:Rebuild` 验证。失败后的普通增量构建可能因为陈旧输出而误报成功。

## 问题、建议与决策

### 1. Release 编译绑定策略

**问题**

核心项目在 Release 中设置 `AvaloniaUseCompiledBindingsByDefault=true` 和 `IsAotCompatible=true`，但大量迁移视图尚无类型上下文。Debug 默认关闭编译绑定，因此此前 Debug 成功不能证明 Release/AOT 可用。

**候选方案**

- **1A：全局临时关闭。** Release 也关闭默认编译绑定，最快恢复构建，但会掩盖迁移债务并削弱 AOT 路径。
- **1B：局部过渡。** 保留 Release 全局开关，在尚未完成的视图根节点显式设置 `x:CompileBindings="False"`；按模块补齐类型后移除局部豁免。
- **1C：一次性全量类型化。** 立即为所有视图和模板补 `x:DataType` 并迁移全部绑定，最终状态最好，但变更面和回归风险最大。

**决策：1B。** 局部豁免必须可搜索、可逐项删除；完成模块应使用 `x:DataType` 和编译绑定，不允许依赖全局降级。

### 2. 缺失 ViewModel 动作

**问题**

迁移视图仍引用大量旧动作，但对应 Avalonia ViewModel 是精简骨架或根本没有方法。仅让 XAML 编译通过并不能恢复业务流程。

**候选方案**

- **2A：直接搬运旧事件处理器。** 速度较快，但会把 WPF 控件、窗口和平台耦合继续带入 Avalonia。
- **2B：重建 ViewModel 动作。** 从旧实现提取业务意图，复用当前领域模型、编辑器服务、存储/窗口抽象；以命令公开动作，视图只负责无法抽象的原始输入。
- **2C：保留占位并延后。** 只消除崩溃或静默失败，不恢复功能。

**决策：2B。** 不机械复制 WPF UI 依赖；迁移旧代码时保留原有注释，必要时补充 Avalonia 平台适配说明。

### 3. `EventMethodBehavior` 与命令边界

**问题**

当前行为通过方法名反射调用 `AssociatedObject.DataContext`：方法不存在时静默返回，模板内目标容易错误，异步返回值也没有可靠等待。普通按钮和菜单因此缺少编译期契约。

**候选方案**

- **3A：继续使用并强化反射行为。** 增加日志、异常和异步处理，改动较小，但仍保留字符串方法名契约。
- **3B：显式指定行为目标。** 修正模板/父级 ViewModel 路由，适合必须由事件承载的输入，但普通动作仍非命令。
- **3C：普通交互命令化。** `Click`、菜单和双击改为 `ICommand`，数据项通过参数传递；仅 Pointer、DragDrop、Loaded/Size 等原始事件保留行为或薄适配层。

**决策：3C。** 命令必须有明确 `CanExecute`/异步边界；不能用新的字符串反射包装来伪装成命令迁移。

### 4. 旧 Gemini 主题资源键

**问题**

`EnvironmentToolWindowText` 和 `EnvironmentWindowBackground` 在当前资源图中没有定义，静态资源会在加载路径中失败，动态资源也无法保证对比度和主题切换效果。

**候选方案**

- **4A：增加兼容别名。** 立即定义旧键，快速消除悬空引用，适合作为过渡层。
- **4B：直接替换为框架/Gekimini 键。** 改动简单，但业务视图继续耦合第三方主题命名。
- **4C：建立应用语义资源。** 用应用拥有的语义键和 `ThemeDictionaries` 表达窗口背景、工具文字等角色，逐步替换旧键。

**决策：4A→4C。** 先加入兼容键保证旧引用可解析，同一轮建立语义资源并迁移当前 93 处引用；兼容键继续保留给遗漏视图和外部扩展。

## 明确延期

本轮暂不主动处理以下项目：

- `AudioPlayerToolViewerView.axaml` 中 `glView.ActualWidth` 的失效元素绑定。
- `SelectionFilterView.axaml` 中 4 处 `Panel.ActualWidth` 的 WPF 布局语义。
- 渲染控制与 Soflan 分组列表的 DataGrid 行拖拽排序。旧 `ListViewDragDropManager<T>` 只接受 Avalonia `ListView`，不能承接当前 AXAML 的 `DataGrid`；已移除两个必然静默失效的 `Loaded` 反射钩子，排序功能待后续使用 DataGrid 专用拖放行为实现。数值渲染顺序编辑、保存以及 Soflan 分组/选择功能不受此延期影响。
- 程序设置页的文件关联注册/注销以及 `IFileAssociationService`。用户于 2026-08-01 18:04 明确要求暂时移除、不实现；设置页对应 UI 与仅为本轮试建的服务文件已删除。
- 音频实际偏移、播放后端与波形渲染。核心项目当前排除了 NAudio 实现及 `AudioPlayerToolViewerViewModel.WaveformDrawing.cs`；音频调整窗口已恢复选择、校验、偏移换算和时间轴重算流程，零偏移可复制 WAV，非零偏移会明确报告未实现，不会误报成功。
- 已知 UI 降级、缺失模块、音频后端、快捷键宿主、测试覆盖、依赖漏洞及其他迁移报告事项。

若上述问题阻断本轮选定改动的编译，将只做最小阻断修复，并在此记录原因。

## 实施规则

1. Release 的全局编译绑定设置保持不变。
2. 局部反射绑定豁免只放在未完成的 AXAML 根节点，并在本文件登记数量。
3. 新增或完成迁移的视图优先使用根 `x:DataType`、类型化 `DataTemplate` 和编译绑定。
4. 按钮、菜单、双击等业务意图由 ViewModel 命令承担；指针坐标、拖拽数据、尺寸变化等原始 UI 信息可由薄行为/代码层转换后调用 ViewModel。
5. 主题相关视图只引用应用语义资源；旧键只作为兼容入口。
6. 每批修改后至少执行针对性静态检查；最终执行 Debug 与 Release 的干净重建。

## 实时进度

| 时间 | 批次 | 状态 | 结果 |
|---|---|---|---|
| 2026-08-01 17:37 +08:00 | 建立记录 | 完成 | 已记录复查基线、候选方案、用户决策、延期范围与执行规则 |
| 2026-08-01 17:45 +08:00 | 1B 局部绑定边界 | 完成 | 53 个含绑定 AXAML 已归类为 4 个类型化根与 49 个 `x:CompileBindings="False"` 局部豁免；未覆盖 0；Release 全局开关保持 `true`；核心 Release `-t:Rebuild` 为 0 错误、82 警告 |
| 2026-08-01 17:45 +08:00 | 1B 验证阻断修复 | 完成 | 374 个编译绑定错误清零后暴露 `AboutWindow` 的 `AVLN3000`；已增加显式公共无参构造并委托到原构造，未改变窗口业务逻辑 |
| 2026-08-01 17:49 +08:00 | 4A→4C 主题资源 | 完成 | 新增 `EditorThemeResources.axaml` 并由 `App.axaml` 合并；定义 Default/Light/Dark 语义颜色、2 个应用语义画刷和 2 个旧 Gemini 兼容键；36 个视图的 93 处引用已迁移，旧视图引用为 0；核心 Release `-t:Rebuild` 为 0 错误、82 警告 |
| 2026-08-01 17:50 +08:00 | 2B + 3C 动作/命令盘点 | 完成 | 待迁移普通交互共 73 处：66 个 `Click`、6 个 `DoubleTapped`、1 个 `KeyGesture`；涉及 25 个视图、60 个不同方法名。Pointer、DragDrop、Loaded、Checked 等原始/状态事件不计入本批命令化范围 |
| 2026-08-01 18:09 +08:00 | 设置页与更新对话框命令 | 完成 | 更新对话框、音频/颜色/快捷键/日志/程序设置页共 11 处普通交互已迁移为 `RelayCommand`；模板内颜色与快捷键条目通过父 ViewModel 命令和 `CommandParameter` 传参；核心 Debug 构建为 0 错误、49 警告 |
| 2026-08-01 18:04 +08:00 | 文件关联范围调整 | 完成 | 按用户最新决策撤出文件关联：删除设置页注册/注销 UI、3 个仅用于关联的状态属性，以及尚未接线的 `IFileAssociationService`/默认实现；普通交互目标随之减少 2 个 `Click` |
| 2026-08-01 18:09 +08:00 | 2B + 3C 剩余项复核 | 完成 | 当前剩余 60 处普通行为：53 个 `Click`、6 个 `DoubleTapped`、1 个 `KeyGesture`，涉及 19 个视图；相对 73 处基线已命令化 11 处、按决策删除 2 处 |
| 2026-08-01 18:15 +08:00 | 已有业务实现命令化 | 完成 | 谱面转换、检查器、工程创建、刷子范围对话框及对象检查器共迁移 18 处：16 个按钮、2 个数据行双击；新增非反射 `DoubleTappedCommandBehavior`，并将相关 `async void` 改为可等待命令；核心 Debug 构建为 0 错误、49 警告；剩余 42 处（37/4/1），涉及 10 个视图 |
| 2026-08-01 18:31 +08:00 | 列表工具业务恢复与命令化 | 完成 | 恢复选择/筛选、TGrid 计算、子弹模板、渲染控制、拍号和 Soflan 的当前编辑器订阅及领域操作；修正选择工具与 TGrid 工具错误的服务接口注册；迁移 21 处普通行为（16 个 `Click`、4 个 `DoubleTapped`、1 个 `KeyGesture`），模板原始指针/Checked 行为通过显式 `Target` 路由到父 ViewModel；核心 Debug 构建为 0 错误、49 警告；剩余 21 个 `Click`，仅 3 个视图 |
| 2026-08-01 18:44 +08:00 | 音频调整窗口业务恢复与命令化 | 完成 | 从旧实现恢复输入来源、文件选择、秒/TGrid 偏移换算、校验提示、对象 TGrid 重算及撤销记录；3 个 `Click` 已改为异步 `RelayCommand`。因 `IAudioManager`/NAudio 延期，选择器仅声明 WAV，零偏移使用现有复制回退，非零偏移明确失败；核心 Debug 构建为 0 错误、49 警告；剩余 18 个 `Click`，仅 2 个视图 |
| 2026-08-01 18:50 +08:00 | 音频播放器状态恢复与命令化 | 完成 | 恢复当前编辑器/播放器订阅、播放切换、音量速度代理、22 项音效状态与音量代理、波形选项设置持久化及安全释放；重置、保存、重载 3 个 `Click` 已改为 `RelayCommand`。`IAudioManager` 未注册时不实例化依赖它的音效播放器，并以禁用状态/日志明确降级；渲染 `Loaded` 仍按决策延期；核心 Debug 构建为 0 错误、49 警告；剩余 15 个 `Click`，仅主编辑器视图 |
| 2026-08-01 18:55 +08:00 | 主编辑器上下文菜单命令化 | 完成 | 选择、删除、复制、时间轴记忆/恢复、镜像及 4 种粘贴共 15 个 `Click` 已改为 `RelayCommand`；无业务用途的 `ActionExecutionContext` 已移除，复制/粘贴改为可等待 `Task`，原业务逻辑与注释保留；核心 Debug 构建为 0 错误、49 警告 |
| 2026-08-01 18:55 +08:00 | 2B + 3C 普通交互收口 | 完成 | 73 处基线中 71 处已命令化，文件关联相关 2 处按用户决策删除；AXAML 中 `EventName="Click"`、`DoubleTapped` 和 `KeyGesture` 普通行为均为 0，Pointer、DragDrop、Loaded、Checked 等原始事件继续保留 |
| 2026-08-01 19:58 +08:00 | Debug/Release 最终验证 | 完成（有范围外阻断） | `git diff --check` 通过；核心项目 Debug `Rebuild` 0 错误/82 警告，核心项目 Release `Rebuild` 0 错误/82 警告。解决方案 Release `Rebuild` 在未被本轮修改的 `OngekiFumenEditor.Avalonia.Desktop/Program.cs:28` 失败：`CS0103 TaskScheduler`（86 警告/1 错误）；桌面项目未启用隐式 using，属于既有启动项目缺口，按“其他先不管”未扩展修复 |

## 验证命令

以下命令均从 Avalonia 仓库根目录执行：

```powershell
dotnet build .\src\OngekiFumenEditor.Avalonia\OngekiFumenEditor.Avalonia.csproj --no-restore -c Debug -t:Rebuild -m:1 -v:minimal
dotnet build .\src\OngekiFumenEditor.Avalonia\OngekiFumenEditor.Avalonia.csproj --no-restore -c Release -t:Rebuild -m:1 -v:minimal
dotnet build .\OngekiFumenEditor.Avalonia.sln --no-restore -c Release -t:Rebuild -m:1 -v:minimal
```

## 交接说明

- 先阅读本文件“当前结论”和“实时进度”，再查看 `git diff`。
- 不要把普通增量 Release 构建当成编译绑定验收；必须使用 `-t:Rebuild`。
- 工作树已有若干未跟踪的迁移审计 `.txt` 文件，它们不是本轮生成物，不要删除或覆盖。
- 每完成一个批次，更新状态、精确文件/引用数量、验证命令与剩余风险。

## 2026-08-01 第二轮复查：待决策清单

> 复查时间：2026-08-01 20:55:50 +08:00  
> 状态：以下编号均为候选决策，尚未视为用户选择。推荐项只表示当前代码状态下的工程建议。  
> 边界：`IFileAssociationService` 及相关功能已按用户决定移除，不再列为待决策项。

### 当前证据快照

- 核心项目共有 63 个 AXAML；49 个根节点仍使用 `x:CompileBindings="False"`，仅有 5 处 `x:DataType`。
- 普通 `Click`、`DoubleTapped`、`KeyGesture` 反射行为均为 0；剩余 `EventMethodBehavior` 共 47 处，均为 Pointer、Drag/Drop、Loaded 或 Checked 等原始/状态事件。
- 47 处剩余反射行为中有 7 处方法不存在：启动页 5 处，全局编辑器设置的颜色选择 2 处。它们会静默失效。
- `SplashScreenViewModel` 当前只有 11 行，而旧实现有 155 行；语言、最近文件、启动动作等绑定所需状态尚未迁移。
- Gekimini 已在 `MainViewModel.OnViewAfterLoaded` 中把 14 个 `CommandKeyboardShortcut` 绑定到根 `TopLevel`；缺失的是 Ongeki 编辑器自身 35 个 `KeyBindingDefinition` 的输入宿主和强类型动作映射。这 35 个定义分为 14 个 `Normal`、6 个 `Global`、15 个 `Batch`，均已注册并可由 `keybind.json` 改键。应用 AXAML 另有一处局部 `Delete` 绑定。
- 当前 Skia 主路径已经使用 Avalonia `ICustomDrawOperation` 和租借的 `SKCanvas`；项目同时编译排除了旧 OpenGL、D3D、GL 和自建 Skia 宿主。
- 核心项目当前通过 `<Compile Remove>` 排除 80 个 C# 文件：图形 48、音频 19、旧控件/拖放 8、SVG 编辑 ViewModel 4、波形绘制 1。
- 解决方案同时包含 Desktop 和 Browser，但核心项目仍含 Windows P/Invoke、进程/IPC、INI、自更新器等平台实现；当前产品平台验收边界不明确。
- 仓库没有应用自身的测试项目；现有测试项目均属于 `Dependencies` 下的第三方/内嵌依赖。

### 阻塞下一阶段的决策

#### 5. 首个可交付平台

- **5A：Windows Desktop 优先。推荐。** 首轮只以 Windows Desktop 的构建和关键工作流为发布门槛；Browser 保留工程但不计入本轮功能对等，Linux/macOS 后续再拆平台实现。
- **5B：三平台 Desktop 同步。** 立即把 Windows 专属实现拆到平台项目，并同时验证 Windows、Linux、macOS。
- **5C：Desktop 与 Browser 同步。** 从现在开始要求所有核心功能同时满足浏览器沙箱、裁剪和 AOT；改造面最大。

**推荐理由：** 当前旧音频实现、自更新、IPC、INI、转储和若干辅助功能明显以 Windows 为中心。先选 5A 可以先恢复编辑器核心闭环，同时要求新平台依赖不再继续放入共享核心。

#### 6. 渲染后端收敛策略

- **6A：只保留 Avalonia.Skia 租借画布路径。推荐。** 以现有 `AvaloniaSkiaRenderControl` 为唯一宿主，在该路径补齐可视回归；视觉验收后分批删除已排除的 OpenGL/D3D/旧 Skia 宿主。
- **6B：恢复旧 OpenGL 后端。** 重新移植上下文、着色器、纹理和平台交换链。
- **6C：维持 Skia/OpenGL 双后端。** 同时维护两套绘图实现和平台上下文。

**推荐理由：** 当前 Skia 基本图元实现已存在并接入 Avalonia 渲染生命周期；恢复旧宿主会重新引入原生窗口和图形上下文的跨平台负担。SVG 对象当前通过缓存几何和普通图元绘制，不需要为 `ISvgDrawing` 恢复整个旧后端。

#### 7. 音频后端恢复范围

- **7A：Desktop 项目恢复现有 NAudio 实现。推荐（依赖 5A）。** `IAudioManager` 等接口和通用调度留在核心，NAudio 引用、注册和实现移到 Desktop；随后恢复非零偏移和波形。
- **7B：现在更换跨平台音频后端。** 先选定统一解码/播放库，再为各桌面平台实现输出。
- **7C：首版移除音频播放、音效、波形和非零偏移。** 仅保留谱面编辑和已有的零偏移文件复制。

**推荐理由：** 现有 19 个 NAudio 文件承载播放、音效、循环和变速，复用的业务成本最低；但它们不应继续作为 Browser/共享核心的隐式依赖。

#### 8. 启动页产品策略

- **8A：现在完整恢复启动页。** 迁移语言切换、最近文件分组、新建/打开/快速打开/教程命令及设置持久化。
- **8B：首版隐藏启动页并直接进入主壳。推荐。** 新建、打开、快速打开复用主菜单命令；待编辑器核心稳定后再恢复欢迎页。
- **8C：永久删除启动页。** 同时删除工具栏入口和 `DisableShowSplashScreenAfterBoot` 等状态。

**推荐理由：** 当前启动页不是局部缺口，而是 ViewModel 主体缺失；继续展示会产生大量运行时空绑定和 5 个静默失效入口。8B 能保留核心工作流且显著缩小当前迁移面。

#### 9. 编辑器快捷键输入宿主

- **9A：在现有 Gekimini 根快捷键之上补充编辑器集中输入路由。推荐。** 在根 `TopLevel` 统一接收尚未处理的按键，按 `KeyBindingLayer`、焦点上下文和 `CanExecute` 路由到当前编辑器的强类型动作；配置页继续编辑 `KeyBindingDefinition`。
- **9B：每个 View 分散声明 Avalonia `KeyBinding`。** 局部实现简单，但全局/编辑器/批量模式冲突和动态改键较难统一处理。
- **9C：继续延期快捷键。** 菜单和按钮可用，但编辑器主要键盘工作流不可验收。

**推荐理由：** Gekimini 已能处理保存、撤销等应用命令；现有 Ongeki 定义、持久化和分层模型适合由一个补充路由器承接。Avalonia 没有 WPF `CommandManager` 的等价隐式路由，不应等待框架自动接线，也不应恢复旧 XAML 中的字符串 `ActionMessageKeyBinding`。

##### 9A 具体设计

9A 不会重做 Gekimini 已经工作的 14 个应用命令快捷键。它只补齐当前没有接线的 35 个编辑器快捷键，并把旧 WPF 的字符串动作改为强类型映射。

**功能来源判定：原项目已有功能，当前迁移不完整；不是新增产品功能。**

- 旧 WPF 的 `FumenVisualEditorView.xaml` 已通过 20 个 `ActionMessageKeyBinding` 把 `KeyBindingDefinition` 接到 `KeyboardAction_*`、复制/粘贴和翻页动作；Batch 模式另有 15 个分层快捷键及子模式处理。
- 当前 Avalonia 项目已经迁入 35 个 `KeyBindingDefinition`、`Normal/Global/Batch` 层、`DefaultKeyBindingManager`、`keybind.json` 读写、设置 UI、编辑器动作方法及 Batch 命令处理器。
- 当前缺少的是“按键事件 -> 当前层定义 -> 业务动作”的接线，因此配置和动作代码存在，但绝大多数编辑器快捷键不会被触发。
- `IEditorKeyBindingRouter` 是新的 Avalonia 内部适配层，用来替代不应照搬的 WPF/Caliburn 字符串 `ActionMessageKeyBinding`。它恢复旧功能语义，同时补齐 Avalonia 必需的焦点避让、路由和冲突处理；不会向用户增加一套新的快捷键功能。

按键处理流程：

1. 主窗口的 `TopLevel` 收到 `KeyDown`；已经被局部控件处理的事件立即返回。
2. 检查焦点、模态窗口和编辑状态。焦点位于 `TextBox`、可编辑 DataGrid/ComboBox 等文本输入控件时，普通字母、Delete、复制/粘贴等优先交给控件，不触发编辑器动作。
3. 取得当前激活的 `FumenVisualEditorViewModel`；没有激活编辑器时不处理 Ongeki 编辑器快捷键。
4. 根据 `editor.IsBatchMode` 选择 `Normal` 或 `Batch` 层，同时允许 `Global` 层；使用 `IKeyBindingManager` 的当前配置匹配按键，因此设置页改键后无需重建 XAML。
5. 通过显式注册表把 `KeyBindingDefinition` 映射到 `ICommand`、Gekimini `CommandDefinition` 或强类型编辑器动作。禁止按方法名反射，也不恢复旧 `Message="KeyboardAction_..."` 字符串。
6. 先执行 `CanExecute`/状态检查；只有动作确实执行后才设置 `e.Handled = true`。

范围与优先级：

- Gekimini 的保存、撤销、设置、窗口等 `CommandKeyboardShortcut` 继续由现有 `CommandKeyGestureService` 负责。
- Ongeki 的放置对象、删除/选择、翻页、模式切换和 Batch 子模式由新的编辑器路由器负责。
- View 内局部快捷键和文本编辑优先于编辑器集中路由；集中路由只接收未处理事件。
- 同一作用域内的重复按键不依赖枚举顺序抢占。配置页应显示冲突并拒绝保存，或要求用户明确解除其中一个绑定。

建议实现边界：

- `IEditorKeyBindingRouter`：负责附加/移除 `TopLevel` 事件、焦点过滤、层选择、匹配和执行。
- `EditorKeyBindingAction`（或同等描述对象）：建立定义到强类型动作的显式映射，并提供 `CanExecute`。
- `DefaultKeyBindingManager`：继续负责读取、修改和保存 `keybind.json`，但查询结果需要确定性冲突处理。
- 现有 `CommandKeyGestureService`/`ICommandRouter`：保持为应用命令执行入口，不复制其 14 个绑定。

最低验收用例：

- `Normal` 与 `Batch` 使用相同按键时只执行当前层动作，`Global` 在两层均可执行。
- 设置页改键并保存后立即生效，重启后仍生效。
- 在谱面画布上按 Delete、Ctrl+C/Ctrl+V、PageUp/PageDown 执行一次且只执行一次。
- 在文本框或 DataGrid 编辑单元格中输入 T/H、Delete、Ctrl+C/Ctrl+V 不触发谱面动作。
- 弹出模态对话框后，主编辑器快捷键不穿透执行。

### 可随后决定的范围

#### 10. 剩余原始事件适配方式

- **10A：保留通用 `EventMethodBehavior` 反射。** 改动最少，但仍由字符串方法名维持契约。
- **10B：视图专用事件使用薄代码层，可复用模式使用专用强类型 Behavior。推荐。** Pointer/Drag/Loaded 参数在视图层转换，再调用 ViewModel 命令或明确方法；逐步清除通用反射。
- **10C：所有事件强制命令化。** 会把大量 Avalonia 事件参数和控件细节推入 ViewModel。

**推荐理由：** 10B 与既定 3C 边界一致，同时能消除目前 7 个“字符串存在、方法不存在”的静默失败。

#### 11. 未达可用门槛模块的可见性

- **11A：所有现有菜单和工具栏入口继续可见。** 构建能通过，但用户可能进入迁移壳或平台不可用功能。
- **11B：按功能验收门槛隐藏入口。推荐。** 先隐藏 `InternalTest`、未迁移的 SVG 属性编辑/创建入口、不可用音频动作及按 8B 处理的启动页；保留谱面解析、保存和现有 SVG 几何显示兼容。
- **11C：直接删除这些模块及数据类型。** 清理最彻底，但可能破坏旧谱面兼容性。

**推荐理由：** “不向用户暴露”不等于“删除文件兼容性”。11B 可以避免假功能，同时保留以后恢复所需的领域模型与文件格式支持。

#### 12. 自更新范围

- **12A：保留当前进程内下载、解压、替换和拉起外部 EXE 的自更新。** 功能完整，但强依赖 Windows 发布布局和外部命令行程序。
- **12B：首版仅做 Desktop 版本检查和手动下载跳转。推荐。** 自覆盖更新单独做发布工程验证后再启用；Browser 隐藏该入口。
- **12C：完全移除更新检查和更新 UI。** 由外部分发渠道负责升级。

**推荐理由：** 当前实现位于共享核心，却使用 `Process`、`.exe`、进程终止和目录覆盖；在发布包格式尚未固定前启用自覆盖风险高。

#### 13. DataGrid 行排序交互

- **13A：实现 Avalonia DataGrid 专用拖放排序。** 保持旧版直接拖动体验，但要处理虚拟化、拖动指示和多选边界。
- **13B：先提供上移/下移命令和键盘操作，拖放后补。推荐。** 行为明确、可测试且具备键盘可访问性。
- **13C：首版不允许排序。** 保留数值编辑和保存顺序。

**推荐理由：** 旧 `ListViewDragDropManager<T>` 与当前 DataGrid 不兼容。13B 能先恢复业务能力，且不会把旧 WPF 拖放管理器带回项目。

#### 14. 49 个运行时绑定豁免的处理顺序

- **14A：立即一次性消除全部豁免。** 最快达到完整编译绑定，但回归面最大。
- **14B：按核心工作流分批。推荐。** 优先主编辑器、项目设置、对象检查器和设置页，再处理工具窗口；任何被修改的视图必须补类型并移除豁免。
- **14C：无限期保留 49 个豁免。** Release 能构建，但继续失去大部分绑定的编译期检查。

**推荐理由：** 14B 是既定 1B 的可执行优先级，不改变原决策，并能优先覆盖风险最高的复杂绑定。

#### 15. 迁移验收测试门槛

- **15A：只要求 Debug/Release 构建。** 成本最低，但无法发现空绑定、失效命令和空白渲染。
- **15B：新增应用测试项目和三层门槛。推荐。** 至少包含 AXAML/视图构造测试、关键命令/状态测试、Skia 非空帧与 Desktop 新建/打开/编辑/保存冒烟测试。
- **15C：立即建立完整截图基线和全流程 UI 自动化。** 覆盖最强，初期维护成本最高。

**推荐理由：** 当前已证明“核心项目 0 编译错误”仍可同时存在 7 个失效入口和大量空绑定；15B 是能阻止同类回归的最低合理门槛。

#### 16. 80 个编译排除源文件的清理策略

- **16A：长期保留在项目目录并继续 `<Compile Remove>`。** 方便参考，但审计噪声和误用风险持续存在。
- **16B：现在全部删除。** 目录最干净，但音频等尚未完成替代的实现会同时消失。
- **16C：按子系统验收后删除。推荐。** 渲染、音频、旧控件、SVG 分批确认替代方案；每批在独立清理提交中删除，并在本文档记录 Git 历史位置和替代实现。

**推荐理由：** 16C 既避免过早丢失迁移参考，也不会让已确定废弃的 WPF/原生宿主代码永久留在活跃源码树中。

### 无需产品决策，建议直接修复

- Desktop Release 的 `TaskScheduler` 名称解析错误：补明确命名空间或全限定名，并重新执行解决方案 `Rebuild`。
- 全局编辑器设置中 2 个不存在的颜色选择方法：改为 ViewModel 异步命令并复用现有颜色选择对话框。
- `AudioPlayerToolViewerView.axaml` 的 `glView.ActualWidth`：绑定到实际 Avalonia 渲染宿主的 `Bounds.Width`；在 7C 时则随音频 UI 一并隐藏。
- `SelectionFilterView.axaml` 的 4 个 `Panel.ActualWidth`：改为 Avalonia 的 `Bounds.Width` 或由容器伪类/尺寸观察驱动，不需要改变产品行为。
- `CommonColorPicker.axaml` 的 12 个直接 `Click` 是对话框内部颜色格代码层，不属于缺失 ViewModel 动作，也不需要为满足 3C 强行迁移。

### 本次复查进度

| 时间 | 批次 | 状态 | 结果 |
|---|---|---|---|
| 2026-08-01 20:55 +08:00 | 当前代码重新审计 | 完成 | 旧迁移报告仅作线索，所有数字已用当前工作树复核；确认 47 个剩余原始/状态事件、7 个缺失目标、49 个运行时绑定豁免、80 个编译排除文件及应用测试缺口 |
| 2026-08-01 20:55 +08:00 | 新决策清单 | 待用户决策 | 登记 5～16 项候选及推荐方案；未修改业务代码，等待用户用编号回复 |
| 2026-08-01 21:02 +08:00 | 9A 设计澄清 | 完成 | 确认 Gekimini 已有 14 个根命令快捷键；9A 范围修正为补齐 35 个 Ongeki 编辑器分层快捷键，登记焦点避让、强类型动作映射、冲突策略与验收用例 |
| 2026-08-01 21:12 +08:00 | 9A 功能来源核对 | 完成 | 确认快捷键是原 WPF 项目已有能力；当前已迁移定义、配置、业务动作和 Batch 命令，缺少 Avalonia 输入接线。9A 的新内容仅为平台适配层，不是新增产品功能 |

## 2026-08-01 第三轮决策与实施记录

> 决策时间：2026-08-01 21:34 +08:00  
> 当前实施范围：`5A（附加 Native AOT 与跨平台约束）+ 6A + 8A + 9A`。  
> 延期范围：第 7 项音频暂不处理；第 10～16 项继续保持待决策/待排期，不因本轮实现被默认选中。  
> 已移除范围：`IFileAssociationService` 及相关 UI/功能继续保持移除，不恢复实现。

### 决策 5：Windows Desktop 首发，同时强制 Native AOT 与跨平台边界

用户选择 **5A**，但增加两个不可省略的条件：首发程序必须支持 Native AOT；实现时必须为以后 Linux/macOS Desktop 留出跨平台边界。

本轮采用以下解释和实现边界：

- Windows Desktop 是当前唯一发布验收平台；Browser、Linux 和 macOS 不要求本轮达到功能对等。
- “支持 AOT”不是只设置 `<IsAotCompatible>true</IsAotCompatible>`。本轮必须实际执行 Windows Desktop 的 `dotnet publish -c Release -r win-x64 -p:PublishAot=true`，并把发布成功作为验收门槛。
- 共享项目继续启用 AOT/裁剪分析。新增实现不得依赖按方法名反射、运行时 XAML 字符串动作或未声明的动态类型激活。
- 新增输入路由、启动页业务和渲染选择只使用 Avalonia/托管跨平台 API；不得把新的 Win32、注册表、Windows 路径格式或 `.exe` 假设放入共享项目。
- 现有 Windows 专属的转储、IPC、INI、自更新等代码是已知历史债务。本轮只在它们阻断 AOT 发布时做必要隔离；它们各自的产品策略仍等待后续决策，不能借 5A 擅自删除。
- 平台差异以后通过 Desktop 平台项目中的实现或显式平台服务接口承接。共享 ViewModel 不直接查询 Win32 句柄。

最低验收标准：

- 核心项目 Debug/Release 干净重建均为 0 错误。
- 整体解决方案 Release 干净重建为 0 错误。
- Windows x64 Desktop Native AOT 发布为 0 错误；若第三方库产生 AOT 警告，逐项登记实际影响，不能用关闭分析器掩盖。
- AOT 输出能够启动到主窗口/启动页；若当前环境无法做 GUI 启动冒烟，必须明确记录未验证原因。

### 决策 6：Avalonia.Skia 单渲染后端

用户选择 **6A**。当前 `AvaloniaSkiaRenderControl`、`ICustomDrawOperation` 和 `ISkiaSharpApiLeaseFeature` 租借的 `SKCanvas` 路径成为唯一有效渲染宿主。

实现与清理边界：

- 新功能和修复只接入 Avalonia.Skia 路径，不恢复旧 OpenGL、D3D、GL Context 或自建 Skia 窗口宿主。
- 保持渲染宿主位于共享 Avalonia 层，不使用 Windows 专属交换链，确保未来 Linux/macOS Desktop 可复用。
- 当前已被 `<Compile Remove>` 排除的旧渲染源文件暂不删除；删除属于尚未执行的 16C 清理批次。
- 本轮至少用静态接线检查和构建确认运行路径唯一；可视像素/截图基线仍依赖以后测试门槛决策，但不允许存在可选后端配置指向已排除实现。

### 决策 7：音频继续延期

用户要求第 7 项“先搁置”。因此本轮：

- 不恢复 NAudio，不选择新的跨平台音频库，也不宣称音频功能完成。
- 保持当前 `IAudioManager` 缺失时的显式降级，以及零偏移复制/非零偏移失败边界。
- AOT 发布若被已排除的音频文件影响，只修正项目排除/引用边界，不恢复业务实现。

### 决策 8：完整恢复启动页，并沿用窗口基类

用户选择 **8A**，并明确要求使用 Gekimini 的 `WindowViewBase` / `WindowViewModelBase`。启动页属于本轮必须恢复的原有产品功能。

恢复范围：

- `SplashScreenView` 继续继承 `WindowViewBase`，`SplashScreenViewModel` 继续继承 `WindowViewModelBase`，并通过现有 `IWindowManager`/窗口生命周期显示，而不是直接在 ViewModel 中 `new Window()`。
- 恢复语言列表、当前语言、切换后重启提示、禁用启动时显示设置、最近文件按日期分组。
- 恢复打开最近文件、新建项目、打开项目、快速打开和教程入口；动作使用命令或明确的命令路由，不恢复字符串 `EventMethodBehavior` 方法调用。
- 启动页修改后补齐类型信息和编译绑定，消除该视图现有的 5 个不存在方法引用。
- 启动时是否自动显示必须读取现有 `DisableShowSplashScreenAfterBoot` 设置；工具栏手动入口仍可再次打开。
- 文件选择、教程打开和窗口服务从当前 Avalonia `TopLevel`/现有跨平台服务取得，不新增 Windows Shell 依赖。

最低验收标准：所有可见入口均有实际命令；最近文件与语言状态能够构造；关闭“启动时显示”后持久化；窗口可由框架窗口管理器正常打开。

### 决策 9：集中式编辑器快捷键路由

用户选择 **9A**。沿用上一节“9A 具体设计”，并将以下内容固定为实现要求：

- 只补齐 Ongeki 的 35 个 `KeyBindingDefinition`；Gekimini 已有的 14 个应用命令快捷键不重复注册。
- 在一个根级 Avalonia 输入路由器中处理未被局部控件消费的 `KeyDown`，并可随 `TopLevel` 生命周期附加/移除。
- 根据当前编辑器的 `Normal`/`Batch` 层匹配，同时允许 `Global`；匹配使用 `IKeyBindingManager` 当前值，设置保存后无需重建 XAML即可生效。
- 焦点位于文本编辑、可编辑 DataGrid/ComboBox 或对话框输入区域时，编辑器快捷键必须避让；模态窗口不能向主编辑器穿透。
- 定义到动作使用显式强类型映射，禁止按方法名反射和旧 WPF `ActionMessageKeyBinding` 字符串消息。
- 同一有效作用域内出现重复配置时拒绝不确定执行，并输出可定位的冲突信息；不能依赖注册顺序任选一个。
- 只有动作实际执行后才设置 `KeyEventArgs.Handled = true`，以保留 Avalonia 正常输入与局部快捷键语义。

### 追加决策：IPCHelper / 单实例 IPC 延期

用户明确要求 **IPCHelper 本轮先不实现**。该决定覆盖决策 5 中“仅在阻断 AOT 时做必要隔离”的默认处理：不再为现有 Windows 命名内存映射实现补 AOT 兼容，也不在 Desktop 启动前读取设置来启动 IPC。

本轮边界如下：

- Desktop 入口不再调用共享 `Startup.Initialize`，应用直接进入 Avalonia 生命周期。
- `Startup.cs` 与 `Utils/IPCHelper.cs` 保留为迁移参考，但通过项目文件明确排除编译；不删除原实现和注释。
- 本轮不提供单实例互斥、第二实例参数转发和 `--wait` 等待语义；启动多个 Desktop 进程时不做 IPC 协调。
- Native AOT 启动冒烟曾在 `IPCHelper` 静态初始化阶段因 DI 建立前读取 `ProgramSetting.Default` 而以 `0xC0000409` 退出。该问题不再通过预 DI 设置读取修补，而是按本决策隔离整个 IPC 启动链。
- 后续恢复时应把 IPC 定义为 Desktop 平台服务：共享层只依赖抽象；Windows 可使用命名管道/互斥体等实现，Linux/macOS 分别选择对应平台机制，并单独定义多实例和参数转发协议。

### 本轮实施计划与实时状态

| 时间 | 批次 | 状态 | 结果/下一步 |
|---|---|---|---|
| 2026-08-01 21:34 +08:00 | 最新决策登记 | 完成 | 已登记 5A+AOT/跨平台、6A、7 延期、8A+窗口基类、9A；10～16 继续不处理 |
| 2026-08-01 21:34 +08:00 | AOT/平台/Skia 基线 | 完成（警告待分类） | 已补 Desktop `TaskScheduler` 命名空间并新增 `win-x64-aot.pubxml`；Desktop Release Rebuild 为 0 错误/83 警告；实际 win-x64 Native AOT publish 成功并输出原生可执行文件。确认唯一有效渲染注册和选择均为 Avalonia.Skia。AOT 分析发现启动页反射绑定、通用 `EventMethodBehavior`、Gekimini/Dock 反射路径及少量应用反射警告，后续按本轮范围继续消减并分类登记 |
| 2026-08-01 21:58 +08:00 | 启动页恢复 | 完成 | 已用 `WindowViewBase` / `WindowViewModelBase` 恢复语言、最近文件分组、禁用启动显示、新建/打开/快速打开/教程命令和启动后自动显示；视图改为编译绑定并移除字符串事件动作。核心 Release 构建 0 错误/118 警告。因第 7 项音频延期，快速打开现在会正常选择并尝试载入谱面，但需要音频服务的后续阶段仍会给出显式错误，不能视为音频链路已恢复 |
| 2026-08-01 22:08 +08:00 | 编辑器快捷键 | 完成 | 新增 `IEditorKeyBindingRouter` / `DefaultEditorKeyBindingRouter` 并由共享应用在主 `TopLevel` 就绪后附加；Bubble 路由只接收未处理的 `KeyDown`，按当前编辑器 Normal/Batch 层叠加 Global 层动态匹配现有配置。显式强类型映射覆盖旧 WPF 20 项普通/全局动作和 15 项 Batch 子模式，共 35/35 项且无方法名反射；文本框、数值框、下拉框、DataGrid、`WindowViewBase` 内输入均避让；同一有效层重复绑定会记录全部冲突并拒绝执行。核心 Release 首次编译为 0 错误/123 警告，其中新增 5 条仅来自新文件可空上下文，已显式启用 `#nullable` 消除，等待最终重建确认 |
| 2026-08-01 22:11 +08:00 | 最终验证 | 进行中 | 核心 Debug Rebuild 0 错误/82 警告，核心 Release Rebuild 0 错误/159 警告，顶层解决方案 Release Rebuild（含 Desktop、Browser 及依赖）0 错误/163 警告；35/35 映射及无方法名反射静态检查通过。剩余步骤：重新发布 win-x64 Native AOT、启动原生 EXE 冒烟并分类登记最终警告 |
| 2026-08-01 22:25 +08:00 | IPCHelper 延期 | 完成 | 按用户最新决策撤销 Desktop 的 IPC 启动调用及预 DI 设置读取方案；`Startup.cs`、`Utils/IPCHelper.cs` 保留源码但从共享项目编译中排除。本轮明确不提供单实例、第二实例参数转发和 `--wait`，下一步重新构建、发布并执行不含 IPC 的 Native AOT 启动冒烟 |
| 2026-08-01 22:28 +08:00 | IPC 隔离后 Desktop Release 构建 | 完成 | `OngekiFumenEditor.Avalonia.Desktop.csproj --no-restore -c Release` 为 0 错误/112 警告；编译输出不再包含 `IPCHelper`。现存警告仍包括既有裁剪/AOT 反射路径，以及 `SkiaSharp 2.88.3`、`Tmds.DBus.Protocol 0.21.2` 的高严重性 NuGet 公告 |
| 2026-08-01 22:29 +08:00 | IPC 隔离后解决方案 Release 构建 | 完成 | 顶层 `OngekiFumenEditor.Avalonia.sln --no-restore -c Release` 增量构建为 0 错误/4 警告；Desktop、Browser、共享项目及依赖均成功产出。进入 Native AOT 重新发布阶段 |
| 2026-08-01 22:31 +08:00 | IPC 隔离后 win-x64 Native AOT 发布 | 完成（警告已登记） | `win-x64-aot.pubxml` 发布成功并生成 40,982,528 字节原生 EXE；发布分析中不再出现 `IPCHelper`。剩余警告来自 Gekimini/Dock 的反射绑定与消息注册、对象属性浏览器/解析器等既有动态路径、Avalonia DataGrid/ReactiveUI 程序集，以及两个高严重性 NuGet 公告；不将本次结果表述为无警告 AOT。下一步执行原生窗口启动冒烟 |
| 2026-08-01 22:32 +08:00 | 原生 EXE 第一次窗口冒烟 | 失败，修复中 | 原生进程已越过 IPC 并创建可响应窗口，但窗口标题“提示”实际是致命异常对话框。`current.log` 确认主窗口初始布局时 `DialogHostAvalonia.DialogHost.OnApplyTemplate` 因缺少 `PART_DialogHostRoot` 模板退出；该问题是 DialogHost 主题资源未合并造成的独立 XAML 启动阻断，不是 IPC。下一步补最小主题资源接线后重新构建、发布、冒烟 |
| 2026-08-01 22:36 +08:00 | Gekimini 应用主题契约恢复 | 完成，待原生复验 | 确认派生 `App.axaml` 不会继承 Gekimini 基类 XAML 资源；已显式接入 DialogHost、Dock Fluent、StatusBar、ToolBar、WindowManager 主题，并补回 DialogButton、容器和 ManagedWindow 资源。Desktop Release 构建为 0 错误/112 警告，下一步重新发布 Native AOT 并复验真实主窗口与启动页 |
| 2026-08-01 22:39 +08:00 | 原生 EXE 第二次窗口冒烟 | 失败，修复中 | DialogHost 已恢复，Shell 初始化成功；启动页受管窗口绘制时，`Iciclecreek.Avalonia.WindowManager.WindowManagerTheme` 使用的 `IconPacks.Avalonia.Core 1.3.1` 通过反射式 JSON 加载 `Dictionary<PackIconCodiconsKind,string>`，Native AOT 缺少对应转换器代码并触发 fatal。优先移除该主题对运行时 IconPacks JSON 的依赖，而不是全局放宽裁剪/反射 |
| 2026-08-01 22:42 +08:00 | WindowManager IconPacks AOT 依赖移除 | 完成，待原生复验 | 5 个受管窗口标题栏图标已改为静态跨平台 `PathIcon` 几何，移除 `IconPacks.Avalonia.Codicons` 包与样式引用；恢复后的 `project.assets.json` 中 IconPacks 引用为 0。Desktop Release 构建为 0 错误/70 警告，进入第三次 Native AOT 发布与启动冒烟 |
| 2026-08-01 22:45 +08:00 | 原生 EXE 第三次窗口冒烟 | 进程通过，视觉复查发现文本缺失 | Native AOT 主窗口 `Gekimini.Avalonia` 持续稳定且可响应，日志 fatal 匹配为 0，可优雅退出；`PrintWindow` 确认 Shell、菜单、工具栏、状态栏、Welcome 启动页和静态窗口按钮均真实渲染。但启动页使用 `TranslateExtension` 的标签为空，说明反射式本地化 Binding 在 AOT 下被裁剪，尚不能完成 8A 视觉验收 |
| 2026-08-01 22:51 +08:00 | 全局 TranslateExtension AOT 修复 | 完成，待原生视觉复验 | 仓库内 274 处 `{markup:Translate ...}` 原先返回反射式 Avalonia `Binding`；现按既有“切换语言后重启”产品语义，在 XAML 构造时直接返回当前本地化文本并保留 `StringFormat`。Desktop Release 构建 0 错误，构建输出不再出现该扩展的反射 Binding 警告；下一步重新 AOT 发布并截图复验标签 |
| 2026-08-01 22:54 +08:00 | 原生 EXE 最终启动与视觉冒烟 | 完成 | 最新 win-x64 Native AOT EXE 为 42,344,960 字节；主窗口 `Gekimini.Avalonia` 稳定 7.1 秒、可响应、fatal 日志匹配 0，并优雅退出。`PrintWindow` 截图确认 Shell、Welcome 启动页、全部本地化标签、四个入口、语言选择、最近文件区、“不再显示”选项及静态窗口按钮均真实可见。截图暂存于发布目录 `native-aot-startup-smoke-final.png`；剩余步骤仅为最终 Debug/Release 构建与差异核对 |
| 2026-08-01 22:58 +08:00 | 本轮最终验收 | 完成（保留警告） | 共享核心 Debug 构建为 0 错误/71 警告，顶层解决方案 Release 构建为 0 错误/58 警告，`git diff --check` 通过且原生冒烟进程无残留；结合已完成的 win-x64 Native AOT 发布和真实窗口视觉冒烟，本轮 5A+AOT/跨平台边界、6A、8A、9A 以及 IPCHelper 延期范围验收完成。当前仍保留 Gekimini/Dock 与应用既有动态/反射路径的裁剪分析警告、Browser 的 NU1507/WASM0001，以及 `SkiaSharp 2.88.3`、`Tmds.DBus.Protocol 0.21.2` 的高严重性 NuGet 公告；第 7 项音频、IFileAssociationService、IPCHelper 与第 10～16 项不在本轮实现范围。`TranslateExtension` 现在按既有“切换语言后重启”语义生成构造时文本，已创建视图不会在同一进程内实时重标注。 |

## 2026-08-01 当前未决策清单（第四轮复核）

> 复核时间：2026-08-01 23:08 +08:00  
> 状态：只有第 10～16 项仍需要用户选择；本节是当前权威版本，前面的第二轮清单保留为历史快照。  
> 已有结论：第 1～6、8、9 项已经决策并实施；第 7 项音频、`IFileAssociationService` 和 `IPCHelper` 已明确延期，不属于“尚未决策”。

### 当前证据变化

- `EventMethodBehavior` 已从第二轮的 47 处降到 **42 处**：`PointerPressed` 22、`PointerMoved` 10、`Checked` 3、`Loaded` 3，`DragEnter`、`Drop`、`FocusableChanged`、`Unchecked` 各 1。启动页原有 5 处已经随 8A 移除；仍有全局编辑器设置中的 2 个颜色选择方法不存在，会静默失败。
- 63 个 AXAML 中，根级 `x:CompileBindings="False"` 已从 49 个降到 **48 个**，含 `x:DataType` 的文件从 5 个增加到 **6 个**。Native AOT 启动冒烟只证明已走到的启动页和 Shell 路径可用，不能覆盖其余运行时绑定。
- `<Compile Remove>` 当前匹配 **82 个**物理 C# 文件：旧图形后端 48、NAudio 19、波形绘制 1、旧控件/拖放 8、SVG 编辑 ViewModel 4、`Startup.cs`/`IPCHelper.cs` 2。
- 启动页已经完成，因此不再属于第 11 项建议隐藏的模块。仍公开存在的未达门槛入口主要是 `InternalTest`、缺少后端的音频工具/调整窗口，以及 ViewModel 已排除的 SVG 创建/属性编辑路径。
- 自更新仍位于共享核心，下载包后依赖 `OngekiFumenEditor.CommandLine.exe`、进程终止、目录覆盖和 `.exe` 重启。由于 `Startup.Initialize` 已随 IPC 延期从启动链移除，当前命令执行器没有入口，自覆盖更新链实际上不可验收。

### 10. 剩余原始事件适配方式

**问题与影响**

- 42 处通用行为仍通过字符串方法名和反射查找目标，不利于 Native AOT/裁剪，也没有可靠的编译期契约。
- 其中既有真正的原始输入（拖放、指针移动、渲染宿主 `Loaded`），也有用 `PointerPressed` 包装的业务动作（插值、复制模板、打开更新对话框等），不能一刀切按同一种方式迁移。
- 两个颜色选择目标当前不存在，是已确认的静默失效。

**候选方案**

- **10A：保留并强化通用 `EventMethodBehavior`。** 增加缺失目标报错、异步等待和 AOT 保留声明。改动最少，但长期保留字符串契约，与已确定的 AOT 方向冲突。
- **10B：按事件语义分流。推荐。** 真正需要 Avalonia 事件参数的 Pointer/Drag/Drop/Loaded 使用薄代码层或专用强类型 Behavior；业务意图改为命令；Checked/Unchecked 优先改为双向绑定或命令。通用反射行为只作为可追踪的临时兼容层逐批归零。
- **10C：所有事件强制命令化。** 表面统一，但会把 `PointerEventArgs`、控件引用和坐标转换推入 ViewModel，破坏既定视图边界。

**建议验收**：不存在任何缺失方法目标；业务动作不再使用 `PointerPressed` 伪装；每个保留的原始事件都有明确目标类型、卸载/取消订阅边界和 AOT 验证。

### 11. 未达可用门槛模块的可见性

**问题与影响**

- `InternalTest` 仍有菜单入口。
- 音频播放器工具和音频调整窗口仍有入口，但第 7 项已延期，播放、音效、波形和非零偏移不能形成完整闭环。
- SVG 领域模型、解析和现有几何显示需要保留旧谱面兼容性，但 4 个创建/属性编辑 ViewModel 被排除，相关编辑入口不应被当作已实现功能。

**候选方案**

- **11A：所有入口继续显示。** 便于开发人员进入，但正式用户会遇到禁用、报错或无效果的功能。
- **11B：按能力和验收门槛控制可见性/可执行性。推荐。** 正式构建隐藏 `InternalTest`；音频入口等第 7 项恢复后再开放；隐藏 SVG 创建/属性编辑入口但保留解析、保存、领域模型和既有几何显示。已完成的启动页保持可见，不再隐藏。
- **11C：删除未达门槛模块及领域类型。** 清理最彻底，但会破坏旧谱面数据兼容性，也会提前删除已经明确延期的实现参考。

**建议验收**：所有可见入口都能完成其承诺的工作流；能力判断集中管理并可按 Desktop/Browser 区分，不在各 View 中散落平台判断。

### 12. 自更新范围

**问题与影响**

- 当前实现是 Windows 发布布局专用的自覆盖更新器，却位于共享核心；它会下载 ZIP、启动 `.exe`、终止进程、备份/覆盖目录并再次拉起程序。
- IPC/命令行启动链延期后，下载包中的 `updater` 命令和 `--wait` 语义当前没有可达入口；继续显示“立即更新”会形成假功能。
- Native AOT 单文件/多文件布局、签名、权限、回滚和跨平台包格式均未建立发布测试。

**候选方案**

- **12A：恢复完整自覆盖更新。** 将实现移到 Desktop 平台层，建立独立更新器、签名/校验、权限、进程协调、回滚和各发布格式测试。功能最完整，工程与安全成本最高。
- **12B：首版只保留版本检查和手动下载跳转。推荐。** 共享层只保留版本信息；Desktop 通过平台启动服务打开下载页，Browser 隐藏或使用浏览器导航；删除/禁用当前自覆盖入口。
- **12C：完全移除版本检查与更新 UI。** 全部交给 GitHub Releases、包管理器或其他分发渠道。

**建议验收**：12B 下不得执行进程终止或目录覆盖；离线/服务器失败不影响编辑器启动；下载地址和版本来源可配置并使用 HTTPS。

### 13. DataGrid 行排序交互

**问题与影响**

- 渲染控制列表和 Soflan 分组列表原来依赖 WPF/ListView 拖放排序；当前使用 Avalonia DataGrid，旧 `ListViewDragDropManager<T>` 已排除，排序入口实际缺失。
- DataGrid 虚拟化、多选、编辑单元格、拖动指示和滚动边缘自动滚动使直接移植旧管理器不可行。

**候选方案**

- **13A：实现 Avalonia DataGrid 专用拖放排序。** 最接近旧版体验，但需要完整处理虚拟化、多选、插入指示、自动滚动和撤销。
- **13B：先提供上移/下移命令和键盘操作。推荐。** 保持选中项、支持 `CanExecute` 边界和撤销记录，先恢复排序业务能力；拖放作为后续增强。
- **13C：首版禁止排序。** 只允许编辑数值和保存当前顺序，开发成本最低，但旧版排序工作流缺失。

**建议验收**：移动后模型顺序、显示顺序和保存后重载顺序一致；首行/末行、多选、过滤状态及撤销/重做有确定行为。

### 14. 48 个运行时绑定豁免的处理顺序

**问题与影响**

- 48 个视图仍关闭编译绑定，属性拼写、模板数据类型和父级路由错误只能在运行时暴露。
- 本轮 AOT 冒烟已经实际发现“构建成功但主题模板缺失”“反射图标崩溃”“本地化文字为空”，说明只靠构建不能证明完整 XAML 路径可用。

**候选方案**

- **14A：一次性消除全部 48 个豁免。** 最快得到完整编译期检查，但会同时改动大量视图、模板和绑定，回归面最大。
- **14B：按核心工作流分批移除。推荐。** 第一批主编辑器、工程设置和对象属性浏览器；第二批设置页与常用工具窗口；第三批等待音频、SVG 等延期模块恢复时处理。任何被业务修改的视图都必须顺带移除该视图豁免。
- **14C：无限期保留豁免。** 当前 Release/AOT 仍可构建，但长期依赖反射绑定，与 Native AOT 目标不一致。

**建议验收**：每批补齐根和模板 `x:DataType`、使用编译绑定、构造所有涉及视图并执行关键命令；批次完成后精确更新剩余豁免数。

### 15. 迁移验收测试门槛

**问题与影响**

- 仓库仍没有应用自身测试项目。
- 本轮三次 Native AOT 运行时问题都不是普通编译能发现的，因此“Debug/Release 0 错误”不足以作为迁移完成门槛。

**候选方案**

- **15A：只要求 Debug/Release/AOT 构建和人工冒烟。** 初始成本最低，但容易重复出现资源、空绑定、命令和空白渲染回归。
- **15B：建立分层的应用测试门槛。强烈推荐。** 包含 AXAML/资源/视图构造测试，ViewModel 命令和快捷键路由测试，Skia 非空像素测试，以及 Windows Desktop Native AOT 的新建/打开/编辑/保存启动冒烟；少量关键页面再做截图基线。
- **15C：立即建立完整端到端 UI 自动化和全页面截图基线。** 覆盖最强，但当前界面仍在迁移，基线维护和不稳定测试成本最高。

**建议验收**：测试可在本地和 CI 重复执行；失败能定位到视图/命令/像素或原生启动层；AOT 冒烟检查进程响应、fatal 日志和优雅退出。

### 16. 82 个编译排除源文件的清理策略

**问题与影响**

- 被排除文件仍位于活跃源码树，方便参考但会增加审计噪声、误引用和重复实现风险。
- 其中图形后端已由 6A 明确替代；音频与 IPC 则是明确延期，不能在尚无替代方案时一起删除。

**候选方案**

- **16A：长期保留全部文件并继续 `<Compile Remove>`。** 迁移参考最完整，但活跃目录会永久混合现行与废弃架构。
- **16B：现在删除全部 82 个文件。** 目录最干净，但会丢失音频、IPC 等延期功能的迁移参考，风险不可接受。
- **16C：按子系统验收后分批删除。推荐。** 图形 48 个在 Skia 测试门槛建立后清理；旧控件/拖放 8 个在第 13 项完成后清理；SVG 4 个在第 11 项范围稳定后处理；音频 20 个和 IPC 2 个在各自重新决策并有替代实现后处理。

**建议验收**：每批使用独立提交，记录删除文件的最后 Git 位置、替代实现和验证命令；删除前确认没有编译、资源、反射或文档引用。

### 推荐组合与回复格式

当前工程建议为：**`10B + 11B + 12B + 13B + 14B + 15B + 16C`**。

推荐实施顺序为：先做 **11B + 12B** 防止用户进入假功能；再做 **15B** 建立门槛；随后做 **10B + 14B** 清理事件与绑定；最后做 **13B + 16C** 恢复排序并分批清理旧源码。

用户可以直接回复完整组合，也可以只决定部分，例如：`10B + 11B + 12B，其他继续搁置`。未明确选择的编号继续保持待决策，不会自动采用推荐项。

| 时间 | 批次 | 状态 | 结果 |
|---|---|---|---|
| 2026-08-01 23:08 +08:00 | 当前未决策项复核 | 完成，待用户选择 | 确认只有第 10～16 项仍未决；按当前工作树更新为 42 处反射事件、2 个缺失目标、48 个编译绑定豁免和 82 个编译排除文件，并移除已完成启动页的过时隐藏建议。推荐组合登记为 `10B + 11B + 12B + 13B + 14B + 15B + 16C`，尚未视为用户决策。 |

## 2026-08-01 第五轮决策与实施记录

> 决策时间：2026-08-01 23:29 +08:00  
> 用户选择：`10B + 11C + 12C + 13A + 14A + 15B`；第 16 项继续延期。  
> 新增范围：第 17 项恢复 NAudio，并为 Desktop/Browser 提供不同的低延迟 `IWavePlayer` 工厂。  
> 冲突解释：第 17 项是比 11C 更具体、更新的音频要求，因此音频模块不在 11C 删除范围内；11C 用于删除 `InternalTest` 和未完成的 SVG 创建/属性编辑功能。  
> 测试语料：本机谱面目录为 `C:\Users\mikir\Desktop\音寄谱\拉面`；测试基础设施提供环境变量覆盖与缺失时的明确结果，不能把个人绝对路径作为 CI 唯一入口。

### 决策 10B：按事件语义分流

- 真正依赖 Avalonia 事件参数的 Pointer/Drag/Drop/Loaded 交互迁移到视图薄代码层或专用强类型 Behavior。
- 插值、打开对话框、复制模板等业务意图改为命令；Checked/Unchecked 优先改为双向绑定或命令。
- 清除字符串方法名静默失败；保留的任何事件适配都必须有明确目标类型、生命周期和 AOT 边界。

### 决策 11C：先删除未达门槛模块

- 删除 `Modules/InternalTest` 及其菜单、命令和视图。
- 删除未完成的 SVG 创建/属性编辑功能及其专属入口和专属类型；依赖审计中若发现解析/保存/绘制链只服务于该已删除功能，则一并清理并记录兼容性影响。
- 音频不随 11C 删除，因为第 17 项明确要求恢复其平台实现。
- 启动页已经按 8A 完成，不属于删除范围。

### 决策 12C：先删除自更新

- 删除版本检查、更新提示、下载、自覆盖、进程终止/拉起和更新命令行入口。
- 删除程序设置页中的更新配置与 UI；不保留不可达的“立即更新”按钮。
- 后续若重新考虑更新功能，按独立平台服务和发布包安全模型重新设计，不复活当前共享核心中的 Windows 自覆盖实现。

### 决策 13A：实现 Avalonia DataGrid 专用拖放排序

- 为渲染控制列表和 Soflan 分组列表恢复直接拖放排序。
- 使用 Avalonia `DataTransfer`/路由事件；处理虚拟化容器、多选、插入位置、拖动反馈、边缘自动滚动、模型顺序、选择保持和撤销边界。
- 不恢复只适用于旧 ListView/WPF 的拖放管理器。

### 决策 14A：一次性消除全部编译绑定豁免

- 为当前 48 个 `x:CompileBindings="False"` 视图补齐根和模板 `x:DataType`，迁移为编译绑定并删除所有局部豁免。
- 不以 `ReflectionBinding`、关闭全局开关或裁剪警告抑制作为替代方案。
- 每轮由 Release XAML 编译器暴露真实错误，直至豁免数为 0，并构造关键视图验证资源和命令路径。

### 决策 15B：建立分层应用测试

- 新增应用测试项目，覆盖 AXAML/资源/视图构造、关键命令和输入路由、DataGrid 排序、音频工厂平台契约、Skia 非空输出与 Desktop Native AOT 冒烟。
- 使用 `C:\Users\mikir\Desktop\音寄谱\拉面` 作为本机真实谱面语料，执行发现、解析和关键格式回归；测试目录通过环境变量配置，以便其他开发机和 CI 提供等价语料。
- 测试必须能区分“语料目录缺失”和“谱面解析失败”，不得用跳过失败来伪造通过。

### 决策 16：继续延期

- 当前不主动删除其余 `<Compile Remove>` 源文件。
- 11C、12C 明确要求删除的功能代码不受第 16 项延期保护；它们属于本轮产品范围删除，不是一般迁移清理。

### 决策 17：恢复 NAudio 平台播放工厂

- 在共享音频契约中定义 `INAudioWavePlayerFactory`，公开 `Task<IWavePlayer> CreateDefaultWavePlayer()`。
- Desktop 项目实现该接口，沿用原项目设置选择 WASAPI 或 ASIO，并把 Windows/设备枚举依赖留在 Desktop 平台层。
- Browser 项目引用 `MikiraSora/NAudio.BrowserAudioWorklet` 的实际发布包/项目，根据其真实 API 提供低延迟 `BrowserAudioWorkletPlayer`，不伪造不存在的类型或初始化流程。
- 共享 `IAudioManager` 只依赖工厂抽象；平台实现通过各入口项目注册。新增路径必须通过裁剪/AOT 分析，不能用警告抑制掩盖动态代码问题。

### 第五轮实时进度

| 时间 | 批次 | 状态 | 结果 |
|---|---|---|---|
| 2026-08-01 23:29 +08:00 | 决策登记与并行拆分 | 进行中 | 已登记 10B、11C、12C、13A、14A、15B、17 与第 16 项延期；音频平台工厂、DataGrid 拖放和编译绑定审计并行进行，主流程开始功能删除与事件迁移。 |
| 2026-08-01 23:43 +08:00 | 11C/12C 源码删除 | 已落地，待编译验证 | 已删除 `Modules/InternalTest`、更新器/更新对话框/更新命令执行器、`EditorSvgObjectControlProvider`、`SvgPrefab` 领域模型、两套专属解析与写出命令及编辑器绘制目标；程序设置页同步删除更新配置并启用根类型编译绑定，`System.CommandLine` 依赖已移除。通用 `PreviewSvgGenerator` 与 `Svg` 库保留。兼容性影响：旧谱面中的编辑器私有 `SvgPrefab` 扩展不再解析、保留或重新写出；标准 Ongeki 谱面对象和 SVG 预览导出不受影响。第 16 项延期范围内的已排除 OpenGL 参考源码暂不整理。 |
| 2026-08-01 23:43 +08:00 | 17 音频平台工厂 | 进行中 | 已核实上游 `NAudio.BrowserAudioWorklet` 当前没有 NuGet/Release；以固定提交的源码子模块和 Browser 项目引用接入。共享接口、Desktop WASAPI/ASIO 工厂和 Browser AudioWorklet 工厂已落盘，正在恢复共享 NAudio 管线并等待构建验证。 |
| 2026-08-01 23:44 +08:00 | 首次核心 Debug 编译 | 发现 6 个错误，修复中 | 删除范围本身未出现残留类型错误；发现 Nyageki 格式化器缺少仍需的 `System.Text`（2 处，已修复）、DataGrid 排序辅助的只读列表索引错误（1 处，已交回第 13 项）以及恢复后的 `NAudioManager` 尚缺音效播放方法（3 处，已交回第 17 项）。其余为既有警告；待并行实现收敛后复编。 |
| 2026-08-01 23:48 +08:00 | 14A 全量编译绑定审计 | 完成审计，开始分层修复 | 删除范围后仍有 46 个整文件豁免。21 个视图可直接补根类型，15 个需补模板类型；其余需要先修真实模型/绑定缺陷。关键阻塞包括：`FumenVisualEditorSettingsViewModel` 漏迁移、选择对象表和 Soflan 表为异构行、`SelectionFilter` 使用字符串 `DisplayMemberPath/SelectedMemberPath`、三个自定义控件漏掉 self-source、失效 `glView` 引用及若干静态类型错误。实施顺序按“模型与 self-source → 简单视图 → 同质模板 → 异构表/筛选器 → 全局开关与 AOT”执行，不以 `ReflectionBinding` 绕过。 |
| 2026-08-01 23:51 +08:00 | 13A DataGrid 行拖放 | 完成，待 15B 自动化覆盖 | 已新增基于 Avalonia `DataTransfer` 的强类型通用行排序行为，并接入渲染控制列表与 Soflan 分组列表。支持表内 token 校验、多选、虚拟化可见行命中、前/后/组内放置、插入指示、边缘自动滚动、选择恢复及撤销/重做；Soflan 支持叶节点同组/跨组移动，OGKR 按当前模型顺序写出。核心 Debug/Release 均 0 error。限制：不支持跨表或外部拖放，Soflan 组节点不可移动且仅作目标，右侧 Soflan 点表不在 13A 范围。 |
| 2026-08-01 23:57 +08:00 | 10B 反射事件迁移（主批次） | 进行中，剩余 21 处 | 已将程序颜色选择改为 `RelayCommand`，谱面编辑器 DragEnter/Drop 与渲染宿主加载改为强类型路由事件，检查器加载、两张明细表单击、Soflan 单选状态和音效开关改为视图薄事件/已有双向绑定，子弹与铃铛拖出改为显式 `PointerEventArgs` 流程，复制弹幕模板改为命令；无参数目标的 `FocusableChanged` 日志占位已删除。核心 Debug 0 error。当前所有剩余 `EventMethodBehavior` 均位于对象属性面板 6 个视图，正在并行迁移；音频波形绘制文件仍按第 16 项编译排除，因此本轮仅去掉其不可达 Loaded 反射入口。 |
| 2026-08-02 00:09 +08:00 | 10B 反射事件迁移 | 完成 | 对象属性面板 6 个视图的拖动入口已迁移为强类型 Pointer 事件，插值、沿轨道刷对象、合并等业务动作已迁移为 `RelayCommand`；全仓 AXAML 已无 `EventMethodBehavior`、`MethodName` 或 `$executionContext` 事件接线，未再被引用的 `EventMethodBehavior.cs` 已删除。保留的 `ActionExecutionContext` 仍用于编辑器键位路由与第 16 项延期源码，不属于反射方法查找。核心 Debug/Release 分支验证均为 0 error。 |
| 2026-08-02 00:09 +08:00 | 14A 编译绑定（简单视图与基础缺陷） | 阶段完成，剩余 26 个复杂视图 | 14 个简单视图已补齐根 `x:DataType` 并启用编译绑定；另修复 `RangeValue`、`CommonOperationButton`、`Toast` 的显式 self-source，避免覆盖调用方 `DataContext`。误迁移为空壳且类名错误的编辑器设置工具已恢复原项目的活动编辑器跟踪、设置切换、单位/时间格式选项和动态标题，并启用编译绑定。核心 Debug 0 error；当前仍有 26 个 `x:CompileBindings=False`，进入同质模板与异构表批次。 |
| 2026-08-02 00:09 +08:00 | 17 音频平台工厂 | 三端 Debug 通过，AOT 验证中 | 共享核心、Desktop、Browser 的 Debug 构建均已通过。由于 NAudio 2.3 的 Windows WASAPI 实现不满足当前 AOT 路径，已将共享实现和 BrowserAudioWorklet 源码统一到 NAudio 3 preview.19；Desktop 依赖图保留 WASAPI 并排除 ASIO 资源，正在继续实际 Native AOT 发布验证。 |
| 2026-08-02 00:31 +08:00 | 14A 全量编译绑定 | 完成 | 56 个核心 AXAML 中，51 个承载数据上下文的视图/控件已补齐根及模板强类型；其余 5 个为 `App.axaml` 和无数据上下文绑定的主题资源。全仓 `x:CompileBindings=False`、`ReflectionBinding`、字符串 `DisplayMemberPath/SelectedMemberPath` 以及反射事件接线均为 0；Debug/Release 的全局编译绑定开关均已设为 `true`。异构对象表和 Soflan 表通过强类型行适配器完成，筛选器已删除反射式 CheckListBox/CheckComboBox。核心 Debug 在全局开关生效后 0 error；完整 Release 亦为 0 error。 |
| 2026-08-02 00:51 +08:00 | 15B 真实谱面语料研究 | 完成研究，测试实现待落地 | 已递归确认本机语料共 8 个文件：1 个 `.nyageki`、1 个 `.nyagekiProj`、4 个 `.nyagekiScript`、1 个 WAV 和 1 个 PNG。测试入口采用 `ONGEKI_FUMEN_TEST_CORPUS_ROOT`，未设置时仅在 Windows 回退用户指定目录；显式错误路径判失败，默认目录缺失则给出明确跳过原因。主谱面采用解析、内存格式化、再解析的语义快照比较；工程文件真实反序列化，脚本只验证发现和可读性、不执行。语料中唯一未知命令为 11C 已明确删除的 `SvgPrefab`，测试将把这一预期丢弃边界写成显式断言。 |
| 2026-08-02 00:51 +08:00 | 17 第三方依赖可继承性复核 | 阻塞项处理中 | 上游固定提交 `c9bb476` 的 `Interactive` 配置确认为 20ms 且项目引用会携带 AudioWorklet 静态资源；但上游当前基于 NAudio 2.3，现有 NAudio 3/AOT 适配产生了 4 个未提交的子模块内修改。该状态无法由父仓库可靠继承，因此不能作为完成结果。将改为“干净固定提交 + 主仓可追踪 NAudio 3 兼容覆盖层”，并重新执行 Browser Release/AOT。 |
| 2026-08-02 01:03 +08:00 | 17 Browser 可继承依赖与 AOT | 完成 | `NAudio.BrowserAudioWorklet` 子模块已恢复为干净的固定提交 `c9bb4766e155a0ea59dfa1c789ea9e567d1b60bd`；NAudio 3 API 适配移至主仓跟踪的 `Dependencies/Directory.Build.targets` 与 `Dependencies/NAudio.BrowserAudioWorklet.NAudio3Compat`，干净检出即可复现。Browser Debug 非增量构建和关闭 AOT 的 Release 非增量构建均为 0 error；随后完整 Release AOT 发布约 387 秒成功，产物包含原生 WebAssembly、`naudio-audio-worklet.js` 和 processor 及其压缩版本。发布仍报告既有 `NU1507`、SkiaSharp `NU1903`，以及 Dock/Avalonia/ReactiveUI 等第三方裁剪警告；这些不是音频实现产生的零错误结论，也不会被记为“零警告”。 |
| 2026-08-02 01:17 +08:00 | 17 共享音频契约复核 | 阶段完成，最终三端复验待测试落地后执行 | 删除了共享命名空间与 `NAudioImpl` 中重复的 `AudioOutputType`，设置页、管理器和 Desktop 工厂现共用同一枚举；Native AOT 下 ASIO 回退 WASAPI 已改为全异步路径，不再同步等待。文件读取工厂现声明平台真实支持格式：Desktop 为 MP3/WAV/AIFF/ACB，Browser 为 PCM/IEEE-float WAV 与 AIFF；Browser 不再在文件选择器中宣称支持 MP3/ACB，并会在进入 ACB 转码前明确拒绝。调整后 Core Debug 构建为 0 error、23 个既有警告；待测试工程完成后重新执行 Desktop/Browser、Desktop Native AOT 与 Browser AOT，之前的 AOT 成功记录不替代最终代码复验。 |
| 2026-08-02 01:45 +08:00 | 15B Headless 快速测试与迁移缺陷修复 | 完成 | 新增测试项目已注册到 solution；修正非 GUI 测试宿主重复挂载 Debug DeveloperTools、旧 `LocalizeConverter` 被错误迁为单值转换器且把普通 Avalonia 标记扩展塞入 `MultiBinding`、对象池管理器遗漏源码生成 DI、DataGrid 拖拽引用不存在的主题键，以及 Nyageki 显式 Soflan 与构造器哨兵重复累积。测试工程 Debug 构建 0 error；`Category!=ExternalCorpus` 共 92/92 通过，包含 51 个视图逐一构造/挂载/布局、应用资源、DataGrid、快捷键、Skia、音频契约和内置谱面 round-trip。仍保留 SkiaSharp `NU1903` 与核心既有编译警告，未记为零警告。 |
| 2026-08-02 01:50 +08:00 | 15B 外部“拉面”语料回归 | 完成 | 使用 `ONGEKI_FUMEN_TEST_CORPUS_ROOT=C:\Users\mikir\Desktop\音寄谱\拉面` 运行真实语料测试，5/5 通过；与快速集合计 97/97。为避免把模型实现细节和无语义顺序误报为迁移缺陷，BPM 数量按实际枚举项统计而不采用含哨兵语义的 `Count` 属性；round-trip 同时比较语义指纹和按序数排序的完整序列化行集合，从而仍可发现字段丢失、重复或值变化，但允许相同 TGrid 对象重新排序；工程文件清单明确忽略 `.git`/`.svn`/`.hg` 元数据。11C 删除的 1 类 `SvgPrefab` 命令仍作为预期兼容边界显式断言。测试工程 Debug 构建 0 error，快速集复跑 92/92 通过。 |
| 2026-08-02 02:05 +08:00 | 15B 测试缺口与断言质量复核 | 完成，平台复验继续 | 通过实证伪变异抽查本轮高风险逻辑：DataGrid `After` 插入边界和 Nyageki 首个显式 Soflan 哨兵替换均被既有测试杀死；键位冲突阈值从 `> 1` 改为 `> 2` 时旧测试仍通过，确认真实存活变异。现已为冲突分支增加 Error 日志副作用与分支消息断言，同一变异会按预期失败。另补 `LocalizeConverter` 的多参数顺序、null 字符串化和参数不足异常测试。非增量重建后快速集 96/96、外部语料 5/5，总计 101/101；无跳过。断言质量审计无零断言、仅平凡断言或恒真断言，平台音频真实运行时仍由后续平台/AOT及依赖测试覆盖。 |
| 2026-08-02 02:05 +08:00 | 17 NAudio 3 上游状态机测试接线 | 完成 | 固定上游提交的原始 NAudio 2.3 配置本来可通过 91 项，但不能证明主仓实际使用的 NAudio 3 覆盖层。已扩展主仓 `Dependencies/Directory.Build.targets`：生产与测试项目统一 `3.0.0-preview.19`，通过主仓链接的 Span/旧数组适配测试桩保持子模块零修改，并以内部显式接口保留“直接写入调用方数组”的断言，不使用反射或动态调用。实际 NAudio 3 配置下 Release 91/91 通过、0 失败/0 跳过；子模块状态干净，HEAD 与 gitlink 均为 `c9bb4766e155a0ea59dfa1c789ea9e567d1b60bd`。这些测试覆盖假 `IAudioWorkletBridge` 状态机，不冒充真实浏览器 AudioContext/AudioWorklet 集成。 |
| 2026-08-02 02:22 +08:00 | 15B/17 最终测试与平台矩阵 | 完成 | Solution 级发现列出 101 个展开用例；设置 `ONGEKI_FUMEN_TEST_CORPUS_ROOT=C:\Users\mikir\Desktop\音寄谱\拉面` 后全量 101/101 通过、0 失败/0 跳过（快速集 96/96，真实语料 5/5）。Core、Desktop、Browser 的 Debug/Release 均 0 error。Desktop `win-x64-aot` 发布成功，43,757,056 字节原生 EXE 启动后存活 8 秒；Browser Release 全量 AOT 约 402 秒发布成功，当前启动清单引用 `dotnet.native.39uhyvxksp.wasm`（48,173,323 字节）和 `NAudio.BrowserAudioWorklet.icy10roiyk.wasm`，并包含 AudioWorklet 主脚本/processor 的原始、Brotli、GZip 资源。活动 AXAML 编译绑定豁免、反射绑定、字符串成员路径、反射事件行为均为 0；`IPCHelper`/旧 OpenGL SVG 仅作为第 16 项延期且被 `<Compile Remove>` 排除。保留依赖漏洞、第三方裁剪/AOT、旧 JSON 反射序列化和 WASM P/Invoke 收集警告，未记为零警告。 |
| 2026-08-02 02:30 +08:00 | 17 Browser AOT 干净产物复核 | 完成 | 另发布到全新目录 `src/OngekiFumenEditor.Avalonia.Browser/bin/Release/net10.0-browser/publish-clean-20260802-0225`，避免复用标准增量发布目录中的历史哈希文件。干净目录只有当前未压缩哈希产物 `dotnet.native.39uhyvxksp.wasm`（48,173,323 字节）与 `NAudio.BrowserAudioWorklet.icy10roiyk.wasm`（42,261 字节），无旧哈希副本；两份 WASM、`naudio-audio-worklet.js`（20,873 字节）和 `naudio-audio-worklet-processor.js`（10,843 字节）的原始、Brotli、GZip 版本均齐全。后续 Browser 产物验收应以该干净目录为准。 |

## 2026-08-02 当前剩余决策复核（第六轮）

> 复核时间：2026-08-02 03:01 +08:00  
> 严格结论：第 1～17 项都已有当前决策，没有遗漏选择的旧编号。第 10～15、17 项已经实施；第 16 项已明确延期；`IFileAssociationService`、`IPCHelper`、11C/12C 删除范围也都有当前处理结论。第 7 项音频延期已被更新且更具体的第 17 项取代。  
> XAML 结论：活动源码中的失效 `ActualWidth` 绑定、`x:CompileBindings="False"`、`ReflectionBinding`、字符串成员路径和 `EventMethodBehavior` 当前均为 0，因此没有新的活动 XAML 迁移方案需要选择。

### 新发现且需要产品决策的边界

#### 18. 音频波形可视化

**当前状态：** `AudioPlayerToolViewerView` 仍显示 150 像素的波形宿主、缩放/采样参数、启用开关和波形选项；但负责创建渲染控件、采样并绘制波形的 `AudioPlayerToolViewerViewModel.WaveformDrawing.cs` 仍被 `<Compile Remove>` 排除，当前主 ViewModel 只保留设置属性，实际宿主不会接入绘制内容。

- **18A：使用现有 Avalonia.Skia lease 路径重新实现波形宿主。长期完整迁移推荐。** 不直接启用依赖旧渲染上下文的排除文件；把采样、取消、尺寸更新和生命周期迁到新的强类型 Avalonia 控件/行为，并增加非空像素与卸载测试。
- **18B：暂时隐藏波形宿主、波形选项和全局可视化设置。当前延期范围推荐。** 保留音乐与音效播放，避免向用户展示空白功能；以后实施 18A 时再恢复入口。
- **18C：维持当前可见空白宿主。** 不推荐；与 11C 的“可见功能必须达到门槛”原则冲突。

#### 19. 非零音频偏移

**当前状态：** 音频调整窗口及菜单入口可见；零偏移只复制原 WAV，任何非零偏移都会返回 `Audio offset is not implemented in Avalonia migration yet.`。第 17 项恢复的是播放工厂，不会自动补齐离线音频重写。

- **19A：实现平台中立的 WAV 帧偏移服务。推荐。** 正偏移补静音帧、负偏移裁剪帧，按采样帧边界处理 PCM/IEEE-float WAV，先成功生成临时文件再原子替换目标，并对谱面 TGrid 重算保持现有撤销语义。
- **19B：在实现前隐藏音频调整菜单及窗口。** 不保留可执行到失败终点的入口。
- **19C：继续保留显式失败。** 便于开发观察，但不适合作为正式可用功能。

#### 20. Native AOT、ASIO 与平台能力展示

**当前状态：** 普通 Desktop/JIT 构建可创建 ASIO；`win-x64-aot` 中 NAudio ASIO 仍依赖运行时生成委托，所以选择 ASIO 会直接回退 WASAPI。共享音频设置页仍向 Browser 展示 `WaveOut`/`WASAPI`/`ASIO`，并展示只在 Windows x64 生效的 SoundTouch 变速设置；Browser 的变速滑块写入后实际播放速率仍为 1。Browser 文件读取目前只支持 `.wav`/`.aif`/`.aiff`，其中 WAV 仅接受 PCM/IEEE-float，MP3、ACB 和压缩 WAV 不支持；当前音乐播放器还会把解码后的整首音频驻留内存。

- **20A：只发布 AOT/WASAPI Desktop，并按平台能力隐藏无效设置。** 单产物最简单，但明确放弃 AOT 包中的 ASIO。
- **20B：AOT/WASAPI 作为主包，同时提供 JIT/ASIO 专用包；两端都使用能力服务控制设置 UI。推荐。** 同时满足 Native AOT 首发与原项目 ASIO 用户，不让 Browser 或 AOT 用户选择无效选项。
- **20C：自行实现 AOT 安全的 ASIO 互操作，并补 Browser/非 Windows 的可移植变速。** 单一功能矩阵最完整，但成本和原生测试面最大。

##### 第 20 项具体运行矩阵（2026-08-02 03:22 +08:00）

| 运行目标 | 实际输出后端 | 选择 ASIO 时 | 变速 | 当前 UI/配置问题 |
| --- | --- | --- | --- | --- |
| Windows Desktop 普通 JIT | WASAPI 或 NAudio ASIO；旧 `WaveOut` 值也回退 WASAPI | 真正创建 `AsioOut` | Windows x64 可使用 SoundTouch | 基本符合旧项目，但仍需真实设备/驱动验证 |
| Windows Desktop Native AOT | 只打包 NAudio WASAPI；项目定义 `NATIVE_AOT` 并排除 `NAudio.Asio`/`NAudio.WinMM` | `CreateAsioPlayer()` 直接返回 WASAPI，目前没有用户提示 | win-x64 SoundTouch 路径仍需实际工作流验证 | 下拉框仍显示 ASIO，用户选择与实际后端不一致 |
| Browser WASM/AOT | 工厂无条件创建 `BrowserAudioWorkletPlayer(Interactive)`，不读取 `AudioOutputType` | 选择值完全不影响输出 | `NAudioManager` 因非 Windows 不创建变速 provider，读取速度恒为 1 | 设置页仍显示 WaveOut/WASAPI/ASIO 和 EnableVarspeed，播放器仍显示速度滑块 |
| Linux/macOS Desktop | 当前没有对应项目与音频工厂；普通 Desktop TFM 本身是 Windows TFM | 不适用 | 现有 SoundTouch DLL 为 win-x64 | 只能称为预留抽象边界，不能称为可运行支持 |

这不表示 Native AOT 或 Browser AudioWorklet 构建失败：两者已经成功发布。问题是**能力声明不真实**。当前代码为了让 AOT 可启动，选择了“ASIO 不可用时静默使用 WASAPI”；Browser 则始终使用 Worklet，却复用了 Windows 设置页。

无论最终选择 20A、20B 还是 20C，都应增加一个共享可读取、平台项目实现的音频能力契约，至少提供：有效输出类型、是否支持变速、支持的输入格式，以及请求后端与实际后端。设置页和播放器只展示有效能力；旧配置请求不可用后端时应记录并向用户明确说明回退，不能静默改变含义。

- 选择 **20A** 时只维护一个 Windows AOT 包：包体和测试矩阵最简单，但正式承诺中必须写明“不支持 ASIO”。
- 选择 **20B** 时维护两个 Windows 包：AOT/WASAPI 是默认包，JIT/ASIO 是有 ASIO 驱动用户的兼容包；发布、下载说明和设备测试翻倍，但同时满足本轮的 Native AOT 要求和旧项目 ASIO 能力。
- 选择 **20C** 时需要替换或重写 NAudio ASIO 的动态委托互操作，并分别解决 Browser、Linux、macOS 的解码和变速；它不是当前 XAML/DI 修补能够顺带完成的工作。

**推荐判断：** 如果 ASIO 是必须保留的正式功能，选择 **20B**；如果首发只要求低延迟 WASAPI，选择 **20A** 更稳。当前记录推荐 20B，是因为用户第 17 项明确要求 Desktop 延续原项目的 WASAPI/ASIO 选择，同时第 5 项又要求 Native AOT。

#### 21. `SvgPrefab` 旧谱面兼容

**当前状态：** 11C 已删除创建/属性编辑 UI、领域类型、解析器和写出器；本机 ramen 语料确认有 1 条 `SvgPrefab`，当前 round-trip 会按已决策排除边界丢弃它。

- **21A：确认永久不兼容 `SvgPrefab`。** 保持当前实现，并在格式说明中明确数据会丢失。
- **21B：只恢复兼容性数据层，不恢复编辑 UI。推荐。** 解析并原样或强类型保存/写回该扩展，编辑器不提供创建和属性修改入口。
- **21C：完整恢复领域模型、绘制与编辑 UI。** 功能最完整，也重新引入 11C 刚删除的维护面。

### 已明确延期、以后才需要复议

| 项目 | 当前决定 | 以后建议 |
| --- | --- | --- |
| 第 16 项编译排除源码 | 继续延期；当前实际为 59 个文件：旧图形后端 48、旧拖放/控件 8、旧波形 partial 1、`Startup`/`IPCHelper` 2 | 采用 16C 分批清理；先独立审计并删除已被 Skia/DataGrid 替代的 56 个文件，波形和 IPC 分别等待第 18 项与 IPC 决策 |
| `InternalTest` | 11C 已删除 | 推荐永久删除；如确有开发价值，只恢复为显式开发构建功能，不进入正式菜单 |
| 自更新 | 12C 已删除 | 不恢复旧自覆盖更新器；若需要更新提醒，推荐只做版本检查和 HTTPS 下载页跳转（原 12B） |
| `IFileAssociationService` | UI 和服务均暂时移除 | 等安装包/分发策略确定后再做 Desktop 平台服务；Browser 不提供该能力 |
| `IPCHelper` / 单实例 | 源码参考保留但排除编译 | 先决定是否允许多实例及第二实例参数/`--wait` 协议；需要时使用 Desktop 平台服务，Windows 采用互斥体/命名管道，其他系统独立实现 |
| Linux/macOS Desktop | 5A 只要求保留架构边界，不要求本轮功能对等；当前普通 Desktop TFM 为 `net10.0-windows10.0.19041.0`，输出固定使用 WASAPI/ASIO，解码依赖 Windows 能力，并携带 win-x64 SoundTouch DLL | 当前明确以 Windows Desktop + Browser 为发布范围；若要宣称 Linux/macOS 支持，必须拆出对应入口项目，新增输出、解码和变速实现并做平台构建/运行验证 |

### 不需要产品选择但仍需执行的验证与治理

- 在真实 WASAPI/ASIO 设备上验证选择、初始化失败回退、播放、停止和释放；当前只有构建、AOT 启动与状态机证据。
- 在真实浏览器中验证用户手势授权、首次创建 AudioContext、AudioWorklet 播放、标签页挂起及中断恢复；当前 91 项测试使用 fake bridge。
- 补 Windows Native AOT 的“打开真实工程/播放/编辑/保存”工作流；当前 Native AOT 自动证据是启动存活，领域读写由 Headless 语料测试覆盖，两者尚未形成同一条端到端路径。
- CI 需要提供可再分发语料或受控 artifact，并对外部语料测试强制 0 skip；本机 ramen 5/5 不能自动代表 CI。
- 单独处理 `SkiaSharp`、`Tmds.DBus.Protocol` 的 NuGet 安全公告，以及第三方裁剪/AOT、旧 JSON 反射序列化、WASM P/Invoke 和 `NU1507` 警告；当前是已登记风险，不是零警告状态。

### 当前推荐优先级

1. 先决定 **18、19、20、21**，因为它们直接决定用户可见功能与旧谱面数据是否完整。当前建议组合为 **`18B（短期，后续 18A）+ 19A + 20B + 21B`**。
2. 随后执行 **16C 第一批**，清理已被验证替代的旧图形和旧控件源码。
3. 发布前完成真实设备、真实浏览器、Native AOT 工作流与依赖安全验证。
4. 自更新、文件关联、IPC、InternalTest 和 Linux/macOS 功能对等继续维持当前延期，不必阻塞上述工作。

> 2026-08-02 03:13 +08:00 音频实现协作者完成只读闭环审计：复核结果与第 18～20 项一致，并额外确认 Browser 格式/内存边界及 Linux/macOS 尚无可运行 Desktop 实现；未修改或构建任何文件。

## 2026-08-02 第七轮决策与实施记录

> 决策时间：2026-08-02 03:31 +08:00
> 本节覆盖第六轮对 18、19、20、21 的推荐，但保留旧记录作为决策历史。第 16 项其余排除源码仍延期；其中波形与 `Svg*` 相关范围分别由新的第 18、21 项明确解冻并重新迁移。

### 本轮最终决策

| 编号 | 用户决策 | 实施口径 |
| --- | --- | --- |
| 18 | 使用 Skia 完全迁移和实现 | 以 Avalonia Skia lease 和强类型控件生命周期恢复真实波形采样、缩放、限帧、取消和绘制；不重新启用旧 OpenGL 宿主。 |
| 19 | 19A | 新增平台中立 WAV 帧偏移服务：正偏移补静音帧、负偏移裁剪整帧，支持 PCM/IEEE-float，临时文件成功后再替换目标，并保持谱面 TGrid 重算与撤销语义。 |
| 20 | 20B | Windows 默认发行 `win-x64-aot`（WASAPI），另发 JIT/ASIO 兼容包；共享能力服务决定可见后端、变速和格式能力，旧配置不可用时给出明确回退信息。Browser 只显示其真实 Worklet 能力。 |
| 21 | 开始重新迁移实现 `Svg*` 物件相关功能 | 恢复领域模型、Nyageki/OGKR 解析写出、创建/属性编辑 UI 和 Skia 绘制链；保留原源码注释并改为 Avalonia、编译绑定、AOT 友好的实现，不恢复旧 OpenGL 后端。 |

### 第七轮实时进度

| 时间 | 批次 | 状态 | 结果 |
| --- | --- | --- | --- |
| 2026-08-02 03:31 +08:00 | 决策登记 | 完成 | 已冻结 18、19A、20B、21 的实施口径；`SvgPrefab` 不再是允许丢弃的兼容边界，真实 ramen 语料中的 1 条命令必须解析并在 round-trip 后保留。 |
| 2026-08-02 03:31 +08:00 | 实施前基线 | 完成 | `dotnet build .\OngekiFumenEditor.Avalonia.sln -c Release --no-incremental -v:minimal` 退出码 0、0 error、117 个既有 warning，用作本轮回归基线；不把既有依赖漏洞、裁剪和反射序列化警告记为本轮新增成功条件。 |
| 2026-08-02 03:31 +08:00 | 四线实施 | 进行中 | 波形、WAV 偏移、音频能力/双发行、Svg* 功能链并行研究与实现；完成一项即在本表追加构建、测试和残余平台边界。 |
| 2026-08-02 04:00 +08:00 | 18 Skia 波形实现 | 已落地，待动态测试 | 已恢复强类型 Skia 渲染宿主、渲染会话生命周期、采样峰值准备、单/双声道几何、缩放和限帧，并增加卸载取消与几何测试；没有重新启用旧 OpenGL 波形 partial。 |
| 2026-08-02 04:00 +08:00 | 19A WAV 偏移 | 已落地，待动态测试 | 已新增 `IWavAudioOffsetService` 及平台中立 WAV 实现，并通过事务层接回音频调整窗口；正偏移补整帧静音，负偏移裁剪整帧，临时文件成功后再替换目标，相关正负零偏移和失败原子性测试已写入。 |
| 2026-08-02 04:00 +08:00 | 20B 能力模型与双发行 | 已落地，待平台发布验证 | 已新增共享音频能力契约和 Desktop/Browser 实现；设置页与播放器按真实能力隐藏无效后端及变速，配置回退会给出原因；新增 `win-x64-jit` profile，AOT 工厂不再静默接受 ASIO。 |
| 2026-08-02 04:00 +08:00 | 合并态核心 Release 构建 | 通过 | 三条音频改动共同存在时，核心项目 `Release --no-incremental` 为 0 error、110 个 warning；警告仍含既有 NuGet 安全公告、第三方空值分析、裁剪/AOT 和 JSON 反射序列化问题。Svg* 迁移此时尚未并入，因此该构建不能替代第 21 项最终验证。 |
| 2026-08-02 04:00 +08:00 | 21 Svg* 完整迁移 | 进行中 | 已确定采用 `Svg.Skia` 的 Skia 原生路线，恢复领域、Nyageki/OGKR、工具箱/属性编辑、编辑器绘制和真实 ramen 往返；明确不复制 WPF `DrawingGroup`、SharpVectors 或旧 OpenGL 绘制实现。 |
| 2026-08-02 04:10 +08:00 | 18 Skia 波形动态验证 | 完成 | 首轮像素测试真实捕获到“回调已执行但截帧全黑”，据此将独立波形宿主改为在 Avalonia Skia lease 内直接绘制 `SKPath`/覆盖层；修正后波形组 11/11 通过，包含真实波形颜色像素、单/双声道几何、重采样、限帧、卸载停止和异步取消。 |
| 2026-08-02 04:10 +08:00 | 19A WAV 偏移动态验证 | 完成 | WAV 服务与事务组 12/12 通过；覆盖正/负/零偏移、整帧量化、8-bit PCM 静音、float 双声道、过量裁剪、RIFF chunk/padding 保留、不支持格式、失败不污染目标，以及源码生成 DI 单例解析。 |
| 2026-08-02 04:13 +08:00 | 20B JIT/ASIO 伴随包 | 通过，中间态 | 首次发布发现 Desktop 平台文件缺少显式 `System` 导入并已修复；随后 `win-x64-jit` 发布成功，产物包含 `NAudio.Asio.dll`、`NAudio.WinMM.dll`、`NAudio.Wasapi.dll` 与 `SoundTouch.dll`。该结果证明 ASIO 依赖未被 AOT 条件误裁，但仍需在 Svg* 合并后最终重发。 |
| 2026-08-02 04:15 +08:00 | 20B Browser 能力闭环 | 通过，中间态 | Browser Release 非增量构建为 0 error、117 个 warning，固定 AudioWorklet 能力服务、新工厂构造注入和共享能力 UI 均可编译；保留 `NU1507`、SkiaSharp 安全公告、并行编译器文件锁 warning、第三方裁剪/AOT 和 WASM P/Invoke 收集 warning。 |
| 2026-08-02 04:25 +08:00 | 21 Svg.Skia 依赖闭环 | 完成，中间态 | 实测旧 `Svg 3.x` 与 `Svg.Skia 5.1.1` 会因重复 `SvgElement` 类型产生 `CS0433`，因此移除旧包并统一到 `Svg.Skia 5.1.1`；同时将 SkiaSharp 对齐到 `3.119.2`、HarfBuzzSharp 对齐到 `8.3.1.3`，并按新版 `SKMatrix44.Concat` API 保持既有矩阵组合顺序。Svg 领域模型已经开始恢复，完整解析、绘制、UI 与真实语料验收尚未完成，不能据此把第 21 项标记为完成。 |
| 2026-08-02 04:31 +08:00 | 21 核心中间构建 | 失败，已定位 | 核心 Debug 非增量构建已越过 SkiaSharp 3.119.2 的矩阵 API 变更，仅剩正在并行写入的 `SvgPrefabCommandParser.cs:21` 一个 `CS8506`（switch 表达式共同类型未显式声明）；该错误属于未完成的格式层，已定位为把结果显式声明为 `SvgPrefabBase`，修复后再复建。本次 57 个 warning 仍含既有依赖与源码分析项。 |
| 2026-08-02 04:38 +08:00 | 21 真实语料测试设计复核 | 完成，待实现 | ramen 的 29 类命令必须全部有唯一 parser，唯一 `SvgPrefab` 必须解析为 `[SVG_STR]`，并精确保留颜色相似度、旋转、偏移、透明度、亮度、缩放、容差、T/X 网格、`ま`、字体、颜色 ID 1021、流向和行高；首次规范化会把原文件缺省的 `IsForceColorful=False`、`ColorfulLaneColorId=1021`、曲线工厂显式写出。语义指纹需新增 Svg 数量，原始对象和 reparse 对象都要执行字段断言；另用确定性合成图形覆盖 `[SVG_IMG]`、OGKR 和非空 Skia 像素，避免平台字体差异。已登记剩余格式风险：相对图片路径目前会绝对化、含 `]`/逗号的未转义字段、未知未来字段写回丢失。 |
| 2026-08-02 04:42 +08:00 | 21 格式层修复复建 | 通过，中间态 | 将 Nyageki 类型分派结果显式声明为 `SvgPrefabBase` 后，同一核心 Debug 非增量构建为 0 error、57 个 warning；`Svg.Skia`、SkiaSharp 3.119.2、领域集合及当前 Nyageki/OGKR 格式层能够共同编译。Skia 编辑器绘制、Svg→Lane 操作 UI 和完整测试此时尚未并入，21 仍为进行中。 |
| 2026-08-02 04:51 +08:00 | 20B 回退语义与发布链复核 | 完成，待最终发布 | 独立审查发现 Browser 会把旧 ASIO/WaveOut/WASAPI 配置静默映射为 Worklet，且 AOT 固定后端设置页会在保存其他选项时覆写 JIT/ASIO 偏好；现已改为显式 `UnsupportedBackend → BrowserAudioWorklet` 回退并记录 warning，只有真正可选后端的 JIT profile 才写回请求值。能力与设置语义测试 8/8 通过。仓库 Release workflow 已新增 `AOT_WASAPI_PRIMARY` 与 `JIT_ASIO_COMPANION` 两个 artifact，分别断言排除 ASIO/WinMM 与包含 ASIO/WASAPI/WinMM。真实 ASIO 驱动枚举、格式编码级能力描述仍作为低风险后续边界，不在本轮扩展。 |
| 2026-08-02 05:00 +08:00 | 21 Svg 首轮自动化 | 通过，中间态 | 已加入领域集合/深拷贝、Nyageki 与 OGKR 全字段往返、Unicode 缺失图片路径、Skia 非空像素、矢量段、文本和强类型操作视图测试。首轮 6/7，唯一失败为普通 `[Fact]` 未初始化 parser 所需应用 IoC；Nyageki/OGKR 两项改用 `[AvaloniaFact]` 消除顺序依赖后 7/7 通过。同时复核并恢复旧加权 RGB 距离（避免默认阈值把所有颜色误映射），Skia `Conic` 改用实际权重的有理二次公式；仍待直接验证颜色阈值和 Svg→Lane。 |
| 2026-08-02 05:10 +08:00 | 21 Browser WASM 原生链闭环 | 通过，中间态 | Browser 项目显式对齐 `HarfBuzzSharp.NativeAssets.WebAssembly 8.3.1.3` 与 `SkiaSharp.NativeAssets.WebAssembly 3.119.2`，消除 Avalonia 11 旧传递原生资产 `2.88.9` 与 Svg.Skia/SkiaSharp `3.119.2` 托管 ABI 的符号冲突；Release/AOT WASM 原生链接已成功。Svg、真实 ramen 语料和 AXAML 定向验证合计 67/67，通过后仍安排一次合并态全量测试与最终发布复验。 |
| 2026-08-02 05:13 +08:00 | 21 SVG 专用颜色兼容边界 | 已修正，待回归 | Git 历史确认 1020/1021/1022 是 2022 年为 SVG→左/中/右轨加入、2024 年随 SVG 收缩从全局 `AllColors` 删除的编辑器专用 ID。现保留稳定 ID 并新增 `SvgPrefabColors` 专用集合供 SVG 解析、颜色匹配和转轨使用，但不重新放入普通彩色轨解析、检查器和全局颜色下拉；这样既能精确读写 ramen 的 `ColorfulLaneColorId[1021]`，又不扩大普通谱面格式的有效颜色域。全量测试最后一个失败已定位为 JIT 设置测试未使用 Avalonia/IoC 宿主，测试已改为 `AvaloniaFact`，产品日志路径保持不变。 |
| 2026-08-02 05:30 +08:00 | 变异恢复回归（kimi-code 接管 codex 会话 019fbc61 继续） | 通过 | 三处伪变异（加权颜色阈值、越界 Svg→Lane 防护、AOT 保留 ASIO 偏好）恢复源码后，对应三项定向测试 3/3 回绿；含 ramen 语料的全量测试 143/143、0 跳过，确认工作树恢复绿色状态。 |
| 2026-08-02 05:34 +08:00 | solution Release 非增量复建 | 通过 | 完整 solution 非增量 Release 构建退出码 0、0 error（仍为既有警告集）。 |
| 2026-08-02 05:36 +08:00 | AOT 最终冒烟 | 失败，已定位修复 | `win-x64-aot` 发布成功但 EXE 启动即崩：`TypeInitializationException`，`ToolViewModelTypeCollectedActivator` 静态构造抛出 `ArgumentException: 相同键 AudioPlayerToolViewerViewModel`。根因：Gekimini 源生成器 `TypeCollectedActivatorGenerator` 对 partial 类的每个语法声明各收集一次，第 18 项将该 VM 拆为两文件后产出重复字典键；Debug/Release 生成产物均可复现（计数=2），但测试宿主 `TestApplication` 直接继承 `App`、不经过 `OngekiFumenEditorApp.RegisterServices`，导致全量测试此前未覆盖该激活器初始化路径。修复：生成器按 `FullClassName` 去重；新增 `ToolViewModelTypeCollectedActivatorTests` 回归测试，修复前红（与 AOT 相同异常）、修复后绿。 |
| 2026-08-02 05:51 +08:00 | 修复后全量与双 Windows 发布 | 通过 | 含新回归测试的全量 144/144、0 跳过；`win-x64-aot` 重发成功，53,153,792 字节 EXE 10 秒存活并完成完整启动（主题/语言/布局/键位路由均初始化）；`win-x64-jit` 发布产物包含 `NAudio.Asio.dll`、`NAudio.WinMM.dll`、`NAudio.Wasapi.dll`、`SoundTouch.dll`，EXE 10 秒存活。 |
| 2026-08-02 06:02 +08:00 | Browser AOT 最终发布 | 通过 | 修复后 Browser Release/AOT 发布成功（约 570 秒）；`dotnet.js` 仅引用本次构建的 56,496,934 字节 `dotnet.native.baujsjs5ui.wasm`，无旧哈希 WASM 被引用（增量目录中的旧文件不影响清单）；AudioWorklet 主脚本与 processor 的原始/br/gz 资源齐全。第七轮 18/19A/20B/21 全部验收边界均已具备对应测试或发布证据。 |

### 本轮验收边界

- 波形必须走产品中的 Avalonia/Skia 渲染路径，并以非空像素、尺寸更新、停止/卸载后不继续渲染作为自动化证据。
- WAV 偏移必须覆盖正、负、零偏移、非帧对齐时的取整策略、过量负偏移、无效/不支持格式，以及失败不污染目标文件。
- 两个 Windows 发布配置都必须能构建；AOT 图不得包含 ASIO，JIT 包必须保留 ASIO 依赖。真实 ASIO 驱动和真实 WASAPI 设备仍需要平台人工验证。
- `SvgPrefab` 必须覆盖强类型解析、格式化往返、Skia 非空绘制、视图构造/编译绑定和真实 ramen 语料；不得继续把它列为预期排除命令。
