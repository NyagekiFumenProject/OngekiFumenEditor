# NativeAOT 不兼容代码清单

> 本文档由静态扫描生成，针对的是 `F:/OngekiFumenEditor/OngekiFumenEditor/` 主项目（不含 Dependences/Benchmark/CommandLine/bin/obj 等子项目和产物）。
> 扫描方式：基于 ripgrep 的模式匹配 + 关键文件逐行审阅。所有行号均以最新源码为准。

---

## 摘要

- **主项目 AOT 迁移结论：大量阻塞性问题，目前不可能直接迁移到 NativeAOT。**
  即使忽略掉 WPF/Caliburn.Micro/MEF 这三个无法绕开的框架层级限制，单看项目自身的代码也存在 35+ 处明确的 AOT/Trim 不兼容点；其中至少 12 处属于 P0 级（依赖 `Expression.Compile`、反射元数据、动态加载程序集、`JsonSerializer` 默认反射模式、Roslyn 动态编译等）。
- **已知前置限制（不在本项目代码之内，但已经决定 AOT 不可行）：**
  - **WPF 框架本身：** `net10.0-windows + UseWPF=true`。WPF 在 .NET 9/.NET 10 阶段仍然依赖大量 BAML/XAML 反射，未正式支持 NativeAOT。微软自己在 *Limitations of Native AOT deployment* 中明确列出。
  - **Caliburn.Micro：** 通过 `ViewLocator.LocateForModel`、`ActionMessage`、`Convention` 等大量反射定位 View/ViewModel，整套 MVVM 绑定无源生成方案。
  - **MEF（`System.ComponentModel.Composition`）：** `DirectoryCatalog`、`AssemblySource.AddRange(...)`、`[Export]/[Import]/[ImportingConstructor]` 全靠运行时反射收集类型，AOT 友好度极差。本项目主项目内 250+ 个 `.cs` 文件包含 `[Export]/[Import]` 标注，这是整个应用的 DI 主干（IoC.Get/GetAll）。
  - **Gemini.Framework / MahApps.Metro / AvalonEdit / Costura.Fody：** 同样大量基于反射、XAML、单文件嵌入和 `AssemblyResolve` 动态加载，不在主项目代码内但同样阻塞 AOT。
- **扫描覆盖：** 扫描 `OngekiFumenEditor/` 主项目目录下 1100+ 个 `.cs` 源文件（不含 `obj/`、`bin/`、其它子项目）。
- **发现问题数：** 38 处具体 AOT 不兼容点（P0 = 14、P1 = 15、P2 = 9）。

---

## P0 阻塞性问题 (无法运行 / 严重 IL3050/IL2026)

### 1. `LambdaActivator` —— 通用反射 + Expression.Compile 构造对象工厂

- **位置：** `OngekiFumenEditor/Utils/LambdaActivator.cs:94-117`、`OngekiFumenEditor/Utils/LambdaActivator.cs:42-67`、`OngekiFumenEditor/Utils/LambdaActivator.cs:129-145`
- **类型：** 反射 + 动态代码生成（Linq Expressions）
- **AOT 警告代码：** IL3050（Expression.Compile）、IL2070/IL2075（`type.GetConstructors()`、`type.GetConstructor(types)`）、IL2026
- **问题：** `GetActivatorDelegate` 使用 `Expression.Lambda(...).Compile()` 在运行时为传入的 `Type` 合成构造函数委托；`GetMatchingConstructor` 用 `type.GetConstructor(types)` 按字符串/Type 数组定位构造器。该类还导出了 `CacheLambdaActivator.CreateInstance(type)`，被项目里大量代码用来动态创建 Ongeki 对象、View、Option 实例。
- **运行时风险：** NativeAOT 下 `Expression.Compile()` 直接抛 `PlatformNotSupportedException`/`NotSupportedException`；Trimmer 会在没有 `[DynamicallyAccessedMembers]` 的情况下移除目标类型的构造器，导致 `MissingMethodException`/`NullReferenceException`。
- **建议替换方案：** 改成显式工厂注册（例如 `Dictionary<Type, Func<object>>`），或者为所有可能被创建的类型生成 `[DynamicDependency]`；最理想的是改用 source generator 在编译期产出 `Activator` 表。

### 2. `CacheLambdaActivator.CreateInstance` 的所有调用点

- **位置：**
  - `OngekiFumenEditor/Base/OngekiObjectBase.cs:46` —— `OngekiObjectBase.CopyNew()`，被几乎所有谱面对象复制路径使用
  - `OngekiFumenEditor/Modules/FumenVisualEditor/Base/DropActions/DefaultToolBoxDropAction.cs:20`
  - `OngekiFumenEditor/Modules/EditorSvgObjectControlProvider/ViewModels/ObjectProperty/Operation/SvgPrefabOperationViewModel.cs:96`
  - `OngekiFumenEditor/Modules/FumenObjectPropertyBrowser/ViewModels/DropActions/ConnectableObjectSplitDropAction.cs:27-28`
  - `OngekiFumenEditor/Utils/ViewHelper.cs:22`
