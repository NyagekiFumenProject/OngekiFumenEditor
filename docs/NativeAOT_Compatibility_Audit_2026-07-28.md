# NativeAOT 兼容性审计报告

审计日期：2026-07-28  
审计环境：Windows 10.0.26100、.NET SDK 10.0.302、`win-x64`  
审计对象：`F:\OngekiFumenEditor` 当前工作区

## 1. 结论

**当前项目不兼容 NativeAOT，也无法以 NativeAOT 形式发布。**

这不是单纯消除若干 IL 警告即可解决的问题，存在三层阻断：

1. **框架级硬阻断**：主程序和全部可执行项目均启用 WPF。使用当前 SDK 对主程序执行真实 `PublishAot` 探测时，SDK 直接报 `NETSDK1168`，拒绝对 WPF 应用启用裁剪；NativeAOT 必然要求裁剪。
2. **架构级硬阻断**：主程序在运行时扫描并加载插件程序集，使用 MEF 发现部件；脚本系统还会在运行时用 Roslyn 编译代码、加载新程序集并反射调用入口。这些能力与 NativeAOT 的“无运行时动态程序集加载、无运行时代码生成”模型冲突。
3. **代码和依赖级问题**：AOT/裁剪分析器在自有源码及随仓库维护的直接依赖源码中发现 **154 个唯一诊断点，分布于 58 个源码文件**；另有 **20 个 WPF 生成代码诊断点**。仓库没有任何 `DynamicallyAccessedMembers`、`RequiresUnreferencedCode`、`RequiresDynamicCode` 或 `DynamicDependency` 注解，也没有启用 `IsAotCompatible`/`PublishAot`。

因此，当前合理选择是继续使用 JIT 发布模式。若业务目标必须包含 NativeAOT，应先在以下路线中作出架构决策：

- 保留 WPF/JIT 主进程，将可独立的解析、转换或批处理能力拆成无 WPF、无插件、无运行时脚本的 NativeAOT 子程序；或
- 更换为明确支持 NativeAOT 的 UI/应用模型，并将插件发现和脚本执行改为构建期注册、解释执行或进程外 JIT 工作进程。

在 WPF 和动态扩展模型未解决前，批量添加裁剪注解的收益有限，不能使完整产品可发布为 NativeAOT。

## 2. 审计范围与方法

### 2.1 范围

静态扫描覆盖 `bin`/`obj` 以外的 **2441 个源码/XAML 文件**：

| 类型 | 数量 |
|---|---:|
| C# | 2121 |
| F# | 1 |
| VB | 18 |
| XAML | 301 |

其中主程序目录 1141 个文件，`Dependences` 目录 1272 个文件，其余为命令行、基准和工具项目。编译器审计重点覆盖主解决方案中的 13 个项目；不在主解决方案内的示例、测试和旧平台项目通过全仓静态扫描补充检查。

### 2.2 编译器分析

对支持的目标框架使用以下等价参数逐项目构建，并关闭项目引用重建以隔离诊断归属：

```powershell
dotnet build <project.csproj> --no-incremental `
  -p:BuildProjectReferences=false `
  -p:IsAotCompatible=true `
  -p:EnableTrimAnalyzer=true `
  -p:EnableAotAnalyzer=true
```

`netstandard2.0`/`netstandard2.1` 项目不能直接运行 .NET 8+ AOT 分析器，因此另以命令行属性临时重定向到 `net10.0` 做分析器探测；未修改项目文件，审计结束后已重新还原正常目标的 NuGet 资产。此探测可以发现源码 API 问题，但不等同于正式增加 `net10.0` TFM 后的完整验证。

真实发布探测命令：

```powershell
dotnet publish OngekiFumenEditor/OngekiFumenEditor.csproj `
  --no-restore -c Release -r win-x64 `
  -p:PublishAot=true -p:SelfContained=true `
  -p:BuildProjectReferences=false `
  -p:SkipRecommendedScriptVerification=true
```

结果：

```text
error NETSDK1168: 启用剪裁时，不支持或不推荐使用 WPF。
```

SDK 给出的说明入口为 <https://aka.ms/dotnet-illink/wpf>。

### 2.3 静态扫描

静态扫描用于补充分析器盲区和未参与主解决方案构建的代码。模式统计是定位信号，不应直接相加为缺陷数：

| 模式 | 匹配数 | 文件数 |
|---|---:|---:|
| 反射式 `System.Text.Json` 调用 | 27 | 15 |
| 动态程序集加载 | 5 | 4 |
| Roslyn/Emit 等运行时编译或生成 | 13 | 5 |
| 反射激活、运行时泛型构造、非泛型 Marshal 等 | 67 | 39 |
| `GetTypes`/`GetProperties`/`Type.GetType` 等宽泛反射 | 22 | 17 |
| MEF Import/Export/容器相关 | 1073 | 383 |
| P/Invoke/原生加载相关 | 129 | 17 |
| AOT/裁剪注解 | 0 | 0 |

另外，XAML 中发现 957 个字符串路径形式的 `{Binding ...}` 匹配。它们本身不逐条判定为缺陷，但说明完整 UI 高度依赖 WPF 的运行时绑定和反射基础设施。

## 3. 项目级结果

“源码诊断”只统计唯一的 `文件 + 行 + IL 代码`，排除 `obj` 下生成文件，并避免 WPF 两阶段编译造成的重复。

