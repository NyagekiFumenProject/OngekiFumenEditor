# Runtime Script Safety And Undo Patterns

Use this page when fulfilling a request through MCP runtime automation instead of the legacy `.nyagekiScript` document surface.

## Runtime Tool Contract

The MCP runtime layer exposes four distinct operations:

* `script.compile`: compile and security-check a runtime script without executing it.
* `script.run_current_editor`: execute against the currently active editor.
* `script.run_editor`: execute against a specific opened editor id.
* `script.get_last_result`: retrieve the last cached `ScriptRunResult`.

`script.run_current_editor` and `script.run_editor` both accept:

* `expectedEditorId`: fail fast if the target editor changed.
* `requireConfirmation`: ask for execution approval.
* `wrapUndoTransaction`: combine nested undo actions under one outer transaction.
* `transactionName`: label the operation in undo history.
* `requestedBy` and `clientId`: audit and authorization metadata.

## What The Host Adds

Runtime automation is not a thin call into Roslyn. The host also adds:

* authorization through `IMcpToolAuthorizationService`
* optional backup of the current fumen file before execution
* dispatcher marshaling onto the UI thread
* security validation before build
* last-result caching

Design scripts assuming those protections exist, and do not try to bypass them.

## Security Policy Constraints

The runtime security policy blocks scripts containing these tokens or patterns:

* `#r`
* `System.Reflection`
* `Activator.CreateInstance`
* `Assembly.Load`
* `Type.GetType`
* `MethodInfo`, `PropertyInfo`, `FieldInfo`
* `System.Diagnostics.Process`
* `System.IO.File`, `System.IO.Directory`
* `System.Net`
* `Caliburn.Micro.IoC` and `IoC.Get`
* `ScriptArgsGlobalStore`
* `IEditorDocumentManager`
* `FumenVisualEditorViewModel`

In addition, the policy requires an undoable mutation shape:

* the script must contain `UndoRedoManager.ExecuteAction(...)`
* that call must receive exactly one inline `LambdaUndoAction.Create(name, redo, undo)` or `new LambdaUndoAction(name, redo, undo)`
* `ScriptArgs.TargetEditor` is only allowed inside the redo and undo lambdas

## Preferred Workflow

1. If the script is non-trivial, run `script.compile` first.
2. If the request is read-only and can be answered by MCP editor tools, prefer `editor.get_current`, `editor.get_current_summary`, or `editor.list_opened`.
3. If the request must mutate chart state, wrap the change in an explicit undoable action.
4. Use `expectedEditorId` when the request depends on the currently inspected editor remaining unchanged.
5. Check `ScriptRunResult.Success`, `ErrorCode`, `ErrorMessage`, `Diagnostics`, and `Logs` before reporting completion.

## Allowed Mutation Skeleton

```csharp
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.EditorScriptExecutor.Scripts;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;

Tap tap = null;

ScriptArgs.TargetEditor.UndoRedoManager.ExecuteAction(
    LambdaUndoAction.Create(
        "Add tap from MCP",
        () =>
        {
            var editor = ScriptArgs.TargetEditor;
            var fumen = editor.Fumen;
            tap ??= new Tap
            {
                TGrid = new TGrid(8, 0),
                XGrid = XGrid.Zero,
            };

            fumen.AddObject(tap);
            editor.ScrollTo(tap);
        },
        () =>
        {
            var editor = ScriptArgs.TargetEditor;
            if (tap is not null)
                editor.Fumen.RemoveObject(tap);
        }));
```

The example is intentionally repetitive: reacquiring `ScriptArgs.TargetEditor` inside each lambda is the simplest way to satisfy the security policy.

## Common Failure Causes

* Accessing `ScriptArgs.TargetEditor` before entering the redo or undo lambda.
* Using `#r`, `IoC.Get`, file I/O, process launch, reflection, or network APIs.
* Building a mutation script without an explicit `UndoRedoManager.ExecuteAction(...)`.
* Assuming editor ids are durable across sessions. They are runtime-instance identifiers only.
* Returning a complex object that JSON serialization cannot handle cleanly. Prefer plain DTOs, arrays, numbers, strings, and booleans.

## Reporting Results Back To The User

Treat `ScriptRunResult` as the final truth for MCP execution:

* `Success = true` means the script ran.
* `ReturnValueJson` is the serialized return payload, if any.
* `Logs` contains runtime host messages and fallback-serialization notices.
* `Diagnostics` contains build diagnostics with line and column when available.
* `ErrorCode` and `ErrorMessage` explain authorization, build, security, backup, or runtime failures.

If a runtime script is blocked by policy and the task still matters, either rewrite it to fit the allowed pattern or fall back to a legacy `.nyagekiScript` document or source-code change, depending on the user request.
