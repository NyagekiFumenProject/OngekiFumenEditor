# MCP Runtime Script Host 实施草案

## 文档定位

这是一份准备直接开工的实施草案。

它承接已有两份设计文档：

- [mcp-server-implementation-outline.md](C:/Users/mikir/source/repos/OngekiFumenEditorPlugins.KngkSupport/refHost/OngekiFumenEditor/OngekiFumenEditor/docs/mcp-server-implementation-outline.md)
- [runtime-automation-script-host.md](C:/Users/mikir/source/repos/OngekiFumenEditorPlugins.KngkSupport/refHost/OngekiFumenEditor/OngekiFumenEditor/docs/runtime-automation-script-host.md)

前两份文档偏总体设计和边界说明；本文档收敛到第一版真正要实现的模型、目录、接口、调用链和分工。

## 第一版目标

第一版先只解决一件事：

让外部模型能够对“当前正在运行的编辑器”编写并执行脚本。

第一版调用链定义为：

```text
MCP Client
  -> MCP Server
    -> RuntimeAutomationScriptHost
      -> 当前激活 Editor
        -> 现有脚本执行器
          -> 返回结构化结果
```

这一步优先复用现有脚本系统，而不是先做完整的高层自动化 API。

相关现有代码：

- [IEditorScriptExecutor.cs](C:/Users/mikir/source/repos/OngekiFumenEditorPlugins.KngkSupport/refHost/OngekiFumenEditor/OngekiFumenEditor/Modules/EditorScriptExecutor/Kernel/IEditorScriptExecutor.cs)
- [DefaultEditorScriptExecutor.cs](C:/Users/mikir/source/repos/OngekiFumenEditorPlugins.KngkSupport/refHost/OngekiFumenEditor/OngekiFumenEditor/Modules/EditorScriptExecutor/Kernel/DefaultImpl/DefaultEditorScriptExecutor.cs)
- [ScriptArgs.cs](C:/Users/mikir/source/repos/OngekiFumenEditorPlugins.KngkSupport/refHost/OngekiFumenEditor/OngekiFumenEditor/Modules/EditorScriptExecutor/Scripts/ScriptArgs.cs)
- [IEditorDocumentManager.cs](C:/Users/mikir/source/repos/OngekiFumenEditorPlugins.KngkSupport/refHost/OngekiFumenEditor/OngekiFumenEditor/Modules/FumenVisualEditor/Kernel/IEditorDocumentManager.cs)

## 第一版约束

- 第一版允许模型生成脚本，但不允许放开反射。
- 第一版保留“当前编辑器”工作模式，不做多会话复杂调度。
- 第一版默认要求用户确认后再执行会改谱的脚本。
- 第一版 MCP 只监听本机回环地址，不开放局域网访问。
- 第一版不引入新的 `services.AddSingleton(...)` 风格，统一沿用项目现有的 MEF 导出方式。

## 目录结构

目录采用当前项目的 `Kernel` 命名风格，按职责拆成 `RuntimeAutomation` 和 `Mcp` 两块。

```text
Kernel/
  RuntimeAutomation/
    IRuntimeAutomationScriptHost.cs
    IRuntimeEditorContextProvider.cs
    IScriptSecurityPolicy.cs
    ScriptBuildRequest.cs
    ScriptRunRequest.cs
    ScriptBuildResult.cs
    ScriptRunResult.cs
    ScriptDiagnostic.cs
    ScriptSecurityCheckResult.cs
    EditorContextInfo.cs
    RuntimeAutomationScriptHost.cs
    RuntimeEditorContextProvider.cs
    DefaultScriptSecurityPolicy.cs

  Mcp/
    IMcpServerHost.cs
    McpServerHost.cs
    EditorTools.cs
    ScriptTools.cs
```

## 运行时模型

### McpServerHost

职责：

- 启动和停止 MCP 服务
- 注册 MCP tools
- 维护 transport 生命周期

### RuntimeAutomationScriptHost

职责：

- 统一脚本编译和执行入口
- 在执行前做安全检查
- 绑定当前编辑器
- 切换到 UI 线程
- 注入脚本上下文
- 收集返回值、日志、异常
- 缓存最后一次执行结果

