# Settings, Menus, Localization, and Input

## Settings

* User settings live in `Properties/*.settings` with generated designer files beside them.
* Setting pages live under `Kernel/SettingPages/<Area>/` with matching `Views/` and `ViewModels/`.
* Current high-frequency areas include `Program`, `Audio`, `Logs`, `FumenVisualEditor`, and `KeyBinding`.
* Program-level toggles such as MCP enablement and port configuration already flow through `ProgramSetting`.

## Localization

* User-facing strings live in `Properties/Resources.resx` plus localized variants such as `Resources.zh-Hans.resx` and `Resources.ja.resx`.
* XAML localization uses `UI/Markup/TranslateExtension` and sometimes `UI/ValueConverters/LocalizeConverter`.
* If a command or setting label changes, update the resource files, not hard-coded strings in XAML or view models.
* Multi-part localized labels in dialogs and settings pages often use `MultiBinding` with `LocalizeConverter`, so inspect both the XAML binding and the resource keys.

## Menus and Commands

* Menu placement is distributed by feature through `MenuDefinitions.cs` classes.
* Command definitions and handlers usually live near the feature that owns them.
* Conditional menu visibility may use Gemini definitions such as `ExcludeMenuDefinition`.
* Good anchor examples are `Modules/FumenVisualEditor/Commands/OgkrImpl/MenuDefinitions.cs` for a feature menu and `Kernel/Mcp/MenuDefinitions.cs` for a top-level conditional menu.

## Keybindings

* Keybinding definitions live under `Kernel/KeyBinding` and feature-local static definition classes such as `Modules/FumenVisualEditor/KeyBindingDefinitions.cs`.
* Input triggers live under `UI/KeyBinding/Input/`.
* The keybinding system distinguishes layers such as `Global`, `Normal`, and `Batch`.
* `Modules/FumenVisualEditor/Views/FumenVisualEditorView.xaml` shows the common binding pattern through `ActionMessageKeyBinding` declarations bound to `KeyBindingDefinitions`.
* Batch-mode tools also expose keybinding-backed submodes, so keyboard features may span both the setting pages and editor behavior slices.

## Change Guidance

* When adding a user-facing command, wire the command definition/handler, menu placement, resource text, and optional keybinding together.
* When adding a setting, update the `.settings` file, generated designer usage sites, and the relevant setting page view/viewmodel.
* When adding a program-level option, check both `Kernel/SettingPages/Program/` and the runtime usage sites that read `ProgramSetting.Default`.
* Keep localization coverage aligned across the base and localized `.resx` files when possible.
