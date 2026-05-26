# 反射操作过重代码清单

> 仓库：OngekiFumenEditor（.NET 9 / WPF / Caliburn.Micro / MEF）
> 扫描范围：仅 `OngekiFumenEditor/` 主项目（已排除 `Dependences/`、`OngekiFumenEditor.Benchmark/`、`OngekiFumenEditor.CommandLine/`、`bin/`、`obj/`）
> 扫描时间：2026-05-26

## 摘要

- **扫描 .cs 文件数**：约 1072 个
- **包含反射 API 调用的文件**：约 72 个
- **本报告聚焦的"项目自己写的反射热点"**：22 个具体反射点（外加 3 处框架强制反射备注）
- **反射密集目录排名**：
  1. `Modules/FumenObjectPropertyBrowser/`、`UI/Controls/ObjectInspector/`：属性面板，按对象类型枚举 `PropertyInfo` + 逐属性 `GetCustomAttribute<>` —— 反射最重
  2. `Base/`（`OngekiObjectBase`、`ConnectableObject/ConnectableStartObject`）：通过 `CacheLambdaActivator` 克隆 Ongeki 对象 —— 调用极频繁，但已有缓存，问题不大
  3. `Kernel/CommandExecutor/DefaultCommandExecutor.cs`：CLI Option 注册时大量 `MakeGenericType` + `Expression.Compile` + `PropertyInfo.SetValue` —— 启动一次
  4. `App.xaml.cs` / `AppBootstrapper.cs`：启动加载 Settings、加载插件 Assembly —— 启动一次
  5. `Modules/EditorScriptExecutor/`：脚本执行的 entry point 反射查找（按需，一次）
- **总体可优化空间**：
  - **高 ROI**：MultiObjectsPropertyInfoWrapper 的 `cacheComparerMap` 是实例字段，缓存无效；每个对象选中都会重做 `GetProperty(name)` × N 个对象 + `GetCustomAttribute` × ≥3 次。可以引入"per-Type 属性元数据缓存"。
  - **中 ROI**：`CommandArgs.GetDataArray<T>` 用 `TypeDescriptor.GetConverter(typeof(T))` 每次取，谱面解析每行命中。
  - **低 ROI**：`OngekiObjectBase.CopyNew()`、`InterpolateCurve(Type, Type, Type, …)` 已用 `CacheLambdaActivator`，命中是缓存路径，无需进一步改造。
  - **零 ROI（框架强制）**：MEF（`[Export]/[Import]`、`DirectoryCatalog`、`ComposeParts`）、Caliburn.Micro IoC、WPF Binding、.NET Settings `Default` 属性、`Costura.Fody` ResolveEvent。

---

## 项目内反射辅助层

### `OngekiFumenEditor/Utils/ReflectionHelp.cs`
- **现状**：仅提供 `Type.GetTypeName()` 扩展。维护一个 `Dictionary<Type, string> cacheNames`，已缓存。
- **频度**：被 `OngekiObjectBase.Name` 调用（每次访问 `obj.Name`，例如日志输出/UI 提示）。命中是缓存路径，每个 Type 只做一次拼接。
- **结论**：F. 不值得改。

### `OngekiFumenEditor/Utils/LambdaActivator.cs`
- **现状**：基于 `Expression.Lambda(Type, NewExpression, …).Compile()` 构造无参/带参构造函数委托；提供两个层：
  - `LambdaActivator.CreateInstance(Type, params object[])`：**每次都会重新调用 `GetActivator(ctor)` 编译 Expression**，未缓存（CommandExecutor、`InterpolateCurve(Type,…)`、`SvgPrefabOperationViewModel` 调用此 API）。
  - `CacheLambdaActivator.CreateInstance(Type)`：**有 `Dictionary<Type, ObjectActivator>` 缓存，但仅支持无参构造**（`OngekiObjectBase.CopyNew()`、`ConnectableObjectSplitDropAction`、`DefaultToolBoxDropAction`、`ViewHelper.CreateView`）。
- **频度**：`CopyNew()` 在剪贴板、复制粘贴、SvgPrefab 大量重复对象时单次操作可调用上百次。命中是缓存路径，问题不大。
- **隐患**：
  1. `CacheLambdaActivator` 的字典不是 `ConcurrentDictionary`，且没有 `lock`，理论上多线程并发首次访问同一 Type 会有数据竞争（实际几乎只在 UI 线程使用）。
  2. `LambdaActivator.CreateInstance(Type, args)` 路径没有缓存，**每次都 Expression.Compile()**，是真正昂贵的（一次 ~ms 级，且会留下 JIT 内存）。

