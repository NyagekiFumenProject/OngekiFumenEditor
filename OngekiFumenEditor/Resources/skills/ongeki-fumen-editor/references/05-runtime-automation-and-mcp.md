# Runtime Automation and MCP

## Architecture Split

* `Kernel/Mcp/` exposes tool-shaped MCP endpoints.
* `Kernel/RuntimeAutomation/` owns script-host execution, authorization, client tracking, and security policy. Editor lookups now go through `IEditorDocumentManager` (see `EditorDocumentManagerExtensions` / `EditorContextInfo.From`).
* Keep `Kernel/Mcp` thin. Put live-editor logic and script execution rules in `Kernel/RuntimeAutomation`.
* The main entry points today are `Kernel/Mcp/Tools/Editor/EditorTool.cs` (read-only discovery), `Kernel/Mcp/Tools/Editor/EditorMutationTool*.cs` (object/pallete/history mutation), `Kernel/Mcp/Tools/Editor/EditorDocumentTool*.cs` (open/create/close), `Kernel/Mcp/Tools/Script/ScriptTool.cs`, and `Kernel/Mcp/McpServerHost.cs`.

## Current MCP Tool Surface

28 tools are registered today. Every tool accepts the shared routing/auth parameters
`requestedBy`, `clientId` and `cancellationToken`; editor-scoped tools also accept
`editorId` (defaults to the active editor) and `expectedEditorId` (guards against a
mid-flight editor switch); mutating tools add `requireConfirmation` (default `true`).

### Editor discovery and read-only

| Tool | Purpose |
| --- | --- |
| `editor.get_current` | The active editor: id, display name, paths, dirty/active flags, object counts. |
| `editor.list_opened` | All opened editors, for picking an `editorId`. |
| `editor.get_current_summary` | Stable lightweight summary of the active editor (`NO_ACTIVE_EDITOR` when none). |
| `editor.query_object` | Page one object family inside a TGrid range; returns runtime ids plus the DTO below. |
| `editor.check` | Run every built-in fumen check rule and return the results (see "Fumen Check" below). Read-only. |

### Editor document lifecycle

