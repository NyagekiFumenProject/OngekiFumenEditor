# MCP tool end-to-end tests

End-to-end tests for the editor's MCP tool surface (`editor.*` and `script.*`).

Unlike the unit tests under `Avalonia/tests/`, these drive a **running editor instance**
over the same HTTP/JSON-RPC endpoint an MCP client uses, so they verify the real contract:
tool registration, argument parsing, error codes, undo/redo behaviour and the served skill
documentation.

## Why this exists

The tool surface is large (28 tools) and most of it is only reachable at runtime — the MCP
host starts inside the editor process, not in a test host. Anything that asserts on tool
names, parameters or history semantics has to talk to a live instance, so the suite lives
here instead of in a unit-test project.

## Running

1. Build and start the editor. The MCP server auto-starts in GUI mode only when the
   per-executable setting `EnableMcpServerInGUIMode` is enabled, and it listens on
   `http://127.0.0.1:39281/mcp`.

   ```bash
   dotnet build OngekiFumenEditor/OngekiFumenEditor.csproj -c Debug
   OngekiFumenEditor/bin/Debug/net10.0-windows/OngekiFumenEditor.exe
   ```

2. Run the suites (Node 20+; no npm install required — there are no dependencies):

   ```bash
   node tests/mcp-e2e/run.mjs                 # everything
   node tests/mcp-e2e/run.mjs 20 30           # only suites starting with 20 / 30
   node tests/mcp-e2e/run.mjs --list          # list suites
   node tests/mcp-e2e/run.mjs --url http://127.0.0.1:39281/mcp
   ```

The runner exits non-zero when any check fails.

## What the suites do to your editor

The runner takes over the editor instance it connects to:

* every opened editor is closed (`force: true`) before the run starts
* a copy of a repository chart sample is opened, and all mutations happen on that copy
* the `.tmp/` working directory holds the generated fixtures and is gitignored

Do not point it at an editor window you care about. Every suite cleans up after itself and
leaves exactly one seeded editor open.

## Fixtures

`lib/env.mjs` generates everything the suites need, so a clean checkout can run them:

| fixture | source |
| --- | --- |
| `seed.ogkr`, `second.ogkr` | copies of `OngekiFumenEditor.Benchmark/Data/FumenSamples/*.ogkr` |
| `silence-30s.wav` | synthesised 16-bit PCM WAV (no external audio assets) |
| `fixture.nyagekiProj` | minimal project file pointing at the two files above (its `Id` must be a well-formed GUID, otherwise deserialization throws and the project silently opens empty) |

## Verified contract notes

Behaviours the suites pin down — the details that are easy to get wrong:

* **Argument types must match the C# signature exactly.** `propertyName` / `newValue` are
  *strings*: send booleans and numbers as text (`"true"`, `"12"`). A mismatched JSON type
  (e.g. `true`) fails parameter binding and surfaces as the opaque
  `An error occurred invoking 'editor.x'` — the tool body never runs, so nothing is logged.
* **`script.*` return values arrive as `returnValueJson`**, a JSON *string*; `lib/api.mjs`
  decodes it into `payload.result`. A run that failed before executing has no return value, so
  `result` is explicitly `null`.
* **A wrapped script run is one history entry named after `transactionName`** (default
  `MCP Script`), not after the `LambdaUndoAction` name used inside the script.
* **A script may re-read `ScriptArgs.TargetEditor` inside its queued `LambdaUndoAction`**, and
  the resulting entry stays undoable long afterwards — the host keeps the target editor
  resolvable outside the executor's registration window (suite 80 is the regression guard).
* **`editor.scroll_to` clamps to the loaded audio duration** and never touches history; two
  scroll targets derived from the chart's length can therefore collapse onto the same position.
* **Only a property change marks a chart dirty** (`OngekiFumen.ObjectModifiedChanged`), so
  `add_object` alone leaves `isDirty` false while `modify_object` on the same object flips it.
  `editor.close` refuses a dirty editor with `EDITOR_DIRTY` unless `force: true`.
* **`editor.open_proj` returns before its chart and audio finish loading** — poll
  `editor.get_current_summary` until `counts` are populated (suite 90 does exactly this).

## Layout

```
run.mjs                     entry point: fixtures, editor session, suite loop, exit code
lib/client.mjs              MCP client over Streamable HTTP (JSON-RPC + SSE)
lib/api.mjs                 typed wrapper per tool; the only place tool names appear
lib/harness.mjs             assertions and the per-suite context
lib/history.mjs             apply -> undo -> redo -> undo verification helpers
lib/env.mjs                 fixture generation
lib/coverage.mjs            the expected tool surface (28 tools) + mutating/read-only split
suites/00-tool-surface.mjs  tools/list + resources/read contract, required-parameter contract
suites/10-discovery.mjs     get_current / list_opened / get_current_summary / query_object
suites/20-object-crud.mjs   add_object / modify_object / remove_object + undo/redo
suites/30-connectable.mjs   lanenext / beam / beamnext / curvecontrol / isfarea / laneblock / colorful
suites/40-hold-end.mjs      create_hold_end / remove_hold_end + undo/redo
suites/50-action-scope.mjs  begin_action / end_action / undo / redo
suites/60-bullet-pallete.mjs create / query / modify / remove_bullet_pallete + undo/redo
suites/70-metainfo-check.mjs set_metainfo / check + undo/redo
suites/80-scroll-script.mjs scroll_to / script.compile / run_editor / run_current_editor / get_last_result
suites/90-document-lifecycle.mjs create_proj / open_fast / open_proj / close
suites/99-coverage.mjs      asserts every tool has a suite and every mutating tool has undo/redo
```

## Adding a tool

1. Add the tool name to `EXPECTED_TOOLS` in `lib/coverage.mjs` (and to `MUTATING_TOOLS`
   or `READ_ONLY_TOOLS`).
2. Add a wrapper in `lib/api.mjs`.
3. Extend the relevant suite — or add a new one — and call `ctx.cover('editor.your_tool')`.
4. If the tool changes the chart, give it an `undo/redo` case with `verifyUndoRedo` (or
   `ctx.coverUndoRedo` when the cycle is bespoke).

Suite `99-coverage` fails the run if step 1 or step 3 is missing, so the matrix cannot
silently rot.
