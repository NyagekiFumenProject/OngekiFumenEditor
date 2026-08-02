# Avalonia CommandLine 迁移跟踪

> 建立时间：2026-08-02
> 状态：第一阶段（CLI 框架与 `convert`）已完成并通过验证
> 作用：本文件是 Avalonia CommandLine 后续设计、范围、实施进度、测试证据和结论的统一跟踪入口。

## 1. 目标

将旧版 `OngekiFumenEditor.CommandLine.exe` 的命令能力迁移到独立的
`OngekiFumenEditor.Avalonia.CommandLine.exe`，同时满足以下要求：

- 参考原项目 `DefaultCommandExecutor` 的组织形式和命令契约。
- 使用 `System.CommandLine` 负责命令树、选项、帮助、解析错误和调用分派。
- 命令定义与业务执行解耦，根入口不写死每个命令的处理过程。
- 新增命令时不需要修改 `Program.Main` 或复制一套分派逻辑。
- JIT 与 Native AOT 发行行为一致。
- 为参数解析、业务处理和真实 EXE 建立自动化测试。
- CI 发布契约的调整暂不纳入当前阶段。

## 2. 已确认决策

### 2.1 命令行库

- 同意直接使用 `System.CommandLine` 迁移。
- 实现必须参考原项目的命令名称、选项名称、默认值、必填规则、帮助文本和退出行为。
- 旧项目与 Avalonia 解决方案均采用 `System.CommandLine 2.0.0`。
- 版本已在 `Directory.Packages.props` 中集中管理；JIT 与 Native AOT 发布及真实 EXE
  冒烟均已通过。

### 2.2 总体架构

- 架构应与旧 `DefaultCommandExecutor` 相似：一个根执行器聚合多个命令行定义，各定义拥有自己的选项和处理器。
- 禁止在 `Program.Main` 中使用 `switch`/`if` 写死 `svg`、`convert`、`jacket`、`acb`、`updater` 的业务过程。
- 命令定义统一抽象为 `ICommandLineDefinition`，通过 DI 以集合形式注入根执行器；根执行器只负责组装命令树和执行解析结果。
- 命令处理器统一抽象为 `ICommandLineHandler`；每个命令行定义与对应处理器配对。
- 优先沿用项目现有的 Injectio 编译期注册方式，避免运行时程序集扫描。
- 允许保留类似旧版选项模型的强类型参数对象，但不直接搬运旧版的运行时反射绑定实现。

### 2.3 命令迁移顺序

1. 建立 CLI 宿主、命令框架和 `convert`。
2. 迁移 `svg`。
3. 迁移 `jacket` 与 `acb`。
4. 最后迁移具有目录写入/覆盖风险的 `updater`。

### 2.4 测试

- 同意增加参数解析单元测试。
- 同意增加业务命令集成测试。
- 同意对 JIT 与 Native AOT EXE 执行真实冒烟测试。
- 所有会修改文件的测试必须使用独立临时目录，不接触用户数据。

### 2.5 暂不实施

- 暂不修改 `.github/workflows/BuildProgram.yml` 的占位契约。
- 在命令实现完成前，CI 仍只验证伴随 EXE 被正确打包并返回当前占位结果。
- 完成命令迁移后需要重新评估该延期项，避免 CI 长期把“没有命令”判定为成功。

## 3. 可行性结论：可以直接使用 System.CommandLine

`System.CommandLine` 可以直接承担以下职责：

- `RootCommand` 和子命令树。
- `Command`、`Option<T>`、别名、必填规则和默认值。
- `--help`、`--version`、未知命令及解析错误输出。
- 从解析结果构造强类型选项，并调用异步处理器。
- 统一取消、标准输出、标准错误和退出码处理。

需要避免的不是 `System.CommandLine` 本身，而是旧 `DefaultCommandExecutor` 中的动态绑定方式：

- `typeof(T).GetProperties()` 扫描选项属性。
- `MakeGenericType` 动态构造 `Option<T>`。
- `Expression.Compile()` 动态生成委托。
- 依赖运行时反射发现所有命令。

