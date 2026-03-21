---
name: ongeki-fumen-editor
description: Develop, debug, extend, and review the OngekiFumenEditor WPF codebase built on Caliburn.Micro, Gemini, and MEF. Use when working in this repository on startup and composition, chart-domain model changes, OGKR or .nyagekiProj persistence, FumenVisualEditor behavior, editor modules and tool windows, runtime automation/MCP tools, menu/settings/keybindings/localization, rendering/audio/resources, or repository-specific troubleshooting.
---

# Ongeki Fumen Editor

Use this skill for repo-native work in `OngekiFumenEditor`. It keeps changes aligned with the repository's existing WPF, Gemini, MEF, parser, editor, runtime-automation, and asset-packaging patterns.

Primary entry for the reference set:

* `references/compendium.md`

## Quick Routing

* Startup, plugin loading, shell, GUI/CMD behavior, IPC, or app lifetime:
  read `references/01-bootstrap-composition-and-lifetimes.md`.
* Chart object definitions, object counts, relationship wiring, or editing primitives:
  read `references/02-chart-model-and-editing-primitives.md` and `references/03-ogkr-and-project-file-persistence.md`.
* Editor document behavior, tool windows, toolbox, property browser, or viewport interactions:
  read `references/04-fumen-visual-editor-and-modules.md`.
* MCP tools, runtime automation, script host, authorization, or editor summaries:
  read `references/05-runtime-automation-and-mcp.md`.
* Menus, settings, localization, resources, or keybindings:
  read `references/06-settings-menus-localization-and-input.md`.
* Rendering backend, graphics resources, audio, or project-file resource metadata:
  read `references/07-rendering-audio-and-resources.md` and `references/08-build-debugging-and-change-strategy.md`.
* Script-driven editor tasks:
  start with `references/10-script-execution-surfaces.md`, `references/11-script-api-cheatsheet.md`, and `references/12-script-task-recipes.md`, then load `13` through `19` only as needed.
* Repo-specific implementation checklists or change scoping:
  read `references/09-common-change-recipes.md`.
* First-pass repository orientation:
  read `references/00-repository-map.md`.

## Workflow

1. Route the task before editing.
* Read `references/00-repository-map.md` if the target subsystem is unclear.
* Then load only the pages needed for the current lane.

2. Preserve repository wiring before introducing new abstractions.
* Read `references/01-bootstrap-composition-and-lifetimes.md`.
* Keep WPF, Caliburn.Micro, Gemini, and MEF composition intact.
* Prefer existing `[Export]`, `[Export(typeof(...))]`, `[ImportingConstructor]`, `[ImportMany]`, and `IoC.Get(...)` patterns over introducing a different DI style.
* Keep GUI mode, command-line mode, IPC, splash, and MCP startup behavior coherent.

3. Treat cross-lane features as coordinated edits.
* Chart-domain changes usually require model, parser/formatter, editor mutation, and summary/count checks together.
* UI features often require menu definitions, commands, views/viewmodels, resources, localized text, and settings-page wiring together.
* Resource changes often require `OngekiFumenEditor.csproj` item updates in addition to the file itself.

4. Follow the repo's mutation and persistence rules.
* Keep editor mutations undoable with `UndoRedoManager.ExecuteAction(...)` and explicit undo lambdas.
* Preserve dirty-state transitions and dependent view refresh behavior.
* Preserve `.nyagekiProj` and fumen temp-file save flows instead of replacing them with direct in-place writes.

5. Validate the lane you touched.
* For startup or global wiring, inspect `AppBootstrapper.cs`, shell startup, and plugin discovery behavior.
* For parser or model work, verify both load and save paths.
* For editor work, verify selection, viewport, property-browser, and undo/redo side effects.
* For MCP or runtime scripts, compile first when possible and preserve authorization, backup, and security-policy behavior.
* For strings, settings, or resources, update `.resx`, `.settings`, generated designers, and project metadata as needed.

## Review Focus

When reviewing changes in this repo, prioritize:

* lost MEF exports/imports or shell-registration regressions
* broken GUI/CMD/MCP startup ordering
* parser/save asymmetry between OGKR, `.nyagekiProj`, and in-memory state
* editor mutations that are no longer undoable or no longer update dirty state
* missing localized strings, settings wiring, or resource item metadata
* script or MCP changes that bypass authorization, backup, dispatcher, or security-policy rules

## Repo-specific rules

* Keep the current stack: WPF on `net10.0-windows`, Caliburn.Micro, Gemini, MEF composition, and project-specific helpers.
* Default to `[Export]`, `[Export(typeof(...))]`, `[ImportingConstructor]`, and `[ImportMany]` instead of introducing a new DI style.
* Use `LambdaUndoAction.Create(...)` and `UndoRedoManager.ExecuteAction(...)` for editor mutations that must be undoable.
* `FumenVisualEditorViewModel` is partial; inspect sibling partials before adding editor behavior.
* Many features follow the same slice shape: `MenuDefinitions.cs`, `Commands/`, `Views/`, `ViewModels/`, and sometimes `Kernel/` or `Graphics/`.
* Expect `.nyagekiProj` save/load to normalize relative paths and use temp-file copy strategies.
* If a change affects user-facing strings or settings, update the relevant `.resx`, `.settings`, and setting page view/viewmodel together.
* If a change adds files under `Resources`, verify whether they must be `Resource`, `EmbeddedResource`, or copied as `None` in `OngekiFumenEditor.csproj`.
* Files under `Resources/skills/` are currently copied to the build output as loose files. Treat them as documentation assets, not embedded resources, unless the project file changes again.
