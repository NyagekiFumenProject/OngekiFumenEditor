# Script Execution Surfaces

Use this page when the user request is better satisfied by executing a live script against an opened editor than by patching the source tree.

For task routing, legacy in-app authoring, object-property lookup, or selection/clipboard behavior, also read `16-request-routing-and-script-strategy.md`, `17-common-object-property-index.md`, `18-legacy-script-authoring-and-debugging.md`, and `19-selection-clipboard-and-viewport-operations.md`.

## When to Prefer Scripts

Prefer scripts when the user wants one-off or batch operations on currently opened chart data, for example:

* inspect the active editor and report counts or paths
* add, remove, retime, recolor, or relink chart objects
* generate lane chains or note patterns from input data
* move the editor viewport or recalculate derived editor state after a transformation

Prefer source-code edits instead when the user wants a reusable product feature, a UI change, a new parser/formatter capability, or a persistent behavioral change.

## Two Script Surfaces

### 1. In-app script documents: `.nyagekiScript`

Use the `Modules/EditorScriptExecutor` flow when the task is interactive inside the editor UI.

Key facts:

* Script files use the `.nyagekiScript` extension.
* New scripts are seeded from `Resources/NewScriptTemplate.nyagekiScript`.
* The editor lets the user compile, choose a target editor, run the script, and even open a generated temp project for IDE editing and completion.
* `DefaultEditorScriptExecutor` compiles Roslyn C# Script against assemblies already loaded in the current AppDomain.
* `#r "path\\to\\assembly.dll"` reference directives are supported here.
* There is no runtime-automation security policy on this path, so the script has broad power. Treat it as full trust.
* `Modules/EditorScriptExecutor/Documents/ViewModels/EditorScriptDocumentViewModel.cs` is the main document workflow:
  `OnCheckButtonClicked()` compiles,
  `OnRunButtonClicked()` compiles and executes against the selected editor,
  `OnVSEditButtonClicked()` generates a temp project and watches the generated `.cs` file for resync.

### 2. Runtime automation / MCP scripts

Use the runtime-automation path when the task is being driven through MCP or another assistant-controlled execution flow.

Key facts:

* `Kernel/RuntimeAutomation/RuntimeAutomationScriptHost` still uses `IEditorScriptExecutor` underneath.
* Before execution it applies authorization, optional user confirmation, optional fumen backup, UI-dispatch execution, result caching, and `DefaultScriptSecurityPolicy`.
* `script.compile` is the cheap validation step.
* `script.run_current_editor` and `script.run_editor` are the mutation paths.
* `script.get_last_result` exposes the last structured execution result.
* `#r`, `IoC.Get(...)`, reflection, process launching, and direct host-escape patterns are intentionally blocked or discouraged on this path.
* The runtime surface still uses the same underlying editor-script compiler, so many build errors reproduce across both surfaces. The main difference is authorization, security policy, UI-dispatch execution, backup, and result caching.

## Shared Compilation Model

Both surfaces compile top-level C# Script into a dynamic assembly.

Important implications:

* Top-level statements are the script body.
* The script entry point may `return` a value.
* Default global usings are very small: `System`, `System.IO`, and `System.Diagnostics`.
* Most project namespaces still need explicit `using` lines.
* `ScriptArgs.TargetEditor` is the primary injected live-editor handle.
* In the legacy path, target-editor injection flows through the editor script executor and `ScriptArgsGlobalStore`.

## Portable Script Subset

When possible, write scripts against the stricter runtime-automation subset so they can be reused across both surfaces.

Use this subset:

* add explicit `using` lines for project namespaces you need
* enter through `ScriptArgs.TargetEditor.UndoRedoManager.ExecuteAction(...)`
* access `ScriptArgs.TargetEditor` only inside the `ExecuteAction(...)` expression and its redo/undo lambdas
* for mutations, use `UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(...))`
* keep redo and undo explicit
* return only serializable values such as strings, numbers, arrays, or anonymous objects
* prefer project APIs reachable from `TargetEditor`, `Fumen`, and static helpers over service-locator access

Avoid this subset for portable scripts:

* `IoC.Get(...)`
* `#r`
* direct file/network/process work
* reflection-heavy code
* returning editor/view-model instances

## Current Runtime Caveat

The current `DefaultScriptSecurityPolicy` is stricter than an ideal read/write split:

* if a runtime-automation script touches `ScriptArgs.TargetEditor`, it is expected to do so through `UndoRedoManager.ExecuteAction(...)`
* because of that, pure read-only inspection is usually better served by MCP editor tools such as `editor.get_current`, `editor.list_opened`, and `editor.get_current_summary`
* use scripts for runtime automation mainly when you actually need to mutate live editor/chart state

## Practical Surface Heuristic

* Need a script file the user can keep, edit in an IDE, or rerun from the editor:
  use `.nyagekiScript`.
* Need assistant-driven live mutation with structured success/failure reporting:
  use runtime automation.
* Need only editor facts or counts:
  use `editor.*` MCP tools before reaching for a script.

## Decision Rule

* If the user asks to transform the current chart once: write and run a script.
* If the user asks to inspect current editor state: use a read-only script or MCP editor tools.
* If the user asks for a reusable editor feature: patch source code instead.