---

## 热点反射点（按严重度排序）

### 1. `MultiObjectsPropertyInfoWrapper.cs` —— 多选属性面板每次刷新都跑反射
- **位置**：`OngekiFumenEditor/Modules/FumenObjectPropertyBrowser/MultiObjectsPropertyInfoWrapper.cs:26-45,60-103,114-122`
- **反射 API**：`GetProperty(name, BindingFlags)`、`GetCustomAttribute<>()` × 3、`MakeGenericType(EqualityComparer<>)`、`GetProperty("Default").GetValue(null)`、`Activator.CreateInstance(propertyInfo.PropertyType)`
- **调用频次**：用户每次"重新选中一组对象"就触发；多选 N 个对象时跑 N×(`GetProperty` + 至少 3 次 `GetCustomAttribute`)，对每个共有属性还会从 `EqualityComparer<>.MakeGenericType(...).GetProperty("Default").GetValue(null)` 拿一次 comparer。
- **是否已缓存**：**严重错误的缓存**——`cacheComparerMap` 声明为**实例字段**（line 26），每个 `MultiObjectsPropertyInfoWrapper` 一份；这个 wrapper 本身也是每次选择都新建。等同于没缓存。
- **问题**：
  1. `Activator.CreateInstance(propertyInfo.PropertyType)`（line 119）创建值类型默认值，可改 `RuntimeHelpers.GetUninitializedObject`，或在 type→box 表上缓存。
  2. `MakeGenericType + GetProperty("Default") + GetValue(null)` 完全可以替换为 `EqualityComparer<object>.Default`（或 `(IEqualityComparer)typeof(EqualityComparer<TX>).GetField(...)` 缓存到 `static Dictionary<Type, IEqualityComparer>`）。
- **替换方案**：**D. 一次性注册 + B. 缓存委托**——把 `cacheComparerMap` 提升为 `static ConcurrentDictionary<Type, IEqualityComparer>`；为 `(Type, propName)` → `(getCustomAttribute snapshot, PropertyInfo)` 做一份 per-Type 属性元数据缓存（`ConditionalWeakTable<Type, PropertyMeta[]>` 或 `static Dictionary<Type, PropertyMeta[]>`）。
- **风险/成本**：低；纯本地重构。

### 2. `FumenObjectPropertyBrowserViewModel.OnObjectChanged()` —— 选中后枚举所有属性 + Attribute
- **位置**：`OngekiFumenEditor/Modules/FumenObjectPropertyBrowser/ViewModels/FumenObjectPropertyBrowserViewModel.cs:45-89`
- **反射 API**：每个被选中对象都执行 `x.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)`，再 `GroupBy/Select` 后对每个共有属性 `GetCustomAttribute<ObjectPropertyBrowserShow>()` + `GetCustomAttribute<ObjectPropertyBrowserHide>()`。
- **调用频次**：每次选中/切换选择就跑一次。批量框选 100 个对象时反射量很大。
- **是否已缓存**：否。
- **问题**：与 #1 同源——OngekiObjectBase 的子类是有限闭集，可以启动期一次性反射并缓存"哪些属性可显示 / 别名 / 提示文本"列表。
- **替换方案**：**D. 一次性注册**——在静态构造器或首次访问时，扫描所有 `OngekiObjectBase` 子类（或所有出现过的 `GetType()`），把可显示属性写入 `static Dictionary<Type, DisplayablePropertySnapshot[]>`，包含 `{PropertyInfo, IsAllowSetNull, IsReadOnly, AliasResourceKey, TipResourceKey, SingleOnly}`。
- **风险/成本**：中；要梳理与 `MultiObjectsPropertyInfoWrapper` 共用同一份缓存。

### 3. `PropertyInfoWrapper.cs` —— 每个属性框 4-6 次 `GetCustomAttribute`
- **位置**：`OngekiFumenEditor/UI/Controls/ObjectInspector/UIGenerator/PropertyInfoWrapper.cs:34,67,88,102,111,117,119`
- **反射 API**：每次访问 `DisplayPropertyName` / `DisplayPropertyTipText` / `IsAllowSetNull` / `IsReadOnly` 都会 `PropertyInfo.GetCustomAttribute<XXXAttribute>()`；属性框还要 `PropertyInfo.GetValue/SetValue(...)`。
- **调用频次**：WPF 绑定每次刷新属性面板（≥100ms 一次）、`NotifyOfPropertyChange` 触发的多次 binding 刷新；每个 wrapper 寿命内可能上百次。
- **是否已缓存**：否。
- **问题**：
  - `GetValue/SetValue` 单次属性几乎是热路径；属性面板大量项时累加可见。
  - `GetCustomAttribute<>` 每次都走完整 attribute lookup（虽然 JIT/Runtime 内部对单 attribute 有缓存，但仍然不便宜）。
