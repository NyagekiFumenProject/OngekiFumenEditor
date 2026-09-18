# Fumen Visual Editor and Modules

## Main Document Type

* `Modules/FumenVisualEditor/FumenVisualEditorProvider.cs` handles `.nyagekiProj` and creates `FumenVisualEditorViewModel`.
* `FumenVisualEditorViewModel` is the main editor document and owns `EditorProjectData`, the loaded `OngekiFumen`, audio playback, dirty state, scrolling state, and undo/redo behavior.
* The view model replaces the default undo implementation with `DefaultEditorUndoManager`.
* `Modules/FumenVisualEditor/Kernel/DefaultImpl/DefaultEditorDocumentManager.cs` is the usual coordination layer for current/opened editor tracking.

## Partial-Class Structure

* `FumenVisualEditorViewModel` is split across multiple partial files such as drawing, brush, scroll-viewer, and user-interaction slices.
* Before editing editor behavior, inspect sibling partials to avoid duplicating state or missing an existing hook.
* High-frequency landing zones are `FumenVisualEditorViewModel.UserInteractionActions.cs`, `FumenVisualEditorViewModel.ScrollViewer.cs`, and the interactive-action implementations under `ViewModels/Interactives/Impls/`.

## Document Lifecycle

* `DoNew()`, `DoLoad()`, `Load(...)`, and `DoSave()` are the high-level document lifecycle methods.
* `DoNew()` can parse a selected fumen directly through `IFumenParserManager`.
* `DoLoad()` loads project files through `EditorProjectDataUtils`, and `DoSave()` persists through `EditorProjectDataUtils`.
* Audio is loaded via `IAudioManager`, not manually by the document.

## Editor Coordination

* `IEditorDocumentManager` tracks the current active editor and the full set of opened editors.
* Tool windows, property browsers, runtime automation, and summaries depend on that manager rather than reaching into the shell directly.
* `IFumenObjectPropertyBrowser` is a common downstream dependency. Selection changes and many edit commands refresh it explicitly.

## Module Shape

* Many feature folders under `Modules/` follow the same slice pattern:
* `MenuDefinitions.cs`
* `Commands/`
* `Views/`
* `ViewModels/`
* sometimes `Kernel/`, `Graphics/`, `Toolboxes/`, `Converters/`, or `Behaviors/`
* The editor itself also follows that pattern internally through behaviors, drop actions, command handlers, and tool-window companions.

## Examples to Reuse

* `Modules/FumenObjectPropertyBrowser/`: property editing and batch operations.
* `Modules/FumenVisualEditor/`: core editor interactions, drawing targets, toolboxes, batch mode, and editor project handling.
* `Modules/FumenCheckerListViewer/`: analysis and navigation behavior.
* `Modules/OptionGeneratorTools/`: multi-window utility workflows.
* `Modules/OgkiFumenListBrowser/`: editor-opening and preview pipelines outside the main shell.
* `Modules/FumenEditorSelectingObjectViewer/`: selection-derived summaries and filter-driven object picking.

## Change Guidance

* Keep user-triggered chart mutations undoable and grouped with meaningful action names.
* Refresh dependent views such as property browsers or selection viewers when a feature changes editor state in nonstandard ways.
* If a feature feels like "mouse behavior", inspect `SelectionArea`, batch-mode behaviors, and `UserInteractionActions` before adding a new top-level service.
* If a feature edits object properties, inspect `Modules/FumenObjectPropertyBrowser/UIGenerator/` before creating a second property-edit pipeline.
* Prefer following an existing module slice over inventing a new registration pattern.