- **类型：** 通过 `LambdaActivator` 间接的反射构造
- **AOT 警告代码：** IL2072/IL2075/IL3050
- **问题：** 全部依赖问题 #1 的反射工厂。其中 `OngekiObjectBase.CopyNew` 处于复制/撤销/拖入工具箱等核心编辑路径上。
- **建议替换方案：** 在 `OngekiObjectBase` 子类上定义 `abstract OngekiObjectBase CreateNew();` 或者用 source generator 为每个 `OngekiObjectBase` 子类生成 `CreateNew()` 实现，从根本上避免反射构造。

### 3. `DefaultCommandExecutor.GenerateOptionsByAttributes<T>` —— 通过反射 + Expression 合成命令行 Option

- **位置：** `OngekiFumenEditor/Kernel/CommandExecutor/DefaultCommandExecutor.cs:209-233`
- **类型：** 反射 + `Type.MakeGenericType` + `Expression.Lambda.Compile` + `PropertyInfo.SetValue`
- **AOT 警告代码：** IL3050（MakeGenericType / Expression.Compile）、IL2055（MakeGenericType）、IL2075（GetProperty）、IL2026
- **问题：**
  - 第 211 行：`typeof(T).GetProperties()` 遍历 + `prop.GetCustomAttribute<...>()` 读取属性。
  - 第 215 行：`typeof(Func<,>).MakeGenericType(typeof(ArgumentResult), attrbuteBase.Type)`。
  - 第 218 行：`Expression.Lambda(funcType, valParam, arg)` 然后 `.Compile()`。
  - 第 221 行：`typeof(Option<>).MakeGenericType(attrbuteBase.Type)`。
  - 第 228 行：`optionType.GetProperty(nameof(Option<>.DefaultValueFactory)).SetValue(option, func)`。
  - 第 248 行：`prop.SetValue(obj, val)` 通过反射写值。
- **运行时风险：** NativeAOT 下 `Type.MakeGenericType` 触发的代码可能未编译；`Expression.Compile()` 直接报错。该路径关系到所有 CLI 命令行 `svg/convert/jacket/acb` 子命令解析，CLI 模式直接不可用。
- **建议替换方案：** 使用 `System.CommandLine` 原生的 fluent API 手动注册每个 Option，移除属性扫描与泛型动态构造。

### 4. `DefaultEditorScriptExecutor` —— Roslyn 动态编译 + 内存 `Assembly.Load`

- **位置：** `OngekiFumenEditor/Modules/EditorScriptExecutor/Kernel/DefaultImpl/DefaultEditorScriptExecutor.cs:36-51`、`:94-156`、`:213-238`
- **类型：** 动态代码（Roslyn）、`Assembly.Load(byte[])`、`MethodInfo.CreateDelegate`、`AppDomain.CurrentDomain.AssemblyResolve`、`AssemblyMetadata`/`TryGetRawMetadata`
- **AOT 警告代码：** IL3050/IL2026
- **问题：** 在运行时把所有已加载程序集元数据读出来组合 `MetadataReference`，然后 Roslyn 编译用户脚本到 PE，再 `Assembly.Load(peStream.ToArray(), pdbStream.ToArray())` 加载到进程，最后通过 `assembly.GetType(...).GetMethod(...).CreateDelegate(...)` 调用入口。这是**典型的 AOT 完全不兼容**场景。
- **运行时风险：** AOT 二进制内根本不会带 Roslyn 编译器和 JIT，整个脚本系统都会挂。
- **建议替换方案：** 在 AOT 发行版里禁用脚本子系统；若一定要保留，需要切到 Lua/JavaScript（Jint/QuickJsNet）等解释器并预编译；或者用 source-generated DSL 替代。

### 5. `DefaultDocumentContext.GenerateProjectFile` —— 枚举所有已加载程序集

- **位置：** `OngekiFumenEditor/Modules/EditorScriptExecutor/Kernel/DefaultImpl/DefaultDocumentContext.cs:42-81`
- **类型：** `AppDomain.CurrentDomain.GetAssemblies()` + `assembly.Location`
- **AOT 警告代码：** IL3000（`Assembly.Location` 在 single-file/AOT 下为空字符串）
- **问题：** 用于在脚本编辑器里生成 `.csproj` 引用列表，AOT 下 `Location` 不可用、且整个 `AppDomain` 概念发生退化。
- **建议替换方案：** AOT 模式下隐藏 "Generate Project" 入口。

### 6. `AppBootstrapper.BindServices` —— MEF `DirectoryCatalog` 动态加载插件目录

- **位置：** `OngekiFumenEditor/AppBootstrapper.cs:122-162`
- **类型：** MEF `DirectoryCatalog` + `AppDomain.CurrentDomain.GetAssemblies()` + `AssemblySource.AddRange`
- **AOT 警告代码：** IL2026（MEF 内部反射）、IL3000（`Assembly.GetExecutingAssembly().Location`）
- **问题：** 在 `Plugins/` 子目录里扫描第三方 dll 并通过 MEF `Part` 化注入到 IoC；这一行为本身就要求运行时 JIT/类型加载。Caliburn.Micro 的 `AssemblySource` 同样是反射式 ViewModel/View 查找的数据源。
- **运行时风险：** AOT 下插件机制不可用；`Assembly.Location` 返回空导致 `Path.GetDirectoryName(null)` 抛错。
- **建议替换方案：** AOT 版本中改成静态注册的插件清单；插件目录扫描功能放到非 AOT 构建。

### 7. `App.CheckOrUpgradeAllSettings` —— 反射扫描设置类型并调用静态属性