- **替换方案**：
  - 把 `IsAllowSetNull/IsReadOnly/DisplayPropertyName/DisplayPropertyTipText` 改为构造时一次性计算并缓存到字段（**A. 直接重构**）。
  - 把 `GetValue/SetValue` 替换为缓存的 `Func<object, object>` + `Action<object, object>`（**B. 缓存委托**，用 `Expression.Compile` 一次，按 `PropertyInfo` 作 key）。
- **风险/成本**：低；这是基本套路。

### 4. `ObjectInspectorViewModel.OnObjectChanged()` —— 又一处 `GetProperties + Attribute` 循环
- **位置**：`OngekiFumenEditor/UI/Controls/ObjectInspector/ViewModels/ObjectInspectorViewModel.cs:39-53`
- **反射 API**：`inspectObject?.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)`、`GetCustomAttribute<ObjectPropertyBrowserHide>()`、`GetCustomAttribute<ObjectPropertyBrowserShow>()`。
- **调用频次**：每次 `InspectObject` 设值。
- **是否已缓存**：否。
- **问题**：与 #2 同源，但作用对象不限于 OngekiObject。
- **替换方案**：**D. 一次性注册**——同 #2，共享 per-Type 元数据缓存。
- **风险/成本**：低。

### 5. `DefaultCommandExecutor.GenerateOptionsByAttributes<T>()` & `Generate<T>()` —— `Expression.Compile` + `MakeGenericType` 双连击
- **位置**：`OngekiFumenEditor/Kernel/CommandExecutor/DefaultCommandExecutor.cs:209-254`
- **反射 API**：
  - `typeof(T).GetProperties()` 遍历每个属性
  - `prop.GetCustomAttribute<OptionBindingAttrbuteBase>()`
  - `typeof(Func<,>).MakeGenericType(...)`、`typeof(Option<>).MakeGenericType(...)`
  - `Expression.Lambda(funcType, valParam, arg).Compile()`
  - `LambdaActivator.CreateInstance(optionType, ...)`（注：调用未缓存路径 #见 LambdaActivator 评述）
  - `optionType.GetProperty(nameof(Option<>.DefaultValueFactory)).SetValue(option, func)`
  - `Generate<T>` 中：`typeof(T).GetProperties()` + `prop.SetValue(obj, val)`
- **调用频次**：启动一次（`DefaultCommandExecutor` ctor 注册 5 个动词）。`Generate<T>` 每次 CLI 调用 1 次。
- **是否已缓存**：否（但仅启动反射，影响有限）。
- **问题**：`Expression.Compile()` 用来包装一个常量 default value，等价于 `_ => attribute.DefaultValue`——重型工具。
- **替换方案**：
  - **A. 直接重构**：`Option<T>.DefaultValueFactory` 可以直接传 `(ArgumentResult r) => (T)attribute.DefaultValue`，不需要 Expression Tree。
  - **B. 缓存委托**：如果保留 Expression 路径，把 `(optionType, defaultValue)` → 委托缓存到 `static ConcurrentDictionary`。
  - **C. Source Generator**：把 `OptionBindingAttribute` 改为 partial generator，启动期完全消除反射。
- **风险/成本**：中；要重构现有 `OptionBindingAttrbuteBase` 接口。

### 6. `App.xaml.cs CheckOrUpgradeAllSettings()` —— `Assembly.GetTypes()` + `GetProperty("Default", BindingFlags.Public | BindingFlags.Static)` + `Invoke`
- **位置**：`OngekiFumenEditor/App.xaml.cs:73-101`
- **反射 API**：`assembly.GetTypes()`、`typeof(ApplicationSettingsBase).IsAssignableFrom(t)`、`type.GetProperty("Default", BindingFlags.Public | BindingFlags.Static)`、`defaultProperty.GetValue(null)`。
- **调用频次**：进程启动一次（且 `__NeedUpgradeSetting=true` 时才跑）。
- **是否已缓存**：否（无需缓存，单次执行）。
- **问题**：`Assembly.GetTypes()` 会触发整 Assembly 类型加载，有概率拖慢启动 200~500ms。
- **替换方案**：**A. 直接重构**——已知 `ApplicationSettingsBase` 派生类是有限闭集（`ProgramSetting`、`AudioSetting`、`EditorGlobalSetting`、`ScriptSetting`…），改为静态数组列出 `Action[] upgrades = { () => ProgramSetting.Default.Upgrade(); … }`，完全避开反射。
- **风险/成本**：低；只是会让加新 settings 时需要手动登记一行。