| 项目 | 目标框架 | 源码诊断 | 生成代码诊断 | 结论 |
|---|---|---:|---:|---|
| `OngekiFumenEditor` | `net10.0-windows` + WPF | 83 | 15 | 不兼容；真实发布被 `NETSDK1168` 阻止 |
| `OngekiFumenEditor.CommandLine` | `net10.0-windows` + WPF | 6 | 0 | 不兼容；动态加载主程序集并使用 `dynamic` |
| `OngekiFumenEditor.Benchmark` | `net10.0-windows` + WPF | 11 | 0 | 不兼容；反射、JSON 和程序集路径问题 |
| `RecommendedScriptVerifier` | `net10.0-windows` + WPF | 0 | 0 | WPF 配置阻止 AOT；关闭 `UseWPF` 后可产出 AOT，但 `Microsoft.CodeAnalysis` 报 `IL2104` |
| `SvgConverter` | `net10.0-windows` + WPF | 0 | 0 | 自身分析器干净，但不能证明 WPF 宿主可 AOT |
| `Gemini` | `net10.0-windows` + WPF | 20 | 5 | 不兼容；运行时泛型、类型名恢复、反射路由 |
| `Gemini.Modules.Output` | `net10.0-windows` + WPF | 0（自身） | 0（自身） | 自身未新增诊断，继承 Gemini/Caliburn 风险 |
| `Caliburn.Micro.Platform` | `net8.0/9.0-windows` | 23 | 0 | 不兼容；视图定位、约定绑定、反射激活 |
| `Caliburn.Micro.Core` | `netstandard2.0` | 9 | 0 | 临时以 `net10.0` 分析；容器和事件聚合器不兼容 |
| `Caliburn.Micro.Platform.Core` | `netstandard2.0` | 2（自身） | 0 | 临时以 `net10.0` 分析；另继承 Core 的 9 处 |
| `MigratableSerializer` | `netstandard2.0` | 0 | 0 | 临时 `net10.0` 分析器探测干净 |
| `Earcut` | `netstandard2.0` | 0 | 0 | 临时 `net10.0` 分析器探测干净 |
| `Polyline2DCSharp` | `netstandard2.1` | 0 | 0 | 临时 `net10.0` 分析器探测干净 |

`RecommendedScriptVerifier` 的额外探测：通过命令行临时设定 `UseWPF=false` 后，`PublishAot` 成功完成，但依赖 `Microsoft.CodeAnalysis 4.11.0` 产生 `IL2104`（该程序集内部有裁剪警告）。因此“本项目源码 0 条分析器警告”不能解释为整个工具依赖图已获 AOT 兼容保证。

## 4. 诊断代码汇总

| 诊断 | 数量 | 含义/主要来源 |
|---|---:|---|
| `IL2026` | 45 | 调用标记为不保证裁剪安全的 API；JSON、`TypeDescriptor`、程序集扫描/加载 |
| `IL2055` | 3 | 无法静态分析 `MakeGenericType`；Gemini 命令路由 |
| `IL2057` | 2 | 运行时字符串传给 `Type.GetType`；布局状态恢复 |
| `IL2067` | 13 | `Type` 值未满足成员保留要求 |
| `IL2070` | 16 | 对未注解 `Type` 做成员反射 |
| `IL2072` | 7 | 反射结果或返回值丢失成员保留信息 |
| `IL2075` | 18 | 反射获得的类型继续流向成员访问 |
| `IL2087` | 3 | 泛型参数缺少成员保留要求 |
| `IL2090` | 3 | 泛型类型反射缺少成员保留要求 |
| `IL2091` | 1 | `Activator.CreateInstance<T>` 的泛型参数缺少无参构造要求 |
| `IL3000` | 3 | NativeAOT/单文件中 `Assembly.Location` 不可靠或为空 |
| `IL3050` | 40 | 需要运行时代码生成；JSON、运行时泛型、Marshal 等 |
| **合计** | **154** | 不含 20 个 WPF 生成代码诊断点 |

## 5. 结构性阻断

### 5.1 WPF 是当前发布硬阻断

以下可执行项目都设置了 `<UseWPF>true</UseWPF>`：

- `OngekiFumenEditor/OngekiFumenEditor.csproj:6`
- `OngekiFumenEditor.CommandLine/OngekiFumenEditor.CommandLine.csproj:5`
- `OngekiFumenEditor.Benchmark/OngekiFumenEditor.Benchmark.csproj:5`
- `Tools/RecommendedScriptVerifier/RecommendedScriptVerifier.csproj:6`

当前主项目的发布配置是 framework-dependent single-file（`SelfContained=false` + `PublishSingleFile=true`），这与 NativeAOT 是两种不同部署模型。将 `PublishAot=true` 加入现有配置不会“升级”为 AOT，而会先触发 WPF/裁剪不受支持错误。

主程序分析中另有 15 个、Gemini 中另有 5 个来自 WPF `.g.cs` 的诊断，主要涉及按字符串创建事件委托和反射成员流。Caliburn 还在以下位置运行时解析 XAML：

- `Dependences/gemini/Dependences/Caliburn.Micro/src/Caliburn.Micro.Platform/ConventionManager.cs:83`
- `Dependences/gemini/Dependences/Caliburn.Micro/src/Caliburn.Micro.Platform/ConventionManager.cs:108`
- `Dependences/gemini/Dependences/Caliburn.Micro/src/Caliburn.Micro.Platform/ActionMessage.cs:330`

