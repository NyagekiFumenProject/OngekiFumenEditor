# Injectio + MSDI 替换 MEF / IoC 迁移讨论

> 文档性质：实时讨论与迁移计划草案  
> 建立日期：2026-07-28  
> 最后更新：2026-07-28（第 11 轮）  
> 当前状态：讨论中，尚未授权实施迁移

## 1. 目标与边界

计划使用 [MikiraSora/Injectio](https://github.com/MikiraSora/Injectio) 与
`Microsoft.Extensions.DependencyInjection`（下文简称 MSDI）替换本项目的：

1. `System.ComponentModel.Composition` / MEF 组合容器；
2. `[Export]`、`[Import]`、`[ImportMany]`、`[ImportingConstructor]`、
   `[PartCreationPolicy]` 等注册与注入方式；
3. 应用代码对 Caliburn.Micro 静态 `IoC.Get<T>()` / `IoC.GetAll<T>()` 的直接依赖；
4. Gemini 启动器中基于目录、程序集和 MEF catalog 的组合流程。

本次讨论先确定目标架构、兼容边界、迁移顺序和验收口径。迁移代码尚未开始修改。
迁移旧代码时保留现有注释，不借机删除业务注释。

## 2. 已核实的仓库现状

### 2.1 实际组合入口

- `OngekiFumenEditor/AppBootstrapper.cs` 继承 `Gemini.AppBootstrapper`。
- `Gemini.AppBootstrapper.Configure()` 会递归枚举运行目录下的 DLL，构造
  `AssemblyCatalog`、`AggregateCatalog`、`CatalogExportProvider` 和
  `CompositionContainer`。
- 主程序通过 `SelectAssemblies()` 把主程序、Gemini、Output、解析器等程序集列为
  priority assemblies。MEF 单值解析优先取这些程序集中的导出。
- Gemini 把 `IWindowManager`、`IEventAggregator`、MEF 容器和 bootstrapper 实例作为
  exported value 写入容器。
- Caliburn.Micro 的 `IoC.GetInstance`、`IoC.GetAllInstances`、`IoC.BuildUp` 最终分别
  委托给 MEF 的单值解析、多值解析和 `SatisfyImportsOnce()`。

这意味着 MEF 当前同时承担四项职责：服务容器、扩展点集合、UI 定义注册表、
Caliburn 对象补注入机制。

### 2.2 动态插件是现有公开能力

- 主程序启动时扫描可执行文件旁的 `Plugins/*` 子目录，并用 `DirectoryCatalog`
  将其中的 MEF parts 加入容器。
- 名称以 `OngekiFumenEditorPlugins.` 开头的插件程序集还会加入
  Caliburn `AssemblySource`，用于视图定位等行为。
- `Readme.md` 明确写着“插件机制依赖于 MEF 框架”。
- Wiki 的插件教程明确描述了“把插件包文件夹放入 `Plugins` 目录即可加载”，并把
  `IoC.Get()` / `IoC.GetAll()` 作为插件 API。
- 仓库元数据还出现了 `OngekiFumenEditorPlugins.KngkSupport`，说明该机制至少曾被
  外部插件实际使用，而不是只存在于启动代码中。

### 2.3 MEF 与 IoC 规模（实际主程序构建闭包）

统计范围为主程序、`Gemini` 和当前引用的 `Gemini.Modules.Output`。数字来自源码词法
扫描，用于估算迁移面；其中 `[Export]` 搜索结果有 2 处位于注释，最终实施时应由
编译器或 Roslyn 清单再次校准。

| 项目 | `[Export]` 命中 | `[Import]` | `[ImportMany]` | `[ImportingConstructor]` | `IoC.*` 调用 |
| --- | ---: | ---: | ---: | ---: | ---: |
| OngekiFumenEditor | 418 | 4 | 7 | 52 | 242 |
| Gemini | 73 | 14 | 21 | 19 | 29 |
| Gemini.Modules.Output | 2 | 0 | 0 | 1 | 0 |
| 合计 | 493 | 18 | 28 | 72 | 271 |

补充情况：

- 约 491 处是未被整行注释的直接 `[Export]`。
- `[CommandDefinition]` 还有 90 处、`[CommandHandler]` 还有 86 处；这两个自定义
  attribute 本身继承 MEF `ExportAttribute`，因此也是 MEF 导出，但不计入上表的
  `[Export]` 数量。
- 发现 183 个由 `[Export]` 修饰的静态 UI/配置定义值：63 个菜单项、28 个菜单组、
  28 个命令快捷键、24 个工具栏项、18 个按键绑定、11 个菜单、5 个工具栏组、
  4 个工具栏，以及少量根定义/排除定义。
- 显式 lifetime 标注为 30 个 `Shared`、6 个 `NonShared`。其余导出依赖 MEF 默认
  creation policy，不能机械地全部改成 transient。
- 至少 11 个文件仍使用成员/字段 `[Import]`，另有成员形式的 `[ImportMany]`。
  MSDI 默认只做构造函数注入，必须改造这些对象的创建路径。
- Caliburn `IoC` 的 271 处调用分布在 121 个左右的文件中。高密度区域包括编辑器主
  ViewModel、启动器、文档打开辅助类、音频工具、脚本编辑器和各类命令处理器。

### 2.4 多实现扩展点不是少数特例

现有导出数量较多的 contract 包括：

| Contract | 当前直接 `[Export(typeof(...))]` 数量 |
| --- | ---: |
| `ICommandParser` | 71 |
| `INyagekiCommandParser` | 38 |
| `IFumenEditorDrawingTarget` | 26 |
| `IFumenCheckRule` | 15 |
| `ITypeUIGenerator` | 11 |
| `ISettingsEditor` | 7 |
| `IArgValueConverter` | 6 |
| `IOngekiObjectOperationGenerator` | 6 |

Gemini 还用 `[ImportMany]` 注入命令定义/处理器、菜单、工具栏、主题、编辑器提供器等。
MSDI 的 `IEnumerable<T>` 可以表达这些集合，但现有的 `T[]`、`List<T>` 成员注入需要
改成 `IEnumerable<T>` 构造参数，并在对象内部物化。

## 3. Injectio 能力与边界

根据 Injectio 仓库当前 README，已确认它可以生成：

- Singleton、Scoped、Transient 注册；
- 指定 service type、implementation type 和工厂方法的注册；
- `Self`、`ImplementedInterfaces`、`SelfWithInterfaces` 注册策略；
- `Skip`、`Replace`、`Append` 重复注册策略；
- 可通过 `Append` 配合 MSDI 的 `IEnumerable<T>` 实现多注册；
- open generic、MSDI 8+ keyed service、tags；
- 用 `[RegisterServices]` 调用接收 `IServiceCollection` 的自定义注册模块；
- 每个编译程序集生成一个 `Add[AssemblyName]()` 扩展方法，名称可通过
  `InjectioName` 配置。

由其“源码生成 + 编译期发现”的模型，以及 README 已公开的能力，可以确定以下内容
不能假定为自动等价迁移：

1. 它不会替宿主在运行时发现编译时未知的 `Plugins/*` DLL；
2. 注册 attribute 面向 class，不能直接复刻 MEF 对静态字段/属性值的 `[Export]`；
3. 它不提供 MEF 的成员补注入 / `SatisfyImportsOnce()` 语义；
4. 它不会自动复刻 Gemini 的 priority assembly 覆盖顺序；
5. Injectio 负责生成 `IServiceCollection` 注册代码，实例创建、集合解析、作用域和释放
   仍遵循 MSDI 规则。

因此，Injectio 应定位为“已知类型的注册代码生成器”，MSDI 是唯一运行时容器；菜单、
工具栏、插件和 Caliburn 兼容层需要在项目内明确设计。

## 4. 推荐目标架构（待决策确认）

### 4.1 单一根容器

应用启动阶段只构造一个根 `ServiceCollection` / `ServiceProvider`：

1. 调用 Gemini 项目生成的注册扩展；
2. 调用 Output 模块生成的注册扩展；
3. 调用 OngekiFumenEditor 生成的注册扩展；
4. 添加必须由现成实例或工厂产生的框架服务；
5. 在开发/测试构建启用 `ValidateOnBuild` 和 `ValidateScopes`；
6. 由根 provider 创建主窗口和启动服务，并在应用退出时统一释放 provider。

各程序集拥有自己的注册入口，宿主只编排模块，不再扫描目录中的所有 DLL。

### 4.2 类型服务与多实现

- 原 `[Export(typeof(IFoo))]` 的 shared part 优先映射为
  `[RegisterSingleton<IFoo>]`。
- 原 `NonShared` part 优先映射为 `[RegisterTransient<IFoo>]`。
- 同一 contract 的多实现使用 `DuplicateStrategy.Append`，消费者使用
  `IEnumerable<T>`。
- 一个实现导出为多个 contract 时，必须保证多个 contract 解析到同一个 singleton
  实例，不能简单生成多个彼此独立的 singleton。实现时需检查 Injectio 生成结果；
  必要时用 `[RegisterServices]` 显式写别名工厂。
- `Scoped` 暂不作为桌面应用默认 lifetime。只有出现明确的文档会话、编辑会话或命令
  执行 scope 后才引入，避免把 scoped service 从根容器解析成事实 singleton。

### 4.3 菜单、工具栏、快捷键和按键绑定

推荐保留现有静态定义对象及其引用关系，但取消字段上的 `[Export]`，按模块增加
`[RegisterServices]` 注册方法，把现有实例显式追加到 `IServiceCollection`：

```csharp
services.AddSingleton<MenuDefinition>(MenuDefinitions.FileMenu);
services.AddSingleton<MenuItemDefinition>(MenuDefinitions.OpenMenuItem);
```

这样可以保持父子定义之间基于对象引用的关系，也能继续由
`IEnumerable<MenuDefinition>`、`IEnumerable<MenuItemDefinition>` 等注入 Gemini builder。
不建议为了迁移把每个静态定义包装成无意义的 service class。

为避免 183 个值长期靠人工维护，实施阶段应生成“注册清单校验”：至少断言每种定义的
数量、唯一键、父引用和排序结果；是否进一步扩展 Injectio 支持静态成员注册，留作后续
优化，不作为首轮迁移前提。

### 4.4 Caliburn 兼容与去除 Service Locator

迁移切换点可以暂时把 Caliburn 的三个静态委托接到 MSDI：

- `GetInstance` -> `IServiceProvider.GetRequiredService(type)`；
- `GetAllInstances` -> 从 provider 解析 `IEnumerable<T>` 的非泛型适配；
- `BuildUp` -> 仅作为短期兼容入口，不模拟通用属性注入。

这只是 Caliburn 框架边界的兼容层，不是允许业务代码继续新增 `IoC.Get`。切换后应按
模块逐步把 271 处直接调用迁移到：

- 构造函数依赖；
- `Func<T>` / 明确的业务 factory（确实需要延迟创建时）；
- 方法参数（依赖只属于一次操作时）；
- `ActivatorUtilities` 或显式 factory（框架必须创建对象时）。

所有字段/属性 `[Import]` 和依赖 `BuildUp` 的 result/module 对象，应在切换根容器前改成
构造函数或显式工厂创建。最终业务项目不直接调用 Caliburn `IoC`，也不把
`IServiceProvider` 扩散到普通 ViewModel/业务服务中。

### 4.5 覆盖与顺序

MEF 当前的 priority assembly 机制允许应用覆盖 Gemini 导出，命令路由还依赖“优先
程序集的 handler 最后写入字典”。MSDI 中必须显式表达：

- 单服务覆盖使用清晰的 `Replace` 或宿主最后注册；
- 多服务追加使用 `Append`；
- 命令 handler 冲突不能依赖偶然的源码生成顺序，应由稳定的 priority/order 数据决定；
- 所有使用 `ToDictionary()` 的多实现集合都需要启动期重复键诊断。

## 5. 推荐迁移顺序（草案）

### 阶段 0：锁定行为基线

- 生成 MEF 导出/导入清单，按 contract、实现、lifetime、所在程序集分类。
- 记录菜单、工具栏、快捷键、命令、解析器、绘制目标、检查规则等集合数量与顺序。
- 增加 GUI 启动、CLI 启动、打开谱面、命令路由、设置页、窗口关闭释放等 smoke tests。
- 单独确认插件部署模型；这是后续架构的前置决策。

### 阶段 1：引入 Injectio/MSDI，但不切换运行时容器

- 在三个实际参与组合的项目中引入 Injectio，并使用 `PrivateAssets="all"`。
- 保留 MEF attributes 的同时添加 Injectio 注册 attribute / 注册模块。
- 构建一个仅用于验证的 MSDI provider，检查缺失依赖、重复单值和 lifetime；程序运行仍
  使用 MEF，避免两个容器同时创建业务 singleton。
- 将数组、多值成员注入逐步改成 MSDI 可解析的 `IEnumerable<T>` 构造注入；这类构造
  在过渡期仍可由 MEF 使用。

### 阶段 2：补齐非类型导出和框架创建路径

- 为 183 个静态定义建立模块化 `RegisterServices` 清单。
- 把字段/属性 `[Import]` 改成构造注入。
- 把依赖 `BuildUp` 的 coroutine result、module、窗口/ViewModel 创建路径改成显式 factory
  或 `ActivatorUtilities`。
- 显式实现 priority/override 规则，并为所有多实现集合添加重复键与排序测试。

### 阶段 3：原子切换根容器

- Gemini bootstrapper 改为创建并持有 MSDI provider。
- Caliburn 静态 IoC 委托暂时桥接到 MSDI，以保持 Caliburn 内部调用和尚未迁移的业务
  调用可运行。
- 一次性停用 MEF catalog/container 组合，避免同一类型被两个容器各创建一份。
- 完成 GUI、CLI 和资源释放回归后，再进入清理阶段。

### 阶段 4：去除 MEF

- 删除剩余 `[Export]` / `[Import*]` / `[PartCreationPolicy]` 和 MEF 自定义导出
  attribute 继承关系。
- 删除 `System.ComponentModel.Composition` package 和相关 using。
- 删除 `CompositionContainer`、catalog、composition batch、递归 DLL MEF 扫描代码。
- 更新 Readme、Wiki 和插件开发文档。

### 阶段 5：去除业务代码中的 Caliburn IoC

- 按调用密度从核心 ViewModel、文档打开、启动器、音频、命令处理器向外迁移。
- 每完成一个模块，禁止该模块重新引入 `IoC.Get`；用 analyzer/CI 搜索守住边界。
- 最终只允许 Caliburn 兼容层内部保留静态委托；如果 Caliburn 已无此要求，则连兼容层
  一并删除。

### 阶段 6：发布与兼容验证

- 验证 Debug/Release、GUI/CLI、single-file、trim/AOT 分析配置。
- 验证容器释放时 `IDisposable` / `IAsyncDisposable` 服务均被释放。
- 若保留动态插件，分别验证加载失败隔离、版本契约、依赖冲突、卸载能力和发布限制。

说明：去除 MEF/反射扫描会消除一组明显的 trimming/NativeAOT 障碍，但不会单独使当前
WPF 应用整体变为 NativeAOT 兼容；两件事不能作为同一个完成条件。

## 6. 当前风险清单

| 风险 | 影响 | 当前建议 |
| --- | --- | --- |
| 未知 DLL 插件与编译期注册矛盾 | 已确认不能并入根容器；是否另建 JIT 宿主仍待定 | 官方扩展改为编译期模块，动态宿主单独决策 |
| 183 个静态定义无法直接 attribute 注册 | 菜单/工具栏/快捷键可能缺项 | 显式模块注册 + 数量/关系测试 |
| 默认 lifetime 不能机械推断 | 状态丢失、重复后台任务或泄漏 | 先建立 lifetime 清单，保守保持现状 |
| 同一实现导出多个 contract | 可能生成多个 singleton 实例 | 用别名工厂并验证引用相等 |
| `T[]` / 成员 `[ImportMany]` | MSDI 无法直接按现状构造 | 改为 `IEnumerable<T>` 构造注入 |
| priority assembly 隐式覆盖 | 命令/服务选择可能改变 | 明确 Replace/Append/priority 规则 |
| 271 处静态 IoC 调用 | 大爆炸式改造风险高 | 容器原子切换，调用点分模块清理 |
| 双容器过渡 | singleton 重复、循环依赖难诊断 | 镜像注册但运行时只激活一个容器 |

## 7. 决策记录

| 编号 | 决策 | 状态 | 结论 | 影响 |
| --- | --- | --- | --- | --- |
| D-001 | 迁移后是否保留“把未知第三方 DLL 放入 `Plugins` 即可加载并并入根容器” | **已确认** | 不保留；官方/内置扩展改为编译期 Injectio 模块 | 根容器可以保持静态闭包；旧 MEF 插件加载 API 不再兼容 |
| D-002 | 本次迁移是否同时交付隔离的 JIT 动态插件宿主 | **已确认** | 不交付；取消全部运行时第三方 DLL 插件，只保留编译期模块 | 本次范围不含 AssemblyLoadContext、动态插件 ABI、卸载与隔离机制 |
| D-003 | 容器切换后是否允许 Caliburn IoC -> MSDI 兼容桥 | **已确认** | 允许；provider 由 `App` 静态暴露，主程序适配层间接调用 | 可以先切换容器再迁移调用点；兼容桥的最终保留范围仍待后续确认 |
| D-004 | 用户所称 `ISourceProvider` 是否指 MSDI 使用的标准 provider 接口 | **已确认** | 使用 `System.IServiceProvider`；属性名保留 `App.SourceProvider` | 不新建同义接口；MSDI 扩展方法由对应 namespace 提供 |
| D-005 | 首轮迁移是否引入 MSDI scope | **已确认** | 不引入；默认/Shared -> Singleton，NonShared -> Transient | 首轮保持 MEF lifetime 行为，文档/编辑会话 scope 留待独立重构 |
| D-006 | 183 个静态 UI 定义如何注册到 MSDI | **已确认** | 按模块用 `[RegisterServices]` 显式注册现有实例，不扩展 Injectio | 保持定义对象引用关系；注册清单由模块拥有并接受基线测试 |
| D-007 | 如何替代 MEF priority assembly / 隐式覆盖顺序 | **已确认** | 单服务默认冲突即失败、覆盖显式 Replace；多服务 Append 后按业务键/priority 校验 | 服务选择不再依赖程序集、源码生成或模块调用的偶然顺序 |
| D-008 | IoC 兼容 facade 的最终保留范围 | **已确认** | Gemini 可以继续调用 IoC；直接调用清理当前只针对 OngekiFumenEditor | Gemini 不纳入 IoC 调用点清理范围，Ongeki 仍以构造注入/工厂为目标 |
| D-009 | “只修改 OngekiFumenEditor”是否也禁止为移除 MEF 而修改 Gemini 基础设施 | **已确认** | 允许最小 Gemini MEF 基础设施改造，但不清理其 IoC 调用 | 可以实现单一 MSDI 根容器并彻底移除 MEF，同时控制 Gemini 改动范围 |
| D-010 | 容器切换与回滚是否采用“验证期双注册、运行时原子切换、版本回滚” | **已确认** | 采用；禁止运行时双容器和长期 MEF 功能开关 | 每个可发布版本只有一个权威容器，故障通过版本回滚 |
| D-011 | 本次迁移的最终完成标准 | 等待用户确认 | 推荐以 MEF 清零、单 provider、Ongeki IoC 清零及行为基线通过为共同门槛 | 决定何时可删除过渡清单并宣布迁移完成 |

## 8. 待讨论决策树

以下问题按依赖顺序逐个讨论，不会一次要求全部回答：

1. ~~未知第三方 DLL 是否仍可自动并入根容器；~~ **已决定：不保留。**
2. ~~是否在本次迁移中另行提供隔离的运行时 JIT 插件宿主；~~ **已决定：不提供。**
3. ~~迁移期间是否接受 Caliburn IoC -> MSDI 兼容桥；~~ **已决定：允许。**
4. ~~静态 provider 使用标准 `IServiceProvider` 还是新建 `ISourceProvider`；~~ **已决定：使用标准接口。**
5. ~~MEF 默认 lifetime 的迁移原则，以及是否引入文档/编辑会话 scope；~~ **已决定：保守映射且不引入 scope。**
6. ~~静态 UI 定义采用显式注册模块，还是扩展 Injectio 生成器；~~ **已决定：按模块显式注册。**
7. ~~priority/override 和多实现重复键的正式规则；~~ **已决定：显式冲突、覆盖与 priority。**
8. ~~IoC 兼容 facade 的最终保留范围；~~ **已决定：Gemini 可继续调用，调用点清理仅针对 Ongeki。**
9. ~~是否允许为移除 MEF 而最小修改 Gemini 基础设施；~~ **已决定：允许最小改造。**
10. ~~切换方式与回滚策略；~~ **已决定：原子切换并以版本回滚。**
11. 最终验收标准；（当前问题）

## 9. 第 1 轮讨论

### 已形成的判断

- 不能把本项目的迁移理解为批量替换 attribute；MEF 还承载插件发现、静态 UI 定义、
  priority override 和成员补注入。
- 推荐最终只保留一个 MSDI provider，Injectio 只负责生成已知程序集的注册代码。
- 推荐先镜像注册并验证，再原子切换容器；不让 MEF 与 MSDI 同时创建业务 singleton。
- 首个必须由维护者决定的问题是动态插件的交付模型。

### 当前问题 D-001

迁移完成后，是否必须继续支持“用户把主程序编译时未知的第三方 DLL 文件夹放入
`Plugins`，重启后主程序自动发现并把其服务、命令、菜单、解析器等并入宿主”这一能力？

推荐答案：**不再把未知 DLL 并入主程序根容器**。将官方/常用扩展改为编译期引用的
Injectio 模块；如确实需要第三方运行时插件，则把它定义成单独的、显式契约的 JIT 插件
宿主能力，并明确它不属于 NativeAOT 发布路径。理由是运行时未知程序集发现与完整静态
NativeAOT 闭包在根本上冲突，而且直接把第三方服务并入根 provider 会带来版本、lifetime、
卸载和故障隔离问题。

用户在第 2 轮答复“同意”，D-001 已按推荐答案确认。

## 10. 第 2 轮讨论

### D-001 已确认结论

用户同意不再让编译时未知的第三方 DLL 自动并入主程序根容器。由此确定：

- 主程序根 `ServiceProvider` 只包含主程序编译时已知的模块；
- 官方/内置扩展通过项目引用、NuGet/源码依赖或其他编译期依赖进入构建，并调用各自
  的 Injectio 生成注册入口；
- 当前 `Plugins/*` + `DirectoryCatalog` + MEF parts 的加载协议不作为新根容器的兼容
  API；
- 主程序的 NativeAOT/trimming 分析路径不包含运行时未知插件；
- 如果仍要支持第三方动态 DLL，它必须是与根容器分离、边界显式的 JIT 插件宿主，
  不能恢复成“扫描后任意注入宿主服务”的模式。

该决定让根组合模型收敛为编译期静态闭包，但还留下一个独立的范围问题：本次迁移是否
需要顺便设计并实现 JIT 插件宿主。

### 当前问题 D-002

本次 Injectio/MSDI 迁移是否需要同时交付一个隔离的 JIT 动态插件宿主，用来继续加载
第三方 DLL；还是本次直接取消所有运行时第三方插件，只保留编译期模块？

推荐答案：**本次不交付 JIT 动态插件宿主，只保留编译期模块。** 原因是一个可靠的
动态宿主还需要单独设计版本化 contract、`AssemblyLoadContext` 隔离、依赖冲突、服务
白名单、UI 扩展桥、生命周期、失败隔离和卸载行为；它与“替换 MEF/IoC”是不同项目，
同时实施会显著扩大切换面。未来确有需求时，应以独立设计文档和独立发布路径重新引入，
而不是作为根 DI 容器的兼容分支。

用户在第 3 轮答复“同意”，D-002 已按推荐答案确认。

## 11. 第 3 轮讨论

### D-002 已确认结论

用户同意本次不实现隔离的 JIT 动态插件宿主，直接取消运行时第三方 DLL 插件，只保留
编译期 Injectio 模块。由此进一步确定：

- 删除 `Plugins/*` 目录扫描、`DirectoryCatalog` 和插件程序集动态加入
  `AssemblySource` 的启动代码；
- 不为旧 MEF 插件提供兼容加载器、桥接容器或双发布模式；
- 不在本次范围内设计 `AssemblyLoadContext`、插件 ABI、依赖隔离、热卸载或插件安全
  边界；
- 官方扩展必须成为主解决方案的项目引用、确定版本的包依赖，或其他会在编译时进入
  静态注册闭包的模块；
- 发布说明和插件教程需要明确这是破坏性变更，旧插件 DLL 必须迁移为编译期模块后才能
  使用；
- 后续若重新提出动态插件需求，应新建设计文档与独立 JIT 发布路径，不反向污染根
  MSDI 容器。

插件分支至此收敛，接下来需要确定如何处理仓库中约 271 处 Caliburn `IoC.*` 调用。

### 当前问题 D-003

将运行时容器从 MEF 原子切换到 MSDI 后，是否允许短期保留一个
“Caliburn 静态 IoC 委托 -> 根 `IServiceProvider`”兼容桥，让现有 `IoC.Get<T>()` /
`IoC.GetAll<T>()` 调用按模块逐步清理；还是必须先一次性消除全部 Service Locator 调用，
然后才能切换容器？

推荐答案：**允许严格受限的短期兼容桥，并分阶段清理。** 当前约 271 处调用横跨启动、
文档、编辑器、音频、命令和 Gemini 内部；若把“换容器”和“重写所有依赖获取路径”绑在
同一个切换点，回归定位会非常困难。兼容桥应遵守以下硬约束：

- 桥只解析 MSDI，不保留 MEF fallback，也不构造第二个 provider；
- provider 必须在 Caliburn 创建主窗口/视图模型前完成初始化；
- 从引入桥开始禁止新增业务 `IoC.*` 调用，并用 CI 基线计数只能下降；
- `BuildUp` 不实现通用属性注入，所有 `[Import]` 成员必须在切换前改成构造注入或工厂；
- 每个迁移阶段明确清理的模块和剩余调用数；
- 最终完成条件仍是业务代码零 `IoC.*` 调用，兼容桥不是目标架构。

用户在第 4 轮明确允许 IoC 间接调用 `App` 持有的静态 MSDI provider，D-003 已确认；
其最终是迁移期桥还是长期兼容 facade，留到调用点迁移策略一并确认。

## 12. 第 4 轮讨论

### D-003 已确认结论

用户指定允许现有 IoC 实现间接调用一个由 `App` 声明和初始化的静态 MSDI provider，
建议形态为：

```csharp
public static ISourceProvider SourceProvider { get; }
```

该方向与“单一 MSDI 根容器 + Caliburn 兼容入口”一致，但根据当前仓库和 .NET API 核对，
需要补充以下技术约束：

1. MSDI 的标准抽象是 `System.IServiceProvider`。仓库中不存在 `ISourceProvider`；当前
   `SourceProvider` 文字只出现在 MEF `ExportProvider.SourceProvider` 属性上，两者无关。
2. `Caliburn.Micro.Core` 和 Gemini 是主程序的下层项目，不能让其中的静态 `IoC` 类直接
   引用 `OngekiFumenEditor.App`，否则会造成错误的反向依赖甚至项目引用环。
3. 正确的间接路径应是：Caliburn `IoC` 调用其现有委托；主程序
   `OngekiFumenEditor.AppBootstrapper` 覆盖 `GetInstance` / `GetAllInstances`，或者在启动
   阶段设置这些委托；覆盖实现再读取 `App.SourceProvider`。
4. 当前启动顺序是 `new App()` -> `App.InitializeComponent()` -> XAML 创建
   `AppBootstrapper` -> bootstrapper 构造期间调用 `Configure()` -> 安装 Caliburn IoC
   委托 -> `App.Run()`。provider 必须在安装委托前初始化完成。
5. 为同时满足“初始化逻辑属于 App”和“需要注册当前 bootstrapper 实例”，推荐由
   `App` 提供内部初始化方法，`AppBootstrapper.Configure()` 把 `this` 传入并触发：

```csharp
private static IServiceProvider? sourceProvider;

public static IServiceProvider SourceProvider => sourceProvider
    ?? throw new InvalidOperationException("Service provider is not initialized.");

internal IServiceProvider InitializeSourceProvider(AppBootstrapper bootstrapper)
{
    // App 在这里建立 ServiceCollection、调用各 Injectio 模块并注册 bootstrapper 实例。
    // 构建成功后一次性赋给 sourceProvider；重复初始化应直接失败。
}
```

这样对外仍是 getter-only，不暴露公共 setter，并且未初始化访问会给出明确错误，而不是
依赖 `null!`。根 provider 只能构建一次，必须由 `App`/bootstrapper 在退出流程末尾统一
释放。

主程序适配层的概念路径为：

```text
业务旧调用 IoC.Get<T>()
  -> Caliburn IoC.GetInstance 委托
  -> OngekiFumenEditor.AppBootstrapper.GetInstance(...)
  -> App.SourceProvider.GetRequiredService(...)
```

空 key 使用普通 service 解析；非空 Caliburn key 若仍有调用，应显式映射到 MSDI keyed
service，而不是忽略 key。`GetAllInstances` 则映射到 MSDI 多服务集合。`BuildUp` 不借此
恢复属性注入。

### 当前问题 D-004

这里的 `ISourceProvider` 是否只是笔误，实际希望属性类型使用标准
`System.IServiceProvider`，同时保留属性名 `App.SourceProvider`？

推荐答案：**是，使用标准 `IServiceProvider`。** 不建议新增只为改名而存在的
`ISourceProvider`，否则所有 MSDI 扩展方法仍需解包到 `IServiceProvider`，增加一层没有
行为价值的抽象。

用户在第 5 轮确认希望使用 `Microsoft.Extensions.DependencyInjection` 体系中的
`IServiceProvider`。API 的实际接口全名为 `System.IServiceProvider`，D-004 按该可编译
签名确认。

## 13. 第 5 轮讨论

### D-004 已确认结论

用户确认静态属性承载 MSDI provider，不新建项目自定义的 `ISourceProvider`。准确的类型
与 namespace 关系是：

- provider 接口：`System.IServiceProvider`；
- DI 扩展方法 namespace：`Microsoft.Extensions.DependencyInjection`；
- MSDI 内置的具体实现类也名为
  `Microsoft.Extensions.DependencyInjection.ServiceProvider`，但 `App` 不应暴露具体
  实现类型。

因此计划中的公开签名为：

```csharp
public static System.IServiceProvider SourceProvider => sourceProvider
    ?? throw new InvalidOperationException("Service provider is not initialized.");
```

调用文件引入 `using Microsoft.Extensions.DependencyInjection;` 后，可对该标准接口调用
`GetRequiredService()`、`GetServices()`、`GetRequiredKeyedService()` 等扩展方法。属性继续
命名为用户指定的 `App.SourceProvider`。

### 当前问题 D-005

首轮迁移是否严格保持当前 MEF lifetime 语义：未显式标注和显式 `Shared` 的 part 均注册
为 MSDI Singleton，显式 `NonShared` 注册为 Transient，并且暂时完全不引入 Scoped；还是
要在本次迁移中同时建立“每个文档/编辑会话一个 scope”的新生命周期？

推荐答案：**首轮不引入 scope，按 Singleton/Transient 保守映射。** 当前代码没有统一的
文档 scope 创建与释放边界，贸然把服务改成 Scoped 容易从根 provider 解析 scoped service，
使其退化成隐蔽的全局实例，或者在窗口/文档关闭后继续存活。先保持 MEF 行为能把“容器
替换”和“状态生命周期重构”分开；以后确有文档级隔离需求，再围绕明确的
`IServiceScope` 所有者单独设计。

此映射仍需逐项审查同一实现导出多个 contract、后台任务、缓存和可释放资源，不能只靠
attribute 批量替换。

用户在第 6 轮答复“同意”，D-005 已按推荐答案确认。

## 14. 第 6 轮讨论

### D-005 已确认结论

用户同意首轮迁移不引入 MSDI scope，按以下规则保持当前 MEF lifetime：

| MEF 状态 | 首轮 MSDI lifetime | 说明 |
| --- | --- | --- |
| 未显式 `[PartCreationPolicy]` | Singleton | 保持当前默认共享行为 |
| `CreationPolicy.Shared` | Singleton | 30 处显式共享 part |
| `CreationPolicy.NonShared` | Transient | 6 处显式非共享 part |
| Scoped | 禁止 | 首轮没有统一 scope 所有者和释放边界 |

实施时增加以下约束：

- 同一个实现同时导出多个 contract 时，各 contract 必须通过工厂别名解析到同一个
  singleton，不能产生多份实例；
- `ValidateOnBuild` 与 `ValidateScopes` 在开发/测试中启用，即使首轮理论上没有 scoped
  service，也用于阻止依赖包或后续代码误加 scope；
- provider 创建的 `IDisposable` / `IAsyncDisposable` singleton 由根 provider 统一释放，
  需要审查并移除类似当前 `IoC.Get<IAudioManager>().Dispose()` 的重复手动释放路径，或
  明确证明其释放是幂等的；
- 从根 provider 直接解析 disposable transient 会被根容器跟踪到应用退出。需要短期释放
  的对象应改为显式 factory/owner，而不是把 `NonShared` 机械换成 transient 后长期从根
  provider 拉取；
- lifetime 行为测试至少覆盖引用相等/不相等、后台服务只启动一次、窗口关闭及应用退出
  释放。

文档/编辑会话 scope 若未来引入，必须先定义 scope 创建者、传递方式和关闭时的异步释放
责任，不作为本次 MEF/MSDI 等价迁移的一部分。

### 当前问题 D-006

现有 183 个由 `[Export]` 修饰的静态菜单、菜单组、工具栏、快捷键和按键绑定定义，是否
采用“各功能模块提供 `[RegisterServices]` 方法，显式把现有静态实例追加到
`IServiceCollection`”的方式迁移；还是本次扩展 Injectio，让它能够直接扫描并注册静态
字段/属性？

推荐答案：**使用按模块拆分的 `[RegisterServices]` 显式注册，不扩展 Injectio。** 这些
定义之间大量通过对象引用表达父子关系，注册原有实例最容易保持行为；而给通用 DI 生成器
增加静态成员扫描需要定义成员可见性、初始化顺序、泛型 service type、多次导出和诊断
规则，会把项目迁移变成生成器功能开发。

推荐的具体边界是：

- 保留现有静态字段和初始化表达式，只移除字段上的 MEF `[Export]`；
- Gemini 核心及每个 Ongeki 功能模块各自拥有注册方法，不建立一个包含 183 行跨模块知识
  的中央清单；
- 注册方法使用小型强类型 helper 降低重复，但 helper 不通过反射扫描字段；
- Injectio 只负责发现并调用 `[RegisterServices]` 方法；
- 添加清单测试，校验每类定义数量、唯一键、父引用、排序和命令定义引用均与迁移前基线
  一致。

用户在第 7 轮答复“同意”，D-006 已按推荐答案确认。

## 15. 第 7 轮讨论

### D-006 已确认结论

用户同意不扩展 Injectio 的静态成员扫描能力。183 个静态 UI 定义按以下方式迁移：

- 保留现有静态字段、初始化表达式和全部已有注释；
- 移除字段上的 MEF `[Export]`，不为每个定义制造包装 service class；
- Gemini 核心和各 Ongeki 功能模块分别提供 `[RegisterServices]` 静态方法；
- 注册方法把同一个现有实例按其基类/contract 追加到 `IServiceCollection`；
- 允许增加强类型注册 helper 以减少样板代码，但禁止 helper 使用运行时反射扫描；
- Injectio 只负责在编译期发现并调用这些注册方法；
- 迁移前后基线校验 183 个定义的总数及分类数量，并验证唯一键、父子引用、命令引用、
  排除项和最终排序。

注册清单的所有权必须跟随功能模块。例如 FumenVisualEditor 的菜单、工具栏和快捷键由该
模块注册；Gemini 的基础菜单/工具栏由 Gemini 注册。宿主组合根只调用模块入口，不逐项
知道 183 个定义。

### 当前问题 D-007

是否把 MEF 目前的 priority assembly / 发现顺序覆盖规则改成以下显式 MSDI 规则：

1. 单值 service 默认要求唯一；发现重复注册时启动/测试直接失败；
2. 确实需要宿主覆盖 Gemini 默认实现时，只能在组合根用明确的 `Replace` 或专用
   override 注册方法，不能依赖“最后注册碰巧获胜”；
3. 真正的扩展点使用 `Append` + `IEnumerable<T>`；集合枚举顺序不作为业务优先级，消费
   者必须根据 `SortOrder` / 显式 priority 排序；
4. 命令 handler、parser header、按键配置键等逻辑上唯一的业务键若重复，默认报错；确实
   允许覆盖的 contract 必须声明显式 priority，最高者胜出，同 priority 冲突仍报错；
5. Injectio 的 `Skip` / `Replace` / `Append` 必须逐项写明，不使用未审查的默认重复策略。

推荐答案：**采用以上显式规则，不再保留 assembly priority 这种隐式容器语义。** 当前
Gemini 的单值解析优先取 priority provider，命令路由又把 priority assembly handler 排到
最后再覆盖字典，两个机制很难从普通注册代码看出。显式冲突和业务 priority 能让错误在
启动/测试阶段出现，也不会因源码生成顺序或模块调用顺序改变行为。

组合根仍按“Gemini -> 功能模块 -> Ongeki 宿主 override”组织，便于阅读和生成稳定清单；
但除显式 override API 外，不把这个调用顺序当作服务选择契约。

用户在第 8 轮答复“同意”，D-007 已按推荐答案确认。

## 16. 第 8 轮讨论

### D-007 已确认结论

用户同意取消 MEF priority assembly / 发现顺序作为服务选择规则，正式采用：

- 单值 contract 默认只能有一个有效注册，重复即在清单验证或启动测试中失败；
- 有意覆盖只能在宿主组合根使用 Injectio `DuplicateStrategy.Replace`、MSDI `Replace()`
  或语义明确的专用 override 方法；
- 多实现扩展点统一使用 `Append` 和 `IEnumerable<T>`；
- `IEnumerable<T>` 的容器枚举顺序不构成业务契约，消费者必须按稳定的 `SortOrder`、
  priority 或其他业务字段排序；
- command handler、parser header、配置键等逻辑唯一键默认不允许重复；允许覆盖时，最高
  显式 priority 胜出，同 priority 重复仍失败；
- 每个 Injectio 注册都要审查并明确使用 Skip、Replace 或 Append，不能用未确认的默认
  重复行为掩盖冲突；
- 注册清单测试需随机化/改变模块调用顺序，验证除显式宿主 override 外，最终选择与顺序
  不受组合顺序影响。

Gemini `PriorityAssemblies` 不再参与 DI 服务选择和命令 handler 覆盖。若 Caliburn
`AssemblySource` 在首轮仍用于已知程序集的 ViewLocator，它只能保留静态程序集清单职责，
不能重新承担服务优先级或动态插件发现。

### 当前问题 D-008

迁移完成后的终态是否采用以下边界：保留 Caliburn 框架运行所需的静态 IoC 委托，并让它
们调用 `App.SourceProvider`；但 OngekiFumenEditor 与可修改的 Gemini 业务/框架代码最终
不得直接调用 `IoC.Get<T>()`、`IoC.GetAll<T>()`、`IoC.GetInstance()`，普通代码也不得直接
读取 `App.SourceProvider`？

推荐答案：**采用该边界。** 也就是保留“框架兼容适配器”，不保留“Service Locator 作为
业务编程模型”。这样无需为了移除 Caliburn 内部的静态委托而大改整个框架，同时新增和
可维护代码仍使用构造注入、方法注入和显式 factory。

建议用编译期规则固化：

- 仅允许 `OngekiFumenEditor.AppBootstrapper`（及必要的专用兼容类）读取
  `App.SourceProvider`；
- 仅 Caliburn bootstrapper 安装 `IoC.GetInstance` / `GetAllInstances` / `BuildUp` 委托；
- OngekiFumenEditor 和 Gemini 生产源码中的直接 `IoC.*` 调用建立递减基线，最终为零；
- 不把 `IServiceProvider` 注入普通 ViewModel/业务 service；确需动态创建时注入强类型
  factory；
- `BuildUp` 终态保持 no-op 或仅执行明确、无属性注入的兼容行为，禁止重造 MEF
  `SatisfyImportsOnce()`；
- 对外迁移文档不再把 `IoC.Get/GetAll` 作为扩展 API。

如果选择长期允许业务代码继续通过 IoC/App.SourceProvider 取服务，容器虽已换成 MSDI，
但依赖仍被隐藏，循环依赖、lifetime 和测试隔离问题不会得到解决，也与最初“替代 IoC
使用”的目标不一致。

用户在第 9 轮调整范围：Gemini 可以继续调用 IoC，目前只修改
OngekiFumenEditor。D-008 已按该边界确认。

## 17. 第 9 轮讨论

### D-008 已确认结论

用户明确允许 Gemini 继续使用 Caliburn `IoC`，当前直接调用点的重构范围只包括
`OngekiFumenEditor`。因此修订终态约束：

- `OngekiFumenEditor` 生产代码中的 `IoC.Get<T>()`、`IoC.GetAll<T>()`、
  `IoC.GetInstance()` 等调用建立递减基线并以零为目标；
- 普通 Ongeki ViewModel、service 和 helper 不直接读取 `App.SourceProvider`，改用构造
  注入、方法参数或强类型 factory；
- Gemini 现有 `IoC.*` 调用允许保留，并经 Caliburn 委托间接解析
  `App.SourceProvider`；
- `AppBootstrapper` / 专用组合根属于获准的适配边界，可以读取静态 provider；
- 不因本次迁移额外重构 Caliburn.Micro 内部 IoC 机制。

### 范围冲突：Gemini IoC 调用与 Gemini MEF 基础设施是两件事

“允许 Gemini 调用 IoC”可以直接接受；但“目前只修改 OngekiFumenEditor”若表示任何
Gemini 源文件都不能修改，则与此前确认的“用 Injectio + MSDI 替换本项目 MEF”目标冲突。
已核实的原因是：

- `Gemini.AppBootstrapper.Configure()` 本身创建 `AssemblyCatalog`、`AggregateCatalog` 和
  `CompositionContainer`；
- `Gemini.AppBootstrapper.GetInstance/GetAllInstances/BuildUp` 目前直接调用 MEF 容器；
- Gemini 实际构建闭包约有 73 个直接 `[Export]`、14 个 `[Import]`、21 个
  `[ImportMany]` 和 19 个 `[ImportingConstructor]`；
- Gemini 的 Shell、主窗口、菜单/工具栏 builder、命令定义/handler、主题、设置页和
  `IServiceProvider` 适配器仍依赖这些 MEF parts；
- 只覆盖 Ongeki `AppBootstrapper` 而不迁移 Gemini 注册，最多只能得到 MSDI + MEF 双容器，
  无法删除 `System.ComponentModel.Composition`，也违反此前确定的单一根 provider。

因此需要把“调用点清理范围”和“容器基础设施迁移范围”分开：Gemini 可以保留 IoC
调用，但如果要彻底去掉 MEF，仍必须对 Gemini 做最小且有边界的容器迁移。

### 当前问题 D-009

这里的“目前只修改 OngekiFumenEditor”具体采用哪一种范围？

推荐范围：**只重构 Ongeki 的 IoC 业务调用，但允许最小修改 Gemini 的 MEF 基础设施。**
允许修改 Gemini 的范围仅包括：

- 把 Gemini `AppBootstrapper` 从创建 MEF 容器改成使用宿主提供的 MSDI provider/抽象入口；
- 把 Gemini 的 MEF `[Export]/[Import*]/[PartCreationPolicy]` 迁移为 Injectio/MSDI 注册与
  构造注入；
- 把 Gemini 静态菜单、工具栏、命令定义注册为编译期模块；
- 保留 Gemini 的 `IoC.*` 调用，不做业务层 Service Locator 清理；
- 不做与 DI/MEF 替换无关的 Gemini 重构。

备选范围：**Gemini 完全只读。** 那么计划必须降级为 Ongeki-only 混合迁移：Gemini 和
其 MEF 容器继续存在，Ongeki 另用 MSDI；这会推翻“单一 provider”“彻底删除 MEF”和
部分 NativeAOT 改善目标，还会引入两个容器间 singleton、集合扩展点和释放责任的桥接。
不推荐该路线。

用户在第 10 轮答复“同意”，D-009 已按推荐范围确认。

## 18. 第 10 轮讨论

### D-009 已确认结论

用户同意把两类 Gemini 改动明确分开：

- **允许的 Gemini 改动**：移除 MEF 容器和 attributes、引入 Injectio/MSDI 注册、把成员
  注入改成构造注入、注册静态 UI 定义，以及让 Caliburn IoC 委托解析宿主 MSDI provider；
- **不在本次范围的 Gemini 改动**：清理 Gemini 的 `IoC.*` 调用、重写命令/菜单业务模型、
  无关的 API/命名/样式重构，以及修改 Caliburn.Micro 自身的 IoC 编程模型。

最终项目边界因此是：

- OngekiFumenEditor：完整迁移 MEF 注册，并逐步清理直接 `IoC.*` 业务调用；
- Gemini/Gemini.Modules.Output：只迁移维持单一 MSDI 根容器所需的 MEF 基础设施，允许
  Gemini 继续通过 IoC facade 解析；
- Caliburn.Micro：保留现有委托机制，原则上不修改，除非编译或明确的适配缺口证明不可
  避免；
- 运行时：只有 `App.SourceProvider` 指向的一个根 MSDI provider，不保留 MEF fallback。

### 当前问题 D-010

迁移是否采用下面的切换与回滚模型？

1. **验证期允许双注册描述，不允许运行时双解析**：类型可暂时同时具有 MEF 和
   Injectio 注册描述，静态 UI 定义也建立 MSDI 清单；正式程序仍只从 MEF 运行。另建测试
   provider 做注册图、数量和 lifetime 校验，但不把两个容器同时提供给业务代码。
2. **满足门槛后原子切换**：在一个受控切换提交中让 `App` 构建唯一 MSDI provider，
   Caliburn IoC 委托同时指向它；同一提交停用 MEF composition，不存在一部分 contract 从
   MEF、另一部分从 MSDI 获取的混合状态。
3. **切换后立即删除 MEF**：通过 GUI/CLI 冒烟及清单测试后，紧接着删除 MEF attributes、
   catalog、package 和插件扫描；不长期保留 `UseMefContainer` 功能开关。
4. **回滚依靠版本而非运行时开关**：保留切换前可构建、可测试的 Git 提交/发布产物；若
   切换失败，回退到该版本，而不是在新版本内维护第二套容器。

推荐答案：**采用该模型。** 运行时双容器会使 singleton 身份、多实现集合、对象释放和
循环依赖都变得不可推理；长期功能开关还会要求每次改动测试两套组合路径。验证期双注册
描述能够提前发现缺项，而原子切换和版本回滚能保持每个可发布版本只有一个权威容器。

建议切换前至少满足：

- MSDI provider 在 `ValidateOnBuild=true`、`ValidateScopes=true` 下构建成功；
- MEF 与 MSDI 的 contract/实现/lifetime 清单完成可解释的逐项对账；
- 183 个静态 UI 定义及命令、菜单、工具栏、快捷键基线一致；
- 所有成员 `[Import]` / `[ImportMany]` 和依赖 `BuildUp` 的属性注入路径已清零；
- 单值冲突、多实现业务键冲突和多 contract singleton 引用相等测试通过；
- GUI 与 CLI 的关键启动路径均能在测试 provider 下完成解析。

用户在第 11 轮答复“同意”，D-010 已按推荐答案确认。

## 19. 第 11 轮讨论

### D-010 已确认结论

用户同意采用“验证期双注册描述、运行时原子切换、版本回滚”的模型：

- 过渡提交可以同时保留 MEF attribute 和 Injectio 注册描述，但正式程序在任一时刻只激活
  一个容器；
- 测试用 MSDI provider 只承担静态注册图、数量、冲突和 lifetime 验证，不与 MEF 容器
  共享给业务代码；
- 到达切换门槛后，`App.SourceProvider`、Caliburn IoC 委托和所有实际解析路径在同一受控
  切换中转向 MSDI；
- 切换后不保留 MEF fallback、`UseMefContainer` 开关或按 contract 分流的桥；
- 回滚点是切换前最后一个可构建、测试通过的提交/发布产物，失败时整体回退版本；
- MEF 删除可以是紧随原子切换的清理提交，但两者必须属于同一迁移发布，不形成长期双制。

### 当前问题 D-011

是否同意只有同时满足以下条件，才把本次迁移标记为完成？

#### A. 容器与依赖清理

- 运行时只有 `App.SourceProvider` 持有的一个根 MSDI provider，且只构建一次；
- `OngekiFumenEditor`、`Gemini` 和实际引用的 `Gemini.Modules.Output` 不再引用
  `System.ComponentModel.Composition` package；
- 上述构建闭包内 MEF `[Export]`、`[Import*]`、`[PartCreationPolicy]`、catalog、
  `CompositionContainer`、`SatisfyImportsOnce` 和插件目录扫描全部清零；
- 不存在 MEF fallback、双容器、运行时未知 DLL 插件加载或容器选择开关；
- `App.SourceProvider` 使用 `System.IServiceProvider` getter-only API，初始化前访问明确失败，
  应用退出时统一释放且不会重复释放服务。

#### B. IoC 边界

- Gemini 与 Caliburn 允许保留已批准的 `IoC.*` 调用，并全部间接解析唯一 MSDI provider；
- `OngekiFumenEditor` 普通生产代码直接 `IoC.*` 调用为零；
- `OngekiFumenEditor` 只有 `App`、`AppBootstrapper` 或明确列入白名单的专用组合适配类可
  读取 `App.SourceProvider`；
- 不向普通 ViewModel/业务 service 注入裸 `IServiceProvider`，动态创建使用强类型 factory；
- `BuildUp` 不提供属性注入，所有成员 `[Import]` 路径在切换前已改成构造注入或显式工厂。

#### C. 注册与 lifetime 等价

- 默认/Shared -> Singleton、NonShared -> Transient 的逐项清单对账完成；
- 同一个 singleton 实现导出为多个 contract 时，解析结果引用相等；
- 单值重复注册失败，宿主覆盖必须显式 Replace，多实现必须显式 Append；
- 多实现集合按照业务排序字段产生稳定结果，逻辑唯一键冲突按 D-007 规则诊断；
- `ValidateOnBuild=true`、`ValidateScopes=true` 的验证 provider 构建成功；
- disposable singleton/transient 的所有权和释放测试通过。

#### D. UI 与功能基线

- 183 个静态 UI 定义按已记录分类全部注册，并通过数量、唯一键、父引用、排序、排除项、
  命令引用测试；
- 90 个命令定义、86 个命令 handler，以及 parser、drawing target、check rule、theme、
  editor provider、settings editor 等关键多实现扩展点完成清单对账；
- GUI 启动、CLI 启动/退出、主窗口显示、菜单/工具栏/快捷键、文档打开、谱面解析、设置页、
  调度器、音频与正常退出的 smoke test 通过；
- Debug/Release 均可构建，现有自动化测试无回归。

#### E. 文档与范围声明

- Readme 和 Wiki 不再宣称支持 MEF/`Plugins/*` 动态插件，明确旧插件机制是破坏性移除；
- 新的编译期模块注册方式、lifetime、覆盖规则和禁止直接使用 IoC 的 Ongeki 编码约定有
  开发文档；
- NativeAOT 文档只把本迁移记为消除 MEF/运行时发现障碍，不宣称它单独让 WPF 应用达到
  NativeAOT 完整兼容。

推荐答案：**接受整套完成标准。** 可以分阶段、分提交实现，但 A-E 任一项未满足时只标记
为“迁移中”或“MSDI 已切换”，不能标记为“MEF/IoC 迁移完成”。

等待用户回答后，若 D-011 确认，将把已确认决策汇总成可执行工作包和依赖顺序，再进行
最后一轮计划审阅。
