# Issue 31 Scripts Menu Access Plan

## Source

- GitHub issue: https://github.com/NyagekiFumenProject/OngekiFumenEditor/issues/31
- Title: Add a way to quickly access scripts
- Created: 2026-06-10
- Current state when reviewed: open, comments include design notes and "Developing".

## Issue Summary

Users can already create and run `.nyagekiScript` files, but using an existing script is slow because they must browse to the script file manually. The requested feature is a highly accessible script menu, likely in the main menu bar, that lists script files from known locations and lets users open or run them quickly.

A maintainer comment adds two concrete directions:

1. Associate `.nyagekiScript` files with the program, similar to `.nyageki` and `.nyagekiProj`.
2. Add a new `Scripts` main menu item with submenu lists for:
   - project-built-in recommended scripts
   - user's recently opened or newly created custom scripts

The same comment explicitly warns against unsafe one-click execution: users should understand what a script does before executing it.

## Existing Code Facts

- `.nyagekiScript` support already exists through `EditorScriptDocumentProvider`.
- The script editor document is `EditorScriptDocumentViewModel`.
- New scripts are seeded from `Resources/NewScriptTemplate.nyagekiScript`.
- Script documents already support compile, run with confirmation, reload, and Visual Studio edit.
- `DocumentOpenHelper.TryOpenAsDocument(...)` already opens files handled by any `IEditorProvider`, so `.nyagekiScript` can be opened from startup args, IPC, drag-and-drop, and recent-file paths once the OS association passes the path to the app.
- Existing file association UI lives in `Kernel/SettingPages/Program/ViewModels/ProgramSettingViewModel.cs` and `Kernel/SettingPages/Program/Views/ProgramSettingView.xaml`.
- Existing dynamic menu list pattern is `Kernel/RecentFiles` with `CommandListDefinition` and `ICommandListHandler<T>`.
- Top-level menu examples include `Kernel/Mcp/MenuDefinitions.cs` and `Modules/FumenVisualEditor/Commands/OgkrImpl/MenuDefinitions.cs`.
- Resource strings live in `Properties/Resources*.resx`.

## Recommended Product Behavior

Recommended default: the `Scripts` menu should open script documents, not execute scripts immediately.

Rationale:

- The script execution path is full-trust legacy Roslyn scripting.
- Existing `EditorScriptDocumentViewModel.OnRunButtonClicked()` already compiles and asks for confirmation before execution.
- Opening a script document lets users inspect, edit, pick target editor, compile, and then run intentionally.
- This matches the maintainer safety concern while still removing file-browser friction.

Decision:

- The `Scripts` menu opens script documents only.
- Menu items must not compile or execute scripts directly.

## Proposed Menu Shape

Top-level main menu:

- `Scripts`
  - `New Script`
  - `Recommended Scripts`
    - dynamic list of bundled `.nyagekiScript` files
  - `Recent Scripts`
    - dynamic list of recently opened `.nyagekiScript` files

Decision for utility commands:

- Do not add `Open Scripts Folder` in the first implementation.
- Do not add `Refresh Scripts Menu` in the first implementation.
- The first menu scope is only `New Script`, `Recommended Scripts`, and `Recent Scripts`.
- Rationale: built-in scripts are embedded resources and do not have a user-openable folder; dynamic command-list population should make manual refresh unnecessary.

Decision for empty script lists:

- Keep `Recommended Scripts` and `Recent Scripts` visible even when empty.
- Empty lists should contain one disabled menu item:
  - English: `No scripts found`
  - Chinese: `没有可用脚本`
- Do not hide the menu group only because its current dynamic list is empty.

Recommended first implementation scope:

- Add top-level `Scripts` menu.
- Add `New Script` command at the top of the `Scripts` menu.
- Add `Recommended Scripts` cascade backed by a bundled script directory.
- Add `Recent Scripts` cascade filtered from existing recent records by `.nyagekiScript` extension.
- Clicking a script opens it as a document by reusing `DocumentOpenHelper.TryOpenAsDocument(...)`.
- Do not add one-click execution in the first version.

Decision for top-level menu placement:

- Place `Scripts` after the existing `Ongeki` menu and before the existing `Assist` menu.
- The menu is considered part of the chart-editing workflow rather than a help/debug/MCP surface.
- Implementation may move the existing `Assist` menu order from `7` to `8`, and assign `Scripts` to order `7`, because `Ongeki` currently uses order `6`.

Decision for `New Script`:

- Provide a `New Script` / `新建脚本` command in the `Scripts` menu.
- It should reuse the existing `.nyagekiScript` new-document flow.
- New script content should continue to come from `Resources/NewScriptTemplate.nyagekiScript` unless that template behavior is changed separately.

## Built-In Script Storage

Decision: built-in recommended scripts should be embedded resources.

Rationale:

- Built-in scripts may depend on editor APIs that can change incompatibly.
- Bundling them as embedded resources keeps the recommended script set versioned with the application binary.
- This avoids stale loose script files remaining in an output/user directory and appearing as current recommended scripts after the host API changes.
- Built-in scripts should still be opened for user inspection before execution, but the canonical source should come from the current application build.

Important rule from the issue comment: if a built-in script is modified by the user, it must be saved as a new script location.

Decision for opening built-in scripts:

- Open the embedded script content as a script document with a built-in/read-only origin marker.
- The document title should be `[内置/只读]xxxxx.nyagekiScript`.
- Unlike the menu item text, the document title should keep the `.nyagekiScript` extension.
- The editor content should remain editable in memory; "read-only" means the embedded source cannot be overwritten.
- If the user tries to save the document, force a Save As dialog.
- After Save As succeeds, the current document should switch to the saved user file path and continue as that normal script file.
- After Save As succeeds, the saved user script should be posted to the existing recent-file manager and appear in `Recent Scripts`.
- Opening the built-in script itself should also be posted to the existing recent-file manager using a dedicated open type.
- Add `RecentOpenType.OpenEmbeddedRecommendedScript` for built-in recommended scripts.
- For `OpenEmbeddedRecommendedScript`, `RecentRecordInfo.FileName` stores the embedded-resource path, not a filesystem path.
- The built-in embedded resource must never be overwritten by document save operations.
- First implementation may allow opening multiple document instances for the same embedded recommended script.
- Do not add a dedicated built-in-script document de-duplication index in the first implementation.

Implementation implication: `EditorScriptDocumentViewModel` likely needs explicit state for built-in/read-only script origin so `DoSave(...)` can redirect to Save As and clear that state after a successful user-file save.

Decision for first bundled script content:

- Implement the embedded-resource discovery and opening mechanism first.
- Do not add concrete built-in recommended script contents in the first pass.
- The initial `Recommended Scripts` menu may therefore show the disabled `No scripts found` item until a later change adds embedded `.nyagekiScript` resources.
- Future built-in scripts should be reviewed as versioned application assets because they depend on editor APIs that may change over time.

Decision for embedded resource path:

- Source path convention: `OngekiFumenEditor/Resources/Scripts/EmbbedRecommended/*.nyagekiScript`.
- These files should be included as embedded resources.
- The `Recommended Scripts` menu should discover embedded script resources from this project-owned path/prefix.

Decision for recommended-script item display:

- Display the embedded resource file name without the `.nyagekiScript` extension.
- Example: `NormalizeTiming.nyagekiScript` should appear as `NormalizeTiming`.
- First implementation can keep the menu list flat; no subdirectory-based cascade is required yet.

## Custom Script Source Options

The comment mentions user's recently opened or newly created custom scripts. There are two likely approaches:

1. Filter the existing recent-file list to `.nyagekiScript`.
2. Maintain a dedicated custom script directory and list all `.nyagekiScript` files from it.

Decision: `Recent Scripts` should reuse existing recent-file records.

Included record types:

- Ordinary script files: existing records whose path is a `.nyagekiScript` filesystem path.
- Built-in recommended scripts: records whose `RecentOpenType` is `OpenEmbeddedRecommendedScript`.

Rationale:

- Script document load/save already posts records to `IEditorRecentFilesManager`.
- Newly created scripts naturally appear after the first successful save.
- Unsaved new script documents must not appear in `Recent Scripts` because they have no stable file path and cannot be restored after restart.
- Built-in recommended scripts should also appear in `Recent Scripts`, but they require special handling because their `FileName` is an embedded-resource path rather than a filesystem path.
- This avoids introducing a separate custom script library, settings entry, or folder scan behavior in the first implementation.
- If broader custom-script discovery is needed later, it can be added as a separate menu group without changing the recent-script behavior.