**处置**：完整桌面应用在当前技术栈下保持 JIT。只有更换/升级到明确支持 NativeAOT 的 UI 栈后，后续源码修复才可能使完整应用达标。

### 5.2 运行时插件加载和 MEF

`OngekiFumenEditor/AppBootstrapper.cs:117-161` 枚举 `Plugins` 子目录，使用 `DirectoryCatalog` 发现插件，再把运行时程序集加入 `AssemblySource`。`AppBootstrapper.cs:199-205` 和 Gemini/Caliburn 又基于程序集集合建立 MEF 容器和视图/服务发现。

NativeAOT 不能在发布后接收任意新的托管程序集并为其生成原生代码。保留任意第三方 DLL 插件兼容性与“单个完整 NativeAOT 进程”目标互斥，裁剪描述文件也不能解决发布时未知插件的问题。

**可选处置**：

1. 保留当前 JIT 主程序和插件机制；这是成本最低且功能完整的方案。
2. 将插件迁移到进程外 JIT worker，通过稳定 IPC 协议与 AOT 主程序通信。
3. 若插件集合固定，改为构建期清单或源生成静态注册，并把所有插件纳入同一次 AOT 发布。

### 5.3 运行时脚本编译和程序集加载

`OngekiFumenEditor/Modules/EditorScriptExecutor/Kernel/DefaultImpl/DefaultEditorScriptExecutor.cs` 存在完整的运行时编译/执行链：

- `:36-51` 从当前 AppDomain 收集程序集元数据；
- `:125-150` 用 Roslyn 创建脚本编译并 Emit PE/PDB；
- `:156` 用 `Assembly.Load(byte[], byte[])` 加载新程序集；
- `:173` 用 `Assembly.LoadFrom` 加载脚本附加引用；
- `:218-226` 按名称寻找入口并创建委托执行。

`DefaultDocumentContext.cs:62-78` 还依赖 `AppDomain.GetAssemblies()` 和 `Assembly.Location` 生成脚本文档引用。

这是 NativeAOT 的根本不兼容路径。即使 Roslyn 本身能够在 AOT 进程中解析或生成 IL，NativeAOT 进程也不能对发布后新生成的 IL 做 JIT 编译并执行。

**处置**：把脚本执行放到进程外 JIT worker；或改用解释器/受限 DSL；或在发布前预编译并静态链接有限脚本。仅添加 `RequiresDynamicCode` 只能正确标记不兼容，不能恢复功能。

### 5.4 命令行启动器动态调用 WPF 应用

`OngekiFumenEditor.CommandLine/Program.cs`：

- `:19` 按程序集名执行 `Assembly.Load`；
- `:30` 按字符串寻找 `OngekiFumenEditor.App`；
- `:39-42` 用 `Activator.CreateInstance` 和 `dynamic` 调用 WPF 生命周期。

这产生 6 个分析器诊断，也是 NativeAOT 无法静态确定入口调用图的典型模式。

**处置**：命令行项目应直接引用一个不依赖 WPF 的强类型应用服务入口。若目标是提供 AOT CLI，应把解析/转换能力下沉到独立 `net10.0` core library，而不是启动 WPF `App`。

## 6. 可修复的代码级问题

### 6.1 `System.Text.Json` 反射序列化

仓库有 27 个反射式 JSON 调用，主程序中大多数同时产生 `IL2026` 和 `IL3050`。

可以直接改为源生成上下文的固定模型包括：

- IPC：`AppBootstrapper.cs:425`、`Utils/IPCHelper.cs:59`
- 最近文件：`DefaultEditorRecentFilesManager.cs:32,50`
- 键绑定：`DefaultKeyBindingManager.cs:51,64`
- 编辑器工程文件：`CommonEditorProjectFileSerializer.cs:96,101`、`Migration_V0_5_2_To_Latest.cs:23,25`
- 绘制设置：`FumenVisualEditorViewModel.Drawing.cs:167,204`
- 更新信息和设置页：`DefaultProgramUpdater.cs:180`、`ProgramSettingViewModel.cs:241`
- 基准基线：`OngekiFumenEditor.Benchmark/Baselines/BaselineStore.cs:55,82`

需要先收紧运行时契约、不能只做机械替换的动态模型包括：

- `OverlayJsonSettingsProvider.cs:99,127`：类型来自运行时 `SettingsProperty.PropertyType`；
- `RuntimeAutomationScriptHost.cs:379,384`：序列化任意脚本返回对象；
- `McpOperationLogHelper.cs:60`：使用 `payload.GetType()`；
- `CommonEditorProjectFileSerializer<T>`：开放泛型 API 需要显式 `JsonTypeInfo<T>` 或受控 resolver 注册表。

**建议**：建立按业务域拆分的 `JsonSerializerContext`，调用接受 `JsonTypeInfo<T>`/`JsonSerializerContext` 的重载。动态边界应改为有限 DTO/`JsonElement`/显式类型注册，不能回退到 `object + Type` 反射序列化。

### 6.2 反射激活和运行时泛型

高风险位置：

- `Utils/LambdaActivator.cs:42-125`：运行时寻找构造器并用表达式树生成激活委托；
- `Kernel/CommandExecutor/DefaultCommandExecutor.cs:209-248`：反射属性、`MakeGenericType`、动态 `Option<T>` 和表达式编译；
- `Modules/FumenObjectPropertyBrowser/MultiObjectsPropertyInfoWrapper.cs:35-38,69-70,118`：运行时泛型、属性访问和激活；
- `Modules/FumenEditorSelectingObjectViewer/.../SelectionFilterOptions.cs:238,269,295`：非泛型 `Enum.GetValues(Type)`；
- `Modules/FumenBulletPalleteListViewer/ValueConverters/EnumValuesGenerator.cs:15`：运行时枚举数组类型；
- `Modules/FumenVisualEditor/Behaviors/BatchMode/BatchModeSubmode.cs:93`：泛型参数未声明可构造要求。