- **位置：** `OngekiFumenEditor/App.xaml.cs:60-107`、关键反射调用：`:79`、`:88-91`
- **类型：** `Assembly.GetTypes` + `Type.GetProperty("Default", BindingFlags.Public | BindingFlags.Static)` + `PropertyInfo.GetValue` + 派生类型筛选 (`typeof(ApplicationSettingsBase).IsAssignableFrom`)
- **AOT 警告代码：** IL2026/IL2070/IL2075（GetTypes、GetProperty + 字符串名）
- **问题：** 通过反射查找所有 `ApplicationSettingsBase` 派生类，再用 `GetProperty("Default", Static).GetValue(null)` 取 `ProgramSetting.Default` 等 singleton 并调用 `Upgrade/Reload/Save`。Trimmer 不可能把 `Default` 属性保留下来。
- **建议替换方案：** 用一个显式的静态数组列出所有 settings 单例（参考 `ProgramSettingViewModel.ResetAllSettings` 已经手动列了一份）。

### 8. `App.xaml.cs` —— `AssemblyResolve` 从 Costura 嵌入资源加载 satellite assemblies

- **位置：** `OngekiFumenEditor/App.xaml.cs:33`、`:39-188`
- **类型：** `AppDomain.CurrentDomain.AssemblyResolve` + `Assembly.Load(byte[])`
- **AOT 警告代码：** IL3000/IL3001（`Assembly.GetExecutingAssembly().Location`/单文件不支持）
- **问题：** Costura.Fody + 自定义 `AssemblyResolve` 在运行期反射式地从嵌入资源读取 satellite dll。NativeAOT 自带静态链接，不存在程序集解析的概念。
- **建议替换方案：** AOT 下放弃 Costura.Fody（项目 csproj 也已注意到 Debug 模式禁用 Fody），并让多语言资源走标准 satellite assemblies 或者 EmbeddedResource + 手工 `ResourceManager` 流加载。

### 9. `EditorLayoutManager.TryGetDependices` —— 反射读取 `Gemini.Framework` 私有字段

- **位置：** `OngekiFumenEditor/Kernel/EditorLayout/EditorLayoutManager.cs:34-39`
- **类型：** `Type.GetField("_shellView", BindingFlags.NonPublic | BindingFlags.Instance) + FieldInfo.GetValue(...)`
- **AOT 警告代码：** IL2075/IL2070
- **问题：** 直接用字符串 `"_shellView"` 反射拿第三方框架的私有字段；trimmer 必然会把这个字段移除/重命名。
- **建议替换方案：** 给 `Gemini` 源码加 public API；或在 `ShellViewModel` 上加 `[DynamicDependency("_shellView", typeof(Gemini.Modules.Shell.ViewModels.ShellViewModel))]`。

### 10. `MultiObjectsPropertyInfoWrapper` —— 反射构造 `EqualityComparer<T>` + `PropertyInfo` 数据流

- **位置：** `OngekiFumenEditor/Modules/FumenObjectPropertyBrowser/MultiObjectsPropertyInfoWrapper.cs:33-39`、`:70-94`、`:118-122`
- **类型：** `typeof(EqualityComparer<>).MakeGenericType(...).GetProperty("Default").GetValue(null)`、`objType.GetProperty(propertyName, BindingFlags...)`、`Activator.CreateInstance(propertyInfo.PropertyType)`
- **AOT 警告代码：** IL3050（MakeGenericType）+ IL2070/IL2075（GetProperty）+ IL2072（PropertyType 通过 boxing 流失注解）
- **问题：** 对象属性浏览器核心：先 `objType.GetProperty(propertyName)` 拿到 `PropertyInfo`，再为每种属性类型动态生成对应的 `EqualityComparer<T>.Default`，最后 `Activator.CreateInstance(propType)` 制造 default 值。该类作用于编辑器右侧属性面板，所有 OngekiObject 编辑均路径强相关。
- **建议替换方案：**
  - 改用 `EqualityComparer<object>.Default` 配合包装；或对每个具体属性类型预注册 comparer。
  - 用 source generator 给每个 `OngekiObjectBase` 子类生成 `IDictionary<string, IPropertyAccessor>`，绕开 `PropertyInfo`。

### 11. `PropertyInfoWrapper` —— `PropertyInfo.GetValue/SetValue` + `TypeDescriptor.GetConverter` 全反射

- **位置：** `OngekiFumenEditor/UI/Controls/ObjectInspector/UIGenerator/PropertyInfoWrapper.cs:34-67`、`:88-117`
- **类型：** `PropertyInfo.GetValue/SetValue`、`TypeDescriptor.GetConverter(...).ConvertFrom(...)`、多处 `GetCustomAttribute<T>()`
- **AOT 警告代码：** IL2070/IL2075/IL2026（TypeDescriptor 反射）
- **问题：** UI Inspector 主路径：所有谱面对象属性的读写、转换、注解读取全靠反射。`TypeDescriptor` 在 AOT 下需要 fallback 字符串解析器或者预注册 `TypeConverter`。
- **建议替换方案：** source generator 为每个对象生成 `IObjectPropertyAccessProxy` 实例池。

### 12. `FumenObjectPropertyBrowserViewModel.OnObjectChanged` —— 对所有选中对象做 `GetProperties(...)`