### 7. `AppBootstrapper.BindServices()` —— 插件 `DirectoryCatalog.Parts` & `AppDomain.GetAssemblies()`
- **位置**：`OngekiFumenEditor/AppBootstrapper.cs:118-163`
- **反射 API**：`DirectoryCatalog(path)` 内部使用反射、`AppDomain.CurrentDomain.GetAssemblies().Where(...)`。
- **调用频次**：启动一次。
- **是否已缓存**：N/A。
- **问题**：MEF/插件机制天然需要反射。
- **替换方案**：**E. 框架限制**——保持现状即可；如果完全不要插件可以直接删除，但破坏性大。
- **风险/成本**：N/A。

### 8. `LambdaActivator.CreateInstance(Type, params object[])` 未缓存路径
- **位置**：`OngekiFumenEditor/Utils/LambdaActivator.cs:42-56`
- **反射 API**：每次都 `type.GetConstructor(args.Select(GetType))` + `Expression.Compile()`。
- **调用点**：
  - `DefaultCommandExecutor.cs:224`：启动一次
  - `ConnectableStartObject.cs:437,438`：`InterpolateCurve(Type, Type, Type, factory)` 中每曲线段调用 2 次 —— SvgPrefab 导入大量贝塞尔时可达数千次。
  - `SvgPrefabOperationViewModel.cs:96`：每次拖入 SVG Prefab。
- **是否已缓存**：否（与 `CacheLambdaActivator` 截然不同）。
- **问题**：在 SVG Prefab 导入路径下，每段 `LambdaActivator.CreateInstance(startType)` 都会重新 `Expression.Compile`，是真正的"热路径反射"。
- **替换方案**：**B. 缓存委托**——把带参版本也加缓存：`ConcurrentDictionary<(Type, Type[]), ObjectActivator>`；对于 `InterpolateCurve(startType, nextType, …)` 的两个无参构造，可直接走 `CacheLambdaActivator.CreateInstance`。
- **风险/成本**：低；只需修改 `LambdaActivator` 内部实现。

### 9. `OngekiObjectBase.CopyNew()` —— 通过反射克隆
- **位置**：`OngekiFumenEditor/Base/OngekiObjectBase.cs:44-49`
- **反射 API**：`CacheLambdaActivator.CreateInstance(GetType())`。
- **调用频次**：剪贴板粘贴、批处理生成、SvgPrefab 导入。可达几百次/操作。
- **是否已缓存**：是（`CacheLambdaActivator` 内部 Dict）。
- **问题**：命中缓存路径，调用委托即可，开销近似 `new`。
- **替换方案**：**F. 不值得改**；如果实在要继续优化，可以改为虚函数 `protected abstract OngekiObjectBase CreateInstance()` 让每个子类自己 `new`（A）。但 ROI 极低。
- **风险/成本**：F。

### 10. `Parser/Ogkr/CommandArgs.GetDataArray<T>()` —— 谱面解析逐行 `TypeDescriptor.GetConverter`
- **位置**：`OngekiFumenEditor/Parser/Ogkr/CommandArgs.cs:40-66`
- **反射 API**：`TypeDescriptor.GetConverter(type)`。
- **调用频次**：谱面解析每行都会走至少 1 次（落到 `else` 分支，没有自定义 `IArgValueConverter` 的类型，如 `int`、`float`、`string`）。一个谱面常 1000+ 行。
- **是否已缓存**：缓存了**结果数组** `cacheDataArray[type] = arr`（行级缓存），但 `TypeDescriptor.GetConverter` 本身是每次新建（注意：`TypeDescriptor` 内部自带全局缓存，不算很重，但仍走 IDictionary lookup）。
- **问题**：`TypeDescriptor.GetConverter` 内部走 TypeDescriptor 框架链路；对 `int/float/string` 这种核心类型完全可以用 `Func<string, T>` 直接 parse。
- **替换方案**：**B. 缓存委托**——`static ConcurrentDictionary<Type, Func<string, object>>` 在第一次时构造 `t => int.Parse((string)x)`、`t => float.Parse((string)x, CultureInfo.InvariantCulture)`、`t => x` 等。或者**A. 直接重构**——为常用类型走 hardcode switch。
- **风险/成本**：低。