这些做法会增加裁剪和 Native AOT 风险。第一版应由每个命令行定义显式创建强类型
`Option<T>` 并显式绑定到选项模型。若后续确认属性声明更适合维护，应增加编译期源生成器，
而不是恢复运行时反射。

## 4. 建议架构

```text
Program.Main(args)
    -> CommandLineHost
        -> ICommandExecutor / DefaultCommandExecutor
            -> RootCommand
            -> IEnumerable<ICommandLineDefinition>
                -> ConvertCommandLineDefinition -> ICommandLineHandler<FumenConvertOption>
                -> SvgCommandLineDefinition     -> ICommandLineHandler<SvgGenerateOption>
                -> JacketCommandLineDefinition  -> ICommandLineHandler<JacketGenerateOption>
                -> AcbCommandLineDefinition     -> ICommandLineHandler<AcbGenerateOption>
                -> UpdaterCommandLineDefinition -> ICommandLineHandler<UpdaterOption>
                    -> 可复用的核心业务服务
```

### 4.1 责任边界

`Program.Main`：

- 创建 CommandLine 服务容器，不启动 Avalonia 应用生命周期。
- 调用 `ICommandExecutor.ExecuteAsync(args, cancellationToken)`。
- 返回执行器给出的退出码。
- 不包含具体命令名称和业务逻辑。

`DefaultCommandExecutor`：

- 创建根命令及全局选项，例如 `--verbose/-v`。
- 从 DI 获取全部 `ICommandLineDefinition` 并加入根命令。
- 检查重复命令名，错误时立即失败。
- 调用 `System.CommandLine` 执行命令树。
- 统一处理未捕获异常、日志和退出码。

`ICommandLineDefinition`：

- 声明一个顶层命令的名称和描述。
- 创建该命令所需的强类型选项。
- 将解析结果转换为命令选项模型。
- 将解析结果绑定为强类型选项后，转发给对应的 `ICommandLineHandler`。
- 不直接初始化全局应用或 UI。

`ICommandLineHandler`：

- 只接收已经验证的强类型选项和 `CancellationToken`。
- 调用可复用业务服务。
- 返回统一的命令结果/退出码。
- 业务失败写入 stderr；正常结果写入 stdout。
- 不直接依赖 `System.CommandLine.ParseResult`；解析库边界由命令行定义负责。

### 4.2 DI 与命令发现

每个命令行定义使用 Injectio 注册为 `ICommandLineDefinition`。根执行器构造函数接收
`IEnumerable<ICommandLineDefinition>`，这样新增命令时只需要增加新的定义、处理器和业务服务，
根执行器与 `Program.Main` 无需修改。Injectio 在编译期生成注册代码，比运行时程序集扫描更适合
Native AOT。

`ICommandLineHandler` 保留为所有命令行处理器的统一基接口；使用闭合泛型接口建立定义和处理器的
编译期映射：

```csharp
public interface ICommandLineHandler
{
}

public interface ICommandLineHandler<in TOptions> : ICommandLineHandler
{
    Task<int> HandleAsync(TOptions options, CancellationToken cancellationToken);
}

[RegisterSingleton<ICommandLineDefinition>]
internal sealed class ConvertCommandLineDefinition(
    ICommandLineHandler<FumenConvertOption> handler) : ICommandLineDefinition
{
    // Definition 创建 Option<T>，将 ParseResult 绑定成 FumenConvertOption，
    // 再调用 handler.HandleAsync(options, cancellationToken)。
}

[RegisterSingleton<ICommandLineHandler<FumenConvertOption>>]
internal sealed class ConvertCommandLineHandler : ICommandLineHandler<FumenConvertOption>
{
    public async Task<int> HandleAsync(
        FumenConvertOption options,
        CancellationToken cancellationToken)
    {
        var result = await convertService.GenerateAsync(
            options,
            cancellationToken: cancellationToken);
        return result.IsSuccess ? 0 : -4;
    }
}
```