- **位置：** `OngekiFumenEditor/Modules/FumenObjectPropertyBrowser/ViewModels/FumenObjectPropertyBrowserViewModel.cs:45-89`
- **类型：** `obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)` + `GetCustomAttribute<...>` × 5
- **AOT 警告代码：** IL2070/IL2075
- **问题：** 多选属性合并的主入口，同样基于反射。
- **建议替换方案：** 与 #10/#11 一起用 source generator 处理。

### 13. `ObjectInspectorViewModel.OnObjectChanged` —— 通用 inspector 反射枚举属性

- **位置：** `OngekiFumenEditor/UI/Controls/ObjectInspector/ViewModels/ObjectInspectorViewModel.cs:39-58`
- **类型：** `GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)`
- **AOT 警告代码：** IL2070/IL2075
- **问题：** 类似 #12，针对任意对象做属性列表。
- **建议替换方案：** 同上。

### 14. `ParserUtils.GetDataArray<T>` & `Parser.Ogkr.CommandArgs.GetDataArray<T>` —— `TypeDescriptor.GetConverter(typeof(T))`

- **位置：**
  - `OngekiFumenEditor/Parser/ParserUtils.cs:18-25`
  - `OngekiFumenEditor/Parser/Ogkr/CommandArgs.cs:42-66`
- **类型：** `TypeDescriptor.GetConverter` + 反射 `ConvertFromString`
- **AOT 警告代码：** IL2026（`TypeDescriptor` 路径上的反射）
- **问题：** OGKR 谱面解析的核心：从字符串解析任意 `T` 都依赖 `TypeDescriptor` 反射式 converter 查找。AOT 下需要为每个 `T` 显式注册 `TypeConverter` 或者放弃此通用泛型。
- **建议替换方案：** 写显式的 `if (typeof(T) == typeof(int)) return int.Parse(...);` 分支或者用 source generator。

---

## P1 高优先级警告

### 15. `ViewHelper.CreateView` —— `viewModel.GetType().GetCustomAttribute<MapToViewAttribute>()`

- **位置：** `OngekiFumenEditor/Utils/ViewHelper.cs:21-23`
- **类型：** `GetType().GetCustomAttribute<MapToViewAttribute>()` + `CacheLambdaActivator.CreateInstance(mapToAttr.ViewType)` + `Caliburn.Micro.ViewLocator.LocateForModel`
- **AOT 警告代码：** IL2075（ViewType）/ IL2026（ViewLocator 反射）
- **问题：** Caliburn.Micro 的 view/viewmodel 绑定整个建立在反射之上。
- **建议替换方案：** 转向显式 `IViewFor<T>` 映射注册或预生成的 view dictionary。

### 16. `EnumValueTypeUIViewModel` —— `Enum.GetNames(type)` + `Enum.Parse(type, string)`

- **位置：** `OngekiFumenEditor/UI/Controls/ObjectInspector/ViewModels/EnumValueTypeUIViewModel.cs:9-15`
- **类型：** 非泛型 `Enum.GetNames(Type)` / `Enum.Parse(Type, string)`
- **AOT 警告代码：** IL2026（这两个非泛型重载在 .NET 9 标注了 `[RequiresDynamicCode]`/`[RequiresUnreferencedCode]`）
- **建议替换方案：** 用泛型 `Enum.GetNames<T>()` / `Enum.Parse<T>` 或者 `Enum.GetValuesAsUnderlyingType` + 类型分发。

### 17. `MultiKeyGestureConverter` 与 `Configure` 中的运行期 `TypeConverter` 实例化

- **位置：**
  - `OngekiFumenEditor/UI/KeyBinding/Input/MultiKeyGestureConverter.cs:16-126`
  - `OngekiFumenEditor/AppBootstrapper.cs:191`：`(MultiKeyGesture)new MultiKeyGestureConverter().ConvertFrom(splits[1])`
- **类型：** `TypeConverter` 派生 + 显式调用 `ConvertFrom`，未通过 source generator
- **AOT 警告代码：** IL2026
- **问题：** WPF `KeyConverter`、`ModifierKeysConverter` 本身在 AOT 上也是反射型 converter；项目同时还在 Caliburn.Micro Parser 钩子里调用它。
- **建议替换方案：** 改成纯字符串解析（不走 `TypeConverter`）。

### 18. `JsonSerializer.Serialize/Deserialize` 反射版本（IL3050/IL2026 主要来源）

- **位置：**
  - `OngekiFumenEditor/AppBootstrapper.cs:424` —— `JsonSerializer.Deserialize<IPCHelper.ArgsWrapper>(...)`
  - `OngekiFumenEditor/Utils/IPCHelper.cs:67` —— `JsonSerializer.Serialize(new ArgsWrapper { Args = args })`
  - `OngekiFumenEditor/Kernel/SettingPages/Program/ViewModels/ProgramSettingViewModel.cs:214` —— `JsonSerializer.Serialize(software)`
  - `OngekiFumenEditor/Kernel/KeyBinding/DefaultKeyBindingManager.cs:51,64` —— `Config` 类型
  - `OngekiFumenEditor/Kernel/RuntimeAutomation/RuntimeAutomationScriptHost.cs:379,384` —— `JsonSerializer.Serialize(result)`（result 是 `object`，最差情况）
  - `OngekiFumenEditor/Kernel/RuntimeAutomation/McpOperationLogHelper.cs:60` —— `JsonSerializer.SerializeToElement(payload, payload.GetType(), options)`（运行时 Type）
  - `OngekiFumenEditor/Kernel/RecentFiles/DefaultImp/DefaultEditorRecentFilesManager.cs:32,50` —— `List<RecentRecordInfo>`
  - `OngekiFumenEditor/Modules/FumenVisualEditor/Kernel/EditorProjectFile/Serializers/CommonEditorProjectFileSerializer.cs:58-74` —— 含匿名类型 + 自定义 `TimeSpanJsonConverter`
  - `OngekiFumenEditor/Modules/FumenVisualEditor/Kernel/EditorProjectFile/Migrations/Migration_V0_5_2_To_Latest.cs:23-25` —— `EditorProjectDataModel_V0_5_2` ↔ `EditorProjectDataModel`
  - `OngekiFumenEditor/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.Drawing.cs:166,203` —— `Dictionary<string, RenderTargetOrderVisible>`
