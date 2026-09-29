# Runtime Automation and MCP

## Architecture Split

* `Kernel/Mcp/` exposes tool-shaped MCP endpoints.
* `Kernel/RuntimeAutomation/` owns script-host execution, authorization, client tracking, and security policy. Editor lookups now go through `IEditorDocumentManager` (see `EditorDocumentManagerExtensions` / `EditorContextInfo.From`).
* Keep `Kernel/Mcp` thin. Put live-editor logic and script execution rules in `Kernel/RuntimeAutomation`.
* The main entry points today are `Kernel/Mcp/Tools/Editor/EditorTool.cs`, `Kernel/Mcp/Tools/Script/ScriptTool.cs`, and `Kernel/Mcp/McpServerHost.cs`.

## Current MCP Tool Surface

* Read-only editor tools:
* `editor.get_current`
* `editor.list_opened`
* `editor.get_current_summary`
* `editor.query_object` (pages one object family inside a TGrid range; returns runtime ids)
* Script tools:
* `script.compile`
* `script.run_current_editor`
* `script.run_editor`
* `script.get_last_result`
* Editor mutation tools (`Kernel/Mcp/Tools/Editor/EditorMutationTool.cs`):
* `editor.begin_action` / `editor.end_action` open and close an undo/redo combine scope per editor. Mutations issued in between are queued and applied by `end_action`, which reports each operation's outcome, rolls the whole batch back when one of them failed, and can discard the queue with `discard=true`.
* `editor.add_object` (tap/flick/comment/bpm) returns the new runtime object id.
* `editor.modify_object` sets one whitelisted property (tGridUnit, tGridGrid, xGridUnit, xGridGrid, isCritical, direction, content, bpm).
* `editor.remove_object` removes by runtime object id.
* `editor.scroll_to` moves the viewport/playback position.
* Outside an action scope these mutations apply immediately; inside one they report `applied=false, queued=true` and their real result arrives with `editor.end_action`.
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

* `IEditorDocumentManager` is the single source for editor lookups: `Kernel/Mcp/Tools/Editor/EditorTool.cs` reads `CurrentActivatedEditor` and a snapshot of `GetCurrentEditors()` directly.
* `EditorDocumentManagerExtensions` owns the shared helpers: `GetEditorSnapshot` (materialises the internal `HashSet` before enumerating it across threads) and `TryGetEditorById` (id comparison via `RuntimeAutomationEditorId`).
* `EditorContextInfo.From(viewModel)` projects a `FumenVisualEditorViewModel` into `EditorContextInfo`, which carries instance-scoped editor IDs, display names, file paths, dirty/active state, and lightweight object counts.
* Keep `EditorContextInfo` as the tool-facing result shape; automation that needs to mutate an editor should use the view model resolved through the document manager, not this DTO.

## Authorization Lane

* `McpToolAuthorizationService` registers clients, tracks remembered approvals, optionally rejects anonymous use, and can request a backup before script execution.
* `McpClientAuthorizationManager` keys remembered approvals by `clientId`, then `requestedBy`, then a shared anonymous identity.
* Interactive confirmation is the default for mutation tools.
* Program-level behavior is shaped by `ProgramSetting`: MCP enablement, listen port, anonymous-client policy, and `AllowAllMcpOperationsByDefault` (default off) - when enabled every tool call is approved without a dialog and the anonymous-client rejection is skipped, with `[MCP AUTH] ... "source":"setting"` in the log.

## Script Host Lane

* `RuntimeAutomationScriptHost` builds scripts, applies security checks, switches to the UI dispatcher, optionally wraps changes in an undo combine transaction, caches the last result, and can back up the target fumen file before execution.
* Read-only build failures and runtime failures return structured `ScriptBuildResult` and `ScriptRunResult` objects with error codes.
* For script authoring details, load `10-script-execution-surfaces.md`, `11-script-api-cheatsheet.md`, and `12-script-task-recipes.md`.
* `ScriptTool` exposes the main request knobs:
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