**建议**：优先用显式工厂/静态注册表替代运行时激活和 `MakeGenericType`。确实由已知类型驱动的反射，再在最内层 API 上添加最小的 `DynamicallyAccessedMembers` 并向调用者传播；`BatchModeInputSubmode<T>` 可评估增加 `where T : new()`。表达式树在 NativeAOT 下通常只能解释执行，不能把它当作恢复运行时代码生成能力的方案。

### 6.3 `TypeDescriptor` 和属性浏览器

以下路径使用 `TypeDescriptor.GetConverter` 或运行时属性发现：

- `Parser/ParserUtils.cs:67`
- `Parser/Ogkr/CommandArgs.cs:100`
- `Utils/TypeConvertHelper.cs:22,37`
- `Modules/OgkiFumenListBrowser/Models/OngekiFumenSet.cs:83`
- `UI/Controls/ObjectInspector/UIGenerator/PropertyInfoWrapper.cs:49`
- `Modules/FumenObjectPropertyBrowser/ViewModels/FumenObjectPropertyBrowserViewModel.cs:46`
- `UI/Controls/ObjectInspector/ViewModels/ObjectInspectorViewModel.cs:39`

`TypeDescriptor` 的通用转换器发现要求保留很宽的类型成员，盲目添加 `DynamicallyAccessedMemberTypes.All` 会显著扩大输出并继续引入警告。

**建议**：解析器对已知标量建立显式转换表（`TryParse`/枚举解析）；属性编辑器由源生成或显式注册元数据；只对无法替换、类型集合封闭的入口做精确注解。

### 6.4 程序集扫描和私有成员访问

- `Utils/Settings/ApplicationSettingsBaseInjector.cs:23-24` 扫描程序集所有类型并寻找静态 `Default`；`:36` 反射私有 `Initializer`。
- `Kernel/EditorLayout/EditorLayoutManager.cs:34` 访问 Gemini 的私有 `_shellView` 字段。
- Gemini `ToolboxService.cs:18` 扫描 `AssemblySource` 中全部类型。
- Gemini `LayoutItemStatePersister.cs:140,166` 从持久化字符串恢复类型。

这些代码对裁剪极其脆弱，私有实现字段还存在普通版本升级风险。

**建议**：设置类、工具箱项和布局类型使用显式注册表/稳定类型 ID；通过公共接口传递 `IShellView`，不要反射第三方私有字段。若某条路径仍允许真正动态类型，应明确标记为不支持裁剪，而不是抑制警告。

### 6.5 原生互操作

仓库的 129 个 P/Invoke/原生加载匹配不等于 129 个 AOT 缺陷；固定签名的 `DllImport` 通常可以用于 NativeAOT。分析器明确指出的动态封送问题是：

- `Kernel/Graphics/Skia/GlContexts/Glx/Glx.cs:62`：非泛型 `Marshal.GetDelegateForFunctionPointer`；
- `Glx.cs:91`：非泛型 `Marshal.PtrToStructure`；
- `Kernel/Graphics/Skia/GlContexts/Wgl/Wgl.cs:229`：非泛型 `GetDelegateForFunctionPointer`。

**建议**：改用 `Marshal.GetDelegateForFunctionPointer<TDelegate>` 和 `Marshal.PtrToStructure<T>`，并给委托/结构体保留精确的 `UnmanagedFunctionPointer`、`StructLayout`、字符集和调用约定。当前项目只声明 `win-x64` RID，5 个自带原生 DLL 也需要按目标 RID 做真实 AOT 冒烟测试。

### 6.6 单文件/程序集路径

`IL3000` 位置：

- `OngekiFumenEditor/Modules/EditorScriptExecutor/Kernel/DefaultImpl/DefaultDocumentContext.cs:70`
- `OngekiFumenEditor.Benchmark/Program.cs:31`
- `Dependences/gemini/src/Gemini/AppBootstrapper.cs:181`

需要应用目录时使用 `AppContext.BaseDirectory`。需要程序集文件作为 Roslyn metadata 时，NativeAOT 中不存在可等价替换的托管程序集文件；该需求应随脚本 worker 一并移出 AOT 进程。

## 7. Gemini/Caliburn 风险

Gemini/Caliburn 是主 UI 组合、命令、视图定位和 IoC 的基础，诊断不是孤立调用：

- Gemini `CommandRouter.cs:151,155,156` 使用运行时 `MakeGenericType`；
- `CommandHandlerWrapper.cs:16-31` 按运行时接口类型查找方法；
- `LayoutItemStatePersister.cs:140,166` 按字符串恢复类型；
- Caliburn `ViewLocator.cs:339` 反射实例化视图；
- Caliburn `ConventionManager`、`ViewModelBinder`、`ActionMessage`、`MessageBinder` 依赖按名称/约定发现成员和转换器；
- Caliburn `SimpleContainer.cs:128-294` 使用运行时泛型和激活器构造服务。

逐个添加注解可能让部分警告消失，但仍需要确保所有视图、ViewModel、命令处理器、构造器和绑定成员被保留。结合 WPF/运行时 XAML，这相当于重做框架级静态注册和绑定生成，不是局部修复。