### RuntimeEditorContextProvider

职责：

- 获取当前激活编辑器
- 获取指定编辑器
- 获取所有已打开编辑器
- 对 MCP 层输出轻量化的 `EditorContextInfo`

### DefaultScriptSecurityPolicy

职责：

- 在执行前做文本级危险能力检查
- 返回结构化 `ScriptSecurityCheckResult`

### EditorTools

职责：

- 暴露只读 MCP tools
- 只读当前编辑器上下文
- 不直接持有或暴露编辑器 VM

### ScriptTools

职责：

- 暴露脚本编译/执行类 MCP tools
- 做轻量参数整形
- 不直接执行编辑器操作
- 核心逻辑全部委托给 `IRuntimeAutomationScriptHost`

## MCP Tools 规划

### EditorTools 中的内容

第一版只放只读工具：

- `editor.get_current`
- `editor.list_opened`
- `editor.get_current_summary`

它们的职责只有三件事：

- 接收 MCP 参数
- 调 `IRuntimeEditorContextProvider`
- 把结果整理成稳定 JSON 输出

### ScriptTools 中的内容

第一版放脚本类工具：

- `script.compile`
- `script.run_current_editor`
- `script.run_editor`
- `script.get_last_result`

它们的职责只有三件事：

- 接收 MCP 参数
- 调 `IRuntimeAutomationScriptHost`
- 把宿主结果转换成 tool result

## MCP 层边界

`Kernel/Mcp` 不应直接依赖以下类型：

- `FumenVisualEditorViewModel`
- `UndoRedoManager`
- `ScriptArgs`
- 谱面领域对象模型

`Kernel/Mcp` 只应依赖：

- `IRuntimeAutomationScriptHost`
- `IRuntimeEditorContextProvider`

这样可以保证 MCP 层只是协议壳，而不是业务实现层。

## RuntimeAutomation 接口草案

### IRuntimeAutomationScriptHost

```csharp
namespace OngekiFumenEditor.Kernel.RuntimeAutomation;

public interface IRuntimeAutomationScriptHost
{
    Task<ScriptBuildResult> BuildAsync(ScriptBuildRequest request, CancellationToken cancellationToken = default);

    Task<ScriptRunResult> RunOnCurrentEditorAsync(ScriptRunRequest request, CancellationToken cancellationToken = default);

    Task<ScriptRunResult> RunOnEditorAsync(string editorId, ScriptRunRequest request, CancellationToken cancellationToken = default);

    ScriptRunResult? GetLastResult();
}
```

### IRuntimeEditorContextProvider

```csharp
namespace OngekiFumenEditor.Kernel.RuntimeAutomation;

public interface IRuntimeEditorContextProvider
{
    EditorContextInfo? GetCurrentEditor();

    EditorContextInfo? GetEditor(string editorId);

    IReadOnlyList<EditorContextInfo> GetOpenedEditors();
}
```

### IScriptSecurityPolicy

```csharp
namespace OngekiFumenEditor.Kernel.RuntimeAutomation;

public interface IScriptSecurityPolicy
{
    ScriptSecurityCheckResult Check(string scriptText);
}
```

## DTO 草案

### ScriptBuildRequest

```csharp
namespace OngekiFumenEditor.Kernel.RuntimeAutomation;

public sealed class ScriptBuildRequest
{
    public string ScriptText { get; init; } = string.Empty;
    public bool EnableSecurityCheck { get; init; } = true;
}
```

### ScriptRunRequest

```csharp
namespace OngekiFumenEditor.Kernel.RuntimeAutomation;

public sealed class ScriptRunRequest
{
    public string ScriptText { get; init; } = string.Empty;
    public string? ExpectedEditorId { get; init; }
    public bool RequireConfirmation { get; init; } = true;
    public bool WrapUndoTransaction { get; init; } = true;
    public string? TransactionName { get; init; }
    public string? RequestedBy { get; init; }
}
```

### ScriptBuildResult

```csharp
namespace OngekiFumenEditor.Kernel.RuntimeAutomation;

public sealed class ScriptBuildResult
{
    public bool Success { get; init; }
    public IReadOnlyList<ScriptDiagnostic> Diagnostics { get; init; } = Array.Empty<ScriptDiagnostic>();
    public IReadOnlyList<string> SecurityIssues { get; init; } = Array.Empty<string>();
}
```

