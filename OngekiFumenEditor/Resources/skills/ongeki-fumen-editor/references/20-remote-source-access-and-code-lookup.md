# Remote Source Access and Code Lookup

Use this page when the skill is available but the full `OngekiFumenEditor` source tree is not local, not indexed, or only partially visible to the current agent.

The canonical remote source repository is:

* `https://github.com/NyagekiFumenProject/OngekiFumenEditor`

Prefer the repository source tree over README or wiki pages when the task depends on concrete code behavior, method names, object shapes, or script APIs.

## URL Patterns

Map repo-relative paths directly onto GitHub:

* Repository root:
  `https://github.com/NyagekiFumenProject/OngekiFumenEditor`
* Folder view:
  `https://github.com/NyagekiFumenProject/OngekiFumenEditor/tree/master/<repo-relative-path>`
* File view:
  `https://github.com/NyagekiFumenProject/OngekiFumenEditor/blob/master/<repo-relative-path>`
* Raw file:
  `https://raw.githubusercontent.com/NyagekiFumenProject/OngekiFumenEditor/master/<repo-relative-path>`

Example mappings:

* `OngekiFumenEditor/AppBootstrapper.cs`
* `OngekiFumenEditor/Kernel/Mcp/EditorTools.cs`
* `OngekiFumenEditor/Kernel/RuntimeAutomation/RuntimeAutomationScriptHost.cs`
* `OngekiFumenEditor/Modules/EditorScriptExecutor/Scripts/ScriptArgs.cs`

## Recommended Lookup Flow

1. Read `00-repository-map.md` to identify the lane.
2. Convert the repo-relative folder or file path into a GitHub `tree/master` or `blob/master` URL.
3. Open only the folders and files needed for the task.
4. Cross-check the lane-specific reference pages in this skill before writing code or a script.

If the task is about live editor automation, script APIs, or safe mutations, load the relevant reference pages from this skill even after reading the remote source. The skill pages explain the repo-specific constraints that are easy to miss when reading source alone.

## Source Lookup Priorities

For common task types, start with these source areas:

* Startup, shell, plugin loading, GUI/CMD mode:
  `OngekiFumenEditor/AppBootstrapper.cs`, `OngekiFumenEditor/App.xaml.cs`, `OngekiFumenEditor.CommandLine/Program.cs`
* Chart model, object definitions, and persistence:
  `OngekiFumenEditor/Base/`, `OngekiFumenEditor/Parser/`
* Editor interactions, tool windows, and document behavior:
  `OngekiFumenEditor/Modules/FumenVisualEditor/`, `OngekiFumenEditor/Modules/`
* MCP tools and runtime automation:
  `OngekiFumenEditor/Kernel/Mcp/`, `OngekiFumenEditor/Kernel/RuntimeAutomation/`
* Legacy script surface:
  `OngekiFumenEditor/Modules/EditorScriptExecutor/`
* Settings, localization, resources, and keybindings:
  `OngekiFumenEditor/Kernel/SettingPages/`, `OngekiFumenEditor/Properties/`, `OngekiFumenEditor/Resources/`, `OngekiFumenEditor/UI/`

## Script Authoring Guidance

When writing runtime automation scripts or answering scriptability questions:

* Read `10-script-execution-surfaces.md`, `11-script-api-cheatsheet.md`, and `12-script-task-recipes.md`.
* Inspect the remote source for the exact editor APIs and object properties you plan to use.
* Verify whether the task belongs in runtime automation, legacy `.nyagekiScript`, or a source patch by checking `16-request-routing-and-script-strategy.md`.
* Prefer reading the concrete object type or editor helper implementation before inventing a script shape from memory.

When writing source patches instead of scripts:

* Use the skill's repository map and lane references to find the correct slice first.
* Treat GitHub as the source of truth for missing files, but keep the final change aligned with the repo's existing patterns and folder structure.

## Practical Rules

* Quote repo-relative paths in notes and answers so the next agent can reopen the same source quickly.
* Prefer direct file URLs when you already know the path; prefer folder URLs when the exact file is still unclear.
* If a GitHub path does not resolve, verify the branch and the exact casing of the repo-relative path.
* Re-check the actual source before claiming a method, property, or helper exists.
