# Bootstrap, Composition, and Lifetimes

## Startup Modes

* `AppBootstrapper.OnStartup(...)` splits into GUI mode and command-line mode.
* Command-line mode initializes `ISchedulerManager`, delegates to `ICommandExecutor`, and exits with the command result.
* GUI mode initializes exception handling, IPC, scheduler state, program-argument processing, shell display, splash behavior, updater checks, and optional MCP auto-start.

## Composition Rules

* The repo uses MEF composition through Gemini/Caliburn rather than Microsoft.Extensions.DependencyInjection.
* `AppBootstrapper.BindServices(...)` scans `Plugins/*` subdirectories beside the executable with `DirectoryCatalog`.
* All plugin folders under `Plugins/*` can contribute MEF parts through `DirectoryCatalog`.
* Assemblies whose names start with `OngekiFumenEditorPlugins.` are additionally appended to `AssemblySource`, which helps view lookup and other assembly-source-driven UI behavior.
* `SelectAssemblies()` extends the default assembly set with Gemini output and main-window assemblies.

## UI Lifetime and Shell Behavior

* GUI startup calls `DisplayRootViewForAsync<IMainWindow>()`.
* The main window enables drag-and-drop file opening and persists window size and position via `ProgramSetting`.
* `ViewLocator.LocateForModel` is wrapped so missing views fall back to `ViewHelper.CreateView(model)`.
* `WindowTitleHelper`, `CommonStatusBar`, and scheduler initialization are part of the normal startup contract and should not be bypassed casually.

## IPC and Background Behavior

* `InitIPCServer()` maintains single-host style IPC behavior and forwards argument requests back through `IProgramArgProcessManager`.
* `OnExit(...)` stops MCP, disposes audio, terminates the scheduler, and flushes logs in a defined order.

## MCP Startup

* MCP auto-start only happens when `ProgramSetting.Default.EnableMcpServerInGUIMode` is true.
* Startup and shutdown both use thin helper methods that swallow and log failures rather than hard-crashing the editor shell.

## Change Guidance

* Export shared services through MEF and let the existing composition system discover them.
* Keep plugin loading, shell startup, and GUI/CMD branching compatible when adding new global services.
* If a feature depends on app startup order, place it near the scheduler, shell, or MCP startup sections instead of hiding it in random modules.