- **类型：** `System.Text.Json` 反射 mode
- **AOT 警告代码：** IL2026 + IL3050（每处至少一条）
- **问题：** 没有任何一处用 `JsonSerializerContext`/source generator。其中 `McpOperationLogHelper` 还用了 `payload.GetType()`（运行时 Type）这种最不友好的形式，AOT 下完全没法静态推断。
- **建议替换方案：** 集中创建一个或多个 `[JsonSerializable]` partial `JsonSerializerContext`，覆盖：`ArgsWrapper`、`Software`、`KeyBindingManager.Config`、`List<RecentRecordInfo>`、`EditorProjectDataModel*`、`Dictionary<string, RenderTargetOrderVisible>` 等；对 `RuntimeAutomationScriptHost.SerializeReturnValue` 这类返回 `object` 的场景标 `[RequiresUnreferencedCode]` 或改成 `ToString()` fallback。

### 19. `DefaultEditorScriptExecutor.AppDomain_AssemblyResolve` 配合脚本宿主

- **位置：** `OngekiFumenEditor/Modules/EditorScriptExecutor/Kernel/DefaultImpl/DefaultEditorScriptExecutor.cs:63`、`:203-211`
- **类型：** `AppDomain.CurrentDomain.AssemblyResolve` + 自维护的 `Dictionary<string, Assembly>`
- **AOT 警告代码：** IL3000
- **问题：** 同 #4 同根问题；AOT 下没有 AssemblyResolve。
- **建议替换方案：** 同 #4，整体在 AOT 中砍掉脚本子系统。

### 20. `Marshal.GetDelegateForFunctionPointer(ptr, typeof(T))` —— WGL/GLX 函数指针绑定

- **位置：**
  - `OngekiFumenEditor/Kernel/Graphics/Skia/GlContexts/Wgl/Wgl.cs:229`
  - `OngekiFumenEditor/Kernel/Graphics/Skia/GlContexts/Glx/Glx.cs:62`
- **类型：** `Marshal.GetDelegateForFunctionPointer(IntPtr, Type)` 非泛型重载
- **AOT 警告代码：** IL2026（该重载在 .NET 9 标注 `[RequiresDynamicCode]`）
- **问题：** 通过函数指针把 OpenGL 扩展函数包成委托，老的非泛型 API 触发动态代码生成警告。
- **建议替换方案：** 切到泛型 `Marshal.GetDelegateForFunctionPointer<T>(IntPtr)` 或更现代的 `[UnmanagedCallersOnly]`/`FunctionPointer<>`。

### 21. `Assembly.GetExecutingAssembly().Location` / `typeof(X).Assembly.Location` 多处使用

- **位置：**
  - `OngekiFumenEditor/App.xaml.cs:35`
  - `OngekiFumenEditor/AppBootstrapper.cs:122`、`:212`
  - `OngekiFumenEditor/Kernel/ProgramUpdater/DefaultProgramUpdater.cs:215`、`:234`
  - `OngekiFumenEditor/UI/Dialogs/ExceptionTermWindow.xaml.cs:30`
  - `OngekiFumenEditor/Modules/EditorScriptExecutor/Documents/ViewModels/EditorScriptDocumentViewModel.cs:167`
  - `OngekiFumenEditor/Modules/EditorScriptExecutor/Kernel/DefaultImpl/DefaultDocumentContext.cs:62-73`
- **类型：** 单文件/AOT 下 `Assembly.Location` 返回空字符串
- **AOT 警告代码：** IL3000
- **问题：** 这些位置普遍把 `Location` 当成"程序所在目录"使用；AOT 下需要换成 `AppContext.BaseDirectory` 或 `Environment.ProcessPath`。
- **建议替换方案：** 全局替换为 `AppContext.BaseDirectory` 或 `Path.GetDirectoryName(Environment.ProcessPath)`。

### 22. `FileVersionInfo.GetVersionInfo(typeof(...).Assembly.Location)`

- **位置：** `OngekiFumenEditor/AppBootstrapper.cs:212`、`OngekiFumenEditor/UI/Dialogs/ExceptionTermWindow.xaml.cs:30`
- **类型：** 同 #21 + 进一步把空字符串送入 `FileVersionInfo`
- **AOT 警告代码：** IL3000
- **建议替换方案：** 改为 `AssemblyInformationalVersionAttribute` 反读或者 `Environment.ProcessPath`。

