# OngekiFumenEditor.Core Migration Todo

## Goal

Create a platform-neutral library for:

- fumen domain models
- parsing and serialization
- pure calculations and interpolation
- validation contracts and reusable rules
- runtime automation DTOs and abstractions

## Phase 1

- Move parser abstractions from `OngekiFumenEditor/Parser`
- Move parser implementations that do not require WPF or editor view models
- Move curve interpolater abstractions and implementations
- Move pure utility helpers such as math, collection, comparer, pooling, hashing, and base64 helpers

### Phase 1 Progress

- Moved runtime automation DTOs and abstractions
- Moved comparer, hashing, file, reflection, random, interpolation, and parser helper utilities
- Moved grid primitives and basic object infrastructure needed by pure domain code
- Moved property browser metadata attributes and property change helper extensions
- Moved non-visual collection infrastructure: sortable collections, `TGridSortList`, `FixedSizeCycleCollection`, and `BpmList`
- Added a core-side pure BPM length helper to decouple `BpmList` from editor-side `MathUtils`
- Moved pure soflan collections and cache calculation
- Moved the full `Base` tree into `OngekiFumenEditor.Core`
- Replaced core-side `System.Windows.Media.Color` usage with `OngekiFumenEditor.Base.ValueTypes.Color`
- Reworked SVG processing around `VectorScene` / `VectorPath` / `VectorPoint`
- Removed direct WPF and `SvgConverter` dependencies from `OngekiFumenEditor.Core`
- Moved WPF-specific SVG scene building back to `OngekiFumenEditor`, which now builds `VectorScene` and calls `ApplySvgContent(...)` itself
- Moved the remaining default curve interpolater factory into `OngekiFumenEditor.Core`
- Moved object pool primitives and array/object pool helper extensions into `OngekiFumenEditor.Core`
- Moved shared LINQ and `TGrid` extension helpers into `OngekiFumenEditor.Core` with `netstandard2.1`-safe implementations
- Moved the pure portions of `MathUtils` into `OngekiFumenEditor.Core`, including range-combine helpers and BPM/grid math
- Moved `AbortableThread` and `ViewModelReferenceAttribute` into `OngekiFumenEditor.Core`
- Moved pure `Utils.Ogkr.InterpolateAll` lane interpolation helper into `OngekiFumenEditor.Core`
- Left editor-only utils such as WPF/Skia/OpenTK converters (`BrushHelper`, `ColorExtensionMethod`, `SvgExtensionMethod`, `Vector*ExtensionMethod`, `Matrix4ExtensionMethod`), command routing and view plumbing (`CommandRouterHelper`, `ActionExecutionContextExtensionMethod`, `CoroutineExtensionMethod`, `MapToViewAttribute`, `ViewHelper`, `WindowTitleHelper`), resource and shell helpers (`ResourceUtils`, `FileDialogHelper`, `DocumentOpenHelper`, `ProcessUtils`, `ConsoleWindowHelper`, `IPCHelper`, `IniFile`), status/logging infrastructure (`CommonStatusBar`, `StatusBarHelper`, `Log`, `Logs/*`, `ObjectPoolManager`), crash handling (`DeadHandler/*`), and editor-only OGKR workflow helper (`Utils.Ogkr.StandardizeFormat`) in `OngekiFumenEditor`

## Phase 2

- Introduce a core-side observable base to replace `Caliburn.Micro.PropertyChangedBase`
- Move grid primitives and non-visual domain types from `OngekiFumenEditor/Base`
- Split property browser metadata out of core models
- Split WPF color and drawing state out of core models

## Phase 3

- Extract checker result models and validation contracts
- Replace `FumenVisualEditorViewModel` parameters in checker interfaces with a core-safe context abstraction
- Extract runtime automation request and result DTOs plus service abstractions

## Blockers

- `Base` currently uses `Caliburn.Micro.PropertyChangedBase`
- some domain types still inherit Caliburn observable infrastructure
- property browser attributes currently depend on editor resources
- some helpers still resolve services through `IoC`
- some calculators and rule interfaces still depend on `FumenVisualEditorViewModel`
- current `OpenTK` 4.x packages in this repo target `netstandard2.1`, so connectable-object curve interpolation code cannot move into `netstandard2.0` core without an abstraction or package/version strategy change

## Nice To Have Later

- evaluate whether project file serialization belongs in a separate infrastructure project
- evaluate whether SVG preview generation belongs in a separate tools project
- keep MCP host, script execution host, menus, settings, and WPF views out of core