## 8. 非主解决方案代码

全仓扫描还发现以下不在当前主解决方案产品路径中的 AOT 问题：

- `Dependences/gemini/src/Gemini.Modules.CodeCompiler/CodeCompiler.cs:38-55`：运行时 Roslyn 编译并 `Assembly.Load`；
- `Dependences/gemini/src/Gemini.Demo/Modules/Home/ViewModels/HelixViewModel.cs:118-137`：依赖程序集路径、运行时编译结果和反射激活；
- `Dependences/SvgToXaml/SvgToXaml/Program.cs:64`：从字节动态加载程序集；
- `Dependences/SvgToXaml/SvgToXaml/ViewModels/SvgImagesViewModel.cs:102`：使用入口程序集 `Location`；
- `Dependences/MigratableSerializer/MigratableSerializer.TestConsole`：9 个反射式 JSON 调用，需要源生成上下文；
- `Dependences/earcut.net/tests/JSTests.cs:1`：测试依赖 `Newtonsoft.Json.Linq`，并在 `:19` 使用 `Assembly.CodeBase`；
- Caliburn 的旧平台实现、示例和测试含大量 `Activator.CreateInstance`、程序集发现和 `rd.xml`。这些 `rd.xml` 是 UWP/.NET Native 时代配置，不能当作现代 NativeAOT 兼容证明。

这些代码不会增加当前主应用的唯一 IL 诊断统计，但若单独发布相应示例/工具，也需要各自处理或明确排除 AOT 目标。

## 9. 依赖风险与审计盲区

### 9.1 缺少 AOT 兼容元数据

启用 .NET 10 的 `VerifyReferenceAotCompatibility=true` 后，主程序依赖闭包中有 **247 个不同程序集名**未声明 AOT 兼容元数据。该数字包括 WPF/ASP.NET 框架程序集和很多面向较早 TFM 构建的包，因此只是“需要验证”的库存，**不是 247 个已确认缺陷**。`.NET 10` 才引入可供该检查读取的程序集级 AOT 兼容元数据，旧包即使实际可用也可能被报告。

高风险依赖族包括：

- WPF、AvalonDock、MahApps、WPF Behaviors、SkiaSharp WPF、OpenTK WPF；
- `Microsoft.CodeAnalysis*`、`Microsoft.Build*`；
- `System.ComponentModel.Composition` 和 Gemini/Caliburn；
- 10 个通过 `<Reference HintPath>` 引入、没有同仓源码的托管 DLL；
- 5 个随应用分发的原生 DLL。

其中二进制引用无法通过源码扫描证明裁剪/AOT 兼容。必须获得供应方声明/源码，或在框架阻断解除后通过完整 `dotnet publish -p:PublishAot=true` 和功能测试验证。

### 9.2 当前还原兼容性警告

正常还原已存在：

- `AssocSupport 1.1.0` 以 .NET Framework 资产供 `net10.0-windows` 使用；
- `SkiaSharp.Views.WPF 3.119.2` 同样产生 `NU1701`；
- `AngleSharp 0.16.1` 与 `Microsoft.Build.Tasks.Core 17.7.2` 另有已知漏洞警告。

`NU1701` 不是 NativeAOT 分析结果，但它表示包没有面向当前 TFM 的明确资产，会进一步降低 AOT 迁移的可预测性。漏洞警告与 AOT 无直接关系，仍应独立处理。

## 10. 建议路线

### 阶段 0：确定目标边界

1. 若目标是完整 WPF 编辑器：维持 JIT/self-contained/single-file，停止把 NativeAOT 作为当前发布验收项。
2. 若目标是提高 CLI/批处理启动速度：新建无 WPF 的 core/CLI 边界，只迁移静态可达的解析、校验、转换功能。
3. 若目标是最终完整 AOT：先确定 UI 替换方案，以及插件/脚本的进程外协议；不要从逐条压警告开始。  

### 阶段 1：建立可持续分析

对准备进入 AOT 边界的 `net8.0+` library 设置：

```xml
<PropertyGroup>
  <IsAotCompatible>true</IsAotCompatible>
</PropertyGroup>
```

旧 TFM 库应正式多目标到至少 `net8.0`，并仅对兼容 TFM 条件启用。CI 要求自有源码 `0` 条 IL 警告；不要用 `#pragma warning disable` 或无依据的抑制隐藏问题。

### 阶段 2：优先消除高占比模式

1. 为固定 JSON 模型建立源生成上下文。
2. 用显式工厂/注册表替换 `LambdaActivator`、运行时 `MakeGenericType` 和宽泛程序集扫描。
3. 用稳定 ID 和静态映射替换类型名持久化。
4. 用泛型 Marshal 重载修复 3 个动态封送点。
5. 用 `AppContext.BaseDirectory` 替换真正表示应用目录的 `Assembly.Location`。

### 阶段 3：实际发布验证

只有在宿主框架允许后，才能以完整依赖图执行：

```powershell
dotnet publish <aot-entry.csproj> -c Release -r win-x64 -p:PublishAot=true
```

验收标准：

- 发布成功；
- 自有源码和可控依赖没有 IL 警告；
- 所有 JSON、视图/命令注册、设置加载、原生库和 IPC 路径通过功能测试；
- 不在 AOT 进程内加载发布时未知的托管插件或执行新生成 IL；
- 对每个无法审计的二进制依赖有供应方声明或独立替换计划。

