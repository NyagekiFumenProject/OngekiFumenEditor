# Common Change Recipes

## Add a New Chart Object or Command

* Add the domain type under `Base/OngekiObjects/` or `Base/EditorObjects/`.
* Route ownership through `OngekiFumen.AddObject(...)` and `RemoveObject(...)`.
* Add parser support under `Parser/Ogkr/CommandParserImpl/` when the object is part of `.ogkr`.
* Add formatter output in `Parser/Ogkr/DefaultOngekiFumenFormatter.cs` when the object participates in raw chart persistence.
* Add drawing, toolbox, property-browser, and selection behavior if the object is editor-visible.
* Update summary/count surfaces if the object appears in UI summaries or MCP output.

## Add or Change Project Data

* Update the relevant `EditorProjectDataModel*` type.
* Update versioned serializers and migrations.
* Update `EditorProjectDataUtils` load/save path handling if the field affects persistence.
* Re-open and save a `.nyagekiProj` path in the editor lane after the change, not just isolated serializer tests.

## Add a New Tool Window or Module Command

* Mirror the existing feature-slice structure: menu definitions, command definitions/handlers, views, and view models.
* Register through MEF and Gemini conventions rather than creating windows ad hoc from unrelated code.
* Use an existing module such as `Modules/FumenObjectPropertyBrowser/` or `Modules/AudioPlayerToolViewer/` as a structural reference before inventing a new slice.

## Add an MCP Capability

* Put editor-state extraction in `Kernel/RuntimeAutomation` if it depends on live editor objects.
* Keep `Kernel/Mcp` thin: parameter handling, authorization preview text, and tool-shaped results.
* Update menu/admin surfaces only when server behavior itself changes.
* If the tool only needs a summary or list of opened editors, prefer extending the editor-context layer instead of tunneling full view-model access into the MCP surface.

## Write or Fix Runtime Automation Scripts

* Compile first with `script.compile` when debugging security-policy issues.
* Keep chart mutation inside `UndoRedoManager.ExecuteAction(...)` redo/undo lambdas.
* Assume real editor-object access is UI-thread-bound.

## Add a Setting or User-Visible String

* Update the `.settings` file, setting page, and resource strings together.
* Prefer localized resources over inline literals in XAML or C#.
* If the option lives under program settings, verify both the program settings page and the feature code that reads `ProgramSetting.Default`.

## Add a Packaged Resource

* Place the file under `Resources/`.
* Update `OngekiFumenEditor.csproj` with the correct `Resource`, `EmbeddedResource`, or `None Update ... CopyToOutputDirectory` metadata.
* Verify whether runtime code expects a pack URI or a filesystem path.
* If the resource only appears in Release or only in Debug, inspect Costura, `SoundTouch.dll`, and other configuration-conditional packaging rules before changing consumer code.

## Add Plugin-Facing Functionality

* Keep exported parts MEF-friendly.
* Remember that runtime plugin discovery scans executable-side `Plugins/*` subdirectories.
* If the plugin assembly name follows `OngekiFumenEditorPlugins.*`, startup code will also add it to `AssemblySource`.

## Review a Cross-Cutting Change

* Start with the narrowest changed lane, then trace outward: startup, parser/persistence, editor behavior, MCP/runtime automation, or resource packaging.
* Check for undo/redo regressions, dirty-state loss, missing `.resx` updates, and missing `OngekiFumenEditor.csproj` metadata.
* If the change introduces a new abstraction, verify it still fits the repo's MEF + Gemini + `IoC.Get(...)` composition style.