Decision for recent record validation:

- Add `IEditorRecentFilesManager.CheckValid(RecentRecordInfo info)`.
- `CheckValid(...)` returns whether the record is currently openable.
- For ordinary filesystem records, validity means the referenced file still exists.
- For `OpenEmbeddedRecommendedScript`, validity means the embedded resource still exists in the current application build.
- Dynamic recent menus should call `CheckValid(...)` rather than directly calling `File.Exists(...)`.

Decision for recent-script item display:

- For ordinary filesystem script records, use the existing recent-file menu display style: `_1 DisplayName (FullPath)`.
- Keep full filesystem paths visible for ordinary script files, consistent with the current `File > Recent Files` behavior.
- For `OpenEmbeddedRecommendedScript` records, store `RecentRecordInfo.DisplayName` as `[内置]xxxxx.nyagekiScript`.
- For `OpenEmbeddedRecommendedScript` menu items, display exactly `[内置]xxxxx.nyagekiScript`.
- Do not show a numeric mnemonic or the embedded-resource path for built-in recent menu items.
- The document title remains `[内置/只读]xxxxx.nyagekiScript`; the recent menu display name intentionally uses the shorter `[内置]` prefix.

## File Association Scope

Add `.nyagekiScript` to the existing file association setting page.

Expected behavior:

- Register `.nyagekiScript` with the main executable and `%1` argument.
- Startup arg processing already calls `DocumentOpenHelper.TryOpenAsDocument(...)`, so the script should open in the script editor.
- Drag-and-drop should also work because it uses the same helper.

Decision:

- Add `.nyagekiScript` to the existing Program settings file-association checkbox group.
- The `.nyagekiScript` association checkbox should be checked by default.
- Registration should use the same program icon and `%1` command argument style as `.nyagekiProj`, `.nyageki`, and `.ogkr`.
- Recommended association description: `Ongeki Fumen Editor Script File`.

## Localization

New user-facing strings likely required:

- `Scripts`
- `Recommended Scripts`
- `Recent Scripts`
- possibly `Open Scripts Folder`, `Refresh Scripts Menu`, `No Scripts Found`, `Script file does not exist`, and `.nyagekiScript` file association description.

Need update at least:

- `Resources.resx`
- `Resources.zh-Hans.resx`
- `Resources.ja.resx` if maintaining current coverage.

## Implementation Slice

Likely new or modified areas:

- `Modules/EditorScriptExecutor/MenuDefinitions.cs` or a new script-menu feature folder.
- `Modules/EditorScriptExecutor/Commands/*` for command definitions and command-list handlers.
- `Kernel/SettingPages/Program/ViewModels/ProgramSettingViewModel.cs` for `.nyagekiScript` association flag and registration.
- `Kernel/SettingPages/Program/Views/ProgramSettingView.xaml` for the association checkbox.
- `Properties/Resources*.resx` and generated designer if needed.
- `OngekiFumenEditor.csproj` for embedded `Resources/Scripts/EmbbedRecommended/**/*.nyagekiScript` resources if built-in scripts are added.
- Optional changes to `EditorScriptDocumentViewModel` if built-in scripts must be enforced as read-only or save-as-only.

## Open Questions