该方案的映射关系由选项类型确定：

- `ConvertCommandLineDefinition` 只能获得 `ICommandLineHandler<FumenConvertOption>`。
- DI 不需要在多个 `ICommandLineHandler` 中按命令名查找。
- Definition 保留 `System.CommandLine` 依赖；Handler 保持强类型且不依赖解析库。
- 每个命令应拥有自己的选项类型，避免两个 Definition 争用同一个闭合泛型 Handler 注册。

不推荐让 Definition 注入 `IEnumerable<ICommandLineHandler>` 后按字符串、类型名或属性筛选。这会把
重复注册和缺失注册推迟到运行时，也会丢失编译期类型检查。若最终不采用泛型接口，次选方案是让
Definition 直接注入具体的 `ConvertCommandLineHandler`，仍不进行运行时查找。

该映射已经按上述方案实现：`ConvertCommandLineDefinition` 的构造函数直接注入
`ICommandLineHandler<FumenConvertOption>`，Injectio 生成定义集合和闭合泛型 Handler 注册。
未使用 `Assembly.GetTypes()` 或按命令名查找 Handler。

### 4.3 Definition 与 Handler 的配对规则

- 一个 `ICommandLineDefinition` 对应一个闭合的 `ICommandLineHandler<TOptions>`。
- Definition 负责命令名称、帮助、`Option<T>`、解析验证和选项模型构造。
- Handler 负责业务验证、业务服务调用、输出和业务退出码。
- `ParseResult`、`Command`、`Option<T>` 不得传入 Handler。
- `TOptions` 应为只承载命令输入的强类型对象，不持有 DI 服务或 UI 对象。
- 处理器生命周期默认使用 singleton；若后续处理器持有单次执行状态，应改用 transient，并将状态限制在
  `HandleAsync` 调用内。

### 4.4 无 UI 宿主

当前已通过 `AddOngekiFumenEditorCommandLine()` 建立独立宿主。CLI 不创建
`Avalonia.Application`，不启动 Dispatcher、Shell、窗口或启动画面；`convert` 调用路径使用
构造函数注入的 `IFumenConvertService`、解析器、转换器和检查规则，不再依赖
`Avalonia.Application.Current` 才能工作。日志与对象池也增加了无 UI 初始化路径。

第一阶段为了复用既有 Injectio 生成结果，`AddOngekiFumenEditorCommandLine()` 当前仍调用完整的
`AddOngekiFumenEditorAvalonia()` 注册扩展。UI 服务只被注册而没有被 CLI 实例化，但这些服务描述符
会扩大 Native AOT 的可达面并产生已有共享核心裁剪警告。后续应使用 Injectio tags 或独立的编译期
核心注册模块，只注册解析、格式转换、SVG 和资源生成所需服务；在完成该拆分前，不把现状描述为
“只注册无 UI 服务”。

## 5. 命令迁移矩阵

| 命令 | 旧版实现 | Avalonia 当前基础 | 当前结论 |
|---|---|---|---|
| `convert` | `FumenConverterWrapper` | 已提取 `IFumenConvertService` 并实现强类型 Definition/Handler | 第一阶段完成；JIT/AOT 行为已验证 |
| `svg` | `IPreviewSvgGenerator` | SVG 生成器和选项模型已存在 | 第二优先；需补无 UI 调用、时长和 PNG 语义检查 |
| `jacket` | `JacketGenerateWrapper` | 未发现已迁移命令实现 | 需要移植生成服务及 AssetsTools 依赖边界 |
| `acb` | `AcbGeneratorFuckWrapper` | 有底层 ACB/音频依赖，但未发现命令生成实现 | 需要移植生成服务并验证原生依赖/AOT |
| `updater` | `IProgramUpdater.CommandExecuteUpdate` | 未发现已迁移命令实现 | 最后处理；必须增加路径和覆盖保护 |

旧版全局选项：

- `--verbose` / `-v`

旧版命令参数应以原项目源码为基线逐项登记；除非另有决策，不擅自改名或删除参数。