### ScriptRunResult

```csharp
namespace OngekiFumenEditor.Kernel.RuntimeAutomation;

public sealed class ScriptRunResult
{
    public bool Success { get; init; }
    public string? EditorId { get; init; }
    public string? TransactionName { get; init; }
    public string? ReturnValueJson { get; init; }
    public IReadOnlyList<string> Logs { get; init; } = Array.Empty<string>();
    public IReadOnlyList<ScriptDiagnostic> Diagnostics { get; init; } = Array.Empty<ScriptDiagnostic>();
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
}
```

### ScriptDiagnostic

```csharp
namespace OngekiFumenEditor.Kernel.RuntimeAutomation;

public sealed class ScriptDiagnostic
{
    public string Severity { get; init; } = "Info";
    public string Message { get; init; } = string.Empty;
    public int? Line { get; init; }
    public int? Column { get; init; }
}
```

### EditorContextInfo

```csharp
namespace OngekiFumenEditor.Kernel.RuntimeAutomation;

public sealed class EditorContextInfo
{
    public string EditorId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? ProjectPath { get; init; }
    public string? FumenPath { get; init; }
    public bool IsDirty { get; init; }
    public bool IsActive { get; init; }
}
```

### ScriptSecurityCheckResult

```csharp
namespace OngekiFumenEditor.Kernel.RuntimeAutomation;

public sealed class ScriptSecurityCheckResult
{
    public bool Success { get; init; }
    public IReadOnlyList<string> Issues { get; init; } = Array.Empty<string>();
}
```

## Mcp 接口草案

### IMcpServerHost

```csharp
namespace OngekiFumenEditor.Kernel.Mcp;

public interface IMcpServerHost
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    bool IsRunning { get; }
}
```

`McpServerHost` 作为实现类，负责 MCP server 生命周期。

## EditorTools / ScriptTools 建议内容

### EditorTools

`EditorTools` 是只读工具集合，不承载核心业务逻辑。

建议方法职责：

- `GetCurrent()`：返回当前激活编辑器
- `ListOpened()`：返回所有已打开编辑器
- `GetCurrentSummary()`：返回当前谱面的轻量摘要

示意：

```csharp
[Export]
[PartCreationPolicy(CreationPolicy.Shared)]
internal sealed class EditorTools
{
    private readonly IRuntimeEditorContextProvider editorContextProvider;

    [ImportingConstructor]
    public EditorTools(IRuntimeEditorContextProvider editorContextProvider)
    {
        this.editorContextProvider = editorContextProvider;
    }

    public object GetCurrent()
    {
        return editorContextProvider.GetCurrentEditor();
    }

    public object ListOpened()
    {
        return new
        {
            items = editorContextProvider.GetOpenedEditors()
        };
    }
}
```

### ScriptTools

`ScriptTools` 是脚本工具集合，同样不承载核心业务逻辑。

建议方法职责：

- `Compile(...)`
- `RunCurrentEditor(...)`
- `RunEditor(...)`
- `GetLastResult()`

示意：

```csharp
[Export]
[PartCreationPolicy(CreationPolicy.Shared)]
internal sealed class ScriptTools
{
    private readonly IRuntimeAutomationScriptHost scriptHost;

    [ImportingConstructor]
    public ScriptTools(IRuntimeAutomationScriptHost scriptHost)
    {
        this.scriptHost = scriptHost;
    }

    public Task<ScriptBuildResult> Compile(string scriptText, CancellationToken ct)
    {
        return scriptHost.BuildAsync(new ScriptBuildRequest
        {
            ScriptText = scriptText
        }, ct);
    }

    public ScriptRunResult? GetLastResult()
    {
        return scriptHost.GetLastResult();
    }
}
```

## 脚本安全策略

第一版不做强沙箱，但仍然保留脚本级约束。

### 第一版禁止内容

文本级至少拦截这些内容：

- `#r`
- `System.Reflection`
- `Activator.CreateInstance`
- `Assembly.Load`
- `Type.GetType`
- `MethodInfo`
- `PropertyInfo`
- `FieldInfo`
- `System.Diagnostics.Process`
- `System.IO.File`
- `System.IO.Directory`
- `System.Net`