### 23. `OngekiFumenSet.GetPathValue<T>` —— 反射式 `TypeDescriptor.GetConverter(typeof(T))`

- **位置：** `OngekiFumenEditor/Modules/OgkiFumenListBrowser/Models/OngekiFumenSet.cs:77-88`
- **类型：** `TypeDescriptor.GetConverter` + `ConvertFromString`
- **AOT 警告代码：** IL2026
- **建议替换方案：** 同 #14。

### 24. `EnumSpecificationOption` —— `Enum.GetValues(enumType)`/`Enum.GetValues(typeof(T))`（非泛型）

- **位置：** `OngekiFumenEditor/Modules/FumenEditorSelectingObjectViewer/Base/SelectionFilter/SelectionFilterOptions.cs:238`、`:269`、`:295`、`:579`
- **类型：** 非泛型 `Enum.GetValues(Type)` 重载
- **AOT 警告代码：** IL3050（标注 `[RequiresDynamicCode]`）
- **建议替换方案：** 改用 `Enum.GetValues<T>()`，把入口由 `Type` 改成泛型。

### 25. Caliburn.Micro `Parser.CreateTrigger` 钩子里的 `Enum.Parse(typeof(Key), splits[1], true)`

- **位置：** `OngekiFumenEditor/AppBootstrapper.cs:187`
- **类型：** 非泛型 `Enum.Parse`
- **AOT 警告代码：** IL2026
- **建议替换方案：** `Enum.Parse<Key>(splits[1], true)`。

### 26. `Assembly.GetTypes()` 全集枚举（`App.xaml.cs`）

- **位置：** `OngekiFumenEditor/App.xaml.cs:79`
- **类型：** `Assembly.GetTypes()`
- **AOT 警告代码：** IL2026
- **问题：** 即使没有 trimmer 也会出现"类型已被裁剪"的运行时缺失。
- **建议替换方案：** 同 #7。

### 27. `epType.GetMethod(ep.MetadataName)` + `MethodInfo.CreateDelegate(typeof(Func<object[], Task<object>>))`

- **位置：** `OngekiFumenEditor/Modules/EditorScriptExecutor/Kernel/DefaultImpl/DefaultEditorScriptExecutor.cs:218-226`
- **类型：** 反射定位脚本入口方法 + `CreateDelegate`
- **AOT 警告代码：** IL2075/IL2026
- **建议替换方案：** 同 #4。

### 28. `Type.MakeGenericType` 直接出现共 3 处

- **位置：**
  - `OngekiFumenEditor/Modules/FumenObjectPropertyBrowser/MultiObjectsPropertyInfoWrapper.cs:36`
  - `OngekiFumenEditor/Kernel/CommandExecutor/DefaultCommandExecutor.cs:215`
  - `OngekiFumenEditor/Kernel/CommandExecutor/DefaultCommandExecutor.cs:221`
- **AOT 警告代码：** IL3050（`MakeGenericType` 是 NativeAOT 明确不能静态推断的 API）
- **建议替换方案：** 改为预生成的 `Dictionary<Type, Func<object>>` 或 source generator。

### 29. MEF `[Export]/[Import]/[ImportingConstructor]/[ImportMany]` 用法广泛

- **位置（节选，全项目 250+ 文件）：**
  - `OngekiFumenEditor/Kernel/KeyBinding/DefaultKeyBindingManager.cs:38-39` —— `[ImportingConstructor] + [ImportMany] KeyBindingDefinition[]`
  - `OngekiFumenEditor/Kernel/EditorLayout/EditorLayoutManager.cs:21-25` —— `[Export]+[Import]`
  - `OngekiFumenEditor/Kernel/CommandExecutor/DefaultCommandExecutor.cs:32` —— `[Export]`
  - `OngekiFumenEditor/Kernel/RuntimeAutomation/RuntimeAutomationScriptHost.cs:22-24,39-45`
  - `OngekiFumenEditor/Modules/EditorScriptExecutor/Kernel/DefaultImpl/DefaultEditorScriptExecutor.cs:23-25`