1. Resolved: menu items open scripts for review/editing only; no direct execution from menu.
2. Resolved: built-in recommended scripts should be embedded resources, versioned with the application.
3. Resolved: built-in scripts open with title `[内置/只读]xxxxx.nyagekiScript`; save attempts force Save As; after Save As the document uses the new user file path.
4. Resolved: `Recent Scripts` uses existing recent records, including ordinary `.nyagekiScript` files and built-in recommended script records.
5. Resolved: newly created script documents appear in `Recent Scripts` only after the first successful save to a real file path.
6. Resolved: add `New Script` to the `Scripts` menu, reusing the existing script document creation flow.
7. Resolved: empty menu groups remain visible and show a disabled `No scripts found` / `没有可用脚本` item.
8. Resolved: first implementation only adds the embedded-script mechanism; concrete built-in script content will be added later.
9. Resolved: place the top-level `Scripts` menu after `Ongeki` and before `Assist`.
10. Resolved: it is acceptable to move `Assist` from menu order `7` to `8` and use order `7` for `Scripts`.
11. Resolved: add `.nyagekiScript` to the existing Program settings file association UI, default checked, using the app icon and `%1` open command.
12. Resolved: embedded recommended scripts use source path `OngekiFumenEditor/Resources/Scripts/EmbbedRecommended/*.nyagekiScript`.
13. Resolved: built-in/read-only script documents allow in-memory editing; save still forces Save As because the embedded source cannot be overwritten.
14. Resolved: after a built-in script is saved through Save As, the resulting user script should be added to recent records and appear in `Recent Scripts`.
15. Resolved: ordinary `Recent Scripts` file items use `_1 DisplayName (FullPath)`; embedded recommended script items display exactly `[内置]xxxxx.nyagekiScript`.
16. Resolved: do not add `Open Scripts Folder` or `Refresh Scripts Menu` in the first implementation.
17. Resolved: `Recommended Scripts` item text should be the embedded script file name without the `.nyagekiScript` extension.
18. Resolved: built-in script document titles keep the `.nyagekiScript` extension: `[内置/只读]xxxxx.nyagekiScript`.
19. Resolved: opening a built-in recommended script posts a recent record using `RecentOpenType.OpenEmbeddedRecommendedScript`; `FileName` stores the embedded-resource path.
20. Resolved: add `IEditorRecentFilesManager.CheckValid(RecentRecordInfo info)` for filesystem and embedded-resource validity checks.
21. Resolved: use standard enum spelling `OpenEmbeddedRecommendedScript`; keep the source directory spelling `EmbbedRecommended` as specified.
22. Resolved: `OpenEmbeddedRecommendedScript` recent records use display name `[内置]xxxxx.nyagekiScript`.
23. Resolved: first implementation may open duplicate document instances for the same embedded recommended script.

## Current Recommendation Snapshot

- `Scripts` menu opens scripts, does not execute them directly.
- Built-in scripts are embedded resources under `OngekiFumenEditor/Resources/Scripts/EmbbedRecommended/`.
- First implementation does not need to include actual built-in script content; the mechanism should handle an empty embedded list.
- Built-in scripts open as `[内置/只读]xxxxx.nyagekiScript`; they are editable in memory, but user modifications must be saved through Save As, and the document then switches to the saved file.
- First implementation can allow duplicate opened documents for the same embedded recommended script.
- Built-in script opens also become recent entries through `RecentOpenType.OpenEmbeddedRecommendedScript`, with `FileName` storing the embedded-resource path.
- Built-in script recent entries use `DisplayName` format `[内置]xxxxx.nyagekiScript`.
- Built-in script Save As results become ordinary user scripts and should immediately appear in `Recent Scripts`.
- `Recent Scripts` should reuse existing recent records, including ordinary `.nyagekiScript` file records and `OpenEmbeddedRecommendedScript` records.
- Ordinary `Recent Scripts` file item text should follow existing recent-file format `_1 DisplayName (FullPath)`.
- Embedded recommended script recent item text should be exactly `[内置]xxxxx.nyagekiScript`, with no resource path displayed.
- `IEditorRecentFilesManager.CheckValid(...)` should replace direct file-existence checks for dynamic recent menus.
- `Recommended Scripts` item text should be the script file name without `.nyagekiScript`.
- Add a `New Script` command in `Scripts`, backed by the current `.nyagekiScript` template flow.
- First menu scope excludes `Open Scripts Folder` and `Refresh Scripts Menu`.
- Empty `Recommended Scripts` and `Recent Scripts` groups stay visible with a disabled `No scripts found` item.
- Place the top-level `Scripts` menu after `Ongeki` and before `Assist`.
- Implement the placement by assigning `Scripts` order `7` and moving `Assist` to order `8`.
- `.nyagekiScript` file association should be added to the existing Program settings association UI.
- `.nyagekiScript` file association should be checked by default and use description `Ongeki Fumen Editor Script File`.
