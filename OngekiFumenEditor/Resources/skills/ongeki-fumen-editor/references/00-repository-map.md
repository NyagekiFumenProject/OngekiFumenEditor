# Repository Map

## Top-Level Areas

* `Base/`: chart-domain types, collections, core grid/time abstractions, and object families such as lanes, taps, holds, bullets, beams, and editor-side objects.
* `Parser/`: parser manager, file-format serializers/deserializers, OGKR command parsing, and Nyageki/default persistence helpers.
* `Modules/`: feature slices for the main editor document, tool windows, rendering layers, converters, dialogs, commands, and toolbox features.
* `Kernel/`: cross-cutting services such as runtime automation, MCP, settings pages, graphics, audio, updater, keybinding, scheduler, and editor layout.
* `UI/`: shared controls, converters, markup helpers, keybinding triggers, dialogs, and theme resources used across modules.
* `Properties/`: generated settings, generated resource designer files, and localized `.resx` content.
* `Resources/`: icons, textures, sounds, templates, embedded files, and repo-side skill content.
* `docs/`: design notes, especially around recent runtime automation and MCP work.
* `Dependencies/`: packaged native and third-party managed binaries referenced by the main project.

## Task Routing

* App startup, plugin loading, assembly composition, shell behavior: `01-bootstrap-composition-and-lifetimes.md`
* Chart objects, collections, connectable lanes, object counts, soflans: `02-chart-model-and-editing-primitives.md`
* `.ogkr` parsing/formatting or `.nyagekiProj` persistence: `03-ogkr-and-project-file-persistence.md`
* Editor document behavior, toolboxes, interactions, tool windows: `04-fumen-visual-editor-and-modules.md`
* MCP server, runtime automation scripts, security checks, editor summaries: `05-runtime-automation-and-mcp.md`
* Menus, settings pages, localization, keybindings: `06-settings-menus-localization-and-input.md`
* Rendering backends, audio systems, asset packaging: `07-rendering-audio-and-resources.md`
* Repo-specific build rules and safe implementation strategy: `08-build-debugging-and-change-strategy.md`
* Concrete implementation checklists: `09-common-change-recipes.md`

## Cross-Cutting Patterns

* The repo mixes MEF exports/imports with static `IoC.Get(...)` service access. Do not replace that style piecemeal.
* Many feature folders repeat the same shape: `MenuDefinitions.cs`, `Commands/`, `Views/`, `ViewModels/`, and sometimes `Kernel/` or `Graphics/`.
* `FumenVisualEditorViewModel` is partial and split by responsibility; inspect sibling partials before adding editor behavior.
* Many editor mutations must stay undoable and must participate in dirty-state updates.
* Save flows favor temp-file writes plus copy-over instead of in-place file mutation.