### 安全原则

- 第一版不允许反射
- 第一版编译前先做安全检查
- 第一版即使客户端传 `RequireConfirmation=false`，服务端也可以拒绝执行
- 第一版仅允许本机回环访问 MCP server

这套限制属于“弱沙箱 + 本地确认”，不是完整安全沙箱。

## 确认流

确认权在服务端，不在模型端。

约定如下：

- `script.compile` 不需要确认
- `script.run_*` 默认需要确认
- 如果请求中 `RequireConfirmation=false`，服务端仍可返回 `USER_CONFIRMATION_REQUIRED`

## ErrorCode 约定

`ScriptRunResult.ErrorCode` 先收敛到固定集合：

- `NO_ACTIVE_EDITOR`
- `EDITOR_NOT_FOUND`
- `EDITOR_CHANGED`
- `SECURITY_CHECK_FAILED`
- `SCRIPT_BUILD_FAILED`
- `SCRIPT_RUNTIME_ERROR`
- `SCRIPT_RETURN_SERIALIZATION_FAILED`
- `USER_CONFIRMATION_REQUIRED`

## 脚本返回值约定

第一版统一要求脚本返回可序列化结果。

建议约定：

- `return null;` 视为无结果
- `return "ok";` 返回字符串
- `return new { created = 3, deleted = 1 };` 返回 JSON
- 如果返回复杂编辑器对象，则拒绝直接序列化

## Undo / Redo 约定

目标是尽量保证“模型一次操作 = 用户一次可撤销操作”。

建议约定：

- `script.run_current_editor` 默认 `WrapUndoTransaction=true`
- 事务默认名为 `MCP Script`
- 模型传入 `TransactionName` 时优先使用模型传入值
- 如果当前脚本执行器暂时不好外包事务，第一版可退而求其次，由脚本自己使用 `UndoRedoManager`

## 运行时执行链

`script.run_current_editor` 固定走如下链路：

```text
ScriptTools.RunCurrentEditor()
  -> IRuntimeAutomationScriptHost.RunOnCurrentEditorAsync()
    -> IRuntimeEditorContextProvider.GetCurrentEditor()
    -> IScriptSecurityPolicy.Check()
    -> IEditorScriptExecutor.Compile/Prepare
    -> UI Dispatcher.InvokeAsync(...)
    -> ScriptArgsGlobalStore push current editor
    -> execute script
    -> collect logs / return value
    -> ScriptArgsGlobalStore pop
    -> cache last result
    -> return ScriptRunResult
```

关键点：

- `ScriptArgs` 的注入和清理必须成对
- 任何真正触碰编辑器对象的逻辑都必须在 UI 线程中完成

## RuntimeAutomation 实现职责

### RuntimeEditorContextProvider

职责：

- 基于现有编辑器管理服务获取当前 editor
- 给每个 editor 生成稳定 `EditorId`
- 对外只暴露 `EditorContextInfo`

建议内部维护 editor 到稳定 id 的映射。

### RuntimeAutomationScriptHost

职责：

- 调安全策略
- 调现有脚本执行器
- 切 UI 线程
- 绑定 editor
- 执行脚本
- 缓存最后结果

### DefaultScriptSecurityPolicy

职责：

- 文本级黑名单检查
- 后续可演进为 Roslyn 语义分析

## MCP Tool 示例结构

### editor.get_current

输入：

```json
{}
```

输出：

```json
{
  "editorId": "editor-1",
  "displayName": "song_4k.ogkr",
  "projectPath": "C:\\\\...\\\\test.nyagekiProj",
  "fumenPath": "C:\\\\...\\\\song_4k.ogkr",
  "isDirty": true
}
```

### editor.list_opened

输入：

```json
{}
```

输出：

```json
{
  "items": [
    {
      "editorId": "editor-1",
      "displayName": "song_4k.ogkr",
      "isDirty": true
    }
  ]
}
```

### script.compile

输入：

```json
{
  "scriptText": "var editor = ScriptArgs.TargetEditor; ..."
}
```

输出：

```json
{
  "success": true,
  "diagnostics": [],
  "securityIssues": []
}
```