## 11. 完整源码诊断位置

以下为本次可复现分析中的 154 个源码诊断点。同行多个代码代表同一调用同时违反裁剪和 AOT 要求。

### 11.1 主程序（83）

```text
OngekiFumenEditor/AppBootstrapper.cs:425                         IL2026, IL3050
OngekiFumenEditor/Kernel/CommandExecutor/DefaultCommandExecutor.cs:211 IL2090
OngekiFumenEditor/Kernel/CommandExecutor/DefaultCommandExecutor.cs:215 IL3050
OngekiFumenEditor/Kernel/CommandExecutor/DefaultCommandExecutor.cs:221 IL3050
OngekiFumenEditor/Kernel/CommandExecutor/DefaultCommandExecutor.cs:239 IL2090
OngekiFumenEditor/Kernel/EditorLayout/EditorLayoutManager.cs:34  IL2075
OngekiFumenEditor/Kernel/Graphics/Skia/GlContexts/Glx/Glx.cs:62 IL3050
OngekiFumenEditor/Kernel/Graphics/Skia/GlContexts/Glx/Glx.cs:91 IL3050
OngekiFumenEditor/Kernel/Graphics/Skia/GlContexts/Wgl/Wgl.cs:229 IL3050
OngekiFumenEditor/Kernel/KeyBinding/DefaultKeyBindingManager.cs:51 IL2026, IL3050
OngekiFumenEditor/Kernel/KeyBinding/DefaultKeyBindingManager.cs:64 IL2026, IL3050
OngekiFumenEditor/Kernel/ProgramUpdater/DefaultProgramUpdater.cs:180 IL2026, IL3050
OngekiFumenEditor/Kernel/RecentFiles/DefaultImp/DefaultEditorRecentFilesManager.cs:32 IL2026, IL3050
OngekiFumenEditor/Kernel/RecentFiles/DefaultImp/DefaultEditorRecentFilesManager.cs:50 IL2026, IL3050
OngekiFumenEditor/Kernel/RuntimeAutomation/McpOperationLogHelper.cs:60 IL2026, IL3050
OngekiFumenEditor/Kernel/RuntimeAutomation/RuntimeAutomationScriptHost.cs:379 IL2026, IL3050
OngekiFumenEditor/Kernel/RuntimeAutomation/RuntimeAutomationScriptHost.cs:384 IL2026, IL3050
OngekiFumenEditor/Kernel/SettingPages/Program/ViewModels/ProgramSettingViewModel.cs:241 IL2026, IL3050
OngekiFumenEditor/Modules/EditorScriptExecutor/Kernel/DefaultImpl/DefaultDocumentContext.cs:70 IL3000
OngekiFumenEditor/Modules/EditorScriptExecutor/Kernel/DefaultImpl/DefaultEditorScriptExecutor.cs:156 IL2026
OngekiFumenEditor/Modules/EditorScriptExecutor/Kernel/DefaultImpl/DefaultEditorScriptExecutor.cs:158 IL2026
OngekiFumenEditor/Modules/EditorScriptExecutor/Kernel/DefaultImpl/DefaultEditorScriptExecutor.cs:173 IL2026
OngekiFumenEditor/Modules/EditorScriptExecutor/Kernel/DefaultImpl/DefaultEditorScriptExecutor.cs:218 IL2026
OngekiFumenEditor/Modules/EditorScriptExecutor/Kernel/DefaultImpl/DefaultEditorScriptExecutor.cs:219 IL2075
OngekiFumenEditor/Modules/FumenBulletPalleteListViewer/ValueConverters/EnumValuesGenerator.cs:15 IL3050
OngekiFumenEditor/Modules/FumenEditorSelectingObjectViewer/Base/SelectionFilter/SelectionFilterOptions.cs:238 IL3050
OngekiFumenEditor/Modules/FumenEditorSelectingObjectViewer/Base/SelectionFilter/SelectionFilterOptions.cs:269 IL3050
OngekiFumenEditor/Modules/FumenEditorSelectingObjectViewer/Base/SelectionFilter/SelectionFilterOptions.cs:295 IL3050
OngekiFumenEditor/Modules/FumenObjectPropertyBrowser/MultiObjectsPropertyInfoWrapper.cs:35 IL3050
OngekiFumenEditor/Modules/FumenObjectPropertyBrowser/MultiObjectsPropertyInfoWrapper.cs:70 IL2075
OngekiFumenEditor/Modules/FumenObjectPropertyBrowser/MultiObjectsPropertyInfoWrapper.cs:118 IL2072
OngekiFumenEditor/Modules/FumenObjectPropertyBrowser/ViewModels/FumenObjectPropertyBrowserViewModel.cs:46 IL2075
OngekiFumenEditor/Modules/FumenVisualEditor/Behaviors/BatchMode/BatchModeSubmode.cs:93 IL2091
OngekiFumenEditor/Modules/FumenVisualEditor/Kernel/EditorProjectFile/Migrations/Migration_V0_5_2_To_Latest.cs:23 IL2026, IL3050
OngekiFumenEditor/Modules/FumenVisualEditor/Kernel/EditorProjectFile/Migrations/Migration_V0_5_2_To_Latest.cs:25 IL2026, IL3050
OngekiFumenEditor/Modules/FumenVisualEditor/Kernel/EditorProjectFile/Serializers/CommonEditorProjectFileSerializer.cs:96 IL2026, IL3050
OngekiFumenEditor/Modules/FumenVisualEditor/Kernel/EditorProjectFile/Serializers/CommonEditorProjectFileSerializer.cs:101 IL2026, IL3050
OngekiFumenEditor/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.Drawing.cs:167 IL2026, IL3050
OngekiFumenEditor/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.Drawing.cs:204 IL2026, IL3050
OngekiFumenEditor/Modules/OgkiFumenListBrowser/Models/OngekiFumenSet.cs:83 IL2026, IL2087
OngekiFumenEditor/Parser/Ogkr/CommandArgs.cs:100              IL2026, IL2067
OngekiFumenEditor/Parser/ParserUtils.cs:67                    IL2026, IL2067
OngekiFumenEditor/UI/Controls/ObjectInspector/UIGenerator/PropertyInfoWrapper.cs:49 IL2026, IL2072
OngekiFumenEditor/UI/Controls/ObjectInspector/ViewModels/ObjectInspectorViewModel.cs:39 IL2075
OngekiFumenEditor/Utils/IPCHelper.cs:59                       IL2026, IL3050
OngekiFumenEditor/Utils/LambdaActivator.cs:53                 IL2067
OngekiFumenEditor/Utils/LambdaActivator.cs:64                 IL2087
OngekiFumenEditor/Utils/LambdaActivator.cs:72                 IL2067
OngekiFumenEditor/Utils/LambdaActivator.cs:74                 IL2070
OngekiFumenEditor/Utils/LambdaActivator.cs:85                 IL2090
OngekiFumenEditor/Utils/LambdaActivator.cs:125                IL2070
OngekiFumenEditor/Utils/Settings/ApplicationSettingsBaseInjector.cs:23 IL2026
OngekiFumenEditor/Utils/Settings/ApplicationSettingsBaseInjector.cs:24 IL2070
OngekiFumenEditor/Utils/Settings/OverlayJsonSettingsProvider.cs:65 IL2026, IL3050
OngekiFumenEditor/Utils/Settings/OverlayJsonSettingsProvider.cs:99 IL2026, IL3050
OngekiFumenEditor/Utils/Settings/OverlayJsonSettingsProvider.cs:127 IL2026, IL3050
OngekiFumenEditor/Utils/TypeConvertHelper.cs:22               IL2067
OngekiFumenEditor/Utils/TypeConvertHelper.cs:37               IL2026, IL2067
```