## 6. 第一阶段实施范围

### 6.1 CLI 基础设施

- [x] 在 Central Package Management 中加入 `System.CommandLine 2.0.0`。
- [x] CommandLine 项目引用核心项目及必要的 DI/日志包。
- [x] 新增不启动 UI 的 CommandLine 服务注册入口。
- [x] 新增 `ICommandExecutor`、`DefaultCommandExecutor`、`ICommandLineDefinition` 和 `ICommandLineHandler`。
- [x] 根帮助、版本、未知命令和全局 verbose 正常工作。
- [x] 定义并测试 stdout/stderr 和首个业务命令退出码约定。
- [x] 删除当前“所有调用都返回占位文本”的 `Program.Main` 实现。
- [ ] 将完整核心 Injectio 注册拆成按 CLI 能力选择的 headless 注册集合。

### 6.2 convert 命令

- [x] 保留 `--inputFile`、`--outputFile`、`--standardize`。
- [x] 必需参数在业务处理前由解析层拒绝。
- [x] 输入/输出路径要求为完全限定路径，不依赖当前工作目录的隐式行为。
- [x] 转换服务改用构造函数注入。
- [x] 成功时原子写入目标文件并返回 0。
- [x] 不支持格式、无效输入、输出失败返回稳定非零退出码并写 stderr。
- [x] 支持取消且不留下半写入目标文件或 `.tmp` 文件。

第一阶段退出码：

| 退出码 | 含义 | 输出 |
|---:|---|---|
| `0` | 成功 | 默认无输出；`--verbose/-v` 输出日志 |
| `1` | `System.CommandLine` 解析错误、未知命令或未知选项 | 帮助/错误信息 |
| `-3` | 输入或输出路径不是完全限定路径 | stderr |
| `-4` | 格式不支持、转换失败或业务异常 | stderr |

## 7. 测试与验收

### 7.1 单元测试（已完成）

- 根帮助列出全部已注册命令。
- 重复命令名构建失败。
- `--verbose/-v` 行为一致。
- 每个必填选项缺失时返回解析错误。
- 布尔值和路径参数绑定正确；当前 `convert` 没有枚举参数。
- 未知命令/未知选项不会进入业务处理器。

### 7.2 集成测试（`convert` 已完成）

- 使用仓库 fixture 执行真实 `convert` 输入到输出。
- 比较关键语义或规范化输出，不只检查文件存在。
- 覆盖不支持格式、处理器异常和取消场景；取消时保留既有目标且不残留临时文件。
- 后续按命令增加 SVG 内容、图片尺寸、ACB 资源和 updater 临时目录断言。

### 7.3 EXE 冒烟测试（第一阶段已完成）

- JIT 与 Native AOT 均运行根帮助和版本。
- 每个命令运行 `--help`。
- 每个已完成命令至少执行一条成功路径和一条错误路径。
- 校验退出码、stdout、stderr 和输出文件。
- updater 只能操作专用临时目录。

## 8. 当前验证基线

2026-08-02 迁移前检查结果：

- Avalonia 全量测试：144/144 通过，0 跳过。
- CommandLine JIT 与 Native AOT 各执行 15 组参数，共 30 次。
- 两种 EXE 均无崩溃、无挂起、stderr 为空。
- 30 次调用全部返回退出码 1 和同一条“命令执行器尚未迁移”提示。
- `--help`、`--version`、未知命令及五个旧版命令均未被识别。
- Native AOT EXE 为 1,110,016 字节，无 runtimeconfig，发布本身正常。
- 当前测试项目没有 CommandLine 相关自动化测试。

该基线只证明伴随 EXE 的占位契约稳定，不能证明任何业务命令可用。

2026-08-02 第一阶段实现后验证结果：