### script.run_current_editor

输入：

```json
{
  "scriptText": "var editor = ScriptArgs.TargetEditor; ...",
  "expectedEditorId": "editor-1",
  "requireConfirmation": true,
  "wrapUndoTransaction": true,
  "transactionName": "MCP Script"
}
```

输出：

```json
{
  "success": true,
  "editorId": "editor-1",
  "transactionName": "MCP Script",
  "returnValueJson": "{\"created\":3}",
  "logs": [
    "已创建 3 个 Tap"
  ],
  "errorMessage": null
}
```

## MEF 声明方式

本项目当前是 MEF + `IoC.Get(...)` 风格，第一版新增模块也必须保持一致。

参考现有实现：

- [AppBootstrapper.cs](C:/Users/mikir/source/repos/OngekiFumenEditorPlugins.KngkSupport/refHost/OngekiFumenEditor/OngekiFumenEditor/AppBootstrapper.cs)
- [WindowTitleHelper.cs](C:/Users/mikir/source/repos/OngekiFumenEditorPlugins.KngkSupport/refHost/OngekiFumenEditor/OngekiFumenEditor/Utils/WindowTitleHelper.cs)
- [DefaultRenderManager.cs](C:/Users/mikir/source/repos/OngekiFumenEditorPlugins.KngkSupport/refHost/OngekiFumenEditor/OngekiFumenEditor/Kernel/Graphics/DefaultRenderManager.cs)
- [DefaultProgramUpdater.cs](C:/Users/mikir/source/repos/OngekiFumenEditorPlugins.KngkSupport/refHost/OngekiFumenEditor/OngekiFumenEditor/Kernel/ProgramUpdater/DefaultProgramUpdater.cs)

### 约定

- 服务类使用 `[Export(typeof(...))]`
- 单例服务使用 `[PartCreationPolicy(CreationPolicy.Shared)]`
- 优先使用 `[ImportingConstructor]`
- 不引入 `services.AddSingleton(...)`

### 示例

```csharp
[Export(typeof(IRuntimeAutomationScriptHost))]
[PartCreationPolicy(CreationPolicy.Shared)]
internal sealed class RuntimeAutomationScriptHost : IRuntimeAutomationScriptHost
{
    [ImportingConstructor]
    public RuntimeAutomationScriptHost(
        IRuntimeEditorContextProvider editorContextProvider,
        IScriptSecurityPolicy scriptSecurityPolicy,
        IEditorScriptExecutor editorScriptExecutor)
    {
        ...
    }
}
```

```csharp
[Export(typeof(IMcpServerHost))]
[PartCreationPolicy(CreationPolicy.Shared)]
internal sealed class McpServerHost : IMcpServerHost
{
    [ImportingConstructor]
    public McpServerHost(
        IRuntimeAutomationScriptHost scriptHost,
        IRuntimeEditorContextProvider editorContextProvider,
        EditorTools editorTools,
        ScriptTools scriptTools)
    {
        ...
    }
}
```

## 第一阶段实施顺序

推荐按以下顺序开工：

1. `RuntimeEditorContextProvider`
2. `DefaultScriptSecurityPolicy`
3. `RuntimeAutomationScriptHost.BuildAsync`
4. `RuntimeAutomationScriptHost.RunOnCurrentEditorAsync`
5. `EditorTools` / `ScriptTools`
6. `McpServerHost`

这样每一步都能单独验证，不会一开始就把 transport、脚本、UI 调度和协议层缠在一起。

## 第一阶段完成标准

第一阶段完成后，至少应满足：

- 能列出当前激活编辑器
- 能列出已打开编辑器
- 能对脚本文本做安全检查和编译
- 能将脚本执行到当前 editor
- 能返回结构化执行结果
- 能保留最后一次执行结果供 MCP 查询

## 开工后的下一个直接任务

建议下一步直接开始落代码，优先顺序如下：

1. 建立 `Kernel/RuntimeAutomation`
2. 建立 `Kernel/Mcp`
3. 先写接口和 DTO
4. 再写 `RuntimeEditorContextProvider`
5. 再写 `DefaultScriptSecurityPolicy`
6. 最后接 `RuntimeAutomationScriptHost`

