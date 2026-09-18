# Build, Debugging, and Change Strategy

## Project Facts

* The project targets `net10.0-windows` with `UseWPF=true`.
* `LangVersion` is `preview`.
* Debug builds disable Fody handling in this project.
* Release builds embed some native dependencies and post-build runs `Dependencies/Topten.nvpatch.exe`.

## Build-Sensitive Areas

* `OngekiFumenEditor.csproj` contains a large amount of explicit resource metadata. Missing item updates are a common source of runtime failures.
* Native dependencies under `costura-win-x64` and `Dependencies/` are part of the build/runtime contract.
* Settings and `.resx` designer files are generated artifacts; treat them as part of a coordinated edit.
* Debug and release differ in native packaging behavior, so "works in Debug only" often points back to project-file metadata rather than application logic.

## Debugging Heuristics

* If shell behavior is wrong, inspect `AppBootstrapper.cs` first.
* If an editor feature seems incomplete, inspect sibling partial classes and related module slices before rewriting it.
* If persistence is wrong, inspect both parser/formatter code and `EditorProjectDataUtils`.
* If an MCP or script task fails, inspect authorization, security policy, and dispatcher/undo requirements before blaming the transport layer.
* If a program option appears disconnected, inspect both `Kernel/SettingPages/Program/` and the concrete `ProgramSetting.Default` call sites.
* If icons or packaged assets fail to load, check whether the runtime expects a pack URI, an embedded resource, or a copied output file.

## Safe Change Strategy

* Keep changes narrow and aligned with existing feature slices.
* Preserve MEF export/import style and existing `IoC.Get(...)` access patterns.
* Preserve temp-file save flows rather than writing directly to live project or fumen files.
* Preserve undoability, dirty-state transitions, and localized text coverage when adding editor features.
* Prefer validating the smallest affected lane first: parser round trip, editor command, MCP tool call, or resource load path.

## Resource-Specific Note

* Files under `Resources/skills/` are currently copied to the output directory as loose files and are also exposed by the MCP host as read-only `skill://...` resources. If that packaging or host behavior changes, update both the project file and this skill reference set.