### 11. `ParserUtils.GetDataArray<T>()` —— 同样反射 TypeDescriptor
- **位置**：`OngekiFumenEditor/Parser/ParserUtils.cs:16-25`
- **反射 API**：`TypeDescriptor.GetConverter(typeof(T))`、`converter.IsValid(x)`、`converter.ConvertFromString(x)`。
- **调用频次**：解析旧版/某些自定义命令时；每条命令各行调用一次。
- **是否已缓存**：方法局部变量，单次调用作用域内已抽出，**但跨调用未缓存**。
- **问题**：与 #10 同。
- **替换方案**：**B. 缓存委托**——`static ConcurrentDictionary<Type, Func<string, object>>`。
- **风险/成本**：低。

### 12. `ViewHelper.CreateView()` —— 视图模型查找视图
- **位置**：`OngekiFumenEditor/Utils/ViewHelper.cs:19-25`
- **反射 API**：`viewModel.GetType().GetCustomAttribute<MapToViewAttribute>()` + `CacheLambdaActivator.CreateInstance(viewType)`。
- **调用频次**：每次视图加载（窗口/对话框/工具栏）。
- **是否已缓存**：`CacheLambdaActivator` 是缓存的，但 `GetCustomAttribute<MapToViewAttribute>` 没缓存。
- **问题**：`MapToViewAttribute` 在 ViewModel Type 上，单 Type 一次即可。
- **替换方案**：**B. 缓存委托**——`static ConcurrentDictionary<Type, Type?> viewTypeMap`，第一次反射查找 attr，之后直接拿 `viewType`。
- **风险/成本**：低。

### 13. `FumenVisualEditorColorSettingViewModel` —— 启动反射 Color 属性
- **位置**：`OngekiFumenEditor/Kernel/SettingPages/FumenVisualEditor/ViewModels/FumenVisualEditorColorSettingViewModel.cs:20-24`
- **反射 API**：`typeof(EditorGlobalSetting).GetProperties()` + `Where(x => x.Name.StartsWith("Color") && x.PropertyType == typeof(System.Drawing.Color))`。
- **调用频次**：构造一次；构造方 `[PartCreationPolicy(CreationPolicy.Shared)]` —— 程序一生只一次。
- **是否已缓存**：N/A，启动一次性。
- **问题**：单次反射，可忽略。
- **替换方案**：**F. 不值得改**。
- **风险/成本**：F。

### 14. `EditorLayoutManager.TryGetDependices()` —— 私有字段反射
- **位置**：`OngekiFumenEditor/Kernel/EditorLayout/EditorLayoutManager.cs:34-38`
- **反射 API**：`shell.GetType().GetField("_shellView", BindingFlags.NonPublic | BindingFlags.Instance)` + `fieldInfo.GetValue(shell)`。
- **调用频次**：用户每次「加载/保存布局」按钮（少）。
- **是否已缓存**：否。
- **问题**：访问 Gemini 框架的内部字段；反射可避免修改第三方库；调用频次低。
- **替换方案**：
  - **B. 缓存委托**：把 `FieldInfo` 与 `Func<IShell, IShellView>` 缓存到 static。
  - **E. 框架限制**：Gemini 不提供公开 API，这是合理做法。
- **风险/成本**：F 或低。

### 15. `DefaultEditorScriptExecutor` —— 脚本入口反射查找 + `CreateDelegate`
- **位置**：`OngekiFumenEditor/Modules/EditorScriptExecutor/Kernel/DefaultImpl/DefaultEditorScriptExecutor.cs:215-228,36-51`
- **反射 API**：
  - `assembly.GetType("namespace.type")` + `epType.GetMethod(ep.MetadataName)` + `epMethod.CreateDelegate(typeof(Func<object[], Task<object>>))`
  - 构造时 `AppDomain.CurrentDomain.GetAssemblies()` + `TryGetRawMetadata` 给 Roslyn
- **调用频次**：脚本执行一次/编译一次。
- **是否已缓存**：N/A，按需。
- **问题**：脚本编译路径，反射 + 委托缓存已是 Roslyn 标准用法。
- **替换方案**：**E. 框架限制 / F. 不值得改**。
- **风险/成本**：F。

### 16. `RenderPerfomenceMeasurePanelViewModel` —— Monitor 选择 `IoC.GetAll<IPerfomenceMonitor>().First(x => x.GetType() == MonitorType)`
- **位置**：`OngekiFumenEditor/Kernel/Graphics/Performence/ViewModels/RenderPerfomenceMeasurePanelViewModel.cs:39-45,197`
- **反射 API**：用 `GetType()` 作为身份比较。
- **调用频次**：性能面板打开时一次列出；选择条目时一次。
- **是否已缓存**：N/A。
- **问题**：`GetType()` 本身很廉价；用 Type 做 Dictionary key 没问题。
- **替换方案**：**F. 不值得改**。
- **风险/成本**：F。