### 11.2 命令行（6）

```text
OngekiFumenEditor.CommandLine/Program.cs:30 IL2026
OngekiFumenEditor.CommandLine/Program.cs:39 IL2072
OngekiFumenEditor.CommandLine/Program.cs:40 IL2026, IL3050
OngekiFumenEditor.CommandLine/Program.cs:42 IL2026, IL3050
```

### 11.3 基准项目（11）

```text
OngekiFumenEditor.Benchmark/Baselines/BaselineStore.cs:55 IL2026, IL3050
OngekiFumenEditor.Benchmark/Baselines/BaselineStore.cs:82 IL2026, IL3050
OngekiFumenEditor.Benchmark/Benchmarks/ParserUtilsBenchmarks.cs:62 IL2026, IL2087
OngekiFumenEditor.Benchmark/Benchmarks/ParserUtilsBenchmarks.cs:107 IL2026, IL2067
OngekiFumenEditor.Benchmark/Program.cs:31 IL3000
OngekiFumenEditor.Benchmark/Program.cs:90 IL2026
OngekiFumenEditor.Benchmark/Program.cs:92 IL2070
```

### 11.4 Gemini（20）

```text
Dependences/gemini/src/Gemini/AppBootstrapper.cs:181 IL3000
Dependences/gemini/src/Gemini/Framework/Commands/CommandHandlerWrapper.cs:16 IL2070
Dependences/gemini/src/Gemini/Framework/Commands/CommandHandlerWrapper.cs:17 IL2070
Dependences/gemini/src/Gemini/Framework/Commands/CommandHandlerWrapper.cs:23 IL2070
Dependences/gemini/src/Gemini/Framework/Commands/CommandHandlerWrapper.cs:24 IL2070
Dependences/gemini/src/Gemini/Framework/Commands/CommandHandlerWrapper.cs:30 IL2070
Dependences/gemini/src/Gemini/Framework/Commands/CommandHandlerWrapper.cs:31 IL2070
Dependences/gemini/src/Gemini/Framework/Commands/CommandRouter.cs:151 IL2055, IL3050
Dependences/gemini/src/Gemini/Framework/Commands/CommandRouter.cs:155 IL2055, IL3050
Dependences/gemini/src/Gemini/Framework/Commands/CommandRouter.cs:156 IL2055, IL3050
Dependences/gemini/src/Gemini/Framework/Commands/CommandRouter.cs:201 IL2070
Dependences/gemini/src/Gemini/Framework/Controls/HwndWrapper.cs:446 IL3050
Dependences/gemini/src/Gemini/Framework/Services/InputManager.cs:43 IL2075
Dependences/gemini/src/Gemini/Modules/Shell/Services/LayoutItemStatePersister.cs:140 IL2057
Dependences/gemini/src/Gemini/Modules/Shell/Services/LayoutItemStatePersister.cs:166 IL2057
Dependences/gemini/src/Gemini/Modules/Shell/Views/LayoutUtility.cs:56 IL2075
Dependences/gemini/src/Gemini/Modules/Toolbox/Services/ToolboxService.cs:18 IL2026
```