- Avalonia Release 全量测试：162/162 通过，0 失败，0 跳过；其中新增 CommandLine 测试 18 项。
- JIT self-contained 与 Native AOT 均发布成功。
- 两种 EXE 各执行 10 组真实参数，共 20 次；全部得到预期退出码、stdout、stderr 和文件结果。
- 每套冒烟覆盖根帮助、版本、`convert --help`、未知命令、缺失必填参数、相对路径、
  Nyageki→OGKR、OGKR→Nyageki、`--standardize` 和不支持的输出格式。
- JIT 与 AOT 生成的 OGKR、Nyageki 和规范化 OGKR 大小分别一致为 635、762、609 字节。
- 所有成功转换均无 `.tmp` 残留；失败转换没有创建目标文件。
- JIT 启动 EXE 为 162,816 字节；Native AOT EXE 为 24,012,800 字节。
- AOT 发布仍报告共享核心中既有的反射/裁剪警告，主要来自设置、本地化、属性浏览器和 SVG；
  `CommandArgs` 的运行时 `TypeDescriptor` 回退已删除，因此其 AOT 警告已消除。

## 9. 待确认事项

- [x] `System.CommandLine` 采用 `2.0.0` 及其正式 API。
- [x] 采用 `ICommandLineHandler<TOptions> : ICommandLineHandler` 的强类型处理器签名。
- [ ] 是否完全保持旧版帮助文本本地化，还是第一阶段先固定一种语言。
- [x] `convert` 第一阶段保留旧版 `-3`、`-4`；新增命令前再决定通用退出码体系。
- [ ] 使用 Injectio tags 或独立生成模块缩小 CommandLine 的核心服务注册与 AOT 可达面。
- [ ] `svg --png` 在当前 Avalonia SVG 实现中的准确语义。
- [ ] SVG 的 `--audioFile` 是否继续必填；无音频时旧业务代码实际存在按谱面估算时长的分支。
- [ ] jacket/acb 所需外部工具和原生依赖是否允许进入 Native AOT 包。
- [ ] updater 是否仍需要作为公开命令保留，以及允许覆盖的目录边界。

## 10. 决策与进度日志

| 日期 | 类型 | 内容 | 状态 |
|---|---|---|---|
| 2026-08-02 | 决策 | 使用 `System.CommandLine`，参考旧项目形式迁移 | 已确认 |
| 2026-08-02 | 决策 | 保留类似 `DefaultCommandExecutor` 的可扩展模块架构，不在入口写死命令过程 | 已确认 |
| 2026-08-02 | 命名 | 命令定义统一命名为 `ICommandLineDefinition`，对应处理器统一命名为 `ICommandLineHandler` | 已确认 |
| 2026-08-02 | 方案 | Definition 通过构造函数注入闭合的 `ICommandLineHandler<TOptions>`，非泛型接口作为统一基接口 | 已实现 |
| 2026-08-02 | 决策 | 按 convert、svg、jacket/acb、updater 顺序迁移 | 已确认 |
| 2026-08-02 | 决策 | 增加单元、集成和 JIT/AOT EXE 冒烟测试 | 已确认 |
| 2026-08-02 | 延期 | 暂不修改 CI 占位校验 | 已确认 |
| 2026-08-02 | 调研 | convert/svg 基础已存在；jacket/acb/updater 命令实现尚未迁移 | 已完成 |
| 2026-08-02 | 实现 | 完成可扩展 CLI 框架、无 UI 启动入口、`convert` Definition/Handler 与可复用转换服务 | 已完成 |
| 2026-08-02 | 测试 | 新增 18 项 CommandLine 测试，全量 162/162 通过 | 已完成 |
| 2026-08-02 | 发布 | JIT self-contained 与 Native AOT 发布成功，两种 EXE 共 20 次真实冒烟全部通过 | 已完成 |
| 2026-08-02 | 风险 | CLI 暂时复用完整核心 Injectio 注册；后续按能力拆分以缩小 AOT 可达面 | 待处理 |
| 2026-08-02 | 后续 | 第二阶段迁移 `svg`，继续沿用 Definition/Handler/业务服务边界 | 待开始 |

后续讨论形成的新约束、范围变化、测试结果和实现结论均应更新本文件，并在本日志追加记录。