### 17. `DefaultDebugPerfomenceMonitor.OnBeginDrawCommand` —— 每帧每命令 `command.GetType()`
- **位置**：`OngekiFumenEditor/Kernel/Graphics/Performence/DefaultDebugPerfomenceMonitor.cs:232-251,258,266`
- **反射 API**：`command.GetType()`（不是真正反射调用，但用作 Dict key）。
- **调用频次**：**真正的热路径**——渲染循环每帧 N 条 DrawCommand，每条命令都 `Begin/End` 各一次。
- **是否已缓存**：使用 `Dictionary<Type, …>` 缓存了 PerformanceData，但 `GetType()` 调用本身每次都执行。
- **问题**：`GetType()` 在常规 class 上是单次 vtable 访问，~ns 级，几乎免费。不算反射重。
- **替换方案**：**F. 不值得改**；唯一可优化的是这是 Debug Monitor，本身可以在 Release 关闭。
- **风险/成本**：F。

### 18. `EnumSpecificationOption` 构造 —— `Enum.GetValues(enumType)`
- **位置**：`OngekiFumenEditor/Modules/FumenEditorSelectingObjectViewer/Base/SelectionFilter/SelectionFilterOptions.cs:238,269,295,579`
- **反射 API**：`Enum.GetValues(Type)`、`Enum.GetValues<T>()`。
- **调用频次**：选择过滤器初始化时（每个枚举类型 1-2 次）。
- **是否已缓存**：否。
- **问题**：单次少量，开销可忽略；.NET 7+ 的 `Enum.GetValues<T>()` 已是无装箱直接返回值。
- **替换方案**：**F. 不值得改**。
- **风险/成本**：F。

### 19. `ConnectableStartObject.InterpolateCurve(Type, Type, Type, factory)` —— `LambdaActivator.CreateInstance` 无缓存版
- **位置**：`OngekiFumenEditor/Base/OngekiObjects/ConnectableObject/ConnectableStartObject.cs:435-440`
- **反射 API**：`LambdaActivator.CreateInstance(startType)` × 每段曲线、`LambdaActivator.CreateInstance(nextType)` × 每点。
- **调用频次**：曲线插值生成（如 SvgPrefab 大量子点 / Beam 插值）；几百到几千次。
- **是否已缓存**：否（走 #8 的非缓存路径）。
- **问题**：是真正的热路径反射，影响 SvgPrefab 导入和 InterpolateCurve 响应。
- **替换方案**：**B. 缓存委托**——同 #8；或者**A. 直接重构**——再加一个 `InterpolateCurve(Func<ConnectableStartObject> startFactory, Func<ConnectableChildObjectBase> nextFactory, …)` 重载（其实已经有），让调用方提前 `CacheLambdaActivator.GetActivator(type)` 取委托后传入。
- **风险/成本**：低。

### 20. `SvgPrefabOperationViewModel.Generate` 内部 `LambdaActivator.CreateInstance(targetObject.GetType())`
- **位置**：`OngekiFumenEditor/Modules/EditorSvgObjectControlProvider/ViewModels/ObjectProperty/Operation/SvgPrefabOperationViewModel.cs:96`
- **反射 API**：`LambdaActivator.CreateInstance(targetObject.GetType())`。
- **调用频次**：SvgPrefab 导入每个子曲线 1 次（外加 #19 的进一步反射）。
- **是否已缓存**：否。
- **问题**：与 #19 同源，但该处可以直接换成 `CacheLambdaActivator.CreateInstance(...)`（无参 ctor 就行）。
- **替换方案**：**A. 直接重构**——把 `LambdaActivator.CreateInstance` 换成 `CacheLambdaActivator.CreateInstance`，立刻命中缓存。
- **风险/成本**：极低（一行）。

### 21. `BatchModeInputSubmode<T>.GenerateObject()` —— `Activator.CreateInstance<T>()`
- **位置**：`OngekiFumenEditor/Modules/FumenVisualEditor/Behaviors/BatchMode/BatchModeSubmode.cs:91-95`
- **反射 API**：`Activator.CreateInstance<T>()`（注：`T : OngekiTimelineObjectBase, new()`）。
- **调用频次**：用户按下批量放置键时；按住可能每 frame 触发。
- **是否已缓存**：泛型 `Activator.CreateInstance<T>()` 在 .NET 9 已被 JIT 特化为 `new T()`，不是真反射。
- **问题**：无（编译器/JIT 优化）。
- **替换方案**：**F. 不值得改**；若有洁癖可加 `where T : new()` 后写 `return new T();`。
- **风险/成本**：F。

