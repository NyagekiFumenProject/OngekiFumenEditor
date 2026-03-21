# Selection, Clipboard, And Viewport Operations

Use this page when a script needs to act on the current selection, manipulate the viewport, or copy and paste editor objects.

## Selection APIs

`FumenVisualEditorViewModel` exposes selection helpers directly:

* `editor.SelectObjects`
* `editor.ClearSelection()`
* `editor.AddToSelection(obj)`
* `editor.ReplaceSelection(obj)`
* `editor.TryCancelAllObjectSelecting(...)`
* `editor.NotifyObjectClicked(obj)`
* `editor.DeleteSelection(selection?)`
* `editor.SelectionArea` is the active range-selection state object when the workflow depends on drag selection semantics rather than simple object clicks.

## Behavior Notes

* `SelectObjects` returns selected displayable objects, not only top-level chart objects.
* `NotifyObjectClicked(...)` is the safest way to emulate a single-object selection change because it also keeps property-browser behavior aligned with editor interaction rules.
* `DeleteSelection(...)` is more robust than manually deleting each selected object because it already understands curve control objects and other attached child objects.
* `TryCancelAllObjectSelecting(...)` follows the editor's mutual-exclusion rules and modifier-key behavior.
* `Modules/FumenEditorSelectingObjectViewer/` is the best reference slice when a new feature needs complex selection filtering or selection-derived summaries.

## Viewport APIs

Use these after a mutation when the user expects the editor to focus the result:

* `editor.ScrollTo(TGrid)`
* `editor.ScrollTo(TimeSpan)`
* `editor.ScrollTo(ITimelineObject)`
* `editor.GetCurrentTGrid()`
* `editor.CurrentPlayTime`
* `editor.RecalculateTotalDurationHeight()` is often needed after duration-shaping edits before scrolling to the new result.

## Clipboard Services

Clipboard work is a legacy-script feature because it depends on `IoC.Get<IFumenEditorClipboard>()`, which runtime automation blocks.

The clipboard service supports:

* `CopyObjects(editor, editor.SelectObjects)`
* `PasteObjects(editor, pasteOption, placePoint?)`

Available `PasteOption` values:

* `None`
* `Direct`
* `XGridZeroMirror`
* `SelectedRangeCenterXGridMirror`
* `SelectedRangeCenterTGridMirror`

## Clipboard Caveats

* copy and paste expect design mode, not preview mode
* paste mirrors also flip flick direction where appropriate
* clipboard logic already preserves undo behavior, placement offsets, and many object-specific reconstruction rules
* `DefaultFumenEditorClipboard` is a good reference when a product feature should reuse editor-native copy/paste semantics instead of reimplementing them.

## Example: Select, Copy, And Focus

Legacy script example:

```csharp
using Caliburn.Micro;
using OngekiFumenEditor.Modules.EditorScriptExecutor.Scripts;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using System.Linq;
using static OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.FumenVisualEditorViewModel;

var editor = ScriptArgs.TargetEditor;
if (editor is null)
    return;

var tap = editor.Fumen.Taps.FirstOrDefault();
if (tap is null)
    return;

editor.ReplaceSelection(tap);
await IoC.Get<IFumenEditorClipboard>().CopyObjects(editor, editor.SelectObjects);
await IoC.Get<IFumenEditorClipboard>().PasteObjects(editor, PasteOption.XGridZeroMirror);
editor.ScrollTo(tap);
```

## Runtime Guidance

For runtime automation:

* use selection APIs only if the script is already mutating through the allowed undo pattern
* avoid clipboard services entirely
* prefer object creation or direct model edits over trying to reproduce UI-level gestures
