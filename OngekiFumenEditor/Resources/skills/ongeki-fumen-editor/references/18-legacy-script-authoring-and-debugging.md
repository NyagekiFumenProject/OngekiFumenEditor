# Legacy Script Authoring And Debugging

Use this page when the right answer is a `.nyagekiScript` document instead of MCP runtime execution.

## When Legacy Scripts Are The Right Choice

Prefer the in-app script document flow when:

* the user wants a script file they can save and rerun manually
* the script needs `#r` assembly references
* the script needs `IoC.Get(...)` or other service-locator access
* the task depends on clipboard services, editor-only UI helpers, or trusted host access
* the user wants to open the script in an external IDE with project context

## Authoring Flow Inside The Editor

`EditorScriptDocumentViewModel` provides the document workflow:

* new documents are seeded from `Resources/NewScriptTemplate.nyagekiScript`
* the document keeps a list of currently opened target editors
* `Check` compiles the script and shows compile errors in a message box
* `Run` compiles, asks for confirmation, executes, and shows only success or failure
* `Reload File` reloads the saved script file from disk
* `VS Edit` generates a temp `net8.0-windows` project and watches the generated `.cs` file for changes
* the target-editor picker is fed from `IEditorDocumentManager.GetCurrentEditors()`, so it reflects live editor open/close events

## Compilation And Execution Model

`DefaultEditorScriptExecutor` compiles top-level C# Script with:

* all assemblies already loaded in the current AppDomain as references
* minimal default usings: `System`, `System.IO`, and `System.Diagnostics`
* support for `#r "path\\to\\assembly.dll"` directives
* `ScriptArgs.TargetEditor` injected through `ScriptArgsGlobalStore`

The executor returns an `ExecuteResult`, but the current document UI only reports success or the error message. It does not surface the returned object value in the run dialog.
That means legacy scripts are good for trusted execution, but not ideal when the user needs structured machine-readable output.

## External IDE Workflow

`VS Edit` uses `IDocumentContext.GenerateProjectFile(...)` to produce a temp `Script.csproj` that references currently loaded assemblies with `Private=false`.

This is useful when:

* the script needs Roslyn completion outside the in-app editor
* the user wants to inspect referenced APIs with normal IDE tooling
* the script is long enough that editing in AvalonEdit becomes inconvenient

The file watcher syncs the generated `.cs` file back into the `.nyagekiScript` document content.
`FileSystemWatcher` only becomes active after that workflow starts, so plain in-editor editing does not mirror from any external source.

## Debugging Guidance

* For compile failures, inspect the `BuildResult.Errors` shown by the `Check` or `Run` path.
* For runtime failures, inspect `ExecuteResult.ErrorMessage`.
* For ordinary trace output, use `Log.LogInfo(...)`.
* For lower-level trace output, use `Log.LogDebug(...)`.
* If the script stops seeing the expected target editor, reselect the document's current target editor before running again.
* If the generated temp project stops syncing, inspect the watcher-backed `VS Edit` workflow before assuming the script text itself is wrong.

## Model Guidance

* If the user asks for a reusable script artifact, emit `.nyagekiScript` content rather than a runtime automation snippet.
* If the script requires trusted host services or file references, choose legacy scripting explicitly instead of trying to squeeze it into runtime policy.
* If the user only wants the live result right now and the task fits runtime policy, prefer MCP runtime automation instead because it has clearer execution reporting and guardrails.
