# Request Routing And Script Strategy

Use this page when translating a user request into the correct execution surface instead of jumping straight into code.

## First Decision: Live Operation Or Product Change

* If the user wants a one-off operation on currently opened chart data, prefer scripts or MCP editor tools.
* If the user wants a reusable editor feature, parser change, rendering change, or persistent workflow improvement, patch source code instead.
* If the user is already asking about repository files, commands, menus, or view models, do not detour through scripts. That is a source-edit task.

## Second Decision: Read-Only Or Mutating

* Read-only requests:
  prefer MCP editor tools first, especially `editor.get_current`, `editor.get_current_summary`, and `editor.list_opened`.
  Use a script only when the built-in editor tools do not expose the exact aggregate or traversal the user needs.

* Mutating requests:
  use runtime automation when the task is being executed through MCP and the change fits the runtime security policy.
  use a legacy `.nyagekiScript` document when the workflow depends on `#r`, `IoC.Get(...)`, clipboard services, or editor-only interactive tooling.
  if the task depends on selection, clipboard, or richer viewport semantics, also load `19-selection-clipboard-and-viewport-operations.md`.

## Surface Selection Rules

* `editor.*` MCP tools:
  best for inspection, editor discovery, object counts, active-file checks, and lightweight summaries.
  best first step when you need an editor id before a later `script.run_editor` call.

* `script.compile`:
  best when the runtime script is non-trivial, when the policy may reject it, or when you want diagnostics before asking for execution.

* `script.run_current_editor` or `script.run_editor`:
  best for one-shot live chart mutations that can be expressed with `UndoRedoManager.ExecuteAction(...)` and without blocked APIs.

* legacy `.nyagekiScript`:
  best for trusted in-editor workflows, richer host access, `IoC.Get(...)`, `#r`, clipboard use, or when the user explicitly wants a script file they can keep and rerun manually.

* source edit:
  best for repeatable product behavior, new menu items, new parsers, rendering changes, or anything that should survive after the current session.

## Translation Checklist

1. Identify the target editor.
   If the task is runtime-driven, capture the current editor id first and keep it in `expectedEditorId` when stability matters.
   `editor.get_current` or `editor.get_current_summary` is the usual way to capture that id.

2. Identify the user's coordinate language.
   If the request is phrased in seconds, go through `TGridCalculator`.
   If it is phrased in bars or beats, stay in `TGrid`.
   If it is phrased as "selected objects" or "visible area", load the selection and viewport references.

3. Identify the object family.
   Choose the runtime type from `13-script-object-type-index.md` and the important fields from `17-common-object-property-index.md`.

4. Decide whether runtime policy permits the plan.
   If the script needs `IoC.Get(...)`, `#r`, clipboard services, or other blocked APIs, move to legacy script authoring instead of fighting the runtime policy.
   If the request only needs file paths, counts, or active-editor discovery, move back to `editor.*` tools instead of writing a script.

5. Execute with a transaction name that describes the user intent.
   Runtime example: `"Batch add taps from timestamps"`.

6. Report outcome in user terms.
   Include counts, target editor name, affected time range, and any blocked-policy or compile diagnostics.

## Practical Mapping Examples

* "Count taps and holds in the active editor":
  use `editor.get_current_summary` first.

* "Add 50 taps at these timestamps in the current editor":
  use `script.run_current_editor` with `TGridCalculator.ConvertAudioTimeToTGrid(...)`.

* "Mirror the current selection and paste it around X=0":
  use a legacy script, because clipboard and richer editor interaction are easier there.

* "Open a script file I can modify in Visual Studio and rerun later":
  generate a `.nyagekiScript` and point the user to the legacy document workflow.

* "Add a permanent menu command that does this":
  patch source code, not a script.

## Failure Handling

* If `script.compile` reports security issues, either rewrite into the allowed undo pattern or switch to legacy script authoring if the task truly needs the blocked capability.
* If runtime execution fails because the editor changed, reacquire the editor id and rerun only if the user still wants the change on the new active editor.
* If the same request keeps recurring, stop producing one-off scripts and implement a real feature.