- **类型：** MEF（System.ComponentModel.Composition）属性式 DI
- **AOT 警告代码：** IL2026/IL3050（MEF 内部全是反射）
- **问题：** 主项目的 IoC/DI 完全是 MEF 驱动的（`IoC.Get<T>()`、`IoC.GetAll<T>()`），属于框架级阻塞。
- **建议替换方案：** 长期看必须换 `Microsoft.Extensions.DependencyInjection` + 自动注册的 source generator（例如 [Jab](https://github.com/pakrym/jab) / [StrongInject](https://github.com/YairHalberstadt/stronginject)）。这是一项大规模重构。

---

## P2 警告与待评估

### 30. `[DllImport]`（非 `[LibraryImport]`）

- **位置：** 共 60+ 处，集中在：
  - `OngekiFumenEditor/Utils/IniFile.cs:17,20`
  - `OngekiFumenEditor/Utils/DeadHandler/DumpFileHelper.cs:27,30,33,36`
  - `OngekiFumenEditor/UI/ListViewDragDropManager/MouseUtilities.cs:24,27`
  - `OngekiFumenEditor/Kernel/Graphics/OpenGL/DefaultOpenGLRenderManagerImpl.cs:40,43`
  - `OngekiFumenEditor/Kernel/Audio/NAudioImpl/SoundTouch/SoundTouchInterop64.cs:12-174`
  - `OngekiFumenEditor/Kernel/Graphics/Skia/Utils/Win32/{User32,Kernel32,Gdi32}.cs`
  - `OngekiFumenEditor/Kernel/Graphics/Skia/GlContexts/{Wgl,Glx,Cgl}/*.cs`
  - `OngekiFumenEditor/Kernel/Graphics/Skia/Utils/X11/Xlib.cs`
- **类型：** `[DllImport]` 没有迁移到源生成的 `[LibraryImport]`
- **AOT 警告代码：** SYSLIB1054（建议而非强阻塞）+ marshalling 复杂结构时可能 IL2050
- **问题：** AOT 下 `[DllImport]` 仍能工作，但若涉及复杂 marshalling（如字符串、结构体）会比 `[LibraryImport]` 慢且潜在不安全。
- **建议替换方案：** 渐进式迁移为 `[LibraryImport]`，目前不属于阻塞。

### 31. `ConfigurationManager.OpenExeConfiguration(...)` —— App.config 反射

- **位置：** `OngekiFumenEditor/App.xaml.cs:62`
- **类型：** `System.Configuration` 反射 + XML
- **AOT 警告代码：** IL2026
- **建议替换方案：** 改成自维护 JSON 配置文件。

### 32. `ApplicationSettingsBase`（设计器生成的 `Settings`）

- **位置：** 整体 `Properties/*.settings.cs`、`Settings.Designer.cs`（在主项目内）+ `App.xaml.cs:80-94` 通过反射调用
- **类型：** `LocalFileSettingsProvider` 内部用反射读写属性
- **AOT 警告代码：** IL2026
- **建议替换方案：** 同 #31。

### 33. `XPathSelectElement` / `XPathSelectElements` —— `System.Xml.XPath`

- **位置：** `OngekiFumenEditor/Modules/OgkiFumenListBrowser/Models/OngekiFumenSet.cs:46,80`
- **类型：** XPath 字符串编译
- **AOT 警告代码：** 通常不会有 IL 警告，但运行时部分实现走反射 + `Regex.CompileToAssembly`。
- **建议替换方案：** 改用 `XDocument.Descendants(...)`/`Element(...)` 链式 API。

### 34. `MahApps.Metro.Controls`、`AvalonEdit` 等大量 WPF 第三方 UI 控件

- **位置：** 见 csproj/`<PackageReference>`
- **类型：** XAML + DependencyProperty 反射
- **AOT 警告代码：** 不在本项目代码内
- **说明：** 仅作记录；详见"框架层级前置阻塞"。

### 35. `AbortableThread` / `WindowsIdentity`/`WindowsPrincipal`

- **位置：** `OngekiFumenEditor/Utils/AbortableThread.cs`、`OngekiFumenEditor/AppBootstrapper.cs:219-222`
- **类型：** `Thread.Abort` 在新 .NET 已不可用；`WindowsIdentity` 在 AOT/单文件下行为不变但需要 Windows 平台
- **AOT 警告代码：** PlatformNotSupported（非 IL，但是行为差异）
- **建议替换方案：** `CancellationToken` 替代 `Thread.Abort`。

### 36. `FileVersionInfo`、`FodyWeavers`/Costura 嵌入资源、`pack://application:,,,/...` URI

- **位置：** `AppBootstrapper.cs:321-323`、`ProgramSettingViewModel.cs:132`
- **类型：** WPF pack URI 内部走 BAML + 程序集资源
- **AOT 警告代码：** 不在本项目代码内（WPF 限制）
- **说明：** 仅作记录。

### 37. `XGridTypeUIViewModel/TGridTypeUIViewModel/...` 各 UIGenerator —— 通过 `IoC.GetAll<ITypeUIGenerator>()` 反射式注册

- **位置：** `OngekiFumenEditor/UI/Controls/ObjectInspector/UIGenerator/PropertiesUIGenerator.cs:14-19` 及 `UIGenerator/TypeImplement/*.cs`
- **类型：** MEF 派发 + 反射构造
- **AOT 警告代码：** 同 #29
- **建议替换方案：** 同 #29。

### 38. `Caliburn.Micro.Parser.CreateTrigger` 钩子里隐式依赖框架反射

- **位置：** `OngekiFumenEditor/AppBootstrapper.cs:171-197`
- **类型：** Caliburn.Micro 内部反射（不可控）
- **AOT 警告代码：** IL2026（来自第三方）
- **建议替换方案：** 同 Caliburn.Micro 前置阻塞，无法在本项目内完全消除。

---

## 框架层级前置阻塞（不在本项目代码内，但需指出）

| 项 | 说明 | 当前状态 | 建议 |
|---|---|---|---|
| **WPF + NativeAOT** | `UseWPF=true`，net10.0-windows | .NET 10 仍未正式支持 AOT 发布 WPF 应用，BAML/XAML 强依赖运行期 reader | 短期等 WPF AOT 路线图；若必须 AOT 化，考虑 Avalonia/Uno |
| **Caliburn.Micro** | `ViewLocator`、`ActionMessage`、`Convention` 大量反射 | 无 AOT 友好版本 | 长期需替换为支持 source generator 的 MVVM 框架 |
| **System.ComponentModel.Composition (MEF)** | `DirectoryCatalog`、`AggregateCatalog`、`[Export]/[Import]` 全反射 | 项目主 IoC | 必须替换为 MS.DI + source generator |
| **Gemini.Framework** | 主项目基类（`Tool`、`PersistedDocument`、`CommandDefinition` 等）来自 Gemini，内部 MEF + WPF | 第三方 | 替换或 fork |
| **Costura.Fody** | 编译期把依赖塞进资源，运行时 AssemblyResolve | 与 single-file/AOT 冲突 | 在 AOT 构建里禁用（项目 csproj 已经在 Debug 禁用） |
| **MahApps.Metro / AvalonEdit / Microsoft.Xaml.Behaviors / DryIoc.Microsoft.DependencyInjection** | 多个 NuGet 内部反射 | 不可控 | 监控上游 AOT 支持 |
| **Roslyn (`Microsoft.CodeAnalysis.CSharp.Scripting`)** | 脚本执行的核心 | 永远不会 AOT-friendly | AOT 构建删脚本子系统 |
| **NAudio + SoundTouch + Skia** | 大量 native 互操作（已用 `[DllImport]`） | 兼容 AOT，但需迁 `[LibraryImport]` 才能避免 marshaller 反射 | 渐进迁移 |

---

## 跨切关注点

- **反射"集中点"：**
  - `OngekiFumenEditor/Utils/LambdaActivator.cs` —— 全局通用反射 + Expression.Compile 工厂（**整改优先级最高**）
  - `OngekiFumenEditor/AppBootstrapper.cs` 与 `OngekiFumenEditor/App.xaml.cs` —— 启动期 MEF 插件加载、AssemblyResolve、设置 upgrade
  - `OngekiFumenEditor/UI/Controls/ObjectInspector/UIGenerator/PropertyInfoWrapper.cs` 与 `OngekiFumenEditor/Modules/FumenObjectPropertyBrowser/*` —— 属性面板反射核心
  - `OngekiFumenEditor/Modules/EditorScriptExecutor/Kernel/DefaultImpl/*` —— 整个脚本子系统都是动态代码
  - `OngekiFumenEditor/Parser/ParserUtils.cs` + `OngekiFumenEditor/Parser/Ogkr/CommandArgs.cs` —— OGKR 解析层 `TypeDescriptor` 反射

- **建议引入 source generator 的位置：**
  1. **`OngekiObjectBase` 子类工厂**：为每个谱面对象生成 `CreateNew()`、`CopyNew()`、`IPropertyAccessor`，替换 `LambdaActivator`/`CacheLambdaActivator`、`PropertyInfo.GetValue/SetValue`。
  2. **JsonSerializerContext**：合并所有 `JsonSerializer.Serialize/Deserialize` 调用涉及的类型。
  3. **MEF → MS.DI 自动注册**：用 [Jab](https://github.com/pakrym/jab)/[StrongInject](https://github.com/YairHalberstadt/stronginject) 在编译期生成容器，替换 `IoC.Get<T>()`/`[Export]`。
  4. **`ITypeUIGenerator` 注册表**：在编译期把所有实现类列入静态数组，避免 `IoC.GetAll<ITypeUIGenerator>()` 反射枚举。
  5. **`OptionBindingAttrbute` → CLI Option 注册**：用 generator 在编译期把命令行类型的属性 → System.CommandLine `Option` 列表全部展开，消灭 #3。

---

## 迁移可行性结论

- **结论：当前 OngekiFumenEditor 主项目无法迁移到 NativeAOT。**
- **核心原因：**
  - WPF / Caliburn.Micro / MEF / Gemini.Framework / Costura.Fody / Roslyn Scripting 这六大框架级依赖整体未支持 NativeAOT，构成不可绕过的前置阻塞。
  - 主项目自身还存在 14 处 P0 级（`Expression.Compile`、`MakeGenericType`、反射式工厂、Roslyn 动态编译、MEF DirectoryCatalog、属性面板反射、CLI 命令反射生成、JSON 反射序列化、`AppDomain.AssemblyResolve` 等），全部在编辑器主路径上，移除任何一个都需要大规模重构。
- **可行的中间策略（不追求完整 AOT，但缩小可信运行集）：**
  1. 在主项目 `.csproj` 加 `<IsTrimmable>false</IsTrimmable>`，先确保普通发布不被裁剪误伤。
  2. **逐步引入 source generator** 替换 #1/#2/#10/#11/#12/#13/#14/#27 等"通用反射工厂"，把热路径上的反射降下来。
  3. 把 JSON 调用都迁到 `JsonSerializerContext`（这一步本身就能直接减很多 IL2026/IL3050 警告）。
  4. CLI 工具子项目（`OngekiFumenEditor.CommandLine`）单独抽出来做 AOT —— 但 CLI 项目不在本扫描范围内，需另行评估。
- **长期方向：** 若严格需要 AOT，整体需要换 UI 框架（Avalonia/Uno）+ 自研 DI + 移除脚本子系统，本质相当于重写大半个项目。

---

> 本扫描仅基于静态匹配，没有运行 IL 分析器；真正启用 `<IsAotCompatible>true</IsAotCompatible>` 后编译还会出现更多的 IL2026/IL2070/IL3050 警告。建议在开始迁移前，先在脱敏的副本上启用分析器跑一遍 `dotnet build -p:IsAotCompatible=true` 收集完整 warning 列表。