### 22. `OngekiFumenSet.GetPathValue<T>()` —— XPath 解析后 `TypeDescriptor.GetConverter`
- **位置**：`OngekiFumenEditor/Modules/OgkiFumenListBrowser/Models/OngekiFumenSet.cs:77-88`
- **反射 API**：`TypeDescriptor.GetConverter(typeof(T)).ConvertFromString(strValue)`。
- **调用频次**：打开「谱面浏览」目录时遍历每个 Music.xml。
- **是否已缓存**：否。
- **问题**：与 #10/#11 同源，但调用频次低。
- **替换方案**：**B. 缓存委托** 或 **F. 不值得改**。
- **风险/成本**：F-低。

---

## 框架强制的反射点（备注，无须修改）

### 1. Caliburn.Micro IoC（`IoC.Get`/`IoC.GetAll`/`PropertyChangedBase.NotifyOfPropertyChange(() => xxx)`）
- 大量出现于全项目几乎每个 ViewModel。
- 由 Caliburn 框架内部使用 Expression Tree 解析 lambda，无法替换。
- **结论：E. 框架限制**。

### 2. MEF Compose（`[Export]/[Import]/[ImportingConstructor]/DirectoryCatalog`）
- `AppBootstrapper.BindServices()`、所有 `[Export(typeof(...))]` 注解类。
- MEF 完全基于反射。
- **结论：E. 框架限制**。如有需要可改为 `Microsoft.Extensions.DependencyInjection` + 源生成器注册，但属于全局重构，超出本评估范围。

### 3. WPF DataBinding（XAML 中 `{Binding XXX}`、`DependencyProperty.Register`）
- 所有 `.xaml` 文件、`PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(...))`。
- WPF Binding 引擎用反射读 PropertyDescriptor，是平台特性。
- **结论：E. 框架限制**。

### 4. ApplicationSettingsBase.Default / .Save() / .Upgrade()
- `App.xaml.cs CheckOrUpgradeAllSettings` 与各 Settings 自动生成代码。
- .NET Framework 设置系统强依赖反射；可改 `Microsoft.Extensions.Configuration` 但重构成本高。
- **结论：E. 框架限制**。

### 5. Costura.Fody `App.OnAssemblyResolve` 反射相关
- `App.xaml.cs:39-188`：解析嵌入资源 satellite assembly。
- 仅在加载时一次性走 `Assembly.Load`，无法用其他方式替换。
- **结论：E. 框架限制**。

### 6. Roslyn 脚本 / `Assembly.TryGetRawMetadata` / `AppDomain.GetAssemblies`
- 脚本执行器 `DefaultEditorScriptExecutor` 与 `DefaultDocumentContext`。
- Roslyn 集成本质需要这些操作。
- **结论：E. 框架限制**。

---

## 推荐的系统性改进

### A. 引入 Source Generator 的最优落点
1. **`ObjectPropertyBrowser` 属性元数据**（覆盖 #1、#2、#3、#4）
   - 定义 `[GeneratePropertyBrowserMetadata]` 标记类（或扫描所有 `OngekiObjectBase` 子类）。
   - 生成 `partial` 静态：`public static PropertyMetadata[] GetBrowserProperties()` 返回 `{ PropertyName, AliasResourceKey, TipResourceKey, IsReadOnly, IsAllowSetNull, SingleOnly, Getter: Func<TObj, object>, Setter: Action<TObj, object> }`。
   - 收益：完全消除 `MultiObjectsPropertyInfoWrapper`/`PropertyInfoWrapper`/`FumenObjectPropertyBrowserViewModel`/`ObjectInspectorViewModel` 的运行时反射。
   - 工作量：中等（要支持继承属性合并 + 资源键查找）。

2. **`OptionBindingAttribute` 命令行注册**（覆盖 #5）
   - 为 `[OptionBinding]` 标记的属性，生成 `static Option[] CreateOptionsFor<T>()` 与 `static void ApplyTo<T>(T obj, ParseResult r)`，消除 `Expression.Compile` + `MakeGenericType`。

### B. 缓存委托落点
1. `LambdaActivator.CreateInstance(Type, args)`：把所有路径都加缓存（`ConcurrentDictionary<(Type, Type[]), Delegate>`）。修一处，受益 #5、#8、#19、#20。
2. `TypeDescriptor.GetConverter(...)`：常用类型 `int/long/float/double/string/bool/enum` 直接 hard-code parser，避免逐次走 TypeDescriptor。修一处，受益 #10、#11、#22。
3. `ViewHelper.CreateView`：缓存 `Type → ViewType`，#12。

