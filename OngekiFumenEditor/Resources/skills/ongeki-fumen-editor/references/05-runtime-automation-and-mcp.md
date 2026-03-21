# Runtime Automation and MCP

## Architecture Split

* `Kernel/Mcp/` exposes tool-shaped MCP endpoints.
* `Kernel/RuntimeAutomation/` owns editor-context lookup, script-host execution, authorization, client tracking, and security policy.
* Keep `Kernel/Mcp` thin. Put live-editor logic and script execution rules in `Kernel/RuntimeAutomation`.
* The main entry points today are `Kernel/Mcp/EditorTools.cs`, `Kernel/Mcp/ScriptTools.cs`, and `Kernel/Mcp/McpServerHost.cs`.

## Current MCP Tool Surface

* Read-only editor tools:
* `editor.get_current`
* `editor.list_opened`
* `editor.get_current_summary`
* Script tools:
* `script.compile`
* `script.run_current_editor`
* `script.run_editor`
* `script.get_last_result`
* `editor.get_current_summary` returns the most useful stable summary shape for assistants:
  editor id, display name, project path, fumen path, dirty/active flags, and lightweight object counts.

## Current MCP Resource Surface

* `Kernel/Mcp/SkillResources.cs` exposes built-in repo guidance as read-only MCP resources.
* On connection, the server sends short discovery instructions that point clients at `skill://index`.
* Packaged skill files are also registered as direct resources so clients can discover real URIs through `resources/list`, not only through resource templates.
* The normal read flow is:
  `skill://index` -> `skill://ongeki-fumen-editor/index` -> `skill://ongeki-fumen-editor/SKILL.md`.
* Reference pages and agent metadata are then readable through URIs like:
  `skill://ongeki-fumen-editor/references/05-runtime-automation-and-mcp.md` and `skill://ongeki-fumen-editor/agents/openai.yaml`.
* This resource surface is guidance-only. Live editor state and script execution still flow through tools and `Kernel/RuntimeAutomation/`.

## Editor Context Lane

* `RuntimeEditorContextProvider` converts `FumenVisualEditorViewModel` instances into `EditorContextInfo`.
* `EditorContextInfo` carries instance-scoped editor IDs, display names, file paths, dirty/active state, and lightweight object counts.
* Use this provider when MCP or automation needs editor facts without exposing the full view model.

## Authorization Lane

* `McpToolAuthorizationService` registers clients, tracks remembered approvals, optionally rejects anonymous use, and can request a backup before script execution.
* `McpClientAuthorizationManager` keys remembered approvals by `clientId`, then `requestedBy`, then a shared anonymous identity.
* Interactive confirmation is the default for mutation tools.
* Program-level behavior is shaped by `ProgramSetting`, especially MCP enablement, listen port, and anonymous-client policy.

## Script Host Lane

* `RuntimeAutomationScriptHost` builds scripts, applies security checks, switches to the UI dispatcher, optionally wraps changes in an undo combine transaction, caches the last result, and can back up the target fumen file before execution.
* Read-only build failures and runtime failures return structured `ScriptBuildResult` and `ScriptRunResult` objects with error codes.
* For script authoring details, load `10-script-execution-surfaces.md`, `11-script-api-cheatsheet.md`, and `12-script-task-recipes.md`.
* `ScriptTools` exposes the main request knobs:
  `expectedEditorId`, `requireConfirmation`, `wrapUndoTransaction`, `transactionName`, `requestedBy`, and `clientId`.
* `script.run_current_editor` and `script.run_editor` default to `requireConfirmation = true` and `wrapUndoTransaction = true`.

## Security Policy

* `DefaultScriptSecurityPolicy` blocks reflection, process launching, direct file/network APIs, direct `IoC.Get(...)`, and other host-escape surfaces.
* It also enforces an undoable mutation pattern: chart mutation must flow through `UndoRedoManager.ExecuteAction(...)` with explicit redo and undo lambdas.
* `ScriptArgs.TargetEditor` is only allowed inside the `ExecuteAction(...)` call path and its redo/undo lambdas.

## Script Shape

```csharp
ScriptArgs.TargetEditor.UndoRedoManager.ExecuteAction(
    LambdaUndoAction.Create(
        "Example",
        () =>
        {
            var editor = ScriptArgs.TargetEditor;
            // mutate editor.Fumen here
        },
        () =>
        {
            var editor = ScriptArgs.TargetEditor;
            // undo the mutation here
        }));
```

That shape is not just style guidance. It matches what the runtime security validator and host orchestration currently expect.

## Server, Menu, And Shell Surfaces

* GUI startup can auto-start MCP through `AppBootstrapper.TryStartMcpServerAsync()` when `ProgramSetting.Default.EnableMcpServerInGUIMode` is enabled.
* `Kernel/Mcp/MenuDefinitions.cs` and `Kernel/Mcp/Commands/McpMenuCommandHandlers.cs` provide the visible shell surface for start/stop/copy-url/revoke-client actions.
* `McpServerHost.ServerUrl` is the normal shell-facing server endpoint string.

## Failure Shapes To Expect

* Read-only tools can return authorization-denied objects with `success = false`, `errorCode`, and `errorMessage`.
* `editor.get_current_summary` returns `NO_ACTIVE_EDITOR` when no editor is active.
* Mutation tools report failures through `ScriptRunResult.Success`, `ErrorCode`, `ErrorMessage`, `Diagnostics`, and `Logs`.

## Change Guidance

* Add new editor-derived facts in `RuntimeAutomation` first if they depend on live editor state.
* Keep MCP tool classes focused on parameter handling, authorization preview text, and result shaping.
* If a feature only changes shell behavior, prefer updating menu handlers or host lifecycle wiring instead of expanding the tool surface.
* Compile first when debugging policy failures; run only after the script shape passes security checks.