### 11.5 Caliburn.Micro.Platform（23）

```text
Dependences/gemini/Dependences/Caliburn.Micro/src/Caliburn.Micro.Platform/Action.cs:172 IL2075
Dependences/gemini/Dependences/Caliburn.Micro/src/Caliburn.Micro.Platform/ActionMessage.cs:559 IL2075
Dependences/gemini/Dependences/Caliburn.Micro/src/Caliburn.Micro.Platform/ActionMessage.cs:783 IL2070
Dependences/gemini/Dependences/Caliburn.Micro/src/Caliburn.Micro.Platform/ConventionManager.cs:666 IL2070
Dependences/gemini/Dependences/Caliburn.Micro/src/Caliburn.Micro.Platform/ConventionManager.cs:677 IL2070
Dependences/gemini/Dependences/Caliburn.Micro/src/Caliburn.Micro.Platform/MessageBinder.cs:131 IL2026, IL2067
Dependences/gemini/Dependences/Caliburn.Micro/src/Caliburn.Micro.Platform/MessageBinder.cs:138 IL2026, IL2072
Dependences/gemini/Dependences/Caliburn.Micro/src/Caliburn.Micro.Platform/MessageBinder.cs:225 IL2067
Dependences/gemini/Dependences/Caliburn.Micro/src/Caliburn.Micro.Platform/Platforms/net46-netcore/Bootstrapper.cs:53 IL2026
Dependences/gemini/Dependences/Caliburn.Micro/src/Caliburn.Micro.Platform/Platforms/net46-netcore/Bootstrapper.cs:153 IL2067
Dependences/gemini/Dependences/Caliburn.Micro/src/Caliburn.Micro.Platform/Platforms/net46-netcore/Bootstrapper.cs:163 IL2067
Dependences/gemini/Dependences/Caliburn.Micro/src/Caliburn.Micro.Platform/Platforms/net46-netcore/WindowManager.cs:307 IL2075
Dependences/gemini/Dependences/Caliburn.Micro/src/Caliburn.Micro.Platform/View.cs:355 IL2075
Dependences/gemini/Dependences/Caliburn.Micro/src/Caliburn.Micro.Platform/View.cs:534 IL2075
Dependences/gemini/Dependences/Caliburn.Micro/src/Caliburn.Micro.Platform/ViewLocator.cs:339 IL2067
Dependences/gemini/Dependences/Caliburn.Micro/src/Caliburn.Micro.Platform/ViewLocator.cs:536 IL2075
Dependences/gemini/Dependences/Caliburn.Micro/src/Caliburn.Micro.Platform/ViewModelBinder.cs:147 IL2070
Dependences/gemini/Dependences/Caliburn.Micro/src/Caliburn.Micro.Platform/ViewModelBinder.cs:227 IL2075
Dependences/gemini/Dependences/Caliburn.Micro/src/Caliburn.Micro.Platform/XamlPlatformProvider.cs:234 IL2075
Dependences/gemini/Dependences/Caliburn.Micro/src/Caliburn.Micro.Platform/XamlPlatformProvider.cs:252 IL2075
Dependences/gemini/Dependences/Caliburn.Micro/src/Caliburn.Micro.Platform/XamlPlatformProvider.cs:273 IL2075
```

### 11.6 Caliburn.Micro.Core / Platform.Core（11）

```text
Dependences/gemini/Dependences/Caliburn.Micro/src/Caliburn.Micro.Core/ConductorWithCollectionAllActive.cs:120 IL2072
Dependences/gemini/Dependences/Caliburn.Micro/src/Caliburn.Micro.Core/ContainerExtensions.cs:111 IL2026
Dependences/gemini/Dependences/Caliburn.Micro/src/Caliburn.Micro.Core/EventAggregator.cs:126 IL2075
Dependences/gemini/Dependences/Caliburn.Micro/src/Caliburn.Micro.Core/EventAggregator.cs:132 IL2072
Dependences/gemini/Dependences/Caliburn.Micro/src/Caliburn.Micro.Core/SimpleContainer.cs:128 IL3050
Dependences/gemini/Dependences/Caliburn.Micro/src/Caliburn.Micro.Core/SimpleContainer.cs:138 IL3050
Dependences/gemini/Dependences/Caliburn.Micro/src/Caliburn.Micro.Core/SimpleContainer.cs:197 IL2072
Dependences/gemini/Dependences/Caliburn.Micro/src/Caliburn.Micro.Core/SimpleContainer.cs:269 IL2067
Dependences/gemini/Dependences/Caliburn.Micro/src/Caliburn.Micro.Core/SimpleContainer.cs:294 IL2070
Dependences/gemini/Dependences/Caliburn.Micro/src/Caliburn.Micro.Platform.Core/AssemblySource.cs:52 IL2026
Dependences/gemini/Dependences/Caliburn.Micro/src/Caliburn.Micro.Platform.Core/AssemblySource.cs:70 IL2026
```

## 12. 复现与参考资料

- NativeAOT 限制和分析器：<https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/>
- 裁剪警告处理：<https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/fixing-warnings>
- WPF 裁剪说明入口（由 `NETSDK1168` 提供）：<https://aka.ms/dotnet-illink/wpf>

审计后的正常解决方案构建已重新执行：`dotnet build OngekiFumenEditor.sln --no-restore -m:1`，结果为 **0 个错误、127 个既有普通警告**。这些普通 C#/NuGet 警告未计入本报告的 154 个 NativeAOT/裁剪诊断。
