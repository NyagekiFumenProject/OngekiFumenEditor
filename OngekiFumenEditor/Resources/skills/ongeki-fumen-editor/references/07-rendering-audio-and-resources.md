# Rendering, Audio, and Resources

## Rendering Structure

* Rendering infrastructure lives mainly under `Kernel/Graphics/` and `Modules/FumenVisualEditor/Graphics/`.
* The repo supports multiple render-manager implementations and backends such as OpenGL and Skia.
* `Kernel/Graphics/DefaultRenderManager.cs` selects the active implementation from `ProgramSetting.Default.DefaultRenderManagerImplementName`.
* `Kernel/Graphics/OpenGL/DefaultOpenGLRenderManagerImpl.cs` and `Kernel/Graphics/Skia/DefaultSkiaDrawingManagerImpl.cs` are the main backend entry points.
* Skia backend choice also flows through `ProgramSetting.Default.SkiaRenderBackend`.
* Rendering behavior often depends on user settings, cached resources, and scheduler-driven refresh flows.

## Audio Structure

* Audio services live under `Kernel/Audio/`.
* Editor documents load playback through `IAudioManager` and hold an `IAudioPlayer`.
* `IAudioManager.LoadAudioAsync(...)` is the normal entry for document and tool-window audio loading.
* `Modules/AudioPlayerToolViewer/` is a good reference lane when a rendering or editor change also needs synchronized playback behavior.
* Timing and scrolling behavior often depend on the audio lane, so rendering-only edits can still have audio implications.

## Resource Packaging

* `Resources/` contains icons, editor textures, sounds, script templates, and other packaged files.
* `OngekiFumenEditor.csproj` explicitly classifies many resources as `Resource`, `EmbeddedResource`, or `None Update ... CopyToOutputDirectory`.
* Debug and release builds do not package everything the same way. `SoundTouch.dll` is copied in Debug and embedded for release handling.
* Release builds also run `Dependencies/Topten.nvpatch.exe` in a post-build step.
* Do not assume SDK defaults are enough for new runtime assets.

## Paths and Load Styles

* Some assets are loaded through pack URIs.
* Some assets are loaded from copied files in the output directory.
* Some assets are embedded and never appear as loose files after build.
* Common icon usage follows `pack://application:,,,/OngekiFumenEditor;component/Resources/...`.
* `.resx` files are part of the embedded-resource contract and must stay consistent with localized variants.

## Change Guidance

* When adding a new asset, decide first whether the runtime expects pack-resource access, embedded access, or filesystem access.
* When changing rendering, trace the path from setting -> render manager -> drawing target -> asset lookup.
* If a change only works in Debug or only in Release, inspect the project file's resource metadata and post-build targets before blaming renderer code.
* Preserve cache and scheduler behavior for performance-sensitive drawing lanes.