| Tool | Purpose |
| --- | --- |
| `editor.open_fast` | Fast-open a chart (`.ogkr`/`.nyageki`); resolves the audio next to it, or pass `audioPath`. |
| `editor.open_proj` | Open a `.nyagekiProj` project; returns once the new editor reports Ready. |
| `editor.create_proj` | New project from an audio file, optionally seeded with a chart (`baseBpm` defaults to the chart's first BPM). |
| `editor.close` | Close an editor tab. **Never saves**, is not undoable, and shows no dialogs. |

`editor.close` details:

* Omit `editorId` to close the active editor.
* A dirty editor is refused with `EDITOR_DIRTY` so the caller can save or `editor.undo` first; pass `force=true` to discard the unsaved changes and close anyway.
* The response reports `closed`, `wasDirty`, `discardedUnsavedChanges`, `remainingEditorCount`, `remainingEditorIds` and `activeEditorId`.
* Internally it raises a one-shot bypass flag on the view model (`RequestCloseWithoutPrompt`) so the Caliburn `CanCloseAsync` save prompt is skipped exactly once, then awaits `TryCloseAsync`. Other errors: `NO_ACTIVE_EDITOR`, `EDITOR_NOT_FOUND`, `EDITOR_CHANGED`, `CLOSE_FAILED`.

### Object mutation

| Tool | Purpose |
| --- | --- |
| `editor.add_object` | Add one object (see the family cheat-sheet); returns the runtime object id. |
| `editor.modify_object` | Set one whitelisted property of an object (see the property cheat-sheet). |
| `editor.remove_object` | Remove an object by runtime id. |
| `editor.scroll_to` | Move the viewport/playback to a TGrid position (design mode also moves playback). |
| `editor.create_hold_end` | Attach a HoldEnd to a hold addressed by its runtime id (gives it length). |
| `editor.remove_hold_end` | Drop a hold's end (the hold becomes zero-length again). |

### Fumen meta info

| Tool | Purpose |
| --- | --- |
| `editor.set_metainfo` | Set one chart meta info field (see "Fumen Meta Info" below). One undoable editor action. |

### Action scope and history

| Tool | Purpose |
| --- | --- |
| `editor.begin_action` | Open an undo/redo combine scope per editor. |
| `editor.end_action` | Apply the queued batch as one undo entry, or drop it with `discard=true`. |
| `editor.undo` / `editor.redo` | Step the history; report the entry name and updated counts. |

* Outside an action scope mutations apply immediately; inside one they report `applied=false, queued=true` and their real outcome (per operation, with whole-batch rollback on any failure) arrives with `editor.end_action`.
* `editor.end_action` also accepts `name` for the combined history entry.

### Bullet palletes

| Tool | Purpose |
| --- | --- |
| `editor.create_bullet_pallete` | Create a BPL and return its StrID (allocated up-front, so it survives a discarded scope as an unused id). |
| `editor.modify_bullet_pallete` | Edit editorName/shooter/target/size/type/speed/placeOffset/randomOffsetRange. |
| `editor.remove_bullet_pallete` | Remove a BPL; refused with `PALLETE_IN_USE` while any bullet/bell still references it. |
| `editor.query_bullet_pallete` | Detail one StrID, or page all palletes (`editorNameContains` filter). |

### Script

| Tool | Purpose |
| --- | --- |
| `script.compile` | Compile a script without running it. |
| `script.run_current_editor` / `script.run_editor` | Run against the active / a named editor (default `requireConfirmation=true`, `wrapUndoTransaction=true`). |
| `script.get_last_result` | Fetch the last script result. |

## Object Family, Property And Value Cheat-Sheet

`editor.add_object` and `editor.query_object` accept the same twelve families
(`CreatableFamilies` in `EditorMutationTool.cs`):
`tap`, `flick`, `comment`, `bpm`, `bullet`, `bell`, `meter`, `clickse`, `enemy`, `lane`, `hold`, `soflan`.

Family-specific add arguments:

* `bullet` / `bell` — `bulletPalleteStrId` (required for bullet; optional for bell, where `--` means the Ongeki default bell).
* `meter` — `meterBunShi` / `meterBunbo` (default 4/4).
* `enemy` — `enemyWave` (`Wave1` / `Wave2` / `Boss`, default `Boss`).
* `lane` — `laneType` (`center` default, plus `left`, `right`, `colorful`, `enemy`, `wallLeft`, `wallRight`).
* `hold` — optional `endTGridUnit` / `endTGridGrid`, or attach the end later with `editor.create_hold_end`.
* `soflan` — `soflanType` (`duration` default, or `interpolatable`, `keyframe`) plus `speed`, `soflanGroup`, `applySpeedInDesignMode`. `duration`/`interpolatable` require `endTGridUnit`/`endTGridGrid`; `keyframe` is a single point and forbids them.

`editor.modify_object` whitelist (`SupportedModifyProperties`):
`tGridUnit`, `tGridGrid`, `xGridUnit`, `xGridGrid`, `isCritical`, `direction`, `content`, `bpm`, `bulletPallete`, `bunShi`, `bunbo`, `enemyWave`, `endTGridUnit`, `endTGridGrid`, `speed`, `soflanGroup`, `applySpeedInDesignMode`, `referenceLaneRecordId`.

* `bulletPallete` — bullet/bell only; value is a pallete StrID (`""` or `--` clears a bell's pallete).
* `bunShi`/`bunbo` — meter; `enemyWave` — enemy; `endTGrid*` — hold with an end, or any soflan; `speed`/`soflanGroup`/`applySpeedInDesignMode` — soflan.
* `referenceLaneRecordId` — tap/hold only (see Lane Docking below); `""` or a negative value clears the binding.

`editor.query_object` DTO fields (per object): `id`, `type`, `tGrid{unit,grid,totalGrid}`,
`xGrid{unit,grid,totalGrid}`, `isCritical`, `bulletPalleteStrId`, `referenceLaneRecordId`
(null when floating), `meterBunShi`, `meterBunbo`, `enemyWave`, `laneType`, `recordId`
(lane only — this is the value you pass back as `referenceLaneRecordId`), `hasHoldEnd`,
`endTGrid{unit,grid,totalGrid}`, `soflanType`, `soflanSpeed`, `soflanGroup`, `applySpeedInDesignMode`.
TGrid/XGrid totals are reported in the editor's internal scale.

## Lane Docking (tap / hold)

Only `tap` and `hold` implement `ILaneDockable`. A lane's `RecordId` is its stable
identity (not its runtime id); resolve it against `fumen.Lanes`.

* **Bind** — `editor.add_object` with `referenceLaneRecordId`, or `editor.modify_object` with property `referenceLaneRecordId` and the RecordId as `newValue`. An omitted, negative, `""` or `"null"` value leaves the object floating.
* **Snap** — add `snapXToLane: true` to re-derive the XGrid from the lane at the object's TGrid: a tap gets its `XGrid`; a hold gets both `XGrid` and its `HoldEnd.XGrid`. Snapping is strict — when the lane has no path at that TGrid the call fails (`INVALID_ARGUMENT`) rather than silently keeping the old XGrid.
* Snapping also works while moving: `editor.modify_object` accepts `snapXToLane` together with `referenceLaneRecordId`, `tGridUnit` or `tGridGrid`.
* On undo both the property and every snapped XGrid are rolled back together (captured via `DockableXGridSnapshot`).

Failure codes: `LANE_NOT_FOUND` (unknown RecordId), `INVALID_ARGUMENT` (lane args on a
non-dockable family, `snapXToLane` without a lane, snap on a non-snappable property,
snap combined with clearing the lane, snap on an unbound object, or the lane has no path
at that TGrid).

## Fumen Check (`editor.check`)

Runs exactly the rules the **FumenCheckerListViewer** tool runs: it resolves every
`IFumenCheckRule` export (`IoC.GetAll<IFumenCheckRule>()`) and calls `CheckRule(fumen, context)`
on the UI thread. It is **read-only** — it never modifies the chart — so call it freely.

Response shape:

* `ruleCount` — how many rules ran; `ruleFailures[]` — rules that threw (a broken rule can never
  fail the whole check).
* `total` / `errorCount` / `problemCount` / `suggestCount` — counts over **all** results.
* `results[]` — ordered by severity (`error` → `problem` → `suggest`), then by TGrid. Each entry has
  `ruleName`, `severity` (`error` / `problem` / `suggest`), `description`, `location`, plus the
  structured target: `objectType` + `objectId` when the rule points at an object, or
  `tGrid{unit,grid,totalGrid}` when it points at a position. Either may be `null` — some rules
  (e.g. the header-const one) only carry a human `location` string.
* `matched` / `returned` / `truncated` — after the filter and limit.

Parameters:

* `minSeverity` is a **floor**, not an exact match: `problem` returns problems *and* errors,
  `error` returns only errors, `suggest` (default) returns everything.
* `limit` bounds `results` only (default 200, max 1000); the counts always cover everything.

### Rule catalog

`ruleCount` is **15** today. Core rules (11): `WrongLocation` (a lane-docked tap/hold is not
actually on its lane), `MissingHoldEndObject`, `MissingRefObject`, `ObjectOverlap`,
`ObjectTimelineNotAligned`, `ConflictRecordIdLanes`, `MissingBossEnemySet`,
`InvalidConnectablePath`, `LaneBlockAcrossWalls`, `Soflan`, `SoflanConflict`.

Ogkr rules (4): `[Ongeki] HeaderConstMismatch` (flags `MetaInfo.XRESOLUTION != 4096`,
`TRESOLUTION != 1920`, or an empty `Creator` — handy for verifying `editor.set_metainfo`),
`[Ongeki] ColorIdInvaild`, `[Ongeki] ColorfulLaneBrightnessInvaild`,
`[Ongeki] NotInterpolatedCurve`.

> `WallConflictCheckRule` exists in the source but carries **no `[Export]`**, so MEF never
> registers it and it never runs. Add the export if wall-conflict detection is wanted.

## Mandatory: run `editor.check` after every mutation batch

**After mutating a chart, always call `editor.check` before reporting success.**

Individual edits can pass every argument check yet still leave the chart inconsistent, and the
checker is the only feedback loop that catches it. Concrete examples:

* Docking a tap/hold with `referenceLaneRecordId` but *without* `snapXToLane` is accepted by
  `editor.add_object` / `editor.modify_object`, but the object then sits at `XGrid=0` instead of on
  the lane and trips `WrongLocation`.
* Removing a hold end (`editor.remove_hold_end`) leaves the hold with no end → `MissingHoldEndObject`.
* Adding or editing a soflan can trip `Soflan` / `SoflanConflict`.

Recommended loop:

1. Mutate — ideally inside `editor.begin_action` / `editor.end_action`, so the whole batch is a
   single undo entry.
2. `editor.check` with `minSeverity: "problem"`.
3. If `total > 0`, fix the cause (or `editor.undo`) and re-run until it is clean.
4. Only then report the work as done.

## Fumen Meta Info (`editor.set_metainfo`)

Sets one field of `fumen.MetaInfo` — the same values the **FumenMetaInfoBrowser** tool shows.
It is a single undoable editor action, so it participates in `editor.undo` / `editor.redo` and in
an action scope. Field names are matched case-insensitively; `newValue` is text parsed per field.

| Field | Type | Notes |
| --- | --- | --- |
| `creator` | string | `MetaInfo.Creator`. |
| `version` | string | `"1.0.0"` form, parsed by `System.Version`. |
| `bpmFirst` | number | Also updates `BpmList.FirstBpm` so the chart's first BPM really changes. |
| `bpmCommon` / `bpmMinimum` / `bpmMaximum` | number | Header BPM statistics only. |
| `meterBunshi` / `meterBunbo` | integer | Also updates `MeterChanges.FirstMeter` (the meta field alone is not enough — its change notification is a dead path). |
| `tResolution` / `xResolution` | integer | `MetaInfo.TRESOLUTION` / `XRESOLUTION`. |
| `clickDefinition` | integer | `MetaInfo.ClickDefinition`. |
| `tutorial` | boolean | `true` / `false` (also accepts `1` / `0`). |
| `bulletDamage` / `hardBulletDamage` / `dangerBulletDamage` / `beamDamage` | number | |
| `progJudgeBpm` | number | |

The response echoes `oldValue` (canonical form) and `newValue`. Unknown fields fail with
`UNSUPPORTED_METAINFO`; unparseable values fail with `INVALID_ARGUMENT`. A missing editor yields
`NO_ACTIVE_EDITOR` / `EDITOR_NOT_FOUND`, and a chart without meta info yields `NO_METAINFO`.

Verifying a write without a getter:

* `creator` / `tResolution` / `xResolution` are checked by `[Ongeki] HeaderConstMismatch`, so
  `editor.check` shows whether the value landed (and whether `editor.undo` reverted it).
* `editor.query_object objectType='meter'` returns the first meter, so the `meterBunshi` /
  `meterBunbo` derived sync is observable as the entry at `tGrid.totalGrid === 0`.
* Otherwise call `editor.set_metainfo` again and read the echoed `oldValue`.

> There is currently **no** `editor.get_metainfo`; the response's `oldValue` is the only way to read
> a field back through the tool surface.

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
* `editor.check` returns `NO_ACTIVE_EDITOR` / `EDITOR_NOT_FOUND` / `EDITOR_CHANGED` / `NO_FUMEN`, and
  `INVALID_ARGUMENT` for a bad `minSeverity`. Per-rule failures land in `ruleFailures[]` instead of
  failing the call.
* `editor.set_metainfo` returns `UNSUPPORTED_METAINFO` (unknown field), `INVALID_ARGUMENT`
  (unparseable value), `NO_METAINFO` (chart has no meta info) or the shared editor-resolution errors.
* `editor.close` returns `EDITOR_DIRTY` for a dirty editor unless `force=true`.

## Change Guidance

* Add new editor-derived facts in `RuntimeAutomation` first if they depend on live editor state.
* Keep MCP tool classes focused on parameter handling, authorization preview text, and result shaping.
* If a feature only changes shell behavior, prefer updating menu handlers or host lifecycle wiring instead of expanding the tool surface.
* Compile first when debugging policy failures; run only after the script shape passes security checks.
