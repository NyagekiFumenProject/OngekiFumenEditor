# OGKR and Project-File Persistence

## Parser Manager

* `IFumenParserManager` selects serializers and deserializers by file extension.
* `Parser/DefaultImpl/Nyageki/DefaultFumenParser.cs` wires `[ImportMany]` serializers and deserializers and chooses the first one matching the extension.
* `Parser/DefaultImpl/Nyageki/DefaultNyagekiFumenParser.cs` is the native `.nyageki` lane and also finishes by calling `fumen.Setup()`.
* If a new file format is added, it must participate in MEF discovery and extension matching.

## `.ogkr` Read Path

* `Parser/Ogkr/DefaultOngekiFumenParser.cs` reads the file line-by-line.
* It looks up an `ICommandParser` by command header, lets the parser create chart objects, stores them immediately into `OngekiFumen`, then runs `AfterParse(...)`.
* Most format extensions land under `Parser/Ogkr/CommandParserImpl/` and are exported through `[Export(typeof(ICommandParser))]`.
* `fumen.Setup()` runs after all commands are parsed.

## `.ogkr` Write Path

* `Parser/Ogkr/DefaultOngekiFumenFormatter.cs` writes the file in ordered sections such as `[HEADER]`, `[B_PALETTE]`, composition/lane data, notes, curves, comments, SVG, and soflan-group wrappers.
* Section order is part of the format contract. Do not reshuffle it casually.
* If a feature changes persistence shape, inspect both the formatter section ordering and the command-parser side before assuming the format is symmetric.

## `.nyagekiProj` Read and Write Path

* The editor document provider handles `.nyagekiProj`, not raw `.ogkr`.
* `EditorProjectFileManager` uses `MigratableSerializerManager` with versioned serializers and explicit migrations.
* `Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.cs` routes document open/save through `EditorProjectDataUtils.TryLoadFromFileAsync(...)` and `TrySaveEditorAsync(...)`.
* `EditorProjectDataUtils.TryLoadFromFileAsync(...)` normalizes project-relative audio and fumen paths to absolute paths, then loads the referenced fumen file through `IFumenParserManager`.
* `EditorProjectDataUtils.TrySaveEditorAsync(...)` clones project data, converts paths back to project-relative form, saves to temp files, then copies them over the real files.

## Editor-Only Data

* Editor-only metadata such as bullet-palette editor display data is stored in project data, not in `.ogkr`.
* Keep raw chart semantics and editor presentation data separated unless the file format explicitly requires both.

## Change Guidance

* When adding persistence fields, decide whether they belong in raw chart data, project data, or both.
* Update versioned serializers, migrations, and `EditorProjectDataUtils` together.
* If a change affects editor-visible counts, summaries, or derived layout data, verify they still survive both direct chart parsing and project-file round trips.
* Preserve relative-path normalization and temp-file safety behavior.