### C. 一次性注册（启动阶段反射缓存）
1. `App.xaml.cs CheckOrUpgradeAllSettings`：把"哪些 SettingsBase 子类需要升级"改为显式数组（#6）。
2. `EditorLayoutManager._shellView`：缓存 FieldInfo 到 static（#14）。

### D. 把 `Activator.CreateInstance(Type)` 改成 `new` 工厂表
当前：
- `MultiObjectsPropertyInfoWrapper.cs:119` 创建值类型默认 ——> `RuntimeHelpers.GetUninitializedObject(propertyInfo.PropertyType)` 或预先缓存。
- 其他 `Activator.CreateInstance<T>()` 调用 JIT 已优化，无须改。

### E. 小工程性修复
- 把 `MultiObjectsPropertyInfoWrapper.cacheComparerMap` 从实例字段提升为 `static ConcurrentDictionary<Type, IEqualityComparer>`（#1）—— **一行修复，立刻见效**。
- `OngekiObjectBase.CopyNew()` 若极致追求性能，再加抽象 `protected abstract OngekiObjectBase NewInstance()`；但建议保持现状。

---

## 结论

整个项目的反射使用呈现以下分布：

1. **被 Caliburn.Micro / MEF / WPF 三件套强制的反射** —— 占据绝大多数 `GetType()`/`PropertyInfo` 出现位置，**无须也无法修改**。
2. **`OngekiFumenEditor/Utils/` 下的 `ReflectionHelp` 与 `LambdaActivator`** —— 已是合理的"反射 → 委托"封装层；其中 `CacheLambdaActivator`（无参 ctor）已缓存，**仅 `LambdaActivator.CreateInstance(Type, params)` 路径未缓存**，这是最简单可摘的低垂果实（修一处受益多处）。
3. **属性面板（FumenObjectPropertyBrowser + ObjectInspector）** —— **真正问题最大**的反射密集区，且 `MultiObjectsPropertyInfoWrapper.cacheComparerMap` 实例字段是 BUG 级别的缓存失效。建议优先：(i) 立刻把该字段改 static；(ii) 中期引入"per-Type 属性元数据快照"缓存（启动期或惰性收集）；(iii) 长期可上 Source Generator。
4. **谱面解析器 `CommandArgs.GetDataArray<T>`** —— `TypeDescriptor.GetConverter` 每行命中，虽然 `TypeDescriptor` 内部已有缓存，但仍可通过常用类型 hard-code 进一步提速。
5. **CLI `DefaultCommandExecutor.GenerateOptionsByAttributes`** —— Expression.Compile 用得过重，可大幅简化（仅启动一次执行，但移除后启动会更快、binary 体积更小）。
6. **`Base/OngekiObjectBase.CopyNew()` + `ConnectableStartObject.InterpolateCurve(Type,…)`** —— 已经走缓存路径，但后者错用了非缓存版本。把 `LambdaActivator.CreateInstance` 换成 `CacheLambdaActivator.CreateInstance` 即可立刻优化。

按"性价比"建议改造顺序：

| 优先级 | 改造点 | 难度 | 收益 |
|---|---|---|---|
| **P0** | #1 `MultiObjectsPropertyInfoWrapper.cacheComparerMap` 改 static | 极低 | 高 |
| **P0** | #20 `SvgPrefabOperationViewModel` 改用 `CacheLambdaActivator` | 极低 | 中 |
| **P1** | #8/#19 `LambdaActivator.CreateInstance(Type,…)` 加缓存 | 低 | 中 |
| **P1** | #10/#11 `CommandArgs` / `ParserUtils` 缓存 Converter 委托 | 低 | 中 |
| **P1** | #3 `PropertyInfoWrapper` 把 attribute 查询和 getter/setter 缓存到字段 | 低-中 | 高 |
| **P2** | #2/#4 属性面板 per-Type 元数据快照 | 中 | 高 |
| **P2** | #5 `DefaultCommandExecutor` 移除 `Expression.Compile` | 中 | 中（仅启动） |
| **P3** | Source Generator 替换 `ObjectPropertyBrowser*` 反射 | 高 | 极高（长期） |
| **P3** | `App.xaml.cs CheckOrUpgradeAllSettings` 显式枚举 SettingsBase | 低 | 低（仅启动） |

**总体评价**：项目反射使用整体克制，作者已经主动通过 `CacheLambdaActivator` 抽象掉了 ctor 反射；目前最大的可优化空间集中在"属性面板路径"。修复 P0/P1 一共约 8 处，工作量小于 1 人日，预计可减少属性面板切换/SvgPrefab 导入路径的反射调用 70-90%。
